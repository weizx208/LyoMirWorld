using System;
using System.Collections.Generic;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    public class MapManager
    {
        private static MapManager? _instance;
        public static MapManager Instance => _instance ??= new MapManager();

        private readonly Dictionary<uint, LogicMap> _maps = new();
        private readonly object _lock = new();

        private MapManager()
        {
            
        }

        
        
        
        public bool Load()
        {
            try
            {
                
                var logicMaps = LogicMapMgr.Instance.GetAllMaps();
                int loadedCount = 0;
                
                foreach (var logicMap in logicMaps)
                {
                    
                    
                    AddMap(logicMap);
                    loadedCount++;

                    LogManager.Default.Debug($"加载地图: ID={logicMap.MapId}, 名称={logicMap.MapName}, 大小={logicMap.Width}x{logicMap.Height}");
                }

                
                if (loadedCount == 0)
                {
                    LogManager.Default.Warning("未从LogicMapMgr加载到地图，使用默认地图");
                    InitializeDefaultMaps();
                    loadedCount = _maps.Count;
                }

                LogManager.Default.Info($"已加载 {loadedCount} 个地图");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载地图数据失败: {ex.Message}", exception: ex);
                
                InitializeDefaultMaps();
                return false;
            }
        }

        
        
        
        private void InitializeDefaultMaps()
        {
            
            AddMap(new LogicMap(0, "比奇城", 1000, 1000)
            {
                IsSafeZone = true,
                AllowPK = false,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(1, "毒蛇山谷", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(2, "盟重土城", 1000, 1000)
            {
                IsSafeZone = true,
                AllowPK = false,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(3, "沙巴克城", 600, 600)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = false,
                AllowRecall = false
            });

            AddMap(new LogicMap(4, "祖玛寺庙一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(5, "石墓一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(6, "沃玛寺庙一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(7, "赤月峡谷一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(8, "牛魔寺庙一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(9, "封魔谷", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(10, "苍月岛", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(11, "白日门", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(12, "魔龙城", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            LogManager.Default.Info($"已加载 {_maps.Count} 个默认地图");
        }

        
        
        
        public void AddMap(LogicMap map)
        {
            lock (_lock)
            {
                _maps[map.MapId] = map;
            }
        }

        
        
        
        public LogicMap? GetMap(uint mapId)
        {
            lock (_lock)
            {
                _maps.TryGetValue(mapId, out var map);
                return map;
            }
        }

        
        
        
        public bool RemoveMap(uint mapId)
        {
            lock (_lock)
            {
                return _maps.Remove(mapId);
            }
        }

        
        
        
        public List<LogicMap> GetAllMaps()
        {
            lock (_lock)
            {
                return new List<LogicMap>(_maps.Values);
            }
        }

        
        
        
        public int GetMapCount()
        {
            lock (_lock)
            {
                return _maps.Count;
            }
        }

        
        
        
        public bool HasMap(uint mapId)
        {
            lock (_lock)
            {
                return _maps.ContainsKey(mapId);
            }
        }

        
        
        
        public string GetMapName(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.MapName ?? "未知地图";
        }

        
        
        
        public (int width, int height) GetMapSize(uint mapId)
        {
            var map = GetMap(mapId);
            if (map == null)
                return (0, 0);
            return (map.Width, map.Height);
        }

        
        
        
        public bool CanTeleportTo(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowTeleport ?? false;
        }

        
        
        
        public bool CanRecallTo(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowRecall ?? false;
        }

        
        
        
        public bool IsSafeZone(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.IsSafeZone ?? false;
        }

        
        
        
        public bool AllowPK(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowPK ?? false;
        }

        
        
        
        public bool AllowPets(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowPets ?? false;
        }

        
        
        
        public bool AllowMounts(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowMounts ?? false;
        }

        
        
        
        public float GetExpFactor(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.ExpFactor ?? 1.0f;
        }

        
        
        
        public float GetDropFactor(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.DropFactor ?? 1.0f;
        }

        
        
        
        public void SetExpFactor(uint mapId, float factor)
        {
            var map = GetMap(mapId);
            if (map != null)
            {
                map.ExpFactor = factor;
            }
        }

        
        
        
        public void SetDropFactor(uint mapId, float factor)
        {
            var map = GetMap(mapId);
            if (map != null)
            {
                map.DropFactor = factor;
            }
        }

        
        
        
        public void UpdateAllMaps()
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    map.Update();
                }
            }
        }

        
        
        
        public int GetTotalPlayerCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetPlayerCount();
                }
            }
            return total;
        }

        
        
        
        public int GetTotalMonsterCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetMonsterCount();
                }
            }
            return total;
        }

        
        
        
        public int GetTotalNPCCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetNPCCount();
                }
            }
            return total;
        }

        
        
        
        public int GetTotalItemCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetItemCount();
                }
            }
            return total;
        }

        
        
        
        public int GetTotalObjectCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetTotalObjectCount();
                }
            }
            return total;
        }

        
        
        
        public void ShowAllMapInfo()
        {
            lock (_lock)
            {
                LogManager.Default.Info("=== 地图信息 ===");
                foreach (var map in _maps.Values)
                {
                    LogManager.Default.Info($"[{map.MapId}] {map.MapName} - 大小:{map.Width}x{map.Height} 玩家:{map.GetPlayerCount()} 怪物:{map.GetMonsterCount()} NPC:{map.GetNPCCount()}");
                }
            }
        }

        
        
        
        public LogicMap? FindPlayerMap(uint playerId)
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    if (map.GetPlayer(playerId) != null)
                    {
                        return map;
                    }
                }
                return null;
            }
        }

        
        
        
        public LogicMap? FindMonsterMap(uint monsterId)
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    if (map.GetMonster(monsterId) != null)
                    {
                        return map;
                    }
                }
                return null;
            }
        }

        
        
        
        public LogicMap? FindNPCMap(uint npcId)
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    if (map.GetNPC(npcId) != null)
                    {
                        return map;
                    }
                }
                return null;
            }
        }

        
        
        
        public LogicMap? FindItemMap(uint itemId)
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    if (map.GetItem(itemId) != null)
                    {
                        return map;
                    }
                }
                return null;
            }
        }

        
        
        
        public bool TeleportPlayer(HumanPlayer player, uint targetMapId, int x, int y)
        {
            var targetMap = GetMap(targetMapId);

            if (targetMap == null)
            {
                player.SaySystem("目标地图不存在");
                return false;
            }

            
            x = Math.Clamp(x, 0, Math.Max(0, targetMap.Width - 1));
            y = Math.Clamp(y, 0, Math.Max(0, targetMap.Height - 1));
            return player.ChangeMap(targetMapId, (ushort)x, (ushort)y);
        }

        
        
        
        public bool RecallPlayer(HumanPlayer player, uint targetMapId)
        {
            var targetMap = GetMap(targetMapId);
            if (targetMap == null || !targetMap.IsSafeZone)
            {
                player.Say("目标地图不是安全区");
                return false;
            }

            
            int centerX = targetMap.Width / 2;
            int centerY = targetMap.Height / 2;

            return TeleportPlayer(player, targetMapId, centerX, centerY);
        }

        
        
        
        public bool RandomTeleportPlayer(HumanPlayer player, uint targetMapId)
        {
            var targetMap = GetMap(targetMapId);
            if (targetMap == null)
            {
                player.Say("目标地图不存在");
                return false;
            }

            Random random = new Random();
            int attempts = 0;
            const int maxAttempts = 100;

            while (attempts < maxAttempts)
            {
                int x = random.Next(0, targetMap.Width);
                int y = random.Next(0, targetMap.Height);

                if (targetMap.CanMoveTo(x, y))
                {
                    return TeleportPlayer(player, targetMapId, x, y);
                }

                attempts++;
            }

            player.Say("无法找到可传送的位置");
            return false;
        }

        internal LogicMap? GetTownMap()
        {
            throw new NotImplementedException();
        }
    }
}
