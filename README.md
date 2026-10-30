# KSA Chinese：5707 简体中文汉化

为 Kitten Space Agency 提供显示层 Mod、逐条审核词典、中文字体及便携启动器。当前适配精确版本 **2026.10.25.5707 / Windows x64** 与记录的依赖哈希，使用 **StarMap 0.4.8**。

[下载 v0.1.7](https://github.com/kuxiaowo/KSA-CHINESE/releases/tag/v0.1.7)。已有 StarMap/Borea 时使用 **KsaChinese-0.1.7.zip**，将其中 KsaChinese 文件夹安装到所选档案的 mods 目录；详见 [生态安装与迁移](README-生态安装.md)。标准包只含插件、词典、字体和许可，需要单独安装 StarMap 0.4.8 和 .NET 10 x64 Runtime。

也提供 [便携包](README-便携Mod.md)：完整解压到含 KSA.exe 的游戏目录，运行 `KsaChinese/KsaChineseLauncher.exe`。保留已有 Profile 和 launcher-settings.json，迁移前备份。独立多语言项目 [KSA Babel](https://github.com/kuxiaowo/KSA-Babel) 提供九种语言；同一档案选择其中一个版本。

0.1.7 补入空间天气与 CME、HDR 设置、乘员辐射与退役、轨道剂量、储罐压力和屏蔽材料，以及 5707 的 156 条更新正文；修复新版乘员名单和储罐显示入口。新增 265 条启用审核记录，总计 5051 条；这是审核底稿数量，不是唯一字符串数量或全游戏完成率。本轮新增译文经过 AI 语境复核，尚未由维护者逐条确认。

本轮无图形验证通过 1101 项检查和 738 个目标引用；独立游戏档案完成中英切换、空间天气、乘员名单、HDR 设置、更新日志、地图及保存重载。证据和限制见 [5707 适配报告](reports/0.1.7-5707适配与补译.md)。更早更新正文、原生库与异常文本、贴图印字、所有状态和分辨率遍历仍未完成；HDR 设置文字检查不等于物理 HDR 输出验收。

型号、规格代号、控件和资源 ID、数值、单位、存档键及玩家自定义名称保留。只改显示层，未知版本或依赖哈希拒绝启用，缺译文回退英文。语言选择按档案保存，不随模组更新覆盖；个人存档不会自动搬动。

社区目录已有独立 KsaChinese 条目。[原 Ahwoo 帖子](https://forums.ahwoo.com/forums/kitten-space-agency/mod-releases/ksa-chinese-0-1-6-simplified-chinese-ui-for-ksa-5638.1440/)及[首次收录 PR](https://github.com/KSAModding/content-index/pull/223)记录 0.1.6 历史发布；本轮索引更新状态以新适配报告中的链接为准。旧 0.1.5/0.1.6 对应 5638，0.1.4 及以前对应 5602，不能用于 5707。旧发布和报告保留，不修改历史兼容范围。

开发需要 .NET 10 SDK、合法的本地 KSA 5707 安装及 StarMap 0.4.8：

```powershell
python scripts/build_translations.py
.\scripts\build.ps1 -GameDir 'D:\Games\Kitten Space Agency' -LoaderDir 'D:\Tools\StarMap-0.4.8' -Validate
python scripts/package_mod.py
python scripts/package_portable.py
```

Python 工具优先使用 conda。`build_translations.py` 从 review-*.jsonl 生成运行词典、resx 和 [5707 审核底稿](translations/reviewed-5707.jsonl)。`build_font.py` 需要 fontTools 和固定哈希的 Noto 源字体。`build_display_sites.py --audit-dir <审计目录> --game-dir <游戏目录> --check` 用本地审计资料核对显示入口；游戏 DLL、反编译资料、测试档案和构建输出不进入源码或玩家包。

开发流程为 developing → PR → main。代码及作者译文遵循 MIT，字体遵循 OFL，保留 KsaUiLanguages 等来源署名，见 [第三方许可](THIRD_PARTY.md)。游戏截图另依 [截图许可说明](LICENSES/KSA-Screenshot.txt)，不将游戏画面宣称为 MIT。原 [0.1.6 生态报告](reports/0.1.6-生态接入与验证.md)和更早报告仅代表对应历史版本。
