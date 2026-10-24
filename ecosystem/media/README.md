# 社区目录图标

## 当前封面：中文游戏截图

`ksa-chinese-hud-v2.jpg` 使用维护者提供并选定的真实游戏截图左上区域，保留“俯仰、偏航、滚转、资源、速度、模拟速度”等中文仪表与地球背景。只裁切、缩放和导出 JPEG，没有重绘、添加文字或调整游戏界面。

- 尺寸与大小：512 × 512，71827 字节。
- SHA-256：`223b33ad3439705518fbfec37fb55cac53d85abfed8e5766a32876baf8106c02`。
- 原图：3840 × 2160；裁切区域 x=0、y=0、width=1440、height=1440。保持选定区域的横向范围，向下保留同一原图中的地球背景以适配方形卡片。
- 原图 SHA-256：`022eeaa08cf4beeeb4efe90e73c604cb1c31063f0c8832b8b98e5610946b20ef`；完整截图只保留在本机，不随本次发布上传。
- 截图署名：kuxiaowo；游戏画面及资产归 Kitten Space Agency / RocketWerkz 及相应权利人。图片单独使用 `LicenseRef-KSA-Screenshot`，见 [截图展示说明](../../LICENSES/KSA-Screenshot.txt)，不将游戏画面声明为 MIT。

## 历史图标

`ksa-chinese-icon-v1.jpg` 是此前使用的独立社区图标。图案为太空猫和“中”字气泡，不使用游戏截图、官方标志或第三方角色素材。

- 许可：MIT，与本项目代码相同；原文见仓库根目录 `LICENSE`。
- 制作方式：维护者通过 OpenAI 内置图像生成工具制作；导出为 512 × 512 JPEG（54695 字节），以满足社区目录的图标限制。
- SHA-256：`4f86da34eaee37104abb3040e4820003a9a3b47ca5267d16998ed035b171ba2a`。
- 文件为不透明正方形；留出缩略图安全边距。
- 图片更新时使用新文件名（例如 `v2`），同时更新 `KsaChinese.toml.in` 内的 URL、SHA-256、尺寸和字节数，避免缓存与记录不一致。

生成提示词（2026-10-24）：

```text
Use case: logo-brand. Asset type: one original square PNG catalogue icon for the community mod KSA Chinese / 简体中文, used in Borea desktop. Create a clean, memorable, friendly cat astronaut emblem that communicates Chinese localization: a symmetrical ivory cat face with small triangular ears inside a simple round space helmet; one small warm coral speech-bubble badge containing exactly the single Chinese character 中, clearly legible. Background solid deep midnight navy, a thin muted teal orbital arc and only two tiny geometric stars. Flat vector-like graphic rendered as a raster image, crisp clean silhouettes, extremely limited solid color palette, no gradients, no texture, no photographic elements, no shadows, no noisy details. All important content centered with at least 14 percent safe padding for square thumbnail cropping, legible at 64 pixels. Square 512 by 512 pixels if supported. Opaque background. No other words or lettering, no watermarks, no official game logos or copied mascots. It must look like an independent community translation mod, not an official studio badge. Deliver one final icon only.
```
