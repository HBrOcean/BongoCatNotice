# 设计说明

> 本文说明 BongoCatNotice 的整体设计：目标、技术选型、模块职责、关键机制与设计取舍。
> 关于皮肤格式与制作流程，见 [自定义皮肤教程](./custom-skin-guide.md)。

## 1. 项目定位

**BongoCatNotice** 是基于 [WLongSAMA/BongoCatLike](https://github.com/WLongSAMA/BongoCatLike) 修改而来的衍生项目。

原项目是一个"会跟着你敲键盘的桌面小猫"：它监听全局键盘/鼠标事件，用左右爪的敲击动画回应输入。本项目的目标是让这只小猫承担**通知与提醒**职责——把系统通知、待办、定时提醒这类信息，交给一只会敲桌子的小猫递到用户面前，而不是再弹一个冷冰冰的横幅。

因此本项目的设计原则是：

1. **不打扰**：常驻托盘、透明无边框、可鼠标穿透，不抢焦点。
2. **轻**：不依赖 Steam、不联网、CPU/内存在空闲时接近 0。
3. **可换装**：完整沿用原项目的皮肤/帽子体系，227 套皮肤 + 387 顶帽子开箱可用。
4. **可被自己改造**：皮肤是纯 PNG + JSON 注册，不需要重新编译就能扩充资源。

## 2. 技术选型

| 维度 | 选择 | 理由 |
|---|---|---|
| 语言 / 运行时 | C# / .NET 10 | 与原项目一致；单文件 Native AOT 发布后无需用户装运行时 |
| UI 框架 | Avalonia 12.1.2 | 跨平台（Windows / Linux / macOS），支持透明无边框窗口与自绘 |
| 图形 | SkiaSharp（Avalonia 后端） | 256×256 小图逐帧切换开销极低 |
| 全局输入 | SharpHook（libuiohook） | 跨平台全局键鼠钩子，不注入目标进程、不联网 |
| 序列化 | System.Text.Json + 源生成器 | AOT 友好，避免运行时反射 |
| 打包 | Native AOT + UPX（可选） | 体积小、冷启动快 |

选 Avalonia 而不是 WPF 的核心原因：原项目已经用它，且透明窗口 + 托盘 + 鼠标穿透这套组合在三个平台上的行为一致，移植成本最低。

## 3. 模块结构

```
BongoCatNotice/
├── Program.cs                  # 入口，AOT 相关配置
├── app.manifest                # DPI 感知 / 单实例等清单
├── BongoCatNotice.csproj       # 目标框架、AOT 开关、图标
│
├── Views/
│   ├── MainWindow.axaml(.cs)   # 主体窗口：皮肤渲染、敲击动画、拖拽、窗口尺寸
│   ├── SettingWindow.axaml(.cs)# 设置面板：皮肤/帽子列表、语言、系统选项
│   └── App.axaml(.cs)          # 应用启动、单实例、窗口生命周期
│
├── ViewModels/
│   └── SettingViewModel.cs     # 设置面板的数据绑定与文案
│
├── Utilities/
│   ├── SkinManager.cs          # 皮肤/帽子资源加载、当前装扮状态（单例）
│   ├── ConfigManager.cs        # 配置读写（JSON）
│   ├── GlobalHelper.cs         # 全局常量、配置实例、缩放换算
│   ├── RectArea.cs             # 不透明像素包围盒
│   ├── Localization.cs         # 多语言字符串
│   ├── MousePenetration.cs     # 鼠标穿透（平台相关）
│   └── AutorunManager.cs       # 开机自启
│
├── Locales/                    # 语言文件
├── Assets/
│   ├── skin/                   # 227 套皮肤，每套 4 帧 + 图标
│   ├── hat/                    # 387 顶帽子
│   ├── emote/ animate/ consumable/ default/
│   ├── items.json              # 皮肤/帽子清单（源生成器反序列化）
│   └── offset.json             # 每顶帽子的挂载偏移
├── Tools/upx/                  # CI 压缩用
└── .github/workflows/          # 多平台自动构建与发布
```

## 4. 关键机制

### 4.1 皮肤分层渲染

原项目的皮肤不是"一整张图"，而是**分层素材**：

| 文件 | 内容 |
|---|---|
| `XxxLeft.png` | 猫头 + 身体 + 左爪（静止） |
| `XxxLeftPunch.png` | 猫头 + 身体 + 左爪（按下） |
| `XxxRight.png` | 右爪（静止）——**单独一层，不含身体** |
| `XxxRightPunch.png` | 右爪（按下） |
| `XxxIcon.png` | 设置面板里的图标 |

渲染时按 XAML 中的声明顺序叠三层（越靠后越在上）：

| 顺序 | 控件 | 内容 |
|---|---|---|
| 1（底层） | `SkinImage` | 身体 + 左爪（Left / LeftPunch） |
| 2 | `HatImage` | 帽子 |
| 3（顶层） | `HandImage` | 右爪（Right / RightPunch） |

这个顺序带来两个实际效果：

- **帽子盖在身体（含尾巴）之上**：皮肤的画布内容和帽子重叠时不会视觉冲突，比如尾巴伸进帽子区，帽子仍会正常压在尾巴前，看起来就是"戴在头上"。
- **右爪盖在帽子之前**：敲击瞬间爪子会出现在帽檐前面，符合"手在最前"的层次感。

所以"双手举起"的画面实际上是 `Left + Right` 叠加的结果——项目封面就是这么合成的。

> 另有 `mode: "full"` 的单层整猫模式（`image` 为 3 帧、`HandImage` 置空）作为扩展点保留在代码中，当前没有皮肤使用。

### 4.2 敲击事件流

```
SharpHook 全局钩子 (Task.Run 中常驻)
   ├─ KeyPressed   → _activeKeys.TryAdd   → Press()
   ├─ KeyReleased  → _activeKeys.TryRemove → Release()
   ├─ MousePressed → _activeMouseButtons.TryAdd   → Press()
   └─ MouseReleased→ _activeMouseButtons.TryRemove → Release()
```

`Press()` 立即切到敲击帧，`Release()` 在所有输入释放后恢复静止帧。两个关键设计：

1. **不做动画排队**。早期实现用 `_isAnimating` 锁，一次动画 200ms，期间的输入全部丢弃，快速连击会明显"吃键"。现在改为状态驱动：按下即生效，不排队。
2. **最短可见时长 + 版本号**。`Release()` 延迟 `MinHitVisibleMs = 60` 毫秒再恢复静止帧，避免高速敲击时敲击帧一闪而过看不见；同时用 `_hitVersion` 做版本校验，等待期间若有新的按下，就放弃本次恢复，保证连击不丢帧。

左右爪用 `Interlocked.Increment` 的原子计数交替（奇偶），避免键盘与鼠标事件并发时两边抢同一个变量。

### 4.3 窗口尺寸与位置补偿

窗口大小 = 当前"猫 + 帽子"的不透明像素包围盒（`GetImageArea()`），这样窗口边缘紧贴猫，不会有一大片透明区域挡住下面的窗口点击。

换装（换皮肤/换帽子/改缩放）时包围盒尺寸会变。若固定窗口左上角，猫就会在屏幕上上下跳动。因此以**底部 + 水平中心**为锚点补偿窗口位置：

- 垂直：高度变化量整体补偿到 `Position.Y`（脚不动，身体上下伸展）
- 水平：宽度变化量的一半补偿到 `Position.X`（左右对称扩展）
- 需要按 `RenderScaling` 换算物理像素

补偿只在 `SetWindow()` 完成初始化后启用（`_positionCompensationReady`），否则启动阶段会按错误的基准补偿。

### 4.4 资源与内存

位图是非托管资源，必须显式释放。项目里三处关键处理：

1. **换肤时**：`SkinManager.SkinId` setter 里先 `Dispose()` 旧的 4 帧再加载新的。随机换装（最短 60 秒一次）长时间运行也不会持续增长。
2. **换帽时**：旧帽子位图在新帽子加载完成后再 `Dispose()`，避免与仍在显示的图像冲突。
3. **设置窗口**：一次构造会创建 227 + 387 = 614 个图标位图，窗口 `Closed` 时遍历视觉树统一释放。

另外 `SkinImage` 属性返回的是缓存数组而非每次 `[..]` 复制——敲击时这个属性会被高频读取，复制会产生大量短命小对象。

### 4.5 快捷键

全局快捷键 **Ctrl+Y** 直接复用上面的 SharpHook 钩子，不额外注册系统热键（避免独占、避免与其它软件冲突）：

```csharp
if (e.Data.KeyCode == KeyCode.VcY &&
    (_activeKeys.ContainsKey(KeyCode.VcLeftControl) ||
     _activeKeys.ContainsKey(KeyCode.VcRightControl)))
{
    Dispatcher.UIThread.Post(() => SettingWindow.ShowOrActivate(this, "Setting"));
}
```

选中 Ctrl+Y 而不是 Ctrl+C，是因为后者会与"复制"冲突，全局劫持体验很差。

### 4.6 配置持久化

配置为单个 JSON（`ConfigManager`），字段包括：窗口坐标、皮肤 ID、帽子 ID、缩放、翻转、置顶、随机换装间隔、语言、鼠标穿透、开机自启等。

写入时机有两处：

- 窗口 `Closing` 时保存全部（含窗口坐标）
- **换肤/换帽后立即保存**，但只写装扮 ID，不写坐标——因为启动阶段 `SetSkin()` 早于 `SetWindow()`，此时坐标还没恢复，写入会把上次的位置覆盖掉

## 5. 设计取舍与已知限制

| 项 | 现状 | 说明 |
|---|---|---|
| 敲击帧精度 | 4 帧（静止/左击/右击 + 合成） | 原项目素材是位图序列，无法像 L2D 那样插值，节奏靠切换时机保证 |
| 皮肤体积 | 227 套约 31MB | 换装时按需加载单套，不做全量缓存（256×256 解码后每张约 256KB，全缓存会占用数百 MB 非托管内存） |
| 16px 图标 | 细节不可辨 | 皮肤素材是插画而非符号化图形，小尺寸下只能靠配色识别 |
| 多显示器 | 依赖系统 DPI 缩放 | 跨不同缩放比的显示器拖动时，需重新计算包围盒 |
| notice 功能 | 未实现 | 见路线图 |

## 6. 构建与发布

**本地**：

```bash
dotnet run -c Release                                  # 直接运行
dotnet publish ./BongoCatNotice.csproj -c Release -r win-x64   # Windows 单文件
```

Release 配置启用 Native AOT，需要 MSVC 工具链（「使用 C++ 的桌面开发」工作负载）；没有的话加 `-p:PublishAot=false` 走普通自包含发布。

**CI**：`.github/workflows/build-and-release.yml` 在 push tag 时按矩阵构建 win-x64 / linux-x64 / osx-x64 / osx-arm64，非 Windows 产物会 `strip` 并在启用时用 UPX 压缩，最后汇总到 Release。

## 7. 路线图

- [ ] **notice 核心**：接收系统通知 / 待办 / 定时的统一入口，把消息以"小猫举手"的形式呈现
- [ ] 通知样式：气泡、举牌、敲桌子节奏区分优先级
- [ ] 免打扰时段与优先级规则
- [ ] 皮肤制作辅助工具：四帧对齐检查（对齐要求见皮肤教程）

## 8. 相关文档

- [自定义皮肤教程](./custom-skin-guide.md)
- [更新日志](../changelog.md)
- [贡献指南](../CONTRIBUTING.md)
