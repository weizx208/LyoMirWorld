using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    public class ConfigLoader
    {
        private static ConfigLoader? _instance;
        public static ConfigLoader Instance => _instance ??= new ConfigLoader();

        private static readonly string DATA_PATH = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data"));
        private static readonly string MAPS_PATH = Path.Combine(DATA_PATH, "maps");
        private static readonly string SCRIPT_PATH = Path.Combine(DATA_PATH, "script");
        private static readonly string MARKET_PATH = Path.Combine(DATA_PATH, "Market");
        private static readonly string GAMEMASTER_PATH = Path.Combine(DATA_PATH, "GameMaster");
        private static readonly string GUILD_PATH = Path.Combine(DATA_PATH, "guildbase", "guilds");
        private static readonly string FIGURE_PATH = Path.Combine(DATA_PATH, "figure");
        private static readonly string VARIABLES_PATH = Path.Combine(DATA_PATH, "Variables");
        private static readonly string STRINGLIST_PATH = Path.Combine(DATA_PATH, "stringlist");
        private static readonly string MONITEMS_PATH = Path.Combine(DATA_PATH, "MonItems");
        private static readonly string MONGENS_PATH = Path.Combine(DATA_PATH, "MonGens");
        private static readonly string TASK_PATH = Path.Combine(DATA_PATH, "task");

        private readonly Dictionary<int, Dictionary<int, HumanDataDesc>> _humanDataDescs = new();
        private readonly Dictionary<int, StartPoint> _startPoints = new();
        private readonly Dictionary<string, int> _startPointNameToIndex = new();
        private readonly List<FirstLoginInfo> _firstLoginInfos = new();
        private readonly Dictionary<string, string> _gameNames = new();
        private readonly Dictionary<int, float> _gameVars = new();
        private readonly Dictionary<int, int> _channelWaitTimes = new();
        
        private readonly Parsers.ItemDataParser _itemDataParser = new();
        private readonly Parsers.MagicDataParser _magicDataParser = new();
        private readonly Parsers.NpcConfigParser _npcConfigParser = new();
        private readonly Parsers.MonsterDataParser _monsterDataParser = new();

        private readonly MarketManager _marketManager = MarketManager.Instance;
        private readonly AutoScriptManager _autoScriptManager = AutoScriptManager.Instance;
        private readonly TitleManager _titleManager = TitleManager.Instance;
        private readonly TopManager _topManager = TopManager.Instance;
        private readonly TaskManager _taskManager = TaskManager.Instance;
        
        private readonly PhysicsMapMgr _physicsMapMgr = PhysicsMapMgr.Instance;
        private readonly MagicManager _magicManager = MagicManager.Instance;
        private readonly NpcManagerEx _npcManagerEx = NpcManagerEx.Instance;
        private readonly MonsterManagerEx _monsterManagerEx = MonsterManagerEx.Instance;
        private readonly ScriptObjectMgr _scriptObjectMgr = ScriptObjectMgr.Instance;
        private readonly MonsterGenManager _monsterGenManager = MonsterGenManager.Instance;
        private readonly MonItemsMgr _monItemsMgr = MonItemsMgr.Instance;
        private readonly SpecialEquipmentManager _specialEquipmentManager = SpecialEquipmentManager.Instance;
        
        private string _notice = string.Empty;
        private readonly List<string> _lineNotices = new();

        private ConfigLoader() { }

        public bool LoadAllConfigs()
        {
            return LoadAllConfigsAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> LoadAllConfigsAsync()
        {
            try
            {
                LogManager.Default.Info("开始加载游戏配置文件（异步）...");
                await Task.Run(() => LoadScriptSystemOptimized());

                if (!LoadServerConfig())
                {
                    LogManager.Default.Error("加载服务器配置失败");
                    return false;
                }

                await Task.Run(() => LoadMapSystem());

                await Task.Run(() => LoadMapRelatedConfigs());

                await Task.Run(() => LoadItemSystem());

                await Task.Run(() => LoadMagicSystem());

                await Task.Run(() => LoadTitles());

                await Task.Run(() => LoadBundleItem());

                await Task.Run(() => LoadNpcConfigs());

                await Task.Run(() => LoadGMConfigs());

                await Task.Run(() => LoadGuildConfigs());

                await Task.Run(() => LoadMonItemsMgr());

                await Task.Run(() => LoadMonsterManagerEx());

                await Task.Run(() => LoadMonsterGenManager());

                await Task.Run(() => LoadHumanPlayerMgr());

                await Task.Run(() => _monsterGenManager.InitAllGen());

                InitSpecialProtocol();

                await LoadSandCityAsync();

                await Task.Run(() => LoadTopList());

                await Task.Run(() => LoadSpecialItem());

                await Task.Run(() => LoadMineList());

                await Task.Run(() => LoadMarket());

                await Task.Run(() => LoadAutoScript());

                await Task.Run(() => LoadMapScript());

                await Task.Run(() => LoadTasks());

                await Task.Run(() => EnsureHumanDataDescsLoaded());

                await Task.Run(() => EnsureStartPointsLoaded());

                await Task.Run(() => EnsureFirstLoginInfoLoaded());

                LogManager.Default.Info("所有配置文件加载完成");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("加载配置文件时发生错误", exception: ex);
                return false;
            }
        }

        private void LoadMapRelatedConfigs()
        {
            LoadSafeArea();
            LoadStartPoint();
            LoadNotice();
        }

        private bool LoadServerConfig()
        {
            LogManager.Default.Info("加载服务器配置...");
            
            try
            {
                string serverConfigFile = Path.Combine(DATA_PATH, "server.txt");
                if (!File.Exists(serverConfigFile))
                {
                    LogManager.Default.Error($"服务器配置文件不存在: {serverConfigFile}");
                    return false;
                }

                var iniFile = new IniFile(serverConfigFile);
                
                float expFactor = iniFile.GetInteger("setting", "expfactor", 100) / 100.0f;
                GameWorld.Instance.SetExpFactor(expFactor);
                
                LoadSpeedConfigFromIni(iniFile);
                
                LoadNameConfigFromIni(iniFile);
                
                LoadVarConfigFromIni(iniFile);
                
                LoadChatWaitConfigFromIni(iniFile);
                
                LoadHumanDataDescsFromIni(iniFile);
                
                LoadFirstLoginInfoFromIni(iniFile);
                
                LogManager.Default.Info("服务器配置加载完成");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载服务器配置失败", exception: ex);
                return false;
            }
        }

        private void LoadSpeedConfigFromIni(IniFile iniFile)
        {
            int walkSpeed = iniFile.GetInteger("speed", "walk", 600);
            int runSpeed = iniFile.GetInteger("speed", "run", 300);
            int attackSpeed = iniFile.GetInteger("speed", "attack", 800);
            int beAttackSpeed = iniFile.GetInteger("speed", "beattack", 800);
            int spellSkillSpeed = iniFile.GetInteger("speed", "spellskill", 800);

            SetGameVar(GameVarConstants.WalkSpeed, walkSpeed);
            SetGameVar(GameVarConstants.RunSpeed, runSpeed);
            SetGameVar(GameVarConstants.AttackSpeed, attackSpeed);
            SetGameVar(GameVarConstants.BeAttackSpeed, beAttackSpeed);
            SetGameVar(GameVarConstants.SpellSkillSpeed, spellSkillSpeed);

            LogManager.Default.Debug($"速度配置 - 行走:{walkSpeed} 跑步:{runSpeed} 攻击:{attackSpeed}");
        }

        private void LoadNameConfigFromIni(IniFile iniFile)
        {
            string goldName = iniFile.GetString("name", "goldname", "金币");
            string maleName = iniFile.GetString("name", "malename", "男");
            string femaleName = iniFile.GetString("name", "femalename", "女");
            string warrName = iniFile.GetString("name", "warr", "战士");
            string magicanName = iniFile.GetString("name", "magican", "法师");
            string taoshiName = iniFile.GetString("name", "taoshi", "道士");
            string guildNotice = iniFile.GetString("name", "GUILDNOTICE", "公告");
            string killGuilds = iniFile.GetString("name", "KILLGUILDS", "敌对行会");
            string allyGuilds = iniFile.GetString("name", "ALLYGUILDS", "联盟行会");
            string members = iniFile.GetString("name", "MEMBERS", "行会成员");
            string version = iniFile.GetString("name", "version", "1, 8, 8, 8");
            string topOfWorld = iniFile.GetString("name", "topofworld", "天下第一");
            string upgradeMineStone = iniFile.GetString("name", "upgrademinestone", "黑铁矿石");
            string loginScript = iniFile.GetString("name", "loginscript", "system.login");
            string levelupScript = iniFile.GetString("name", "levelupscript", "system.levelup");
            string logoutScript = iniFile.GetString("name", "logoutscript", "system.logout");
            string physicsMapPath = iniFile.GetString("name", "PHYSICSMAPPATH", "./data/maps/physics");
            string physicsCachePath = iniFile.GetString("name", "PHYSICSCACHEPATH", "./data/maps/pm_cache");

            SetGameName(GameName.GoldName, goldName);
            SetGameName(GameName.MaleName, maleName);
            SetGameName(GameName.FemaleName, femaleName);
            SetGameName(GameName.WarrName, warrName);
            SetGameName(GameName.MagicanName, magicanName);
            SetGameName(GameName.TaoshiName, taoshiName);
            SetGameName(GameName.GuildNotice, guildNotice);
            SetGameName(GameName.KillGuilds, killGuilds);
            SetGameName(GameName.AllyGuilds, allyGuilds);
            SetGameName(GameName.Members, members);
            SetGameName(GameName.Version, version);
            SetGameName(GameName.TopOfWorld, topOfWorld);
            SetGameName(GameName.UpgradeMineStone, upgradeMineStone);
            SetGameName(GameName.LoginScript, loginScript);
            SetGameName(GameName.LevelUpScript, levelupScript);
            SetGameName(GameName.LogoutScript, logoutScript);
            SetGameName(GameName.PhysicsMapPath, physicsMapPath);
            SetGameName(GameName.PhysicsCachePath, physicsCachePath);

            LogManager.Default.Debug($"职业名称 - 战士:{warrName} 法师:{magicanName} 道士:{taoshiName}");
        }

        private void LoadVarConfigFromIni(IniFile iniFile)
        {
            int maxGold = iniFile.GetInteger("var", "maxgold", 5000000);
            int maxYuanbao = iniFile.GetInteger("var", "maxyuanbao", 2000);
            int maxGroupMember = iniFile.GetInteger("var", "maxgroupmember", 10);
            int redPkPoint = iniFile.GetInteger("var", "redpkpoint", 12);
            int yellowPkPoint = iniFile.GetInteger("var", "yellowpkpoint", 6);
            int storageSize = iniFile.GetInteger("var", "storeagesize", 39);
            int charInfoBackupTime = iniFile.GetInteger("var", "charinfobackuptime", 30);
            int onePkPointTime = iniFile.GetInteger("var", "onepkpointtime", 120);
            int grayNameTime = iniFile.GetInteger("var", "graynametime", 60);
            int oncePkPoint = iniFile.GetInteger("var", "oncepkpoint", 3);
            int pkCurseRate = iniFile.GetInteger("var", "pkcurserate", 50);
            int addFriendLevel = iniFile.GetInteger("var", "ADDFRIENDLEVEL", 7);
            bool enableSafeAreaNotice = iniFile.GetInteger("var", "ENABLESAFEAREANOTICE", 0) != 0;
            int privateShopLevel = iniFile.GetInteger("var", "PRIVATESHOPLEVEL", 20);
            int initDressColor = iniFile.GetInteger("var", "INITDRESSCOLOR", -1);
            int repairDamageDura = iniFile.GetInteger("var", "REPAIRDAMAGEDURA", 1000);
            int dropTargetDistance = iniFile.GetInteger("var", "DROPTARGETDISTANCE", 14);
            int weaponDamageRate = iniFile.GetInteger("var", "WEAPONDAMAGERATE", 15);
            int dressDamageRate = iniFile.GetInteger("var", "DRESSDAMAGERATE", 15);
            int defenceDamageRate = iniFile.GetInteger("var", "DEFENCEDAMAGERATE", 15);
            int jewelryDamageRate = iniFile.GetInteger("var", "JEWELRYDAMAGERATE", 15);
            int randomUpgradeItemRate = iniFile.GetInteger("var", "RANDOMUPGRADEITEMRATE", 16);
            int pushedDelay = iniFile.GetInteger("var", "PUSHEDDELAY", 1200);
            int pushedHitDelay = iniFile.GetInteger("var", "PUSHEDHITDELAY", 1200);
            int dbUpdateDelay = iniFile.GetInteger("var", "DBUPDATEDELAY", 2000);
            int maxUpgradeTimes = iniFile.GetInteger("var", "MAXUPGRADETIMES", 10);
            int rushGridDelay = iniFile.GetInteger("var", "RUSHGRIDDELAY", 400);
            int monGenFactor = iniFile.GetInteger("var", "MONGENFACTOR", 100);
            int hpRecoverPoint = iniFile.GetInteger("var", "HPRECOVERPOINT", 16);
            int hpRecoverTime = iniFile.GetInteger("var", "HPRECOVERTIME", 1000);
            int mpRecoverPoint = iniFile.GetInteger("var", "MPRECOVERPOINT", 16);
            int mpRecoverTime = iniFile.GetInteger("var", "MPRECOVERTIME", 1000);
            int guildWarTime = iniFile.GetInteger("var", "GUILDWARTIME", 3600 * 3);
            int startGuildMemberCount = iniFile.GetInteger("var", "STARTGUILDMEMBERCOUNT", 64);
            int dropTargetTime = iniFile.GetInteger("var", "DROPTARGETTIME", 30);
            int sandCityTakeTime = iniFile.GetInteger("var", "sandcitytaketime", 300);
            int warEnemyColor = iniFile.GetInteger("var", "WARENEMYCOLOR", 243);
            int warAllyColor = iniFile.GetInteger("var", "WARALLYCOLOR", 4);
            int warNormalColor = iniFile.GetInteger("var", "WARNORMALCOLOR", 219);
            int warTimeLong = iniFile.GetInteger("var", "WARTIMELONG", 240);
            int warStartTime = iniFile.GetInteger("var", "warstarttime", 20) % 24;
            int bodyTime = iniFile.GetInteger("var", "bodytime", 60);
            int itemUpdateTime = iniFile.GetInteger("setting", "downitemupdatetime", 300) * 1000;

            SetGameVar(GameVarConstants.MaxGold, maxGold);
            SetGameVar(GameVarConstants.MaxYuanbao, maxYuanbao);
            SetGameVar(GameVarConstants.MaxGroupMember, maxGroupMember);
            SetGameVar(GameVarConstants.RedPkPoint, redPkPoint);
            SetGameVar(GameVarConstants.YellowPkPoint, yellowPkPoint);
            SetGameVar(GameVarConstants.StorageSize, storageSize);
            SetGameVar(GameVarConstants.CharInfoBackupTime, charInfoBackupTime);
            SetGameVar(GameVarConstants.OnePkPointTime, onePkPointTime);
            SetGameVar(GameVarConstants.GrayNameTime, grayNameTime);
            SetGameVar(GameVarConstants.OncePkPoint, oncePkPoint);
            SetGameVar(GameVarConstants.PkCurseRate, pkCurseRate);
            SetGameVar(GameVarConstants.AddFriendLevel, addFriendLevel);
            SetGameVar(GameVarConstants.EnableSafeAreaNotice, enableSafeAreaNotice ? 1 : 0);
            SetGameVar(GameVarConstants.PrivateShopLevel, privateShopLevel);
            SetGameVar(GameVarConstants.InitDressColor, initDressColor);
            SetGameVar(GameVarConstants.RepairDamagedDura, repairDamageDura);
            SetGameVar(GameVarConstants.DropTargetDistance, dropTargetDistance);
            SetGameVar(GameVarConstants.WeaponDamageRate, weaponDamageRate);
            SetGameVar(GameVarConstants.DressDamageRate, dressDamageRate);
            SetGameVar(GameVarConstants.DefenceDamageRate, defenceDamageRate);
            SetGameVar(GameVarConstants.JewelryDamageRate, jewelryDamageRate);
            SetGameVar(GameVarConstants.RandomUpgradeItemRate, randomUpgradeItemRate);
            SetGameVar(GameVarConstants.PushedDelay, pushedDelay);
            SetGameVar(GameVarConstants.PushedHitDelay, pushedHitDelay);
            SetGameVar(GameVarConstants.DBUpdateDelay, dbUpdateDelay);
            SetGameVar(GameVarConstants.MaxUpgradeTimes, maxUpgradeTimes);
            SetGameVar(GameVarConstants.RushGridDelay, rushGridDelay);
            SetGameVar(GameVarConstants.MonGenFactor, monGenFactor);
            SetGameVar(GameVarConstants.HpRecoverPoint, hpRecoverPoint);
            SetGameVar(GameVarConstants.HpRecoverTime, hpRecoverTime);
            SetGameVar(GameVarConstants.MpRecoverPoint, mpRecoverPoint);
            SetGameVar(GameVarConstants.MpRecoverTime, mpRecoverTime);
            SetGameVar(GameVarConstants.GuildWarTime, guildWarTime);
            SetGameVar(GameVarConstants.StartGuildMemberCount, startGuildMemberCount);
            SetGameVar(GameVarConstants.DropTargetTime, dropTargetTime);
            SetGameVar(GameVarConstants.SandCityTakeTime, sandCityTakeTime);
            SetGameVar(GameVarConstants.WarEnemyColor, warEnemyColor);
            SetGameVar(GameVarConstants.WarAllyColor, warAllyColor);
            SetGameVar(GameVarConstants.WarNormalColor, warNormalColor);
            SetGameVar(GameVarConstants.WarTimeLong, warTimeLong);
            SetGameVar(GameVarConstants.WarStartTime, warStartTime);
            SetGameVar(GameVarConstants.BodyTime, bodyTime);
            SetGameVar(GameVarConstants.ItemUpdateTime, itemUpdateTime);

            bool useBigBag = iniFile.GetInteger("setting", "enable60slots", 0) != 0;
            GameWorld.Instance.SetUseBigBag(useBigBag);

            LogManager.Default.Debug($"游戏变量 - 最大金币:{maxGold} 最大元宝:{maxYuanbao} 最大组队人数:{maxGroupMember}");
        }

        private void LoadChatWaitConfigFromIni(IniFile iniFile)
        {
            int normalWait = iniFile.GetInteger("chatwait", "normal", 1);
            int cryWait = iniFile.GetInteger("chatwait", "cry", 10);
            int whisperWait = iniFile.GetInteger("chatwait", "whisper", 2);
            int groupWait = iniFile.GetInteger("chatwait", "group", 2);
            int guildWait = iniFile.GetInteger("chatwait", "guild", 3);
            int coupleWait = iniFile.GetInteger("chatwait", "couple", 1);
            int gmWait = iniFile.GetInteger("chatwait", "gm", 0);
            int friendWait = iniFile.GetInteger("chatwait", "friend", 2);

            SetChannelWaitTime(ChatWaitChannel.Normal, normalWait);
            SetChannelWaitTime(ChatWaitChannel.Cry, cryWait);
            SetChannelWaitTime(ChatWaitChannel.Whisper, whisperWait);
            SetChannelWaitTime(ChatWaitChannel.Group, groupWait);
            SetChannelWaitTime(ChatWaitChannel.Guild, guildWait);
            SetChannelWaitTime(ChatWaitChannel.Couple, coupleWait);
            SetChannelWaitTime(ChatWaitChannel.GM, gmWait);
            SetChannelWaitTime(ChatWaitChannel.Friend, friendWait);

            LogManager.Default.Debug($"聊天等待 - 普通:{normalWait}秒 喊话:{cryWait}秒");
        }

        private void LoadHumanDataDescsFromIni(IniFile iniFile)
        {
            string warriorFile = iniFile.GetString("humandata", "warrior", null);
            string magicianFile = iniFile.GetString("humandata", "magician", null);
            string taoshiFile = iniFile.GetString("humandata", "taoshi", null);

            if (warriorFile.ToLower().StartsWith(".\\data\\"))
            {
                warriorFile = warriorFile.Remove(0, 7);
            }
            if (magicianFile.ToLower().StartsWith(".\\data\\"))
            {
                magicianFile = magicianFile.Remove(0, 7);
            }
            if (taoshiFile.ToLower().StartsWith(".\\data\\"))
            {
                taoshiFile = taoshiFile.Remove(0, 7);
            }

            if (!string.IsNullOrEmpty(warriorFile))
            {
                string warriorPath = Path.Combine(DATA_PATH, warriorFile);
                if (LoadHumanDataDesc(0, warriorPath))
                {
                    LogManager.Default.Info($"战士人物数据描述已加载: {warriorPath}");
                }
                else
                {
                    LogManager.Default.Warning($"无法加载战士人物数据描述: {warriorPath}");
                }
            }

            if (!string.IsNullOrEmpty(magicianFile))
            {
                string magicianPath = Path.Combine(DATA_PATH, magicianFile);
                if (LoadHumanDataDesc(1, magicianPath))
                {
                    LogManager.Default.Info($"法师人物数据描述已加载: {magicianPath}");
                }
                else
                {
                    LogManager.Default.Warning($"无法加载法师人物数据描述: {magicianPath}");
                }
            }

            if (!string.IsNullOrEmpty(taoshiFile))
            {
                string taoshiPath = Path.Combine(DATA_PATH, taoshiFile);
                if (LoadHumanDataDesc(2, taoshiPath))
                {
                    LogManager.Default.Info($"道士人物数据描述已加载: {taoshiPath}");
                }
                else
                {
                    LogManager.Default.Warning($"无法加载道士人物数据描述: {taoshiPath}");
                }
            }
        }

        private void LoadFirstLoginInfoFromIni(IniFile iniFile)
        {
            try
            {
                var firstLoginInfo = new FirstLoginInfo();
                
                firstLoginInfo.Level = iniFile.GetInteger("firstlogin", "startlevel", 1);
                
                firstLoginInfo.Gold = (uint)iniFile.GetInteger("firstlogin", "startgold", 0);
                
                string startItem = iniFile.GetString("firstlogin", "startitem", null);
                if (!string.IsNullOrEmpty(startItem))
                {
                    var itemParts = startItem.Split('/');
                    foreach (var itemPart in itemParts)
                    {
                        var itemDetails = itemPart.Split('*');
                        if (itemDetails.Length >= 1)
                        {
                            var firstLoginItem = new FirstLoginItem
                            {
                                ItemName = itemDetails[0].Trim(),
                                Count = itemDetails.Length >= 2 ? int.Parse(itemDetails[1].Trim()) : 1
                            };
                            firstLoginInfo.Items.Add(firstLoginItem);
                        }
                    }
                }
                
                _firstLoginInfos.Clear();
                _firstLoginInfos.Add(firstLoginInfo);
                
                GameWorld.Instance.SetFirstLoginInfo(firstLoginInfo);
                
                LogManager.Default.Info($"首次登录信息加载完成: 等级={firstLoginInfo.Level}, 金币={firstLoginInfo.Gold}, 物品数={firstLoginInfo.Items.Count}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载首次登录信息失败", exception: ex);
            }
        }

        private void InitSpecialProtocol()
        {
            LogManager.Default.Info("初始化特殊协议...");
            
            try
            {
                LogManager.Default.Info("特殊协议初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化特殊协议失败", exception: ex);
            }
        }

        private async Task LoadSandCityAsync()
        {
            LogManager.Default.Info("加载沙城配置...");
            
            await Task.Run(() =>
            {
                try
                {
                    var sandCity = SandCity.Instance;
                    sandCity.Init();
                    
                    LogManager.Default.Info("沙城配置加载完成");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载沙城配置失败", exception: ex);
                }
            });
        }

        private void LoadMagicSystem()
        {
            LogManager.Default.Info("加载技能系统...");

            LoadMagicManager();

            LoadBaseMagic();

            LoadMagicExt();
        }

        private void EnsureHumanDataDescsLoaded()
        {
            if (_humanDataDescs.Count == 0)
            {
                LogManager.Default.Warning("人物数据描述未加载，尝试重新加载");
                LoadHumanDataDescs();
            }
            else
            {
                LogManager.Default.Debug("人物数据描述已加载");
            }
        }

        private void EnsureStartPointsLoaded()
        {
            if (_startPoints.Count == 0)
            {
                LogManager.Default.Warning("出生点配置未加载，尝试重新加载");
                LoadStartPoints();
            }
            else
            {
                LogManager.Default.Debug("出生点配置已加载");
            }
        }

        private void EnsureFirstLoginInfoLoaded()
        {
            if (_firstLoginInfos.Count == 0)
            {
                LogManager.Default.Warning("首次登录信息未加载，尝试重新加载");
                LoadFirstLoginInfo();
            }
            else
            {
                LogManager.Default.Debug("首次登录信息已加载");
            }
        }

        private void LoadScriptSystemOptimized()
        {
            LogManager.Default.Info("加载脚本系统...");

            bool isLoaded = _scriptObjectMgr.GetScriptObjectCount() > 0;
            if (!isLoaded)
            {
                string scriptPath = SCRIPT_PATH;
                if (Directory.Exists(scriptPath))
                {
                    _scriptObjectMgr.Load(scriptPath);
                    int scriptCount = _scriptObjectMgr.GetScriptObjectCount();
                    LogManager.Default.Info($"成功加载 {scriptCount} 个脚本对象");
                }
                else
                {
                    LogManager.Default.Warning($"脚本目录不存在: {scriptPath}");
                }
            }
            else
            {
                LogManager.Default.Debug("脚本系统已加载，跳过重复加载");
            }

            LoadSystemScript();
        }

        private void LoadSpeedConfig(Dictionary<string, string> config)
        {
            int walkSpeed = GetConfigInt(config, "speed.walk", 600);
            int runSpeed = GetConfigInt(config, "speed.run", 300);
            int attackSpeed = GetConfigInt(config, "speed.attack", 800);
            int beAttackSpeed = GetConfigInt(config, "speed.beattack", 800);
            int spellSkillSpeed = GetConfigInt(config, "speed.spellskill", 800);

            SetGameVar(GameVarConstants.WalkSpeed, walkSpeed);
            SetGameVar(GameVarConstants.RunSpeed, runSpeed);
            SetGameVar(GameVarConstants.AttackSpeed, attackSpeed);
            SetGameVar(GameVarConstants.BeAttackSpeed, beAttackSpeed);
            SetGameVar(GameVarConstants.SpellSkillSpeed, spellSkillSpeed);

            LogManager.Default.Debug($"速度配置 - 行走:{walkSpeed} 跑步:{runSpeed} 攻击:{attackSpeed}");
        }

        private void LoadNameConfig(Dictionary<string, string> config)
        {
            string goldName = GetConfigString(config, "name.goldname", "金币");
            string maleName = GetConfigString(config, "name.malename", "男");
            string femaleName = GetConfigString(config, "name.femalename", "女");
            string warrName = GetConfigString(config, "name.warr", "战士");
            string magicanName = GetConfigString(config, "name.magican", "法师");
            string taoshiName = GetConfigString(config, "name.taoshi", "道士");

            SetGameName(GameName.GoldName, goldName);
            SetGameName(GameName.MaleName, maleName);
            SetGameName(GameName.FemaleName, femaleName);
            SetGameName(GameName.WarrName, warrName);
            SetGameName(GameName.MagicanName, magicanName);
            SetGameName(GameName.TaoshiName, taoshiName);

            LogManager.Default.Debug($"职业名称 - 战士:{warrName} 法师:{magicanName} 道士:{taoshiName}");
        }

        private void LoadVarConfig(Dictionary<string, string> config)
        {
            int maxGold = GetConfigInt(config, "var.maxgold", 5000000);
            int maxYuanbao = GetConfigInt(config, "var.maxyuanbao", 2000);
            int maxGroupMember = GetConfigInt(config, "var.maxgroupmember", 10);
            int redPkPoint = GetConfigInt(config, "var.redpkpoint", 12);
            int yellowPkPoint = GetConfigInt(config, "var.yellowpkpoint", 6);
            int storageSize = GetConfigInt(config, "var.storagesize", 100);
            int charInfoBackupTime = GetConfigInt(config, "var.charinfobackuptime", 5);
            int onePkPointTime = GetConfigInt(config, "var.onepkpointtime", 60);
            int grayNameTime = GetConfigInt(config, "var.graynametime", 300);
            int oncePkPoint = GetConfigInt(config, "var.oncepkpoint", 1);
            int pkCurseRate = GetConfigInt(config, "var.pkcurserate", 10);
            int addFriendLevel = GetConfigInt(config, "var.addfriendlevel", 30);
            bool enableSafeAreaNotice = GetConfigBool(config, "var.enablesafeareanotice", true);

            SetGameVar(GameVarConstants.MaxGold, maxGold);
            SetGameVar(GameVarConstants.MaxYuanbao, maxYuanbao);
            SetGameVar(GameVarConstants.MaxGroupMember, maxGroupMember);
            SetGameVar(GameVarConstants.RedPkPoint, redPkPoint);
            SetGameVar(GameVarConstants.YellowPkPoint, yellowPkPoint);
            SetGameVar(GameVarConstants.StorageSize, storageSize);
            SetGameVar(GameVarConstants.CharInfoBackupTime, charInfoBackupTime);
            SetGameVar(GameVarConstants.OnePkPointTime, onePkPointTime);
            SetGameVar(GameVarConstants.GrayNameTime, grayNameTime);
            SetGameVar(GameVarConstants.OncePkPoint, oncePkPoint);
            SetGameVar(GameVarConstants.PkCurseRate, pkCurseRate);
            SetGameVar(GameVarConstants.AddFriendLevel, addFriendLevel);
            SetGameVar(GameVarConstants.EnableSafeAreaNotice, enableSafeAreaNotice ? 1 : 0);

            LogManager.Default.Debug($"游戏变量 - 最大金币:{maxGold} 最大元宝:{maxYuanbao} 最大组队人数:{maxGroupMember}");
        }

        private void LoadChatWaitConfig(Dictionary<string, string> config)
        {
            int normalWait = GetConfigInt(config, "chatwait.normal", 1);
            int cryWait = GetConfigInt(config, "chatwait.cry", 10);
            int whisperWait = GetConfigInt(config, "chatwait.whisper", 2);
            int groupWait = GetConfigInt(config, "chatwait.group", 2);
            int guildWait = GetConfigInt(config, "chatwait.guild", 3);

            SetChannelWaitTime(ChatWaitChannel.Normal, normalWait);
            SetChannelWaitTime(ChatWaitChannel.Cry, cryWait);
            SetChannelWaitTime(ChatWaitChannel.Whisper, whisperWait);
            SetChannelWaitTime(ChatWaitChannel.Group, groupWait);
            SetChannelWaitTime(ChatWaitChannel.Guild, guildWait);

            LogManager.Default.Debug($"聊天等待 - 普通:{normalWait}秒 喊话:{cryWait}秒");
        }

        private void LoadScriptSystem()
        {
            LogManager.Default.Info("加载脚本系统...");

            LoadScriptObjectMgr();

            string scriptPath = SCRIPT_PATH;
            if (Directory.Exists(scriptPath))
            {
                LogManager.Default.Info($"加载脚本目录: {scriptPath}");
                LoadScriptObjects(scriptPath);
            }

            string varsPath = VARIABLES_PATH;
            if (Directory.Exists(varsPath))
            {
                LogManager.Default.Info($"加载脚本变量目录: {varsPath}");
                LoadScriptVariables(varsPath);
            }

            string stringListPath = STRINGLIST_PATH;
            if (Directory.Exists(stringListPath))
            {
                LogManager.Default.Info($"加载字符串列表: {stringListPath}");
                LoadStringList(stringListPath);
            }
        }

        private void LoadScriptObjects(string scriptPath)
        {
            try
            {
                var parser = new Parsers.SimpleScriptParser();
                var scriptFiles = Directory.GetFiles(scriptPath, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in scriptFiles)
                {
                    var scriptLines = parser.Parse(file);
                    if (scriptLines.Count > 0)
                    {
                        loadedCount++;
                        LogManager.Default.Debug($"加载脚本文件: {Path.GetFileName(file)} ({scriptLines.Count} 行)");
                    }
                }

                LogManager.Default.Info($"加载了 {loadedCount} 个脚本文件");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载脚本对象失败: {scriptPath}", exception: ex);
            }
        }

        private void LoadScriptVariables(string varsPath)
        {
            try
            {
                var parser = new Parsers.TextFileParser();
                var varFiles = Directory.GetFiles(varsPath, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in varFiles)
                {
                    var variables = parser.LoadKeyValue(file);
                    if (variables.Count > 0)
                    {
                        loadedCount++;
                        LogManager.Default.Debug($"加载变量文件: {Path.GetFileName(file)} ({variables.Count} 个变量)");
                    }
                }

                LogManager.Default.Info($"加载了 {loadedCount} 个变量文件");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载脚本变量失败: {varsPath}", exception: ex);
            }
        }

        private void LoadStringList(string stringListPath)
        {
            try
            {
                var parser = new Parsers.TextFileParser();
                var stringFiles = Directory.GetFiles(stringListPath, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in stringFiles)
                {
                    var strings = parser.LoadLines(file);
                    if (strings.Count > 0)
                    {
                        loadedCount++;
                        LogManager.Default.Debug($"加载字符串文件: {Path.GetFileName(file)} ({strings.Count} 个字符串)");
                    }
                }

                LogManager.Default.Info($"加载了 {loadedCount} 个字符串文件");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载字符串列表失败: {stringListPath}", exception: ex);
            }
        }

        private void LoadMapConfigs()
        {
            LogManager.Default.Info("加载地图配置...");

            LoadPhysicsMaps();

            LoadLogicMaps();

            LoadSafeArea();

            LoadStartPoint();

            LoadMapScript();
        }

        private void LoadPhysicsMaps()
        {
            string physicsPath = Path.Combine(MAPS_PATH, "physics");
            string cachePath = Path.Combine(MAPS_PATH, "pm_cache");

            if (Directory.Exists(physicsPath))
            {
                LogManager.Default.Info($"物理地图路径: {physicsPath}");
                LogManager.Default.Info($"物理地图缓存路径: {cachePath}");
                
                try
                {
                    _physicsMapMgr.Init(physicsPath, cachePath);
                    int mapCount = _physicsMapMgr.LoadedMapCount;
                    LogManager.Default.Info($"成功加载 {mapCount} 个物理地图");
                    
                    var mapParser = new Parsers.MapFileParser();
                    var mapFiles = Directory.GetFiles(physicsPath, "*.map", SearchOption.AllDirectories);
                    int oldLoadedCount = 0;
                    
                    foreach (var mapFile in mapFiles)
                    {
                        string cacheFile = Path.Combine(cachePath, Path.GetFileNameWithoutExtension(mapFile) + ".pmc");
                        Parsers.MapFileParser.MapData? mapData = null;
                        
                        if (File.Exists(cacheFile))
                        {
                            mapData = mapParser.LoadMapCache(cacheFile);
                        }
                        
                        if (mapData == null)
                        {
                            mapData = mapParser.LoadMapFile(mapFile);
                            if (mapData != null)
                            {
                                mapParser.SaveMapCache(mapData, cacheFile);
                            }
                        }
                        
                        if (mapData != null)
                        {
                            oldLoadedCount++;
                            LogManager.Default.Debug($"旧解析器加载物理地图: {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                        }
                    }
                    
                    LogManager.Default.Debug($"旧解析器加载了 {oldLoadedCount} 个物理地图");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载物理地图失败: {physicsPath}", exception: ex);
                }
            }
        }

        private void LoadLogicMaps()
        {
            string logicPath = Path.Combine(MAPS_PATH, "logic");

            if (Directory.Exists(logicPath))
            {
                LogManager.Default.Info($"加载逻辑地图: {logicPath}");
                try
                {
                    LogicMapMgr.Instance.Load(logicPath);
                    int mapCount = LogicMapMgr.Instance.GetMapCount();
                    LogManager.Default.Info($"成功加载 {mapCount} 个逻辑地图配置");
                    
                    var logicParser = new Parsers.LogicMapConfigParser();
                    if (logicParser.LoadMapConfigs(logicPath))
                    {
                        int oldMapCount = 0;
                        foreach (var map in logicParser.GetAllMaps())
                        {
                            oldMapCount++;
                            LogManager.Default.Debug($"逻辑地图配置: ID={map.MapID}, 名称={map.MapName}");
                        }
                        LogManager.Default.Debug($"旧解析器加载了 {oldMapCount} 个逻辑地图配置");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载逻辑地图失败: {logicPath}", exception: ex);
                }
            }
        }

        private void LoadSafeArea()
        {
            string safeAreaFile = Path.Combine(DATA_PATH, "safearea.csv");

            if (File.Exists(safeAreaFile))
            {
                LogManager.Default.Info($"加载安全区配置: {safeAreaFile}");
                try
                {
                    var lines = SmartReader.ReadAllLines(safeAreaFile);
                    int count = 0;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        var parts = line.Split(',');
                        if (parts.Length >= 4)
                        {
                            if (int.TryParse(parts[0], out int mapId) &&
                                int.TryParse(parts[1], out int centerX) &&
                                int.TryParse(parts[2], out int centerY) &&
                                int.TryParse(parts[3], out int radius))
                            {
                                count++;
                            }
                        }
                    }
                    LogManager.Default.Info($"加载了 {count} 个安全区配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载安全区配置失败: {safeAreaFile}", exception: ex);
                }
            }
        }

        private void LoadStartPoint()
        {
            string startPointFile = Path.Combine(DATA_PATH, "startpoint.csv");

            if (File.Exists(startPointFile))
            {
                LogManager.Default.Info($"加载出生点配置: {startPointFile}");
                try
                {
                    var lines = SmartReader.ReadAllLines(startPointFile);
                    int count = 0;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        var parts = line.Split(',');
                        if (parts.Length >= 7)
                        {
                            string name = parts[0].Trim();
                            if (int.TryParse(parts[1], out int mapId) &&
                                int.TryParse(parts[2], out int x) &&
                                int.TryParse(parts[3], out int y) &&
                                int.TryParse(parts[4], out int range))
                            {
                                count++;
                            }
                        }
                    }
                    LogManager.Default.Info($"加载了 {count} 个出生点配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载出生点配置失败: {startPointFile}", exception: ex);
                }
            }
        }

        private void LoadMapScript()
        {
            string mapScriptFile = Path.Combine(DATA_PATH, "mapscript.txt");

            if (File.Exists(mapScriptFile))
            {
                LogManager.Default.Info($"加载地图脚本: {mapScriptFile}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var lines = parser.LoadLines(mapScriptFile);
                    int count = 0;
                    
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;
                        
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string mapName = parts[0].Trim();
                            string scriptName = parts[1].Trim();
                            
                            LogManager.Default.Debug($"地图脚本: {mapName} -> {scriptName}");
                            count++;
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个地图脚本配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载地图脚本失败: {mapScriptFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"地图脚本文件不存在: {mapScriptFile}");
            }
        }

        private void LoadItemConfigs()
        {
            LogManager.Default.Info("加载物品配置...");

            LoadBaseItem();

            LoadItemLimit();

            LoadItemScript();

            LoadBundleItem();

            LoadSpecialItem();
        }

        private void LoadBaseItem()
        {
            string itemFile = Path.Combine(DATA_PATH, "baseitem.txt");

            if (File.Exists(itemFile))
            {
                LogManager.Default.Info($"加载基础物品: {itemFile}");
                
                if (ItemManager.Instance.Load(itemFile))
                {
                    LogManager.Default.Info("基础物品数据加载成功");
                }
                else
                {
                    LogManager.Default.Warning("加载基础物品数据失败");
                }
                
                if (_itemDataParser.Load(itemFile))
                {
                    LogManager.Default.Debug("ItemDataParser加载物品数据成功");
                }
            }
            else
            {
                LogManager.Default.Warning($"基础物品文件不存在: {itemFile}");
            }
        }

        private void LoadItemLimit()
        {
            string limitFile = Path.Combine(DATA_PATH, "itemlimit.txt");

            if (File.Exists(limitFile))
            {
                LogManager.Default.Info($"加载物品限制: {limitFile}");
                if (!ItemManager.Instance.LoadLimit(limitFile))
                    LogManager.Default.Warning("加载物品限制失败");
            }
            else
            {
                LogManager.Default.Warning($"物品限制文件不存在: {limitFile}");
            }
        }

        private void LoadItemScript()
        {
            string scriptFile = Path.Combine(DATA_PATH, "itemscript.txt");

            if (File.Exists(scriptFile))
            {
                LogManager.Default.Info($"加载物品脚本链接: {scriptFile}");
                if (!ItemManager.Instance.LoadScriptLink(scriptFile))
                    LogManager.Default.Warning("加载物品脚本链接失败");
            }
            else
            {
                LogManager.Default.Warning($"物品脚本链接文件不存在: {scriptFile}");
            }
        }

        private void LoadBundleItem()
        {
            string bundleFile = Path.Combine(DATA_PATH, "bundleitem.csv");

            if (File.Exists(bundleFile))
            {
                LogManager.Default.Info($"加载捆绑物品: {bundleFile}");
                try
                {
                    BundleManager.Instance.LoadBundle(bundleFile, true); 
                    int bundleManagerCount = BundleManager.Instance.GetBundleCount();
                    LogManager.Default.Info($"成功加载 {bundleManagerCount} 个捆绑物品配置");
                    
                    var parser = new Parsers.CSVParser();
                    var bundleData = parser.Parse(bundleFile, false);
                    int oldCount = 0;
                    
                    foreach (var row in bundleData)
                    {
                        if (row.Count >= 3)
                        {
                            string itemName = row["Column0"];
                            string bundleName = row["Column1"];
                            int itemCount = int.Parse(row["Column2"]);

                            oldCount++;
                        }
                    }
                    
                    LogManager.Default.Debug($"旧解析器加载了 {oldCount} 个捆绑物品配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载捆绑物品失败: {bundleFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"捆绑物品文件不存在: {bundleFile}");
            }
        }

        private void LoadSpecialItem()
        {
            string specialFile = Path.Combine(DATA_PATH, "specialitem.txt");

            if (File.Exists(specialFile))
            {
                LogManager.Default.Info($"加载特殊装备: {specialFile}");
                try
                {
                    if (_specialEquipmentManager.LoadSpecialEquipmentFunction(specialFile))
                    {
                        int count = _specialEquipmentManager.GetSpecialEquipmentCount();
                        LogManager.Default.Info($"成功加载 {count} 个特殊装备配置");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载特殊装备配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载特殊装备失败: {specialFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"特殊装备文件不存在: {specialFile}");
            }
        }

        private void LoadMagicConfigs()
        {
            LogManager.Default.Info("加载技能配置...");

            LoadMagicManager();

            LoadBaseMagic();

            LoadMagicExt();
        }

        private void LoadBaseMagic()
        {
            string magicFile = Path.Combine(DATA_PATH, "basemagic.txt");

            if (File.Exists(magicFile))
            {
                LogManager.Default.Info($"加载基础技能: {magicFile}");
                if (_magicDataParser.Load(magicFile))
                {
                    LogManager.Default.Info("基础技能加载成功");
                }
                else
                {
                    LogManager.Default.Warning("加载基础技能失败");
                }
            }
            else
            {
                LogManager.Default.Warning($"基础技能文件不存在: {magicFile}");
            }
        }

        private void LoadMagicExt()
        {
            string magicExtFile = Path.Combine(DATA_PATH, "magicext.csv");

            if (File.Exists(magicExtFile))
            {
                LogManager.Default.Info($"加载技能扩展: {magicExtFile}");
                try
                {
                    var parser = new Parsers.CSVParser();
                    var magicExtData = parser.Parse(magicExtFile, true);
                    int count = 0;
                    
                    foreach (var row in magicExtData)
                    {
                        if (row.ContainsKey("MagicID") && row.ContainsKey("ExtValue"))
                        {
                            string magicId = row["MagicID"];
                            string extValue = row["ExtValue"];
                            
                            LogManager.Default.Debug($"技能扩展: {magicId} -> {extValue}");
                            count++;
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个技能扩展配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载技能扩展失败: {magicExtFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"技能扩展文件不存在: {magicExtFile}");
            }
        }

        private void LoadNpcConfigs()
        {
            LogManager.Default.Info("加载NPC配置...");

            LoadNpcManagerEx();

            LoadNpcGen();
        }

        private void LoadNpcGen()
        {
            string npcGenFile = Path.Combine(DATA_PATH, "npcgen.txt");

            if (File.Exists(npcGenFile))
            {
                LogManager.Default.Info($"加载NPC生成: {npcGenFile}");
                if (_npcConfigParser.Load(npcGenFile))
                {
                    int npcCount = 0;
                    foreach (var npc in _npcConfigParser.GetAllNpcs())
                    {
                        npcCount++;
                    }
                    LogManager.Default.Info($"成功加载 {npcCount} 个NPC配置");
                }
                else
                {
                    LogManager.Default.Warning("加载NPC生成配置失败");
                }
            }
            else
            {
                LogManager.Default.Warning($"NPC生成文件不存在: {npcGenFile}");
            }
        }

        private void LoadMonsterConfigs()
        {
            LogManager.Default.Info("加载怪物配置...");

            LoadBaseMonster();

            LoadMonsterScript();

            LoadMonsterGen();

            LoadMonsterItems();
        }

        private void LoadBaseMonster()
        {
            string monsterFile = Path.Combine(DATA_PATH, "BaseMonsterEx.txt");

            if (File.Exists(monsterFile))
            {
                LogManager.Default.Info($"加载基础怪物: {monsterFile}");
                if (_monsterDataParser.Load(monsterFile))
                {
                    int monsterCount = 0;
                    foreach (var monster in _monsterDataParser.GetAllMonsters())
                    {
                        monsterCount++;
                    }
                    LogManager.Default.Info($"成功加载 {monsterCount} 个怪物数据");
                }
                else
                {
                    LogManager.Default.Warning("加载基础怪物数据失败");
                }
            }
            else
            {
                LogManager.Default.Warning($"基础怪物文件不存在: {monsterFile}");
            }
        }

        private void LoadMonsterScript()
        {
            string scriptFile = Path.Combine(DATA_PATH, "monsterscript.txt");

            if (File.Exists(scriptFile))
            {
                LogManager.Default.Info($"加载怪物脚本: {scriptFile}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var lines = parser.LoadLines(scriptFile);
                    int count = 0;
                    
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string monsterName = parts[0].Trim();
                            string scriptName = parts[1].Trim();
                            
                            LogManager.Default.Debug($"怪物脚本: {monsterName} -> {scriptName}");
                            count++;
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个怪物脚本配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载怪物脚本失败: {scriptFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"怪物脚本文件不存在: {scriptFile}");
            }
        }

        private void LoadMonsterGen()
        {
            string monGenPath = MONGENS_PATH;

            if (Directory.Exists(monGenPath))
            {
                LogManager.Default.Info($"加载怪物生成配置: {monGenPath}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var genFiles = Directory.GetFiles(monGenPath, "*.txt", SearchOption.AllDirectories);
                    int totalCount = 0;
                    
                    foreach (var file in genFiles)
                    {
                        var lines = parser.LoadLines(file);
                        int fileCount = 0;
                        
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                                continue;
                            
                            var parts = line.Split(',');
                            if (parts.Length >= 5)
                            {
                                string monsterName = parts[4].Trim();
                                fileCount++;
                            }
                        }

                        totalCount += fileCount;
                    }
                    
                    LogManager.Default.Info($"加载了 {totalCount} 个怪物生成配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载怪物生成配置失败: {monGenPath}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"怪物生成目录不存在: {monGenPath}");
            }
        }

        private void LoadMonsterItems()
        {
            string monItemsPath = MONITEMS_PATH;

            if (Directory.Exists(monItemsPath))
            {
                LogManager.Default.Info($"加载怪物掉落: {monItemsPath}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var itemFiles = Directory.GetFiles(monItemsPath, "*.txt", SearchOption.AllDirectories);
                    int totalCount = 0;
                    
                    foreach (var file in itemFiles)
                    {
                        var lines = parser.LoadLines(file);
                        int fileCount = 0;
                        
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                                continue;
                            
                            var parts = line.Split(',');
                            if (parts.Length >= 3)
                            {
                                string monsterName = parts[0].Trim();
                                string itemName = parts[1].Trim();
                                float dropRate = float.Parse(parts[2].Trim());
                                
                                fileCount++;
                            }
                        }
                        
                        LogManager.Default.Debug($"怪物掉落文件: {Path.GetFileName(file)} ({fileCount} 个掉落配置)");
                        totalCount += fileCount;
                    }
                    
                    LogManager.Default.Info($"加载了 {totalCount} 个怪物掉落配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载怪物掉落失败: {monItemsPath}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"怪物掉落目录不存在: {monItemsPath}");
            }
        }

        private void LoadGuildConfigs()
        {
            LogManager.Default.Info("加载行会配置...");

            string guildPath = GUILD_PATH;

            if (Directory.Exists(guildPath))
            {
                LogManager.Default.Info($"加载行会数据: {guildPath}");
                try
                {
                    var guildFiles = Directory.GetFiles(guildPath, "*.txt", SearchOption.AllDirectories);
                    int loadedCount = 0;
                    
                    foreach (var file in guildFiles)
                    {
                        var parser = new Parsers.TextFileParser();
                        var lines = parser.LoadLines(file);
                        
                        foreach (var line in lines)
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string guildName = parts[0].Trim();
                                string guildData = parts[1].Trim();
                                loadedCount++;
                                LogManager.Default.Debug($"行会数据: {guildName} -> {guildData}");
                            }
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {loadedCount} 个行会数据");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载行会数据失败: {guildPath}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"行会目录不存在: {guildPath}");
            }
        }

        private void LoadGMConfigs()
        {
            LogManager.Default.Info("加载GM配置...");

            LoadGMList();

            LoadGMCommandDef();
        }

        private void LoadGMList()
        {
            string gmListFile = Path.Combine(DATA_PATH, "gmlist.txt");

            if (File.Exists(gmListFile))
            {
                LogManager.Default.Info($"加载GM列表: {gmListFile}");
                try
                {
                    int count = GmManager.Instance.LoadGmList(gmListFile);
                    LogManager.Default.Info($"加载了 {count} 个GM账号");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载GM列表失败: {gmListFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"GM列表文件不存在: {gmListFile}");
            }
        }

        private void LoadGMCommandDef()
        {
            string cmdListFile = Path.Combine(GAMEMASTER_PATH, "cmdlist.txt");

            if (File.Exists(cmdListFile))
            {
                LogManager.Default.Info($"加载GM命令定义: {cmdListFile}");
                try
                {
                    int count = GmManager.Instance.LoadCommandDef(cmdListFile);
                    LogManager.Default.Info($"加载了 {count} 个GM命令定义");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载GM命令定义失败: {cmdListFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"GM命令定义文件不存在: {cmdListFile}");
            }
        }

        private void LoadNotice()
        {
            string noticeFile = Path.Combine(DATA_PATH, "notice.txt");
            if (File.Exists(noticeFile))
            {
                LogManager.Default.Info($"加载公告: {noticeFile}");
                try
                {
                    _notice = SmartReader.ReadTextFile(noticeFile);
                    LogManager.Default.Info($"公告内容长度: {_notice.Length}");
                    
                    GameWorld.Instance.SetNotice(_notice);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载公告失败: {noticeFile}", exception: ex);
                }
            }

            string lineNoticeFile = Path.Combine(DATA_PATH, "linenotice.txt");
            if (File.Exists(lineNoticeFile))
            {
                LogManager.Default.Info($"加载滚动公告: {lineNoticeFile}");
                try
                {
                    var lines = SmartReader.ReadAllLines(lineNoticeFile);
                    _lineNotices.Clear();
                    
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;
                        _lineNotices.Add(line);
                    }
                    LogManager.Default.Info($"加载了 {_lineNotices.Count} 条滚动公告");
                    
                    GameWorld.Instance.SetLineNotices(_lineNotices);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载滚动公告失败: {lineNoticeFile}", exception: ex);
                }
            }
        }
        
        public string GetNotice() => _notice;
        
        public List<string> GetLineNotices() => _lineNotices;

        private void LoadTitles()
        {
            string titlesFile = Path.Combine(DATA_PATH, "titles.csv");

            if (File.Exists(titlesFile))
            {
                LogManager.Default.Info($"加载称号: {titlesFile}");
                try
                {
                    if (_titleManager.Load(titlesFile))
                    {
                        LogManager.Default.Info($"称号配置加载成功，共 {_titleManager.GetAllTitles().Count()} 个称号");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载称号配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载称号失败: {titlesFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"称号文件不存在: {titlesFile}");
            }
        }

        private void LoadTopList()
        {
            string topNpcFile = Path.Combine(FIGURE_PATH, "topnpc.txt");
            if (File.Exists(topNpcFile))
            {
                LogManager.Default.Info($"加载排行榜NPC: {topNpcFile}");
                try
                {
                    if (_topManager.LoadTopNpcs(topNpcFile))
                    {
                        LogManager.Default.Info($"排行榜NPC配置加载成功，共 {_topManager.GetAllTopNpcs().Count()} 个NPC");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载排行榜NPC配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载排行榜NPC失败: {topNpcFile}", exception: ex);
                }
            }

            string topListFile = Path.Combine(FIGURE_PATH, "toplist.txt");
            if (File.Exists(topListFile))
            {
                LogManager.Default.Info($"加载排行榜数据配置: {topListFile}");
                try
                {
                    if (_topManager.LoadTopListConfig(topListFile))
                    {
                        LogManager.Default.Info("排行榜数据配置加载成功");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载排行榜数据配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载排行榜数据配置失败: {topListFile}", exception: ex);
                }
            }
        }
        private void LoadMineList()
        {
            string mineListFile = Path.Combine(DATA_PATH, "minelist.txt");

            if (File.Exists(mineListFile))
            {
                LogManager.Default.Info($"加载矿石列表: {mineListFile}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var lines = parser.LoadLines(mineListFile);
                    int count = 0;
                    
                    foreach (var line in lines)
                    {
                        var parts = line.Split(',');
                        if (parts.Length >= 5)
                        {
                            if (uint.TryParse(parts[0].Trim(), out uint mapId) &&
                                ushort.TryParse(parts[2].Trim(), out ushort duraMin) &&
                                ushort.TryParse(parts[3].Trim(), out ushort duraMax) &&
                                ushort.TryParse(parts[4].Trim(), out ushort rate))
                            {
                                string oreName = parts[1].Trim();
                                
                                var map = LogicMapMgr.Instance.GetLogicMapById(mapId);
                                if (map != null)
                                {
                                    map.AddMineItem(oreName, duraMin, duraMax, rate);
                                    count++;
                                }
                                else
                                {
                                    LogManager.Default.Warning($"地图 {mapId} 不存在，无法添加矿石: {oreName}");
                                }
                            }
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个矿石配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载矿石列表失败: {mineListFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"矿石列表文件不存在: {mineListFile}");
            }
        }

        private void LoadMarket()
        {
            string scrollTextFile = Path.Combine(MARKET_PATH, "scrolltext.txt");
            if (File.Exists(scrollTextFile))
            {
                LogManager.Default.Info($"加载市场滚动文字: {scrollTextFile}");
                try
                {
                    _marketManager.LoadScrollText(scrollTextFile);
                    LogManager.Default.Info("市场滚动文字加载成功");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载市场滚动文字失败: {scrollTextFile}", exception: ex);
                }
            }

            string mainDirFile = Path.Combine(MARKET_PATH, "MainDir.txt");
            if (File.Exists(mainDirFile))
            {
                LogManager.Default.Info($"加载市场主目录: {mainDirFile}");
                try
                {
                    if (_marketManager.LoadMainDirectory(mainDirFile))
                    {
                        LogManager.Default.Info("市场主目录配置加载成功");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载市场主目录配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载市场主目录失败: {mainDirFile}", exception: ex);
                }
            }
        }

        private void LoadAutoScript()
        {
            string autoScriptFile = Path.Combine(DATA_PATH, "autoscript.txt");

            if (File.Exists(autoScriptFile))
            {
                LogManager.Default.Info($"加载自动脚本: {autoScriptFile}");
                try
                {
                    _autoScriptManager.Load(autoScriptFile);
                    LogManager.Default.Info("自动脚本加载成功");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载自动脚本失败: {autoScriptFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"自动脚本文件不存在: {autoScriptFile}");
            }
        }

        private void LoadTasks()
        {
            string taskPath = TASK_PATH;

            if (Directory.Exists(taskPath))
            {
                LogManager.Default.Info($"加载任务系统: {taskPath}");
                try
                {
                    if (_taskManager.Load(taskPath))
                    {
                        LogManager.Default.Info("任务系统加载成功");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载任务系统失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载任务系统失败: {taskPath}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"任务目录不存在: {taskPath}");
            }
        }

        private void LoadHumanDataDescs()
        {
            LogManager.Default.Info("加载人物数据描述...");

            LoadHumanDataDesc(0, Path.Combine(DATA_PATH, "humandata_warr.txt"));
            
            LoadHumanDataDesc(1, Path.Combine(DATA_PATH, "humandata_magican.txt"));
            
            LoadHumanDataDesc(2, Path.Combine(DATA_PATH, "humandata_taoshi.txt"));
        }

        private void LoadStartPoints()
        {
            LogManager.Default.Info("加载出生点配置...");

            string startPointFile = Path.Combine(DATA_PATH, "startpoint.csv");
            if (!File.Exists(startPointFile))
            {
                LogManager.Default.Warning($"出生点配置文件不存在: {startPointFile}");
                return;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(startPointFile);
                int index = 0;
                var startPoints = new Dictionary<int, StartPoint>();
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 7)
                    {
                        string name = parts[0].Trim();
                        if (int.TryParse(parts[1], out int mapId) &&
                            int.TryParse(parts[2], out int x) &&
                            int.TryParse(parts[3], out int y) &&
                            int.TryParse(parts[4], out int range))
                        {
                            var startPoint = new StartPoint
                            {
                                Index = index,
                                Name = name,
                                MapId = mapId,
                                X = x,
                                Y = y,
                                Range = range
                            };

                            _startPoints[index] = startPoint;
                            _startPointNameToIndex[name] = index;
                            
                            startPoints[index] = startPoint;
                            index++;
                        }
                    }
                }

                GameWorld.Instance.SetStartPoints(startPoints);
                LogManager.Default.Info($"加载了 {_startPoints.Count} 个出生点配置");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载出生点配置失败: {startPointFile}", exception: ex);
            }
        }

        private void LoadFirstLoginInfo()
        {
            LogManager.Default.Info("加载首次登录信息...");

            string firstLoginFile = Path.Combine(DATA_PATH, "firstlogin.txt");
            if (!File.Exists(firstLoginFile))
            {
                LogManager.Default.Warning($"首次登录配置文件不存在: {firstLoginFile}");
                return;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(firstLoginFile);
                var currentInfo = new FirstLoginInfo();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        switch (key)
                        {
                            case "level":
                                if (int.TryParse(value, out int level))
                                    currentInfo.Level = level;
                                break;
                            case "gold":
                                if (uint.TryParse(value, out uint gold))
                                    currentInfo.Gold = gold;
                                break;
                            case "item":
                                var itemParts = value.Split('*');
                                if (itemParts.Length == 2 && 
                                    int.TryParse(itemParts[1], out int count))
                                {
                                    currentInfo.Items.Add(new FirstLoginItem
                                    {
                                        ItemName = itemParts[0].Trim(),
                                        Count = count
                                    });
                                }
                                break;
                        }
                    }
                }

                _firstLoginInfos.Add(currentInfo);
                
                GameWorld.Instance.SetFirstLoginInfo(currentInfo);
                
                LogManager.Default.Info($"加载首次登录信息: 等级={currentInfo.Level}, 金币={currentInfo.Gold}, 物品数={currentInfo.Items.Count}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载首次登录信息失败: {firstLoginFile}", exception: ex);
            }
        }

        private void LoadHumanPlayerMgr()
        {
            LogManager.Default.Info("初始化玩家管理器...");
            try
            {
                var playerMgr = HumanPlayerMgr.Instance;
                LogManager.Default.Info("玩家管理器初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化玩家管理器失败", exception: ex);
            }
        }

        private void LoadMagicManager()
        {
            LogManager.Default.Info("加载魔法技能管理器...");
            try
            {
                _magicManager.LoadAll();
                int magicCount = _magicManager.GetMagicCount();
                LogManager.Default.Info($"成功加载 {magicCount} 个魔法技能配置");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载魔法技能管理器失败", exception: ex);
            }
        }

        private void LoadNpcManagerEx()
        {
            LogManager.Default.Info("加载NPC管理器扩展...");
            try
            {
                string npcGenFile = Path.Combine(DATA_PATH, "npcgen.txt");
                if (File.Exists(npcGenFile))
                {
                    _npcManagerEx.Load(npcGenFile);
                    LogManager.Default.Info("NPC管理器扩展加载完成");
                }
                else
                {
                    LogManager.Default.Warning($"NPC生成文件不存在: {npcGenFile}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载NPC管理器扩展失败", exception: ex);
            }
        }

        private void LoadScriptObjectMgr()
        {
            LogManager.Default.Info("加载脚本对象管理器...");
            try
            {
                string scriptPath = SCRIPT_PATH;
                if (Directory.Exists(scriptPath))
                {
                    _scriptObjectMgr.Load(scriptPath);
                    int scriptCount = _scriptObjectMgr.GetScriptObjectCount();
                    int varCount = _scriptObjectMgr.GetDefineVariableCount();
                    LogManager.Default.Info($"成功加载 {scriptCount} 个脚本对象和 {varCount} 个定义变量");
                }
                else
                {
                    LogManager.Default.Warning($"脚本目录不存在: {scriptPath}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载脚本对象管理器失败", exception: ex);
            }
        }

        private void LoadMonsterManagerEx()
        {
            LogManager.Default.Info("加载怪物管理器扩展...");
            try
            {
                string monsterFile = Path.Combine(DATA_PATH, "BaseMonsterEx.txt");
                if (File.Exists(monsterFile))
                {
                    if (_monsterManagerEx.LoadMonsters(monsterFile))
                    {
                        LogManager.Default.Info("怪物管理器扩展加载完成");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载怪物定义文件失败");
                    }
                }
                else
                {
                    LogManager.Default.Warning($"怪物定义文件不存在: {monsterFile}");
                }

                string monsterScriptFile = Path.Combine(DATA_PATH, "monsterscript.txt");
                if (File.Exists(monsterScriptFile))
                {
                    _monsterManagerEx.LoadMonsterScript(monsterScriptFile);
                    LogManager.Default.Info("怪物脚本加载完成");
                }
                else
                {
                    LogManager.Default.Warning($"怪物脚本文件不存在: {monsterScriptFile}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物管理器扩展失败", exception: ex);
            }
        }

        private void LoadMonsterGenManager()
        {
            LogManager.Default.Info("加载怪物生成管理器...");
            try
            {
                string monGenPath = MONGENS_PATH;
                if (Directory.Exists(monGenPath))
                {
                    if (_monsterGenManager.LoadMonGen(monGenPath))
                    {
                        int genCount = _monsterGenManager.GetGenCount();
                        LogManager.Default.Info($"成功加载 {genCount} 个怪物生成点");
                        
                        _monsterGenManager.InitAllGen();
                        LogManager.Default.Info("怪物生成点初始化完成");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载怪物生成配置失败");
                    }
                }
                else
                {
                    LogManager.Default.Warning($"怪物生成目录不存在: {monGenPath}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物生成管理器失败", exception: ex);
            }
        }

        private void LoadMonItemsMgr()
        {
            LogManager.Default.Info("加载怪物掉落管理器...");
            try
            {
                string monItemsPath = MONITEMS_PATH;
                if (Directory.Exists(monItemsPath))
                {
                    if (_monItemsMgr.LoadMonItems(monItemsPath))
                    {
                        int itemsCount = _monItemsMgr.GetMonItemsCount();
                        LogManager.Default.Info($"成功加载 {itemsCount} 个怪物掉落配置");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载怪物掉落配置失败");
                    }
                }
                else
                {
                    LogManager.Default.Warning($"怪物掉落目录不存在: {monItemsPath}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物掉落管理器失败", exception: ex);
            }
        }

        private void LoadTimeSystem()
        {
            LogManager.Default.Info("初始化时间系统...");
            try
            {
                var timeSystem = TimeSystem.Instance;
                LogManager.Default.Info("时间系统初始化完成");
                
                var startupTime = timeSystem.GetStartupTime();
                var currentGameTime = timeSystem.GetCurrentTime();
                LogManager.Default.Info($"服务器启动时间: {startupTime:yyyy-MM-dd HH:mm:ss}");
                LogManager.Default.Info($"当前游戏时间: {currentGameTime} (小时*4 + 分钟/15)");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化时间系统失败", exception: ex);
            }
        }

        private void LoadSystemScript()
        {
            LogManager.Default.Info("初始化系统脚本...");
            try
            {
                var systemScriptObject = ScriptObjectMgr.Instance.GetScriptObject("system");
                if (systemScriptObject != null)
                {
                    SystemScript.Instance.Init(systemScriptObject);
                    LogManager.Default.Info("系统脚本初始化完成");
                    
                    var scriptObj = SystemScript.Instance.GetScriptObject();
                    if (scriptObj != null)
                    {
                        LogManager.Default.Info($"系统脚本对象: {scriptObj.Name}, 文件: {scriptObj.FilePath}");
                    }
                }
                else
                {
                    LogManager.Default.Warning("系统脚本对象不存在，创建空脚本对象");
                    var emptyScriptObject = new ScriptObject();
                    SystemScript.Instance.Init(emptyScriptObject);
                    LogManager.Default.Info("使用空脚本对象初始化系统脚本");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化系统脚本失败", exception: ex);
            }
        }

        private void LoadSpecialEquipmentManager()
        {
            LogManager.Default.Info("初始化特殊装备管理器...");
            try
            {
                var specialEquipmentManager = SpecialEquipmentManager.Instance;
                LogManager.Default.Info("特殊装备管理器初始化完成");
                
                int equipmentCount = specialEquipmentManager.GetSpecialEquipmentCount();
                LogManager.Default.Info($"特殊装备管理器已加载 {equipmentCount} 个装备配置");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化特殊装备管理器失败", exception: ex);
            }
        }

        private void LoadMapManager()
        {
            LogManager.Default.Info("初始化地图管理器...");
            try
            {
                if (MapManager.Instance.Load())
                {
                    int mapCount = MapManager.Instance.GetMapCount();
                    LogManager.Default.Info($"成功加载 {mapCount} 个地图");
                }
                else
                {
                    LogManager.Default.Warning("加载地图数据失败");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化地图管理器失败", exception: ex);
            }
        }

        private int GetConfigInt(Dictionary<string, string> config, string key, int defaultValue)
        {
            if (config.TryGetValue(key, out var value) && int.TryParse(value, out int result))
                return result;
            return defaultValue;
        }

        private string GetConfigString(Dictionary<string, string> config, string key, string defaultValue)
        {
            return config.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private bool GetConfigBool(Dictionary<string, string> config, string key, bool defaultValue)
        {
            if (config.TryGetValue(key, out var value))
            {
                if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1")
                    return true;
                if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0")
                    return false;
            }
            return defaultValue;
        }

        public HumanDataDesc? GetHumanDataDesc(int profession, int level)
        {
            var desc = GameWorld.Instance.GetHumanDataDesc(profession, level);
            if (desc != null)
                return desc;
            
            if (_humanDataDescs.TryGetValue(profession, out var levelDict) &&
                levelDict.TryGetValue(level, out var localDesc))
            {
                return localDesc;
            }
            return null;
        }

        public StartPoint? GetStartPoint(int index)
        {
            var point = GameWorld.Instance.GetStartPoint(index);
            if (point != null)
                return point;
            
            return _startPoints.TryGetValue(index, out var localPoint) ? localPoint : null;
        }

        public StartPoint? GetStartPoint(string name)
        {
            var point = GameWorld.Instance.GetStartPoint(name);
            if (point != null)
                return point;
            
            if (_startPointNameToIndex.TryGetValue(name, out int index))
            {
                return GetStartPoint(index);
            }
            return null;
        }

        public bool GetBornPoint(int profession, out int mapId, out int x, out int y, string? startPointName = null)
        {
            return GameWorld.Instance.GetBornPoint(profession, out mapId, out x, out y, startPointName);
        }

        public FirstLoginInfo? GetFirstLoginInfo()
        {
            var info = GameWorld.Instance.GetFirstLoginInfo();
            if (info != null)
                return info;
            
            return _firstLoginInfos.Count > 0 ? _firstLoginInfos[0] : null;
        }

        public string GetGameName(string nameKey)
        {
            var name = GameWorld.Instance.GetGameName(nameKey);
            if (name != nameKey) 
                return name;
            
            return _gameNames.TryGetValue(nameKey, out var localName) ? localName : nameKey;
        }

        public float GetGameVar(int varKey)
        {
            var value = GameWorld.Instance.GetGameVar(varKey);
            if (value != 0f) 
                return value;
            
            return _gameVars.TryGetValue(varKey, out var localValue) ? localValue : 0f;
        }

        public int GetChannelWaitTime(int channel)
        {
            var time = GameWorld.Instance.GetChannelWaitTime(channel);
            if (time != 1) 
                return time;
            
            return _channelWaitTimes.TryGetValue(channel, out var localTime) ? localTime : 1;
        }

        private void SetGameName(string key, string value)
        {
            _gameNames[key] = value;
            GameWorld.Instance.SetGameName(key, value);
        }

        private void SetGameVar(int key, float value)
        {
            _gameVars[key] = value;
            GameWorld.Instance.SetGameVar(key, value);
        }

        private void SetChannelWaitTime(int channel, int seconds)
        {
            _channelWaitTimes[channel] = seconds;
            GameWorld.Instance.SetChannelWaitTime(channel, seconds);
        }

        public bool LoadHumanDataDesc(int profession, string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"人物数据描述文件不存在: {filePath}");
                return false;
            }

            try
            {
                LogManager.Default.Info($"加载人物数据描述: 职业{profession} 文件:{filePath}");
                var lines = SmartReader.ReadAllLines(filePath);
                
                if (!_humanDataDescs.ContainsKey(profession))
                {
                    _humanDataDescs[profession] = new Dictionary<int, HumanDataDesc>();
                }

                var levelDict = _humanDataDescs[profession];
                var descs = new Dictionary<int, HumanDataDesc>();
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 24)
                    {
                        if (int.TryParse(parts[0].Trim(), out int level))
                        {
                            var desc = new HumanDataDesc
                            {
                                Level = level,
                                MinAc = ushort.Parse(parts[1].Trim()),
                                MaxAc = ushort.Parse(parts[2].Trim()),
                                MinMac = ushort.Parse(parts[3].Trim()),
                                MaxMac = ushort.Parse(parts[4].Trim()),
                                MinDc = ushort.Parse(parts[5].Trim()),
                                MaxDc = ushort.Parse(parts[6].Trim()),
                                MinMc = ushort.Parse(parts[7].Trim()),
                                MaxMc = ushort.Parse(parts[8].Trim()),
                                MinSc = ushort.Parse(parts[9].Trim()),
                                MaxSc = ushort.Parse(parts[10].Trim()),
                                Hp = ushort.Parse(parts[11].Trim()),
                                Mp = ushort.Parse(parts[12].Trim()),
                                BagWeight = ushort.Parse(parts[13].Trim()),
                                BodyWeight = ushort.Parse(parts[14].Trim()),
                                HandWeight = ushort.Parse(parts[15].Trim()),
                                HitRate = ushort.Parse(parts[16].Trim()),
                                Escape = ushort.Parse(parts[17].Trim()),
                                MagicRecover = ushort.Parse(parts[18].Trim()),
                                HpRecover = ushort.Parse(parts[19].Trim()),
                                MageEscape = ushort.Parse(parts[20].Trim()),
                                PoisonEscape = ushort.Parse(parts[21].Trim()),
                                LevelupExp = uint.Parse(parts[23].Trim()),
                            };

                            levelDict[level] = desc;
                            
                            descs[level] = desc;
                            count++;
                        }
                    }
                }

                GameWorld.Instance.SetHumanDataDescs(profession, descs);
                
                LogManager.Default.Info($"加载了职业 {profession} 的 {count} 个等级数据");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载人物数据描述失败: {filePath}", exception: ex);
                return false;
            }
        }

        public void ReloadConfig(string configType)
        {
            try
            {
                switch (configType.ToLower())
                {
                    case "item":
                        LoadItemConfigs();
                        LogManager.Default.Info("物品配置已重新加载");
                        break;

                    case "monster":
                        LoadMonsterManagerEx();
                        LoadMonItemsMgr();
                        LoadMonsterGenManager();
                        _monsterGenManager.InitAllGen();
                        LogManager.Default.Info("怪物配置已重新加载");
                        break;

                    case "magic":
                    case "skill":
                        LoadMagicConfigs();
                        LogManager.Default.Info("技能配置已重新加载");
                        break;

                    case "npc":
                        LoadNpcConfigs();
                        LogManager.Default.Info("NPC配置已重新加载");
                        break;

                    case "map":
                        LoadMapConfigs();
                        LogManager.Default.Info("地图配置已重新加载");
                        break;

                    case "guild":
                        LoadGuildConfigs();
                        LogManager.Default.Info("行会配置已重新加载");
                        break;

                    case "gm":
                        LoadGMConfigs();
                        LogManager.Default.Info("GM配置已重新加载");
                        break;

                    case "market":
                        LoadMarket();
                        LogManager.Default.Info("市场配置已重新加载");
                        break;

                    case "autoscript":
                        LoadAutoScript();
                        LogManager.Default.Info("自动脚本已重新加载");
                        break;

                    case "task":
                        LoadTasks();
                        LogManager.Default.Info("任务配置已重新加载");
                        break;

                    case "all":
                        LoadAllConfigs();
                        LogManager.Default.Info("所有配置已重新加载");
                        break;

                    default:
                        LogManager.Default.Warning($"未知的配置类型: {configType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"重新加载配置失败: {configType}", exception: ex);
            }
        }

        private void LoadMapSystem()
        {
            LogManager.Default.Info("加载地图系统...");
            LoadMapConfigs();
        }

        private void LoadItemSystem()
        {
            LogManager.Default.Info("加载物品系统...");
            LoadItemConfigs();
        }
    }

    public class HumanDataDesc
    {
        public int Level { get; set; }
        public ushort Hp { get; set; }
        public ushort Mp { get; set; }
        public uint LevelupExp { get; set; }
        public ushort MinAc { get; set; }
        public ushort MaxAc { get; set; }
        public ushort MinMac { get; set; }
        public ushort MaxMac { get; set; }
        public ushort MinDc { get; set; }
        public ushort MaxDc { get; set; }
        public ushort MinMc { get; set; }
        public ushort MaxMc { get; set; }
        public ushort MinSc { get; set; }
        public ushort MaxSc { get; set; }
        public ushort HandWeight { get; set; }
        public ushort BagWeight { get; set; }
        public ushort BodyWeight { get; set; }
        public ushort HitRate { get; set; }
        public ushort Escape { get; set; }
        public ushort MageEscape { get; set; }
        public ushort PoisonEscape { get; set; }
        public ushort HpRecover { get; set; }
        public ushort MagicRecover { get; set; }
    }

    public class StartPoint
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MapId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Range { get; set; }
    }

    public class FirstLoginInfo
    {
        public int Level { get; set; } = 1;
        public uint Gold { get; set; } = 1000;
        public List<FirstLoginItem> Items { get; set; } = new();
    }

    public class FirstLoginItem
    {
        public string ItemName { get; set; } = string.Empty;
        public int Count { get; set; } = 1;
    }
}
