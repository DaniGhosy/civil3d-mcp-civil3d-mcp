"""detect_symbols_cv — searches a plan image for every template in a library, at multiple scales
and rotations, scoring with normalized cross-correlation (cv2.TM_CCOEFF_NORMED) and collapsing
overlapping hits per-symbol with non-maximum suppression. This is the "maximize precision within
classical CV" piece: template matching alone tolerates neither scale nor rotation, so both are
swept explicitly (rotation defaults to the 0/90/180/270 that hand-drawn CAD symbols actually use).
"""
import cv2

from .templates import load_manifest

DEFAULT_SCALES = [0.85, 0.9, 0.95, 1.0, 1.05, 1.1, 1.15]
DEFAULT_ROTATIONS = [0, 90, 180, 270]
DEFAULT_MATCH_THRESHOLD = 0.75
NMS_IOU_THRESHOLD = 0.3
MAX_PEAKS_PER_PASS = 200  # safety cap per template/scale/rotation pass — see _find_peaks
DARK_RATIO_TOLERANCE = 0.25  # see _dark_ratio
UNIFORM_TEMPLATE_MARGIN = 0.02  # see detect_symbols_cv's near-uniform-template guard


def _rotate_image(image, angle_degrees: float):
    if angle_degrees % 360 == 0:
        return image
    if angle_degrees % 360 == 90:
        return cv2.rotate(image, cv2.ROTATE_90_CLOCKWISE)
    if angle_degrees % 360 == 180:
        return cv2.rotate(image, cv2.ROTATE_180)
    if angle_degrees % 360 == 270:
        return cv2.rotate(image, cv2.ROTATE_90_COUNTERCLOCKWISE)

    h, w = image.shape[:2]
    center = (w / 2, h / 2)
    matrix = cv2.getRotationMatrix2D(center, angle_degrees, 1.0)

    cos = abs(matrix[0, 0])
    sin = abs(matrix[0, 1])
    new_w = int(h * sin + w * cos)
    new_h = int(h * cos + w * sin)
    matrix[0, 2] += (new_w / 2) - center[0]
    matrix[1, 2] += (new_h / 2) - center[1]

    return cv2.warpAffine(image, matrix, (new_w, new_h), borderValue=(255, 255, 255))


def _find_peaks(result, template_w: int, template_h: int, threshold: float) -> list[tuple[int, int, float]]:
    """Iteratively picks the strongest remaining match and blanks out the template-sized region
    around it before looking for the next one. cv2.matchTemplate gives a WIDE band of high
    correlation around each real hit (worse for flat/solid symbols) — collecting every pixel over
    threshold via np.where() and only cleaning up afterwards with NMS is what actually hung: tens
    of thousands of raw candidates feeding an O(n^2) Python NMS. This is the standard fix for
    "find N instances of a template" and stays O(matches), not O(pixels-over-threshold^2).
    """
    working = result.copy()
    peaks: list[tuple[int, int, float]] = []

    for _ in range(MAX_PEAKS_PER_PASS):
        _, max_val, _, max_loc = cv2.minMaxLoc(working)
        if max_val < threshold:
            break

        x, y = max_loc
        peaks.append((x, y, float(max_val)))

        half_w, half_h = max(1, template_w // 2), max(1, template_h // 2)
        x0, y0 = max(0, x - half_w), max(0, y - half_h)
        x1, y1 = min(working.shape[1], x + half_w), min(working.shape[0], y + half_h)
        working[y0:y1, x0:x1] = -1.0

    return peaks


def _dark_ratio(gray_region) -> float:
    """Fraction of a grayscale region that's "ink" (foreground) after Otsu thresholding. Used to
    reject matches whose confidence score is degenerate: TM_CCOEFF_NORMED divides by local
    variance, so a template placed over a completely flat/blank patch of the plan can score a
    false 1.0 confidence — confirmed against a synthetic all-white test image, where every sliding
    window position "matched" a solid template. Comparing dark-pixel ratio against the template's
    own ratio catches that: a real match's underlying pixels should look roughly as "inky" as the
    template that supposedly matched it.
    """
    if gray_region.size == 0:
        return 0.0
    _, binary = cv2.threshold(gray_region, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    return cv2.countNonZero(binary) / binary.size


def _iou(a: dict, b: dict) -> float:
    ax2, ay2 = a["x"] + a["width"], a["y"] + a["height"]
    bx2, by2 = b["x"] + b["width"], b["y"] + b["height"]

    ix1, iy1 = max(a["x"], b["x"]), max(a["y"], b["y"])
    ix2, iy2 = min(ax2, bx2), min(ay2, by2)
    intersection = max(0, ix2 - ix1) * max(0, iy2 - iy1)
    if intersection == 0:
        return 0.0

    union = a["width"] * a["height"] + b["width"] * b["height"] - intersection
    return intersection / union if union > 0 else 0.0


def _non_max_suppression(detections: list[dict]) -> list[dict]:
    detections = sorted(detections, key=lambda d: d["confidence"], reverse=True)
    kept: list[dict] = []
    for detection in detections:
        if not any(_iou(detection, k) > NMS_IOU_THRESHOLD for k in kept):
            kept.append(detection)
    return kept


def detect_symbols_cv(
    image_path: str,
    library_path: str,
    match_threshold: float = DEFAULT_MATCH_THRESHOLD,
    scales: list[float] | None = None,
    rotations: list[float] | None = None,
) -> dict:
    image = cv2.imread(image_path, cv2.IMREAD_COLOR)
    if image is None:
        raise ValueError(f"Could not read image at '{image_path}'.")
    image_gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    image_h, image_w = image_gray.shape[:2]

    scales = scales or DEFAULT_SCALES
    rotations = rotations or DEFAULT_ROTATIONS

    manifest = load_manifest(library_path)
    all_detections: list[dict] = []
    skipped_templates: list[dict] = []

    for entry in manifest.get("templates", []):
        name = entry["name"]
        template = cv2.imread(entry["templatePath"], cv2.IMREAD_COLOR)
        if template is None:
            skipped_templates.append({"name": name, "reason": f"Could not read '{entry['templatePath']}'."})
            continue
        template_gray = cv2.cvtColor(template, cv2.COLOR_BGR2GRAY)

        # TM_CCOEFF_NORMED divides by the template's own local variance — a template with no
        # internal contrast (a flat crop with no visible border/edge) makes that division
        # degenerate and every position in the image "matches" with a false ~1.0 confidence.
        # Confirmed against a solid-fill synthetic template during development; a real symbol
        # crop should always have SOME internal edge, so this should only trip on bad crops.
        uniform_ratio = _dark_ratio(template_gray)
        if uniform_ratio < UNIFORM_TEMPLATE_MARGIN or uniform_ratio > 1 - UNIFORM_TEMPLATE_MARGIN:
            skipped_templates.append(
                {
                    "name": name,
                    "reason": "Template has no internal contrast (a solid/flat crop) — template "
                    "matching is mathematically degenerate on it. Re-crop the symbol so its edges "
                    "are visible inside the template.",
                }
            )
            continue

        name_detections: list[dict] = []
        for rotation in rotations:
            rotated = _rotate_image(template_gray, rotation)
            base_h, base_w = rotated.shape[:2]

            for scale in scales:
                scaled_w, scaled_h = int(base_w * scale), int(base_h * scale)
                if scaled_w < 5 or scaled_h < 5 or scaled_w > image_w or scaled_h > image_h:
                    continue

                resized = cv2.resize(rotated, (scaled_w, scaled_h), interpolation=cv2.INTER_AREA)
                result = cv2.matchTemplate(image_gray, resized, cv2.TM_CCOEFF_NORMED)
                template_dark_ratio = _dark_ratio(resized)

                for x, y, confidence in _find_peaks(result, scaled_w, scaled_h, match_threshold):
                    region = image_gray[y : y + scaled_h, x : x + scaled_w]
                    if abs(_dark_ratio(region) - template_dark_ratio) > DARK_RATIO_TOLERANCE:
                        continue  # degenerate match over a flat/blank patch — see _dark_ratio

                    name_detections.append(
                        {
                            "name": name,
                            "x": x,
                            "y": y,
                            "width": scaled_w,
                            "height": scaled_h,
                            "confidence": confidence,
                            "scale": scale,
                            "rotation": rotation,
                        }
                    )

        all_detections.extend(_non_max_suppression(name_detections))

    return {
        "imagePath": image_path,
        "libraryPath": library_path,
        "matchThreshold": match_threshold,
        "detections": all_detections,
        "skippedTemplates": skipped_templates,
    }
