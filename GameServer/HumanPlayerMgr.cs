using MirCommon;
using MirCommon.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace GameServer
{
    
    
    
    
    public class HumanPlayerMgr
    {
        private static HumanPlayerMgr? _instance;
        public static HumanPlayerMgr Instance => _instance ??= new HumanPlayerMgr();

        
        private const int MAX_HUMANPLAYER = 128;

        
        private readonly Dictionary<uint, HumanPlayer> _playersById = new();

        
        private readonly Dictionary<string, HumanPlayer> _playersByName = new(StringComparer.OrdinalIgnoreCase);

        
        private uint _nextPlayerId = 1;

        
        private readonly object _lock = new();

        private HumanPlayerMgr() { }

        
        
        
        public HumanPlayer? FindByName(string name)
        {
            lock (_lock)
            {
                return _playersByName.TryGetValue(name, out var player) ? player : null;
            }
        }

        
        
        
        public HumanPlayer? FindById(uint id)
        {
            lock (_lock)
            {
                
                uint type = (id & 0xFF000000u) >> 24;
                if (type != 0)
                {
                    if (type != (uint)MirObjectType.Player)
                        return null;

                    id &= 0x00FFFFFFu;
                }

                return _playersById.TryGetValue(id, out var player) ? player : null;
            }
        }

        
        
        
        public HumanPlayer? NewPlayer(string account, string name, uint charDbId, TcpClient _client)
        {
            lock (_lock)
            {
                
                if (_playersById.Count >= MAX_HUMANPLAYER)
                {
                    LogManager.Default.Warning($"已达到最大玩家数量限制: {MAX_HUMANPLAYER}");
                    return null;
                }

                
                if (_playersByName.ContainsKey(name))
                {
                    LogManager.Default.Warning($"玩家名称已存在: {name}");
                    return null;
                }

                
                uint playerId = _nextPlayerId++;
                if (playerId > 0xffffff) 
                {
                    playerId = 1; 
                    _nextPlayerId = 2;
                }

                
                uint objectId = ObjectIdUtil.MakeObjectId(MirObjectType.Player, playerId);

                
                var player = new HumanPlayer(account, name, charDbId, _client);
                
                
                
                typeof(GameObject).GetProperty("ObjectId")?.SetValue(player, objectId);

                
                _playersById[playerId] = player;
                _playersByName[name] = player;

                
                

                LogManager.Default.Info($"创建新玩家: {name} (ID: {objectId:X8})");
                return player;
            }
        }

        
        
        
        public bool DeletePlayer(HumanPlayer player)
        {
            if (player == null)
                return false;

            lock (_lock)
            {
                
                uint playerId = player.ObjectId & 0xffffff;

                
                _playersByName.Remove(player.Name);

                
                bool removed = _playersById.Remove(playerId);

                if (removed)
                {
                    
                    
                    

                    LogManager.Default.Info($"删除玩家: {player.Name} (ID: {player.ObjectId:X8})");
                }

                return removed;
            }
        }

        
        
        
        
        public bool AddPlayerNameList(HumanPlayer player, string name)
        {
            if (player == null || string.IsNullOrEmpty(name))
                return false;

            lock (_lock)
            {
                
                if (_playersByName.ContainsKey(name))
                {
                    LogManager.Default.Warning($"玩家名称已存在: {name}");
                    return false;
                }

                
                _playersByName[name] = player;

                LogManager.Default.Debug($"添加玩家到名称列表: {name} -> {player.ObjectId:X8}");
                return true;
            }
        }

        
        
        
        public int GetCount()
        {
            lock (_lock)
            {
                return _playersById.Count;
            }
        }

        
        
        
        public List<HumanPlayer> GetAllPlayers()
        {
            lock (_lock)
            {
                return _playersById.Values.ToList();
            }
        }

        
        
        
        public HumanPlayer? FindPlayer(string identifier)
        {
            
            var player = FindByName(identifier);
            if (player != null)
                return player;

            
            if (uint.TryParse(identifier, out uint id))
            {
                return FindById(id);
            }

            return null;
        }

        
        
        
        public bool IsPlayerOnline(string name)
        {
            lock (_lock)
            {
                return _playersByName.ContainsKey(name);
            }
        }

        
        
        
        public bool IsPlayerOnline(uint id)
        {
            lock (_lock)
            {
                return _playersById.ContainsKey(id & 0xffffff);
            }
        }

        
        
        
        public void BroadcastToAllPlayers(byte[] message)
        {
            lock (_lock)
            {
                foreach (var player in _playersById.Values)
                {
                    try
                    {
                        player.SendMessage(message);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"广播消息给玩家失败: {player.Name}", exception: ex);
                    }
                }
            }
        }

        
        
        
        public void BroadcastSystemMessage(string message)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt16(0x64); 
            builder.WriteUInt16(0xff00); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString($"[系统公告] {message}");

            BroadcastToAllPlayers(builder.Build());
        }

        
        
        
        public void UpdateAllPlayers()
        {
            lock (_lock)
            {
                foreach (var player in _playersById.Values.ToList()) 
                {
                    try
                    {
                        player.Update();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"更新玩家失败: {player.Name}", exception: ex);
                    }
                }
            }
        }

        
        
        
        public void DisconnectAllPlayers()
        {
            lock (_lock)
            {
                var players = _playersById.Values.ToList();
                foreach (var player in players)
                {
                    try
                    {
                        
                        var builder = new PacketBuilder();
                        builder.WriteUInt16(0x100); 
                        builder.WriteUInt16(0);
                        builder.WriteUInt16(0);
                        builder.WriteUInt16(0);
                        builder.WriteString("服务器关闭");

                        player.SendMessage(builder.Build());

                        
                        player.CurrentMap?.RemoveObject(player);

                        LogManager.Default.Info($"断开玩家连接: {player.Name}");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"断开玩家连接失败: {player.Name}", exception: ex);
                    }
                }

                
                _playersById.Clear();
                _playersByName.Clear();
                _nextPlayerId = 1;

                LogManager.Default.Info($"已断开所有 {players.Count} 个玩家连接");
            }
        }

        
        
        
        public PlayerStats GetPlayerStats()
        {
            lock (_lock)
            {
                var stats = new PlayerStats
                {
                    TotalPlayers = _playersById.Count,
                    MaxPlayers = MAX_HUMANPLAYER,
                    PlayersByJob = new Dictionary<byte, int>(),
                    PlayersByLevel = new Dictionary<int, int>()
                };

                
                foreach (var player in _playersById.Values)
                {
                    
                    if (!stats.PlayersByJob.ContainsKey(player.Job))
                        stats.PlayersByJob[player.Job] = 0;
                    stats.PlayersByJob[player.Job]++;

                    
                    int levelGroup = (player.Level / 10) * 10;
                    if (!stats.PlayersByLevel.ContainsKey(levelGroup))
                        stats.PlayersByLevel[levelGroup] = 0;
                    stats.PlayersByLevel[levelGroup]++;
                }

                return stats;
            }
        }

        
        
        
        public List<HumanPlayer> FindNearbyPlayers(uint mapId, int x, int y, int range)
        {
            var result = new List<HumanPlayer>();

            lock (_lock)
            {
                foreach (var player in _playersById.Values)
                {
                    if (player.CurrentMap?.MapId == mapId)
                    {
                        int dx = Math.Abs(player.X - x);
                        int dy = Math.Abs(player.Y - y);
                        if (dx <= range && dy <= range)
                        {
                            result.Add(player);
                        }
                    }
                }
            }

            return result;
        }

        
        
        
        public List<HumanPlayer> FindPlayersInMap(uint mapId)
        {
            var result = new List<HumanPlayer>();

            lock (_lock)
            {
                foreach (var player in _playersById.Values)
                {
                    if (player.CurrentMap?.MapId == mapId)
                    {
                        result.Add(player);
                    }
                }
            }

            return result;
        }
    }

    
    
    
    public class PlayerStats
    {
        public int TotalPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public Dictionary<byte, int> PlayersByJob { get; set; } = new();
        public Dictionary<int, int> PlayersByLevel { get; set; } = new();

        public override string ToString()
        {
            string jobStats = string.Join(", ", PlayersByJob.Select(kv => $"{GetJobName(kv.Key)}:{kv.Value}"));
            string levelStats = string.Join(", ", PlayersByLevel.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}-{kv.Key + 9}级:{kv.Value}"));

            return $"在线玩家: {TotalPlayers}/{MaxPlayers} | 职业分布: {jobStats} | 等级分布: {levelStats}";
        }

        private string GetJobName(byte job)
        {
            return job switch
            {
                0 => "战士",
                1 => "法师",
                2 => "道士",
                _ => "未知"
            };
        }
    }
}
