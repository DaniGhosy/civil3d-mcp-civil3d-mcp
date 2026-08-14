"""Shared OCR word/line extraction, used by both legend.extract_legend_templates (needs lines to
find where each symbol cell is) and ocr.ocr_extract_labels (needs lines as the useful unit of a
"label"). Groups by Tesseract's own (block_num, par_num, line_num) instead of reimplementing
proximity clustering — Tesseract already solved that problem.
"""
import pytesseract


def run_ocr_words(image, min_confidence: int = 0) -> list[dict]:
    data = pytesseract.image_to_data(image, output_type=pytesseract.Output.DICT)

    words = []
    for i in range(len(data["text"])):
        text = data["text"][i].strip()
        conf = int(data["conf"][i]) if data["conf"][i] not in ("-1", "") else -1
        if not text or conf < min_confidence:
            continue

        words.append(
            {
                "text": text,
                "left": data["left"][i],
                "top": data["top"][i],
                "width": data["width"][i],
                "height": data["height"][i],
                "confidence": conf,
                "blockNum": data["block_num"][i],
                "parNum": data["par_num"][i],
                "lineNum": data["line_num"][i],
            }
        )

    return words


def group_words_into_lines(words: list[dict]) -> list[dict]:
    lines_by_key: dict[tuple, list[dict]] = {}
    for word in words:
        key = (word["blockNum"], word["parNum"], word["lineNum"])
        lines_by_key.setdefault(key, []).append(word)

    lines = []
    for line_words in lines_by_key.values():
        line_words.sort(key=lambda w: w["left"])
        left = min(w["left"] for w in line_words)
        top = min(w["top"] for w in line_words)
        right = max(w["left"] + w["width"] for w in line_words)
        bottom = max(w["top"] + w["height"] for w in line_words)
        avg_confidence = sum(w["confidence"] for w in line_words) / len(line_words)

        lines.append(
            {
                "text": " ".join(w["text"] for w in line_words),
                "left": left,
                "top": top,
                "width": right - left,
                "height": bottom - top,
                "confidence": avg_confidence,
                "words": line_words,
            }
        )

    lines.sort(key=lambda l: l["top"])
    return lines
