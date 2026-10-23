# 已经维护者批准并发布

2026-10-23：[Ahwoo 正式帖子](https://forums.ahwoo.com/forums/kitten-space-agency/mod-releases/ksa-chinese-0-1-6-simplified-chinese-ui-for-ksa-5638.1440/)。以下保留发布正文。

目标：[Ahwoo Mod Releases](https://forums.ahwoo.com/forums/kitten-space-agency/mod-releases/)

建议分类前缀：User Interface

标题：KSA Chinese 0.1.6 — Simplified Chinese UI for KSA 5638

以下为拟发布正文：

---

**KSA Chinese 0.1.6** provides partial Simplified Chinese localization for **Kitten Space Agency 2026.10.19.5638 on Windows x64**.

It translates supported menus, settings, tooltips, part and substance captions, gauges and selected version-history text. A bundled, renamed Noto Sans SC subset supplies Chinese glyphs. Use the **语言 / Language** menu to switch between English and 简体中文; the choice is saved separately for each game profile/Borea instance.

This is an unofficial, partial translation. Older version-history entries, native library/error text, texture lettering and full UI/visual coverage remain unfinished. Technical identifiers, model numbers, units, placeholders and player-supplied names are preserved. Missing translations fall back to the original text. Uninspected game/dependency hashes disable the plugin.

**Download:** [0.1.6 release](https://github.com/kuxiaowo/KSA-CHINESE/releases/tag/v0.1.6). Choose **KsaChinese-0.1.6.zip** for an existing StarMap/Borea installation. This standard ZIP contains no loader or launcher.

**Source:** [KSA-CHINESE](https://github.com/kuxiaowo/KSA-CHINESE/tree/v0.1.6)

**Installation and migration:** [instructions](https://github.com/kuxiaowo/KSA-CHINESE/blob/main/README-%E7%94%9F%E6%80%81%E5%AE%89%E8%A3%85.md)

**Requirements / dependencies:**

- KSA 2026.10.19.5638, Windows x64; other game builds and platforms are not supported by this release.
- [StarMap 0.4.8](https://github.com/StarMapLoader/StarMap/releases/tag/0.4.8), by KlaasWhite, MIT; [forum thread](https://forums.ahwoo.com/threads/starmap-mod-loader.384/). StarMap supplies Harmony for runtime patches.
- [Microsoft .NET 10 x64 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- [Borea](https://github.com/KSAModding/Borea) is an optional content manager. Public catalogue installation becomes available after this listing is accepted into the community index.

**Manual installation:** Extract the `KsaChinese` directory into the chosen profile's `mods` directory, enable `KsaChinese` in that profile's mod manifest, and launch through StarMap. Borea instances have their own profile directories.

**Migrating from an older portable package:** Back up its `KsaChinese/Profile` first. Move the old `<game>/Content/KsaChinese` outside Content before installing into the instance's mods directory, so an older same-id copy cannot shadow the new one. The new instance does not automatically import the portable profile's saves.

**License and attribution:** Code and translations are MIT. Portions of the plugin and the initial translation set derive from [LiX-Works/KsaUiLanguages](https://github.com/LiX-Works/KsaUiLanguages/tree/b504774158cc03b582a0e75bfe94330cb841f11d), commit b504774158cc03b582a0e75bfe94330cb841f11d, MIT; original credits are retained. The renamed font subset derives from [Google Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc), SIL OFL 1.1; the source-font hash and full license are included. The distributed content's SPDX expression is **MIT AND OFL-1.1**. Full texts and credits are included in the ZIP. No game DLLs, original Core assets, personal saves or decompiled game source are distributed.

**Reports:** [Issues](https://github.com/kuxiaowo/KSA-CHINESE/issues). Please include the game version, mod version, installation method, and a description or screenshot of the affected UI. Remove personal information from any logs before sharing.
