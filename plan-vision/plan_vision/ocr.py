"""ocr_extract_labels — reads text labels off a scanned image. Same conceptual shape as
civil3d_label.extract_text_entities, but in pixel coordinates instead of drawing coordinates.
"""
import cv2

from .text_layout import group_words_into_lines, run_ocr_words


def ocr_extract_labels(image_path: str, min_confidence: int = 0) -> dict:
    image = cv2.imread(image_path, cv2.IMREAD_COLOR)
    if image is None:
        raise ValueError(f"Could not read image at '{image_path}'.")

    words = run_ocr_words(image, min_confidence)
    lines = group_words_into_lines(words)

    labels = [
        {
            "text": line["text"],
            "x": line["left"],
            "y": line["top"],
            "width": line["width"],
            "height": line["height"],
            "confidence": line["confidence"],
        }
        for line in lines
    ]

    return {"imagePath": image_path, "minConfidence": min_confidence, "labels": labels}
