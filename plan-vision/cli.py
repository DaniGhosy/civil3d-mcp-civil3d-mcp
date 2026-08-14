#!/usr/bin/env python3
"""Entry point invoked by src/utils/PythonBridge.ts (one Node child_process per call, same shape
as the C# plugin's JSON-RPC dispatch but over a subprocess instead of TCP). Usage:

    echo '<json args>' | python cli.py <command>

Prints one JSON object to stdout on success (exit 0). On failure, prints an error message to
stderr and exits non-zero — never a raw Python traceback, so the Node side gets something it can
surface directly to the user.
"""
import json
import sys

from plan_vision.detect import detect_symbols_cv
from plan_vision.legend import extract_legend_templates
from plan_vision.ocr import ocr_extract_labels
from plan_vision.rasterize import rasterize_pdf_page
from plan_vision.templates import train_symbol_template


def _cmd_rasterize_pdf_page(args: dict) -> dict:
    return rasterize_pdf_page(
        pdf_path=args["pdfPath"],
        page=args["page"],
        output_path=args["outputPath"],
        dpi=args.get("dpi", 300),
    )


def _cmd_extract_legend_templates(args: dict) -> dict:
    return extract_legend_templates(
        legend_image_path=args["legendImagePath"],
        library_path=args["libraryPath"],
        min_confidence=args.get("minConfidence", 0),
    )


def _cmd_train_symbol_template(args: dict) -> dict:
    return train_symbol_template(
        image_path=args["imagePath"],
        name=args["name"],
        library_path=args["libraryPath"],
        region=args.get("region"),
    )


def _cmd_detect_symbols_cv(args: dict) -> dict:
    return detect_symbols_cv(
        image_path=args["imagePath"],
        library_path=args["libraryPath"],
        match_threshold=args.get("matchThreshold", 0.75),
        scales=args.get("scales"),
        rotations=args.get("rotations"),
    )


def _cmd_ocr_extract_labels(args: dict) -> dict:
    return ocr_extract_labels(
        image_path=args["imagePath"],
        min_confidence=args.get("minConfidence", 0),
    )


COMMANDS = {
    "rasterize_pdf_page": _cmd_rasterize_pdf_page,
    "extract_legend_templates": _cmd_extract_legend_templates,
    "train_symbol_template": _cmd_train_symbol_template,
    "detect_symbols_cv": _cmd_detect_symbols_cv,
    "ocr_extract_labels": _cmd_ocr_extract_labels,
}


def main() -> int:
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        print(f"Usage: cli.py <{'|'.join(COMMANDS)}>  (JSON args on stdin)", file=sys.stderr)
        return 1

    command = sys.argv[1]
    try:
        raw_args = sys.stdin.read()
        args = json.loads(raw_args) if raw_args.strip() else {}
        result = COMMANDS[command](args)
        print(json.dumps(result))
        return 0
    except KeyError as exc:
        print(f"Missing required parameter: {exc}", file=sys.stderr)
        return 1
    except Exception as exc:  # noqa: BLE001 - top-level dispatcher, must not leak a raw traceback
        print(f"{type(exc).__name__}: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
