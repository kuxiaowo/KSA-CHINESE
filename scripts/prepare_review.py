"""Import MIT reference translations into a traceable, initially unreviewed ledger."""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
REF = ROOT / '.tools/reference/KsaUiLanguages'
pack = json.loads((ROOT / 'assets/KsaChinese/Locales/zh-CN.json').read_text(encoding='utf-8'))
native = {r['key']: r for r in map(json.loads, (ROOT / 'work/translation-candidates-5602.jsonl').read_text(encoding='utf-8').splitlines()) if r.get('kind') == 'native-lstring'}
context = json.loads((REF / 'reference/translation-context.zh-CN.json').read_text(encoding='utf-8'))['sections']
native_ref = json.loads((REF / 'reference/native-source-5541.json').read_text(encoding='utf-8'))
rows=[]
for section, entries in pack.items():
    if not isinstance(entries,dict): continue
    for key,zh in entries.items():
        item=native.get(key,{})
        rows.append(dict(id=f'{section}:{key}',section=section,key=key,english=item.get('source',key),chinese=zh,
                         source_version='2026.10.13.5602',translation_origin='LiX-Works/KsaUiLanguages@b504774158cc03b582a0e75bfe94330cb841f11d (MIT)',
                         context=context.get(section,{}).get(key,''),review_status='inherited-unreviewed',
                         reviewer='',evidence=[],units_or_frame='',notes='',visual_status='not-tested'))
(ROOT/'translations/imported.jsonl').write_text(''.join(json.dumps(r,ensure_ascii=False)+'\n' for r in rows),encoding='utf-8')
print({'rows':len(rows),'sections':{s:sum(r['section']==s for r in rows) for s in {r['section'] for r in rows}}})
