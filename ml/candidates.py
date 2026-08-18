"""Shared candidate-extraction logic for the Civil3D symbol-recognition dataset tools.

Used by both dataset_builder.py (terminal) and review_app/app.py (Flask UI) so the two
entry points never compute features or build dataset rows differently. Pure post-processing
on already-extracted MCP JSON -- no Civil3D/plugin access here, see CLAUDE.md.
"""

import json
import re
import sys
from collections import Counter
from pathlib import Path

KNOWN_TYPES = ["Line", "Circle", "Arc", "Hatch", "Polyline"]


def load_json(path: Path):
    try:
        with path.open("r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        sys.exit(f"error: no se pudo leer {path}: {exc}")


def as_list(data, wrapper_key: str):
    if isinstance(data, list):
        return data
    if isinstance(data, dict) and wrapper_key in data:
        return data[wrapper_key]
    sys.exit(f"error: formato inesperado, se esperaba una lista o un objeto con '{wrapper_key}'")


def load_groups(path: Path):
    return as_list(load_json(path), "groups")


def load_bounds(path: Path):
    return as_list(load_json(path), "bounds")


def load_texts(path: Path):
    return as_list(load_json(path), "entities")


def index_bounds_by_handle(bounds_list):
    return {b["handle"]: b for b in bounds_list}


def slugify(text: str) -> str:
    return re.sub(r"[^a-z0-9]+", "", text.lower())


def make_id(source_drawing: str, handles) -> str:
    slug = slugify(Path(source_drawing).stem)
    return f"{slug}-{'-'.join(h.lower() for h in handles)}"


def union_bbox(bounds_items):
    xs_min = [b["minPoint"]["x"] for b in bounds_items]
    xs_max = [b["maxPoint"]["x"] for b in bounds_items]
    ys_min = [b["minPoint"]["y"] for b in bounds_items]
    ys_max = [b["maxPoint"]["y"] for b in bounds_items]
    bbox_w = max(xs_max) - min(xs_min)
    bbox_h = max(ys_max) - min(ys_min)
    aspect_ratio = bbox_w / bbox_h if bbox_h else None
    return bbox_w, bbox_h, aspect_ratio


def nearest_text(center, texts):
    if not texts:
        return None, None
    best_text = None
    best_dist = None
    for t in texts:
        pos = t["position"]
        dist = ((pos["x"] - center["x"]) ** 2 + (pos["y"] - center["y"]) ** 2) ** 0.5
        if best_dist is None or dist < best_dist:
            best_dist = dist
            best_text = t.get("text")
    return best_text, best_dist


def compute_group_features(group, bounds_by_handle, texts):
    handles = group["handles"]
    present = [bounds_by_handle[h] for h in handles if h in bounds_by_handle]
    missing = [h for h in handles if h not in bounds_by_handle]

    type_counts = Counter(b["entityType"] for b in present if b.get("entityType"))
    for t in KNOWN_TYPES:
        type_counts.setdefault(t, 0)

    if present:
        bbox_w, bbox_h, aspect_ratio = union_bbox(present)
    else:
        bbox_w = bbox_h = aspect_ratio = None

    has_hatch = type_counts.get("Hatch", 0) > 0
    text, dist = nearest_text(group["center"], texts)

    features = {
        "entity_count": len(handles),
        "type_counts": dict(type_counts),
        "bbox_w": bbox_w,
        "bbox_h": bbox_h,
        "aspect_ratio": aspect_ratio,
        "has_hatch": has_hatch,
        "nearest_text": text,
        "nearest_text_dist": dist,
    }
    return features, missing


def shape_descriptors(handles, bounds_by_handle):
    """Per-handle drawable shape for visualizing a candidate: exact for Line/Circle (their
    GeometricExtents ARE their true geometry), an approximate bounding box for anything
    else (Arc/Hatch/Polyline extents don't reconstruct the real outline)."""
    shapes = []
    for h in handles:
        b = bounds_by_handle.get(h)
        if not b:
            continue
        mn, mx = b["minPoint"], b["maxPoint"]
        etype = b.get("entityType")
        if etype == "Line":
            shapes.append({"type": "line", "x1": mn["x"], "y1": mn["y"], "x2": mx["x"], "y2": mx["y"]})
        elif etype == "Circle":
            cx, cy = (mn["x"] + mx["x"]) / 2, (mn["y"] + mx["y"]) / 2
            r = (mx["x"] - mn["x"]) / 2
            shapes.append({"type": "circle", "cx": cx, "cy": cy, "r": r})
        else:
            shapes.append({"type": "box", "x1": mn["x"], "y1": mn["y"], "x2": mx["x"], "y2": mx["y"]})
    return shapes


def merge_groups_near_texts(sub_groups, texts, text_pattern, radius):
    """Reassign small proximity sub-groups into one candidate per matching text label.

    For composite symbols whose parts are too spread out for entity-to-entity proximity
    clustering to find in one shot (e.g. a column = outline + hatch fill + corner marks,
    each with its own far-apart center) but where every real instance has a text label
    right next to it -- group by "nearest matching label within radius" instead of by
    inter-entity distance. Sub-groups with no matching label in range come back separately
    as unclaimed (noise, or a real symbol with no visible label).
    """
    pattern = re.compile(text_pattern)
    labels = [t for t in texts if t.get("text") and pattern.match(t["text"])]

    def dist(a, b):
        return ((a["x"] - b["x"]) ** 2 + (a["y"] - b["y"]) ** 2) ** 0.5

    claimed = {}
    unclaimed = []
    for sg in sub_groups:
        best_label, best_dist = None, None
        for lbl in labels:
            d = dist(sg["center"], lbl["position"])
            if d <= radius and (best_dist is None or d < best_dist):
                best_dist, best_label = d, lbl
        if best_label is None:
            unclaimed.append(sg)
        else:
            key = (best_label["text"], best_label["position"]["x"], best_label["position"]["y"])
            claimed.setdefault(key, {"label": best_label, "subgroups": []})
            claimed[key]["subgroups"].append(sg)

    merged = []
    for bucket in claimed.values():
        subgroups = bucket["subgroups"]
        handles = [h for sg in subgroups for h in sg["handles"]]
        xs = [sg["center"]["x"] for sg in subgroups]
        ys = [sg["center"]["y"] for sg in subgroups]
        spread = max(dist(sg["center"], {"x": sum(xs) / len(xs), "y": sum(ys) / len(ys)}) for sg in subgroups)
        merged.append({
            "handles": handles,
            "layer": subgroups[0]["layer"],
            "center": {"x": sum(xs) / len(xs), "y": sum(ys) / len(ys)},
            "anchor_text": bucket["label"]["text"],
            "subgroup_count": len(subgroups),
            "spread": spread,
        })
    return merged, unclaimed


def build_candidates(source_drawing, groups, bounds_by_handle, texts):
    """Compute id/features/shapes for every group. Pure, no I/O."""
    candidates = []
    for group in groups:
        cid = make_id(source_drawing, group["handles"])
        features, missing = compute_group_features(group, bounds_by_handle, texts)
        shapes = shape_descriptors(group["handles"], bounds_by_handle)
        candidates.append({
            "id": cid,
            "source_drawing": source_drawing,
            "entity_handles": group["handles"],
            "layer": group.get("layer"),
            "features": features,
            "missing_bounds": missing,
            "shapes": shapes,
        })
    return candidates


def load_existing_ids(output_path: Path):
    if not output_path.exists():
        return set()
    ids = set()
    with output_path.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                ids.add(json.loads(line)["id"])
    return ids


def make_label(category, subtype, confirmed_by):
    if not category:
        return None
    if category.strip().lower() in {"n", "negativo", "ruido"}:
        return {
            "category": "ninguno",
            "subtype": None,
            "confirmed_by": confirmed_by,
            "confidence_source": "manual",
        }
    return {
        "category": category.strip(),
        "subtype": (subtype or "").strip() or None,
        "confirmed_by": confirmed_by,
        "confidence_source": "manual",
    }


def build_row(candidate, label):
    return {
        "id": candidate["id"],
        "source_drawing": candidate["source_drawing"],
        "entity_handles": candidate["entity_handles"],
        "layer": candidate["layer"],
        "features": candidate["features"],
        "label": label,
    }


def append_row(output_path: Path, row):
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("a", encoding="utf-8") as f:
        f.write(json.dumps(row, ensure_ascii=False) + "\n")
        f.flush()
