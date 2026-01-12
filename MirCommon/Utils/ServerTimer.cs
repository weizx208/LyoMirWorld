using System;
using System.Diagnostics;

namespace MirCommon.Utils
{
    
    
    
    
    public class ServerTimer
    {
        private uint _savedTime;
        private uint _timeoutTime;
        private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        
        
        
        public ServerTimer()
        {
            _savedTime = 0;
            _timeoutTime = 0;
        }

        
        
        
        
        public static uint GetTime()
        {
            return (uint)_stopwatch.ElapsedMilliseconds;
        }

        
        
        
        
        public void SaveTime()
        {
            _savedTime = GetTime();
        }

        
        
        
        
        public void SaveTime(uint newTimeout)
        {
            SetInterval(newTimeout);
            SaveTime();
        }

        
        
        
        
        public static bool IsTimeOut(uint startTime, uint timeout)
        {
            uint currentTime = GetTime();
            return GetTimeToTime(startTime, currentTime) >= timeout;
        }

        
        
        
        
        public bool IsTimeOut(uint timeout)
        {
            uint currentTime = GetTime();
            return GetTimeToTime(_savedTime, currentTime) >= timeout;
        }

        
        
        
        
        public void SetInterval(uint interval)
        {
            _savedTime = GetTime();
            _timeoutTime = interval;
        }

        
        
        
        
        public bool IsTimeOut()
        {
            uint currentTime = GetTime();
            return GetTimeToTime(_savedTime, currentTime) >= _timeoutTime;
        }

        
        
        
        
        public uint GetTimeout()
        {
            return _timeoutTime;
        }

        
        
        
        
        public uint GetSavedTime()
        {
            return _savedTime;
        }

        
        
        
        
        public void SetSavedTime(uint time)
        {
            _savedTime = time;
        }

        
        
        
        public void Reset()
        {
            SaveTime();
        }

        
        
        
        public uint GetRemainingTime()
        {
            if (_timeoutTime == 0)
                return 0;

            uint elapsed = GetTimeToTime(_savedTime, GetTime());
            return elapsed >= _timeoutTime ? 0 : _timeoutTime - elapsed;
        }

        
        
        
        public uint GetElapsedTime()
        {
            return GetTimeToTime(_savedTime, GetTime());
        }

        
        
        
        
        private static uint GetTimeToTime(uint t1, uint t2)
        {
            const uint MAX_TIME = uint.MaxValue;
            return t1 <= t2 ? (t2 - t1) : (MAX_TIME - t1 + t2);
        }

        
        
        
        public override string ToString()
        {
            return $"ServerTimer[Saved={_savedTime}, Timeout={_timeoutTime}, Remaining={GetRemainingTime()}]";
        }
    }
}
