# KSA Chinese：5638 简体中文汉化

为 Kitten Space Agency 提供独立显示层 Mod、逐条审核词典、中文字体及便携启动器。当前适配精确版本 **2026.10.19.5638** 与记录的依赖哈希。

[下载 v0.1.6](https://github.com/kuxiaowo/KSA-CHINESE/releases/tag/v0.1.6)。已被社区目录收录，刷新 Borea 目录后搜索 **KsaChinese**，选择目标实例安装。手动安装时下载 **KsaChinese-0.1.6.zip**，将其中 KsaChinese 文件夹安装到所选档案的 mods 目录，见 [生态安装与迁移](README-生态安装.md)。标准包只含汉化插件、词典、字体和许可；需要单独安装 StarMap 0.4.8 和 .NET 10 x64 Runtime。[Ahwoo 帖子](https://forums.ahwoo.com/forums/kitten-space-agency/mod-releases/ksa-chinese-0-1-6-simplified-chinese-ui-for-ksa-5638.1440/) · [已合并的收录 PR](https://github.com/KSAModding/content-index/pull/223)。

也保留 [便携包](README-便携Mod.md)：完整解压到含 KSA.exe 的游戏目录，运行 `KsaChinese/KsaChineseLauncher.exe`。原 KSA.exe 不执行汉化代码，包不替换游戏 DLL。迁移到 Borea 前备份旧 Profile，移走旧 Content/KsaChinese，避免同名副本覆盖实例版本。

0.1.6 增加标准模组包、社区索引模板，以及按档案保存的语言选择；不同实例独立，更新模组文件不会覆盖个人语言设置。接入进度与验证边界见 [生态接入报告](reports/0.1.6-生态接入与验证.md)。

0.1.5 新增极光和极光质量设置、辐射仪表与提示、磁层地图开关、缺失或变化部件告警，以及 5638 的 93 条更新正文；保留此前 5602 的 135 条正文。迁移 BRUTAL 依赖、头像相机和银道标签入口，并使用 StarMap 0.4.8。地图开关在实际截图中定位到小方法内联问题，现改在调用处翻译可见文案，保留独立控件 ID。

现有译文有 4786 条启用审核记录，0.1.6 有 1059 项无图形检查通过。翻译覆盖范围见 [0.1.5 适配与补译报告](reports/0.1.5-5638适配与补译.md)，本轮安装与实例验证见 [0.1.6 报告](reports/0.1.6-生态接入与验证.md)；数量不代表全游戏完成率。更早的更新正文、原生工具库和异常文字、模型与贴图印字，以及完整界面/状态遍历仍有未完成范围。

型号、规格代号、控件和资源 ID、数值、单位、存档键及玩家自定义名称保留。只改显示层，未知版本或依赖哈希拒绝启动，缺译文回退英文。默认档案目录为 `KsaChinese/Profile`，个人存档不自动搬动；升级时保留既有 Profile 和 launcher-settings.json。

历史发布 0.1.0、0.1.1、0.1.2、0.1.4 均已按维护者要求设为正式 Release；0.1.4 及以前适配 5602，不能在 5638 上使用。正式发布状态不代表汉化已全量完成。0.1.3 只曾作本地验证，没有独立 Release。旧版报告见 [0.1.4](reports/0.1.4-扩展补译与验证.md) 和 [0.1.3](reports/0.1.3-部件补译验证.md)。

开发需要 .NET 10 SDK、合法的本地 KSA 5638 安装及 StarMap 0.4.8：

```powershell
.\scripts\build.ps1 -GameDir 'C:\Games\Kitten Space Agency' -LoaderDir 'C:\Tools\StarMap-0.4.8' -Validate
```

Python 工具优先在 conda 环境中执行。`scripts/build_translations.py` 从 review-*.jsonl 生成运行词典、resx 及 [当前汇总底稿](translations/reviewed-5638.jsonl)；保留旧版汇总用于追溯。`scripts/build_font.py` 从词典和插件源码收集字形，重建需要 fontTools 及固定哈希的 Noto 源文件。`scripts/package_mod.py` 按白名单生成标准包；`scripts/package_portable.py` 生成含启动器和加载器的便携包。[社区接入文件](ecosystem/README.md)包含已发布帖文、收录记录、索引模板和验证复现说明。

扩展显示清单为 `assets/KsaChinese/display-sites.json`。可用本机审计数据执行 `scripts/build_display_sites.py --audit-dir <审计目录> --game-dir <游戏目录> --check` 校验。反编译材料、候选与游戏资源只供本机检查，不随源码或包分发。

分支流程为 developing → PR → main；代码和译文遵循 MIT，字体遵循 OFL，来源见 [第三方许可](THIRD_PARTY.md)。测试只用工作区独立档案，不读取个人存档；原 [可行性调查](reports/汉化可行性评估-2026-10-13.md) 针对 5602，不作为新版运行结论。
