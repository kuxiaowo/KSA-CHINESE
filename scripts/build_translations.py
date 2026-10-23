"""Generate runtime JSON/resx only from review decisions, preserving a ledger."""
from pathlib import Path
import collections,json,re,xml.etree.ElementTree as ET

root=Path(__file__).resolve().parents[1]
imported={r['id']:r for r in map(json.loads,(root/'translations/imported.jsonl').read_text(encoding='utf-8').splitlines())}
reviewed={}
for file in sorted((root/'translations').glob('review-*.jsonl')):
    for line in file.read_text(encoding='utf-8').splitlines():
        r=json.loads(line)
        if r['id'] in reviewed: raise ValueError('Duplicate review: '+r['id'])
        reviewed[r['id']]={**imported.get(r['id'],{}),**r,'review_file':file.name}
missing=set(imported)-set(reviewed)
if missing: raise ValueError(f'{len(missing)} imported entries still await explicit review')
pack={'locale':'zh-CN','displayName':'简体中文','fontFile':'KsaUiNotoSC.ttf'}
for section in ('native','literals','ui','tooltips','editor','startup','parts','controls','hud','planning','utility','gauges','scoped','display','content','history'): pack[section]={}
brace=re.compile(r'\{[^{}]+\}')
printf=re.compile(r'%(?:[-+ #0]*\d*(?:\.\d+)?(?:ll|l|h)?[diuoxXfFeEgGaAcsp])')
markup=re.compile(r'<[^>]*>')
errors=[]
for r in reviewed.values():
    if r['review_status']!='reviewed': continue
    en,zh=r['english'],r['chinese']
    if r['section']=='editor' and en.endswith(': ') and not zh.endswith(': '):
        # PartArchetypes splits cards at the ASCII delimiter. Keep it for every
        # matching display fragment rather than relying on punctuation width.
        zh=zh.rstrip().rstrip('：:')+': '
        r['chinese']=zh
        r['notes'] += ' Root layout correction: preserve trailing ASCII colon-space required by part-card splitting; semantic wording unchanged.'
    if r['section']!='parts':
        for name,rx in [('placeholders',brace),('printf',printf),('markup',markup)]:
            # History bodies are passed to ImGui.Text as unformatted content.
            # A prose percentage such as "2% of" is not a printf %o token.
            if name=='printf' and r['section']=='history':continue
            if (collections.Counter(rx.findall(en))!=collections.Counter(rx.findall(zh)) if name!='markup' else rx.findall(en)!=rx.findall(zh)):
                errors.append((r['id'],name))
        if en.endswith(': ') and not zh.endswith(': '): errors.append((r['id'],'ASCII colon layout delimiter'))
    if not r.get('reviewer') or not r.get('evidence') or not r.get('notes'):
        errors.append((r['id'],'missing review evidence/notes'))
    pack[r['section']][r['key']]=zh
if errors: raise ValueError(errors)
out=root/'assets/KsaChinese/Locales/zh-CN.json'
out.write_text(json.dumps(pack,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
ledger=sorted(reviewed.values(),key=lambda r:(r['section'],r['key']))
game_build=json.loads((root/'assets/KsaChinese/compatibility.json').read_text(encoding='utf-8'))['version'].rsplit('.',1)[-1]
(root/f'translations/reviewed-{game_build}.jsonl').write_text(''.join(json.dumps(r,ensure_ascii=False)+'\n' for r in ledger),encoding='utf-8')
resx=ET.Element('root')
for key,value in [('Language','简体中文'),('Font','KsaUiNotoSC.ttf'),*pack['native'].items()]:
    data=ET.SubElement(resx,'data',{'name':key,'{http://www.w3.org/XML/1998/namespace}space':'preserve'})
    ET.SubElement(data,'value').text=value
ET.indent(resx,space='  ')
lang=root/'assets/KsaChinese/Languages'; lang.mkdir(exist_ok=True)
ET.ElementTree(resx).write(lang/'zh-CN.resx',encoding='utf-8',xml_declaration=True)
report={'schema':1,'ledger_rows':len(ledger),'review_status_counts':dict(collections.Counter(r['review_status'] for r in ledger)),
        'active_translation_sections':{s:len(v) for s,v in pack.items() if isinstance(v,dict)},
        'excluded_entries':[{'id':r['id'],'status':r['review_status'],'notes':r.get('notes','')} for r in ledger if r['review_status']!='reviewed'],
        'placeholder_printf_markup_errors':[], 'meaning':'review counts and runtime dictionary size, not full-game coverage or visual verification'}
(root/'reports/translation-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print({k:v for k,v in report.items() if k!='excluded_entries'})
