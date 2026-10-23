# 社区目录接入

本目录保存外部发布前可审阅的内容。维护者要求：发帖、向其他项目提交 PR 或其他外部发布必须先核实。当前没有向 Ahwoo 或 KSAModding 仓库发布内容。

- `forum-post.en.md`：拟在 Ahwoo Mod Releases 发布的帖子，User Interface 分类。
- `KsaChinese.toml.in`：社区索引条目模板，论坛地址明确留空。需要真实帖子地址，不能直接提交模板。
- `index-pr.en.md`：拟向 KSAModding/content-index 提交的 PR 说明。

许可为 MIT AND OFL-1.1；只收录 0.1.6 及以后，避免把旧便携包和旧 5602 支持范围误套入。游戏上下界均为 2026.10.19.5638；平台限定 Windows；StarMap 上下界均为 0.4.8，后续扩大前需测试。

社区 CONTRIBUTING.md 与 Schema 要求 `links.forums` 为真实 Ahwoo 帖子 URL。发帖后运行：

```powershell
python scripts/prepare_listing.py --forums-url 'https://forums.ahwoo.com/threads/实际帖子.实际编号/' --output '.tools/content-index/listings/KsaChinese.toml'
```

然后在 content-index 本地副本运行上游 `tools/check_layout.py`、`check_schema.py`、`check_index.py`、`check_license.py`、`check_status.py`、`check_images.py` 和联网 `check_release.py listings/KsaChinese.toml`。只有真实地址和发行包检查都通过，才提交经维护者确认的外部 PR。仓库属于 kuxiaowo，以该账户提交可按上游规则证明发布源所有权；若改用其他账户需另行提供控制权证明。

## 本机复现

准备 `.tools/content-index`、`.tools/content-index-releases` 的上游副本与 `.tools/Borea-source` 的 v0.5.1 源码（提交 284d5cf929c8ede99320f8afc451fa44f12aeadd），以及合法的游戏与 StarMap 0.4.8。本项目不分发上游源码副本或游戏文件。

在 conda 环境安装上游 `tools/requirements.txt`。构建插件并运行原有 Validation 后，执行 `scripts/package_mod.py`、`scripts/prepare_ecosystem_validation.py`。后者只在忽略的本机测试目录使用合成论坛地址生成候选索引，**不代表最终条目已通过真实论坛验证**，也不做外部发布。

`src/EcosystemValidation` 引用原版 Borea v0.5.1 源码工程，使用上游的测试接口注入独立根目录和候选索引/下载传输；安装器、依赖规划器、实例仓库、替换器、卸载器、加载器配置和进程启动器均为上游实现。测试程序的程序集名使用 Borea.Cli.Tests 以访问其既有内部测试入口；不修改上游源码，不随玩家包交付。

构建后运行 `Borea.Cli.Tests.dll <项目绝对路径> <全新的work内test-profile目录> <隔离游戏目录>`。游戏目录不能残留旧 Content/KsaChinese。运行期间真实启动游戏，通过开发专用 SmokeDriver 验证显示与设置、保存与重载；SmokeDriver 不随包发布。加入 `--published` 会从已发布的 GitHub 下载真实压缩包；索引仍是本机候选。不要把该结果写成公共目录已收录或 Borea 桌面 UI 点击验收。

包同时保留便携版时，标准包严格命名为 `KsaChinese-<版本>.zip`，符合上游多附件选择规则，避免 watcher 误选便携包。
