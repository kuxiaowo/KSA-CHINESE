"""Package explicit Mod/loader/license/source/report allowlists, never game files."""
from pathlib import Path
import datetime,hashlib,json,shutil,uuid,zipfile,re

root=Path(__file__).resolve().parents[1]
stage=root/'work'/('package-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:8])
stage.mkdir(parents=True)
def copy(source,dest):
    source=root/source; dest=stage/dest; dest.parent.mkdir(parents=True,exist_ok=True)
    if source.is_dir(): shutil.copytree(source,dest,ignore=shutil.ignore_patterns('bin','obj','__pycache__','*.pyc'))
    else: shutil.copy2(source,dest)
copy(Path('work/build/KsaChinese/KsaChinese.dll'),Path('PlayerPayload/KsaChinese/KsaChinese.dll'))
for asset in (root/'assets/KsaChinese').iterdir(): copy(asset.relative_to(root),Path('PlayerPayload/KsaChinese')/asset.name)
loader_names=['0Harmony.dll','StarMap.API.dll','StarMap.Core.dll','StarMap.deps.json','StarMap.dll','StarMap.exe','StarMap.runtimeconfig.json','StarMap.Types.dll']
for name in loader_names: copy(Path('.tools/StarMap-0.4.7')/name,Path('Loader')/name)
for path in ('installer','LICENSES','reports','translations','src','scripts'):
    copy(Path(path),Path(path))
previous_package_report=stage/'reports/package-validation.json'
if previous_package_report.exists(): previous_package_report.unlink()
for name in ('README.md','README-安装卸载.md','LICENSE','THIRD_PARTY.md','AGENTS.md','.gitattributes','.gitignore'):
    copy(Path(name),Path(name))
allowed_dlls={'KsaChinese.dll',*{n for n in loader_names if n.endswith('.dll')}}
files=sorted(p for p in stage.rglob('*') if p.is_file())
for p in files:
    if p.suffix=='.dll' and p.name not in allowed_dlls: raise ValueError('Unexpected DLL '+str(p))
    if any(s in p.parts for s in ('.tools','inspection','test-profile','saves')) or p.name=='KSA.decompiled.cs': raise ValueError('Private/game material '+str(p))
manifest={str(p.relative_to(stage)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
(stage/'FILES-SHA256.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
dist=root/'dist'; dist.mkdir(exist_ok=True)
version=re.search(r'^version = "([^"]+)"',(root/'assets/KsaChinese/mod.toml').read_text(encoding='utf-8'),re.M).group(1)
archive=dist/f'KsaChinese-{version}-5602-Legacy.zip'
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for p in sorted(stage.rglob('*')):
        if p.is_file(): z.write(p,str(p.relative_to(stage)).replace('\\','/'))
with zipfile.ZipFile(archive) as z:
    if z.testzip() is not None: raise ValueError('Corrupt archive')
hashvalue=hashlib.sha256(archive.read_bytes()).hexdigest()
(dist/'SHA256SUMS.txt').write_text(hashvalue+'  '+archive.name+'\n',encoding='utf-8')
result=dict(archive=str(archive),sha256=hashvalue,bytes=archive.stat().st_size,files=len(manifest)+1,stage=str(stage),
            allowed_dlls=sorted(allowed_dlls),game_binaries_or_decompiled_source_in_package=False)
(root/'reports/package-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
