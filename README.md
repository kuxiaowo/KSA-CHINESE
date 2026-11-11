# KSA Chinese 简体中文汉化

为 Kitten Space Agency 提供简体中文显示层 Mod、词典、字体和便携启动器。0.1.8 开发版适配 Windows x64 **KSA 2026.11.10.5801** 与 **StarMap 0.5.4**。

[公开发行版 v0.1.7](https://github.com/kuxiaowo/KSA-CHINESE/releases/tag/v0.1.7) 对应 5707。5801 可以从本源码构建。标准包放入所选档案的 mods 目录，便携包从独立启动器进入游戏。 [标准安装](README-生态安装.md) · [便携安装](README-便携Mod.md)

需要更多界面语言时可选独立的 KSA Babel。同一档案启用其中一个 Mod。 [KSA Babel](https://github.com/kuxiaowo/KSA-Babel)

5801 补译生命保障、乘员状态、居住区与 EVA 物资转移、辐射和生命保障开关、编辑器撤销重做、部件变体、储罐提示与 HDR 分项亮度。新版本的 242 行更新日志已逐行核对。

运行验证及剩余覆盖见 [5801 适配报告](reports/0.1.8-5801适配与验证.md)。

型号、规格代号、控件和资源 ID、数值、单位、存档键及玩家自定义名称保留。只改显示层，未知版本或依赖哈希拒绝启用，缺译文回退英文。语言选择按档案保存，不随模组更新覆盖；个人存档不会自动搬动。

Borea 的 KsaChinese 公共条目当前指向既有发布。旧版本的适配范围及验证保存在各自报告中。 [5707 适配报告](reports/0.1.7-5707适配与补译.md)

开发需要 .NET 10 SDK、合法的本地 KSA 5801 安装及 StarMap 0.5.4。

```powershell
python scripts/build_translations.py
.\scripts\build.ps1 -GameDir 'D:\Games\Kitten Space Agency' -LoaderDir 'D:\Tools\StarMap-0.5.4' -Validate
python scripts/package_mod.py
python scripts/package_portable.py
```

Python 工具优先使用 conda。`build_translations.py` 生成运行词典、resx 和审核底稿。`build_font.py` 需要 fontTools 和固定哈希的 Noto 源字体。`audit_parts.py` 按 Core 清单顺序核对合并后的显示字段。 [5801 审核底稿](translations/reviewed-5801.jsonl)

开发流程为 developing → PR → main。代码及作者译文遵循 MIT，字体遵循 OFL，保留 KsaUiLanguages 等来源署名，见 [第三方许可](THIRD_PARTY.md)。游戏截图另依 [截图许可说明](LICENSES/KSA-Screenshot.txt)，不将游戏画面宣称为 MIT。原 [0.1.6 生态报告](reports/0.1.6-生态接入与验证.md)和更早报告仅代表对应历史版本。
