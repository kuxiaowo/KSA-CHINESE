"""Build the 5638 display-site manifest from local AST/IL and the review ledger.

The audit inputs contain game-derived investigation data and must stay local.
Only method identities and reviewed display keys are written to the Mod.
"""
from pathlib import Path
from collections import defaultdict
import argparse, hashlib, json, re

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--audit-dir',type=Path,required=True)
parser.add_argument('--game-dir',type=Path,required=True)
parser.add_argument('--check',action='store_true',help='Fail on differences without rewriting the asset')
args=parser.parse_args()
AUDIT=args.audit_dir.resolve()
compat=json.loads((ROOT/'assets/KsaChinese/compatibility.json').read_text(encoding='utf8'))
for name,expected in compat['files'].items():
    if hashlib.sha256((args.game_dir/name).read_bytes()).hexdigest()!=expected:
        raise SystemExit('Uninspected game identity: '+name)
rows=lambda p:[json.loads(l) for l in p.read_text(encoding='utf-8-sig').splitlines() if l.strip()]
game_build=compat['version'].rsplit('.',1)[-1]
existing={r['id']:r for r in rows(ROOT/f'translations/reviewed-{game_build}.jsonl')}
new={}
calls=rows(AUDIT/'ast/ui-calls.jsonl'); literals=rows(AUDIT/'ast/literals.jsonl')
il=rows(AUDIT/'il/il-methods.jsonl'); by_short=defaultdict(set)
for x in il:
    if x['assembly']=='KSA.dll':
        for name in {x['type'].removeprefix('KSA.').replace('+','.'),x['type'].rsplit('.',1)[-1].replace('+','.')}:
            by_short[(name,x['member'])].add(x['type'])
sites={}
excluded={'ImGuiHelper','ConsoleStyle','ConsoleWidgets','MapController'}
for x in calls:
    loc=x['location'];key=(loc['type'],loc['member'])
    if '/decompiled/KSA/' not in loc['file'] or loc['type'] in excluded or loc['member'].startswith('.'):continue
    inv=x['invocation']
    if not (re.search(r'^ImGui\.(?:Text|SeparatorText|Begin(?:Menu|Combo|Table|Popup|Tab|$)|MenuItem|Button|SmallButton|Checkbox|Selectable|TreeNode|CollapsingHeader|Input|Drag|Slider|RadioButton|TableSetupColumn)',inv)
            or inv.startswith(('ImGuiHelper.Draw','ImGuiHelper.ColoredFillBar','ConsoleWidgets.','ConsoleStyle.BeginWindow','ConsoleStyle.FooterStatus'))):continue
    if loc['type'].startswith('ImGuiBackend'):continue
    for typ in by_short.get(key,[]): sites[(typ,loc['member'])]={'Type':typ,'Method':loc['member'],'Fields':set(),'Literals':set()}
for typ,meth in [('KSA.SubstanceLibrary','SubstanceTooltipText'),('KSA.InputEvents+ChangeControlPartData','Apply'),('KSA.SequenceList','GetModuleAction'),('KSA.ChuteTuningWindow','ClothModeLabel'),('KSA.Parachute','StateLabel'),('KSA.ResourcesPanel','Draw')]:
    sites.setdefault((typ,meth),{'Type':typ,'Method':meth,'Fields':set(),'Literals':set()})
# Plot legends and draw-list text are outside the original ImGui call query.
for x in literals:
    loc=x['location'];inv=x.get('invocation') or ''
    if '/decompiled/KSA/' not in loc['file']:continue
    if not (inv.startswith('ImPlot.') or inv.endswith('.AddText')):continue
    if loc['type'] in excluded or loc['member'].startswith('.'):continue
    for typ in by_short.get((loc['type'],loc['member']),[]):
        sites.setdefault((typ,loc['member']),{'Type':typ,'Method':loc['member'],'Fields':set(),'Literals':set()})
for x in literals:
    loc=x['location'];key=(loc['type'],loc['member'])
    if '/decompiled/KSA/' not in loc['file']:continue
    inv=x.get('invocation') or ''
    for typ in by_short.get(key,[]):
        site=sites.get((typ,loc['member']))
        if site is None:continue
        # A helper label may be assigned to a local before the call. This set
        # authorizes only the helper's final Text(variable) rendering, never its
        # input variable, selection value, sort key or generated native ID.
        site['Fields'].add(x['value'])
        if (typ,loc['member']) in [('KSA.ChuteTuningWindow','ClothModeLabel'),('KSA.Parachute','StateLabel')]:site['Literals'].add(x['value'])
        if typ=='KSA.EngineDesigner' and loc['member']=='DrawEngineDesignDebugWindow' and x['value'] in ['part','parts']:site['Literals'].add(x['value'])
        if inv.startswith('ImGuiHelper.') and x['argument']==0:site['Fields'].add(x['value'])
        if (any(y in inv for y in ('ImGui.','ImGuiHelper.','ConsoleWidgets.','ConsoleStyle.','ImPlot.')) or inv.endswith('.AddText')) and x['value'] and not x['value'].startswith('##'):
            site['Literals'].add(x['value'])
        elif inv.endswith(('AppendLiteral','Append','AppendFormatted')) and re.search('[A-Za-z]',x['value']):site['Literals'].add(x['value'])
for x in literals:
    loc=x['location']
    if '/decompiled/KSA/' not in loc['file'] or not loc['member'].startswith('field:'):continue
    for (typ,method),site in sites.items():
        if typ.rsplit('.',1)[-1].replace('+','.')==loc['type']:site['Fields'].add(x['value'])
manifest=[]
# The compiler lowers this interpolated Text call into an AppendLiteral
# fragment. Authorize that reviewed fragment only in its actual UI method.
fragment_site=sites.get(('KSA.Rendering.CelestialTextureStreamer','DrawDebugWindow'))
if fragment_site is not None:
    il_strings=rows(AUDIT/'il/il-strings.jsonl')
    if not any(x['type']=='KSA.Rendering.CelestialTextureStreamer' and x['member']=='DrawDebugWindow' and x['value']=='Budget: ' for x in il_strings):
        raise SystemExit('Texture-streaming budget display fragment changed')
    fragment_site['Literals'].add('Budget: ')
display_sections={'literals','ui','tooltips','editor','startup','controls','hud','planning','utility','gauges','display'}
reviewed_keys={r['key'] for r in [*existing.values(),*new.values()] if r['section'] in display_sections and r['review_status']=='reviewed'}
for site in sites.values():
    # Methods with open generic parameters are listed for discovery separately,
    # and closed display combos have dedicated adapters.
    if '`' in site['Type']:continue
    identity=site['Type']+'.'+site['Method']
    def needed(value):
        visible=value.split('##',1)[0]
        return value in reviewed_keys or visible in reviewed_keys or identity+'|'+visible in reviewed_keys
    # The distribution needs only reviewed display keys. Drop paths, log text,
    # shader/XML snippets and other non-display literals from the manifest.
    site['Fields']={v for v in site['Fields'] if needed(v)}
    site['Literals']={v for v in site['Literals'] if needed(v)}
    manifest.append({k:sorted(v) if isinstance(v,set) else v for k,v in site.items()})
output=ROOT/'assets/KsaChinese/display-sites.json'
content=json.dumps(sorted(manifest,key=lambda s:(s['Type'],s['Method'])),ensure_ascii=False,indent=2)+'\n'
if args.check:
    if not output.exists() or output.read_text(encoding='utf8')!=content:
        raise SystemExit('Display manifest differs from the reviewed inventory; inspect before rebuilding.')
else:
    output.write_text(content,encoding='utf8')
print(json.dumps({'methods':len(manifest),'reviewed_field_keys':sum(len(x['Fields']) for x in manifest),
                  'reviewed_literal_keys':sum(len(x['Literals']) for x in manifest),'check_only':args.check}))
