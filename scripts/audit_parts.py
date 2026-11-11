"""Audit the display fields of Core part definitions in manifest load order."""
import argparse
import hashlib
import json
from pathlib import Path
import tomllib
from xml.etree import ElementTree as ET


def read_rows(core, assets):
    rows = {}
    hashes = {}
    for filename in assets:
        if not filename.endswith(".xml"):
            continue
        path = core / filename
        hashes[filename] = hashlib.sha256(path.read_bytes()).hexdigest()
        for node in ET.parse(path).getroot():
            if node.tag not in ("Part", "SubPart"):
                continue
            key = (node.tag, node.attrib["Id"])
            operation = node.get("Modify")
            if operation in ("Remove", "RemoveIfExists"):
                rows.pop(key, None)
                continue
            if operation in ("Patch", "PatchIfExists"):
                if key not in rows:
                    if operation == "PatchIfExists":
                        continue
                    raise ValueError(f"{filename}: patch without a base {key}")
                row = rows[key]
            else:
                if key in rows and operation not in ("Replace", "ReplaceIfExists"):
                    raise ValueError(f"{filename}: duplicate definition {key}")
                if operation == "ReplaceIfExists" and key not in rows:
                    continue
                row = rows[key] = {
                    "definition": filename,
                    "parent": node.get("Parent"),
                    "abstract": node.get("Abstract", "false").lower() == "true",
                }
            if "DisplayName" in node.attrib:
                row["name"] = node.attrib["DisplayName"]
                row["display_source"] = filename
    return rows, hashes


def display_name(key, rows, visited=frozenset()):
    if key in visited:
        raise ValueError(f"Cyclic parent reference: {key}")
    row = rows[key]
    if "name" in row:
        return row["name"], row["display_source"]
    if row["parent"] is not None:
        return display_name((key[0], row["parent"]), rows, visited | {key})
    return None, row["definition"]


def main():
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", required=True, type=Path)
    parser.add_argument("--runtime", type=Path, help="part-name-runtime.json from an isolated smoke test")
    args = parser.parse_args()
    asset_root = next(path for path in (root / "assets").iterdir() if (path / "compatibility.json").is_file())
    compatibility = json.loads((asset_root / "compatibility.json").read_text(encoding="utf-8"))
    if hashlib.sha256((args.game / "KSA.dll").read_bytes()).hexdigest() != compatibility["files"]["KSA.dll"]:
        raise SystemExit("Uninspected KSA.dll; part audit stopped.")
    build = compatibility["version"].rsplit(".", 1)[-1]
    pack = json.loads((asset_root / "Locales/zh-CN.json").read_text(encoding="utf-8"))["parts"]
    ledger = {
        row["key"]: row["chinese"]
        for row in map(json.loads, (root / f"translations/reviewed-{build}.jsonl").read_text(encoding="utf-8").splitlines())
        if row["section"] == "parts" and row["review_status"] == "reviewed"
    }
    if pack != ledger:
        raise ValueError("Generated part dictionary differs from the reviewed ledger.")
    core = args.game / "Content/Core"
    assets = tomllib.loads((core / "mod.toml").read_text(encoding="utf-8-sig"))["assets"]
    rows, hashes = read_rows(core, assets)
    result = []
    for key, row in sorted(rows.items()):
        kind, ident = key
        if row["abstract"]:
            continue
        original, source = display_name(key, rows)
        result.append({
            "kind": kind, "id": ident, "parent": row["parent"], "name_source": source,
            "original": original or ident, "explicit_name": bool(original), "translation": pack.get(ident),
        })

    stats = {
        "schema": 2, "game_version": compatibility["version"], "source_xml_sha256": hashes,
        "part_definitions": sum(row["kind"] == "Part" for row in result),
        "subpart_definitions": sum(row["kind"] == "SubPart" for row in result),
        "missing_top_level": [row["id"] for row in result if row["kind"] == "Part" and row["translation"] is None],
        "missing_named_subparts": [row["id"] for row in result if row["kind"] == "SubPart"
                                    and row["explicit_name"] and row["translation"] is None],
        "scope": "Core display-name projection only; the game resolves Key/Ref and component data. Registry and UI checks are separate.",
    }
    if args.runtime:
        loaded = json.loads(args.runtime.read_text(encoding="utf-8-sig"))
        native = {row["id"]: row["original"] for row in result}
        stats["runtime"] = {
            "registered": len(loaded),
            "translated": sum(row["original"] != row["chinese"] for row in loaded),
            "identical": [row["key"] for row in loaded if row["original"] == row["chinese"]],
            "name_projection_differences": [row["key"] for row in loaded if row["key"] in native
                                            and native[row["key"]] != row["original"]],
        }
    (root / f"reports/parts-{build}-audit.json").write_text(
        json.dumps(stats, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print({key: val for key, val in stats.items() if key != "source_xml_sha256"})
    if stats["missing_top_level"] or stats["missing_named_subparts"]:
        raise SystemExit("Missing Core part captions; inspect the report.")


if __name__ == "__main__":
    main()
