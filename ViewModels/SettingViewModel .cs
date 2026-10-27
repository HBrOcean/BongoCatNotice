using System;
using System.ComponentModel;
using BongoCatNotice.Utilities;

namespace BongoCatNotice.ViewModels
{
    public class SettingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public static bool IsWindows => OperatingSystem.IsWindows();

        public static string DefaultSkinText => Localization.Instance["SettingWindow.DefaultSkin.Header"];
        public static string SearchPlaceholderText => Localization.Instance["SettingWindow.DefaultSkin.Search"];
        public static string CustomSkinText => Localization.Instance["SettingWindow.CustomSkin.Header"];
        public static string EmptySkinText => Localization.Instance["SettingWindow.CustomSkin.EmptySkin"];
        public static string SettingText => Localization.Instance["SettingWindow.Setting.Header"];
        public static string SystemSettingText => Localization.Instance["SettingWindow.Setting.Setting"];
        public static string LanguageText => Localization.Instance["SettingWindow.Setting.Language"];
        public static string TopmostText => Localization.Instance["SettingWindow.Setting.Topmost"];
        public static string MousePenetrationText => Localization.Instance["SettingWindow.Setting.MousePenetration"];
        public static string AutorunText => Localization.Instance["SettingWindow.Setting.Autorun"];
        public static string TaskbarIconText => Localization.Instance["SettingWindow.Setting.TaskbarIcon"];
        public static string VisualsText => Localization.Instance["SettingWindow.Setting.Visuals"];
        public static string FlipText => Localization.Instance["SettingWindow.Setting.Flip"];
        public static string ZoomText => Localization.Instance["SettingWindow.Setting.Zoom"];
        public static string BobbingText => Localization.Instance["SettingWindow.Setting.Bobbing"];
        public static string InteractionText => Localization.Instance["SettingWindow.Setting.Interaction"];
        public static string DisableDragText => Localization.Instance["SettingWindow.Setting.DisableDrag"];
        public static string AdsorptionText => Localization.Instance["SettingWindow.Setting.Adsorption"];
        public static string RandomText => Localization.Instance["SettingWindow.Setting.Random"];
        public static string RandomSkinText => Localization.Instance["SettingWindow.Setting.RandomSkin"];
        public static string NeverText => Localization.Instance["SettingWindow.Setting.Never"];
        public static string OneMinuteText => Localization.Instance["SettingWindow.Setting.OneMinute"];
        public static string ThreeMinutesText => Localization.Instance["SettingWindow.Setting.ThreeMinutes"];
        public static string FiveMinutesText => Localization.Instance["SettingWindow.Setting.FiveMinutes"];
        public static string FifteenMinutesText => Localization.Instance["SettingWindow.Setting.FifteenMinutes"];
        public static string ThirtyMinutesText => Localization.Instance["SettingWindow.Setting.ThirtyMinutes"];
        public static string VersionText => Localization.Instance["SettingWindow.Setting.Version"];
        public static string BackgroundText => Localization.Instance["SettingWindow.Setting.Background"];
        public static string HotkeyText => Localization.Instance["SettingWindow.Setting.Hotkey"];
        public static string BgYellowOrangeText => Localization.Instance["SettingWindow.Setting.BgYellowOrange"];
        public static string BgBlueWhiteText => Localization.Instance["SettingWindow.Setting.BgBlueWhite"];
        public static string BgCustomText => Localization.Instance["SettingWindow.Setting.BgCustom"];
        public static string BgStartText => Localization.Instance["SettingWindow.Setting.BgStart"];
        public static string BgEndText => Localization.Instance["SettingWindow.Setting.BgEnd"];
        public static string BgHintText => Localization.Instance["SettingWindow.Setting.BgHint"];
        public static string GlassText => Localization.Instance["SettingWindow.Setting.Glass"];
        public static string GlassToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Glass"];
        public static bool GlassValue => GlobalHelper.Config.GlassEffect;
        public static int BgThemeIndexValue => BackgroundTheme.ToIndex(GlobalHelper.Config.BackgroundTheme);
        public static string HomeText => Localization.Instance["SettingWindow.Setting.Home"];
        public static string UpdateText => Localization.Instance["SettingWindow.Setting.Update"];
        public static string ExitAppText => Localization.Instance["SettingWindow.Setting.ExitApp"];
        public static string ExitText => Localization.Instance["SettingWindow.Setting.Exit"];
        public static string LanguageToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Language"];
        public static string TopmostToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Topmost"];
        public static string MousePenetrationToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.MousePenetration"];
        public static string AutorunToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Autorun"];
        public static string TaskbarIconToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.TaskbarIcon"];
        public static string FlipToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Flip"];
        public static string ZoomToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Zoom"];
        public static string BobbingToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Bobbing"];
        public static string DisableDragToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.DisableDrag"];
        public static string AdsorptionToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Adsorption"];
        public static string RandomSkinToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.RandomSkin"];
        public static string HomeToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Home"];
        public static string UpdateToolTipText => Localization.Instance["SettingWindow.Setting.ToolTip.Update"];

        // ---- 定时提醒 ----
        public static string ReminderHeaderText => Localization.Instance["SettingWindow.Reminder.Header"];
        public static string ReminderHintText => Localization.Instance["SettingWindow.Reminder.Hint"];
        public static string ReminderAddText => Localization.Instance["SettingWindow.Reminder.Add"];
        public static string ReminderEmptyText => Localization.Instance["SettingWindow.Reminder.Empty"];
        public static string ReminderRepeatDailyText => Localization.Instance["SettingWindow.Reminder.RepeatDaily"];
        public static string ReminderRepeatWeekdayText => Localization.Instance["SettingWindow.Reminder.RepeatWeekday"];
        public static string ReminderRepeatOnceText => Localization.Instance["SettingWindow.Reminder.RepeatOnce"];
        public static string ReminderTimeToolTipText => Localization.Instance["SettingWindow.Reminder.ToolTip.Time"];
        public static string ReminderTitleToolTipText => Localization.Instance["SettingWindow.Reminder.ToolTip.Title"];
        public static string ReminderMessageToolTipText => Localization.Instance["SettingWindow.Reminder.ToolTip.Message"];
        public static string ReminderAutoCloseToolTipText => Localization.Instance["SettingWindow.Reminder.ToolTip.AutoClose"];
        public static string ReminderDeleteToolTipText => Localization.Instance["SettingWindow.Reminder.ToolTip.Delete"];
        public static string ReminderTestText => Localization.Instance["SettingWindow.Reminder.Test"];
        public static string ReminderTestToolTipText => Localization.Instance["SettingWindow.Reminder.ToolTip.Test"];
        public static string ReminderEnabledText => Localization.Instance["SettingWindow.Reminder.Enabled"];
        public static string ReminderMasterText => Localization.Instance["SettingWindow.Reminder.Master"];
        public static string ReminderMasterToolTipText => Localization.Instance["SettingWindow.Reminder.ToolTip.Master"];
        public static string ReminderOffHintText => Localization.Instance["SettingWindow.Reminder.OffHint"];
        public static bool RemindersOnValue => GlobalHelper.Config.RemindersEnabled;

        /// <summary>提醒列表，直接绑定 ReminderStore 的集合</summary>
        public System.Collections.ObjectModel.ObservableCollection<BongoCatNotice.Models.ReminderItem> Reminders
            => ReminderStore.Items;

        public bool HasReminders => ReminderStore.Items.Count > 0;

        public static bool TopmostValue => GlobalHelper.Config.Topmost;
        public static bool MousePenetrationValue => GlobalHelper.Config.MousePenetration;
        public static bool AutorunValue => GlobalHelper.Config.Autorun;
        public static bool TaskbarIconValue => GlobalHelper.Config.TaskbarIcon;
        public static bool FlipValue => GlobalHelper.Config.Flip;
        public static int ZoomValue => GlobalHelper.Config.Zoom;
        public static bool BobbingValue => GlobalHelper.Config.Bobbing;
        public static bool DisableDragValue => GlobalHelper.Config.DisableDrag;
        public static bool AdsorptionValue => GlobalHelper.Config.Adsorption;

        public static int RandomSkinValue => GlobalHelper.Config.RandomSkin;

        public SettingViewModel()
        {
            ReminderStore.Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasReminders));

            Localization.LanguageChanged += () =>
            {
                OnPropertyChanged(nameof(DefaultSkinText));
                OnPropertyChanged(nameof(CustomSkinText));
                OnPropertyChanged(nameof(EmptySkinText));
                OnPropertyChanged(nameof(SettingText));
                OnPropertyChanged(nameof(SystemSettingText));
                OnPropertyChanged(nameof(LanguageText));
                OnPropertyChanged(nameof(TopmostText));
                OnPropertyChanged(nameof(MousePenetrationText));
                OnPropertyChanged(nameof(AutorunText));
                OnPropertyChanged(nameof(TaskbarIconText));
                OnPropertyChanged(nameof(VisualsText));
                OnPropertyChanged(nameof(FlipText));
                OnPropertyChanged(nameof(ZoomText));
                OnPropertyChanged(nameof(BobbingText));
                OnPropertyChanged(nameof(InteractionText));
                OnPropertyChanged(nameof(DisableDragText));
                OnPropertyChanged(nameof(AdsorptionText));
                OnPropertyChanged(nameof(RandomText));
                OnPropertyChanged(nameof(RandomSkinText));
                OnPropertyChanged(nameof(NeverText));
                OnPropertyChanged(nameof(OneMinuteText));
                OnPropertyChanged(nameof(ThreeMinutesText));
                OnPropertyChanged(nameof(FiveMinutesText));
                OnPropertyChanged(nameof(FifteenMinutesText));
                OnPropertyChanged(nameof(ThirtyMinutesText));
                OnPropertyChanged(nameof(VersionText));
                OnPropertyChanged(nameof(HomeText));
                OnPropertyChanged(nameof(UpdateText));
                OnPropertyChanged(nameof(ExitAppText));
                OnPropertyChanged(nameof(ExitText));
                OnPropertyChanged(nameof(LanguageToolTipText));
                OnPropertyChanged(nameof(TopmostToolTipText));
                OnPropertyChanged(nameof(MousePenetrationToolTipText));
                OnPropertyChanged(nameof(AutorunToolTipText));
                OnPropertyChanged(nameof(TaskbarIconToolTipText));
                OnPropertyChanged(nameof(FlipToolTipText));
                OnPropertyChanged(nameof(ZoomToolTipText));
                OnPropertyChanged(nameof(BobbingToolTipText));
                OnPropertyChanged(nameof(DisableDragToolTipText));
                OnPropertyChanged(nameof(AdsorptionToolTipText));
                OnPropertyChanged(nameof(RandomSkinToolTipText));
                OnPropertyChanged(nameof(HomeToolTipText));
                OnPropertyChanged(nameof(UpdateToolTipText));
                OnPropertyChanged(nameof(GlassText));
                OnPropertyChanged(nameof(GlassToolTipText));
                OnPropertyChanged(nameof(ReminderHeaderText));
                OnPropertyChanged(nameof(ReminderHintText));
                OnPropertyChanged(nameof(ReminderAddText));
                OnPropertyChanged(nameof(ReminderEmptyText));
                OnPropertyChanged(nameof(ReminderRepeatDailyText));
                OnPropertyChanged(nameof(ReminderRepeatWeekdayText));
                OnPropertyChanged(nameof(ReminderRepeatOnceText));
                OnPropertyChanged(nameof(ReminderTimeToolTipText));
                OnPropertyChanged(nameof(ReminderTitleToolTipText));
                OnPropertyChanged(nameof(ReminderMessageToolTipText));
                OnPropertyChanged(nameof(ReminderAutoCloseToolTipText));
                OnPropertyChanged(nameof(ReminderDeleteToolTipText));
                OnPropertyChanged(nameof(ReminderTestText));
                OnPropertyChanged(nameof(ReminderTestToolTipText));
                OnPropertyChanged(nameof(ReminderEnabledText));
                OnPropertyChanged(nameof(ReminderMasterText));
                OnPropertyChanged(nameof(ReminderMasterToolTipText));
                OnPropertyChanged(nameof(ReminderOffHintText));
            };
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
