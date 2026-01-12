namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using MirCommon;
    using MirCommon.Utils;

    
    
    
    public class GuildWar
    {
        public GuildEx? RequestGuild { get; set; }    
        public GuildEx? AttackGuild { get; set; }     
        public ServerTimer WarTimer { get; set; }     

        public GuildWar()
        {
            RequestGuild = null;
            AttackGuild = null;
            WarTimer = new ServerTimer();
        }

        public GuildWar(GuildEx requestGuild, GuildEx attackGuild, uint warDuration)
        {
            RequestGuild = requestGuild;
            AttackGuild = attackGuild;
            WarTimer = new ServerTimer();
            WarTimer.SaveTime(warDuration * 1000); 
        }

        
        
        
        public bool IsTimeOut()
        {
            return WarTimer.IsTimeOut();
        }

        
        
        
        public uint GetRemainingTime()
        {
            return WarTimer.GetRemainingTime();
        }

        
        
        
        public string GetWarInfo()
        {
            if (RequestGuild == null || AttackGuild == null)
                return "无效的战争信息";

            var remainingMinutes = GetRemainingTime() / 60000;
            return $"{RequestGuild.Name} 对 {AttackGuild.Name} 的战争，剩余时间: {remainingMinutes}分钟";
        }
    }


    
    
    
    public class GuildWarManager
    {
        private static GuildWarManager? _instance;
        public static GuildWarManager Instance => _instance ??= new GuildWarManager();

        private const int MAX_GUILD_WAR = 1024; 

        
        private readonly ObjectPool<GuildWar> _warPool;

        
        private readonly GuildWar?[] _guildWars = new GuildWar[MAX_GUILD_WAR];
        private readonly object _warLock = new();

        private uint _warCount = 0;      
        private uint _updatePtr = 0;     

        
        private uint _warDuration = 3 * 60 * 60; 

        private GuildWarManager()
        {
            
            _warPool = new ObjectPool<GuildWar>(() => new GuildWar(), 100, 1000);
        }

        
        
        
        private GuildWar? GetWarFromPool()
        {
            return _warPool.Get();
        }

        
        
        
        private void ReturnWarToPool(GuildWar war)
        {
            if (war == null)
                return;

            
            war.RequestGuild = null;
            war.AttackGuild = null;
            war.WarTimer = new ServerTimer();

            _warPool.Return(war);
        }

        
        
        
        public bool RequestWar(GuildEx requestGuild, GuildEx attackGuild)
        {
            if (requestGuild == null || attackGuild == null)
            {
                SetError(1000, "行会参数无效");
                return false;
            }

            if (requestGuild.GuildId == attackGuild.GuildId)
            {
                SetError(1001, "不能对自己行会宣战");
                return false;
            }

            lock (_warLock)
            {
                
                if (_warCount >= MAX_GUILD_WAR)
                {
                    SetError(1002, "已经达到最大战争数!");
                    return false;
                }

                
                if (requestGuild.IsAllyGuild(attackGuild))
                {
                    requestGuild.BreakAlly(attackGuild.Name);
                }

                if (attackGuild.IsAllyGuild(requestGuild))
                {
                    attackGuild.BreakAlly(requestGuild.Name);
                }

                
                for (uint i = 0; i < _warCount; i++)
                {
                    var existingWar = _guildWars[i];
                    if (existingWar == null)
                        continue;

                    if ((existingWar.AttackGuild == attackGuild && existingWar.RequestGuild == requestGuild) ||
                        (existingWar.AttackGuild == requestGuild && existingWar.RequestGuild == attackGuild))
                    {
                        SetError(1003, "无法重复申请行会战!");
                        return false;
                    }
                }

                
                var newWar = GetWarFromPool();
                if (newWar == null)
                {
                    SetError(1004, "当前战争资源紧缺，无法进行行会战!");
                    return false;
                }

                
                if (!attackGuild.AddKillGuild(requestGuild))
                {
                    SetError(1005, "和对方进行行会战的行会已经达到上限，请稍候再试");
                    return false;
                }

                if (!requestGuild.AddKillGuild(attackGuild))
                {
                    attackGuild.RemoveKillGuild(requestGuild);
                    SetError(1006, "和您的行会进行行会战的行会已经达到上限，请稍候再试");
                    return false;
                }

                
                newWar.RequestGuild = requestGuild;
                newWar.AttackGuild = attackGuild;
                newWar.WarTimer.SaveTime(_warDuration * 1000); 

                
                _guildWars[_warCount] = newWar;
                _warCount++;

                
                string warMessage = $"{requestGuild.Name}和{attackGuild.Name}的行会战争开始，持续三小时";
                attackGuild.SendWords(warMessage);
                attackGuild.ReviewAroundNameColor();
                requestGuild.SendWords(warMessage);
                requestGuild.ReviewAroundNameColor();

                LogManager.Default.Info($"行会战争开始: {requestGuild.Name} vs {attackGuild.Name}");
                return true;
            }
        }

        
        
        
        
        public void Update()
        {
            lock (_warLock)
            {
                if (_warCount == 0)
                    return;

                if (_updatePtr >= _warCount)
                    _updatePtr = 0;

                var currentWar = _guildWars[_updatePtr];
                if (currentWar == null)
                {
                    _updatePtr++;
                    return;
                }

                
                if (currentWar.IsTimeOut())
                {
                    EndWar(currentWar);
                }

                _updatePtr++;
            }
        }

        
        
        
        private void EndWar(GuildWar war)
        {
            if (war.RequestGuild == null || war.AttackGuild == null)
                return;

            
            war.AttackGuild.RemoveKillGuild(war.RequestGuild);
            war.RequestGuild.RemoveKillGuild(war.AttackGuild);

            
            war.AttackGuild.ReviewAroundNameColor();
            war.RequestGuild.ReviewAroundNameColor();

            
            string endMessage = $"{war.RequestGuild.Name}和{war.AttackGuild.Name}的行会战争结束";
            war.AttackGuild.SendWords(endMessage);
            war.RequestGuild.SendWords(endMessage);

            LogManager.Default.Info($"行会战争结束: {war.RequestGuild.Name} vs {war.AttackGuild.Name}");

            
            RemoveWar(war);
        }

        
        
        
        private void RemoveWar(GuildWar war)
        {
            lock (_warLock)
            {
                
                int warIndex = -1;
                for (int i = 0; i < _warCount; i++)
                {
                    if (_guildWars[i] == war)
                    {
                        warIndex = i;
                        break;
                    }
                }

                if (warIndex == -1)
                    return;

                
                ReturnWarToPool(war);

                
                _warCount--;
                _guildWars[warIndex] = _guildWars[_warCount];
                _guildWars[_warCount] = null;

                
                if (_updatePtr >= _warCount)
                    _updatePtr = 0;
            }
        }

        
        
        
        public bool ForceEndWar(uint requestGuildId, uint attackGuildId)
        {
            lock (_warLock)
            {
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war == null || war.RequestGuild == null || war.AttackGuild == null)
                        continue;

                    if ((war.RequestGuild.GuildId == requestGuildId && war.AttackGuild.GuildId == attackGuildId) ||
                        (war.RequestGuild.GuildId == attackGuildId && war.AttackGuild.GuildId == requestGuildId))
                    {
                        EndWar(war);
                        return true;
                    }
                }
                return false;
            }
        }

        
        
        
        public List<GuildWar> GetAllWars()
        {
            lock (_warLock)
            {
                var wars = new List<GuildWar>();
                for (int i = 0; i < _warCount; i++)
                {
                    if (_guildWars[i] != null)
                    {
                        wars.Add(_guildWars[i]!);
                    }
                }
                return wars;
            }
        }

        
        
        
        public List<GuildWar> GetGuildWars(GuildEx guild)
        {
            if (guild == null)
                return new List<GuildWar>();

            lock (_warLock)
            {
                var wars = new List<GuildWar>();
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war == null || war.RequestGuild == null || war.AttackGuild == null)
                        continue;

                    if (war.RequestGuild.GuildId == guild.GuildId || war.AttackGuild.GuildId == guild.GuildId)
                    {
                        wars.Add(war);
                    }
                }
                return wars;
            }
        }

        
        
        
        public bool AreGuildsAtWar(GuildEx guild1, GuildEx guild2)
        {
            if (guild1 == null || guild2 == null)
                return false;

            lock (_warLock)
            {
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war == null || war.RequestGuild == null || war.AttackGuild == null)
                        continue;

                    if ((war.RequestGuild.GuildId == guild1.GuildId && war.AttackGuild.GuildId == guild2.GuildId) ||
                        (war.RequestGuild.GuildId == guild2.GuildId && war.AttackGuild.GuildId == guild1.GuildId))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        
        
        
        public (int totalWars, int activeWars) GetStatistics()
        {
            lock (_warLock)
            {
                int activeWars = 0;
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war != null && !war.IsTimeOut())
                    {
                        activeWars++;
                    }
                }
                return ((int)_warCount, activeWars);
            }
        }

        
        
        
        public void SetWarDuration(uint durationInSeconds)
        {
            _warDuration = durationInSeconds;
            LogManager.Default.Info($"行会战争持续时间设置为: {durationInSeconds}秒 ({durationInSeconds / 3600}小时)");
        }

        
        
        
        public uint GetWarDuration()
        {
            return _warDuration;
        }

        
        
        
        public void ClearAllWars()
        {
            lock (_warLock)
            {
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war != null)
                    {
                        
                        if (war.RequestGuild != null && war.AttackGuild != null)
                        {
                            war.AttackGuild.RemoveKillGuild(war.RequestGuild);
                            war.RequestGuild.RemoveKillGuild(war.AttackGuild);
                        }
                        ReturnWarToPool(war);
                        _guildWars[i] = null;
                    }
                }
                _warCount = 0;
                _updatePtr = 0;
                LogManager.Default.Info("已清理所有行会战争");
            }
        }

        
        
        
        private void SetError(int errorCode, string errorMessage)
        {
            LogManager.Default.Error($"行会战争错误 {errorCode}: {errorMessage}");
        }

        
        
        
        public string GetStatusInfo()
        {
            var stats = GetStatistics();
            return $"行会战争管理器状态: 总战争数={stats.totalWars}, 活跃战争={stats.activeWars}, 池可用对象={_warPool.AvailableCount}, 已创建对象={_warPool.CreatedCount}";
        }
    }
}
