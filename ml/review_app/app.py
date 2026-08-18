#!/usr/bin/env python3
"""Local Flask review tool for labeling Civil3D symbol candidates.

Click-based alternative to dataset_builder.py's prompt-per-candidate terminal loop -- same
dataset.jsonl schema, same candidates.py feature/shape computation, just a browser UI instead
of stdin. Standalone: never talks to Civil3D or the plugin, only reads the JSON already
exported from a live MCP session (see CLAUDE.md, ml/ section).

Run from the repo root:
    python ml/review_app/app.py --source-drawing "..." --groups ... --bounds ... --texts ...
"""

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import candidates as c

from flask import Flask, jsonify, render_template, request

app = Flask(__name__)
STATE: dict = {}


@app.route("/")
def index():
    return render_template(
        "index.html",
        source_drawing=STATE["source_drawing"],
        output_path=str(STATE["output"]),
        candidates=STATE["candidates"],
        labeled_ids=sorted(STATE["labeled_ids"]),
    )


@app.route("/api/label", methods=["POST"])
def label():
    data = request.get_json(force=True, silent=True) or {}
    cid = data.get("id")
    candidate = STATE["by_id"].get(cid)
    if not candidate:
        return jsonify({"error": "candidato desconocido"}), 404
    if cid in STATE["labeled_ids"]:
        return jsonify({"error": "ese candidato ya estaba guardado"}), 409

    lbl = c.make_label(data.get("category"), data.get("subtype"), STATE["confirmed_by"])
    if lbl is None:
        return jsonify({"error": "falta la categoria"}), 400

    row = c.build_row(candidate, lbl)
    c.append_row(STATE["output"], row)
    STATE["labeled_ids"].add(cid)
    return jsonify({"ok": True, "label": lbl})


def main():
    parser = argparse.ArgumentParser(
        description="Revisor visual (Flask) para etiquetar candidatos de simbolos, alternativa a dataset_builder.py."
    )
    parser.add_argument("--source-drawing", required=True, help="Nombre del archivo .dwg de origen")
    parser.add_argument("--groups", required=True, type=Path, help="JSON de group_entities_by_proximity")
    parser.add_argument("--bounds", required=True, type=Path, help="JSON (array) de get_entity_bounds")
    parser.add_argument("--texts", required=True, type=Path, help="JSON de extract_text_entities")
    parser.add_argument("--output", type=Path, default=Path("ml/dataset.jsonl"), help="JSONL de salida (se appendea)")
    parser.add_argument("--confirmed-by", default="daniel")
    parser.add_argument("--port", type=int, default=5000)
    args = parser.parse_args()

    groups = c.load_groups(args.groups)
    bounds_by_handle = c.index_bounds_by_handle(c.load_bounds(args.bounds))
    texts = c.load_texts(args.texts)
    candidates = c.build_candidates(args.source_drawing, groups, bounds_by_handle, texts)

    STATE["source_drawing"] = args.source_drawing
    STATE["candidates"] = candidates
    STATE["by_id"] = {cand["id"]: cand for cand in candidates}
    STATE["labeled_ids"] = c.load_existing_ids(args.output)
    STATE["output"] = args.output
    STATE["confirmed_by"] = args.confirmed_by

    app.run(debug=False, port=args.port)


if __name__ == "__main__":
    main()
