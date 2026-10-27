using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace BongoCatNotice.Utilities
{
    /// <summary>
    /// 设置面板背景主题：预设（黄橙 / 蓝白）+ 自定义双色渐变。
    /// 主题与自定义颜色都保存在 AppConfig 里，下次启动沿用上次的选择。
    /// </summary>
    public static class BackgroundTheme
    {
        public const string YellowOrange = "yellow-orange";
        public const string BlueWhite = "blue-white";
        public const string Custom = "custom";

        public static readonly (string Start, string End) YellowOrangePreset = ("#FFF8C9", "#FFD19A");
        public static readonly (string Start, string End) BlueWhitePreset = ("#66CCFF", "#CCFFFF");

        /// <summary>主题字符串 -> 下拉框索引</summary>
        public static int ToIndex(string? theme) => theme switch
        {
            BlueWhite => 1,
            Custom => 2,
            _ => 0
        };

        /// <summary>下拉框索引 -> 主题字符串</summary>
        public static string FromIndex(int index) => index switch
        {
            1 => BlueWhite,
            2 => Custom,
            _ => YellowOrange
        };

        /// <summary>按当前配置解析实际使用的起止色</summary>
        public static (string Start, string End) Resolve()
        {
            AppConfig cfg = GlobalHelper.Config;
            return cfg.BackgroundTheme switch
            {
                BlueWhite => BlueWhitePreset,
                Custom => (Normalize(cfg.BackgroundStart, YellowOrangePreset.Start),
                           Normalize(cfg.BackgroundEnd, YellowOrangePreset.End)),
                _ => YellowOrangePreset
            };
        }

        /// <summary>宽松解析颜色：允许省略 #，允许 #AARRGGBB</summary>
        public static bool TryParse(string? text, out Color color)
        {
            color = Colors.White;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string s = text.Trim();
            if (!s.StartsWith('#'))
                s = "#" + s;

            return Color.TryParse(s, out color);
        }

        /// <summary>统一成 #RRGGBB；无效则返回回退值</summary>
        public static string Normalize(string? text, string fallback)
            => TryParse(text, out Color c) ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : fallback;

        /// <summary>构建自上而下的线性渐变</summary>
        public static IBrush BuildBrush(string start, string end)
        {
            if (!TryParse(start, out Color c1))
                c1 = Color.Parse(YellowOrangePreset.Start);
            if (!TryParse(end, out Color c2))
                c2 = Color.Parse(YellowOrangePreset.End);

            LinearGradientBrush brush = new()
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
            };
            brush.GradientStops.Add(new GradientStop(c1, 0));
            brush.GradientStops.Add(new GradientStop(c2, 1));
            return brush;
        }

        /// <summary>
        /// 应用当前主题。window 与内容容器一起设，避免标签栏和内容区出现色差。
        /// 开启「液态玻璃」时改为半透明磨砂：窗口层交给系统做模糊，
        /// 玻璃层负责那一层白色薄雾 + 高光描边。平台不支持模糊时自动降级成纯半透明。
        /// </summary>
        public static void Apply(Window? window, TemplatedControl? content = null, Border? glassLayer = null)
        {
            if (window is null)
                return;

            if (GlobalHelper.Config.GlassEffect)
            {
                ApplyGlass(window, content, glassLayer);
                return;
            }

            window.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            (string start, string end) = Resolve();
            IBrush brush = BuildBrush(start, end);
            window.Background = brush;

            if (content is not null)
                content.Background = brush;

            if (glassLayer is not null)
                glassLayer.IsVisible = false;
        }

        private static void ApplyGlass(Window window, TemplatedControl? content, Border? glassLayer)
        {
            // 按喜好顺序给出候选：真亚克力 → 普通模糊 → 纯透明。
            // Avalonia 会挑平台实际支持的那一个，都不支持就退化成普通窗口。
            window.TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.Transparent
            };

            window.Background = Brushes.Transparent;

            if (content is not null)
                content.Background = Brushes.Transparent;

            if (glassLayer is null)
                return;

            // 玻璃的色调取自当前主题本身，而不是固定用白色。
            // 这样暖色主题即使背后是冷色壁纸，玻璃也不会泛蓝，整体仍保持暖调。
            Color pale = GetThemeTint();

            LinearGradientBrush veil = new()
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
            };
            veil.GradientStops.Add(new GradientStop(WithAlpha(pale, 242), 0));
            veil.GradientStops.Add(new GradientStop(WithAlpha(pale, 220), 0.45));
            veil.GradientStops.Add(new GradientStop(WithAlpha(pale, 234), 1));

            glassLayer.IsVisible = true;
            glassLayer.Background = veil;
            glassLayer.BorderBrush = new SolidColorBrush(WithAlpha(pale, 195));
            glassLayer.BorderThickness = new Thickness(1);
            // 顶边一道内高光、底边一道极淡收边，制造玻璃切面的感觉
            glassLayer.BoxShadow = BoxShadows.Parse("inset 0 1 0 #A0FFFFFF, inset 0 -1 0 #33FFFFFF");
        }

        /// <summary>
        /// 由主题渐变算出玻璃底色调：取两端中间色，再往白里提 84%，
        /// 只留下一点点色相，暖主题出暖白、冷主题出冷白。
        /// </summary>
        public static Color GetThemeTint()
        {
            (string start, string end) = Resolve();
            Color c1 = TryParse(start, out Color p1) ? p1 : Color.Parse(YellowOrangePreset.Start);
            Color c2 = TryParse(end, out Color p2) ? p2 : Color.Parse(YellowOrangePreset.End);

            Color mid = Color.FromRgb(
                (byte)((c1.R + c2.R) / 2),
                (byte)((c1.G + c2.G) / 2),
                (byte)((c1.B + c2.B) / 2));

            return Mix(mid, Colors.White, 0.80);
        }

        private static Color Mix(Color a, Color b, double t)
        {
            t = t < 0 ? 0 : (t > 1 ? 1 : t);
            return Color.FromArgb(
                (byte)(a.A + (b.A - a.A) * t),
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);
    }
}
