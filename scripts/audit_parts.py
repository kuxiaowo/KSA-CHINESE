"""Read-only audit of core top-level part names against the reviewed dictionary.

Run in conda: python scripts/audit_parts.py --game <KSA installation>
This is a definition inventory, not a count of selectable or visually tested parts.
"""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--game', required=True, type=Path)
args = parser.parse_args()
compat = json.loads((root/'assets/KsaChinese/compatibility.json').read_text(encoding='utf-8'))
if hashlib.sha256((args.game/'KSA.dll').read_bytes()).hexdigest() != compat['files']['KSA.dll']:
    raise ValueError('Uninspected KSA.dll; part audit stopped.')
game_build = compat['version'].rsplit('.',1)[-1]
pack = json.loads((root/'assets/KsaChinese/Locales/zh-CN.json').read_text(encoding='utf-8'))
ledger = {r['key']: r for r in map(json.loads, (root/f'translations/reviewed-{game_build}.jsonl').read_text(encoding='utf-8').splitlines()) if r['section']=='parts' and r['review_status']=='reviewed'}
parts, data, subparts = {}, {}, {}
sources = {}
for path in sorted((args.game/'Content/Core').glob('*.xml')):
    for node in ET.parse(path).getroot():
        key = node.get('Id')
        if node.tag == 'Part':
            if key in parts: raise ValueError('Duplicate part '+key)
            parts[key] = (path.name, node)
        elif node.tag == 'SubPart':
            subparts[key] = (path.name, node)
        elif node.tag in ('PartGameData', 'SubPartGameData'):
            data.setdefault(key, []).append((path.name, node))
    sources[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()

rows = []
for key, (filename, node) in parts.items():
    displayed = node.get('DisplayName') or key
    tags = {x.get('Value') for x in node.findall('EditorTag')}
    for _, extra in data.get(key, []):
        displayed = extra.get('DisplayName') or displayed
        tags.update(x.get('Value') for x in extra.findall('EditorTag'))
    translated = pack['parts'].get(key)
    decision = ledger.get(key)
    if translated is not None and (decision is None or decision['chinese'] != translated):
        raise ValueError('Missing or mismatched semantic review '+key)
    rows.append(dict(key=key, source_file='Content/Core/'+filename, original=displayed,
                     chinese=translated, editor_tags=sorted(tags),
                     status='missing' if translated is None else 'preserved-model' if translated==displayed else 'translated'))

missing = [r['key'] for r in rows if r['status']=='missing']
named_subparts = [dict(key=key, original=n.get('DisplayName'), chinese=pack['parts'].get(key))
                  for key in subparts for _,n in data.get(key,[]) if n.get('DisplayName')]
report = dict(schema=1, game_version=compat['version'], top_level_definitions=len(rows),
              translated=sum(r['status']=='translated' for r in rows),
              preserved_models=[r['key'] for r in rows if r['status']=='preserved-model'],
              missing=missing, named_subparts=named_subparts,
              internal_subpart_definitions=len(subparts),
              source_xml_sha256=sources, parts=rows,
              limits='Core top-level definitions including hidden/test assets; not selectable-part count or per-entry visual acceptance. Internal mesh/subpart IDs remain technical identifiers unless explicitly named and reviewed.')
(root/f'reports/parts-{game_build}-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print({k:v for k,v in report.items() if k not in ('source_xml_sha256','parts')})
if missing or any(r['chinese'] is None for r in named_subparts):
    raise SystemExit('Missing core part display translations')
