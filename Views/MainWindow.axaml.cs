using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using BongoCatNotice.Utilities;
using SharpHook;
using SharpHook.Data;
using MouseButton = SharpHook.Data.MouseButton;

namespace BongoCatNotice.Views
{
    public partial class MainWindow : Window
    {
        private TrayIcon _trayIcon = new();
        private NativeMenuItem? _skinItem;
        private NativeMenuItem? _settingItem;
        private NativeMenuItem? _exitItem;
        private Point? _dragStartPoint;
        private PointerPressedEventArgs? _pressedEventArgs;
        // 敲击响应:按下即切换敲击帧,松开后恢复静止;不再用全局动画锁丢弃连击输入。
        private int _sideCounter = 0;
        private int _heldInputs = 0;
        private int _hitVersion = 0;
        private const int MinHitVisibleMs = 60;
        private Task? _bobbingAnimationInstance;
        private double _lastWindowWidth = 0;
        private double _lastWindowHeight = 0;
        private bool _positionCompensationReady = false;
        // 边缘吸附：拖拽停稳后再对齐，避免拖动过程中被反复拉回
        private readonly DispatcherTimer _snapTimer = new() { Interval = TimeSpan.FromMilliseconds(140) };
        private bool _applyingSnap = false;
        // 当前贴住的是哪几条边，窗口尺寸变化后按这个状态重新贴齐
        [Flags]
        private enum SnapEdge
        {
            None = 0,
            Left = 1,
            Right = 2,
            Top = 4,
            Bottom = 8
        }
        private SnapEdge _snapState = SnapEdge.None;
        private bool _snapAnimating = false;
        private double _lastAppliedScaling = 1;
        //private double LastHeight = 0;
        //private CancellationTokenSource? _cancellationTokenSource;

        private ConcurrentDictionary<KeyCode, bool> _activeKeys = new();
        private ConcurrentDictionary<MouseButton, bool> _activeMouseButtons = new();

        DispatcherTimer RandomSkinTimer = new();
        private bool isRandomSkinTimerRunning = false;

        // 定时提醒：气泡窗口管家 + 调度器
        private NoticePresenter? _noticePresenter;

        public MainWindow()
        {
            InitializeComponent();
            Opened += MainWindow_Opened;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            SetLocalization();
            SetTrayIcon();
            SetSkin(GlobalHelper.Config.SkinId);
            SetHat(GlobalHelper.Config.HatId);
            SetWindow();
            SetRandomSkin();
            ListeningPress();
            PositionChanged += MainWindow_PositionChanged;
            _snapTimer.Tick += (_, _) =>
            {
                _snapTimer.Stop();
                ApplyAdsorption();
            };
            _lastAppliedScaling = RenderScaling <= 0 ? 1 : RenderScaling;

            // 定时提醒：读配置 → 起调度器
            ReminderStore.Load();
            _noticePresenter = new NoticePresenter(this);
            ReminderStore.Start(_noticePresenter);
        }

        private void MainWindow_PositionChanged(object? sender, PixelPointEventArgs e)
        {
            // 拖到不同缩放比的显示器时重新计算窗口尺寸，否则包围盒仍按旧 DPI 计算会错位
            double scaling = RenderScaling <= 0 ? 1 : RenderScaling;
            if (Math.Abs(scaling - _lastAppliedScaling) > 0.01)
            {
                _lastAppliedScaling = scaling;
                _lastWindowWidth = 0;
                _lastWindowHeight = 0;
                _snapState = SnapEdge.None;   // 换到另一块屏了，旧贴边状态作废
                SetWindowSize();
            }

            if (!GlobalHelper.Config.Adsorption || _applyingSnap)
                return;

            // 拖拽期间位置连续变化，等停稳再吸附
            _snapTimer.Stop();
            _snapTimer.Start();
        }

        /// <summary>
        /// 边缘吸附：窗口靠近屏幕工作区边缘时自动贴齐。
        /// · 阈值随 DPI 缩放，高缩放下手感一致
        /// · 多显示器之间的接缝不吸附，否则窗口会粘在两屏中间拖不过去
        /// · 吸附过程 110ms 缓动，不是瞬间跳过去
        /// </summary>
        private void ApplyAdsorption()
        {
            if (!GlobalHelper.Config.Adsorption || !_positionCompensationReady ||
                _applyingSnap || _snapAnimating || _dragStartPoint is not null)
                return;

            Screen? screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is null)
                return;

            PixelRect wa = screen.WorkingArea;
            double rs = RenderScaling <= 0 ? 1 : RenderScaling;

            int winWidth = (int)Math.Round(Bounds.Width * rs);
            int winHeight = (int)Math.Round(Bounds.Height * rs);
            if (winWidth <= 0 || winHeight <= 0)
                return;

            int snapDistance = (int)Math.Round(18 * rs);
            int x = Position.X;
            int y = Position.Y;
            int right = wa.X + wa.Width;
            int bottom = wa.Y + wa.Height;
            SnapEdge state = SnapEdge.None;

            if (!HasNeighborScreen(screen, SnapEdge.Left) && Math.Abs(x - wa.X) <= snapDistance)
            {
                x = wa.X;
                state |= SnapEdge.Left;
            }
            else if (!HasNeighborScreen(screen, SnapEdge.Right) && Math.Abs(right - (x + winWidth)) <= snapDistance)
            {
                x = right - winWidth;
                state |= SnapEdge.Right;
            }

            if (!HasNeighborScreen(screen, SnapEdge.Top) && Math.Abs(y - wa.Y) <= snapDistance)
            {
                y = wa.Y;
                state |= SnapEdge.Top;
            }
            else if (!HasNeighborScreen(screen, SnapEdge.Bottom) && Math.Abs(bottom - (y + winHeight)) <= snapDistance)
            {
                y = bottom - winHeight;
                state |= SnapEdge.Bottom;
            }

            _snapState = state;

            if (x == Position.X && y == Position.Y)
                return;

            SnapTo(x, y);
        }

        /// <summary>按当前贴边状态重新对齐（换皮肤、改缩放、换显示器后调用）</summary>
        private void ReapplySnap()
        {
            if (!GlobalHelper.Config.Adsorption || _snapState == SnapEdge.None || _applyingSnap)
                return;

            Screen? screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is null)
                return;

            PixelRect wa = screen.WorkingArea;
            double rs = RenderScaling <= 0 ? 1 : RenderScaling;
            int winWidth = (int)Math.Round(Bounds.Width * rs);
            int winHeight = (int)Math.Round(Bounds.Height * rs);
            if (winWidth <= 0 || winHeight <= 0)
                return;

            int x = Position.X;
            int y = Position.Y;

            if (_snapState.HasFlag(SnapEdge.Left))
                x = wa.X;
            else if (_snapState.HasFlag(SnapEdge.Right))
                x = wa.X + wa.Width - winWidth;

            if (_snapState.HasFlag(SnapEdge.Top))
                y = wa.Y;
            else if (_snapState.HasFlag(SnapEdge.Bottom))
                y = wa.Y + wa.Height - winHeight;

            if (x != Position.X || y != Position.Y)
                SnapTo(x, y);
        }

        /// <summary>缓动到目标位置，110ms easeOutCubic；用户中途开始拖动则立即中止</summary>
        private async void SnapTo(int x, int y)
        {
            if (_snapAnimating)
                return;

            PixelPoint from = Position;
            var to = new PixelPoint(x, y);
            if (from == to)
                return;

            _snapAnimating = true;
            _applyingSnap = true;   // 整个动画期间都屏蔽位置变化回调，避免自我触发
            try
            {
                const int durationMs = 110;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < durationMs)
                {
                    if (_dragStartPoint is not null)
                        return;

                    double t = watch.ElapsedMilliseconds / (double)durationMs;
                    double e = 1 - Math.Pow(1 - t, 3);
                    Position = new PixelPoint(
                        (int)Math.Round(from.X + (to.X - from.X) * e),
                        (int)Math.Round(from.Y + (to.Y - from.Y) * e));

                    await Task.Delay(8);
                }

                Position = to;
            }
            catch (Exception ex)
            {
                // async void 里抛出去就是未捕获异常，桌宠会直接消失
                Console.WriteLine($"吸附动画失败: {ex.Message}");
            }
            finally
            {
                _applyingSnap = false;
                _snapAnimating = false;
            }
        }

        /// <summary>该方向是否还有另一块屏幕贴着；有则这一侧不吸附，否则窗口会卡在两屏接缝</summary>
        private bool HasNeighborScreen(Screen current, SnapEdge side)
        {
            PixelRect b = current.Bounds;
            foreach (Screen s in Screens.All)
            {
                if (ReferenceEquals(s, current))
                    continue;

                PixelRect o = s.Bounds;
                bool vOverlap = o.Y < b.Y + b.Height && o.Y + o.Height > b.Y;
                bool hOverlap = o.X < b.X + b.Width && o.X + o.Width > b.X;

                switch (side)
                {
                    case SnapEdge.Left when vOverlap && Math.Abs(o.X + o.Width - b.X) <= 2:
                    case SnapEdge.Right when vOverlap && Math.Abs(o.X - (b.X + b.Width)) <= 2:
                    case SnapEdge.Top when hOverlap && Math.Abs(o.Y + o.Height - b.Y) <= 2:
                    case SnapEdge.Bottom when hOverlap && Math.Abs(o.Y - (b.Y + b.Height)) <= 2:
                        return true;
                }
            }

            return false;
        }


        private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            GlobalHelper.Config.WindowLeft = Position.X;
            GlobalHelper.Config.WindowTop = Position.Y;
            GlobalHelper.Config.SkinId = GlobalHelper.CatSkin.SkinId;
            GlobalHelper.Config.HatId = GlobalHelper.CatSkin.HatId;
            ConfigManager.SaveConfig(GlobalHelper.Config);

            ReminderStore.Stop();
            ReminderStore.SaveNow();
            _noticePresenter?.Dispose();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!GlobalHelper.Config.DisableDrag && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                //_ = showMessagePopup();
                _dragStartPoint = e.GetPosition(this);
                _pressedEventArgs = e;
                e.Handled = false;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragStartPoint.HasValue && _pressedEventArgs != null)
            {
                Point currentPoint = e.GetPosition(this);
                Point delta = currentPoint - _dragStartPoint.Value;
                if (Math.Abs(delta.X) > 3 || Math.Abs(delta.Y) > 3)
                {
                    BeginMoveDrag(_pressedEventArgs);
                    _dragStartPoint = null;
                    _pressedEventArgs = null;
                }
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _dragStartPoint = null;
            _pressedEventArgs = null;
        }

        private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _dragStartPoint = null;
            _pressedEventArgs = null;
        }

        private void SetLocalization()
        {
            Localization.LoadLanguage(GlobalHelper.Config.Language);
            Localization.LanguageChanged += () =>
            {
                _skinItem?.Header = Localization.GetString("Menu.Skin");
                _settingItem?.Header = Localization.GetString("Menu.Setting");
                _exitItem?.Header = Localization.GetString("Menu.Exit");
            };
        }

        private void SetTrayIcon()
        {
            Bitmap icon = new(AssetLoader.Open(new Uri($"avares://{GlobalHelper.ProjectName}/Assets/Icon.ico")));
            _trayIcon.Icon = new WindowIcon(icon);
            _trayIcon.ToolTipText = GlobalHelper.Name;

            _skinItem = new(Localization.GetString("Menu.Skin"));
            _skinItem.Click += (sender, e) => { SettingWindow.ShowOrActivate(this, "Page1"); };

            _settingItem = new(Localization.GetString("Menu.Setting"));
            _settingItem.Click += (sender, e) => { SettingWindow.ShowOrActivate(this, "Setting"); };

            _exitItem = new(Localization.GetString("Menu.Exit"));
            _exitItem.Click += (sender, e) =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.Shutdown();
            };

            NativeMenu menu = [];
            menu.Add(_skinItem);
            menu.Add(_settingItem);
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(_exitItem);

            _trayIcon.Menu = menu;
            _trayIcon.Clicked += (sender, e) =>
            {
                Show();
                Activate();
            };

            TrayIcon.SetIcons(Application.Current!, [_trayIcon]);
        }

        private void SetWindow()
        {
            if (GlobalHelper.Config.WindowLeft == 0 && GlobalHelper.Config.WindowTop == 0)
            {
                Screen screen = Screens.Primary!;
                if (screen == null)
                    return;

                double scaling = screen.Scaling;
                PixelRect workingArea = screen.WorkingArea;

                // 获取窗口的物理像素尺寸
                Size pixelSize = FrameSize ?? ClientSize * scaling;

                double left = workingArea.X + (workingArea.Width - pixelSize.Width) / 2;
                double top = workingArea.Y + (workingArea.Height - pixelSize.Height) / 2;

                // 确保位置在屏幕范围内
                GlobalHelper.Config.WindowLeft = (int)Math.Max(workingArea.X, Math.Min(left, workingArea.X + workingArea.Width - pixelSize.Width));
                GlobalHelper.Config.WindowTop = (int)Math.Max(workingArea.Y, Math.Min(top, workingArea.Y + workingArea.Height - pixelSize.Height));
            }
            Position = new PixelPoint(GlobalHelper.Config.WindowLeft, GlobalHelper.Config.WindowTop);

            ShowInTaskbar = GlobalHelper.Config.TaskbarIcon;
            Topmost = GlobalHelper.Config.Topmost;

            SetFlip(GlobalHelper.Config.Flip);
            SetZoom(GlobalHelper.Config.Zoom);
            SetBobbing(GlobalHelper.Config.Bobbing);

            // 初始布局与窗口位置确定后再启用位置补偿
            _positionCompensationReady = true;

            // 上次退出时是贴着边的，启动后直接贴回去（等布局完成再算）
            Dispatcher.UIThread.Post(ApplyAdsorption, DispatcherPriority.Loaded);
        }

        private void SetWindowSize(int type = 0)
        {
            double scaling = GlobalHelper.GetScaling(GlobalHelper.Config.Zoom);
            RectArea imageArea = GlobalHelper.CatSkin.GetImageArea();

            double newWidth = imageArea.Width * scaling;
            double newHeight = imageArea.Height * scaling;

            // 窗口大小即小猫的可见区域：切换装扮（皮肤/帽子）或缩放时，可见包围盒的尺寸会变化。
            // 若保持窗口左上角不动，小猫会随尺寸变化上下/左右移动，看起来就是「主体偏移」。
            // 这里以「底部 + 水平中心」为锚点补偿窗口位置，使小猫在屏幕上保持稳定
            // （脚不动，身体上下伸展；水平左右对称扩展）。
            if (_positionCompensationReady)
            {
                double renderScaling = RenderScaling <= 0 ? 1 : RenderScaling;

                int offsetY = 0;
                if (_lastWindowHeight > 0 && Math.Abs(newHeight - _lastWindowHeight) > 0.5)
                    offsetY = (int)Math.Round((newHeight - _lastWindowHeight) * renderScaling);

                int offsetX = 0;
                if (_lastWindowWidth > 0 && Math.Abs(newWidth - _lastWindowWidth) > 0.5)
                    offsetX = (int)Math.Round((_lastWindowWidth - newWidth) * renderScaling / 2.0);

                if (offsetX != 0 || offsetY != 0)
                    Position = new PixelPoint(Position.X + offsetX, Position.Y - offsetY);
            }

            Width = newWidth;
            Height = newHeight;
            SkinCanvas.Margin = new Thickness(-imageArea.X * scaling, -imageArea.Y * scaling);

            _lastWindowWidth = newWidth;
            _lastWindowHeight = newHeight;

            // 尺寸变了，如果之前贴着边就按原状态重新贴齐，否则会脱开一截
            if (GlobalHelper.Config.Adsorption && _snapState != SnapEdge.None)
                Dispatcher.UIThread.Post(ReapplySnap, DispatcherPriority.Background);
        }

        private void SetRandomSkin()
        {
            RandomSkinTimer.Tick += (sender, e) =>
            {
                Random random = new();
                string[] skinList = GlobalHelper.CatSkin.GetSkinIdList();
                string[] hatList = GlobalHelper.CatSkin.GetHatIdList();
                SetSkin(skinList[random.Next(skinList.Length)]);
                SetHat(hatList[random.Next(hatList.Length)]);
            };
            RandomSkin(GlobalHelper.Config.RandomSkin);
        }

        /// <summary>
        /// 把配置里的字母映射成 SharpHook 键码。
        /// 只开放几个不与系统常用快捷键冲突的字母。
        /// </summary>
        private static KeyCode HotkeyKeyCode() => GlobalHelper.Config.HotkeyKey?.ToUpperInvariant() switch
        {
            "U" => KeyCode.VcU,
            "I" => KeyCode.VcI,
            "O" => KeyCode.VcO,
            "P" => KeyCode.VcP,
            "L" => KeyCode.VcL,
            _ => KeyCode.VcY
        };

        private void ListeningPress()
        {
            Task task = Task.Run(() =>
            {
                TaskPoolGlobalHook hook = new();

                hook.KeyPressed += (sender, e) =>
                {
                    // 全局快捷键 Ctrl + 字母(默认 Y):打开设置面板(托盘菜单之外的快捷入口)
                    if (e.Data.KeyCode == HotkeyKeyCode())
                    {
                        bool ctrlHeld = _activeKeys.ContainsKey(KeyCode.VcLeftControl)
                                     || _activeKeys.ContainsKey(KeyCode.VcRightControl);
                        if (ctrlHeld)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                SettingWindow.ShowOrActivate(this, "Setting");
                            });
                        }
                    }

                    if (_activeKeys.TryAdd(e.Data.KeyCode, true))
                        Press();
                };

                hook.KeyReleased += (sender, e) =>
                {
                    if (_activeKeys.TryRemove(e.Data.KeyCode, out _))
                        Release();
                };

                hook.MousePressed += (sender, e) =>
                {
                    if (_activeMouseButtons.TryAdd(e.Data.Button, true))
                        Press();
                };

                hook.MouseReleased += (sender, e) =>
                {
                    if (_activeMouseButtons.TryRemove(e.Data.Button, out _))
                        Release();
                };

                hook.Run();
            });
        }

        /// <summary>
        /// 按下:立即切换到敲击帧,左右爪交替。
        /// 不排队、不等待上一次动画结束,快速连击时每一次输入都会被响应。
        /// </summary>
        public void Press()
        {
            Interlocked.Increment(ref _hitVersion);
            Interlocked.Increment(ref _heldInputs);

            int side = Interlocked.Increment(ref _sideCounter);
            bool isLeft = (side & 1) == 1;

            Dispatcher.UIThread.Post(() =>
            {
                if (GlobalHelper.CatSkin.SkinMode == "full")
                {
                    // 整猫帧:[0]=静止 [1]=左击 [2]=右击
                    SkinImage.Source = GlobalHelper.CatSkin.SkinImage[isLeft ? 1 : 2];
                    HandImage.Source = null;
                }
                else if (isLeft)
                {
                    SkinImage.Source = GlobalHelper.CatSkin.SkinImage[1];
                    HandImage.Source = GlobalHelper.CatSkin.SkinImage[2];
                }
                else
                {
                    SkinImage.Source = GlobalHelper.CatSkin.SkinImage[0];
                    HandImage.Source = GlobalHelper.CatSkin.SkinImage[3];
                }
            }, DispatcherPriority.Input);
        }

        /// <summary>
        /// 松开:所有输入都释放后恢复静止帧。
        /// 保留一个最短可见时长,避免高速敲击时敲击帧一闪而过看不见;
        /// 等待期间若有新的按下,则放弃本次恢复,保证连击不丢帧。
        /// </summary>
        public void Release()
        {
            int held = Interlocked.Decrement(ref _heldInputs);
            if (held > 0)
                return;
            if (held < 0)
                Interlocked.Exchange(ref _heldInputs, 0);

            int version = Interlocked.Increment(ref _hitVersion);

            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(MinHitVisibleMs);

                // 等待期间又有新的按键按下,新的敲击帧已经生效,不要再覆盖
                if (version != Volatile.Read(ref _hitVersion))
                    return;

                SkinImage.Source = GlobalHelper.CatSkin.SkinImage[0];
                HandImage.Source = GlobalHelper.CatSkin.SkinMode == "full" ? null : GlobalHelper.CatSkin.SkinImage[2];
            }, DispatcherPriority.Input);
        }

        public void EnableMousePenetration(bool isEnable)
        {
            if (!OperatingSystem.IsWindows())
                return;

            IPlatformHandle platformHandle = TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()!;
            if (platformHandle != null)
            {
                if (isEnable)
                    MousePenetration.Enable(platformHandle.Handle);
                else
                    MousePenetration.Disable(platformHandle.Handle);
            }
            GlobalHelper.Config.MousePenetration = isEnable;
        }

        public void SetFlip(bool isFlip)
        {
            MainGrid.RenderTransform = new ScaleTransform(isFlip ? -1 : 1, 1);
            MainGrid.HorizontalAlignment = isFlip ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
            GlobalHelper.Config.Flip = isFlip;
        }

        public void SetZoom(int index)
        {
            GlobalHelper.Config.Zoom = index;
            double scaling = GlobalHelper.GetScaling(index);
            SkinImage.Width = GlobalHelper.WindowWidth * scaling;
            SkinImage.Height = GlobalHelper.WindowHeight * scaling;
            HandImage.Width = GlobalHelper.WindowWidth * scaling;
            HandImage.Height = GlobalHelper.WindowHeight * scaling;
            HatImage.Width = GlobalHelper.WindowWidth * scaling;
            HatImage.Height = GlobalHelper.WindowHeight * scaling;
            UpdateImagePositions(scaling);
            SetWindowSize(2);
        }

        public void SetBobbing(bool isEnable)
        {
            if (_bobbingAnimationInstance != null)
            {
                SkinCanvas.RenderTransform = null;
                _bobbingAnimationInstance = null;
            }

            if (!isEnable)
            {
                SkinCanvas.RenderTransform = null;
                return;
            }

            Animation animation = new()
            {
                Duration = TimeSpan.FromSeconds(1),
                IterationCount = IterationCount.Infinite,
                Easing = new QuadraticEaseIn(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters = { new Setter(ScaleTransform.ScaleYProperty, 1.0) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(0.5),
                        Setters = { new Setter(ScaleTransform.ScaleYProperty, 1.02) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters = { new Setter(ScaleTransform.ScaleYProperty, 1.0) }
                    }
                }
            };

            SkinCanvas.RenderTransformOrigin = new RelativePoint(0.5, 1, RelativeUnit.Relative);
            _bobbingAnimationInstance = animation.RunAsync(SkinCanvas);
        }

        public void SetSkin(string SkinId)
        {
            GlobalHelper.CatSkin.SkinId = SkinId;
            SkinImage.Source = GlobalHelper.CatSkin.SkinImage[0];
            HandImage.Source = GlobalHelper.CatSkin.SkinMode == "full" ? null : GlobalHelper.CatSkin.SkinImage[2];
            SetWindowSize(1);
            SaveCurrentConfig();
        }

        public void SetHat(string HatId)
        {
            GlobalHelper.CatSkin.HatId = HatId;
            HatImage.Source = GlobalHelper.CatSkin.HatImage;

            double scaling = GlobalHelper.GetScaling(GlobalHelper.Config.Zoom);
            UpdateImagePositions(scaling);
            SetWindowSize(2);
            SaveCurrentConfig();
        }

        /// <summary>
        /// 换装扮后立即落盘,进程异常退出时当前皮肤/帽子不丢失。
        /// 只保存装扮 ID,不写窗口位置 —— 启动阶段 SetSkin 早于 SetWindow,此时坐标尚未恢复。
        /// </summary>
        private void SaveCurrentConfig()
        {
            GlobalHelper.Config.SkinId = GlobalHelper.CatSkin.SkinId;
            GlobalHelper.Config.HatId = GlobalHelper.CatSkin.HatId;
            ConfigManager.SaveConfig(GlobalHelper.Config);
        }

        private void UpdateImagePositions(double scaling)
        {
            RectArea combinedBounds = new(0, 0, 0, 0);
            Position hatOffset = GlobalHelper.CatSkin.HatOffset;
            Bitmap skinBitmap = GlobalHelper.CatSkin.SkinImage[0];
            Bitmap? hatBitmap = GlobalHelper.CatSkin.HatImage;
            if (hatBitmap != null)
            {
                combinedBounds.X = Math.Min(0, hatOffset.X);
                combinedBounds.Y = Math.Min(0, hatOffset.Y);
                combinedBounds.Right = Math.Max(skinBitmap.PixelSize.Width, hatOffset.X + hatBitmap.PixelSize.Width);
                combinedBounds.Bottom = Math.Max(skinBitmap.PixelSize.Height, hatOffset.Y + hatBitmap.PixelSize.Height);
                combinedBounds.Width = combinedBounds.Right - combinedBounds.Left;
                combinedBounds.Height = combinedBounds.Bottom - combinedBounds.Top;
            }
            else
            {
                combinedBounds.X = 0;
                combinedBounds.Y = 0;
                combinedBounds.Width = (int)SkinImage.Bounds.Width;
                combinedBounds.Height = (int)SkinImage.Bounds.Height;
            }

            int skinOffsetX = -combinedBounds.X;
            int skinOffsetY = -combinedBounds.Y;

            Canvas.SetLeft(SkinImage, skinOffsetX * scaling);
            Canvas.SetTop(SkinImage, skinOffsetY * scaling);
            Canvas.SetLeft(HandImage, skinOffsetX * scaling);
            Canvas.SetTop(HandImage, skinOffsetY * scaling);

            if (hatBitmap != null)
            {
                Canvas.SetLeft(HatImage, (skinOffsetX + hatOffset.X) * scaling);
                Canvas.SetTop(HatImage, (skinOffsetY + hatOffset.Y) * scaling);
            }
        }

        public async void HatAnimation()
        {
            await Task.Delay(10);

            HatImage.RenderTransform = new TransformGroup
            {
                Children = {
                    new ScaleTransform(1, 1) //没有此属性会导致帽子动画失效
                }
            };

            Animation animation = new()
            {
                Duration = TimeSpan.FromSeconds(0.5),
                Easing = new QuadraticEaseInOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(ScaleTransform.ScaleXProperty, 0.4),
                            new Setter(ScaleTransform.ScaleYProperty, 0.4)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(0.3),
                        Setters =
                        {
                            new Setter(ScaleTransform.ScaleXProperty, 1.1),
                            new Setter(ScaleTransform.ScaleYProperty, 1.1)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(0.5),
                        Setters =
                        {
                            new Setter(ScaleTransform.ScaleXProperty, 0.8),
                            new Setter(ScaleTransform.ScaleYProperty, 0.8)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(0.7),
                        Setters =
                        {
                            new Setter(ScaleTransform.ScaleXProperty, 1.2),
                            new Setter(ScaleTransform.ScaleYProperty, 1.2)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(ScaleTransform.ScaleXProperty, 1.0),
                            new Setter(ScaleTransform.ScaleYProperty, 1.0)
                        }
                    }
                }
            };
            await animation.RunAsync(HatImage);
        }

        /* 暂时没有好的方案处理 MessagePopup 的弹出位置
        private async Task showMessagePopup()
        {
            if ( MessagePopup.IsOpen)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }

            MessagePopup.IsOpen = true;

            Point? skinImagePosition = SkinImage.TranslatePoint(new Point(0, 0), this);
            Size messageGridSize = MessageGrid.DesiredSize;
            double scaling = GlobalHelper.GetScaling(GlobalHelper.Config.Zoom);

            MessagePopup.HorizontalOffset = ((skinImagePosition?.X ?? 0) + 100) * scaling;
            MessagePopup.VerticalOffset = ((skinImagePosition?.Y ?? 0) + 130) * scaling;

            MessageGrid.RenderTransform = new ScaleTransform(scaling, scaling);
            MessageGrid.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);

            _cancellationTokenSource = new CancellationTokenSource();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _cancellationTokenSource.Token);
                MessagePopup.IsOpen = false;
            }
            catch (OperationCanceledException) { }
        }
        */

        public void RandomSkin(int timeIndex)
        {
            GlobalHelper.Config.RandomSkin = timeIndex;

            if (timeIndex == 0)
            {
                if (isRandomSkinTimerRunning)
                    RandomSkinTimer.Stop();
                isRandomSkinTimerRunning = false;
                return;
            }

            TimeSpan? interval = timeIndex switch
            {
                1 => TimeSpan.FromSeconds(60),
                2 => TimeSpan.FromSeconds(180),
                3 => TimeSpan.FromSeconds(300),
                4 => TimeSpan.FromSeconds(900),
                5 => TimeSpan.FromSeconds(1800),
                _ => null
            };
            if (interval == null)
                return;

            // 定时器已经在跑时也要更新间隔:原实现在运行中切换频率不会生效
            RandomSkinTimer.Interval = interval.Value;
            RandomSkinTimer.Start();
            isRandomSkinTimerRunning = true;
        }

        public void TaskbarAdsorption(bool isAdsorption)
        {
            GlobalHelper.Config.Adsorption = isAdsorption;

            if (isAdsorption)
            {
                // 开启时立刻贴一次边，不用等下次拖动
                ApplyAdsorption();
            }
            else
            {
                // 关掉后忘掉贴边状态，免得下次改尺寸又自己贴回去
                _snapState = SnapEdge.None;
            }
        }
    }
}
