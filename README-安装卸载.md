# KSA Chinese 0.1.0，适配 5602

这是本地制作的非官方**部分汉化预览包**。只支持 Windows x64、KSA `2026.10.13.5602` 和随包 `compatibility.json` 列出的精确文件哈希。游戏更新或依赖发生变化时会安全停止，不能靠改版本号继续用。

## 安装与启动

1. 关闭游戏，完整解压 ZIP 到普通文件夹，不要解压到游戏目录。
2. 需要 Microsoft .NET 10 x64 Runtime。已安装 .NET 10 SDK 的机器也可运行。
3. 双击 `installer/Install.cmd`，输入含 `KSA.dll` 的游戏目录。也可在 PowerShell 使用：

   ```powershell
   .\installer\Install.ps1 -GameDir 'F:\系统应用\Kitten Space Agency'
   ```

4. 默认安装位置为 `%LOCALAPPDATA%\KsaChinese\Build5602`。双击该目录的 `Launch.cmd`。
5. 菜单 `Language` 可即时选择 `English` 或 `简体中文`。当前选择只对本次运行生效；要改变默认语言，可在关闭游戏时修改 `Profile/mods/KsaChinese/language.json` 中的 `locale` 为 `en-US` 或 `zh-CN`。

安装器使用**新的独立玩家目录 `Profile/`**。原有 `Documents/My Games/Kitten Space Agency` 的配置、Mod 清单和个人存档不会被读取、复制或修改。第一次从汉化入口启动看到新档案是正常的。原版 `KSA.exe`／原桌面快捷方式始终可用。

自定义安装位置可传 `-Destination 'D:\KsaChinese5602'`。目标必须是尚不存在的目录；安装器拒绝覆盖已有档案，也拒绝安装进游戏本体目录。本版不自动迁移个人存档。

## 卸载与回退

关闭游戏，双击安装目录的 `Uninstall.cmd`。它将本项目 Mod 移到安装目录的 `Backups/`，并将这份独立档案的清单恢复为只有 `Core`。`Launch.cmd` 此后继续用英文打开这份独立档案，保存的游戏不因删除汉化代码而被改写。

要完全删除加载器及档案，先自行保留需要的 `Profile/saves`、`Profile/Vehicles`，关闭游戏后删除整个独立安装目录。本版不会替你删除存档。要回到原来的个人游戏环境，直接使用原版入口。

## 字体与范围

中文 UI、提示和仪表短标签使用随包、重新命名的 Noto Sans SC 子集。字体只覆盖本包文本及常用符号，任意玩家自定义汉字名称不保证都有字形。启用本汉化时字体重建会选用随包字体。

部分英文缩写、单位、型号、玩家名和动态物质名有意保留。存在源文与实现歧义、旧版入口未匹配或尚未审定的词条会回退英文。绘制效果、全部按钮、不同分辨率、其他 Mod 及长时间运行的限制见 `reports/制作与验证-5602.md`。

源码、逐条复核记录及验证报告随包提供。第三方来源和许可见 `THIRD_PARTY.md` 与 `LICENSES/`。本包不包含 KSA 游戏本体、原始 Core 资源、KSP 字典、反编译源码或个人存档。
