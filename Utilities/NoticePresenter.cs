using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using BongoCatNotice.Views;

namespace BongoCatNotice.Utilities;

/// <summary>一条提醒</summary>
public sealed record NoticeRequest(string Title, string Message, int AutoCloseSeconds = 0);

/// <summary>
/// 提醒气泡的显示管家：独立透明窗口，弹在猫窗口正上方居中。
/// 支持排队、自动关闭、跟随猫窗口移动、屏幕边界钳制。
/// </summary>
public sealed class NoticePresenter : IDisposable
{
    private readonly Window _owner;
    private readonly Queue<NoticeRequest> _queue = new();
    private Window? _window;
    private NoticeBubble? _bubble;
    private bool _showing;
    private bool _disposed;

    public NoticePresenter(Window owner)
    {
        _owner = owner;
        _owner.PositionChanged += OnOwnerMoved;
        _owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    public bool IsShowing => _showing;

    /// <summary>排队显示一条提醒。正在显示时不会被打断。</summary>
    public void Enqueue(NoticeRequest request)
    {
        _queue.Enqueue(request);
        if (!_showing)
        {
            _ = ShowNextAsync();
        }
    }

    public void Enqueue(string title, string message, int autoCloseSeconds = 0)
        => Enqueue(new NoticeRequest(title, message, autoCloseSeconds));

    /// <summary>窗口只创建一次，之后反复复用，避免每条提醒都新建一个 Window</summary>
    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _bubble = new NoticeBubble
        {
            TailSide = BubbleTailSide.Right,   // 尾巴在底边右侧，朝左下指向猫
        };
        _bubble.Dismissed += OnBubbleDismissed;

        _window = new Window
        {
            WindowDecorations = Avalonia.Controls.WindowDecorations.None,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            CanResize = false,
            CanMinimize = false,
            CanMaximize = false,
            ShowActivated = false,             // 不抢焦点，不打断用户正在做的事
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = _bubble,
        };
    }

    private async Task ShowNextAsync()
    {
        if (_disposed || _showing)
        {
            return;
        }

        if (_queue.Count == 0)
        {
            _showing = false;
            return;
        }

        _showing = true;
        NoticeRequest req = _queue.Dequeue();

        EnsureWindow();
        if (_bubble is null || _window is null)
        {
            _showing = false;
            return;
        }

        _bubble.ResetAnimation();
        _bubble.Title = req.Title;
        _bubble.Message = req.Message;

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        // 等一帧布局，Bounds 才有真实值
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        Reposition();
        _bubble.PlayEnter();

        if (req.AutoCloseSeconds > 0)
        {
            _ = AutoCloseAsync(req.AutoCloseSeconds);
        }
    }

    private async Task AutoCloseAsync(int seconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        Dismiss();
    }

    private void OnBubbleDismissed(object? sender, EventArgs e) => Dismiss();

    public async void Dismiss()
    {
        if (_bubble is null || _window is null)
        {
            return;
        }

        // 这里不把 _bubble / _window 置空，窗口要留着复用
        try
        {
            await _bubble.PlayExitAsync();
            _window.Hide();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"关闭提醒气泡失败: {ex.Message}");
            _window.Hide();
        }

        await Task.Delay(100);
        _showing = false;
        _ = ShowNextAsync();
    }

    /// <summary>气泡底边贴住猫窗口顶边，水平居中对齐。</summary>
    private void Reposition()
    {
        if (_window is null)
        {
            return;
        }

        var s = _owner.RenderScaling;
        var ownerW = (int)(_owner.Bounds.Width * s);
        var winW = (int)(_window.Bounds.Width * s);
        var winH = (int)(_window.Bounds.Height * s);

        var x = _owner.Position.X + (ownerW - winW) / 2;
        var y = _owner.Position.Y - winH + (int)(4 * s);   // 尾巴尖压住猫头顶一点点

        var screen = _window.Screens.ScreenFromWindow(_window) ?? _window.Screens.Primary;
        if (screen is not null)
        {
            var wa = screen.WorkingArea;
            x = Math.Clamp(x, wa.X + 4, Math.Max(wa.X + 4, wa.Right - winW - 4));
            y = Math.Max(y, wa.Y + 4);                     // 顶到屏幕上沿就往下压
        }

        _window.Position = new PixelPoint(x, y);
    }

    private void OnOwnerMoved(object? sender, PixelPointEventArgs e)
    {
        if (_window is not null && _showing)
        {
            Reposition();
        }
    }

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        if (e.Property == Visual.BoundsProperty)
        {
            Reposition();
        }
        else if (e.Property == Window.WindowStateProperty)
        {
            // 猫被最小化时，气泡也跟着收起
            if (_owner.WindowState == WindowState.Minimized)
            {
                _window.Hide();
            }
            else if (!_window.IsVisible && _bubble is not null)
            {
                _window.Show();
                Reposition();
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _owner.PositionChanged -= OnOwnerMoved;
        _owner.PropertyChanged -= OnOwnerPropertyChanged;
        _queue.Clear();
        _window?.Close();
        _window = null;
        _bubble = null;
    }
}
