# KSA Chinese：5602 简体中文汉化预览

为 Kitten Space Agency 提供独立代码 Mod、审核词典、中文字体和安装/卸载脚本。适配精确版本 `2026.10.13.5602` 与记录的依赖哈希。

[下载 v0.1.4 便携 Mod](https://github.com/kuxiaowo/KSA-CHINESE/releases/tag/v0.1.4)。完整解压到游戏目录，运行 `KsaChinese/KsaChineseLauncher.exe`。文件位于 `Content/KsaChinese`，随包加载器自动配置；说明见 [便携 Mod](README-便携Mod.md)。原 `KSA.exe` 本身不执行代码汉化。旧版独立安装包仍保留在 v0.1.0 Release。

先阅读 [安装卸载](README-安装卸载.md)、[制作与验证](reports/制作与验证-5602.md)、[逐条底稿](translations/reviewed-5602.jsonl) 和 [第三方许可](THIRD_PARTY.md)。这是部分汉化预览，详细图形/存档与未覆盖限制以报告为准。

运行需要 .NET 10 x64 Runtime。便携包新增自己的目录，不替换游戏文件。默认使用 `KsaChinese/Profile`，可指定已有汉化档案。未知版本/哈希拒绝启动；缺译文回退英文。

开发：安装 .NET 10 SDK，准备合法的本地 KSA 安装与 StarMap 0.4.7，运行 `.\scripts\build.ps1 -GameDir 'C:\Games\Kitten Space Agency' -LoaderDir 'C:\Tools\StarMap-0.4.7' -Validate`。Python 工具优先在 conda 环境中执行，字体重建需要 `fontTools` 和被忽略的固定 Noto 源文件。`scripts/build_translations.py` 从逐条审核记录生成 JSON 与 resx，`scripts/build_font.py` 检查字形，`scripts/package_portable.py` 按白名单生成便携包。源文定位所依赖的反编译材料和调查候选仅存于本机，不随包。

此前 v0.1.2 启用1660条语义审核词条；131项无图形检查通过，并抽查中文HUD、设置、规划及中英切换。v0.1.2 补齐仪表右键菜单，并将启动星际预设名称改为“太阳系及邻近恒星”和“太阳系及邻近恒星（扩充版）”。v0.1.1 的分级显示、字体去重与存档回退验证记录保留。编辑器/部件窗口缺视觉证据，全部按钮与长期稳定性尚未全面验证。

当前版本为 **v0.1.4**，在 [0.1.3 部件预览](reports/0.1.3-部件补译验证.md) 上继续补译：180 个主部件与 470 个子部件的显示映射、物质与相态、反应、药柱、材料、仪表提示和高级窗口，另加入当前 5602 更新说明的 135 条正文。型号、规格代号、真实资源键、玩家名称和数值保持原值。4664 条启用审核记录、1038 项无图形检查及隔离运行范围见 [0.1.4 检查与验证报告](reports/0.1.4-扩展补译与验证.md)。条目数量不是全游戏完成率；更早的更新正文、原生工具库和贴图印字等限制均在报告中列明。

便携包名为 `KsaChinese-0.1.4-5602-Portable.zip`，适配游戏 **5602**，不支持其他版本；SHA-256 见 Release 附件。发布不自动更新个人游戏目录。2026-10-23 官方更新接口返回 **5638**，其适配需要单独检查，不能直接放宽版本保护。

![中文HUD](reports/screenshots/hud-zh.png)

阅读 [可行性评估](reports/汉化可行性评估-2026-10-13.md) 和 [本机统计](reports/local-audit-2026-10-13.json)。本次调查针对 KSA **2026.10.13.5602**。

`scripts/audit_localization.py` 只读取指定的 KSA、KSP 与玩家目录，输出统计和待审核候选清单。候选清单、反编译材料及第三方调查工具仅保存在被 Git 忽略的 `work/`、`.tools/`，不能当作汉化完成率或可分发游戏源码。

分支流程：`developing → PR → main`。代码和译文遵循 MIT，字体遵循 OFL；复用来源与署名见 `THIRD_PARTY.md`。公开调查/验证记录中的个人本机路径已脱敏；记录保留原始游戏与资源哈希。

扩展显示清单以 `assets/KsaChinese/display-sites.json` 为准。维护者可用本机审计输出执行 `scripts/build_display_sites.py --audit-dir <本机审计目录> --game-dir <游戏目录> --check` 校验其与审核底稿的一致性；审计输入不随项目分发。字体生成同时收集词典和插件源码中的中文字形，避免代码内的菜单文字遗漏。
