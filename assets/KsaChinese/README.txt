KSA Chinese 0.1.9 — 5901 简体中文

仅适配 compatibility.json 中检查过的 KSA 2026.11.16.5901 文件。
通过 StarMap 0.5.4 加载，需要 .NET 10 x64 Runtime。
标准包供 StarMap/Borea 安装，见 README-生态安装.md；加载器需单独安装。
便携包的启动和卸载说明位于游戏目录下 KsaChinese/README-便携Mod.md；
README-安装卸载.md 为 5602 旧版独立安装流程，不适用于本版。

“语言 / Language”菜单即时切换 English/简体中文，默认值由 language.json 决定。
菜单选择保存于当前档案 KsaChinese/settings.json，不写入共享模组文件。
原生文本从 JSON native 字典通过提供器接入；zh-CN.resx 是同底稿导出的资源表，
resx 不单独激活显示，主要界面也需要代码补丁。
未审、缺失或上下文冲突的译文回退原文；未知游戏或依赖哈希安全禁用。
本版是部分汉化，覆盖与验证限制见项目报告。
实际资源键、物理参数和存档不改；贴图印字、原生工具库和全部界面的视觉验收尚未完成。

部分代码及初始译文来自 KsaUiLanguages（MIT），经本项目重新适配与复核；
字体使用 Noto Sans SC 子集（OFL）。所有原始署名和许可证均保留。
本包不含游戏DLL、原Core资源、反编译源码、KSP字典或个人存档。


5901 适配重做的生命保障窗口、物资转移、任务和乘员计划、物资装载与发射警告、乘员倒计时和探测器电源。更新日志包含 311 行正文。
