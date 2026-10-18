# 贡献指南

感谢你愿意为 BongoCatNotice 做点什么 🐱

本项目是 [BongoCatLike](https://github.com/WLongSAMA/BongoCatLike) 的衍生项目，遵循 **GPL-2.0**。在提交任何内容之前，请先了解下面几条。

## 提交 Issue

- **先搜索**：确认没有同样的问题/建议再新建。
- **Bug 请用 [Bug 报告模板](../../issues/new?template=bug_report.yml)**，尽量带上：
  - 版本号（设置面板或 `changelog.md` 顶部）
  - 操作系统与版本（Windows 10/11、Ubuntu 24.04、macOS 14…）
  - 复现步骤（1. 2. 3.）
  - 截图或控制台输出
- **功能建议请用 [功能请求模板](../../issues/new?template=feature_request.yml)**，说明使用场景比描述实现方式更有用。

## 开发环境

| 依赖 | 版本 |
|---|---|
| .NET SDK | **10.0** |
| IDE | Visual Studio 2022 / Rider / VS Code（推荐 Rider 或 VS） |
| 平台 | Windows 开发最方便；Linux/macOS 也可正常构建运行 |

```bash
git clone https://github.com/<your-name>/BongoCatNotice.git
cd BongoCatNotice
dotnet run -c Release
```

> 使用 Native AOT 发布需要 MSVC 工具链；日常开发用 `-p:PublishAot=false` 即可。

## 代码约定

1. **保持上游结构**：文件与命名空间布局尽量沿用原项目，方便与上游对比 diff。
2. **非托管资源必须释放**：任何 `new Bitmap(...)` 都要有对应的 `Dispose()`。皮肤/帽子是高频切换对象，泄漏很容易被忽略。
3. **不要在主线程做重活**：全局钩子回调里只做状态更新，UI 变更走 `Dispatcher.UIThread.Post`。
4. **命名**：类/方法 PascalCase，私有字段 `_camelCase`，与现有代码保持一致。
5. **注释用中文**，写"为什么"而不是"是什么"。

## 提交 PR

1. Fork 并新建分支：`feat/xxx`、`fix/xxx`、`docs/xxx`
2. 一次 PR 只做一件事，避免混入无关格式化改动
3. PR 描述里写清楚：**问题 → 方案 → 验证方式**
4. 涉及 UI 的改动请附截图
5. 涉及皮肤资源的改动请说明来源与许可

## 皮肤 / 素材贡献

这是最容易出问题的一类贡献，请务必遵守：

- **只提交你有权分发的内容**：自制素材、明确开放许可（MIT / CC0 / CC-BY 等）的素材。
- **不要提交商业游戏资产**：包括 Steam 版 Bongo Cat 的内置皮肤、付费 DLC 内容、其它游戏的解包素材。这类 PR 会被直接关闭。
- **标注来源**：在 `docs/THIRD_PARTY_NOTICES.md`（若不存在则新建）中记录素材名称、作者、许可、原始链接。
- **四帧对齐**：皮肤必须满足 [自定义皮肤教程](docs/custom-skin-guide.md) 中的对齐规范，否则在换装时会出现主体跳动。

## 许可证

提交贡献即表示你同意以 **GPL-2.0** 授权你的贡献，并确认你有权这样做。
