using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    public class LogicMap : MapObject
    {
        
        
        
        public uint MapId { get; set; }
        
        
        
        
        public string MapName { get; set; } = string.Empty;

        
        
        
        public string MapFile { get; set; } = string.Empty;
        
        
        
        
        public int Width { get; set; }
        
        
        
        
        public int Height { get; set; }
        
        
        
        
        public int MinLevel { get; set; }
        
        
        
        
        public int MaxLevel { get; set; }
        
        
        
        
        public string NeedItem { get; set; } = string.Empty;
        
        
        
        
        public string NeedQuest { get; set; } = string.Empty;
        
        
        
        
        public string ScriptFile { get; set; } = string.Empty;
        
        
        
        
        private readonly Dictionary<uint, MapObject> _objects = new();
        
        
        
        
        private readonly Dictionary<uint, HumanPlayer> _players = new();
        
        
        
        
        private readonly Dictionary<uint, AliveObject> _monsters = new();
        
        
        
        
        private readonly Dictionary<uint, Npc> _npcs = new();
        
        
        
        
        private readonly Dictionary<uint, MapItem> _items = new();
        
        
        
        
        
        private MapCellInfo[,]? _mapCellInfo;
        
        
        
        
        public float ExpFactor { get; set; } = 1.0f;
        
        
        
        
        public float DropFactor { get; set; } = 1.0f;
        
        
        
        
        public bool IsSafeZone { get; set; }
        
        
        
        
        public bool AllowPK { get; set; }
        
        
        
        
        public bool AllowPets { get; set; }
        
        
        
        
        public bool AllowMounts { get; set; }
        
        
        
        
        public bool AllowTeleport { get; set; }
        
        
        
        
        public bool AllowRecall { get; set; }
        
        
        
        
        public MapFlag MapFlags { get; set; }
        
        
        
        
        
        private readonly Dictionary<MapFlag, List<uint>> _flagParams = new();
        
        
        
        
        public MapWeather Weather { get; set; }
        
        
        
        
        public MapTime Time { get; set; }
        
        
        
        
        public string Music { get; set; } = string.Empty;
        
        
        
        
        public string Background { get; set; } = string.Empty;
        public object TownX { get; internal set; }
        public object TownY { get; internal set; }
        public object GuildX { get; internal set; }
        public object GuildY { get; internal set; }

        
        
        
        private readonly List<MineItem> _mineItems = new();
        
        
        
        
        private uint _mineRateMax = 0;
        
        
        
        
        private PhysicsMap? _physicsMap;
        
        
        
        
        private int _miniMapId;
        
        
        
        
        private int _linkCount;
        
        
        
        
        public LogicMap(uint mapId, string mapName, int width, int height)
        {
            MapId = mapId;
            MapName = mapName;
            Width = width;
            Height = height;
            
            
            if (width > 0 && height > 0)
            {
                _mapCellInfo = new MapCellInfo[width, height];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        _mapCellInfo[x, y] = new MapCellInfo();
                    }
                }
            }
        }
        
        
        
        
        public float GetExpFactor()
        {
            return ExpFactor;
        }
        
        
        
        
        public float GetDropFactor()
        {
            return DropFactor;
        }
        
        
        
        
        public MapObject? GetObject(uint objectId)
        {
            return _objects.TryGetValue(objectId, out var obj) ? obj : null;
        }
        
        
        
        
        public HumanPlayer? GetPlayer(uint playerId)
        {
            return _players.TryGetValue(playerId, out var player) ? player : null;
        }
        
        
        
        
        public AliveObject? GetMonster(uint monsterId)
        {
            return _monsters.TryGetValue(monsterId, out var monster) ? monster : null;
        }
        
        
        
        
        public Npc? GetNPC(uint npcId)
        {
            return _npcs.TryGetValue(npcId, out var npc) ? npc : null;
        }
        
        
        
        
        public MapItem? GetItem(uint itemId)
        {
            return _items.TryGetValue(itemId, out var item) ? item : null;
        }

        
        
        
        public bool AddObject(MapObject obj, int x, int y)
        {
            if (obj == null || x < 0 || x >= Width || y < 0 || y >= Height)
                return false;

            
            obj.EnterMap(this, (ushort)x, (ushort)y);

            _objects[obj.ObjectId] = obj;
            
            
            if (_mapCellInfo != null)
            {
                _mapCellInfo[x, y].AddObject(obj);
            }
            
            
            switch (obj.GetObjectType())
            {
                case ObjectType.Player:
                    if (obj is HumanPlayer player)
                        _players[obj.ObjectId] = player;
                    break;
                case ObjectType.Monster:
                    if (obj is AliveObject monster)
                        _monsters[obj.ObjectId] = monster;
                    break;
                case ObjectType.NPC:
                    if (obj is Npc npc)
                        _npcs[obj.ObjectId] = npc;
                    break;
                case ObjectType.Item:
                    if (obj is MapItem item)
                        _items[obj.ObjectId] = item;
                    break;
            }

            
            foreach (var player in GetPlayersInRange(x, y, 18))
            {
                if (player.ObjectId == obj.ObjectId)
                    continue;
                player.AddVisibleObject(obj);
            }
            
            return true;
        }
        
        
        
        
        public bool RemoveObject(MapObject obj)
        {
            if (obj == null || !_objects.ContainsKey(obj.ObjectId))
                return false;
                
            
            int oldX = obj.X;
            int oldY = obj.Y;

            _objects.Remove(obj.ObjectId);
            
            
            if (_mapCellInfo != null && oldX < Width && oldY < Height)
            {
                _mapCellInfo[oldX, oldY].RemoveObject(obj);
            }
            
            
            switch (obj.GetObjectType())
            {
                case ObjectType.Player:
                    _players.Remove(obj.ObjectId);
                    break;
                case ObjectType.Monster:
                    _monsters.Remove(obj.ObjectId);
                    break;
                case ObjectType.NPC:
                    _npcs.Remove(obj.ObjectId);
                    break;
                case ObjectType.Item:
                    _items.Remove(obj.ObjectId);
                    break;
            }

            
            foreach (var player in GetPlayersInRange(oldX, oldY, 18))
            {
                if (player.ObjectId == obj.ObjectId)
                    continue;
                player.RemoveVisibleObject(obj);
            }
            
            obj.CurrentMap = null;
            return true;
        }
        
        
        
        
        public bool RemoveObject(uint objectId)
        {
            if (!_objects.TryGetValue(objectId, out var obj))
                return false;
                
            return RemoveObject(obj);
        }
        
        
        
        
        public bool CanMoveTo(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;
                
            
            
            return true;
        }
        
        
        
        
        public bool VerifyPos(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }
        
        
        
        
        public bool IsBlocked(int x, int y)
        {
            if (!VerifyPos(x, y))
                return true;
                
            if (IsLocked(x, y))
                return true;
                
            
            return IsPhysicsBlocked(x, y);
        }
        
        
        
        
        public bool IsPhysicsBlocked(int x, int y)
        {
            
            if (_physicsMap == null)
                return false;
                
            
            return _physicsMap.IsBlocked(x, y);
        }
        
        
        
        
        public bool IsLocked(int x, int y)
        {
            
            
            return false;
        }
        
        
        
        
        
        public int GetValidPoint(int x, int y, Point[] ptArray, int arraySize)
        {
            if (ptArray == null || arraySize <= 0)
                return 0;
                
            
            Point[] searchPoints = new Point[]
            {
                new Point(-1, -1), new Point(0, -1), new Point(1, -1), new Point(1, 0),
                new Point(1, 1), new Point(0, 1), new Point(-1, 1), new Point(-1, 0),
                new Point(-2, -2), new Point(-1, -2), new Point(0, -2), new Point(1, -2),
                new Point(2, -2), new Point(2, -1), new Point(2, 0), new Point(2, 1),
                new Point(2, 2), new Point(1, 2), new Point(0, 2), new Point(-1, 2),
                new Point(-2, 2), new Point(-2, 1), new Point(-2, 0), new Point(-2, -1),
                new Point(-3, -3), new Point(-2, -3), new Point(-1, -3), new Point(0, -3),
                new Point(1, -3), new Point(2, -3), new Point(3, -3), new Point(3, -2),
                new Point(3, -1), new Point(3, 0), new Point(3, 1), new Point(3, 2),
                new Point(3, 3), new Point(2, 3), new Point(1, 3), new Point(0, 3),
                new Point(-1, 3), new Point(-2, 3), new Point(-3, 3), new Point(-3, 2),
                new Point(-3, 1), new Point(-3, 0), new Point(-3, -1), new Point(-3, -2),
                new Point(-4, -4), new Point(-3, -4), new Point(-2, -4), new Point(-1, -4),
                new Point(0, -4), new Point(1, -4), new Point(2, -4), new Point(3, -4),
                new Point(4, -4), new Point(4, -3), new Point(4, -2), new Point(4, -1),
                new Point(4, 0), new Point(4, 1), new Point(4, 2), new Point(4, 3),
                new Point(4, 4), new Point(3, 4), new Point(2, 4), new Point(1, 4),
                new Point(0, 4), new Point(-1, 4), new Point(-2, 4), new Point(-3, 4),
                new Point(-4, 4), new Point(-4, 3), new Point(-4, 2), new Point(-4, 1),
                new Point(-4, 0), new Point(-4, -1), new Point(-4, -2), new Point(-4, -3)
            };
            
            int count = 0;
            for (int i = 0; i < searchPoints.Length && count < arraySize; i++)
            {
                int newX = x + searchPoints[i].X;
                int newY = y + searchPoints[i].Y;
                
                if (!IsBlocked(newX, newY))
                {
                    ptArray[count] = new Point(newX, newY);
                    count++;
                }
            }
            
            return count;
        }
        
        
        
        
        
        public int GetDropItemPoint(int x, int y, Point[] ptArray, int arraySize)
        {
            if (ptArray == null || arraySize <= 0)
                return 0;
                
            
            Point[] searchPoints = new Point[]
            {
                new Point(-1, -1), new Point(0, -1), new Point(1, -1), new Point(1, 0),
                new Point(1, 1), new Point(0, 1), new Point(-1, 1), new Point(-1, 0),
                new Point(-2, -2), new Point(-1, -2), new Point(0, -2), new Point(1, -2),
                new Point(2, -2), new Point(2, -1), new Point(2, 0), new Point(2, 1),
                new Point(2, 2), new Point(1, 2), new Point(0, 2), new Point(-1, 2),
                new Point(-2, 2), new Point(-2, 1), new Point(-2, 0), new Point(-2, -1),
                new Point(-3, -3), new Point(-2, -3), new Point(-1, -3), new Point(0, -3),
                new Point(1, -3), new Point(2, -3), new Point(3, -3), new Point(3, -2),
                new Point(3, -1), new Point(3, 0), new Point(3, 1), new Point(3, 2),
                new Point(3, 3), new Point(2, 3), new Point(1, 3), new Point(0, 3),
                new Point(-1, 3), new Point(-2, 3), new Point(-3, 3), new Point(-3, 2),
                new Point(-3, 1), new Point(-3, 0), new Point(-3, -1), new Point(-3, -2),
                new Point(-4, -4), new Point(-3, -4), new Point(-2, -4), new Point(-1, -4),
                new Point(0, -4), new Point(1, -4), new Point(2, -4), new Point(3, -4),
                new Point(4, -4), new Point(4, -3), new Point(4, -2), new Point(4, -1),
                new Point(4, 0), new Point(4, 1), new Point(4, 2), new Point(4, 3),
                new Point(4, 4), new Point(3, 4), new Point(2, 4), new Point(1, 4),
                new Point(0, 4), new Point(-1, 4), new Point(-2, 4), new Point(-3, 4),
                new Point(-4, 4), new Point(-4, 3), new Point(-4, 2), new Point(-4, 1),
                new Point(-4, 0), new Point(-4, -1), new Point(-4, -2), new Point(-4, -3)
            };
            
            int[] drops = new int[searchPoints.Length];
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = -1; 
            }
            
            int dropPointCount = 0;
            
            for (int i = 0; i < arraySize; i++)
            {
                Point? bestPoint = null;
                int bestCount = int.MaxValue;
                int bestIndex = -1;
                
                for (int j = 0; j < searchPoints.Length; j++)
                {
                    if (drops[j] == -1)
                    {
                        int newX = x + searchPoints[j].X;
                        int newY = y + searchPoints[j].Y;
                        
                        if (IsBlocked(newX, newY))
                        {
                            drops[j] = -2; 
                            continue;
                        }
                        
                        
                        drops[j] = GetDupCount(newX, newY, ObjectType.DownItem);
                    }
                    
                    if (drops[j] == -2) 
                    {
                        continue;
                    }
                    
                    if (drops[j] < 10) 
                    {
                        if (bestPoint == null || drops[j] < bestCount)
                        {
                            bestCount = drops[j];
                            bestPoint = searchPoints[j];
                            bestIndex = j;
                            
                            if (bestCount == 0)
                                break;
                        }
                    }
                }
                
                if (bestPoint == null)
                    break;
                    
                drops[bestIndex]++;
                ptArray[dropPointCount] = new Point(x + bestPoint.Value.X, y + bestPoint.Value.Y);
                dropPointCount++;
                
                if (dropPointCount >= arraySize)
                    return dropPointCount;
            }
            
            return dropPointCount;
        }
        
        
        
        
        public int GetDupCount(int x, int y)
        {
            int dupCount = 0;
            
            foreach (var obj in _objects.Values)
            {
                if (obj.X == x && obj.Y == y)
                {
                    var objType = obj.GetObjectType();
                    if (objType == ObjectType.Monster || objType == ObjectType.NPC || objType == ObjectType.Player)
                    {
                        if (obj is AliveObject aliveObj && !aliveObj.IsDead)
                        {
                            dupCount++;
                        }
                    }
                }
            }
            
            return dupCount;
        }
        
        
        
        
        public int GetDupCount(int x, int y, ObjectType type)
        {
            
            if (IsPhysicsBlocked(x, y))
                return -1;
                
            int dupCount = 0;
            
            foreach (var obj in _objects.Values)
            {
                if (obj.X == x && obj.Y == y && obj.GetObjectType() == type)
                {
                    dupCount++;
                }
            }
            
            return dupCount;
        }
        
        
        
        
        public bool CanWalk(int x, int y)
        {
            return CanMoveTo(x, y);
        }
        
        
        
        
        public bool MoveObject(MapObject obj, int newX, int newY)
        {
            if (obj == null || !CanMoveTo(newX, newY))
                return false;

            
            int oldX = obj.X;
            int oldY = obj.Y;

            if (oldX == newX && oldY == newY)
                return true;

            MapCellInfo? oldCell = null;
            MapCellInfo? newCell = null;
            if (_mapCellInfo != null)
            {
                if (oldX >= 0 && oldX < Width && oldY >= 0 && oldY < Height)
                    oldCell = _mapCellInfo[oldX, oldY];
                if (newX >= 0 && newX < Width && newY >= 0 && newY < Height)
                    newCell = _mapCellInfo[newX, newY];
            }

            
            var leaveEvents = oldCell?.GetEventObjectsSnapshot();

            if (_mapCellInfo != null && oldX >= 0 && oldX < Width && oldY >= 0 && oldY < Height)
            {
                _mapCellInfo[oldX, oldY].RemoveObject(obj);
            }

            obj.SetPosition((ushort)newX, (ushort)newY);

            if (_mapCellInfo != null)
            {
                _mapCellInfo[newX, newY].AddObject(obj);
            }

            
            if (leaveEvents != null)
            {
                foreach (var ev in leaveEvents)
                {
                    if (ReferenceEquals(ev, obj))
                        continue;
                    if (ev.IsDisabled())
                        continue;
                    ev.OnLeave(obj);
                }
            }

            var enterEvents = newCell?.GetEventObjectsSnapshot();
            if (enterEvents != null)
            {
                foreach (var ev in enterEvents)
                {
                    if (ReferenceEquals(ev, obj))
                        continue;
                    if (ev.IsDisabled())
                        continue;
                    ev.OnEnter(obj);
                }
            }

            return true;
        }
        
        
        
        
        public void BroadcastMessageInRange(int centerX, int centerY, int range, byte[] message)
        {
            SendToNearbyPlayers(centerX, centerY, range, message);
        }
        
        
        
        
        public void BroadcastMessage(byte[] message)
        {
            SendToAllPlayers(message);
        }
        
        
        
        
        public List<MapObject> GetObjectsInRange(int centerX, int centerY, int range)
        {
            var result = new List<MapObject>();
            
            foreach (var obj in _objects.Values)
            {
                int dx = Math.Abs(obj.X - centerX);
                int dy = Math.Abs(obj.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(obj);
                }
            }
            
            return result;
        }
        
        
        
        
        public List<HumanPlayer> GetPlayersInRange(int centerX, int centerY, int range)
        {
            var result = new List<HumanPlayer>();
            
            foreach (var player in _players.Values)
            {
                int dx = Math.Abs(player.X - centerX);
                int dy = Math.Abs(player.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(player);
                }
            }
            
            return result;
        }
        
        
        
        
        public List<AliveObject> GetMonstersInRange(int centerX, int centerY, int range)
        {
            var result = new List<AliveObject>();
            
            foreach (var monster in _monsters.Values)
            {
                int dx = Math.Abs(monster.X - centerX);
                int dy = Math.Abs(monster.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(monster);
                }
            }
            
            return result;
        }
        
        
        
        
        public List<Npc> GetNPCsInRange(int centerX, int centerY, int range)
        {
            var result = new List<Npc>();
            
            foreach (var npc in _npcs.Values)
            {
                int dx = Math.Abs(npc.X - centerX);
                int dy = Math.Abs(npc.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(npc);
                }
            }
            
            return result;
        }
        
        
        
        
        public List<MapItem> GetItemsInRange(int centerX, int centerY, int range)
        {
            var result = new List<MapItem>();
            
            foreach (var item in _items.Values)
            {
                int dx = Math.Abs(item.X - centerX);
                int dy = Math.Abs(item.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(item);
                }
            }
            
            return result;
        }
        
        
        
        
        public MapObject? GetObjectAt(int x, int y)
        {
            foreach (var obj in _objects.Values)
            {
                if (obj.X == x && obj.Y == y)
                {
                    return obj;
                }
            }
            return null;
        }

        
        
        
        
        public MapCellInfo? GetMapCellInfo(int x, int y)
        {
            if (_mapCellInfo == null || x < 0 || x >= Width || y < 0 || y >= Height)
                return null;
                
            return _mapCellInfo[x, y];
        }
        
        
        
        
        public bool IsFightMap()
        {
            
            return !IsSafeZone && AllowPK;
        }

        
        
        
        
        public MapObject? FindEventObject(int x, int y, uint view)
        {
            foreach (var obj in _objects.Values)
            {
                
                if (obj.GetObjectType() == ObjectType.Event || obj.GetObjectType() == ObjectType.VisibleEvent)
                {
                    
                    if (obj.X == x && obj.Y == y)
                    {
                        
                        if (obj is VisibleEvent visibleEvent)
                        {
                            if (visibleEvent.GetView() == view)
                            {
                                return obj;
                            }
                        }
                        else
                        {
                            
                            return obj;
                        }
                    }
                }
            }
            
            return null;
        }
        
        
        
        
        public void SendToNearbyPlayers(int centerX, int centerY, int range, byte[] message, uint excludeObjectId = 0)
        {
            var players = GetPlayersInRange(centerX, centerY, range);
            foreach (var player in players)
            {
                if (player.ObjectId == excludeObjectId)
                    continue;
                player.SendMessage(message);
            }
        }
        
        
        
        
        public void SendToNearbyPlayers(int centerX, int centerY, int range, byte[] message)
        {
            SendToNearbyPlayers(centerX, centerY, range, message, 0);
        }
        
        
        
        
        public void SendToNearbyPlayers(int centerX, int centerY, byte[] message)
        {
            
            SendToNearbyPlayers(centerX, centerY, 10, message);
        }
        
        
        
        
        public void SendToAllPlayers(byte[] message)
        {
            foreach (var player in _players.Values)
            {
                player.SendMessage(message);
            }
        }
        
        
        
        
        public int GetPlayerCount()
        {
            return _players.Count;
        }
        
        
        
        
        public int GetMonsterCount()
        {
            return _monsters.Count;
        }
        
        
        
        
        public int GetNPCCount()
        {
            return _npcs.Count;
        }
        
        
        
        
        public int GetItemCount()
        {
            return _items.Count;
        }
        
        
        
        
        public int GetTotalObjectCount()
        {
            return _objects.Count;
        }
        
        
        
        
        public void Update()
        {
            
            foreach (var monster in _monsters.Values.ToList())
            {
                monster.Update();
            }
            
            
            foreach (var npc in _npcs.Values.ToList())
            {
                npc.Update();
            }
            
            
            var now = DateTime.Now;
            var expiredItems = _items.Values.Where(item => item.ExpireTime.HasValue && item.ExpireTime.Value < now).ToList();
            foreach (var item in expiredItems)
            {
                RemoveObject(item);
            }
        }
        
        
        
        
        public void AddMineItem(string name, ushort duraMin, ushort duraMax, ushort rate)
        {
            var mineItem = new MineItem
            {
                Name = name,
                DuraMin = duraMin,
                DuraMax = duraMax,
                Rate = rate
            };
            
            _mineItems.Add(mineItem);
            _mineRateMax += rate;
        }
        
        
        
        
        public bool GotMineItem(HumanPlayer player)
        {
            if (_mineItems.Count == 0 || _mineRateMax == 0)
                return false;
                
            
            uint randomValue = (uint)new Random().Next((int)_mineRateMax);
            uint currentRate = 0;
            
            foreach (var mineItem in _mineItems)
            {
                currentRate += mineItem.Rate;
                if (randomValue < currentRate)
                {
                    
                    
                    LogManager.Default.Info($"玩家 {player.Name} 在地图 {MapName} 挖到矿石: {mineItem.Name}");
                    return true;
                }
            }
            
            return false;
        }
        
        
        
        
        public bool IsFlagSeted(MapFlag flag)
        {
            return (MapFlags & flag) != 0;
        }
        
        
        
        
        
        public void SetFlag(MapFlag flag)
        {
            MapFlags |= flag;
        }
        
        
        
        
        public void SetFlag(string flagStr)
        {
            
            ParseAndSetFlag(flagStr);
        }
        
        
        
        
        public void SetFlag(MapFlag flag, params uint[] parameters)
        {
            MapFlags |= flag;
            
            if (parameters != null && parameters.Length > 0)
            {
                _flagParams[flag] = new List<uint>(parameters);
            }
        }
        
        
        
        
        public void ClearFlag(MapFlag flag)
        {
            MapFlags &= ~flag;
            _flagParams.Remove(flag);
        }
        
        
        
        
        public List<uint>? GetFlagParams(MapFlag flag)
        {
            return _flagParams.TryGetValue(flag, out var parameters) ? parameters : null;
        }
        
        
        
        
        public uint GetFlagParam(MapFlag flag, int index = 0)
        {
            if (_flagParams.TryGetValue(flag, out var parameters) && index >= 0 && index < parameters.Count)
            {
                return parameters[index];
            }
            return 0;
        }
        
        
        
        
        public bool IsFlagSeted(MapFlag flag, uint paramValue = 0)
        {
            if (!IsFlagSeted(flag))
                return false;
                
            if (paramValue == 0)
                return true;
                
            var parameters = GetFlagParams(flag);
            if (parameters == null || parameters.Count == 0)
                return false;
                
            return parameters.Contains(paramValue);
        }
        
        
        
        
        
        private void ParseAndSetFlag(string flagStr)
        {
            if (string.IsNullOrEmpty(flagStr))
                return;
                
            string upperFlagStr = flagStr.ToUpper();
            
            
            int paramStart = upperFlagStr.IndexOf('(');
            int paramEnd = upperFlagStr.IndexOf(')');
            
            if (paramStart > 0 && paramEnd > paramStart)
            {
                
                string flagName = upperFlagStr.Substring(0, paramStart).Trim();
                string paramStr = upperFlagStr.Substring(paramStart + 1, paramEnd - paramStart - 1);
                
                
                List<uint> parameters = new List<uint>();
                string[] paramParts = paramStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (string param in paramParts)
                {
                    if (uint.TryParse(param.Trim(), out uint paramValue))
                    {
                        parameters.Add(paramValue);
                    }
                }
                
                
                MapFlag flag = GetMapFlagFromName(flagName);
                if (flag != MapFlag.MF_NONE)
                {
                    SetFlag(flag, parameters.ToArray());
                    
                }
            }
            else
            {
                
                MapFlag flag = GetMapFlagFromName(upperFlagStr);
                if (flag != MapFlag.MF_NONE)
                {
                    SetFlag(flag);
                    
                }
            }
        }
        
        
        
        
        private MapFlag GetMapFlagFromName(string flagName)
        {
            
            
            switch (flagName)
            {
                case "SABUKPALACE":
                    return MapFlag.MF_NONE;
                case "FIGHTMAP":
                    return MapFlag.MF_FIGHT;
                case "NORANDOMMOVE":
                    return MapFlag.MF_NORUN;
                case "NORECONNECT":
                    return MapFlag.MF_NONE; 
                case "RIDEHORSE":
                    return MapFlag.MF_NOMOUNT;
                case "LEVELABOVE":
                case "LEVELBELOW":
                    return MapFlag.MF_NONE; 
                case "LIMITJOB":
                    return MapFlag.MF_NONE; 
                case "PKPOINTABOVE":
                case "PKPOINTBELOW":
                    return MapFlag.MF_NONE; 
                case "NOESCAPE":
                    return MapFlag.MF_NOTELEPORT;
                case "NOHOME":
                    return MapFlag.MF_NORECALL;
                case "MINE":
                    return MapFlag.MF_MINE;
                case "WEATHER":
                case "DAY":
                case "NIGHT":
                    return MapFlag.MF_NONE; 
                case "NOGROUPMOVE":
                    return MapFlag.MF_NONE; 
                case "SANDCITYHOME":
                    return MapFlag.MF_NONE; 
                case "NODMOVE":
                    return MapFlag.MF_NOWALK;
                case "NOFLASHMOVE":
                    return MapFlag.MF_NOTELEPORT;
                case "USERDEFINE1":
                case "USERDEFINE2":
                case "USERDEFINE3":
                case "USERDEFINE4":
                    return MapFlag.MF_NONE; 
                case "SAFE":
                    return MapFlag.MF_SAFE;
                case "NOPK":
                    return MapFlag.MF_NOPK;
                case "NOMONSTER":
                    return MapFlag.MF_NOMONSTER;
                case "NOPET":
                    return MapFlag.MF_NOPET;
                case "NODROP":
                    return MapFlag.MF_NODROP;
                case "NOGUILDWAR":
                    return MapFlag.MF_NOGUILDWAR;
                case "NODUEL":
                    return MapFlag.MF_NODUEL;
                case "NOSKILL":
                    return MapFlag.MF_NOSKILL;
                case "NOITEM":
                    return MapFlag.MF_NOITEM;
                case "NOSPELL":
                    return MapFlag.MF_NOSPELL;
                case "NOSIT":
                    return MapFlag.MF_NOSIT;
                case "NOSTAND":
                    return MapFlag.MF_NOSTAND;
                case "NODIE":
                    return MapFlag.MF_NODIE;
                case "NORESPAWN":
                    return MapFlag.MF_NORESPAWN;
                case "NOLOGOUT":
                    return MapFlag.MF_NOLOGOUT;
                case "NOSAVE":
                    return MapFlag.MF_NOSAVE;
                case "NOLOAD":
                    return MapFlag.MF_NOLOAD;
                case "NOSCRIPT":
                    return MapFlag.MF_NOSCRIPT;
                case "NOEVENT":
                    return MapFlag.MF_NOEVENT;
                case "NOMESSAGE":
                    return MapFlag.MF_NOMESSAGE;
                case "NOCHAT":
                    return MapFlag.MF_NOCHAT;
                case "NOWHISPER":
                    return MapFlag.MF_NOWHISPER;
                case "NOSHOUT":
                    return MapFlag.MF_NOSHOUT;
                case "NOTRADE":
                    return MapFlag.MF_NOTRADE;
                case "NOSTORE":
                    return MapFlag.MF_NOSTORE;
                default:
                    LogManager.Default.Warning($"未知的地图标志: {flagName}");
                    return MapFlag.MF_NONE;
            }
        }
        
        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.Map;
        }
        
        
        
        
        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            
            msg = Array.Empty<byte>();
            return false;
        }

        
        
        
        public void SetPhysicsMap(PhysicsMap physicsMap)
        {
            _physicsMap = physicsMap;
            
            
            if (_physicsMap != null)
            {
                _physicsMap.AddRefMap(this);
            }
        }

        
        
        
        public void SetMiniMap(int miniMapId)
        {
            _miniMapId = miniMapId;
        }

        
        
        
        public void SetLinkCount(int linkCount)
        {
            _linkCount = linkCount;
        }


        
        
        
        public int GetMiniMapId()
        {
            return _miniMapId;
        }

        
        
        
        public int GetLinkCount()
        {
            return _linkCount;
        }

        
        
        
        public PhysicsMap? GetPhysicsMap()
        {
            return _physicsMap;
        }

        
        
        
        
        public void InitMapCells()
        {
            try
            {
                
                if (_physicsMap == null)
                {
                    LogManager.Default.Error($"地图 {MapName} 没有关联的物理地图，无法初始化单元格");
                    return;
                }

                
                if (Width <= 0 || Height <= 0)
                {
                    LogManager.Default.Error($"地图 {MapName} 尺寸无效: {Width}x{Height}");
                    return;
                }

                
                if (_mapCellInfo != null)
                {
                    _mapCellInfo = null;
                }

                
                _mapCellInfo = new MapCellInfo[Width, Height];
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        _mapCellInfo[x, y] = new MapCellInfo();
                    }
                }

                
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化地图单元格失败: {MapName}", exception: ex);
            }
        }

        
        
        
        
        
        
        public void InitLinks()
        {
            if (_linkCount <= 0)
                return;

            try
            {
                
                
                
                
                
                
                
                
                
                
                
                
                
                

                
                
                
                
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化地图链接失败: {MapName}", exception: ex);
            }
        }
    }
    
    
    
    
    public enum MapWeather
    {
        Clear = 0,          
        Rain = 1,           
        Snow = 2,           
        Fog = 3,            
        Storm = 4,          
        Sandstorm = 5       
    }
    
    
    
    
    public enum MapTime
    {
        Day = 0,            
        Night = 1,          
        Dawn = 2,           
        Dusk = 3            
    }
    
    
    
    
    public class MineItem
    {
        public string Name { get; set; } = string.Empty;
        public ushort DuraMin { get; set; }
        public ushort DuraMax { get; set; }
        public ushort Rate { get; set; }
    }
    
    
    
    
    
    public class LinkPoint
    {
        public uint LinkId { get; set; }
        public uint SourceMapId { get; set; }
        public ushort SourceX { get; set; }
        public ushort SourceY { get; set; }
        public uint TargetMapId { get; set; }
        public ushort TargetX { get; set; }
        public ushort TargetY { get; set; }
        public int NeedLevel { get; set; }
        public string NeedItem { get; set; } = string.Empty;
        public string NeedQuest { get; set; } = string.Empty;
        public string ScriptFile { get; set; } = string.Empty;
    }
    
    
    
    
    public enum TerrainType
    {
        Normal = 0,     
        Water = 1,      
        Mountain = 2,   
        Forest = 3,     
        Desert = 4,     
        Snow = 5,       
        Lava = 6        
    }
}
