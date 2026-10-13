"""Record only observed runtime/log evidence and exact screenshot identities."""
from pathlib import Path
import hashlib,json,re,shutil
root=Path(__file__).resolve().parents[1]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return p.read_text(encoding='utf-8',errors='replace')
full=read(root/'work/runtime/final2.stdout.log')
quick=read(root/'work/runtime/quick.stdout.log')
fallback=read(root/'work/runtime/fallback2.stdout.log')
fullerr=read(root/'work/runtime/final2.stderr.log')
fallbackerr=read(root/'work/runtime/fallback2.stderr.log')
installed=read(root/'work/runtime/installed-quick2.stdout.log')
installederr=read(root/'work/runtime/installed-quick2.stderr.log')
assert '[Smoke] completed' in full
assert '[Smoke] quick-completed' in quick
assert "finished loading save game 'ksa_chinese_5602_test'" in full
assert '[Smoke] fallback-completed' in fallback
assert '[Smoke] uninstalled-loaded; controlled=Rocket' in fallback
assert not fullerr.strip() and not fallbackerr.strip()
assert '[Smoke] quick-completed' in installed and not installederr.strip()
install=json.loads((root/'reports/installer-validation.json').read_text(encoding='utf-8-sig'))
assert sorted(install['save_before'],key=lambda r:r['name'])==sorted(install['save_after'],key=lambda r:r['name'])
assert install['uninstall_mod_absent']
identities={
 'hud-zh.png':('work/test-profile/exports/screenshots/ksa_20261013_212422_1920x1080.png','quick','中文HUD；标签和数字分别渲染，缩写保持'),
 'settings-display-zh.png':('work/test-profile/exports/screenshots/ksa_20261013_211849_1920x1080.png','final2','显示页、导航、DISPLAY上下文修正'),
 'settings-graphics-zh.png':('work/test-profile/exports/screenshots/ksa_20261013_212429_1920x1080.png','quick','图形页顶部；不包含滚动下方所有设置'),
 'settings-controls-zh.png':('work/test-profile/exports/screenshots/ksa_20261013_212436_1920x1080.png','quick','控制页顶部键位标题与原按键；未改绑定'),
 'planning-zh.png':('work/test-profile/exports/screenshots/ksa_20261013_211925_1920x1080.png','final2','飞行计划/转移规划初始状态；未计算转移解'),
 'hud-en.png':('work/test-profile/exports/screenshots/ksa_20261013_212444_1920x1080.png','quick','英文回退；字体比例缓存修正后'),
 'hud-zh-roundtrip.png':('work/test-profile/exports/screenshots/ksa_20261013_212451_1920x1080.png','quick','英文切回中文后HUD'),
 'tracking-zh.png':('work/test-profile/exports/screenshots/ksa_20261013_212005_1920x1080.png','final2','目标窗无目标提示；地面窗有遮挡，绘图未完整验收'),
 'uninstalled-en.png':('work/test-profile-installed/Profile/exports/screenshots/ksa_20261013_212352_1920x1080.png','fallback2','卸载后实际载入中文测试存档；不再有Language菜单'),
}
screens=[]
folder=root/'reports/screenshots';folder.mkdir(exist_ok=True)
for name,(source,run,meaning) in identities.items():
    path=root/source
    if not path.is_file(): raise FileNotFoundError(path)
    dest=folder/name; shutil.copy2(path,dest)
    screens.append(dict(file='screenshots/'+name,sha256=sha(dest),bytes=dest.stat().st_size,run=run,interpretation=meaning,visual_review='root inspected actual PNG'))
audit=json.loads((root/'reports/local-audit-2026-10-13.json').read_text(encoding='utf-8'))
game=Path(audit['game_directory'])
unchanged={name:sha(game/name)==expected for name,expected in audit['input_sha256'].items()}
assert all(unchanged.values())
report=dict(schema=1,game_version='2026.10.13.5602',plugin_sha256=sha(root/'work/build/KsaChinese/KsaChinese.dll'),
 font_sha256=sha(root/'assets/KsaChinese/Fonts/KsaUiNotoSC.ttf'),
 runs=[dict(id='final2',plugin_sha256='5ff8a4604b8079d9126291ca0cb2d5f484b71430f129b351dc1a9270eb05d223',log_sha256=sha(root/'work/runtime/final2.stdout.log'),completed=True,role='save/load/planning/editor runtime; before subsequent font-cache-only fix'),
       dict(id='quick',plugin_sha256=sha(root/'work/build/KsaChinese/KsaChinese.dll'),log_sha256=sha(root/'work/runtime/quick.stdout.log'),completed=True,role='final font-cache fix and actual settings tab indices; HUD and English roundtrip'),
       dict(id='installed-quick2',plugin_sha256=sha(root/'work/build/KsaChinese/KsaChinese.dll'),log_sha256=sha(root/'work/runtime/installed-quick2.stdout.log'),completed=True,role='final Chinese installed Launch.ps1 on Windows PowerShell 5.1; hash guard, settings and roundtrip'),
       dict(id='fallback2',log_sha256=sha(root/'work/runtime/fallback2.stdout.log'),completed=True,role='installed Launch.ps1 on Windows PowerShell 5.1; KsaChinese uninstalled; same test save loads')],
 observations=dict(chinese_hud=True,display_settings=True,graphics_top=True,controls_top=True,planning_initial=True,target_track_empty=True,
                   editor_and_part_window_drawn_without_exception=True,editor_visual_verified=False,editor_screenshot_rejection='Screenshot: not available in the editor',
                   game_save_created_and_loaded=True,uninstalled_save_loaded_in_english=True,test_save_hashes_unchanged_by_uninstall=True),
 audited_game_inputs_unchanged=unchanged,original_player_save_contents_read=False,screenshots=screens,
 limits=['developer driver triggers native code; not full human mouse/keyboard interaction','no editor/part layout screenshots','no complete transfer calculation or maneuver execution','single machine/GPU; partial resolution coverage','no long run or personal save migration'])
(root/'reports/runtime-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print({'screenshots':len(screens),'game_inputs_unchanged':all(unchanged.values()),'save_uninstall_hash_check':True})
