using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    public struct EventTimeup
    {
        public ushort Year;
        public ushort Month;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort DayOfWeek;

        public EventTimeup()
        {
            Year = ushort.MaxValue;
            Month = ushort.MaxValue;
            Day = ushort.MaxValue;
            Hour = ushort.MaxValue;
            Minute = ushort.MaxValue;
            DayOfWeek = ushort.MaxValue;
        }

        public bool IsValid()
        {
            return Year != ushort.MaxValue || Month != ushort.MaxValue || Day != ushort.MaxValue ||
                   Hour != ushort.MaxValue || Minute != ushort.MaxValue || DayOfWeek != ushort.MaxValue;
        }

        public bool Matches(DateTime time)
        {
            if (Year != ushort.MaxValue && time.Year != Year) return false;
            if (Month != ushort.MaxValue && time.Month != Month) return false;
            if (Day != ushort.MaxValue && time.Day != Day) return false;
            if (Hour != ushort.MaxValue && time.Hour != Hour) return false;
            if (Minute != ushort.MaxValue && time.Minute != Minute) return false;
            if (DayOfWeek != ushort.MaxValue && (int)time.DayOfWeek != DayOfWeek) return false;
            
            return true;
        }
    }

    public class TimeScript
    {
        public string ScriptPage { get; set; } = string.Empty;
        public EventTimeup Timeup { get; set; }
        public TimeScript? Next { get; set; }
    }

    public class AutoScriptManager
    {
        private static AutoScriptManager? _instance;
        public static AutoScriptManager Instance => _instance ??= new AutoScriptManager();

        private TimeScript? _timeScripts;
        private HumanPlayer? _scriptTarget;
        private DateTime _lastMinuteCheck = DateTime.MinValue;

        private AutoScriptManager() { }

        public void Destroy()
        {
            ClearTimeScripts();
            _scriptTarget = null;
        }

        public void OnMinuteChange(DateTime currentTime)
        {
            if (_lastMinuteCheck.Year == currentTime.Year &&
                _lastMinuteCheck.Month == currentTime.Month &&
                _lastMinuteCheck.Day == currentTime.Day &&
                _lastMinuteCheck.Hour == currentTime.Hour &&
                _lastMinuteCheck.Minute == currentTime.Minute)
            {
                return;
            }

            _lastMinuteCheck = currentTime;
            
            var current = _timeScripts;
            while (current != null)
            {
                if (current.Timeup.Matches(currentTime))
                {
                    ExecuteTimeScript(current);
                }
                current = current.Next;
            }
        }

        public void Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"自动脚本文件不存在: {filePath}");
                return;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        var timePart = parts[0].Trim();
                        var scriptPart = parts[1].Trim();

                        var timeup = ParseEventTimeup(timePart);
                        if (timeup.IsValid())
                        {
                            AddTimeScript(ref timeup, scriptPart);
                            count++;
                        }
                    }
                }

                LogManager.Default.Info($"加载自动脚本: {count} 个时间脚本");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载自动脚本失败: {filePath}", exception: ex);
            }
        }

        public void AddTimeScript(ref EventTimeup timeup, string scriptPage)
        {
            var newScript = new TimeScript
            {
                ScriptPage = scriptPage,
                Timeup = timeup,
                Next = _timeScripts
            };

            _timeScripts = newScript;
        }

        public void Update()
        {
            var currentTime = DateTime.Now;
            OnMinuteChange(currentTime);
        }

        public HumanPlayer? GetScriptTarget() => _scriptTarget;

        public void SetScriptTarget(HumanPlayer? target)
        {
            _scriptTarget = target;
        }

        public List<TimeScript> GetAllTimeScripts()
        {
            var scripts = new List<TimeScript>();
            var current = _timeScripts;
            
            while (current != null)
            {
                scripts.Add(current);
                current = current.Next;
            }
            
            return scripts;
        }

        public void ClearTimeScripts()
        {
            _timeScripts = null;
        }

        private EventTimeup ParseEventTimeup(string timeStr)
        {
            var timeup = new EventTimeup();
            
            try
            {
                var parts = timeStr.Split('/');
                if (parts.Length >= 4)
                {
                    if (parts[0] != "*" && ushort.TryParse(parts[0], out ushort year))
                        timeup.Year = year;

                    if (parts[1] != "*" && ushort.TryParse(parts[1], out ushort month))
                        timeup.Month = month;

                    if (parts[2] != "*" && ushort.TryParse(parts[2], out ushort day))
                        timeup.Day = day;

                    var timeParts = parts[3].Split(':');
                    if (timeParts.Length >= 2)
                    {
                        if (timeParts[0] != "*" && ushort.TryParse(timeParts[0], out ushort hour))
                            timeup.Hour = hour;
                        
                        if (timeParts[1] != "*" && ushort.TryParse(timeParts[1], out ushort minute))
                            timeup.Minute = minute;
                    }

                    if (parts.Length > 4 && parts[4] != "*" && ushort.TryParse(parts[4], out ushort dayOfWeek))
                        timeup.DayOfWeek = dayOfWeek;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析事件时间失败: {timeStr}", exception: ex);
            }

            return timeup;
        }

        private void ExecuteTimeScript(TimeScript script)
        {
            try
            {
                LogManager.Default.Info($"执行自动脚本: {script.ScriptPage} 时间: {FormatEventTimeup(script.Timeup)}");
                
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"执行自动脚本失败: {script.ScriptPage}", exception: ex);
            }
        }

        private string FormatEventTimeup(EventTimeup timeup)
        {
            var year = timeup.Year != ushort.MaxValue ? timeup.Year.ToString() : "*";
            var month = timeup.Month != ushort.MaxValue ? timeup.Month.ToString() : "*";
            var day = timeup.Day != ushort.MaxValue ? timeup.Day.ToString() : "*";
            var hour = timeup.Hour != ushort.MaxValue ? timeup.Hour.ToString() : "*";
            var minute = timeup.Minute != ushort.MaxValue ? timeup.Minute.ToString("D2") : "*";
            var dayOfWeek = timeup.DayOfWeek != ushort.MaxValue ? timeup.DayOfWeek.ToString() : "*";
            
            return $"{year}/{month}/{day}/{hour}:{minute}/{dayOfWeek}";
        }
    }
}
