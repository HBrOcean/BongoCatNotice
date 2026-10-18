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
        /// </summary>
        public static void Apply(Window? window, TemplatedControl? content = null)
        {
            if (window is null)
                return;

            (string start, string end) = Resolve();
            IBrush brush = BuildBrush(start, end);
            window.Background = brush;

            if (content is not null)
                content.Background = brush;
        }
    }
}
