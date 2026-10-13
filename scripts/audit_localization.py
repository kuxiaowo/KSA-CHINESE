"""Read-only KSA localization inventory. Outputs must be outside game/player dirs.

Use an existing conda environment with fontTools. The decompiled input is local
inspection material produced by ILSpy, never a source file to redistribute.
Regex-derived C# entries are discovery candidates, not a UI coverage measurement.
"""

import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import tomllib
from xml.etree import ElementTree as ET


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def decode_literal(value):
    # The supported subset is deliberately narrow; reject unknown escapes.
    def replace(match):
        token = match.group(0)
        simple = {'\\n': '\n', '\\r': '\r', '\\t': '\t', '\\0': '\0',
                  '\\\\': '\\', '\\"': '"', "\\'": "'"}
        if token in simple:
            return simple[token]
        if token.startswith(("\\u", "\\U")):
            return chr(int(token[2:], 16))
        raise ValueError(f"Unsupported C# escape: {token}")
    return re.sub(r"\\(?:u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|.)", replace, value)


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", type=Path, required=True)
    parser.add_argument("--player", type=Path, required=True)
    parser.add_argument("--ksp", type=Path, required=True)
    parser.add_argument("--decompiled", type=Path, required=True)
    parser.add_argument("--game-version", required=True,
                        help="FileVersion inspected independently; not inferred by this script")
    parser.add_argument("--summary", type=Path, required=True)
    parser.add_argument("--inventory", type=Path, required=True)
    args = parser.parse_args()
    for destination in (args.summary.resolve(), args.inventory.resolve()):
        for source in (args.game.resolve(), args.player.resolve(), args.ksp.resolve()):
            if destination == source or source in destination.parents:
                parser.error("Output path must be outside the game and player directories")

    content = args.game / "Content"
    resx = content / "Core/Languages/en-US.resx"
    resx_entries = {entry.attrib["name"]: entry.findtext("value", default="")
                    for entry in ET.parse(resx).getroot().findall("data")}
    config_keys = {"Font", "Language"}
    shipped_keys = set(resx_entries) - config_keys
    code = args.decompiled.read_text(encoding="utf-8-sig")
    start = code.index("public static class LStrings\n")
    end = code.index("\n\tpublic ", start + 30)
    block = code[start:end]
    csharp_string = r'"((?:\\.|[^"\\])*)"'
    registered = []
    pattern = re.compile(r'\bCreate\(' + csharp_string + r',\s*' + csharp_string + r'\)')
    for match in pattern.finditer(block):
        key, source = map(decode_literal, match.groups())
        registered.append({"kind": "native-lstring", "key": key, "source": source,
                           "present_in_shipped_resx": key in shipped_keys,
                           "decompiled_line": code.count("\n", 0, start + match.start()) + 1})
    keys = {entry["key"] for entry in registered}
    if len(keys) != len(registered):
        raise ValueError("Duplicate LString IDs in extracted candidates")
    if not keys or not shipped_keys.issubset(keys):
        raise ValueError("Unexpected native extraction results; inspect the decompiler format")
    prefix_counts = Counter(re.match(r"Alert|Settings|FlightComputer|.+", key).group(0) for key in keys)

    xml_candidates = []
    xml_errors = []
    for path in sorted((content / "Core").rglob("*.xml")):
        try:
            tree = ET.parse(path)
        except ET.ParseError as error:
            xml_errors.append({"file": str(path.relative_to(args.game)), "error": str(error)})
            continue
        for ordinal, element in enumerate(tree.getroot().iter()):
            fields = ["DisplayName", "Label"]
            if element.tag == "Tooltip":
                fields.append("Value")
            for field in fields:
                value = element.get(field)
                if value and re.search(r"[a-zA-Z]", value):
                    xml_candidates.append({"kind": "xml-display-candidate",
                        "file": path.relative_to(args.game).as_posix(), "element": element.tag,
                        "element_ordinal": ordinal, "field": field,
                        "id": element.get("Id"), "source": value})
    xml_fields = Counter(entry["field"] for entry in xml_candidates)
    xml_unique = Counter({field: len({entry["source"] for entry in xml_candidates if entry["field"] == field})
                         for field in xml_fields})

    literal_candidates = []
    literal_pattern = re.compile(csharp_string)
    for line_number, line in enumerate(code.splitlines(), 1):
        # A useful lower-bound discovery heuristic, intentionally not a parser.
        if not any(token in line for token in ("ImGui.", "ImGuiHelper.", "ConsoleWidgets.")):
            continue
        for match in literal_pattern.finditer(line):
            try:
                value = decode_literal(match.group(1))
            except ValueError:
                continue
            if re.search(r"[a-zA-Z]", value):
                literal_candidates.append({"kind": "ui-literal-candidate", "source": value,
                    "decompiled_line": line_number,
                    "review_note": "May be an internal ID, format, tooltip or label; review context before translating"})

    from fontTools.ttLib import TTFont
    fonts = []
    for path in sorted(content.rglob("*.ttf")):
        font = TTFont(path)
        cmap = font.getBestCmap() or {}
        fonts.append({"file": path.relative_to(args.game).as_posix(), "sha256": sha256(path),
                      "mapped_codepoints": len(cmap),
                      "cjk_unified_ideographs": sum(0x4E00 <= point <= 0x9FFF for point in cmap),
                      "missing_probe_characters": [char for char in "中文轨道比冲" if ord(char) not in cmap]})
        font.close()

    # Store counts, languages and paths, rather than copying whole KSP dictionaries.
    ksp_files = [args.ksp / "GameData/Squad/Localization/dictionary.cfg",
                 args.ksp / "GameData/SquadExpansion/Serenity/Localization/dictionary.cfg",
                 args.ksp / "GameData/MechJeb2/Localization/en-us.cfg",
                 args.ksp / "GameData/MechJeb2/Localization/zh-cn.cfg"]
    ksp_sources = []
    for path in ksp_files:
        if path.is_file():
            text = path.read_text(encoding="utf-8-sig")
            ksp_sources.append({"file": str(path), "sha256": sha256(path),
                "entries": len(re.findall(r"^\s*#[^\s=]+\s*=", text, re.M)),
                "locale_sections": re.findall(r"^\s*(zh-cn|en-us)\s*$", text, re.M)})
    manifest = args.player / "manifest.toml"
    enabled_mods = []
    if manifest.is_file():
        enabled_mods = [entry["id"] for entry in tomllib.loads(manifest.read_text(encoding="utf-8-sig"))
                        .get("mods", []) if entry.get("enabled")]
    watched = [args.game / "KSA.dll", args.game / "Brutal.Localization.dll", resx,
               content / "Core/Languages.xml", content / "Core/mod.toml",
               content / "Core/Shaders/GaugeLabel.frag"]
    summary = {
        "schema": 1, "audited_at_utc": datetime.now(timezone.utc).isoformat(),
        "game_directory": str(args.game), "player_directory": str(args.player),
        "game_version_independently_inspected": args.game_version,
        "content_extension_counts": dict(Counter(path.suffix or "(no extension)"
                                                for path in content.rglob("*") if path.is_file())),
        "enabled_player_manifest_mods": enabled_mods,
        "native": {"shipped_resx_entries": len(resx_entries), "configuration_entries": len(config_keys),
            "shipped_game_strings": len(shipped_keys), "code_create_entries": len(keys),
            "code_prefix_counts": dict(prefix_counts),
            "code_keys_missing_in_resx": sorted(keys - shipped_keys),
            "changed_english_source_keys": sorted(entry["key"] for entry in registered
                if entry["key"] in shipped_keys and resx_entries[entry["key"]] != entry["source"])},
        "xml": {"candidate_occurrences": len(xml_candidates), "by_field": dict(xml_fields),
                "unique_source_by_field": dict(xml_unique), "parse_errors": xml_errors},
        "ui_literal_heuristic": {"candidate_occurrences": len(literal_candidates),
            "unique_sources": len({entry["source"] for entry in literal_candidates}),
            "limitation": "Regex discovery only. Includes IDs; excludes other draw APIs, dynamic strings and multiline calls. Not a UI denominator."},
        "fonts": fonts, "ksp_reference_sources": ksp_sources,
        "input_sha256": {path.relative_to(args.game).as_posix(): sha256(path) for path in watched},
        "runtime_validation": "not performed; no game or mod was launched by this audit",
    }
    write_json(args.summary, summary)
    args.inventory.parent.mkdir(parents=True, exist_ok=True)
    with args.inventory.open("w", encoding="utf-8") as stream:
        for entry in registered + xml_candidates + literal_candidates:
            entry["translation_status"] = "unreviewed"
            stream.write(json.dumps(entry, ensure_ascii=False) + "\n")
    print(json.dumps({"summary": str(args.summary), "inventory": str(args.inventory),
        "shipped_game_strings": len(shipped_keys), "native_code_keys": len(keys),
        "xml_candidates": len(xml_candidates), "ui_literal_candidates": len(literal_candidates),
        "xml_errors": len(xml_errors)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
