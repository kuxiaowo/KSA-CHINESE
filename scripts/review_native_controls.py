"""Persist root's item-by-item review of the remaining native/control group.

English/Chinese pairs were read individually; source searches below locate
evidence, they do not decide a translation's semantic review status.
"""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
code=(root/'.tools/inspection/KSA.decompiled.cs').read_text(encoding='utf-8').splitlines()
imported=[json.loads(s) for s in (root/'translations/imported.jsonl').read_text(encoding='utf-8').splitlines()]
native={r['key']:r for r in map(json.loads,(root/'work/translation-candidates-5602.jsonl').read_text(encoding='utf-8').splitlines()) if r['kind']=='native-lstring'}
fixes={
 'SettingsGraphicsBloomsThresholdBloom':('阈值泛光','47539是布尔开关ThresholdBloom，不是可调数值阈值。'),
 'SettingsInterfaceEditorHideEdgeOnRotationRings':('隐藏侧对视线的旋转环','238163-238177按环轴与视线点积绝对值0.15隐藏侧对视线的环，不表示环之间重叠。'),
 'AlertControllingFrom':('当前控制点：{Name}','控制起点/控制模块名称，不是飞行器当前空间位置。'),
 'AlertFuelPortExists':('此部件已有推进剂接口，请点击现有接口','推进剂管线接口，包含燃料或氧化剂；统一fuel line组的推进剂用语。'),
 'AlertNearLimitHydrodynamic':('飞船接近最大水动力载荷！','与Aerodynamic并列，表示接触海洋产生的水动力结构载荷。'),
 'AlertWreckageHydrodynamicForces':('飞船因水动力载荷过大而损毁！','流体动力泛称过宽；该结构损毁条件对应水体载荷。'),
 'FlightComputerTooltipBurnEmpty':('未设置有效机动时长。','234214触发条件为BurnDuration<=0；是机动持续推力作用时间，非发动机点火延迟。'),
 'Make Vehicle Root':('设为当前飞船','240681-240683执行SetFocusedTree(part.Tree)，不是改变部件树根。'),
 'Camera Reference Frame':('切换相机参考坐标系','CameraReferenceFrame各坐标系转换实现；不改参考系枚举。'),
 'Map Forward':('地图垂直上移','MapController Forward映射Z正方向PositionOffsetFollowing；不是缩放或远离天体。'),
 'Map Backward':('地图垂直下移','MapController Backward映射Z负方向；与Scope/滚轮距离缩放不同。'),
 'Texture Streaming':('纹理流送','统一Graphics/Startup术语；按需加载纹理而非视频直播。'),
}
new={
 'AlertAutoBurnStageRanDry':'自动机动已暂停：本级推进剂耗尽。请执行分级，再重新打开自动模式以完成机动。',
 'AlertAutoBurnStageRanDryVehicle':'{Vehicle}：自动机动已暂停，本级推进剂耗尽。请执行分级，再重新打开自动模式以完成机动。',
 'SettingsTabsId':'分类',
 'SettingsGraphicsSpecularAntiAliasing':'镜面反射抗锯齿',
 'ShadowFilterTooltip_Hard':'不作滤波：开销最低，但阴影像素感明显。',
 'ShadowFilterTooltip_Pcf':'简单盒式滤波：固定模糊，性能开销较低。',
 'ShadowFilterTooltip_Pcss':'半影遮挡：视觉效果最佳，性能开销较高。',
 'SettingsGraphicsVolumetricsPlumeTrailShadows':'尾焰轨迹阴影',
 'SettingsGraphicsVolumetricsExplosionQuality':'爆炸效果质量',
 'SettingsGraphicsVolumetricsMaxExplosionInstances':'爆炸实例数量上限',
 'SettingsInterfaceOrbitBurnPathColor':'机动轨迹颜色',
}
group=[r for r in imported if r['section'] in ('native','controls','startup','literals')]
present={r['key'] for r in group if r['section']=='native'}
for key,zh in new.items():
    if key not in present: group.append(dict(id='native:'+key,section='native',key=key,english=native[key]['source'],chinese=zh,source_version='2026.10.13.5602',translation_origin='KSA-CHINESE 5602 missing-native review'))
result=[]
for original in group:
    r=dict(original); key=r['key']; section=r['section']; en=r['english']
    old=r['chinese']; status='reviewed'
    notes=[]
    if section=='native':
        search=key
        references=[(i+1,l.strip()) for i,l in enumerate(code) if key in l]
        if key.startswith('Settings'):
            notes.append('已逐条对照5602文本ID、英文及中文；该设置只改显示名，不改设置键、值或取值范围。')
            unit='显示/渲染/模拟设置；具体原始单位和取值范围保留'
        elif key.startswith('Alert'):
            notes.append('已逐条核对警告/状态源文；保留事件条件和所有命名占位符，玩家名称由原格式化机制填入。')
            unit='提示本身无数值换算；原格式化参数原样'
        else:
            notes.append('已逐条核对5602说明文本；只改提示，不改控制门限/滤波算法。')
            unit='不适用；说明文本'
        if key.startswith('AlertAutoBurn'):
            r['chinese']=new.get(key,old.replace('自动点火','自动机动'))
            notes.append('AutoBurn涉及连续推力执行、结束或阶段耗尽，不限于瞬间点火。36998阶段耗尽要求用户分级再重新启用。')
        if 'GForce' in key or 'GLoad' in key:
            notes.append('G载荷为加速度相对于g的比值，不是以牛顿表示的力；保持项目G载荷表述。')
            unit='g载荷比；不是N'
        if 'Burn' in key and not key.startswith('AlertAutoBurn'):
            notes.append('Burn指轨道/持续推力机动，与Rocket反应中的燃烧分开；SOI为主天体引力影响范围。')
        if key=='SettingsTabsId': status='defer'; notes.append('含Id的Tabs定义不作为可见文案启用，避免把潜在控件ID译掉。')
        if key=='SettingsDisplayFrameQueueLimit': unit='渲染帧数量；不是物理参考系'
        if key=='SettingsSimulationOrbitSolvers': unit='工作线程数；47635为1至CPU逻辑处理器数滑条'
        if key=='SettingsSimulationMaxFlightComputerRate': unit='飞行控制逻辑更新频率；不是飞行速度'
        if key=='SettingsGraphicsTextureStreamingBudget': unit='VRAM显存预算；不等同实际占用'
        if key=='SettingsInterfaceScale': unit='界面缩放比例；非编辑器部件物理尺寸'
        if key=='SettingsInterfaceFontDensity': unit='字体栅格化密度比例；非屏幕分辨率'
        if 'Camera' in key: notes.append('Camera是观察相机，Orbit camera表示环绕观察对象，不是飞行器入轨。')
        if key.startswith('ShadowFilterTooltip'): notes.append('对应Hard/PCF/PCSS阴影滤波说明，保留算法名称与性能相对关系。')
    elif section=='controls':
        search=''.join(key.split())
        references=[(i+1,l.strip()) for i,l in enumerate(code) if ('InputAction.'+search) in l or re.search(r'\b'+re.escape(search)+r'\s*=',l)]
        unit='动作标题；键位、InputAction枚举和TOML绑定保持原样'
        notes.append('逐条对照动作名称、默认绑定和实际引用；翻译仅用于键位行/捕获弹窗的标题，不改变按键执行。')
        if key.startswith(('Forward','Backward','Left','Right','Translate','Roll')):
            notes.append('具体姿态/平移参考系由当前飞行控制与EVA模式决定；标题不承诺全局惯性方向。')
        if key.startswith('Editor '): notes.append('编辑器动作与飞行器在模拟中的控制分开；模式/步长/物理尺寸未修改。')
        if key.startswith('H U D Layout') or 'H U D Layout' in key: notes.append('布局索引或前后切换；不改布局持久化ID及玩家名字。')
        if key.startswith('Map '): unit='地图相机黄道轴方向/平移，非轨道飞行器方向；不改Scope缩放'
        if key in ('Toggle Editor Part Window','Editor Toggle Symmetry','Editor Camera Focus','I V A Switch To Next Seat','Camera Mode'):
            status='defer'; notes.append('名称/枚举登记存在，但本轮未完整确认与实际快捷键处理器一一对应；暂缓译文，不能声称功能通过。')
        if key in ('INPUT','PRESS A KEY OR MOUSE BUTTON','HELD'):
            references=[(i+1,l.strip()) for i,l in enumerate(code) if '"'+key+'"' in l]; notes.append('捕获绑定弹窗提示；输入键值原样。')
    else:
        search=key
        references=[(i+1,l.strip()) for i,l in enumerate(code) if '"'+key+'"' in l]
        unit='不适用；界面标签/提示'
        if section=='startup':
            notes.append('逐条对照启动配置字段及选项；纹理/阴影设置表示渲染分辨率、滤波或槽数，系统名用于显示而非资源ID。')
            if key.startswith('Solar System') or key.startswith('Earth Only'):
                for p in Path(r'F:\系统应用\Kitten Space Agency\Content\Core').glob('*System*.xml'):
                    if key in p.read_text(encoding='utf-8'): notes.append('当前系统名定义：Content/Core/'+p.name)
            if key in ('Solar System (Dense)','Solar System (Lite)','Solar System (Light)','VeryLow'):
                status='defer'; notes.append('旧版别名未在5602当前显示源文确认，保留原文。')
        else:
            notes.append('逐条对照所示按钮/区域/说明原文；未改变操作回调，状态提示和玩家内容分开。')
            if key=='DISPLAY': notes.append('导航译显示；垂直同步下的DISPLAY帧率值由独立Settings.VsyncFpsPreview显示显示器刷新率。')
            if key=='CAMERA AND FRAME': notes.append('位于显示页相机和帧率分组，Frame此处为渲染帧，不是参考坐标系。')
            if 'Fuel' in key or 'Donor' in key: notes.append('推进剂管线、接口和储箱的连接/流动条件；包含燃料及氧化剂，保留同飞船/同部件等条件。')
            if key=='DEBUG': status='defer'; notes.append('旧硬编码区域暂缓；native Debug另有明确ID。')
        if not references and status=='reviewed' and not (section=='startup' and key in ('High','Medium','Low','Very Low','Solar System','Earth Only','Solar System + Interstellar','Earth Only + Interstellar','Testing','Sandbox','None')):
            status='defer'; notes.append('当前源文未直接匹配且尚未核实动态生成路径，暂缓。')
    if key in fixes:
        r['chinese'],note=fixes[key]; notes.append(note)
    if section=='literals' and key in ('GRAPHICS','AUDIO','INTERFACE','CONTROLS','SIMULATION','CONSOLE','MODS'):
        status='reviewed'
        notes=['已逐条对照SettingsTab枚举：45539-45549；BuildNavRailLabels:46860-46865通过GetName再ToUpperInvariant动态生成这些导航标签，所以没有ldstr裸字符串。语义分别为图形、声音、界面、控制、模拟、控制台、模组；不改导航索引。']
        references=[(45539,'SettingsTab'),(46860,'BuildNavRailLabels')]
    if section=='literals' and key in ('START KSA','ENABLE','NO'):
        status='reviewed'
        notes=['逐条对照20815/21141/21147 Popup.CreateButton(Start KSA/Enable/No)；PopupButton绘制转换为大写再调用ConsoleWidgets。只改按钮显示，启动/启用/拒绝回调原样。']
        references=[(20815,'PopupButtonStart'),(21141,'PopupButtonYes'),(21147,'PopupButtonNo')]
    if section=='native' and r['english']!=native[key]['source']: raise ValueError('Native source mismatch '+key)
    evidence=[f'.tools/inspection/KSA.decompiled.cs:{line}; symbol/source key {search}' for line,text in references[:8]]
    if not evidence: evidence=['5602 source search: no direct match; review status states dynamic or deferred limit']
    r.update(review_status=status,reviewer='root-native-controls-review',evidence=evidence,units_or_frame=unit,notes=' '.join(notes),
             original_chinese=old,visual_status='not-tested-per-entry',review_scope='root semantic review; native-controls subagent stopped at usage limit before saving; not claimed as completed independent review')
    result.append(r)
out=root/'translations/review-native-controls.jsonl'
out.write_text(''.join(json.dumps(r,ensure_ascii=False)+'\n' for r in result),encoding='utf-8')
print({'rows':len(result),'reviewed':sum(r['review_status']=='reviewed' for r in result),'deferred':sum(r['review_status']=='defer' for r in result)})
