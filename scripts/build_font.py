"""Build a renamed OFL CJK subset from the pinned Noto Sans SC source."""
from pathlib import Path
import hashlib,json
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset

root=Path(__file__).resolve().parents[1]
source=root/'.tools/NotoSansSC-variable.ttf'
expected='a3041811a78c361b1de50f953c805e0244951c21c5bd412f7232ef0d899af0da'
if hashlib.sha256(source.read_bytes()).hexdigest()!=expected:
    raise ValueError('Noto font source changed; review before rebuilding')
texts=['Language','English','简体中文','显示器刷新率']
# A small number of adapters have context-specific Chinese captions in code.
# Include their Han characters as well, so a new menu caption cannot silently
# lose glyphs merely because it is outside a locale JSON section.
for p in (root/'src/KsaChinese').glob('*.cs'):
    texts.append(''.join(c for c in p.read_text(encoding='utf-8') if '\u3400' <= c <= '\u9fff'))
for p in (root/'assets/KsaChinese/Locales').glob('*.json'):
    pack=json.loads(p.read_text(encoding='utf-8'))
    texts.append(pack['displayName'])
    for section in pack.values():
        if isinstance(section,dict): texts.extend(section.keys()); texts.extend(section.values())
font=instantiateVariableFont(TTFont(source),{'wght':400},inplace=True)
available=set(font.getBestCmap())
required={ord(c) for s in texts for c in s if ord(c)>=32}
missing=required-available
if missing: raise ValueError(f'Missing font codepoints: {sorted(missing)}')
for lower,upper in ((32,256),(0x370,0x400),(0x2000,0x2070),(0x2190,0x2300),(0x2500,0x2600)):
    required.update(set(range(lower,upper)) & available)
options=subset.Options(); options.layout_features=['*']; options.name_IDs=['*']
builder=subset.Subsetter(options=options); builder.populate(unicodes=required); builder.subset(font)
names={1:'KSA Chinese Noto SC Subset',2:'Regular',3:'KsaChineseNotoSC-Regular-0.1.0',4:'KSA Chinese Noto SC Subset Regular',6:'KsaChineseNotoSC-Regular',16:'KSA Chinese Noto SC Subset',17:'Regular'}
for ident,value in names.items():
    for record in list(font['name'].names):
        if record.nameID==ident: font['name'].setName(value,ident,record.platformID,record.platEncID,record.langID)
    font['name'].setName(value,ident,3,1,0x409)
out=root/'assets/KsaChinese/Fonts/KsaUiNotoSC.ttf'; font.save(out)
actual=set(TTFont(out).getBestCmap())
assert not required-actual
report=dict(source='https://github.com/google/fonts/tree/main/ofl/notosanssc',source_sha256=expected,
            subset_sha256=hashlib.sha256(out.read_bytes()).hexdigest(),mapped_codepoints=len(actual),required_codepoints=len(required),
            missing_codepoints=[],scope='all runtime translation keys and values plus Han characters in plugin source; UI/tooltip/gauge share this subset; arbitrary player names may need additional characters',license='SIL OFL 1.1')
(root/'reports/font-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(report)
