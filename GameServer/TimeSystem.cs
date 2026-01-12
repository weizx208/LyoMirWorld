using System;
using System.Collections.Generic;

namespace GameServer
{
    
    
    
    
    public interface ITimeEventObject
    {
        void OnMinuteChange(DateTime currentTime);
        void OnHourChange(DateTime currentTime);
        void OnDayChange(DateTime currentTime);
        void OnMonthChange(DateTime currentTime);
        void OnYearChange(DateTime currentTime);
    }

    
    
    
    
    public class TimeSystem
    {
        private static TimeSystem _instance;
        private DateTime _lastUpdateTime;
        private DateTime _startupTime;
        private DateTime _currentTime;
        private Queue<ITimeEventObject> _timeEventQueue;
        private ushort _currentGameTime; 

        
        
        
        public static TimeSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TimeSystem();
                }
                return _instance;
            }
        }

        
        
        
        private TimeSystem()
        {
            _lastUpdateTime = DateTime.Now;
            _startupTime = DateTime.Now;
            _currentTime = DateTime.Now;
            _timeEventQueue = new Queue<ITimeEventObject>();
            _currentGameTime = CalculateGameTime(_startupTime);
        }

        
        
        
        
        private ushort CalculateGameTime(DateTime time)
        {
            return (ushort)(time.Hour * 4 + time.Minute / 15);
        }

        
        
        
        public ushort GetCurrentTime()
        {
            return _currentGameTime;
        }

        
        
        
        public bool RegisterTimeEvent(ITimeEventObject timeEvent)
        {
            if (timeEvent == null)
                return false;

            _timeEventQueue.Enqueue(timeEvent);
            return true;
        }

        
        
        
        
        public void Update()
        {
            
            TimeSpan elapsed = DateTime.Now - _lastUpdateTime;
            if (elapsed.TotalMilliseconds >= 60000)
            {
                _lastUpdateTime = DateTime.Now;
                
                DateTime oldTime = _currentTime;
                _currentTime = DateTime.Now;

                
                if (oldTime.Year != _currentTime.Year)
                    OnYearChange();
                
                if (oldTime.Month != _currentTime.Month)
                    OnMonthChange();
                
                if (oldTime.Day != _currentTime.Day)
                    OnDayChange();
                
                if (oldTime.Hour != _currentTime.Hour)
                    OnHourChange();
                
                if (oldTime.Minute != _currentTime.Minute)
                    OnMinuteChange();
            }
        }

        
        
        
        private void OnMinuteChange()
        {
            
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnMinuteChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }

                
                ushort newGameTime = CalculateGameTime(_currentTime);
                if (newGameTime != _currentGameTime)
                {
                    
                    
                    var process = new GlobeProcess(GlobeProcessType.TimeSystemUpdate, (uint)newGameTime);
                    GameWorld.Instance?.AddGlobeProcess(process);
                    _currentGameTime = newGameTime;
                }
        }

        
        
        
        private void OnHourChange()
        {
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnHourChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }
        }

        
        
        
        private void OnDayChange()
        {
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnDayChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }
        }

        
        
        
        private void OnMonthChange()
        {
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnMonthChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }
        }

        
        
        
        private void OnYearChange()
        {
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnYearChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }
        }

        
        
        
        public DateTime GetStartupTime()
        {
            return _startupTime;
        }

        
        
        
        public DateTime GetCurrentSystemTime()
        {
            return _currentTime;
        }

        
        
        
        public int GetTimeEventCount()
        {
            return _timeEventQueue.Count;
        }
    }
}
