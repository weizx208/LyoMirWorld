namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    
    
    
    
    public class GuildEx : Guild
    {
        
        private readonly HashSet<uint> _killGuilds = new();
        private readonly object _killGuildLock = new();

        
        private readonly HashSet<uint> _allyGuilds = new();
        private readonly object _allyGuildLock = new();

        
        private uint _warCount = 0;
        private const uint MAX_WAR_COUNT = 5; 

        public GuildEx(uint guildId, string name, uint leaderId, string leaderName) 
            : base(guildId, name, leaderId, leaderName)
        {
        }

        
        
        
        public override bool IsKillGuild(Guild otherGuild)
        {
            if (otherGuild == null)
                return false;

            
            if (otherGuild is not GuildEx otherGuildEx)
                return false;

            lock (_killGuildLock)
            {
                return _killGuilds.Contains(otherGuildEx.GuildId);
            }
        }

        
        
        
        public bool AddKillGuild(GuildEx otherGuild)
        {
            if (otherGuild == null || otherGuild.GuildId == GuildId)
                return false;

            lock (_killGuildLock)
            {
                if (_killGuilds.Count >= MAX_WAR_COUNT)
                {
                    LogManager.Default.Warning($"行会 {Name} 已达到最大敌对行会数量限制");
                    return false;
                }

                if (_killGuilds.Contains(otherGuild.GuildId))
                    return false;

                _killGuilds.Add(otherGuild.GuildId);
                _warCount++;

                LogManager.Default.Info($"行会 {Name} 添加敌对行会: {otherGuild.Name}");
                return true;
            }
        }

        
        
        
        public bool RemoveKillGuild(GuildEx otherGuild)
        {
            if (otherGuild == null)
                return false;

            lock (_killGuildLock)
            {
                if (!_killGuilds.Contains(otherGuild.GuildId))
                    return false;

                _killGuilds.Remove(otherGuild.GuildId);
                if (_warCount > 0)
                    _warCount--;

                LogManager.Default.Info($"行会 {Name} 移除敌对行会: {otherGuild.Name}");
                return true;
            }
        }

        
        
        
        public List<uint> GetKillGuilds()
        {
            lock (_killGuildLock)
            {
                return new List<uint>(_killGuilds);
            }
        }

        
        
        
        public int GetKillGuildCount()
        {
            lock (_killGuildLock)
            {
                return _killGuilds.Count;
            }
        }

        
        
        
        public override bool IsAllyGuild(Guild otherGuild)
        {
            if (otherGuild == null)
                return false;

            
            if (otherGuild is not GuildEx otherGuildEx)
                return false;

            lock (_allyGuildLock)
            {
                return _allyGuilds.Contains(otherGuildEx.GuildId);
            }
        }

        
        
        
        public bool AddAllyGuild(GuildEx otherGuild)
        {
            if (otherGuild == null || otherGuild.GuildId == GuildId)
                return false;

            lock (_allyGuildLock)
            {
                if (_allyGuilds.Contains(otherGuild.GuildId))
                    return false;

                _allyGuilds.Add(otherGuild.GuildId);
                LogManager.Default.Info($"行会 {Name} 与 {otherGuild.Name} 结盟");
                return true;
            }
        }

        
        
        
        public bool BreakAlly(string otherGuildName)
        {
            lock (_allyGuildLock)
            {
                var guildToRemove = _allyGuilds.FirstOrDefault(guildId => 
                {
                    var guild = GuildManager.Instance.GetGuild(guildId) as GuildEx;
                    return guild != null && guild.Name == otherGuildName;
                });

                if (guildToRemove != 0)
                {
                    _allyGuilds.Remove(guildToRemove);
                    LogManager.Default.Info($"行会 {Name} 与 {otherGuildName} 解除联盟");
                    return true;
                }

                return false;
            }
        }

        
        
        
        public List<uint> GetAllyGuilds()
        {
            lock (_allyGuildLock)
            {
                return new List<uint>(_allyGuilds);
            }
        }

        
        
        
        public int GetAllyGuildCount()
        {
            lock (_allyGuildLock)
            {
                return _allyGuilds.Count;
            }
        }

        
        
        
        public void SendWords(string message)
        {
            var onlineMembers = GetOnlineMembers();
            foreach (var member in onlineMembers)
            {
                var player = HumanPlayerMgr.Instance.FindById(member.PlayerId);
                if (player != null)
                {
                    player.SaySystem(message);
                }
            }
        }

        
        
        
        
        public void ReviewAroundNameColor()
        {
            var onlineMembers = GetOnlineMembers();
            foreach (var member in onlineMembers)
            {
                var player = HumanPlayerMgr.Instance.FindById(member.PlayerId);
                if (player != null)
                {
                    
                    
                    UpdatePlayerNameColor(player);
                }
            }
        }

        
        
        
        private void UpdatePlayerNameColor(HumanPlayer player)
        {
            if (player == null || player.CurrentMap == null)
                return;

            
            var nearbyPlayers = player.CurrentMap.GetPlayersInRange(player.X, player.Y, 18);
            foreach (var nearbyPlayer in nearbyPlayers)
            {
                if (nearbyPlayer.ObjectId == player.ObjectId)
                    continue;

                
                bool isAtWar = false;
                if (nearbyPlayer.Guild is GuildEx nearbyGuildEx)
                {
                    isAtWar = IsKillGuild(nearbyGuildEx);
                }

                
                
                
                UpdateNameColorForViewer(player, nearbyPlayer, isAtWar);
            }
        }

        
        
        
        private void UpdateNameColorForViewer(HumanPlayer targetPlayer, HumanPlayer viewer, bool isAtWar)
        {
            if (targetPlayer == null || viewer == null)
                return;

            
            
            
            if (isAtWar)
            {
                LogManager.Default.Debug($"玩家 {viewer.Name} 看到 {targetPlayer.Name} 的名字颜色变为红色（战争状态）");
                
                
                
            }
            else
            {
                LogManager.Default.Debug($"玩家 {viewer.Name} 看到 {targetPlayer.Name} 的名字颜色恢复正常");
                
                
                
            }
        }

        
        
        
        public bool CanAddKillGuild()
        {
            lock (_killGuildLock)
            {
                return _killGuilds.Count < MAX_WAR_COUNT;
            }
        }

        
        
        
        public bool IsInWar()
        {
            lock (_killGuildLock)
            {
                return _killGuilds.Count > 0;
            }
        }

        
        
        
        public string GetWarInfo()
        {
            lock (_killGuildLock)
            {
                if (_killGuilds.Count == 0)
                    return "当前没有行会战争";

                var guildNames = new List<string>();
                foreach (var guildId in _killGuilds)
                {
                    var guild = GuildManager.Instance.GetGuild(guildId) as GuildEx;
                    if (guild != null)
                    {
                        guildNames.Add(guild.Name);
                    }
                }

                return $"正在与以下行会进行战争: {string.Join(", ", guildNames)}";
            }
        }

        
        
        
        public string GetAllyInfo()
        {
            lock (_allyGuildLock)
            {
                if (_allyGuilds.Count == 0)
                    return "当前没有联盟行会";

                var guildNames = new List<string>();
                foreach (var guildId in _allyGuilds)
                {
                    var guild = GuildManager.Instance.GetGuild(guildId) as GuildEx;
                    if (guild != null)
                    {
                        guildNames.Add(guild.Name);
                    }
                }

                return $"联盟行会: {string.Join(", ", guildNames)}";
            }
        }

        
        
        
        public void ClearAllKillRelations()
        {
            lock (_killGuildLock)
            {
                var killGuildsCopy = new List<uint>(_killGuilds);
                foreach (var guildId in killGuildsCopy)
                {
                    var otherGuild = GuildManager.Instance.GetGuildEx(guildId);
                    if (otherGuild != null)
                    {
                        otherGuild.RemoveKillGuild(this);
                    }
                }
                _killGuilds.Clear();
                _warCount = 0;
            }
        }

        
        
        
        public void ClearAllAllyRelations()
        {
            lock (_allyGuildLock)
            {
                var allyGuildsCopy = new List<uint>(_allyGuilds);
                foreach (var guildId in allyGuildsCopy)
                {
                    var otherGuild = GuildManager.Instance.GetGuildEx(guildId);
                    if (otherGuild != null)
                    {
                        otherGuild.BreakAlly(Name);
                    }
                }
                _allyGuilds.Clear();
            }
        }

        
        
        
        public new string ToString()
        {
            return $"行会: {Name} (ID: {GuildId}), 等级: {Level}, 成员: {GetMemberCount()}/{GetMaxMembers()}, " +
                   $"战争: {GetKillGuildCount()}, 联盟: {GetAllyGuildCount()}";
        }
    }
}
