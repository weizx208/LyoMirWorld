using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public struct EventMapPosition
    {
        
        
        
        public uint MapId { get; set; }

        
        
        
        public uint X { get; set; }

        
        
        
        public uint Y { get; set; }

        
        
        
        public uint Delay { get; set; }

        
        
        
        public ScriptEventFlag Flag { get; set; }

        
        
        
        public EventMapPosition(uint mapId, uint x, uint y, ScriptEventFlag flag = ScriptEventFlag.Enter, uint delay = 0)
        {
            MapId = mapId;
            X = x;
            Y = y;
            Delay = delay;
            Flag = flag;
        }

        
        
        
        public override string ToString()
        {
            return $"[{MapId}:({X},{Y})] 标志:{Flag} 延迟:{Delay}";
        }
    }

    
    
    
    
    [Flags]
    public enum ScriptEventFlag
    {
        
        
        
        Enter = 1,

        
        
        
        Leave = 2
    }

    
    
    
    
    public class ScriptEvent : EventObject
    {
        private static readonly List<ScriptEvent> _scriptEvents = new();
        private static readonly object _lock = new();

        
        
        
        public string ScriptPage { get; private set; } = string.Empty;

        
        
        
        public ScriptEventFlag Flag { get; private set; }

        
        
        
        private ScriptEvent()
        {
        }

        
        
        
        
        public static ScriptEvent? Create(uint mapId, uint x, uint y, ScriptEventFlag flag, string scriptPage)
        {
            if (string.IsNullOrEmpty(scriptPage))
                return null;

            try
            {
                var scriptEvent = new ScriptEvent
                {
                    MapId = (int)mapId,
                    X = (ushort)x,
                    Y = (ushort)y,
                    Flag = flag,
                    ScriptPage = scriptPage
                };

                lock (_lock)
                {
                    _scriptEvents.Add(scriptEvent);
                }

                LogManager.Default.Debug($"创建脚本事件: 地图={mapId}, 位置=({x},{y}), 标志={flag}, 脚本={scriptPage}");
                return scriptEvent;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"创建脚本事件失败: 地图={mapId}, 位置=({x},{y})", exception: ex);
                return null;
            }
        }

        
        
        
        
        public void Release()
        {
            try
            {
                lock (_lock)
                {
                    _scriptEvents.Remove(this);
                }

                
                var map = MapManager.Instance.GetMap((uint)MapId);
                if (map != null)
                {
                    map.RemoveObject(this);
                }

                LogManager.Default.Debug($"释放脚本事件: 地图={MapId}, 位置=({X},{Y}), 脚本={ScriptPage}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"释放脚本事件失败: 地图={MapId}, 位置=({X},{Y})", exception: ex);
            }
        }

        
        
        
        
        public override void OnEnter(MapObject mapObject)
        {
            base.OnEnter(mapObject);

            if ((Flag & ScriptEventFlag.Enter) != 0)
            {
                LogManager.Default.Debug($"脚本事件进入触发: 地图={MapId}, 位置=({X},{Y}), 脚本={ScriptPage}, 对象={mapObject.GetType().Name}");
                
                
                if (mapObject.GetObjectType() == ObjectType.Player)
                {
                    var player = mapObject as HumanPlayer;
                    if (player != null)
                    {
                        
                        var scriptTarget = GetScriptTargetFromPlayer(player);
                        if (scriptTarget != null)
                        {
                            
                            SystemScript.Instance.Execute(scriptTarget, ScriptPage);
                        }
                    }
                }
            }
        }

        
        
        
        
        public override void OnLeave(MapObject mapObject)
        {
            base.OnLeave(mapObject);

            if ((Flag & ScriptEventFlag.Leave) != 0)
            {
                LogManager.Default.Debug($"脚本事件离开触发: 地图={MapId}, 位置=({X},{Y}), 脚本={ScriptPage}, 对象={mapObject.GetType().Name}");
                
                
                if (mapObject.GetObjectType() == ObjectType.Player)
                {
                    var player = mapObject as HumanPlayer;
                    if (player != null)
                    {
                        
                        var scriptTarget = GetScriptTargetFromPlayer(player);
                        if (scriptTarget != null)
                        {
                            
                            SystemScript.Instance.Execute(scriptTarget, ScriptPage);
                        }
                    }
                }
            }
        }

        
        
        
        
        private ScriptTarget? GetScriptTargetFromPlayer(HumanPlayer player)
        {
            
            if (player is ScriptTarget scriptTarget)
            {
                return scriptTarget;
            }
            
            
            
            return new PlayerScriptTargetWrapper(player);
        }

        
        
        
        
        protected override void OnEnterMap(LogicMap map)
        {
            base.OnEnterMap(map);
            LogManager.Default.Debug($"脚本事件进入地图: 地图={map.MapId}, 位置=({X},{Y}), 脚本={ScriptPage}");
        }

        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.ScriptEvent;
        }

        
        
        
        public static List<ScriptEvent> GetAllScriptEvents()
        {
            lock (_lock)
            {
                return new List<ScriptEvent>(_scriptEvents);
            }
        }

        
        
        
        public static List<ScriptEvent> GetScriptEventsByMap(uint mapId)
        {
            var result = new List<ScriptEvent>();
            lock (_lock)
            {
                foreach (var scriptEvent in _scriptEvents)
                {
                    if (scriptEvent.MapId == mapId)
                    {
                        result.Add(scriptEvent);
                    }
                }
            }
            return result;
        }

        
        
        
        public static void ClearAll()
        {
            lock (_lock)
            {
                foreach (var scriptEvent in _scriptEvents)
                {
                    scriptEvent.Release();
                }
                _scriptEvents.Clear();
                LogManager.Default.Info("清理所有脚本事件");
            }
        }
    }

    
    
    
    
    
    public class MapScriptManager
    {
        private static MapScriptManager? _instance;

        
        
        
        public static MapScriptManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MapScriptManager();
                }
                return _instance;
            }
        }

        
        
        
        private MapScriptManager()
        {
        }

        
        
        
        
        private bool ParseEventMapPosition(string eventStr, out EventMapPosition position)
        {
            position = new EventMapPosition();

            try
            {
                
                string str = eventStr.Trim();
                if (str.StartsWith("["))
                {
                    str = str.Substring(1);
                }

                
                var parts = str.Split(new[] { ',', ':', '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    return false;
                }

                
                if (!uint.TryParse(parts[0].Trim(), out uint mapId) ||
                    !uint.TryParse(parts[1].Trim(), out uint x) ||
                    !uint.TryParse(parts[2].Trim(), out uint y))
                {
                    return false;
                }

                position.MapId = mapId;
                position.X = x;
                position.Y = y;
                position.Flag = ScriptEventFlag.Enter; 

                
                if (parts.Length > 3)
                {
                    var flagParts = parts[3].Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var flagPart in flagParts)
                    {
                        var flag = flagPart.Trim().ToLower();
                        if (flag == "enter")
                        {
                            position.Flag |= ScriptEventFlag.Enter;
                        }
                        else if (flag == "leave")
                        {
                            position.Flag |= ScriptEventFlag.Leave;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析事件地图位置失败: {eventStr}", exception: ex);
                return false;
            }
        }

        
        
        
        
        public bool Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    LogManager.Default.Warning($"地图脚本配置文件不存在: {filePath}");
                    return false;
                }

                LogManager.Default.Info($"加载地图脚本配置: {filePath}");

                var lines = SmartReader.ReadAllLines(filePath);
                int loadedCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    
                    var parts = line.Split(new[] { ']' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    string eventStr = parts[0].Trim();
                    string scriptPage = parts[1].Trim();

                    if (!eventStr.StartsWith("[") || string.IsNullOrEmpty(scriptPage))
                        continue;

                    
                    if (!ParseEventMapPosition(eventStr, out var position))
                        continue;

                    
                    AddMapScript(position, scriptPage);
                    loadedCount++;
                }

                LogManager.Default.Info($"成功加载 {loadedCount} 个地图脚本配置");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载地图脚本配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        
        
        
        
        public void AddMapScript(EventMapPosition position, string scriptPage)
        {
            try
            {
                
                var scriptEvent = ScriptEvent.Create(position.MapId, position.X, position.Y, position.Flag, scriptPage);
                if (scriptEvent == null)
                {
                    LogManager.Default.Warning($"创建地图脚本失败: 地图={position.MapId}, 位置=({position.X},{position.Y}), 脚本={scriptPage}");
                    return;
                }

                
                var map = MapManager.Instance.GetMap(position.MapId);
                if (map == null)
                {
                    LogManager.Default.Warning($"地图不存在: {position.MapId}");
                    scriptEvent.Release();
                    return;
                }

                if (!map.AddObject(scriptEvent, (int)position.X, (int)position.Y))
                {
                    LogManager.Default.Warning($"无法将脚本事件添加到地图: {position.MapId}");
                    scriptEvent.Release();
                    return;
                }

                LogManager.Default.Debug($"添加地图脚本: 地图={position.MapId}, 位置=({position.X},{position.Y}), 标志={position.Flag}, 脚本={scriptPage}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"添加地图脚本失败: 地图={position.MapId}, 位置=({position.X},{position.Y})", exception: ex);
            }
        }

        
        
        
        public bool Reload(string filePath)
        {
            try
            {
                
                ScriptEvent.ClearAll();
                return Load(filePath);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"重新加载地图脚本配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        
        
        
        public List<ScriptEvent> GetAllScriptEvents()
        {
            return ScriptEvent.GetAllScriptEvents();
        }

        
        
        
        public List<ScriptEvent> GetScriptEventsByMap(uint mapId)
        {
            return ScriptEvent.GetScriptEventsByMap(mapId);
        }

        
        
        
        public int GetScriptEventCount()
        {
            return ScriptEvent.GetAllScriptEvents().Count;
        }

        
        
        
        public void ClearAll()
        {
            ScriptEvent.ClearAll();
            LogManager.Default.Info("清理所有地图脚本");
        }

        
        
        
        public void Update()
        {
            
            
        }
    }

    
    
    
    
    
    public class PlayerScriptTargetWrapper : ScriptTarget
    {
        private readonly HumanPlayer _player;

        public PlayerScriptTargetWrapper(HumanPlayer player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        
        
        
        
        public string GetTargetName()
        {
            return _player.Name;
        }
        
        
        
        
        
        public uint GetTargetId()
        {
            return _player.ObjectId;
        }
        
        
        
        
        
        public void ExecuteScriptAction(string action, params string[] parameters)
        {
            
            
            LogManager.Default.Debug($"玩家 {_player.Name} 执行脚本动作: {action}, 参数: {string.Join(", ", parameters)}");
            
            
            switch (action.ToLower())
            {
                case "say":
                    if (parameters.Length > 0)
                    {
                        _player.Say(parameters[0]);
                    }
                    break;
                case "additem":
                    if (parameters.Length > 0 && int.TryParse(parameters[0], out int itemId))
                    {
                        
                        LogManager.Default.Info($"脚本动作: 给玩家 {_player.Name} 添加物品 {itemId}");
                    }
                    break;
                case "addgold":
                    if (parameters.Length > 0 && uint.TryParse(parameters[0], out uint gold))
                    {
                        _player.AddGold(gold);
                    }
                    break;
                case "teleport":
                    if (parameters.Length > 2 && 
                        uint.TryParse(parameters[0], out uint mapId) &&
                        uint.TryParse(parameters[1], out uint x) &&
                        uint.TryParse(parameters[2], out uint y))
                    {
                        
                        LogManager.Default.Info($"脚本动作: 传送玩家 {_player.Name} 到地图 {mapId} ({x},{y})");
                    }
                    break;
                default:
                    LogManager.Default.Warning($"未知的脚本动作: {action}");
                    break;
            }
        }
    }
}
