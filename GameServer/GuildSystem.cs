namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    
    
    
    public enum GuildRank
    {
        Member = 0,         
        Elder = 1,          
        ViceLeader = 2,     
        Leader = 3          
    }

    
    
    
    public class GuildMember
    {
        public uint PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public GuildRank Rank { get; set; }
        public DateTime JoinTime { get; set; }
        public uint Contribution { get; set; }
        public uint LastContributionTime { get; set; }
        public bool IsOnline { get; set; }

        public GuildMember(uint playerId, string playerName, GuildRank rank = GuildRank.Member)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            Rank = rank;
            JoinTime = DateTime.Now;
            Contribution = 0;
            LastContributionTime = 0;
            IsOnline = true;
        }
    }

    
    
    
    public class Guild
    {
        public uint GuildId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LeaderName { get; set; } = string.Empty;
        public uint LeaderId { get; set; }
        public DateTime CreateTime { get; set; }
        public string Notice { get; set; } = string.Empty;
        public uint Funds { get; set; }
        public uint Level { get; set; }
        public uint Experience { get; set; }
        
        private readonly Dictionary<uint, GuildMember> _members = new();
        private readonly object _memberLock = new();
        
        
        private readonly List<ItemInstance> _warehouse = new();
        private readonly object _warehouseLock = new();
        private const int MAX_WAREHOUSE_SLOTS = 100;

        public Guild(uint guildId, string name, uint leaderId, string leaderName)
        {
            GuildId = guildId;
            Name = name;
            LeaderId = leaderId;
            LeaderName = leaderName;
            CreateTime = DateTime.Now;
            Level = 1;
            Experience = 0;
            Funds = 0;
            Notice = "欢迎加入行会！";
            
            
            AddMember(leaderId, leaderName, GuildRank.Leader);
        }

        
        
        
        public bool AddMember(uint playerId, string playerName, GuildRank rank = GuildRank.Member)
        {
            lock (_memberLock)
            {
                if (_members.ContainsKey(playerId))
                    return false;

                if (_members.Count >= GetMaxMembers())
                    return false;

                var member = new GuildMember(playerId, playerName, rank);
                _members[playerId] = member;
                
                LogManager.Default.Info($"玩家 {playerName} 加入行会 {Name}");
                return true;
            }
        }

        
        
        
        public bool RemoveMember(uint playerId)
        {
            lock (_memberLock)
            {
                if (!_members.TryGetValue(playerId, out var member))
                    return false;

                
                if (member.Rank == GuildRank.Leader)
                    return false;

                _members.Remove(playerId);
                
                LogManager.Default.Info($"玩家 {member.PlayerName} 离开行会 {Name}");
                return true;
            }
        }

        
        
        
        public bool RemoveMember(string playerName)
        {
            lock (_memberLock)
            {
                var member = _members.Values.FirstOrDefault(m => m.PlayerName == playerName);
                if (member == null)
                    return false;

                
                if (member.Rank == GuildRank.Leader)
                    return false;

                _members.Remove(member.PlayerId);
                
                LogManager.Default.Info($"玩家 {member.PlayerName} 离开行会 {Name}");
                return true;
            }
        }

        
        
        
        public GuildMember? GetMember(uint playerId)
        {
            lock (_memberLock)
            {
                _members.TryGetValue(playerId, out var member);
                return member;
            }
        }

        
        
        
        public List<GuildMember> GetAllMembers()
        {
            lock (_memberLock)
            {
                return _members.Values.ToList();
            }
        }

        
        
        
        public List<GuildMember> GetOnlineMembers()
        {
            lock (_memberLock)
            {
                return _members.Values.Where(m => m.IsOnline).ToList();
            }
        }

        
        
        
        public int GetMemberCount()
        {
            lock (_memberLock)
            {
                return _members.Count;
            }
        }

        
        
        
        public int GetMaxMembers()
        {
            
            return (int)Level * 10 + 20; 
        }

        
        
        
        public bool SetMemberRank(uint playerId, GuildRank newRank)
        {
            lock (_memberLock)
            {
                if (!_members.TryGetValue(playerId, out var member))
                    return false;

                
                if (member.Rank == GuildRank.Leader)
                    return false;

                member.Rank = newRank;
                return true;
            }
        }

        
        
        
        public void AddContribution(uint playerId, uint amount)
        {
            lock (_memberLock)
            {
                if (_members.TryGetValue(playerId, out var member))
                {
                    member.Contribution += amount;
                    
                    
                    AddExperience(amount / 10);
                }
            }
        }

        
        
        
        public void AddExperience(uint amount)
        {
            Experience += amount;
            
            
            uint requiredExp = GetRequiredExperience();
            while (Experience >= requiredExp && Level < 10)
            {
                Experience -= requiredExp;
                Level++;
                requiredExp = GetRequiredExperience();
                
                LogManager.Default.Info($"行会 {Name} 升级到 {Level} 级");
            }
        }

        
        
        
        private uint GetRequiredExperience()
        {
            return Level * 10000;
        }

        
        
        
        public void SetNotice(string notice)
        {
            if (notice.Length > 200)
                notice = notice.Substring(0, 200);
            
            Notice = notice;
        }

        
        
        
        public bool AddFunds(uint amount)
        {
            if (amount > uint.MaxValue - Funds)
                return false;
            
            Funds += amount;
            return true;
        }

        
        
        
        public bool TakeFunds(uint amount)
        {
            if (Funds < amount)
                return false;
            
            Funds -= amount;
            return true;
        }

        
        
        
        public bool AddToWarehouse(ItemInstance item)
        {
            lock (_warehouseLock)
            {
                if (_warehouse.Count >= MAX_WAREHOUSE_SLOTS)
                    return false;

                _warehouse.Add(item);
                return true;
            }
        }

        
        
        
        public bool RemoveFromWarehouse(int index)
        {
            lock (_warehouseLock)
            {
                if (index < 0 || index >= _warehouse.Count)
                    return false;

                _warehouse.RemoveAt(index);
                return true;
            }
        }

        
        
        
        public List<ItemInstance> GetWarehouseItems()
        {
            lock (_warehouseLock)
            {
                return new List<ItemInstance>(_warehouse);
            }
        }

        
        
        
        public int GetWarehouseFreeSlots()
        {
            lock (_warehouseLock)
            {
                return MAX_WAREHOUSE_SLOTS - _warehouse.Count;
            }
        }

        
        
        
        public virtual bool IsKillGuild(Guild otherGuild)
        {
            
            return false;
        }

        
        
        
        public virtual bool IsAllyGuild(Guild otherGuild)
        {
            
            return false;
        }

        
        
        
        public void MemberOnline(uint playerId)
        {
            lock (_memberLock)
            {
                if (_members.TryGetValue(playerId, out var member))
                {
                    member.IsOnline = true;
                }
            }
        }

        
        
        
        public void MemberOffline(uint playerId)
        {
            lock (_memberLock)
            {
                if (_members.TryGetValue(playerId, out var member))
                {
                    member.IsOnline = false;
                }
            }
        }

        internal LogicMap? GetGuildMap()
        {
            throw new NotImplementedException();
        }

        
        
        
        public string GetFrontPage()
        {
            return $"行会名称: {Name}\n会长: {LeaderName}\n等级: {Level}\n成员: {GetMemberCount()}/{GetMaxMembers()}\n资金: {Funds}\n公告: {Notice}";
        }

        
        
        
        public void SendFirstPage(HumanPlayer player)
        {
            if (player == null) return;
            player.SaySystem(GetFrontPage());
        }

        
        
        
        public void SendExp(HumanPlayer player)
        {
            if (player == null) return;
            player.SaySystem($"行会经验: {Experience}/{GetRequiredExperience()}");
        }

        
        
        
        public void SendMemberList(HumanPlayer player)
        {
            if (player == null) return;
            
            var members = GetAllMembers();
            string memberList = $"行会成员 ({members.Count}人):\n";
            foreach (var member in members)
            {
                memberList += $"{member.PlayerName} - {member.Rank} (贡献: {member.Contribution})\n";
            }
            player.SaySystem(memberList);
        }

        
        
        
        public bool ParseMemberList(HumanPlayer player, string memberList)
        {
            if (player == null) return false;
            
            
            LogManager.Default.Info($"解析行会成员列表: {memberList}");
            return true;
        }

        
        
        
        public string GetErrorMsg()
        {
            return "操作失败";
        }

        
        
        
        public bool IsMaster(HumanPlayer player)
        {
            if (player == null)
                return false;
            
            return LeaderId == player.ObjectId;
        }
    }

    
    
    
    public class GuildManager
    {
        private static GuildManager? _instance;
        public static GuildManager Instance => _instance ??= new GuildManager();

        private readonly Dictionary<uint, Guild> _guilds = new();
        private readonly Dictionary<uint, uint> _playerGuildMap = new(); 
        private readonly object _lock = new();
        
        private uint _nextGuildId = 1000;

        private GuildManager() { }

        
        
        
        public Guild? CreateGuild(string name, uint leaderId, string leaderName)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 16)
                return null;

            
            if (GetGuildByName(name) != null)
                return null;

            
            if (GetPlayerGuild(leaderId) != null)
                return null;

            lock (_lock)
            {
                uint guildId = _nextGuildId++;
                var guild = new Guild(guildId, name, leaderId, leaderName);
                
                _guilds[guildId] = guild;
                _playerGuildMap[leaderId] = guildId;
                
                LogManager.Default.Info($"行会 {name} 创建成功，会长：{leaderName}");
                return guild;
            }
        }

        
        
        
        public GuildEx? CreateGuildEx(string name, uint leaderId, string leaderName)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 16)
                return null;

            
            if (GetGuildByName(name) != null)
                return null;

            
            if (GetPlayerGuild(leaderId) != null)
                return null;

            lock (_lock)
            {
                uint guildId = _nextGuildId++;
                var guild = new GuildEx(guildId, name, leaderId, leaderName);
                
                _guilds[guildId] = guild;
                _playerGuildMap[leaderId] = guildId;
                
                LogManager.Default.Info($"扩展行会 {name} 创建成功，会长：{leaderName}");
                return guild;
            }
        }

        
        
        
        public bool DisbandGuild(uint guildId, uint requesterId)
        {
            lock (_lock)
            {
                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                
                if (guild.LeaderId != requesterId)
                    return false;

                
                if (guild is GuildEx guildEx)
                {
                    guildEx.ClearAllKillRelations();
                    guildEx.ClearAllAllyRelations();
                }

                
                foreach (var member in guild.GetAllMembers())
                {
                    _playerGuildMap.Remove(member.PlayerId);
                }

                
                _guilds.Remove(guildId);
                
                LogManager.Default.Info($"行会 {guild.Name} 已解散");
                return true;
            }
        }

        
        
        
        public bool JoinGuild(uint guildId, uint playerId, string playerName)
        {
            lock (_lock)
            {
                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                
                if (_playerGuildMap.ContainsKey(playerId))
                    return false;

                
                if (!guild.AddMember(playerId, playerName))
                    return false;

                _playerGuildMap[playerId] = guildId;
                return true;
            }
        }

        
        
        
        public bool LeaveGuild(uint playerId)
        {
            lock (_lock)
            {
                if (!_playerGuildMap.TryGetValue(playerId, out var guildId))
                    return false;

                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                
                if (guild.LeaderId == playerId)
                    return false;

                
                if (!guild.RemoveMember(playerId))
                    return false;

                _playerGuildMap.Remove(playerId);
                return true;
            }
        }

        
        
        
        public bool KickMember(uint guildId, uint requesterId, uint targetId)
        {
            lock (_lock)
            {
                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                
                var requester = guild.GetMember(requesterId);
                var target = guild.GetMember(targetId);
                
                if (requester == null || target == null)
                    return false;

                
                if (requester.Rank < GuildRank.Elder)
                    return false;

                
                if (target.Rank >= requester.Rank && requesterId != guild.LeaderId)
                    return false;

                
                if (!guild.RemoveMember(targetId))
                    return false;

                _playerGuildMap.Remove(targetId);
                return true;
            }
        }

        
        
        
        public bool SetMemberRank(uint guildId, uint requesterId, uint targetId, GuildRank newRank)
        {
            lock (_lock)
            {
                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                
                if (guild.LeaderId != requesterId)
                    return false;

                return guild.SetMemberRank(targetId, newRank);
            }
        }

        
        
        
        public Guild? GetGuild(uint guildId)
        {
            lock (_lock)
            {
                _guilds.TryGetValue(guildId, out var guild);
                return guild;
            }
        }

        
        
        
        public GuildEx? GetGuildEx(uint guildId)
        {
            lock (_lock)
            {
                if (_guilds.TryGetValue(guildId, out var guild) && guild is GuildEx guildEx)
                {
                    return guildEx;
                }
                return null;
            }
        }

        
        
        
        public Guild? GetGuildByName(string name)
        {
            lock (_lock)
            {
                return _guilds.Values.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
        }

        
        
        
        public GuildEx? GetGuildExByName(string name)
        {
            lock (_lock)
            {
                var guild = _guilds.Values.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                return guild as GuildEx;
            }
        }

        
        
        
        public Guild? GetPlayerGuild(uint playerId)
        {
            lock (_lock)
            {
                if (_playerGuildMap.TryGetValue(playerId, out var guildId))
                {
                    return GetGuild(guildId);
                }
                return null;
            }
        }

        
        
        
        public GuildEx? GetPlayerGuildEx(uint playerId)
        {
            lock (_lock)
            {
                if (_playerGuildMap.TryGetValue(playerId, out var guildId))
                {
                    return GetGuildEx(guildId);
                }
                return null;
            }
        }

        
        
        
        public List<Guild> GetAllGuilds()
        {
            lock (_lock)
            {
                return _guilds.Values.ToList();
            }
        }

        
        
        
        public List<Guild> SearchGuilds(string keyword)
        {
            lock (_lock)
            {
                return _guilds.Values
                    .Where(g => g.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .Take(20)
                    .ToList();
            }
        }

        
        
        
        public void PlayerOnline(uint playerId)
        {
            lock (_lock)
            {
                var guild = GetPlayerGuild(playerId);
                guild?.MemberOnline(playerId);
            }
        }

        
        
        
        public void PlayerOffline(uint playerId)
        {
            lock (_lock)
            {
                var guild = GetPlayerGuild(playerId);
                guild?.MemberOffline(playerId);
            }
        }

        
        
        
        public void AddContribution(uint playerId, uint amount)
        {
            lock (_lock)
            {
                var guild = GetPlayerGuild(playerId);
                guild?.AddContribution(playerId, amount);
            }
        }

        
        
        
        public bool AddGuildFunds(uint guildId, uint amount)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                return guild?.AddFunds(amount) ?? false;
            }
        }

        
        
        
        public bool TakeGuildFunds(uint guildId, uint amount)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                return guild?.TakeFunds(amount) ?? false;
            }
        }

        
        
        
        public bool SetGuildNotice(uint guildId, uint requesterId, string notice)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                if (guild == null)
                    return false;

                
                var member = guild.GetMember(requesterId);
                if (member == null || member.Rank < GuildRank.ViceLeader)
                    return false;

                guild.SetNotice(notice);
                return true;
            }
        }

        
        
        
        public void SendGuildMessage(uint guildId, string message)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                if (guild == null)
                    return;

                var onlineMembers = guild.GetOnlineMembers();
                foreach (var member in onlineMembers)
                {
                    var player = HumanPlayerMgr.Instance.FindById(member.PlayerId);
                    if (player != null)
                    {
                        
                        ChatManager.Instance.SendMessage(player, ChatChannel.GUILD, message);
                    }
                }
            }
        }

        
        
        
        public List<Guild> GetGuildRanking(int count = 10)
        {
            lock (_lock)
            {
                return _guilds.Values
                    .OrderByDescending(g => g.Level)
                    .ThenByDescending(g => g.Experience)
                    .ThenByDescending(g => g.GetMemberCount())
                    .Take(count)
                    .ToList();
            }
        }

        
        
        
        public List<GuildMember> GetMemberRanking(uint guildId, int count = 20)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                if (guild == null)
                    return new List<GuildMember>();

                return guild.GetAllMembers()
                    .OrderByDescending(m => m.Contribution)
                    .ThenByDescending(m => m.Rank)
                    .Take(count)
                    .ToList();
            }
        }

        
        
        
        public bool IsGuildNameAvailable(string name)
        {
            lock (_lock)
            {
                return GetGuildByName(name) == null;
            }
        }

        
        
        
        public (int totalGuilds, int totalMembers, int onlineMembers) GetStatistics()
        {
            lock (_lock)
            {
                int totalGuilds = _guilds.Count;
                int totalMembers = _playerGuildMap.Count;
                int onlineMembers = 0;
                
                foreach (var guild in _guilds.Values)
                {
                    onlineMembers += guild.GetOnlineMembers().Count;
                }
                
                return (totalGuilds, totalMembers, onlineMembers);
            }
        }
    }
}
