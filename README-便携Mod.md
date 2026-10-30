# 5707 简体中文便携 Mod

当前包为 `KsaChinese-0.1.7-5707-Portable.zip`，发布于项目 v0.1.7 Release。已有 StarMap/Borea 时优先选择独立的标准模组包，见 [生态安装与迁移](README-生态安装.md)；不要把整个便携包导入 Borea。完整解压便携包 **到含 `KSA.exe` 的游戏目录**。最终位置应为：

```text
Kitten Space Agency/
  KSA.exe
  Content/KsaChinese/mod.toml
  KsaChinese/KsaChineseLauncher.exe
  KsaChinese/Loader/StarMap.exe
```

需要 Microsoft .NET 10 x64 Runtime。双击 `KsaChinese/KsaChineseLauncher.exe`，或给它创建一个桌面快捷方式。第一次启动自动配置随包加载器，无需输入游戏路径或修改 Mod 清单。安装时不要覆盖已有的 `launcher-settings.json` 或 `Profile`。

本版随包加载器为 StarMap 0.4.8，修复了 5638 的 BRUTAL 依赖更名；旧 0.4.7 不能用于本版。

汉化本身是 StarMap 代码 Mod，便携包将文件放在 `Content/KsaChinese`；加载器仅用于执行 Mod。**原始 `KSA.exe` 不会执行汉化代码**。没有替换原程序，也没有修改游戏 DLL。已使用 StarMap 的玩家请选标准模组包，安装到所选档案的 `mods/KsaChinese`，并在自己的 Mod 清单中启用 `KsaChinese`；不要同时留下 Content 和 mods 两份。

默认将档案保存在 `KsaChinese/Profile`，原来的个人存档不搬动。`launcher-settings.json` 可指定已有档案路径，例如 `{"profile":"D:/KSA-Profile"}`；只在关闭游戏后修改，且已有清单需要启用 Core 和 KsaChinese。本机从旧汉化升级时继续指定旧档案，避免产生另一个新档案。语言可在游戏菜单切换，日常只需一个入口。

启动失败会显示错误，并记录在 `KsaChinese/logs`。加载器的 stdout/stderr 分开保存。仅支持 `2026.10.25.5707` 和适配清单里的精确哈希；更新游戏后文件不符时停止加载，等待新版适配。Mod 文件是否在更新后保留取决于游戏更新器；建议先备份 `Profile` 和 `launcher-settings.json`，不要依赖更新器保留额外目录。

卸载：关闭游戏，先备份需要的档案，将 `Content/KsaChinese` 移出 `Content`，将桌面快捷方式改回原 `KSA.exe`。需要保留存档时保留 `KsaChinese/Profile`；不要把它连同加载器一起删掉。若指定了旧汉化档案，该档案仍位于旧位置。

此前版本补入极光设置、辐射仪表、磁层地图及缺失部件告警，并迁移新版地图、头像相机与银道标签入口。在此前启动、分级和部件翻译的基础上，扩展 650 个主、子部件的显示映射，补入物质、反应、工程与地表材料、药柱、仪表提示及高级工具窗口，并翻译5638 的 93 条更新正文，保留 5602 的 135 条更新正文。型号、规格代号、真实资源键和玩家名称保留；180 个主部件定义包含隐藏/测试资源，不代表编辑器可选部件数。星际预设显示为“太阳系及邻近恒星”，Dense 为“太阳系及邻近恒星（扩充版）”。`VAC/ATM` 是计算性能采用的工况，不代表飞船当前所在环境。

5051 条启用审核记录不能视为全游戏翻译完成。历史版本的编辑器已经隔离运行，但游戏自带截图接口不支持编辑器，尚无本轮编辑器视觉验收；更早的更新正文、原生工具库及贴图印字也未完成全面处理。本轮新增空间天气、HDR、乘员辐射与退役、储罐压力和屏蔽材料，以及 5707 的全部 156 条更新正文。详见 `reports/0.1.7-5707适配与补译.md`。

本包不含游戏本体、Core 原素材、反编译源码或个人存档。许可见 `LICENSES`，代码为 MIT、字体为 OFL。验证记录见项目 `reports`；源码按 developing → PR → main 合并。
