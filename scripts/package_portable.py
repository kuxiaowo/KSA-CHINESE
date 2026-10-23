"""Build a game-directory Mod package using explicit, non-game allowlists."""
from pathlib import Path
import hashlib, json, shutil, zipfile, uuid, re

root = Path(__file__).resolve().parents[1]
version=re.search(r'^version = "([0-9.]+)"',(root/'assets/KsaChinese/mod.toml').read_text(encoding='utf-8'),re.M).group(1)
stage = root/'work'/('portable-package-'+version+'-'+uuid.uuid4().hex[:8])
stage.mkdir(parents=True)

def copy(src, dest):
    dest=stage/dest
    dest.parent.mkdir(parents=True, exist_ok=True)
    if src.is_dir(): shutil.copytree(src,dest)
    else: shutil.copy2(src,dest)

copy(root/'work/build/KsaChinese/KsaChinese.dll', Path('Content/KsaChinese/KsaChinese.dll'))
for p in (root/'assets/KsaChinese').iterdir():
    copy(p,Path('Content/KsaChinese')/p.name)
for name in ('KsaChineseLauncher.exe','KsaChineseLauncher.dll','KsaChineseLauncher.deps.json','KsaChineseLauncher.runtimeconfig.json'):
    copy(root/'work/build/Launcher'/name, Path('KsaChinese')/name)
loader_names=('0Harmony.dll','StarMap.API.dll','StarMap.Core.dll','StarMap.deps.json','StarMap.dll','StarMap.exe','StarMap.runtimeconfig.json','StarMap.Types.dll')
for name in loader_names: copy(root/'.tools/StarMap-0.4.8'/name,Path('KsaChinese/Loader')/name)
copy(root/'LICENSES',Path('KsaChinese/LICENSES'))
for name in ('README-便携Mod.md','THIRD_PARTY.md','LICENSE'):
    copy(root/name,Path('KsaChinese')/name)
allowed={'KsaChinese.dll','KsaChineseLauncher.dll',*(n for n in loader_names if n.endswith('.dll'))}
files=sorted(p for p in stage.rglob('*') if p.is_file())
for p in files:
    if p.suffix=='.dll' and p.name not in allowed: raise ValueError('Unexpected DLL '+str(p))
    if 'Profile' in p.parts or 'saves' in p.parts: raise ValueError('Player data '+str(p))
manifest={p.relative_to(stage).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
(stage/'KsaChinese/FILES-SHA256.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
game_build=json.loads((root/'assets/KsaChinese/compatibility.json').read_text(encoding='utf-8'))['version'].rsplit('.',1)[-1]
archive=root/f'dist/KsaChinese-{version}-{game_build}-Portable.zip'
archive.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for p in sorted(stage.rglob('*')):
        if p.is_file(): z.write(p,p.relative_to(stage).as_posix())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
digest=hashlib.sha256(archive.read_bytes()).hexdigest()
(archive.parent/f'SHA256SUMS-{version}.txt').write_text(digest+'  '+archive.name+'\n',encoding='utf-8')
result=dict(archive=archive.name,sha256=digest,files=len(manifest)+1,bytes=archive.stat().st_size,game_binaries_included=False,player_data_included=False)
(root/'reports/portable-package-validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result))
