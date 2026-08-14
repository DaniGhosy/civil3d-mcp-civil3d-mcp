"""Local template library: a directory of named symbol crops (PNG) plus a manifest.json
mapping name -> templatePath. Shared by legend.extract_legend_templates (bulk, from a legend
image) and train_symbol_template (manual upsert of one template) so both feed the same library
that detect.detect_symbols_cv reads from.
"""
import json
import os
import re

import cv2

MANIFEST_FILENAME = "manifest.json"


def _manifest_path(library_path: str) -> str:
    return os.path.join(library_path, MANIFEST_FILENAME)


def load_manifest(library_path: str) -> dict:
    path = _manifest_path(library_path)
    if not os.path.exists(path):
        return {"templates": []}
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def save_manifest(library_path: str, manifest: dict) -> None:
    os.makedirs(library_path, exist_ok=True)
    with open(_manifest_path(library_path), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)


def _sanitize_filename(name: str) -> str:
    safe = re.sub(r"[^A-Za-z0-9_\-]+", "_", name.strip())
    return safe or "symbol"


def upsert_template(library_path: str, name: str, image) -> str:
    """Writes `image` (a numpy/cv2 array) as the template for `name`, updating the manifest.
    Returns the template's file path."""
    os.makedirs(library_path, exist_ok=True)
    filename = f"{_sanitize_filename(name)}.png"
    template_path = os.path.join(library_path, filename)
    cv2.imwrite(template_path, image)

    manifest = load_manifest(library_path)
    templates = [t for t in manifest.get("templates", []) if t["name"] != name]
    templates.append({"name": name, "templatePath": template_path})
    manifest["templates"] = templates
    save_manifest(library_path, manifest)

    return template_path


def train_symbol_template(image_path: str, name: str, library_path: str, region: dict | None = None) -> dict:
    image = cv2.imread(image_path, cv2.IMREAD_COLOR)
    if image is None:
        raise ValueError(f"Could not read image at '{image_path}'.")

    if region:
        x, y, w, h = int(region["x"]), int(region["y"]), int(region["width"]), int(region["height"])
        crop = image[y : y + h, x : x + w]
        if crop.size == 0:
            raise ValueError(f"Region {region} is empty or out of bounds for '{image_path}'.")
    else:
        crop = image

    template_path = upsert_template(library_path, name, crop)
    manifest = load_manifest(library_path)
    return {
        "success": True,
        "name": name,
        "templatePath": template_path,
        "templateCount": len(manifest.get("templates", [])),
    }
