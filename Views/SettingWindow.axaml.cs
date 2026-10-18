using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using BongoCatNotice.Utilities;
using BongoCatNotice.ViewModels;

namespace BongoCatNotice.Views;

public partial class SettingWindow : Window
{
    public static SettingWindow? CurrentInstance { get; private set; }

    private Dictionary<string, string> LangList = [];
    private Border? LastSkin;
    private Border? LastHat;
    // 背景控件初始化期间抑制事件回调，避免打开窗口就写盘
    private bool _backgroundUiReady;
    // 用于搜索过滤：格子与其显示名
    private readonly List<(Border Cell, string Name)> _allCells = [];

    public SettingWindow()
    {
        InitializeComponent();
        DataContext = new SettingViewModel();
        InitBackgroundSettings();
        InitHotkeySettings();
        Closed += (s, e) =>
        {
            CurrentInstance = null;
            // 本窗口一次性创建了 227 + 387 个图标位图,关闭时统一释放,
            // 否则反复打开设置窗口会持续累积非托管内存
            // (皮肤与帽子条目都挂在 DefaultSkinList 下)
            ReleaseListImages(DefaultSkinList);
        };

        CreateItemList(GlobalHelper.CatSkin.Items.Skin, "skin", skinItem => skinItem.Value.Icon, skinItem => skinItem.Value.Tags, skinItem => skinItem.Value.Name, GlobalHelper.CatSkin.SkinId, ref LastSkin!);
        CreateItemList(GlobalHelper.CatSkin.Items.Hat, "hat", hatItem => hatItem.Value.Icon, hatItem => hatItem.Value.Tags, hatItem => hatItem.Value.Name, GlobalHelper.CatSkin.HatId, ref LastHat!);

        LangList = Localization.GetLangList();
        foreach (KeyValuePair<string, string> lang in LangList)
        {
            LangListComboBox.Items.Add(lang.Value);
            if (GlobalHelper.Config.Language == lang.Key)
                LangListComboBox.SelectedIndex = LangListComboBox.Items.Count - 1;
        }
    }

    /// <summary>
    /// 按目标显示宽度解码内嵌资源。
    /// 皮肤/帽子图标原始为 256×256，直接 new Bitmap 会按全尺寸解码：
    /// 227 + 387 = 614 个格子 → 峰值约 150MB 非托管内存。按 64px 解码可降到几 MB。
    /// </summary>
    private static Bitmap LoadAsset(string relativePath, int decodeWidth)
    {
        Uri uri = new($"avares://{GlobalHelper.ProjectName}/Assets/{relativePath}");

        try
        {
            using Stream stream = AssetLoader.Open(uri);
            return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality);
        }
        catch
        {
            // 个别格式不支持按宽解码时，退回完整解码，保证仍能显示
            using Stream stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
    }

    private static void ReleaseListImages(Control root)
    {
        if (root is Image img && img.Source is Bitmap bmp)
        {
            bmp.Dispose();
            img.Source = null;
        }

        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(root))
        {
            if (child is Control control)
                ReleaseListImages(control);
        }
    }

    /// <summary>
    /// 按配置初始化背景控件（主题下拉 + 自定义双色），并立即应用。
    /// 配置里保存的就是上次关闭时的选择，所以打开设置面板即为上次的效果。
    /// </summary>
    private void InitBackgroundSettings()
    {
        AppConfig cfg = GlobalHelper.Config;

        BackgroundThemeComboBox.SelectedIndex = BackgroundTheme.ToIndex(cfg.BackgroundTheme);

        BgStartBox.Text = BackgroundTheme.Normalize(cfg.BackgroundStart, BackgroundTheme.YellowOrangePreset.Start);
        BgEndBox.Text = BackgroundTheme.Normalize(cfg.BackgroundEnd, BackgroundTheme.YellowOrangePreset.End);

        UpdateColorPreviews();
        UpdateCustomColorPanel();
        ApplyBackground();

        _backgroundUiReady = true;
    }

    /// <summary>可选快捷键字母，顺序与 XAML 下拉项一致</summary>
    private static readonly string[] HotkeyKeys = ["Y", "U", "I", "O", "P", "L"];

    private void InitHotkeySettings()
    {
        string key = (GlobalHelper.Config.HotkeyKey ?? "Y").ToUpperInvariant();
        int index = Array.IndexOf(HotkeyKeys, key);
        HotkeyComboBox.SelectedIndex = index >= 0 ? index : 0;
    }

    private void OnHotkeyChanged(object? sender, SelectionChangedEventArgs e)
    {
        int index = HotkeyComboBox.SelectedIndex;
        if (index < 0 || index >= HotkeyKeys.Length)
            return;

        GlobalHelper.Config.HotkeyKey = HotkeyKeys[index];
        ConfigManager.SaveConfig(GlobalHelper.Config);
    }

    private void UpdateColorPreviews()
    {
        if (BackgroundTheme.TryParse(BgStartBox.Text, out Color c1))
            BgStartPreview.Background = new SolidColorBrush(c1);

        if (BackgroundTheme.TryParse(BgEndBox.Text, out Color c2))
            BgEndPreview.Background = new SolidColorBrush(c2);
    }

    private void UpdateCustomColorPanel()
        => CustomColorPanel.IsVisible = BackgroundThemeComboBox.SelectedIndex == 2;

    private void ApplyBackground()
        => BackgroundTheme.Apply(this, NavigationTabs);

    /// <summary>
    /// 按名字过滤皮肤/帽子格子。614 个条目靠滚动找太费劲，输入关键字即时筛选。
    /// </summary>
    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        string query = (sender as TextBox)?.Text?.Trim() ?? string.Empty;

        foreach ((Border cell, string name) in _allCells)
        {
            cell.IsVisible = query.Length == 0
                || name.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void OnBackgroundThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateCustomColorPanel();

        // 初始化阶段由 InitBackgroundSettings 统一处理，不写盘
        if (!_backgroundUiReady)
            return;

        GlobalHelper.Config.BackgroundTheme = BackgroundTheme.FromIndex(BackgroundThemeComboBox.SelectedIndex);
        ApplyBackground();
        ConfigManager.SaveConfig(GlobalHelper.Config);
    }

    private void OnBackgroundColorChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_backgroundUiReady)
            return;

        // 只接受能解析的颜色：输入到一半的 "#FF" 之类不写入配置
        if (BackgroundTheme.TryParse(BgStartBox.Text, out _))
            GlobalHelper.Config.BackgroundStart =
                BackgroundTheme.Normalize(BgStartBox.Text, BackgroundTheme.YellowOrangePreset.Start);

        if (BackgroundTheme.TryParse(BgEndBox.Text, out _))
            GlobalHelper.Config.BackgroundEnd =
                BackgroundTheme.Normalize(BgEndBox.Text, BackgroundTheme.YellowOrangePreset.End);

        UpdateColorPreviews();
        ApplyBackground();
        ConfigManager.SaveConfig(GlobalHelper.Config);
    }

    private void CreateItemList<T>(IEnumerable<KeyValuePair<string, T>> items, string type, Func<KeyValuePair<string, T>, string> getIcon, Func<KeyValuePair<string, T>, string> getTags, Func<KeyValuePair<string, T>, string> getName, string currentId, ref Border lastBorder)
    {
        foreach (KeyValuePair<string, T> item in items)
        {
            Image mainImage = new()
            {
                // 格子里只显示 50px，按 64px 解码即可（原图 256×256，全尺寸解码会白白吃掉上百 MB）
                Source = LoadAsset($"{type}/{getIcon(item)}", 64),
                Width = 50,
                Height = 50
            };

            Image badgeImage = new()
            {
                Name = "Badge",
                Source = LoadAsset("EmoteEquipped.png", 24),
                IsVisible = false,
                Width = 20,
                Height = 20,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                Margin = new Thickness(-5, 0, 0, -5)
            };

            Grid grid = new();
            grid.Children.Add(mainImage);
            grid.Children.Add(badgeImage);

            Border border = new()
            {
                Tag = new Dictionary<string, string>
                {
                    ["Id"] = item.Key,
                    ["Type"] = type
                },
                Child = grid,
                // 暖色渐变背景上铺一层 60% 白，保证品质边框（尤其银色）仍然清晰
                Background = new SolidColorBrush(Color.Parse("#96FFFFFF")),
                BorderBrush = SkinManager.GetQuality(getTags(item)),
                BorderThickness = new Thickness(5),
                Margin = new Thickness(0, 0, 5, 5)
            };

            if (currentId == item.Key)
            {
                border.Background = Brushes.HotPink;
                ShowBadge(border, true);
                lastBorder = border;
            }

            ToolTip.SetTip(border, getName(item));
            ToolTip.SetPlacement(border, PlacementMode.Top);
            ToolTip.SetVerticalOffset(border, 0);
            border.Classes.Add("Animation");
            border.PointerPressed += Border_PointerPressed;
            DefaultSkinList.Children.Add(border);
            _allCells.Add((border, getName(item)));
        }
    }

    private void Border_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is Dictionary<string, string> tagData)
        {
            PointerPoint point = e.GetCurrentPoint(border);
            if (point.Properties.IsLeftButtonPressed && tagData.TryGetValue("Type", out var type) && tagData.TryGetValue("Id", out var id))
            {
                switch (type)
                {
                    case "skin":
                        if (LastSkin != null)
                        {
                            LastSkin.Background = Brushes.Transparent;
                            ShowBadge(LastSkin, false);
                        }

                        if (GlobalHelper.CatSkin.SkinId == id)
                        {
                            App.MainWindow.SetSkin("0");
                            ShowBadge(border, false);
                        }
                        else
                        {
                            border.Background = Brushes.HotPink;
                            ShowBadge(border, true);
                            App.MainWindow.SetSkin(id);
                        }
                        LastSkin = border;
                        break;
                    case "hat":
                        if (LastHat != null)
                        {
                            LastHat.Background = Brushes.Transparent;
                            ShowBadge(LastHat, false);
                        }

                        if (GlobalHelper.CatSkin.HatId == id)
                        {
                            App.MainWindow.SetHat("0");
                            ShowBadge(border, false);
                        }
                        else
                        {
                            border.Background = Brushes.HotPink;
                            ShowBadge(border, true);
                            App.MainWindow.SetHat(id);
                            App.MainWindow.HatAnimation();
                        }
                        LastHat = border;
                        break;
                }
            }
        }
    }

    private static void ShowBadge(Border border, bool isShow)
    {
        if (border.Child is Grid grid)
        {
            foreach (Control child in grid.Children)
            {
                if (child is Image img && img.Name == "Badge")
                    img.IsVisible = isShow;
            }
        }
    }

    public void NavigateToPage(string pageName)
    {
        if (NavigationTabs == null)
            return;

        NavigationTabs.SelectedIndex = pageName switch
        {
            "Page1" => 0,
            "Page2" => 1,
            "Setting" => 2,
            _ => 0,
        };
    }

    public static void ShowOrActivate(Window owner, string? navigateTo = null)
    {
        if (CurrentInstance != null && !CurrentInstance.IsClosed)
        {
            CurrentInstance.Activate();
            CurrentInstance.Topmost = true;
            CurrentInstance.Topmost = false;

            if (!string.IsNullOrEmpty(navigateTo))
                CurrentInstance.NavigateToPage(navigateTo);
            return;
        }

        CurrentInstance = new SettingWindow
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (!string.IsNullOrEmpty(navigateTo))
            CurrentInstance.NavigateToPage(navigateTo);

        CurrentInstance.Show();
    }

    private bool _isClosed = false;
    public bool IsClosed => _isClosed;

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _isClosed = true;
    }

    private void OnLangListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            var selectedItem = comboBox.SelectedItem;
            if (selectedItem != null)
            {
                foreach (KeyValuePair<string, string> lang in LangList)
                {
                    if (lang.Value == (string)selectedItem)
                    {
                        Localization.LoadLanguage(lang.Key);
                        GlobalHelper.Config.Language = lang.Key;

                        //解决切换语言后，组合框显示内容不变的问题
                        int value = RandomSkinComboBox.SelectedIndex;
                        RandomSkinComboBox.SelectedIndex = -1;
                        RandomSkinComboBox.SelectedIndex = value;
                    }
                }
            }
        }
    }

    private void OnTopmostClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            App.MainWindow.Topmost = GlobalHelper.Config.Topmost = checkBox.IsChecked.GetValueOrDefault();
    }

    private void OnMousePenetrationClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            App.MainWindow.EnableMousePenetration(checkBox.IsChecked.GetValueOrDefault());
    }

    private void OnAutorunClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            try
            {
                if (checkBox.IsChecked.GetValueOrDefault())
                {
                    if (Environment.ProcessPath is string file)
                        AutorunManager.Add(GlobalHelper.Name, file);
                }
                else
                {
                    AutorunManager.Remove(GlobalHelper.Name);
                }
            }
            catch { }
        }
    }

    private void OnTaskbarIconClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            App.MainWindow.ShowInTaskbar = GlobalHelper.Config.TaskbarIcon = checkBox.IsChecked.GetValueOrDefault();
    }

    private void OnFlipClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            App.MainWindow.SetFlip(checkBox.IsChecked.GetValueOrDefault());
    }

    private void OnZoomSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
            App.MainWindow.SetZoom(comboBox.SelectedIndex);
    }

    private void OnBobbingClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            App.MainWindow.SetBobbing(checkBox.IsChecked.GetValueOrDefault());
    }

    private void OnDisableDragClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            GlobalHelper.Config.DisableDrag = checkBox.IsChecked.GetValueOrDefault();
    }

    private void OnAdsorptionClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            GlobalHelper.Config.Adsorption = checkBox.IsChecked.GetValueOrDefault();
    }

    private void OnRandomSkinSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
            App.MainWindow.RandomSkin(comboBox.SelectedIndex);
    }

    private void OnHomeClicked(object? sender, RoutedEventArgs e)
    {
        Launcher.LaunchUriAsync(new Uri(GlobalHelper.Url));
    }

    private async void OnUpdateClicked(object? sender, RoutedEventArgs e)
    {
        // 先打开页面，保证点击立刻有反馈；再在后台比较版本
        Launcher.LaunchUriAsync(new Uri(GlobalHelper.Url + "/releases/latest"));

        string? latest = await UpdateChecker.GetLatestTagAsync();
        if (UpdateChecker.IsNewer(latest, GlobalHelper.Version) && sender is Button button)
            button.Content = $"↑ {latest}";
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
