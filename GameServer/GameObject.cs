using System;
using System.Collections.Generic;
using System.Threading;
using MirCommon;

namespace GameServer
{
    
    
    
    
    public abstract class GameObject
    {
        private static uint _nextObjectId = 1;
        
        public uint ObjectId { get; protected set; }
        public uint InstanceKey { get; protected set; }
        private int _referenceCount = 0;

        protected GameObject()
        {
            ObjectId = Interlocked.Increment(ref _nextObjectId);
            InstanceKey = (uint)DateTime.Now.Ticks;
        }

        public virtual void Clean()
        {
            
        }

        public virtual void Update()
        {
            
        }

        public int AddRef()
        {
            return Interlocked.Increment(ref _referenceCount);
        }

        public int DecRef()
        {
            int count = Interlocked.Decrement(ref _referenceCount);
            if (count < 0)
            {
                throw new InvalidOperationException("引用计数不能为负");
            }
            return count;
        }

        public int GetRefCount() => _referenceCount;
    }

    
    
    
    
    public abstract class MapObject : GameObject
    {
        public int MapId { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public LogicMap? CurrentMap { get; set; }
        
        
        public abstract ObjectType GetObjectType();
        
        
        public bool IsVisible { get; set; } = true;
        
        protected MapObject()
        {
            MapId = -1;
            X = 0;
            Y = 0;
        }

        
        
        
        public virtual void SetPosition(ushort x, ushort y)
        {
            ushort oldX = X;
            ushort oldY = Y;
            X = x;
            Y = y;
            OnPositionChanged(oldX, oldY, x, y);
        }

        
        
        
        public virtual bool EnterMap(LogicMap map, ushort x, ushort y)
        {
            if (CurrentMap != null)
            {
                LeaveMap();
            }

            CurrentMap = map;
            MapId = (int)map.MapId;
            X = x;
            Y = y;

            OnEnterMap(map);
            return true;
        }

        
        
        
        public virtual bool LeaveMap()
        {
            if (CurrentMap == null)
                return false;

            var map = CurrentMap;
            CurrentMap = null;
            MapId = -1;

            OnLeaveMap(map);
            return true;
        }

        
        
        
        public abstract bool GetViewMsg(out byte[] msg, MapObject? viewer = null);

        
        protected virtual void OnPositionChanged(ushort oldX, ushort oldY, ushort newX, ushort newY) { }
        protected virtual void OnEnterMap(LogicMap map) { }
        protected virtual void OnLeaveMap(LogicMap map) { }
    }

    
    
    
    public enum MirObjectType : byte
    {
        DownItem = 0,
        Monster = 1,
        NPC = 2,
        Player = 3,
        VisibleEvent = 4,
    }

    
    
    
    public static class ObjectIdUtil
    {
        public static uint MakeObjectId(MirObjectType type, uint seq24)
        {
            return (seq24 & 0x00FFFFFFu) | ((uint)type << 24);
        }

        public static uint GetIndex(uint objectId) => objectId & 0x00FFFFFFu;

        public static MirObjectType GetType(uint objectId) => (MirObjectType)((objectId >> 24) & 0xFF);
    }

    
    
    
    public enum ObjectType
    {
        Player = 0,         
        NPC = 1,           
        Monster = 2,       
        Item = 3,          
        Event = 4,         
        VisibleEvent = 5,  
        Map = 6,           
        ScriptEvent = 7,   
        DownItem = 8,      
        Max = 9,
        MineSpot = 10,    
        MonsterCorpse = 11, 
        Pet = 12
    }

    
    
    
    public class VisibleObject
    {
        public MapObject? Object { get; set; }
        public uint UpdateFlag { get; set; }
        
        public VisibleObject()
        {
            Object = null;
            UpdateFlag = 0;
        }
    }

    
    
    
    public class ObjectReference<T> where T : GameObject
    {
        private T? _object;
        private uint _instanceKey;

        public void SetObject(T? obj)
        {
            if (_object != null)
            {
                _object.DecRef();
            }

            _object = obj;
            if (_object != null)
            {
                _object.AddRef();
                _instanceKey = _object.InstanceKey;
            }
            else
            {
                _instanceKey = 0;
            }
        }

        public T? GetObject()
        {
            return IsValid() ? _object : null;
        }

        public bool IsValid()
        {
            if (_object == null)
                return false;
            
            return _object.InstanceKey == _instanceKey;
        }

        public void Clear()
        {
            SetObject(null);
        }
    }

    
    
    
    public class ObjectProcess
    {
        public ProcessType Type { get; set; }
        public uint Param1 { get; set; }
        public uint Param2 { get; set; }
        public uint Param3 { get; set; }
        public uint Param4 { get; set; }
        public uint Delay { get; set; }
        public int RepeatTimes { get; set; }
        public string? StringParam { get; set; }
        public DateTime ExecuteTime { get; set; }

        public ObjectProcess(ProcessType type)
        {
            Type = type;
            ExecuteTime = DateTime.Now;
        }

        public bool ShouldExecute()
        {
            return DateTime.Now >= ExecuteTime.AddMilliseconds(Delay);
        }
    }

    
    
    
    public enum ProcessType
    {
        None = 0,
        BeAttack = 1,           
        BeMagicAttack = 2,      
        ClearStatus = 3,        
        Die = 4,                
        Relive = 5,             
        TakeDamage = 6,         
        Heal = 7,               
        AddBuff = 8,            
        RemoveBuff = 9,         
        Cast = 10,              
        ChangeMap = 11,         
        Max = 12
    }
}
