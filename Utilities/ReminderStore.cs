using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using BongoCatNotice.Models;

namespace BongoCatNotice.Utilities
{
    /// <summary>
    /// 提醒的存储与调度。集合改动会自动防抖落盘，调度器每 5 秒检查一次是否到点。
    /// </summary>
    public static class ReminderStore
    {
        /// <summary>界面绑定的提醒列表</summary>
        public static ObservableCollection<ReminderItem> Items { get; } = new();

        private static readonly HashSet<string> FiredKeys = new();
        private static DispatcherTimer? _saveTimer;
        private static DispatcherTimer? _tickTimer;
        private static NoticePresenter? _presenter;

        /// <summary>从配置读入（程序启动时调一次）</summary>
        public static void Load()
        {
            Items.Clear();
            List<ReminderItem>? saved = GlobalHelper.Config.Reminders;
            if (saved is null)
            {
                return;
            }

            foreach (ReminderItem item in saved)
            {
                if (string.IsNullOrEmpty(item.Id))
                {
                    item.Id = Guid.NewGuid().ToString("N");
                }
                Items.Add(item);
            }
        }

        public static ReminderItem Add()
        {
            var item = new ReminderItem
            {
                Hour = 9,
                Minute = 0,
                Title = "该喝水啦 ~",
                Message = "每日提醒",
                RepeatMode = (int)ReminderRepeat.Daily,
                Enabled = true,
                AutoCloseSeconds = 8
            };
            Items.Add(item);
            SaveNow();
            return item;
        }

        public static void Remove(ReminderItem item)
        {
            Items.Remove(item);
            SaveNow();
        }

        /// <summary>属性改动后延迟 800ms 落盘，避免每敲一个字都写文件</summary>
        public static void MarkDirty()
        {
            if (_saveTimer is null)
            {
                _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
                _saveTimer.Tick += (_, _) =>
                {
                    _saveTimer!.Stop();
                    SaveNow();
                };
            }

            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public static void SaveNow()
        {
            try
            {
                GlobalHelper.Config.Reminders = new List<ReminderItem>(Items);
                ConfigManager.SaveConfig(GlobalHelper.Config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"提醒保存失败: {ex.Message}");
            }
        }

        /// <summary>启动调度</summary>
        public static void Start(NoticePresenter presenter)
        {
            _presenter = presenter;
            if (_tickTimer is not null)
            {
                return;
            }

            _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _tickTimer.Tick += (_, _) => Tick();
            _tickTimer.Start();
        }

        public static void Stop()
        {
            _tickTimer?.Stop();
            _tickTimer = null;
            _presenter = null;
        }

        /// <summary>立即预览一条提醒（不走定时判断，用于设置面板里的"测试"）</summary>
        public static void Preview(ReminderItem item)
        {
            _presenter?.Enqueue(item.DisplayTitle, item.Message, item.AutoCloseSeconds);
        }

        private static void Tick()
        {
            if (_presenter is null || !GlobalHelper.Config.RemindersEnabled)
            {
                return;
            }

            DateTime now = DateTime.Now;
            string minuteKey = now.ToString("yyyyMMddHHmm");

            foreach (ReminderItem item in Items)
            {
                if (!item.ShouldFireAt(now))
                {
                    continue;
                }

                // 同一分钟内只触发一次
                if (!FiredKeys.Add(minuteKey + ":" + item.Id))
                {
                    continue;
                }

                _presenter.Enqueue(item.DisplayTitle, item.Message, item.AutoCloseSeconds);

                if ((ReminderRepeat)item.RepeatMode == ReminderRepeat.Once)
                {
                    item.Enabled = false;   // setter 里会触发 MarkDirty 保存
                }
            }

            if (FiredKeys.Count > 500)
            {
                FiredKeys.Clear();
            }
        }
    }
}
