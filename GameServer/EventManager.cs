using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public static class MapEventFlags
    {
        public const ushort EVENTFLAG_ENTEREVENT = 0x8000;  
        public const ushort EVENTFLAG_LEAVEEVENT = 0x4000;  
        public const ushort EVENTFLAG_CITYEVENT = 0x2000;   
    }

    
    
    
    
    public class MapCellInfo
    {
        private readonly object _lock = new();

        
        
        
        public ushort EventFlag { get; set; }

        
        
        
        public LinkedList<MapObject> ObjectList { get; } = new LinkedList<MapObject>();

        
        
        
        public int GetObjectCount()
        {
            lock (_lock)
            {
                return ObjectList.Count;
            }
        }

        
        
        
        public LinkedListNode<MapObject>? GetFirstNode()
        {
            lock (_lock)
            {
                return ObjectList.First;
            }
        }

        
        
        
        public void AddObject(MapObject obj)
        {
            if (obj == null)
                return;

            lock (_lock)
            {
                ObjectList.AddLast(obj);
            }
        }

        
        
        
        public bool RemoveObject(MapObject obj)
        {
            if (obj == null)
                return false;

            lock (_lock)
            {
                return ObjectList.Remove(obj);
            }
        }

        
        
        
        public List<EventObject> GetEventObjectsSnapshot()
        {
            lock (_lock)
            {
                var list = new List<EventObject>();
                var node = ObjectList.First;
                while (node != null)
                {
                    if (node.Value is EventObject ev)
                        list.Add(ev);
                    node = node.Next;
                }
                return list;
            }
        }

        
        
        
        public bool HasEnterEventFlag()
        {
            return (EventFlag & MapEventFlags.EVENTFLAG_ENTEREVENT) != 0;
        }

        
        
        
        public bool HasLeaveEventFlag()
        {
            return (EventFlag & MapEventFlags.EVENTFLAG_LEAVEEVENT) != 0;
        }

        
        
        
        public void SetEnterEventFlag()
        {
            EventFlag |= MapEventFlags.EVENTFLAG_ENTEREVENT;
        }

        
        
        
        public void SetLeaveEventFlag()
        {
            EventFlag |= MapEventFlags.EVENTFLAG_LEAVEEVENT;
        }

        
        
        
        public void ClearEnterEventFlag()
        {
            EventFlag &= unchecked((ushort)~MapEventFlags.EVENTFLAG_ENTEREVENT);
        }

        
        
        
        public void ClearLeaveEventFlag()
        {
            EventFlag &= unchecked((ushort)~MapEventFlags.EVENTFLAG_LEAVEEVENT);
        }
    }

    
    
    
    public abstract class EventProcessor
    {
        public EventProcessor()
        {
        }

        
        
        
        public virtual void Update()
        {
        }

        
        
        
        public virtual void OnEnter(VisibleEvent visibleEvent, MapObject mapObject)
        {
        }

        
        
        
        public virtual void OnLeave(VisibleEvent visibleEvent, MapObject mapObject)
        {
        }

        
        
        
        public virtual void OnUpdate(VisibleEvent visibleEvent)
        {
        }

        
        
        
        public virtual void OnClose(VisibleEvent visibleEvent)
        {
        }

        
        
        
        public virtual void OnCreate(VisibleEvent visibleEvent)
        {
        }
    }

    
    
    
    public class EventObject : MapObject
    {
        protected bool _disabled;

        public EventObject()
        {
            _disabled = false;
        }

        
        
        
        public override void Clean()
        {
            base.Clean();
        }

        
        
        
        public virtual void OnEnter(MapObject mapObject)
        {
        }

        
        
        
        public virtual void OnLeave(MapObject mapObject)
        {
        }

        
        
        
        public virtual void Disable()
        {
            _disabled = true;
        }

        
        
        
        public virtual void Enable()
        {
            _disabled = false;
        }

        
        
        
        public bool IsDisabled()
        {
            return _disabled;
        }

        
        
        
        public void SetEnterFlag(LogicMap map)
        {
            var cellInfo = map.GetMapCellInfo(X, Y);
            if (cellInfo != null)
            {
                cellInfo.SetEnterEventFlag();
            }
        }

        
        
        
        public void SetLeaveFlag(LogicMap map)
        {
            var cellInfo = map.GetMapCellInfo(X, Y);
            if (cellInfo != null)
            {
                cellInfo.SetLeaveEventFlag();
            }
        }

        
        
        
        protected override void OnLeaveMap(LogicMap map)
        {
            var cellInfo = map.GetMapCellInfo(X, Y);
            if (cellInfo != null)
            {
                
                if (cellInfo.HasEnterEventFlag() || cellInfo.HasLeaveEventFlag())
                {
                    
                    int eventCount = 0;
                    var node = cellInfo.GetFirstNode();
                    while (node != null)
                    {
                        var obj = node.Value;
                        if (obj != this && obj.GetObjectType() == ObjectType.Event)
                        {
                            eventCount++;
                        }
                        node = node.Next;
                    }
                    
                    
                    if (eventCount == 0)
                    {
                        if (cellInfo.HasEnterEventFlag())
                        {
                            cellInfo.ClearEnterEventFlag();
                        }
                        if (cellInfo.HasLeaveEventFlag())
                        {
                            cellInfo.ClearLeaveEventFlag();
                        }
                    }
                }
            }
            
            base.OnLeaveMap(map);
        }

        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.Event;
        }

        
        
        
        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            msg = new byte[0];
            
            return false;
        }
    }

    
    
    
    public class VisibleEvent : EventObject
    {
        private uint _view;
        private ServerTimer _runTimer;
        private ServerTimer _closeTimer;
        private EventProcessor _eventProcessor;
        private bool _closed;
        private uint _param1;
        private uint _param2;

        private static uint _nextVisibleEventSeq = 1;

        public VisibleEvent()
        {
            _view = 0;
            _closed = false;
            _param1 = 0;
            _param2 = 0;
            _runTimer = new ServerTimer();
            _closeTimer = new ServerTimer();
        }

        
        
        
        public bool Create(LogicMap map, int x, int y, uint view, uint runTick, uint lastTime, 
                          EventProcessor processor, uint param1 = 0, uint param2 = 0)
        {
            try
            {
                
                MapId = (int)map.MapId;
                X = (ushort)x;
                Y = (ushort)y;
                _view = view;
                _param1 = param1;
                _param2 = param2;
                _eventProcessor = processor;
                _closed = false;

                
                _runTimer.SetInterval(runTick);
                _closeTimer.SetInterval(lastTime);

                
                uint seq = System.Threading.Interlocked.Increment(ref _nextVisibleEventSeq);
                ObjectId = ObjectIdUtil.MakeObjectId(MirObjectType.VisibleEvent, seq);

                
                if (!map.AddObject(this, x, y))
                {
                    LogManager.Default.Warning($"无法将事件添加到地图: {map.MapId}");
                    return false;
                }

                
                _eventProcessor?.OnCreate(this);

                LogManager.Default.Debug($"创建可见事件: 地图={map.MapId}, 位置=({x},{y}), 视野={view}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"创建可见事件失败", exception: ex);
                return false;
            }
        }

        
        
        
        public void Close()
        {
            if (_closed)
                return;

            _closed = true;
            
            
            _eventProcessor?.OnClose(this);

            
            var map = MapManager.Instance.GetMap((uint)MapId);
            if (map != null)
            {
                map.RemoveObject(this);
            }

            LogManager.Default.Debug($"关闭可见事件: 地图={MapId}, 位置=({X},{Y})");
        }

        
        
        
        public override void Clean()
        {
            _eventProcessor = null;
            base.Clean();
        }

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            try
            {
                
                byte[] payload = new byte[8];
                Buffer.BlockCopy(BitConverter.GetBytes(_param1), 0, payload, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(_param2), 0, payload, 4, 4);

                var outMsg = new MirCommon.MirMsgOrign
                {
                    dwFlag = ObjectId,
                    wCmd = 804,
                    wParam = new ushort[3] { (ushort)(_view & 0xffff), (ushort)X, (ushort)Y },
                };

                msg = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(outMsg, payload);
                return msg.Length > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"获取视图消息失败", exception: ex);
                msg = Array.Empty<byte>();
                return false;
            }
        }

        public bool GetOutViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            try
            {
                var outMsg = new MirCommon.MirMsgOrign
                {
                    dwFlag = ObjectId,
                    wCmd = 805,
                    wParam = new ushort[3] { 0, (ushort)X, (ushort)Y },
                };

                msg = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(outMsg, null);
                return msg.Length > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"获取离开视图消息失败", exception: ex);
                msg = Array.Empty<byte>();
                return false;
            }
        }

        
        
        
        public override void OnEnter(MapObject mapObject)
        {
            base.OnEnter(mapObject);
            _eventProcessor?.OnEnter(this, mapObject);
        }

        
        
        
        public override void OnLeave(MapObject mapObject)
        {
            base.OnLeave(mapObject);
            _eventProcessor?.OnLeave(this, mapObject);
        }

        
        
        
        public bool UpdateValid()
        {
            if (_closed || _disabled)
                return false;

            
            if (_closeTimer.IsTimeOut())
            {
                Close();
                return false;
            }

            
            if (_runTimer.IsTimeOut())
            {
                _eventProcessor?.OnUpdate(this);
                _runTimer.Reset();
            }

            return true;
        }

        
        
        
        public void SetParam(uint param1, uint param2)
        {
            _param1 = param1;
            _param2 = param2;
        }

        
        
        
        public uint GetParam1()
        {
            return _param1;
        }

        
        
        
        public uint GetParam2()
        {
            return _param2;
        }

        
        
        
        public uint GetView()
        {
            return _view;
        }

        
        
        
        
        protected override void OnEnterMap(LogicMap map)
        {
            base.OnEnterMap(map);
            
            int mx = X;
            int my = Y;
            
            
            if (!GetViewMsg(out byte[] viewMsgBytes, null))
                return;
            
            
            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    var cellInfo = map.GetMapCellInfo(mx + x, my + y);
                    if (cellInfo != null && cellInfo.GetObjectCount() > 0)
                    {
                        var node = cellInfo.GetFirstNode();
                        while (node != null)
                        {
                            var obj = node.Value;
                            if (obj.GetObjectType() == ObjectType.Player)
                            {
                                if (obj is HumanPlayer player)
                                {
                                    
                                    
                                    
                                    player.SendMessage(viewMsgBytes);
                                }
                            }
                            node = node.Next;
                        }
                    }
                }
            }
        }

        
        
        
        
        protected override void OnLeaveMap(LogicMap map)
        {
            int mx = X;
            int my = Y;
            
            
            if (GetOutViewMsg(out byte[] outViewMsgBytes, null))
            {
                
                
                for (int x = -12; x <= 12; x++)
                {
                    for (int y = -12; y <= 12; y++)
                    {
                        var cellInfo = map.GetMapCellInfo(mx + x, my + y);
                        if (cellInfo != null && cellInfo.GetObjectCount() > 0)
                        {
                            var node = cellInfo.GetFirstNode();
                            while (node != null)
                            {
                                var obj = node.Value;
                                if (obj.GetObjectType() == ObjectType.Player)
                                {
                                    if (obj is HumanPlayer player)
                                    {
                                        
                                        
                                        
                                        player.SendMessage(outViewMsgBytes);
                                    }
                                }
                                node = node.Next;
                            }
                        }
                    }
                }
            }
            
            base.OnLeaveMap(map);
        }

        
        
        
        public EventProcessor GetProcessor()
        {
            return _eventProcessor;
        }

        
        
        
        public void SetDelTimer()
        {
            
            _closeTimer.SetInterval((uint)(30 * 1000));
        }

        
        
        
        public bool IsDelTimerTimeOut(uint timeout)
        {
            return _closeTimer.IsTimeOut(timeout);
        }

        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.VisibleEvent;
        }
    }

    
    
    
    
    public class EventManager
    {
        private static EventManager? _instance;
        public static EventManager Instance => _instance ??= new EventManager();

        
        private readonly ObjectPool<VisibleEvent> _visibleEventPool;
        
        
        private readonly Queue<VisibleEvent> _deleteObjectQueue;
        
        
        private readonly LinkedList<EventProcessor> _processorList;
        
        
        private readonly LinkedList<MapObject> _visibleEventList;
        
        
        private LinkedListNode<MapObject>? _currentUpdateEvent;
        private LinkedListNode<EventProcessor>? _currentUpdateProcessor;

        private readonly object _lock = new();

        public EventManager()
        {
            _visibleEventPool = new ObjectPool<VisibleEvent>(() => new VisibleEvent(), 100);
            _deleteObjectQueue = new Queue<VisibleEvent>(2000);
            _processorList = new LinkedList<EventProcessor>();
            _visibleEventList = new LinkedList<MapObject>();
            _currentUpdateEvent = null;
            _currentUpdateProcessor = null;
        }

        
        
        
        
        public VisibleEvent? NewVisibleEvent(LogicMap map, int x, int y, uint view, 
                                            uint runTick, uint lastTime, EventProcessor processor,
                                            uint param1 = 0, uint param2 = 0)
        {
            lock (_lock)
            {
                
                if (map.FindEventObject(x, y, view) != null)
                {
                    LogManager.Default.Debug($"在位置({x},{y})发现相同视野({view})的事件");
                    return null;
                }

                
                var visibleEvent = _visibleEventPool.Get();
                if (visibleEvent == null)
                {
                    LogManager.Default.Warning("可见事件对象池为空");
                    return null;
                }

                
                if (!visibleEvent.Create(map, x, y, view, runTick, lastTime, processor, param1, param2))
                {
                    _visibleEventPool.Return(visibleEvent);
                    return null;
                }

                
                if (processor == null)
                {
                    _visibleEventList.AddLast(visibleEvent);
                }

                LogManager.Default.Info($"创建新可见事件: 地图={map.MapId}, 位置=({x},{y}), 视野={view}");
                return visibleEvent;
            }
        }

        
        
        
        
        public void DelVisibleEvent(VisibleEvent visibleEvent)
        {
            lock (_lock)
            {
                visibleEvent.Close();
                visibleEvent.Clean();
                _visibleEventPool.Return(visibleEvent);
                
                LogManager.Default.Debug($"删除可见事件: 地图={visibleEvent.MapId}, 位置=({visibleEvent.X},{visibleEvent.Y})");
            }
        }

        
        
        
        
        public void PreDelVisibleEvent(VisibleEvent visibleEvent)
        {
            lock (_lock)
            {
                
                if (visibleEvent.GetProcessor() == null)
                {
                    var node = _visibleEventList.Find(visibleEvent);
                    if (node != null)
                    {
                        _visibleEventList.Remove(node);
                    }
                }

                
                visibleEvent.SetDelTimer();

                
                if (_deleteObjectQueue.Count < 2000)
                {
                    _deleteObjectQueue.Enqueue(visibleEvent);
                }
                else
                {
                    
                    DelVisibleEvent(visibleEvent);
                }
            }
        }

        
        
        
        
        public void UpdateDeleteObject()
        {
            lock (_lock)
            {
                int count = _deleteObjectQueue.Count;
                if (count == 0)
                    return;

                var visibleEvent = _deleteObjectQueue.Dequeue();
                if (visibleEvent != null)
                {
                    
                    if (visibleEvent.IsDelTimerTimeOut((uint)(30 * 1000)))
                    {
                        DelVisibleEvent(visibleEvent);
                    }
                    else
                    {
                        
                        _deleteObjectQueue.Enqueue(visibleEvent);
                    }
                }
            }
        }

        
        
        
        
        public void AddEventProcessor(EventProcessor processor)
        {
            lock (_lock)
            {
                _processorList.AddLast(processor);
                LogManager.Default.Debug($"添加事件处理器: {processor.GetType().Name}");
            }
        }

        
        
        
        
        public void RemoveEventProcessor(EventProcessor processor)
        {
            lock (_lock)
            {
                var node = _processorList.Find(processor);
                if (node != null)
                {
                    _processorList.Remove(node);
                    LogManager.Default.Debug($"移除事件处理器: {processor.GetType().Name}");
                }
            }
        }

        
        
        
        
        public void UpdateEvents()
        {
            lock (_lock)
            {
                
                UpdateVisibleEvents();

                
                UpdateEventProcessors();
            }
        }

        
        
        
        public void Update()
        {
            UpdateEvents();
        }

        
        
        
        private void UpdateVisibleEvents()
        {
            var currentNode = _currentUpdateEvent;
            var nextNode = (LinkedListNode<MapObject>?)null;
            
            if (currentNode == null)
                currentNode = _visibleEventList.First;

            uint count = 0;
            while (currentNode != null && count < 100)
            {
                nextNode = currentNode.Next;
                
                var visibleEvent = currentNode.Value as VisibleEvent;
                if (visibleEvent != null)
                {
                    visibleEvent.UpdateValid();
                }
                
                currentNode = nextNode;
                count++;
            }
            
            _currentUpdateEvent = currentNode;
        }

        
        
        
        private void UpdateEventProcessors()
        {
            var currentProcessor = _currentUpdateProcessor;
            var nextProcessor = (LinkedListNode<EventProcessor>?)null;
            
            if (currentProcessor == null)
                currentProcessor = _processorList.First;

            uint count = 0;
            while (currentProcessor != null && count < 100)
            {
                nextProcessor = currentProcessor.Next;
                currentProcessor.Value.Update();
                currentProcessor = nextProcessor;
                count++;
            }
            
            _currentUpdateProcessor = currentProcessor;
        }

        
        
        
        public int GetVisibleEventCount()
        {
            lock (_lock)
            {
                return _visibleEventList.Count;
            }
        }

        
        
        
        public int GetProcessorCount()
        {
            lock (_lock)
            {
                return _processorList.Count;
            }
        }

        
        
        
        public int GetDeleteQueueCount()
        {
            lock (_lock)
            {
                return _deleteObjectQueue.Count;
            }
        }

        
        
        
        public void ClearAllEvents()
        {
            lock (_lock)
            {
                
                foreach (var mapObject in _visibleEventList)
                {
                    if (mapObject is VisibleEvent visibleEvent)
                    {
                        DelVisibleEvent(visibleEvent);
                    }
                }
                _visibleEventList.Clear();

                
                while (_deleteObjectQueue.Count > 0)
                {
                    var visibleEvent = _deleteObjectQueue.Dequeue();
                    DelVisibleEvent(visibleEvent);
                }

                
                _processorList.Clear();

                
                _currentUpdateEvent = null;
                _currentUpdateProcessor = null;

                LogManager.Default.Info("清理所有事件");
            }
        }
    }
}
