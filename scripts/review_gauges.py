"""Explicit 5602 gauge-label review decisions; no heuristic translation."""
from pathlib import Path
import json
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
rows=[json.loads(s) for s in (ROOT/'work/translation-candidates-5602.jsonl').read_text(encoding='utf-8').splitlines()]
sources={r['source']:r for r in rows if r.get('field')=='Label'}
# Kept short to fit the existing gauge rectangles. Definitions come from the
# adjacent tooltip/action binding and the 5602 rendering/control implementation.
decisions={
 'TARGET':('目标','所选交会目标；动态对象名称另显示，不修改名称。'),
 'CLOSEST APPROACH':('最近交会','交会最近距离/时刻分组标题，非接触碰撞。'),
 'RELATIVE':('相对','修饰交会速度读数；相对目标的运动。'),
 'VELOCITY':('速度','速度量读数；数值与单位原样。'),
 'DISTANCE':('距离','目标间距离，单位保持原格式。'),
 'TOWARD':('朝向','对准目标方向，FlightComputer Toward目标模式。'),
 'ANTIVEL':('逆相速','相对目标速度的反方向；不是绕主天体逆行。'),
 'AWAY':('背向','背离目标方向，与相对速度方向分开。'),
 'ALIGN':('对齐','对接姿态对齐；不表示自动执行对接。'),
 'RATE':('消旋','FlightComputer RateHold/auto-trim将指定参考系中的转动抑制至零；不是任意保持角速度。'),
 'BURN':('机动','计划推力机动；与推进反应的化学燃烧区分。'),
 'CONTROL':('控制','仪表控制分组标题。'),
 'TIME TO MARK':('至标记','距机动标记的时间；时间单位原样。'),
 'START BURN IN':('点火倒计时','距计划机动推力开始的时间；不改变计划时刻。'),
 'BURN TIME':('机动时长','连续推力作用持续时间，非引擎点火延迟。'),
 'DELTAV':('Δv','速度增量；保留专业符号，与瞬时速度区分。'),
 'AUTO':('自动','自动机动控制开关；触发回调原样。'),
 'WARP':('快进','模拟时间快进控制，非空间曲速。'),
 'DELETE':('删除','机动删除按钮；只翻标签，回调和确认原样。'),
 'POS':('正向','PositiveDv：机头对准机动Δv方向。'),
 'NEG':('反向','NegativeDv：机头背向机动Δv方向。'),
 'GAME TIME':('游戏时间','模拟宇宙时间读数，非实际墙钟。'),
 'TIME TO':('剩余时间','相对于所显示事件的倒计时，不改时间值。'),
 'PERIOD':('周期','轨道或机动上下文的周期读数。'),
 'MULT':('倍数','机动编辑Δv调整倍数标签，不译为质量。'),
 'PROGDE':('顺行','VLF顺行轴沿轨道速度。'),
 'NORMAL':('法向','轨道角动量正方向，与径向及向外轴区分。'),
 'OUT':('向外','VLF轨道面内且垂直速度的向外轴，不等同径向外。'),
 'IN':('向内','VLF向外轴的反向，不等同径向内。'),
 'TIME':('时间','机动时刻标签，时间单位原样。'),
 'XFER':('转移','转移计划操作按钮，非资源转输。'),
 'ENGINE':('发动机','发动机仪表标题；发动机ID和型号保留。'),
 'THROTTLE':('油门','推力指令比例，非推进剂节流数值改动。'),
 'PROPELLANT':('推进剂','推进反应所用物质，包含燃料与氧化剂，非仅燃料。'),
 'IGNITE':('点火','发动机启动按钮，原回调保留。'),
 'SHUTDN':('关机','发动机关闭按钮，原回调保留。'),
 'MODE':('模式','所示控制模式，枚举和持久化键保留。'),
 'DIRECT':('直接','手动RCS连续推力或EVA直接控制；两者都表示直接操纵。'),
 'VIEW':('视角','EVA按相机视角操纵，非视图菜单。'),
 'MANUAL':('手动','手动控制模式；不改变姿态或推进计算。'),
 'PERIAPSIS':('近拱点','一般主天体轨道，非限定地球；不强行附高度。'),
 'APOAPSIS':('远拱点','一般主天体轨道，非限定地球；不强行附高度。'),
 'ACTIVATE':('启用','自动控制可用状态/开关标签。'),
 'MANUAL THRUST':('手动推力','手动RCS连续/脉冲策略分组。'),
 'PROFILE':('策略','自动驾驶姿态控制Strict/Balanced/Relaxed策略，不是推力曲线。'),
 'ROLL':('滚转','绕飞行器前向轴的转动/控制策略。'),
 'FWD':('向前','地表相对速度方向；具体方向由SURF模式说明。'),
 'UP':('向上','远离地表方向，E N U局地参考系。'),
 'BACK':('向后','地表相对速度反方向。'),
 'DOWN':('向下','朝向地表方向。'),
 'PULSE':('脉冲','细粒度RCS推力脉冲策略，与电学脉冲不同。'),
 'AHEAD':('前向','LVLH轨道局地水平前向，非有径向分量的速度顺行。'),
 'BEHIND':('后向','LVLH局地水平前向的反向。'),
 'RAD OUT':('径向外','径向远离主天体，和VLF Outward区分。'),
 'RAD IN':('径向内','径向朝向主天体，和VLF Inward区分。'),
 'STRICT':('严格','高响应高精度高推进剂消耗策略。'),
 'BALNCE':('均衡','中等响应精度和推进剂消耗策略。'),
 'RELAX':('宽松','低响应精度和推进剂消耗策略。'),
 'RETRO':('逆行','轨道速度的反方向。'),
 'ANTINML':('反法向','轨道角动量负方向。'),
 'ANY':('自由','滚转Decoupled策略：仅抑制滚转角速度，不锁定滚转角。'),
 'NORM':('正常','滚转Up策略：上方向按所选姿态目标。'),
 'INVERT':('倒置','滚转Down策略：与正常策略反向。'),
 'RESOURCES':('资源','资源仪表标题，不改物质ID。'),
 'YEARS':('年','显示时间单位名；持续时间数值原样。'),
 'DAYS':('天','显示时间单位名；持续时间数值原样。'),
 'HRS':('时','显示小时缩写；不改秒数换算。'),
 'MINS':('分','显示分钟缩写；不改秒数换算。'),
 'SECS':('秒','显示秒缩写。'),
 'SIM SPEED':('模拟速度','时间缩放倍数；非飞行器速度。'),
 'PITCH':('俯仰','姿态俯仰角读数；坐标定义和单位原样。'),
 'YAW':('偏航','姿态偏航角读数；坐标定义和单位原样。'),
 'SPEED':('速度','速度读数；轨道/地表参考系由原仪表模式决定。'),
}
preserve={
 'TWR':'推重比通用缩写，保留以避免挤压布局。','RCS':'反作用控制系统通用缩写，保留。',
 'ORBT':'Lvlh参考系的原模式缩写；保留并用中文tooltip解释。',
 'OVEL':'VlfBody轨道速度参考系缩写；保留并用中文tooltip解释。',
 'SURF':'EnuBody地表参考系缩写；保留并用中文tooltip解释。',
 'STAR':'EclBody星体/黄道惯性参考系原缩写，保留。',
 'TGT':'tooltip目标方向与Navball Dock实现有歧义；保留原缩写，待进一步运行确认。',
 'UT':'通用宇宙时间缩写，保留。','RES':'Resources按钮短缩写，保留以避免布局挤压。',
}
result=[]
for source,record in sorted(sources.items()):
    zh,note=decisions.get(source,(source,preserve.get(source,'未知上下文，保留原文。')))
    row=dict(id='gauges:'+source,section='gauges',key=source,english=source,chinese=zh,source_version='2026.10.13.5602',
             translation_origin='KSA-CHINESE explicit 5602 review',review_status='reviewed' if source in decisions else 'preserve',reviewer='root-gauge-review',
             evidence=[f"{record['file']} element {record['element_ordinal']} Label; adjacent FlightComputer binding/Tooltip",'GaugeCanvas.DrawWidgetText 50305; FlightComputerAttitudeTrackTargetEx.GetTarget 39334-39343'],
             units_or_frame='原数字、单位、坐标计算保持；参考系见notes',notes=note,visual_status='pending-final-build-screenshot')
    result.append(row)
(ROOT/'translations/review-gauges.jsonl').write_text(''.join(json.dumps(r,ensure_ascii=False)+'\n' for r in result),encoding='utf-8')
print({'total':len(result),'translated':sum(r['review_status']=='reviewed' for r in result),'preserved':sum(r['review_status']=='preserve' for r in result)})
