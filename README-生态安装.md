# StarMap / Borea 安装与迁移

标准模组包为 **KsaChinese-0.1.6.zip**，只适配 Windows x64 的 KSA **2026.10.19.5638**，需要 .NET 10 x64 Runtime 和 **StarMap 0.4.8**。包不附带加载器、启动器或游戏文件。代码和译文为 MIT；Noto Sans SC 字体子集为 OFL-1.1，来源见包内 THIRD_PARTY.md 和许可文件。

## Borea

2026-10-23 已完成社区收录：[Ahwoo 帖子](https://forums.ahwoo.com/forums/kitten-space-agency/mod-releases/ksa-chinese-0-1-6-simplified-chinese-ui-for-ksa-5638.1440/) 和 [索引 PR #223](https://github.com/KSAModding/content-index/pull/223)。刷新 Borea 公共目录后搜索 `KsaChinese`，选择目标实例并安装，再由 Borea 启动该实例。0.1.6 的公共目录搜索、下载和隔离实例安装已通过 Borea 0.5.1 原版源码组件验证；本次未进行桌面 GUI 点击验收。

也可以在 Borea 创建实例、安装 StarMap，把 ZIP 中的整个 `KsaChinese` 文件夹放到该实例的 `mods` 目录。启用该模组后由 Borea 启动；手动安装的文件需要使用 Borea 的扫描/接管功能才能由它管理更新。不要把便携包作为标准模组导入。

## 直接使用 StarMap

从 [StarMap 官方发布](https://github.com/StarMapLoader/StarMap/releases/tag/0.4.8) 安装加载器，配置其 GameLocation 指向游戏。把 ZIP 中的 `KsaChinese` 文件夹放入所选玩家档案的 `mods` 文件夹；默认档案为 `Documents/My Games/Kitten Space Agency`，Borea 实例则使用自己的目录。

在游戏的模组管理界面启用 `KsaChinese`，或在该档案的 `manifest.toml` 中加入以下条目，保留已有 Core 和其他模组条目：

```toml
[[mods]]
id = "KsaChinese"
enabled = true
```

通过 StarMap 启动。使用 `KSA.exe` 启动不会执行汉化插件。

## 从旧便携包迁移

1. 关闭游戏。备份旧 `KsaChinese/Profile`，其中可能有你的存档、载具和设置。
2. 将游戏目录中的旧 `Content/KsaChinese` 整个文件夹移出 `Content` 作为备份。**不要同时保留 Content 和实例 mods 中的两个同名模组**，否则可能优先加载旧副本。
3. 安装标准模组包，使用 Borea 或独立 StarMap 启动。新的 Borea 实例不会自动读取旧便携档案；需要旧存档时，使用 Borea 的导入功能并先做好备份。
4. 确认迁移成功后，可移走旧启动器目录；在此之前保留其 Profile。不要直接删除含个人数据的整个 `KsaChinese` 目录。

## 设置、更新和卸载

“语言 / Language”菜单即时切换 English / 简体中文。0.1.6 起，菜单选择保存到**当前档案**的 `KsaChinese/settings.json`，不同实例互不影响，模组更新不会覆盖它。没有个人设置时使用包内 language.json 的默认值。日志位于当前档案 `logs/KsaChinese.log`；模组自带词典和字体从实际 DLL 目录读取，支持 Borea 的共享模组文件目录。

由 Borea 安装的模组使用 Borea 更新、禁用或卸载；手动安装则在游戏关闭后移走 `mods/KsaChinese` 并移除/禁用对应清单条目。卸载保留当前档案的个人设置和存档；若要重置语言，可单独移走 `KsaChinese/settings.json`。

本项目是部分汉化。未翻译文字回退原文；未知游戏版本或不匹配的依赖哈希会禁用汉化。全部界面、旧版更新正文、原生工具库、异常文字和贴图印字仍有未完成范围。跨模组界面翻译与其他汉化插件并用未获保证。
