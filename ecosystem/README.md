# 社区目录接入

本目录保存社区发布内容和验证复现说明。2026-10-23，维护者分别批准发帖和索引 PR：[Ahwoo 帖子](https://forums.ahwoo.com/forums/kitten-space-agency/mod-releases/ksa-chinese-0-1-6-simplified-chinese-ui-for-ksa-5638.1440/) 已发布，[content-index #223](https://github.com/KSAModding/content-index/pull/223) 已通过检查、归属验证并自动合并。其他项目外发布仍需先核实。

- `forum-post.en.md`：已在 Ahwoo Mod Releases 发布的帖子正文，User Interface 分类。
- `KsaChinese.toml.in`：社区索引条目模板，论坛地址明确留空。需要真实帖子地址，不能直接提交模板。
- `index-pr.en.md`：已合并的 KSAModding/content-index PR 说明和最终文件链接。
- `media/ksa-chinese-hud-v2.jpg`：当前目录封面，按维护者选定区域裁切的真实中文游戏截图，512 × 512 JPEG；截图署名、坐标和独立展示说明见 `media/README.md`。此前的 `ksa-chinese-icon-v1.jpg` 保留为历史图标。

目录模板同时维护中文和 English 简介。`abstract` 用于短摘要，`description` 按中文、English、来源与许可排列；当前索引格式没有按客户端语言分别保存简介的字段。`images.icon` 记录本仓库托管图标的 HTTPS 地址、SHA-256、尺寸、字节数及独立图片许可。更新图标时使用新文件名，并一并更新图片记录。

本次按维护者指令将封面更换为选定截图；简介只新增一行 `作者 / Author: [kuxiaowo](https://github.com/kuxiaowo)`，其余正文与摘要不改动。原有贡献者及 MIT/OFL 来源说明继续保留。

展示资料只按已发布的 0.1.6 内容编写；开发中的多语言改动不能提前写成公开版本功能。修改展示资料无需重发插件安装包。2026-10-24，经维护者批准，本仓库 [PR #10](https://github.com/kuxiaowo/KSA-CHINESE/pull/10) 和社区索引 [PR #224](https://github.com/KSAModding/content-index/pull/224) 已合并；[公共快照部署](https://github.com/KSAModding/content-index-releases/actions/runs/40064051771) 成功。公共条目与批准稿全部字段一致，在线图片字节与哈希匹配。

已在正式版 Borea 0.5.1 刷新公共目录并检查：首页卡片、搜索列表图标及双语摘要、详情页顶部图标、中文正文、英文正文及来源许可均正常显示。当前窗口未见缺字方框或文字重叠；其他窗口尺寸、缩放和平台未作视觉验收。记录见 [目录图文验收](../reports/borea-listing-display-validation.json)。后续修改展示资料仍需同步社区条目，只修改本仓库不会自动更新 Borea 的简介。

许可为 MIT AND OFL-1.1；只收录 0.1.6 及以后，避免把旧便携包和旧 5602 支持范围误套入。游戏上下界均为 2026.10.19.5638；平台限定 Windows；StarMap 上下界均为 0.4.8，后续扩大前需测试。

公共快照现已包含 KsaChinese 0.1.6；从该快照搜索、下载并安装到两个隔离实例的 7 项检查通过，见 [公共目录验证](../reports/ecosystem-public-catalog-validation.json) 和 [发布记录](../reports/ecosystem-publication.json)。

社区 CONTRIBUTING.md 与 Schema 要求 `links.forums` 为真实 Ahwoo 帖子 URL。本次渲染命令：

```powershell
python scripts/prepare_listing.py --forums-url 'https://forums.ahwoo.com/forums/kitten-space-agency/mod-releases/ksa-chinese-0-1-6-simplified-chinese-ui-for-ksa-5638.1440/' --output '.tools/content-index/listings/KsaChinese.toml'
```

然后在 content-index 本地副本运行上游 `tools/check_layout.py`、`check_schema.py`、`check_index.py`、`check_license.py`、`check_status.py`、`check_images.py` 和联网 `check_release.py listings/KsaChinese.toml`。只有真实地址和发行包检查都通过，才提交经维护者确认的外部 PR。仓库属于 kuxiaowo，以该账户提交可按上游规则证明发布源所有权；若改用其他账户需另行提供控制权证明。

## 本机复现

准备 `.tools/content-index`、`.tools/content-index-releases` 的上游副本与 `.tools/Borea-source` 的 v0.5.1 源码（提交 284d5cf929c8ede99320f8afc451fa44f12aeadd），以及合法的游戏与 StarMap 0.4.8。本项目不分发上游源码副本或游戏文件。

在 conda 环境安装上游 `tools/requirements.txt`。构建插件并运行原有 Validation 后，执行 `scripts/package_mod.py`、`scripts/prepare_ecosystem_validation.py`。后者只在忽略的本机测试目录使用合成论坛地址生成候选索引，**不代表最终条目已通过真实论坛验证**，也不做外部发布。

`src/EcosystemValidation` 引用原版 Borea v0.5.1 源码工程，使用上游的测试接口注入独立根目录和候选索引/下载传输；安装器、依赖规划器、实例仓库、替换器、卸载器、加载器配置和进程启动器均为上游实现。测试程序的程序集名使用 Borea.Cli.Tests 以访问其既有内部测试入口；不修改上游源码，不随玩家包交付。

构建后运行 `Borea.Cli.Tests.dll <项目绝对路径> <全新的work内test-profile目录> <隔离游戏目录>`。游戏目录不能残留旧 Content/KsaChinese。运行期间真实启动游戏，通过开发专用 SmokeDriver 验证显示与设置、保存与重载；SmokeDriver 不随包发布。加入 `--published` 会从已发布的 GitHub 下载真实压缩包；索引仍是本机候选。不要把该结果写成公共目录已收录或 Borea 桌面 UI 点击验收。

收录后的公共目录验证使用同一命令并加入 `--public-catalog`。该模式使用默认 HTTP 处理器直连真实公共索引和发行附件，不注入候选索引或本机 ZIP；只检查搜索、两个独立实例的安装、文件一致性和兼容界限，写入 `reports/ecosystem-public-catalog-validation.json` 后退出，不启动游戏或修改原有运行验证报告。

包同时保留便携版时，标准包严格命名为 `KsaChinese-<版本>.zip`，符合上游多附件选择规则，避免 watcher 误选便携包。
