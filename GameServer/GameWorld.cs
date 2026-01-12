using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class GameWorld
    {
        private static GameWorld? _instance;
        public static GameWorld Instance => _instance ??= new GameWorld();

        
        private readonly ConcurrentDictionary<uint, HumanPlayer> _players = new();
        private readonly DateTime _startTime = DateTime.Now;
        private long _updateCount = 0;
        private uint _loopCount = 0; 

        
        private readonly ConcurrentDictionary<int, float> _gameVars = new();      
        private readonly ConcurrentDictionary<string, string> _gameNames = new(); 
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, HumanDataDesc>> _humanDataDescs = new(); 
        private readonly ConcurrentDictionary<int, StartPoint> _startPoints = new(); 
        private readonly ConcurrentDictionary<string, int> _startPointNameToIndex = new();
        private readonly List<FirstLoginInfo> _firstLoginInfos = new();
        private readonly ConcurrentDictionary<int, int> _channelWaitTimes = new(); 
        
        
        private string _notice = "欢迎来到lyo的测试传世！";
        private readonly ConcurrentBag<string> _lineNotices = new();
        
        
        private readonly ReaderWriterLockSlim _configLock = new();

        
        private readonly ConcurrentQueue<MonsterEx> _updateMonsterQueue = new(); 
        private readonly ConcurrentQueue<GlobeProcess> _globeProcessQueue = new(); 
        private Timer? _monsterUpdateTimer;
        private Timer? _dbUpdateTimer;
        private const uint UPDATE_LOOP = 1000; 

        
        public event Action<string>? OnConfigChanged;

        private GameWorld()
        {
            InitializeDefaultValues();
        }

        
        
        
        private void InitializeDefaultValues()
        {
            
            SetGameVar(GameVarConstants.MaxGold, 5000000);
            SetGameVar(GameVarConstants.MaxYuanbao, 2000);
            SetGameVar(GameVarConstants.MaxGroupMember, 10);
            SetGameVar(GameVarConstants.RedPkPoint, 12);
            SetGameVar(GameVarConstants.YellowPkPoint, 6);
            SetGameVar(GameVarConstants.StorageSize, 100);
            SetGameVar(GameVarConstants.CharInfoBackupTime, 5);
            SetGameVar(GameVarConstants.OnePkPointTime, 60);
            SetGameVar(GameVarConstants.GrayNameTime, 300);
            SetGameVar(GameVarConstants.OncePkPoint, 1);
            SetGameVar(GameVarConstants.PkCurseRate, 10);
            SetGameVar(GameVarConstants.AddFriendLevel, 30);
            SetGameVar(GameVarConstants.EnableSafeAreaNotice, 1);
            SetGameVar(GameVarConstants.WalkSpeed, 600);
            SetGameVar(GameVarConstants.RunSpeed, 300);
            SetGameVar(GameVarConstants.AttackSpeed, 800);
            SetGameVar(GameVarConstants.BeAttackSpeed, 800);
            SetGameVar(GameVarConstants.SpellSkillSpeed, 800);
            SetGameVar(GameVarConstants.ExpFactor, 1.0f);

            
            SetGameName(GameName.GoldName, "金币");
            SetGameName(GameName.MaleName, "男");
            SetGameName(GameName.FemaleName, "女");
            SetGameName(GameName.WarrName, "战士");
            SetGameName(GameName.MagicanName, "法师");
            SetGameName(GameName.TaoshiName, "道士");
            SetGameName(GameName.Version, "1,8,8,8");

            
            SetChannelWaitTime(ChatWaitChannel.Normal, 1);
            SetChannelWaitTime(ChatWaitChannel.Cry, 10);
            SetChannelWaitTime(ChatWaitChannel.Whisper, 2);
            SetChannelWaitTime(ChatWaitChannel.Group, 2);
            SetChannelWaitTime(ChatWaitChannel.Guild, 3);
            SetChannelWaitTime(ChatWaitChannel.GM, 0);
        }

        
        
        
        public bool Initialize()
        {
            LogManager.Default.Info("游戏世界初始化...");
            
            
            string physicsMapPath = GetGameName(GameName.PhysicsMapPath);
            string physicsCachePath = GetGameName(GameName.PhysicsCachePath);
            
            if (!string.IsNullOrEmpty(physicsMapPath) && !string.IsNullOrEmpty(physicsCachePath))
            {
                PhysicsMapMgr.Instance.Init(physicsMapPath, physicsCachePath);
                LogManager.Default.Info($"物理地图管理器初始化完成: 地图路径={physicsMapPath}, 缓存路径={physicsCachePath}");
            }
            else
            {
                LogManager.Default.Warning("物理地图路径或缓存路径未配置，物理地图管理器初始化跳过");
            }
            
            return true;
        }

        
        
        
        public void Update()
        {
            Interlocked.Increment(ref _updateCount);
             
            
            TimeSystem.Instance.Update();
             
            
            ProcessGlobeProcessQueue();

            
            
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    MonsterGenManager.Instance.UpdateGen();
                }

                for (int i = 0; i < 50; i++)
                {
                    MonsterManagerEx.Instance.UpdateDeleteMonster();
                }

                DownItemMgr.Instance.UpdateDeletedObject();
                DownItemMgr.Instance.UpdateDownItem();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("世界循环刷怪/掉落更新异常", exception: ex);
            }

            
            try
            {
                foreach (var player in _players.Values)
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
            catch (Exception ex)
            {
                LogManager.Default.Error("更新玩家列表异常", exception: ex);
            }
        }

        #region 玩家管理
        public void AddPlayer(HumanPlayer player)
        {
            _players[player.ObjectId] = player;
        }

        
        
        
        public bool AddMapObject(GameObject obj)
        {
            if (obj == null)
                return false;

            
            if (obj is HumanPlayer player)
            {
                AddPlayer(player);

                var map = LogicMapMgr.Instance.GetLogicMapById((uint)player.MapId);
                if (map == null)
                {
                    LogManager.Default.Error($"添加玩家到地图失败：找不到LogicMap，player={player.Name}, mapId={player.MapId}, pos=({player.X},{player.Y})");
                    return false;
                }

                if (!map.AddObject(player, player.X, player.Y))
                {
                    LogManager.Default.Error($"添加玩家到地图失败：map.AddObject返回false，player={player.Name}, mapId={player.MapId}, pos=({player.X},{player.Y})");
                    return false;
                }

                return true;
            }

            
            
            LogManager.Default.Debug($"添加地图对象: {obj.GetType().Name}, ID={obj.ObjectId}");
            return true;
        }

        public void RemovePlayer(uint playerId)
        {
            _players.TryRemove(playerId, out _);
        }

        public HumanPlayer[] GetAllPlayers()
        {
            return _players.Values.ToArray();
        }

        public HumanPlayer? GetPlayer(uint playerId)
        {
            return _players.TryGetValue(playerId, out var player) ? player : null;
        }

        public int GetPlayerCount() => _players.Count;
        #endregion

        #region 对象查找（对齐C++ GetAliveObjectById）

        
        
        
        
        public MapObject? GetAliveObjectById(uint id)
        {
            var type = ObjectIdUtil.GetType(id);
            switch (type)
            {
                case MirObjectType.Player:
                    return HumanPlayerMgr.Instance.FindById(id);
                case MirObjectType.Monster:
                    return MonsterManagerEx.Instance.GetMonsterById(id);
                case MirObjectType.NPC:
                    
                    return NpcManagerEx.Instance.GetNpc(id) ?? (MapObject?)NPCManager.Instance.GetNPC(id);
                default:
                    return null;
            }
        }

        #endregion

        #region 配置数据访问接口
        
        
        
        public float GetGameVar(int varKey)
        {
            return _gameVars.TryGetValue(varKey, out var value) ? value : 0f;
        }

        
        
        
        public void SetGameVar(int varKey, float value)
        {
            _gameVars[varKey] = value;
            OnConfigChanged?.Invoke($"GameVar_{varKey}");
        }

        
        
        
        public void SetExpFactor(float factor)
        {
            SetGameVar(GameVarConstants.ExpFactor, factor);
        }

        
        
        
        public void SetUseBigBag(bool useBigBag)
        {
            
            
            LogManager.Default.Info($"设置大背包标志: {useBigBag}");
        }

        
        
        
        public string GetGameName(string nameKey)
        {
            return _gameNames.TryGetValue(nameKey, out var name) ? name : nameKey;
        }

        
        
        
        public void SetGameName(string nameKey, string value)
        {
            _gameNames[nameKey] = value;
            OnConfigChanged?.Invoke($"GameName_{nameKey}");
        }

        
        
        
        public HumanDataDesc? GetHumanDataDesc(int profession, int level)
        {
            if (_humanDataDescs.TryGetValue(profession, out var levelDict) &&
                levelDict.TryGetValue(level, out var desc))
            {
                return desc;
            }
            return null;
        }

        
        
        
        public void SetHumanDataDesc(int profession, int level, HumanDataDesc desc)
        {
            if (!_humanDataDescs.ContainsKey(profession))
            {
                _humanDataDescs[profession] = new ConcurrentDictionary<int, HumanDataDesc>();
            }
            _humanDataDescs[profession][level] = desc;
            OnConfigChanged?.Invoke($"HumanDataDesc_{profession}_{level}");
        }

        
        
        
        public StartPoint? GetStartPoint(int index)
        {
            return _startPoints.TryGetValue(index, out var point) ? point : null;
        }

        
        
        
        public StartPoint? GetStartPoint(string name)
        {
            if (_startPointNameToIndex.TryGetValue(name, out int index))
            {
                return GetStartPoint(index);
            }
            return null;
        }

        
        
        
        public void SetStartPoint(int index, StartPoint point)
        {
            _startPoints[index] = point;
            if (!string.IsNullOrEmpty(point.Name))
            {
                _startPointNameToIndex[point.Name] = index;
            }
            OnConfigChanged?.Invoke($"StartPoint_{index}");
        }

        
        
        
        public FirstLoginInfo? GetFirstLoginInfo()
        {
            return _firstLoginInfos.Count > 0 ? _firstLoginInfos[0] : null;
        }

        
        
        
        public void SetFirstLoginInfo(FirstLoginInfo info)
        {
            _firstLoginInfos.Clear();
            _firstLoginInfos.Add(info);
            OnConfigChanged?.Invoke("FirstLoginInfo");
        }

        
        
        
        public int GetChannelWaitTime(int channel)
        {
            return _channelWaitTimes.TryGetValue(channel, out var time) ? time : 1;
        }

        
        
        
        public void SetChannelWaitTime(int channel, int seconds)
        {
            _channelWaitTimes[channel] = seconds;
            OnConfigChanged?.Invoke($"ChannelWaitTime_{channel}");
        }

        
        
        
        public bool GetBornPoint(int profession, out int mapId, out int x, out int y, string? startPointName = null)
        {
            mapId = 0;
            x = 0;
            y = 0;

            
            if (!string.IsNullOrEmpty(startPointName))
            {
                var startPoint = GetStartPoint(startPointName);
                if (startPoint != null)
                {
                    mapId = startPoint.MapId;
                    x = startPoint.X;
                    y = startPoint.Y;
                    return true;
                }
            }

            
            
            var defaultStartPoint = GetStartPoint("新手村");
            if (defaultStartPoint != null)
            {
                mapId = defaultStartPoint.MapId;
                x = defaultStartPoint.X;
                y = defaultStartPoint.Y;
                return true;
            }

            
            
            switch (profession)
            {
                case 0: 
                    mapId = 16;
                    x = 477;
                    y = 222;
                    break;
                case 1: 
                    mapId = 16;
                    x = 477;
                    y = 222;
                    break;
                case 2: 
                    mapId = 16;
                    x = 477;
                    y = 222;
                    break;
                default:
                    return false;
            }

            return true;
        }
        #endregion

        #region 批量配置操作
        
        
        
        public void SetGameVars(Dictionary<int, float> vars)
        {
            foreach (var kvp in vars)
            {
                SetGameVar(kvp.Key, kvp.Value);
            }
        }

        
        
        
        public void SetGameNames(Dictionary<string, string> names)
        {
            foreach (var kvp in names)
            {
                SetGameName(kvp.Key, kvp.Value);
            }
        }

        
        
        
        public void SetHumanDataDescs(int profession, Dictionary<int, HumanDataDesc> descs)
        {
            if (!_humanDataDescs.ContainsKey(profession))
            {
                _humanDataDescs[profession] = new ConcurrentDictionary<int, HumanDataDesc>();
            }

            foreach (var kvp in descs)
            {
                _humanDataDescs[profession][kvp.Key] = kvp.Value;
            }
            OnConfigChanged?.Invoke($"HumanDataDescs_{profession}");
        }

        
        
        
        public void SetStartPoints(Dictionary<int, StartPoint> points)
        {
            foreach (var kvp in points)
            {
                SetStartPoint(kvp.Key, kvp.Value);
            }
        }

        
        
        
        public void SetChannelWaitTimes(Dictionary<int, int> waitTimes)
        {
            foreach (var kvp in waitTimes)
            {
                SetChannelWaitTime(kvp.Key, kvp.Value);
            }
        }

        
        
        
        public void SetMineList(Dictionary<string, string> mineList)
        {
            
            
            
            LogManager.Default.Info($"设置矿石列表: {mineList.Count} 个矿石");
            OnConfigChanged?.Invoke("MineList");
        }
        
        
        
        
        public void SetNotice(string notice)
        {
            _notice = notice;
            OnConfigChanged?.Invoke("Notice");
        }
        
        
        
        
        public string GetNotice() => _notice;
        
        
        
        
        public void SetLineNotices(IEnumerable<string> notices)
        {
            _lineNotices.Clear();
            foreach (var notice in notices)
            {
                _lineNotices.Add(notice);
            }
            OnConfigChanged?.Invoke("LineNotices");
        }
        
        
        
        
        public List<string> GetLineNotices() => _lineNotices.ToList();
        #endregion

        #region 统计信息
        public GameMap? GetMap(int mapId)
        {
            
            
            
            return null;
        }

        public GameMap[] GetAllMaps()
        {
            
            
            
            return Array.Empty<GameMap>();
        }

        public int GetMapCount() => 0;

        public long GetUpdateCount() => _updateCount;

        public TimeSpan GetUptime() => DateTime.Now - _startTime;
        #endregion

        #region 用户魔法管理
        
        
        
        
        public UserMagic AllocUserMagic()
        {
            return new UserMagic();
        }

        
        
        
        
        public void FreeUserMagic(UserMagic userMagic)
        {
            
            
            if (userMagic != null)
            {
                
                userMagic.Next = null;
                userMagic.Class = null;
            }
        }

        
        
        
        
        public ObjectProcess AllocProcess(string? stringParam = null)
        {
            var process = new ObjectProcess(ProcessType.None);
            if (!string.IsNullOrEmpty(stringParam))
            {
                
                
            }
            return process;
        }

        
        
        
        
        public void FreeProcess(ObjectProcess process)
        {
            
        }
        #endregion

        #region 怪物更新和全局进程管理
        
        
        
        
        public void StartMonsterUpdateThread()
        {
            if (_monsterUpdateTimer == null)
            {
                _monsterUpdateTimer = new Timer(ThdUpdateMonsterCallback, null, 0, 1); 
                LogManager.Default.Info("怪物更新线程已启动");
            }
        }

        
        
        
        public void StopMonsterUpdateThread()
        {
            _monsterUpdateTimer?.Dispose();
            _monsterUpdateTimer = null;
            LogManager.Default.Info("怪物更新线程已停止");
        }

        
        
        
        
        private void ThdUpdateMonsterCallback(object? state)
        {
            try
            {
                _loopCount++;
                if (_loopCount >= UPDATE_LOOP)
                    _loopCount = 0;

                
                int count = _updateMonsterQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    if (_updateMonsterQueue.TryDequeue(out var monster))
                    {
                        if (monster != null && !monster.IsDeath())
                        {
                            monster.Update();
                            
                            _updateMonsterQueue.Enqueue(monster);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("怪物更新线程异常", exception: ex);
            }
        }

        
        
        
        
        public void AddUpdateMonster(MonsterEx monster)
        {
            if (monster == null || monster.IsDeath())
                return;

            _updateMonsterQueue.Enqueue(monster);
        }

        
        
        
        
        public void RemoveUpdateMonster(MonsterEx monster)
        {
            
            
        }

        
        
        
        
        public bool AddGlobeProcess(GlobeProcess process)
        {
            if (process == null)
                return false;

            _globeProcessQueue.Enqueue(process);
            return true;
        }

        
        
        
        
        public GlobeProcess? GetGlobeProcess()
        {
            if (_globeProcessQueue.TryDequeue(out var process))
                return process;
            return null;
        }

        
        
        
        
        public void StartDBUpdateTimer()
        {
            if (_dbUpdateTimer == null)
            {
                _dbUpdateTimer = new Timer(DBUpdateCallback, null, 2000, 2000); 
                LogManager.Default.Info("数据库更新定时器已启动");
            }
        }

        
        
        
        public void StopDBUpdateTimer()
        {
            _dbUpdateTimer?.Dispose();
            _dbUpdateTimer = null;
            LogManager.Default.Info("数据库更新定时器已停止");
        }

        
        
        
        private void DBUpdateCallback(object? state)
        {
            try
            {
                
                
                
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("数据库更新定时器异常", exception: ex);
            }
        }

        
        
        
        
        public uint GetLoopCount() => _loopCount;

        
        
        
        public int GetUpdateMonsterCount() => _updateMonsterQueue.Count;

        
        
        
        private void ProcessGlobeProcessQueue()
        {
            try
            {
                
                int count = _globeProcessQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    if (_globeProcessQueue.TryDequeue(out var process))
                    {
                        if (process != null && process.ShouldExecute())
                        {
                            
                            ExecuteGlobeProcess(process);
                            
                            
                            if (process.RepeatTimes > 0)
                            {
                                process.RepeatTimes--;
                                process.ExecuteTime = DateTime.Now.AddMilliseconds(process.Delay);
                                _globeProcessQueue.Enqueue(process);
                            }
                        }
                        else if (process != null)
                        {
                            
                            _globeProcessQueue.Enqueue(process);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("处理全局进程队列异常", exception: ex);
            }
        }

        
        
        
        private void ExecuteGlobeProcess(GlobeProcess process)
        {
            try
            {
                switch (process.Type)
                {
                    case GlobeProcessType.TimeSystemUpdate:
                        
                        HandleTimeSystemUpdate(process);
                        break;
                    case GlobeProcessType.EventManagerUpdate:
                         EventManager.Instance?.Update();
                        LogManager.Default.Debug($"事件管理器更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.AutoScriptUpdate: 
                        AutoScriptManager.Instance?.Update();
                        LogManager.Default.Debug($"自动脚本更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.MapScriptUpdate: 
                        MapScriptManager.Instance?.Update();
                        LogManager.Default.Debug($"地图脚本更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.TopManagerUpdate:
                        TopManager.Instance?.Update();
                        LogManager.Default.Debug($"排行榜更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.MarketManagerUpdate:
                        MarketManager.Instance?.Update();
                        LogManager.Default.Debug($"市场管理器更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.SpecialEquipmentUpdate:	
                        SpecialEquipmentManager.Instance?.Update();
                        LogManager.Default.Debug($"特殊装备更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.TitleManagerUpdate:
                        TitleManager.Instance?.Update();
                        LogManager.Default.Debug($"称号管理器更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.TaskManagerUpdate:
                        TaskManager.Instance?.Update();
                        LogManager.Default.Debug($"任务管理器更新进程: {process.Type}");
                        break;
                    default:
                        
                        LogManager.Default.Debug($"执行全局进程: {process.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"执行全局进程异常: {process.Type}", exception: ex);
            }
        }

        
        
        
        private void HandleTimeSystemUpdate(GlobeProcess process)
        {
            
            
            LogManager.Default.Debug($"时间系统更新: 游戏时间={process.Param1}");
            
            
            
        }

        
        
        
        public int GetGlobeProcessCount() => _globeProcessQueue.Count;
        #endregion

        #region 配置热重载
        
        
        
        public void ReloadConfig(string configType)
        {
            try
            {
                LogManager.Default.Info($"重新加载配置: {configType}");
                
                
                OnConfigChanged?.Invoke($"Reload_{configType}");
                
                
                
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"重新加载配置失败: {configType}", exception: ex);
            }
        }
        #endregion
    }


    
    
    
    public static class GameName
    {
        public const string GoldName = "goldname";
        public const string MaleName = "malename";
        public const string FemaleName = "femalename";
        public const string GuildNotice = "GUILDNOTICE";
        public const string KillGuilds = "KILLGUILDS";
        public const string AllyGuilds = "ALLYGUILDS";
        public const string Members = "MEMBERS";
        public const string Version = "version";
        public const string WarrName = "warr";
        public const string MagicanName = "magican";
        public const string TaoshiName = "taoshi";
        public const string TopOfWorld = "topofworld";
        public const string UpgradeMineStone = "upgrademinestone";
        public const string LoginScript = "loginscript";
        public const string LevelUpScript = "levelupscript";
        public const string LogoutScript = "logoutscript";
        public const string PhysicsMapPath = "PHYSICSMAPPATH";
        public const string PhysicsCachePath = "PHYSICSCACHEPATH";
    }

    
    
    
    public static class ChatWaitChannel
    {
        public const int Normal = 0;
        public const int Cry = 1;
        public const int Whisper = 2;
        public const int Group = 3;
        public const int Guild = 4;
        public const int Couple = 5;
        public const int GM = 6;
        public const int Friend = 7;
    }
}
