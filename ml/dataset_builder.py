#!/usr/bin/env python3
"""Terminal dataset builder for the Civil3D symbol-recognition ML project.

Standalone, stdlib + candidates.py only. Combines raw JSON exported from three MCP actions --
civil3d_shape_detection.group_entities_by_proximity, civil3d_object.get_entity_bounds (one call
per handle, results collected into a single array), and civil3d_label.extract_text_entities --
into labeled JSONL rows, one candidate symbol per line. Never talks to Civil3D or the plugin; pure
post-processing on already-extracted JSON, per the architecture decision in the original planning
doc (planteamiento_ml_simbolos_civil3d.pdf, sec. 3). For a click-based alternative to this
prompt-per-candidate CLI, see review_app/ (Flask UI, same schema, same candidates.py).
"""

import argparse
import sys
from pathlib import Path

import candidates as c


def print_candidate(candidate_id, layer, features, missing):
    print(f"\n--- {candidate_id} (capa {layer}) ---")
    print(f"  entidades: {features['entity_count']}  tipos: {features['type_counts']}")
    print(
        f"  bbox: {features['bbox_w']!r} x {features['bbox_h']!r}  "
        f"aspect_ratio: {features['aspect_ratio']!r}"
    )
    print(f"  has_hatch: {features['has_hatch']}")
    text = (features["nearest_text"] or "")[:60]
    print(f"  texto mas cercano: {text!r} (dist {features['nearest_text_dist']!r})")
    if missing:
        print(
            f"  aviso: sin bounds para handles {missing} -- bbox/conteo puede estar incompleto",
            file=sys.stderr,
        )


def prompt_label(confirmed_by: str):
    category = input("Categoria (Enter=omitir, n=negativo/ruido): ").strip()
    subtype = None
    if category and category.lower() not in {"n", "negativo", "ruido"}:
        subtype = input("Subtipo (Enter=ninguno): ").strip() or None
    return c.make_label(category, subtype, confirmed_by)


def main():
    parser = argparse.ArgumentParser(
        description=(
            "Arma dataset.jsonl etiquetado a partir de JSON crudo de civil3d_shape_detection, "
            "civil3d_object y civil3d_label, exportado de una sesion MCP en vivo. No toca Civil3D."
        )
    )
    parser.add_argument("--source-drawing", required=True, help="Nombre del archivo .dwg de origen")
    parser.add_argument("--groups", required=True, type=Path, help="JSON de group_entities_by_proximity")
    parser.add_argument(
        "--bounds", required=True, type=Path, help="JSON (array) de get_entity_bounds, uno por handle"
    )
    parser.add_argument("--texts", required=True, type=Path, help="JSON de extract_text_entities")
    parser.add_argument(
        "--output", type=Path, default=Path("ml/dataset.jsonl"), help="Archivo JSONL de salida (se appendea)"
    )
    parser.add_argument("--confirmed-by", default="daniel", help="Quien confirma las etiquetas (default: daniel)")
    parser.add_argument(
        "--dry-run", action="store_true", help="Solo imprime las features, no pregunta ni escribe nada"
    )
    args = parser.parse_args()

    groups = c.load_groups(args.groups)
    bounds_by_handle = c.index_bounds_by_handle(c.load_bounds(args.bounds))
    texts = c.load_texts(args.texts)
    existing_ids = c.load_existing_ids(args.output)

    saved = 0
    skipped_existing = 0
    for group in groups:
        candidate_id = c.make_id(args.source_drawing, group["handles"])
        if candidate_id in existing_ids:
            skipped_existing += 1
            continue

        features, missing = c.compute_group_features(group, bounds_by_handle, texts)
        print_candidate(candidate_id, group.get("layer"), features, missing)

        if args.dry_run:
            continue

        label = prompt_label(args.confirmed_by)
        if label is None:
            print("  omitido")
            continue

        candidate = {
            "id": candidate_id,
            "source_drawing": args.source_drawing,
            "entity_handles": group["handles"],
            "layer": group.get("layer"),
            "features": features,
        }
        c.append_row(args.output, c.build_row(candidate, label))
        saved += 1
        print(f"  guardado. total en esta sesion: {saved}")

    if skipped_existing:
        print(f"\n{skipped_existing} candidato(s) ya estaban en {args.output}, se omitieron.")
    if not args.dry_run:
        print(f"{saved} fila(s) nueva(s) guardadas en {args.output}.")


if __name__ == "__main__":
    main()
