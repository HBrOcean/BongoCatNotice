using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Transformation;

namespace BongoCatNotice.Views;

/// <summary>尾巴朝向：猫在气泡左下方就用 Left，在右下方就用 Right</summary>
public enum BubbleTailSide
{
    Left,
    Right
}

public partial class NoticeBubble : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<NoticeBubble, string>(nameof(Title), "提醒");

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<NoticeBubble, string>(nameof(Message), string.Empty);

    public static readonly StyledProperty<string> ActionTextProperty =
        AvaloniaProperty.Register<NoticeBubble, string>(nameof(ActionText), "知道了");

    public static readonly StyledProperty<bool> HasActionProperty =
        AvaloniaProperty.Register<NoticeBubble, bool>(nameof(HasAction), true);

    public static readonly StyledProperty<BubbleTailSide> TailSideProperty =
        AvaloniaProperty.Register<NoticeBubble, BubbleTailSide>(nameof(TailSide), BubbleTailSide.Right);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public bool HasAction
    {
        get => GetValue(HasActionProperty);
        set => SetValue(HasActionProperty, value);
    }

    /// <summary>尾巴方向。改这一个值就能切换左右，几何不需要动。</summary>
    public BubbleTailSide TailSide
    {
        get => GetValue(TailSideProperty);
        set => SetValue(TailSideProperty, value);
    }

    public event EventHandler? ActionClicked;
    public event EventHandler? Dismissed;

    public NoticeBubble()
    {
        InitializeComponent();
        UpdateTail();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TailSideProperty)
        {
            UpdateTail();
        }
    }

    private void UpdateTail()
    {
        var right = this.FindControl<Path>("TailRight");
        var left = this.FindControl<Path>("TailLeft");
        if (right is null || left is null)
        {
            return;
        }

        var isLeft = TailSide == BubbleTailSide.Left;
        right.IsVisible = !isLeft;
        left.IsVisible = isLeft;
    }

    /// <summary>把动画重置到起始态，供窗口复用时重新播放入场</summary>
    public void ResetAnimation()
    {
        var root = this.FindControl<Grid>("BubbleRoot");
        if (root is null)
        {
            return;
        }

        root.Opacity = 0;
        root.RenderTransform = TransformOperations.Parse("translateY(6px)");
    }

    /// <summary>淡入 + 上浮。窗口 Show 之后调用。</summary>
    public void PlayEnter()
    {
        var root = this.FindControl<Grid>("BubbleRoot");
        if (root is null)
        {
            return;
        }

        root.Opacity = 1;
        root.RenderTransform = TransformOperations.Parse("translateY(0px)");
    }

    /// <summary>淡出 + 轻微下沉，等转场结束再关闭窗口。</summary>
    public async Task PlayExitAsync()
    {
        var root = this.FindControl<Grid>("BubbleRoot");
        if (root is null)
        {
            return;
        }

        root.Opacity = 0;
        root.RenderTransform = TransformOperations.Parse("translateY(4px)");
        await Task.Delay(200);
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        ActionClicked?.Invoke(this, EventArgs.Empty);
        Dismissed?.Invoke(this, EventArgs.Empty);
    }
}
