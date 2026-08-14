"""extract_legend_templates — the command the PDF catalog didn't have: instead of training one
symbol template at a time by hand, read an already-cropped legend image once and build the whole
template library from it automatically (train_symbol_template stays available as a manual
fallback/correction for rows this can't resolve cleanly).

Assumes the standard legend convention: symbol on the left, description text on the right, one
row per symbol. For each OCR'd text line, the "symbol cell" is the strip from the left edge of the
legend image to the start of that line's text; contours found inside that strip are combined into
one bounding box and cropped out as the template, named after the line's OCR text.
"""
import cv2

from .templates import upsert_template
from .text_layout import group_words_into_lines, run_ocr_words

MARGIN_PX = 4
MIN_CONTOUR_AREA_RATIO = 0.01  # relative to the symbol cell's area


def _find_symbol_bounds(cell_gray) -> tuple[int, int, int, int] | None:
    _, binary = cv2.threshold(cell_gray, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    contours, _ = cv2.findContours(binary, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    cell_area = cell_gray.shape[0] * cell_gray.shape[1]
    min_area = cell_area * MIN_CONTOUR_AREA_RATIO

    significant = [c for c in contours if cv2.contourArea(c) >= min_area]
    if not significant:
        return None

    xs, ys, xe, ye = None, None, None, None
    for contour in significant:
        x, y, w, h = cv2.boundingRect(contour)
        xs = x if xs is None else min(xs, x)
        ys = y if ys is None else min(ys, y)
        xe = x + w if xe is None else max(xe, x + w)
        ye = y + h if ye is None else max(ye, y + h)

    return xs, ys, xe - xs, ye - ys


def extract_legend_templates(legend_image_path: str, library_path: str, min_confidence: int = 0) -> dict:
    image = cv2.imread(legend_image_path, cv2.IMREAD_COLOR)
    if image is None:
        raise ValueError(f"Could not read image at '{legend_image_path}'.")

    image_height, image_width = image.shape[:2]
    words = run_ocr_words(image, min_confidence)
    lines = group_words_into_lines(words)

    created_templates = []
    unresolved_rows = []

    for line in lines:
        cell_left = 0
        cell_top = max(0, line["top"] - MARGIN_PX)
        cell_right = max(0, line["left"] - MARGIN_PX)
        cell_bottom = min(image_height, line["top"] + line["height"] + MARGIN_PX)

        if cell_right <= cell_left or cell_bottom <= cell_top:
            unresolved_rows.append({"text": line["text"], "reason": "No space to the left of the text for a symbol cell."})
            continue

        cell_bgr = image[cell_top:cell_bottom, cell_left:cell_right]
        cell_gray = cv2.cvtColor(cell_bgr, cv2.COLOR_BGR2GRAY)

        bounds = _find_symbol_bounds(cell_gray)
        if bounds is None:
            unresolved_rows.append({"text": line["text"], "reason": "No contour large enough found in the symbol cell."})
            continue

        x, y, w, h = bounds
        symbol_crop = cell_bgr[y : y + h, x : x + w]
        if symbol_crop.size == 0:
            unresolved_rows.append({"text": line["text"], "reason": "Isolated symbol crop was empty."})
            continue

        name = line["text"].strip()
        template_path = upsert_template(library_path, name, symbol_crop)
        created_templates.append(
            {"name": name, "templatePath": template_path, "ocrConfidence": line["confidence"]}
        )

    return {
        "legendImagePath": legend_image_path,
        "libraryPath": library_path,
        "templates": created_templates,
        "unresolvedRows": unresolved_rows,
    }
