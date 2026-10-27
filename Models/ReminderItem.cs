using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using BongoCatNotice.Utilities;

namespace BongoCatNotice.Models
{
    /// <summary>重复方式</summary>
    public enum ReminderRepeat
    {
        Daily = 0,      // 每天
        Weekday = 1,    // 仅工作日
        Once = 2        // 仅一次
    }

    /// <summary>一条定时提醒</summary>
    public class ReminderItem : INotifyPropertyChanged
    {
        private int _hour = 9;
        private int _minute = 0;
        private string _title = "";
        private string _message = "";
        private int _repeatMode = (int)ReminderRepeat.Daily;
        private bool _enabled = true;
        private int _autoCloseSeconds = 8;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public int Hour
        {
            get => _hour;
            set
            {
                if (Set(ref _hour, Math.Clamp(value, 0, 23)))
                {
                    OnPropertyChanged(nameof(TimeText));
                }
            }
        }

        public int Minute
        {
            get => _minute;
            set
            {
                if (Set(ref _minute, Math.Clamp(value, 0, 59)))
                {
                    OnPropertyChanged(nameof(TimeText));
                }
            }
        }

        /// <summary>时间编辑框绑定的 "HH:mm" 文本</summary>
        [JsonIgnore]
        public string TimeText
        {
            get => $"{_hour:00}:{_minute:00}";
            set
            {
                if (TryParseTime(value, out int h, out int m))
                {
                    Hour = h;
                    Minute = m;
                }
                // 解析失败时把界面文本刷回规范值
                OnPropertyChanged(nameof(TimeText));
            }
        }

        public string Title
        {
            get => _title;
            set => Set(ref _title, value ?? "");
        }

        public string Message
        {
            get => _message;
            set => Set(ref _message, value ?? "");
        }

        public int RepeatMode
        {
            get => _repeatMode;
            set => Set(ref _repeatMode, Math.Clamp(value, 0, 2));
        }

        public bool Enabled
        {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        public int AutoCloseSeconds
        {
            get => _autoCloseSeconds;
            set
            {
                if (Set(ref _autoCloseSeconds, Math.Clamp(value, 0, 120)))
                {
                    OnPropertyChanged(nameof(AutoCloseIndex));
                }
            }
        }

        /// <summary>自动关闭时长的下拉索引：3/5/8/15/30/不自动关</summary>
        [JsonIgnore]
        public int AutoCloseIndex
        {
            get => _autoCloseSeconds switch
            {
                3 => 0,
                5 => 1,
                8 => 2,
                15 => 3,
                30 => 4,
                _ => 5
            };
            set => AutoCloseSeconds = value switch
            {
                0 => 3,
                1 => 5,
                2 => 8,
                3 => 15,
                4 => 30,
                _ => 0
            };
        }

        [JsonIgnore]
        public string DisplayTitle => string.IsNullOrWhiteSpace(_title)
            ? Localization.Instance["SettingWindow.Reminder.DefaultTitle"]
            : _title;

        [JsonIgnore]
        public string RepeatDailyLabel => Localization.Instance["SettingWindow.Reminder.RepeatDaily"];

        [JsonIgnore]
        public string RepeatWeekdayLabel => Localization.Instance["SettingWindow.Reminder.RepeatWeekday"];

        [JsonIgnore]
        public string RepeatOnceLabel => Localization.Instance["SettingWindow.Reminder.RepeatOnce"];

        [JsonIgnore]
        public string EnabledLabel => Localization.Instance["SettingWindow.Reminder.Enabled"];

        [JsonIgnore]
        public string TimeTip => Localization.Instance["SettingWindow.Reminder.ToolTip.Time"];

        [JsonIgnore]
        public string TitlePlaceholder => Localization.Instance["SettingWindow.Reminder.ToolTip.Title"];

        [JsonIgnore]
        public string MessagePlaceholder => Localization.Instance["SettingWindow.Reminder.ToolTip.Message"];

        [JsonIgnore]
        public string AutoCloseTip => Localization.Instance["SettingWindow.Reminder.ToolTip.AutoClose"];

        [JsonIgnore]
        public string DeleteTip => Localization.Instance["SettingWindow.Reminder.ToolTip.Delete"];

        /// <summary>这条提醒是否应该在指定时刻触发</summary>
        public bool ShouldFireAt(DateTime now)
        {
            if (!_enabled)
            {
                return false;
            }

            if (now.Hour != _hour || now.Minute != _minute)
            {
                return false;
            }

            if ((ReminderRepeat)_repeatMode == ReminderRepeat.Weekday &&
                (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday))
            {
                return false;
            }

            return true;
        }

        private static bool TryParseTime(string? text, out int hour, out int minute)
        {
            hour = 0;
            minute = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim().Replace('：', ':');
            string[] parts = text.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hour))
            {
                return false;
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minute))
            {
                return false;
            }

            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
            {
                return false;
            }

            return true;
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            ReminderStore.MarkDirty();
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
