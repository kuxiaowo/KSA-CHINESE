KSA Chinese 0.1.0 — 5602 简体中文本地预览

仅适配 compatibility.json 中检查过的 KSA 2026.10.13.5602 文件。
通过 StarMap 0.4.7 加载，需要 .NET 10 x64 Runtime。
完整安装包 README-安装卸载.md 说明了独立玩家目录、启动和卸载方法。

Language 菜单即时切换 English/简体中文，默认值由 language.json 决定。
原生文本从 JSON native 字典通过提供器接入；zh-CN.resx 是同底稿导出的资源表，
resx 不单独激活显示，主要界面也需要代码补丁。
未审、缺失或上下文冲突的译文回退原文；未知游戏或依赖哈希安全禁用。
本版是部分汉化，覆盖与验证限制见项目报告。

部分代码及初始译文来自 KsaUiLanguages（MIT），经本项目重新适配与复核；
字体使用 Noto Sans SC 子集（OFL）。所有原始署名和许可证均保留。
本包不含游戏DLL、原Core资源、反编译源码、KSP字典或个人存档。