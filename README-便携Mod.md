# 5602 简体中文便携 Mod

当前源码构建的本地预览包为 `KsaChinese-0.1.4-5602-Portable.zip`，尚未发布；项目中的 v0.1.2 链接是此前的发布入口。完整解压便携包 **到含 `KSA.exe` 的游戏目录**。最终位置应为：

```text
Kitten Space Agency/
  KSA.exe
  Content/KsaChinese/mod.toml
  KsaChinese/KsaChineseLauncher.exe
  KsaChinese/Loader/StarMap.exe
```

需要 Microsoft .NET 10 x64 Runtime。双击 `KsaChinese/KsaChineseLauncher.exe`，或给它创建一个桌面快捷方式。第一次启动自动配置随包加载器，无需输入游戏路径或修改 Mod 清单。安装时不要覆盖已有的 `launcher-settings.json` 或 `Profile`。

汉化本身是 StarMap 代码 Mod，文件放在 `Content/KsaChinese`；加载器仅用于执行 Mod。**原始 `KSA.exe` 不会执行汉化代码**。没有替换原程序，也没有修改游戏 DLL。已使用 StarMap 的玩家可以只放入 `Content/KsaChinese`，并在自己的 Mod 清单中启用 `KsaChinese`。

默认将档案保存在 `KsaChinese/Profile`，原来的个人存档不搬动。`launcher-settings.json` 可指定已有档案路径，例如 `{"profile":"D:/KSA-Profile"}`；只在关闭游戏后修改，且已有清单需要启用 Core 和 KsaChinese。本机从旧汉化升级时继续指定旧档案，避免产生另一个新档案。语言可在游戏菜单切换，日常只需一个入口。

启动失败会显示错误，并记录在 `KsaChinese/logs`。加载器的 stdout/stderr 分开保存。仅支持 `2026.10.13.5602` 和适配清单里的精确哈希；更新游戏后文件不符时停止加载，等待新版适配。Mod 文件是否在更新后保留取决于游戏更新器；建议先备份 `Profile` 和 `launcher-settings.json`，不要依赖更新器保留额外目录。

卸载：关闭游戏，先备份需要的档案，将 `Content/KsaChinese` 移出 `Content`，将桌面快捷方式改回原 `KSA.exe`。需要保留存档时保留 `KsaChinese/Profile`；不要把它连同加载器一起删掉。若指定了旧汉化档案，该档案仍位于旧位置。

本版在此前启动、分级和部件预览的基础上，扩展 650 个主、子部件的显示映射，补入物质、反应、工程与地表材料、药柱、仪表提示及高级工具窗口，并翻译当前 5602 的 135 条更新正文。型号、规格代号、真实资源键和玩家名称保留；180 个主部件定义包含隐藏/测试资源，不代表编辑器可选部件数。星际预设显示为“太阳系及邻近恒星”，Dense 为“太阳系及邻近恒星（扩充版）”。`VAC/ATM` 是计算性能采用的工况，不代表飞船当前所在环境。

4664 条启用审核记录不能视为全游戏翻译完成。编辑器已经隔离运行，但游戏自带截图接口不支持编辑器，尚无本轮编辑器视觉验收；更早的更新正文、原生工具库及贴图印字也未完成全面处理。详见源码 `reports/0.1.4-扩展补译与验证.md`。

本包不含游戏本体、Core 原素材、反编译源码或个人存档。许可见 `LICENSES`，代码为 MIT、字体为 OFL。验证记录见项目 `reports`；源码按 developing → PR → main 合并。
