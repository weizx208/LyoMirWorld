using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Linq;
using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;

namespace GameServer
{
    
    public class MineSpot : MapObject
    {
        public ItemInstance? Mine()
        {
            
            return null;
        }

        public override ObjectType GetObjectType()
        {
            return ObjectType.Event; 
        }

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            
            msg = Array.Empty<byte>();
            return false;
        }
    }

    
    public class MonsterCorpse : MapObject
    {
        public ItemInstance? GetMeat()
        {
            
            return null;
        }

        public override ObjectType GetObjectType()
        {
            return ObjectType.Event; 
        }

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            
            msg = Array.Empty<byte>();
            return false;
        }
    }

    
    
    
    public enum MineRewardType
    {
        Low = 0,      
        Medium = 1,   
        High = 2      
    }

    
    
    
    public enum e_humanattackmode
    {
        HAM_PEACE = 0,      
        HAM_GROUP = 1,      
        HAM_GUILD = 2,      
        HAM_COUPLE = 3,     
        HAM_MASTER = 4,     
        HAM_CRIME = 5,      
        HAM_ALL = 6,        
        HAM_SUPERMAN = 7,   
        HAM_MAX = 8
    }

    
    
    
    public enum e_chatchannel
    {
        CCH_NORMAL = 0,     
        CCH_WISPER = 1,     
        CCH_CRY = 2,        
        CCH_GM = 3,         
        CCH_GROUP = 4,      
        CCH_GUILD = 5,      
        CCH_MAX = 6
    }

    
    
    
    public enum MoneyType
    {
        Gold = 0,    
        Yuanbao = 1  
    }

    
    
    
    public static class CC
    {
        public const uint GREEN = 0x00FF00;      
        public const uint RED = 0xFF0000;        
        public const uint BLUE = 0x0000FF;       
        public const uint YELLOW = 0xFFFF00;     
        public const uint WHITE = 0xFFFFFF;      
        public const uint BLACK = 0x000000;      
        public const uint CYAN = 0x00FFFF;       
        public const uint MAGENTA = 0xFF00FF;    
        public const uint GRAY = 0x808080;       
        public const uint ORANGE = 0xFFA500;     
        public const uint PURPLE = 0x800080;     
        public const uint BROWN = 0xA52A2A;      
        public const uint PINK = 0xFFC0CB;       
        public const uint GOLD = 0xFFD700;       
        public const uint SILVER = 0xC0C0C0;     
        public const uint BRONZE = 0xCD7F32;     
    }

    
    
    
    
    public partial class HumanPlayer : AliveObject, ScriptTarget
    {
        
        public string Account { get; set; } = string.Empty;
        public uint CharDBId { get; set; }
        public int GmLevel { get; set; } = 0;

        
        public byte Job { get; set; }       
        public byte Sex { get; set; }       
        public byte Hair { get; set; }      
        public byte Direction { get; set; } 

        
        public uint Exp { get; set; }
        public uint Gold { get; set; }
        public uint Yuanbao { get; set; } 

        
        public int BaseDC { get; set; }    
        public int BaseMC { get; set; }    
        public int BaseSC { get; set; }    
        public int BaseAC { get; set; }    
        public int BaseMAC { get; set; }   
        public int Accuracy { get; set; }  
        public int Agility { get; set; }   
        public int Lucky { get; set; }     

        
        public Inventory Inventory { get; private set; }
        public Equipment Equipment { get; private set; }

        
        public SkillBook SkillBook { get; private set; }
        public PlayerQuestManager QuestManager { get; private set; }

        
        private bool _magicLoadedForSave = false;

        public void MarkMagicLoadedForSave(bool loaded) => _magicLoadedForSave = loaded;
        public bool IsMagicLoadedForSave() => _magicLoadedForSave;

        
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;

        
        public DateTime LoginTime { get; private set; }
        public DateTime LastActivity { get; set; }
        public bool IsFirstLogin { get; set; }

        
        private string _startPointName = "0";

        
        private uint _dbFlag0 = 0;
        private uint _forgePoint = 0;
        private ushort _curBagWeight = 0;
        private byte _curBodyWeight = 0;
        private byte _curHandWeight = 0;

        
        
        
        
        private readonly CombatStats _dbBaseStats = new();
        private int _dbBaseMaxHP = 0;
        private int _dbBaseMaxMP = 0;
        private bool _dbBaseStatsLoaded = false;

        
        private readonly CombatStats _equipStatsCache = new();

        
        private const ushort HUOLI_MAX = 1000;
        private ushort _huoli = HUOLI_MAX;

        
        private int _hitPointBonus = 0;

        
        public delegate void SendEncodedMessageDelegate(uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, byte[]? payload = null);
        private SendEncodedMessageDelegate? _sendEncodedMessage;

        
        public uint PkValue { get; set; }

        
        public uint GroupId { get; set; }

        
        public Guild? Guild { get; set; }
        public string GuildGroupName { get; set; } = string.Empty;
        public uint GuildLevel { get; set; }

        
        public TradeObject? CurrentTrade { get; set; }

        
        private uint _tradingWithPlayerId = 0;

        
        private readonly HashSet<string> _flags = new();
        private readonly object _flagLock = new();

        
        private readonly Dictionary<string, string> _variables = new();
        private readonly object _varLock = new();

        
        public PetSystem PetSystem { get; private set; }
        public MountSystem MountSystem { get; private set; }
        public PKSystem PKSystem { get; private set; }
        public AchievementSystem AchievementSystem { get; private set; }
        public MailSystem MailSystem { get; private set; }

        
        private PlayerSkill? _expMagic;

        
        private string _currentTitle = string.Empty;
        private int _currentTitleIndex = 0;

        
        private e_humanattackmode _attackMode = e_humanattackmode.HAM_PEACE;
        private e_chatchannel _chatChannel = e_chatchannel.CCH_NORMAL;

        
        private bool[] _chatChannelDisabled = new bool[(int)e_chatchannel.CCH_MAX];

        
        private string _currentWisperTarget = string.Empty;

        
        private byte _chatColor = 1;

        
        private readonly Dictionary<e_chatchannel, DateTime> _chatChannelTimers = new();

        
        private byte[]? _communityInfoRaw;

        public HumanPlayer(string account, string name, uint charDbId, TcpClient? client = null)
        {
            Account = account;
            Name = name;
            CharDBId = charDbId;
            _tcpClient = client;
            _stream = client?.GetStream();

            LoginTime = DateTime.Now;
            LastActivity = DateTime.Now;

            
            Inventory = new Inventory { MaxSlots = 40 };
            Equipment = new Equipment(this);
            SkillBook = new SkillBook();
            QuestManager = new PlayerQuestManager(this);

            
            PetSystem = new PetSystem(this);
            MountSystem = new MountSystem(this);
            PKSystem = new PKSystem(this);
            AchievementSystem = new AchievementSystem(this);
            MailSystem = new MailSystem(this);

            
            Level = 1;
            Job = 0;
            Sex = 0;
            MaxHP = 100;
            CurrentHP = 100;
            MaxMP = 100;
            CurrentMP = 100;

            
            Stats.MinDC = 1;
            Stats.MaxDC = 3;
            Stats.Accuracy = 5;
            Stats.Agility = 5;

            
            BaseDC = 0;
            BaseMC = 0;
            BaseSC = 0;
            BaseAC = 0;
            BaseMAC = 0;
            Accuracy = 5;
            Agility = 5;
            Lucky = 0;
            
            
            _visibleObjectFlag = 0;
            AddVisibleObjectType(ObjectType.NPC);          
            AddVisibleObjectType(ObjectType.Player);       
            AddVisibleObjectType(ObjectType.Monster);      
            AddVisibleObjectType(ObjectType.DownItem);     
            AddVisibleObjectType(ObjectType.VisibleEvent); 
            AddVisibleObjectType(ObjectType.Pet);          
        }

        
        
        
        public override byte GetRunSpeed()
        {
            try
            {
                
                if (MountSystem != null && MountSystem.IsRiding())
                    return MountSystem.GetRunSpeed();
            }
            catch { }
            return 2;
        }

        
        
        
        public void SetSendMessageDelegate(SendEncodedMessageDelegate sendMessageDelegate)
        {
            _sendEncodedMessage = sendMessageDelegate;
        }

        
        
        
        public bool Init(MirCommon.CREATEHUMANDESC createDesc)
        {
            try
            {
                LogManager.Default.Info($"开始初始化玩家");
                
                var dbinfo = createDesc.dbinfo;

                
                ushort dbLevel = dbinfo.wLevel;
                IsFirstLogin = dbLevel == 0;

                var firstLogin = IsFirstLogin ? GameWorld.Instance.GetFirstLoginInfo() : null;
                int initLevel = IsFirstLogin
                    ? Math.Clamp(firstLogin?.Level ?? 1, 1, 255)
                    : Math.Clamp((int)dbLevel, 1, 255);

                
                Level = (byte)initLevel;
                Exp = IsFirstLogin ? 0u : dbinfo.dwCurExp;
                Gold = IsFirstLogin ? (firstLogin?.Gold ?? dbinfo.dwGold) : dbinfo.dwGold;
                Yuanbao = dbinfo.dwYuanbao;

                
                MapId = (int)dbinfo.mapid;
                X = (ushort)dbinfo.x;
                Y = (ushort)dbinfo.y;
                _startPointName = string.IsNullOrWhiteSpace(dbinfo.szStartPoint) ? "0" : dbinfo.szStartPoint;

                var logicMap = LogicMapMgr.Instance.GetLogicMapById((uint)MapId);
                if (logicMap == null)
                {
                    if (GameWorld.Instance.GetBornPoint(dbinfo.btClass, out int bornMapId, out int bornX, out int bornY, dbinfo.szStartPoint))
                    {
                        LogManager.Default.Warning($"角色地图ID无效，使用出生点: mapid={dbinfo.mapid}, startPoint='{dbinfo.szStartPoint}', bornMapId={bornMapId}, ({bornX},{bornY})");
                        MapId = bornMapId;
                        X = (ushort)bornX;
                        Y = (ushort)bornY;
                    }
                    else
                    {
                        LogManager.Default.Warning($"角色地图ID无效且无法获取出生点: mapid={dbinfo.mapid}, startPoint='{dbinfo.szStartPoint}'");
                    }
                }

                
                Job = dbinfo.btClass;
                Sex = dbinfo.btSex;
                Hair = dbinfo.btHair;

                
                
                
                var humanDataDesc = GameWorld.Instance.GetHumanDataDesc(Job, Level);
                int descMaxHp = humanDataDesc?.Hp ?? 100;
                int descMaxMp = humanDataDesc?.Mp ?? 100;

                int dbCurHp = dbinfo.hp;
                int dbCurMp = dbinfo.mp;
                int dbMaxHp = dbinfo.maxhp;
                int dbMaxMp = dbinfo.maxmp;

                
                int baseMaxHp = dbMaxHp > 0 ? dbMaxHp : descMaxHp;
                int baseMaxMp = dbMaxMp > 0 ? dbMaxMp : descMaxMp;

                baseMaxHp = Math.Max(1, baseMaxHp);
                baseMaxMp = Math.Max(1, baseMaxMp);

                MaxHP = baseMaxHp;
                MaxMP = baseMaxMp;

                LogManager.Default.Info($"初始化HP/MP: DB hp/mp={dbCurHp}/{dbCurMp}, DB maxhp/maxmp={dbMaxHp}/{dbMaxMp}, Desc hp/mp={descMaxHp}/{descMaxMp}, Base maxhp/maxmp={baseMaxHp}/{baseMaxMp}");

                
                CurrentHP = dbCurHp;
                CurrentMP = dbCurMp;

                if (IsFirstLogin)
                {
                    
                    CurrentHP = MaxHP;
                    CurrentMP = MaxMP;
                }
                else
                {
                    
                    if (CurrentHP <= 0)
                    {
                        CurrentHP = MaxHP / 2; 
                    }
                    if (CurrentMP <= 0) CurrentMP = MaxMP;
                }

                
                MaxHP = Math.Max(MaxHP, CurrentHP);
                MaxMP = Math.Max(MaxMP, CurrentMP);

                MaxHP = Math.Clamp(MaxHP, 1, 65535);
                MaxMP = Math.Clamp(MaxMP, 1, 65535);
                CurrentHP = Math.Clamp(CurrentHP, 0, MaxHP);
                CurrentMP = Math.Clamp(CurrentMP, 0, MaxMP);



                
                
                bool useHumanAsBase = IsFirstLogin && humanDataDesc != null;

                int baseMinDC = useHumanAsBase ? humanDataDesc!.MinDc : dbinfo.mindc;
                int baseMaxDC = useHumanAsBase ? humanDataDesc!.MaxDc : dbinfo.maxdc;
                int baseMinMC = useHumanAsBase ? humanDataDesc!.MinMc : dbinfo.minmc;
                int baseMaxMC = useHumanAsBase ? humanDataDesc!.MaxMc : dbinfo.maxmc;
                int baseMinSC = useHumanAsBase ? humanDataDesc!.MinSc : dbinfo.minsc;
                int baseMaxSC = useHumanAsBase ? humanDataDesc!.MaxSc : dbinfo.maxsc;
                int baseMinAC = useHumanAsBase ? humanDataDesc!.MinAc : dbinfo.minac;
                int baseMaxAC = useHumanAsBase ? humanDataDesc!.MaxAc : dbinfo.maxac;
                int baseMinMAC = useHumanAsBase ? humanDataDesc!.MinMac : dbinfo.minmac;
                int baseMaxMAC = useHumanAsBase ? humanDataDesc!.MaxMac : dbinfo.maxmac;

                int baseHitRate = (int)(humanDataDesc?.HitRate ?? 5);
                int baseEscape = (int)(humanDataDesc?.Escape ?? 5);

                Stats.MinDC = baseMinDC;
                Stats.MaxDC = baseMaxDC;
                Stats.MinMC = baseMinMC;
                Stats.MaxMC = baseMaxMC;
                Stats.MinSC = baseMinSC;
                Stats.MaxSC = baseMaxSC;
                Stats.MinAC = baseMinAC;
                Stats.MaxAC = baseMaxAC;
                Stats.MinMAC = baseMinMAC;
                Stats.MaxMAC = baseMaxMAC;
                Stats.Accuracy = baseHitRate;
                Stats.Agility = baseEscape;
                Stats.Lucky = 0;

                
                BaseDC = baseMinDC;
                BaseMC = baseMinMC;
                BaseSC = baseMinSC;
                BaseAC = baseMinAC;
                BaseMAC = baseMinMAC;
                Accuracy = baseHitRate;
                Agility = baseEscape;
                Lucky = 0;

                
                _dbBaseStats.MinDC = baseMinDC;
                _dbBaseStats.MaxDC = baseMaxDC;
                _dbBaseStats.MinMC = baseMinMC;
                _dbBaseStats.MaxMC = baseMaxMC;
                _dbBaseStats.MinSC = baseMinSC;
                _dbBaseStats.MaxSC = baseMaxSC;
                _dbBaseStats.MinAC = baseMinAC;
                _dbBaseStats.MaxAC = baseMaxAC;
                _dbBaseStats.MinMAC = baseMinMAC;
                _dbBaseStats.MaxMAC = baseMaxMAC;
                _dbBaseStats.Accuracy = baseHitRate;
                _dbBaseStats.Agility = baseEscape;
                _dbBaseStats.Lucky = 0;
                _dbBaseMaxHP = baseMaxHp;
                _dbBaseMaxMP = baseMaxMp;
                _dbBaseStatsLoaded = true;

                
                _equipStatsCache.MaxHP = 0;
                _equipStatsCache.MaxMP = 0;
                _equipStatsCache.MinDC = 0;
                _equipStatsCache.MaxDC = 0;
                _equipStatsCache.MinMC = 0;
                _equipStatsCache.MaxMC = 0;
                _equipStatsCache.MinSC = 0;
                _equipStatsCache.MaxSC = 0;
                _equipStatsCache.MinAC = 0;
                _equipStatsCache.MaxAC = 0;
                _equipStatsCache.MinMAC = 0;
                _equipStatsCache.MaxMAC = 0;
                _equipStatsCache.Accuracy = 0;
                _equipStatsCache.Agility = 0;
                _equipStatsCache.Lucky = 0;

                
                GuildGroupName = dbinfo.szGuildName;

                
                _dbFlag0 = (dbinfo.dwFlag != null && dbinfo.dwFlag.Length > 0) ? dbinfo.dwFlag[0] : 0;
                _forgePoint = dbinfo.dwForgePoint;
                _curBagWeight = dbinfo.weight != 0
                    ? dbinfo.weight
                    : (ushort)Math.Clamp((int)(humanDataDesc?.BagWeight ?? 0), 0, ushort.MaxValue);
                _curBodyWeight = dbinfo.bodyweight != 0
                    ? dbinfo.bodyweight
                    : (byte)Math.Clamp((int)(humanDataDesc?.BodyWeight ?? 0), 0, 255);
                _curHandWeight = dbinfo.handweight != 0
                    ? dbinfo.handweight
                    : (byte)Math.Clamp((int)(humanDataDesc?.HandWeight ?? 0), 0, 255);

                
                SendInitMessages();

                
                LogManager.Default.Info($"发送状态改变消息");
                SendStatusChanged();
                LogManager.Default.Info($"发送天气改变消息");
                SendTimeWeatherChanged();
                LogManager.Default.Info($"发送组队模式消息");
                SendGroupMode();
                LogManager.Default.Info($"发送元宝更新消息");
                SendMoneyChanged(MoneyType.Yuanbao);

                
                LogManager.Default.Info($"发送攻击模式消息");
                ChangeAttackMode(e_humanattackmode.HAM_PEACE);
                SaySystemAttrib(CC.GREEN, "更改攻击模式 CTRL+H 查看攻击模式信息 @atkinfo");

                LogManager.Default.Info($"发送聊天模式消息");
                ChangeChatChannel(e_chatchannel.CCH_NORMAL);
                SaySystemAttrib(CC.GREEN, "更改频道 CTRL+S 查看频道信息 @ccinfo");

                
                LogManager.Default.Info($"发送属性消息");
                UpdateProp();
                LogManager.Default.Info($"发送子属性消息");
                UpdateSubProp();

                LogManager.Default.Info($"玩家初始化成功: {Name} 等级:{Level} 职业:{Job} 性别:{Sex}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"玩家初始化失败: {Name}, 错误: {ex.Message}");
                return false;
            }
        }

        
        
        
        private void SendInitMessages()
        {
            
            
            
            
            
            
            
            
            

            
            ushort bagSize = 40; 
            LogManager.Default.Info($"发送背包大小消息: bagSize={bagSize}");
            SendMsg((uint)ObjectId, 0x9594, 0, bagSize, 0);

            
            LogManager.Default.Info("发送SM_READY");
            SendMsg(0xf2d505d7, ProtocolCmd.SM_READY, 0, 0, 0);

            
            string version = GameWorld.Instance.GetGameName(GameName.Version);
            LogManager.Default.Info($"发送版本信息: {version}");
            SendMsg((uint)ObjectId, 0x9591, 0, 0, 0, version);

            
            var map = LogicMapMgr.Instance?.GetLogicMapById((uint)MapId);
            LogManager.Default.Info($"map = {map}");
            if (map != null)
            {
                LogManager.Default.Info("发送SM_SETMAP");
                var mapFile = string.IsNullOrWhiteSpace(map.MapFile) ? map.MapName : map.MapFile;
                SendMsg((uint)ObjectId, ProtocolCmd.SM_SETMAP, (ushort)X, (ushort)Y, (ushort)((Sex << 8) | Direction), mapFile);

                uint[] dwParam = { GetFeather(), 0, GetStatus(), 0 };
                LogManager.Default.Info("发送SM_SETPLAYER");
                SendMsg((uint)ObjectId, ProtocolCmd.SM_SETPLAYER, (ushort)X, (ushort)Y, (ushort)((Sex << 8) | Direction), dwParam);

                LogManager.Default.Info("发送SM_SETPLAYERNAME");
                SendMsg((uint)ObjectId, ProtocolCmd.SM_SETPLAYERNAME, GetNameColor(this), 0, 0, Name);

                
                LogManager.Default.Info("发送地图战斗属性(0x2c4)");
                SendMsg(map.IsFightMap() ? 1u : 0u, 0x2c4, 0, 0, 0);

                
                LogManager.Default.Info("发送SM_SETMAPNAME");
                SendMsg(0, ProtocolCmd.SM_SETMAPNAME, 0, 0, 0, map.MapName);
            }
        }

        
        
        
        protected override void OnEnterMap(LogicMap map)
        {
            if (map == null) return;

            
            SendMapEnterMessages(map);

            
            base.OnEnterMap(map);

            
            SendTimeWeatherChanged();
            

            if (GetStatus() > 0)
                SendStatusChanged();

            
            
            
            
        }

        
        
        
        private void SendMapEnterMessages(LogicMap map)
        {
            if (map == null) return;

            
            LogManager.Default.Info("发送SM_SETMAP(进入地图)");
            var mapFile = string.IsNullOrWhiteSpace(map.MapFile) ? map.MapName : map.MapFile;
            SendMsg((uint)ObjectId, ProtocolCmd.SM_SETMAP, (ushort)X, (ushort)Y, (ushort)((Sex << 8) | Direction), mapFile);

            
            uint[] dwParam = { GetFeather(), 0, GetStatus(), 0 };
            LogManager.Default.Info("发送SM_SETPLAYER(进入地图)");
            SendMsg((uint)ObjectId, ProtocolCmd.SM_SETPLAYER, (ushort)X, (ushort)Y, (ushort)((Sex << 8) | Direction), dwParam);


            LogManager.Default.Info("发送SM_SETPLAYERNAME(进入地图)");
            SendMsg((uint)ObjectId, ProtocolCmd.SM_SETPLAYERNAME, GetNameColor(this), 0, 0, Name);

            LogManager.Default.Info("发送地图战斗属性(0x2c4, 进入地图)");
            SendMsg(map.IsFightMap() ? 1u : 0u, 0x2c4, 0, 0, 0);

            LogManager.Default.Info("发送SM_SETMAPNAME(进入地图)");
            SendMsg(0, ProtocolCmd.SM_SETMAPNAME, 0, 0, 0, map.MapName);

            
        }

        
        
        
        public bool ChangeMap(uint targetMapId, ushort targetX, ushort targetY)
        {
            try
            {
                var toMap = LogicMapMgr.Instance?.GetLogicMapById(targetMapId) ?? MapManager.Instance.GetMap(targetMapId);
                if (toMap == null)
                {
                    SaySystem("目标地图不存在");
                    return false;
                }

                int tx = targetX;
                int ty = targetY;

                
                if (toMap.IsBlocked(tx, ty))
                {
                    var pts = new Point[1];
                    if (toMap.GetValidPoint(tx, ty, pts, 1) > 0)
                    {
                        tx = pts[0].X;
                        ty = pts[0].Y;
                    }
                    else
                    {
                        SaySystem("目标位置不可到达");
                        return false;
                    }
                }

                var fromMap = CurrentMap;
                int ox = X;
                int oy = Y;
                string fromMapName = fromMap == null ? string.Empty : (string.IsNullOrWhiteSpace(fromMap.MapFile) ? fromMap.MapName : fromMap.MapFile);
                string toMapName = string.IsNullOrWhiteSpace(toMap.MapFile) ? toMap.MapName : toMap.MapFile;

                
                try { fromMap?.RemoveObject(this); } catch { }

                
                SendMsg(ObjectId, ProtocolCmd.SM_CLEAROBJECTS, 0, 0, 0);
                SendMsg(ObjectId, ProtocolCmd.SM_CHANGEMAP, (ushort)tx, (ushort)ty, 0, toMapName);

                
                IsMapLoaded = false;
                CleanVisibleList();

                bool added = toMap.AddObject(this, tx, ty);

                if (!added)
                {
                    
                    if (fromMap != null)
                    {
                        fromMap.AddObject(this, ox, oy);

                        if (!string.IsNullOrWhiteSpace(fromMapName))
                        {
                            SendMsg(ObjectId, ProtocolCmd.SM_CHANGEMAP, (ushort)ox, (ushort)oy, 0, fromMapName);
                        }
                    }

                    SaySystem("切换地图失败");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Warning($"切换地图失败: player={Name} - {ex.Message}");
                return false;
            }
        }


        
        
        
        private static uint MakeFeather(byte b1, byte b2, byte b3, byte b4)
        {
            
            return ((uint)b1 << 24) | ((uint)b2 << 16) | ((uint)b3 << 8) | b4;
        }

        
        
        
        public override uint GetFeather()
        {
            
            byte dress = 0;
            byte hair = (byte)Math.Clamp((int)Hair, 0, 255);
            byte weapon = 0;
            byte horse = 0;

            var armor = Equipment.GetItem(EquipSlot.Dress);
            if (armor != null)
            {
                
                int shapeNibble = armor.Definition.Shape & 0x0F;
                int colorNibble = armor.DressColor & 0x0F;
                dress = (byte)((shapeNibble << 4) | colorNibble);
            }

            var weaponItem = Equipment.GetItem(EquipSlot.Weapon);
            if (weaponItem != null)
            {
                
                weapon = weaponItem.Definition.StateView;
            }

            var horseItem = Equipment.GetItem(EquipSlot.Mount);
            if (horseItem != null && MountSystem.IsRiding())
            {
                
                horse = (byte)Math.Clamp((horseItem.Definition.Shape & 0xFF) + 0x40, 0, 255);
            }

            return MakeFeather(dress, hair, weapon, horse);
        }

        
        
        
        public override uint GetHealth()
        {
            ushort cur = (ushort)Math.Clamp(CurrentHP, 0, 65535);
            ushort max = (ushort)Math.Clamp(MaxHP, 1, 65535); 
            return ((uint)max << 16) | cur;
        }


        
        
        
        public override uint GetStatus()
        {
            
            return 0;
        }

        public override byte GetSex() => (byte)Math.Clamp((int)Sex, 0, 255);

        
        
        
        public override byte GetNameColor(MapObject? viewer = null)
        {
            
            
            return 255;
        }

        
        
        
        public void SendMsg(uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, object? data = null)
        {
            try
            {
                
                if (_sendEncodedMessage != null)
                {
                    byte[]? payload = null;
                    if (data != null)
                    {
                        if (data is string strData)
                        {
                            payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(strData);
                        }
                        else if (data is uint[] uintArray)
                        {
                            
                            payload = new byte[uintArray.Length * 4];
                            Buffer.BlockCopy(uintArray, 0, payload, 0, payload.Length);
                        }
                        else if (data is byte[] byteArray)
                        {
                            payload = byteArray;
                        }
                        else
                        {
                            payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(data.ToString() ?? "");
                        }
                    }

                    _sendEncodedMessage(dwFlag, wCmd, w1, w2, w3, payload);
                }
                else
                {
                    
                    byte[]? payload = null;
                    if (data != null)
                    {
                        if (data is string strData)
                        {
                            payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(strData);
                        }
                        else if (data is uint[] uintArray)
                        {
                            
                            payload = new byte[uintArray.Length * 4];
                            Buffer.BlockCopy(uintArray, 0, payload, 0, payload.Length);
                        }
                        else if (data is byte[] byteArray)
                        {
                            payload = byteArray;
                        }
                        else
                        {
                            payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(data.ToString() ?? "");
                        }
                    }

                    
                    var msg = new MirCommon.MirMsgOrign
                    {
                        dwFlag = dwFlag,
                        wCmd = wCmd,
                        wParam = new ushort[3] { w1, w2, w3 },
                        
                    };

                    
                    byte[] encodedMessage = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(msg, payload);
                    if (encodedMessage.Length > 0)
                    {
                        SendMessage(encodedMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"SendMsg失败: {ex.Message}");
            }
        }

        
        
        
        public void ChangeAttackMode(e_humanattackmode newMode)
        {
            if (newMode < e_humanattackmode.HAM_PEACE || newMode >= e_humanattackmode.HAM_MAX)
                return;

            _attackMode = newMode;

            
            SendMsg((uint)ObjectId, 0x105, (ushort)newMode, 0, 0); 

            LogManager.Default.Debug($"{Name} 更改攻击模式为: {newMode}");
        }

        
        
        
        public void ChangeChatChannel(e_chatchannel newChannel)
        {
            if (newChannel < e_chatchannel.CCH_NORMAL || newChannel >= e_chatchannel.CCH_MAX)
                return;

            _chatChannel = newChannel;

            
            SendMsg((uint)ObjectId, 0x106, (ushort)newChannel, 0, 0); 

            LogManager.Default.Debug($"{Name} 更改聊天频道为: {newChannel}");
        }

        
        
        
        public void SendTimeWeatherChanged()
        {
            
            var gameWorld = GameWorld.Instance;
            if (gameWorld == null)
                return;

            
            var currentTime = DateTime.Now;
            SendMsg((uint)ObjectId, 0x107, (ushort)currentTime.Hour, (ushort)currentTime.Minute, 0); 

            
            SendMsg((uint)ObjectId, 0x108, 0, 0, 0); 

            LogManager.Default.Debug($"{Name} 收到时间天气更新");
        }

        
        
        
        public void SendGroupMode()
        {
            
            if (GroupId > 0)
            {
                
                SendMsg((uint)ObjectId, 0x109, 1, 0, 0, GroupId.ToString()); 
            }
            else
            {
                
                SendMsg((uint)ObjectId, 0x109, 0, 0, 0); 
            }

            LogManager.Default.Debug($"{Name} 收到组队模式更新");
        }

        
        
        
        public void SendMoneyChanged(MoneyType moneyType)
        {
            
            
            
            uint amount;
            ushort cmd;

            switch (moneyType)
            {
                case MoneyType.Gold:
                    amount = Gold;
                    cmd = MirCommon.ProtocolCmd.SM_GOLDCHANGED;
                    break;
                case MoneyType.Yuanbao:
                    amount = Yuanbao;
                    cmd = MirCommon.ProtocolCmd.SM_SETSUPERGOLD;
                    break;
                default:
                    return;
            }

            SendMsg(amount, cmd, 0, 0, 0);
            LogManager.Default.Debug($"{Name} {moneyType} 更新为: {amount}");
        }

        public override ObjectType GetObjectType() => ObjectType.Player;

        
        
        
        
        protected override int GetAutoRecoverHp()
        {
            var humanData = GameWorld.Instance.GetHumanDataDesc(Job, Level);
            int recover = 9 + (int)(humanData?.HpRecover ?? 0);
            return Math.Max(0, recover);
        }

        
        
        
        protected override int GetAutoRecoverMp()
        {
            var humanData = GameWorld.Instance.GetHumanDataDesc(Job, Level);
            int recover = 9 + (int)(humanData?.MagicRecover ?? 0);
            return Math.Max(0, recover);
        }

        public override void Update()
        {
            base.Update();

            
            if (_tcpClient != null && !_tcpClient.Connected)
            {
                OnDisconnected();
                return;
            }

            
            QuestManager.Update();

            
            PKSystem.Update();

            

            
            LastActivity = DateTime.Now;

            
            if (CompleteAction())
            {
                
            }
        }

        #region 网络消息

        
        
        
        public override void SendMessage(byte[] message)
        {
            if (_stream == null || !_tcpClient!.Connected)
                return;

            try
            {
                
                
                bool isEncoded = message.Length >= 2 && message[0] == (byte)'#' && message[message.Length - 1] == (byte)'!';
                if (!isEncoded && message.Length >= MirCommon.MirMsgOrign.Size)
                {
                    uint dwFlag = BitConverter.ToUInt32(message, 0);
                    ushort wCmd = BitConverter.ToUInt16(message, 4);
                    ushort w1 = BitConverter.ToUInt16(message, 6);
                    ushort w2 = BitConverter.ToUInt16(message, 8);
                    ushort w3 = BitConverter.ToUInt16(message, 10);

                    byte[]? payload = null;
                    int payloadLen = message.Length - MirCommon.MirMsgOrign.Size;
                    if (payloadLen > 0)
                    {
                        payload = new byte[payloadLen];
                        Buffer.BlockCopy(message, MirCommon.MirMsgOrign.Size, payload, 0, payloadLen);
                    }

                    var msg = new MirCommon.MirMsgOrign
                    {
                        dwFlag = dwFlag,
                        wCmd = wCmd,
                        wParam = new ushort[3] { w1, w2, w3 },
                    };

                    message = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(msg, payload);
                }

                lock (_stream)
                {
                    _stream.Write(message, 0, message.Length);
                    _stream.Flush();
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
                OnDisconnected();
            }
        }

        
        
        
        public void SendProtocolMsg(ushort cmd, byte[] data)
        {
            
            SendMsg(ObjectId, cmd, 0, 0, 0, data);
        }

        
        
        
        private void OnDisconnected()
        {
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch { }

            _stream = null;
            _tcpClient = null;

            
            CurrentMap?.RemoveObject(this);

            LogManager.Default.Info($"玩家断开连接: {Name}");
        }

        #endregion

        
        
        
        public bool IsMapLoaded { get; set; } = false;
        private int _debugViewEnterCount = 0;

        
        private readonly Dictionary<ushort, (ushort Cur, ushort Max)> _bodyEffects = new();

        
        
        
        public void SetBodyEffect(ushort effectId, ushort cur, ushort max)
        {
            if (effectId == 0)
                return;

            lock (_bodyEffects)
            {
                if (cur == 0 && max == 0)
                    _bodyEffects.Remove(effectId);
                else
                    _bodyEffects[effectId] = (cur, max);
            }

            BroadcastBodyEffect(effectId, cur, max);
        }

        private void BroadcastBodyEffect(ushort effectId, ushort cur, ushort max)
        {
            if (CurrentMap == null)
                return;

            foreach (var viewer in CurrentMap.GetPlayersInRange(X, Y, 18))
            {
                viewer.SendMsg(ObjectId, ProtocolCmd.SM_STARTBODYEFFECT, cur, max, effectId);
            }
        }

        private void SendBodyEffectsTo(HumanPlayer viewer)
        {
            if (viewer == null)
                return;

            List<KeyValuePair<ushort, (ushort Cur, ushort Max)>> snapshot;
            lock (_bodyEffects)
            {
                snapshot = _bodyEffects.ToList();
            }

            foreach (var kv in snapshot)
            {
                viewer.SendMsg(ObjectId, ProtocolCmd.SM_STARTBODYEFFECT, kv.Value.Cur, kv.Value.Max, kv.Key);
            }
        }

        public override void OnObjectEnterView(MapObject obj)
        {
            if (!IsMapLoaded)
                return;

            
            if (obj.GetViewMsg(out var msg, this))
            {
                SendMessage(msg);
                int n = System.Threading.Interlocked.Increment(ref _debugViewEnterCount);
                if (n <= 30)
                {
                    LogManager.Default.Debug($"视野进入: viewer={Name}({ObjectId:X8}) objType={obj.GetObjectType()} objId={obj.ObjectId:X8} pos=({obj.X},{obj.Y}) msgLen={msg.Length}");
                }
            }
            else
            {
                int n = System.Threading.Interlocked.Increment(ref _debugViewEnterCount);
                if (n <= 30)
                {
                    LogManager.Default.Debug($"视野进入: viewer={Name}({ObjectId:X8}) objType={obj.GetObjectType()} objId={obj.ObjectId:X8} pos=({obj.X},{obj.Y}) GetViewMsg=false");
                }
            }

            
            if (obj is HumanPlayer otherPlayer)
            {
                otherPlayer.SendBodyEffectsTo(this);
            }
        }

        public override void OnObjectLeaveView(MapObject obj)
        {
            if (!IsMapLoaded)
                return;

            
            if (obj is DownItemObject downItem)
            {
                if (downItem.GetOutViewMsg(out var outMsg, this))
                {
                    SendMessage(outMsg);
                    return;
                }
            }

            if (obj is VisibleEvent visibleEvent)
            {
                if (visibleEvent.GetOutViewMsg(out var outMsg, this))
                {
                    SendMessage(outMsg);
                    return;
                }
            }

            SendMsg(obj.ObjectId, MirCommon.ProtocolCmd.SM_DISAPPEAR, obj.X, obj.Y, 0);
        }

        #region 辅助方法

        
        
        
        private bool IsInRange(GameObject target, int range)
        {
            if (target == null || CurrentMap == null)
                return false;

            
            if (target is MapObject mapObject)
            {
                int distanceX = Math.Abs(X - mapObject.X);
                int distanceY = Math.Abs(Y - mapObject.Y);
                return distanceX <= range && distanceY <= range;
            }

            
            return false;
        }

        
        
        
        public override void Say(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            
            if (!CheckChatCooldown(MirCommon.ChatChannel.WORLD))
                return;

            
            string processedMessage = ChatFilter.Instance.ProcessChatMessage(message);

            
            if (!ChatFilter.Instance.CanSendMessage(processedMessage, out string reason))
            {
                SaySystem($"无法发送消息：{reason}");
                return;
            }

            
            LogManager.Default.Info($"{Name}: {processedMessage}");

            
            SendChatMessage(MirCommon.ChatChannel.WORLD, processedMessage, null);
        }

        
        
        
        public void SaySystem(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            LogManager.Default.Info($"[系统] {Name}: {message}");

            
            SendSystemMessage(message);
        }

        
        
        
        private void SendChatMessage(MirCommon.ChatChannel channel, string message, string? targetName)
        {
            if (CurrentMap == null)
                return;

            
            string fullMessage = $"{Name}: {message}";

            
            switch (channel)
            {
                case MirCommon.ChatChannel.WORLD:
                    
                    SendToNearbyPlayers(fullMessage, channel);
                    break;

                case MirCommon.ChatChannel.PRIVATE:
                    
                    if (!string.IsNullOrEmpty(targetName))
                    {
                        SendWisperMessage(targetName, message);
                    }
                    else if (!string.IsNullOrEmpty(_currentWisperTarget))
                    {
                        SendWisperMessage(_currentWisperTarget, message);
                    }
                    else
                    {
                        SaySystem("当前密谈对象为空，无法密谈！");
                    }
                    break;

                case MirCommon.ChatChannel.HORN:
                    
                    SendToMapPlayers(fullMessage, channel);
                    break;

                case MirCommon.ChatChannel.TEAM:
                    
                    SendToGroupMembers(fullMessage);
                    break;

                case MirCommon.ChatChannel.GUILD:
                    
                    SendToGuildMembers(fullMessage);
                    break;
            }

            
            UpdateChatTimer(channel);
        }

        
        
        
        private bool CheckChatCooldown(MirCommon.ChatChannel channel)
        {
            
            int cooldownSeconds = GetChannelCooldown(channel);

            
            e_chatchannel eChannel = ConvertToEChannel(channel);

            if (_chatChannelTimers.TryGetValue(eChannel, out var lastChatTime))
            {
                var elapsed = DateTime.Now - lastChatTime;
                if (elapsed.TotalSeconds < cooldownSeconds)
                {
                    int remaining = cooldownSeconds - (int)elapsed.TotalSeconds;
                    SaySystem($"{GetChannelName(channel)}频道 {remaining} 秒后才能继续发言！");
                    return false;
                }
            }

            return true;
        }

        
        
        
        private int GetChannelCooldown(MirCommon.ChatChannel channel)
        {
            
            
            return channel switch
            {
                MirCommon.ChatChannel.WORLD => 1,    
                MirCommon.ChatChannel.HORN => 10,      
                MirCommon.ChatChannel.TEAM => 1,     
                MirCommon.ChatChannel.GUILD => 1,     
                MirCommon.ChatChannel.PRIVATE => 1,    
                _ => 1
            };
        }

        
        
        
        private string GetChannelName(MirCommon.ChatChannel channel)
        {
            return channel switch
            {
                MirCommon.ChatChannel.WORLD => "普通",
                MirCommon.ChatChannel.HORN => "喊话",
                MirCommon.ChatChannel.TEAM => "组队",
                MirCommon.ChatChannel.GUILD => "行会",
                MirCommon.ChatChannel.PRIVATE => "密谈",
                _ => "未知"
            };
        }

        
        
        
        private void UpdateChatTimer(MirCommon.ChatChannel channel)
        {
            
            e_chatchannel eChannel = ConvertToEChannel(channel);
            _chatChannelTimers[eChannel] = DateTime.Now;
        }

        
        
        
        private e_chatchannel ConvertToEChannel(MirCommon.ChatChannel channel)
        {
            
            return channel switch
            {
                MirCommon.ChatChannel.WORLD => e_chatchannel.CCH_NORMAL,
                MirCommon.ChatChannel.PRIVATE => e_chatchannel.CCH_WISPER,
                MirCommon.ChatChannel.HORN => e_chatchannel.CCH_CRY,
                MirCommon.ChatChannel.TEAM => e_chatchannel.CCH_GROUP,
                MirCommon.ChatChannel.GUILD => e_chatchannel.CCH_GUILD,
                _ => e_chatchannel.CCH_NORMAL
            };
        }

        
        
        
        private void SendToNearbyPlayers(string message, MirCommon.ChatChannel channel)
        {
            if (CurrentMap == null)
                return;

            
            var nearbyPlayers = CurrentMap.GetObjectsInRange(X, Y, 10)
                .Where(obj => obj is HumanPlayer && obj != this)
                .Cast<HumanPlayer>();

            
            const ushort cmd = MirCommon.ProtocolCmd.SM_SYSCHAT; 
            const ushort attrib = 0x9700;
            const ushort color = 0x38;
            const ushort flags = 0x100;

            foreach (var player in nearbyPlayers)
            {
                if (!player.IsChannelDisabled(channel))
                {
                    player.SendMsg(ObjectId, cmd, attrib, color, flags, message);
                }
            }

            
            SendMsg(ObjectId, cmd, attrib, color, flags, message);
        }

        
        
        
        private void SendWisperMessage(string targetName, string message)
        {
            
            var targetPlayer = HumanPlayerMgr.Instance.FindByName(targetName);

            if (targetPlayer == null)
            {
                SaySystem($"{targetName} 目前不在线，无法密谈！");
                return;
            }

            if (targetPlayer == this)
            {
                SaySystem("你干吗要自言自语呢？");
                return;
            }

            
            if (targetPlayer.IsChannelDisabled(MirCommon.ChatChannel.PRIVATE))
            {
                SaySystem("对方关闭了密谈频道，请稍候再试！");
                return;
            }

            
            const ushort cmd = 0x67; 
            const ushort attrib = 0xfffc;
            const ushort color = 0;
            const ushort flags = 1;
            targetPlayer.SendMsg(ObjectId, cmd, attrib, color, flags, $"{Name}=>{message}");

            
            _currentWisperTarget = targetName;
        }

        
        
        
        private void SendToMapPlayers(string message, MirCommon.ChatChannel channel)
        {
            if (CurrentMap == null)
                return;

            
            const ushort cmd = MirCommon.ProtocolCmd.SM_SYSCHAT; 
            const ushort attrib = 0x9700;
            const ushort color = 0x38;
            const ushort flags = 0x100;

            
            var nearbyPlayers = CurrentMap.GetObjectsInRange(X, Y, 20)
                .Where(obj => obj is HumanPlayer)
                .Cast<HumanPlayer>();

            foreach (var player in nearbyPlayers)
            {
                if (!player.IsChannelDisabled(channel))
                {
                    player.SendMsg(ObjectId, cmd, attrib, color, flags, $"(!){message}");
                }
            }
        }

        
        
        
        private void SendToGroupMembers(string message)
        {
            if (GroupId == 0)
            {
                SaySystem("没有在编组内，组队频道发言无效！");
                return;
            }

            
            var group = GroupObjectManager.Instance?.GetPlayerGroup(this);
            if (group == null)
            {
                SaySystem("组队信息错误，无法发送组队消息");
                return;
            }

            group.SendChatMessage(this, message);
        }

        
        
        
        private void SendToGuildMembers(string message)
        {
            if (Guild == null)
            {
                SaySystem("没有加入行会，行会频道发言无效！");
                return;
            }

            
            
            SaySystem("行会频道功能暂未完全实现");
        }

        
        
        
        private void SendSystemMessage(string message)
        {
            
            const ushort cmd = MirCommon.ProtocolCmd.SM_SYSCHAT; 
            const ushort attrib = 0xff00;
            SendMsg(ObjectId, cmd, attrib, 0, 0, $"[系统] {message}");
        }

        
        
        
        private bool IsChannelDisabled(MirCommon.ChatChannel channel)
        {
            
            e_chatchannel eChannel = ConvertToEChannel(channel);

            
            if ((int)eChannel < _chatChannelDisabled.Length)
            {
                return _chatChannelDisabled[(int)eChannel];
            }

            
            return false;
        }

        
        
        
        public void SendGroupDestroyed()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28F); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            SendMessage(builder.Build());
        }

        
        
        
        public void SaySystemAttrib(uint attrib, string message)
        {
            
            SendMsg(ObjectId,
                MirCommon.ProtocolCmd.SM_SYSCHAT,
                (ushort)(attrib & 0xFFFF),
                (ushort)(attrib >> 16),
                0,
                message);
        }


        
        
        
        private bool IsInRange(MapObject target, int range)
        {
            if (target == null || CurrentMap != target.CurrentMap)
                return false;

            int dx = Math.Abs(X - target.X);
            int dy = Math.Abs(Y - target.Y);
            return dx <= range && dy <= range;
        }

        
        
        
        private uint CalculateRepairCost(ItemInstance item)
        {
            if (item == null)
                return 0;

            
            float durabilityLossRatio = 1.0f - ((float)item.Durability / item.MaxDurability);
            uint baseCost = (uint)(item.Definition.SellPrice * durabilityLossRatio);

            
            if (item.EnhanceLevel > 0)
                baseCost += (uint)(baseCost * item.EnhanceLevel * 0.1f);

            return Math.Max(10, baseCost); 
        }

        
        
        
        private void SendInventoryUpdate()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x288); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            
            var allItems = Inventory.GetAllItems();
            builder.WriteByte((byte)allItems.Count);

            foreach (var kvp in allItems)
            {
                var item = kvp.Value;
                builder.WriteByte((byte)kvp.Key); 
                builder.WriteUInt32((uint)item.InstanceId);
                builder.WriteInt32(item.ItemId);
                builder.WriteString(item.Definition.Name);
                builder.WriteUInt16((ushort)item.Count);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
            }

            SendMessage(builder.Build());
        }

        
        
        
        private void SendEquipmentUpdate(EquipSlot slot, ItemInstance? item)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x287); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte((byte)slot);

            if (item != null)
            {
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteInt32(item.ItemId);
                builder.WriteString(item.Definition.Name);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteByte((byte)item.EnhanceLevel);
            }
            else
            {
                builder.WriteUInt64(0);
                builder.WriteInt32(0);
                builder.WriteString("");
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteByte(0);
            }

            SendMessage(builder.Build());
        }

        
        
        
        private void SendHPMPUpdate()
        {
            SendHpMpChanged();
        }

        
        
        
        
        
        protected override void SendHpMpChanged()
        {
            ushort curHp = (ushort)Math.Max(0, Math.Min(CurrentHP, ushort.MaxValue));
            ushort curMp = (ushort)Math.Max(0, Math.Min(CurrentMP, ushort.MaxValue));
            ushort maxHp = (ushort)Math.Max(0, Math.Min(MaxHP, ushort.MaxValue));

            var msg = new MirCommon.MirMsgOrign
            {
                dwFlag = ObjectId,
                wCmd = MirCommon.ProtocolCmd.SM_HPMPCHANGED,
                wParam = new ushort[3] { curHp, curMp, maxHp },
            };

            byte[] encoded = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(msg, null);
            if (encoded.Length <= 0)
                return;

            
            SendMessage(encoded);

            
            CurrentMap?.SendToNearbyPlayers(X, Y, 18, encoded, ObjectId);
        }

        public override void Heal(int amount)
        {
            base.Heal(amount);
        }

        public override void RestoreMP(int amount)
        {
            base.RestoreMP(amount);
        }

        
        
        
        public bool DoMine(byte direction)
        {
            
            if ((DateTime.Now - _lastMineTime).TotalMilliseconds < 800)
            {
                Say("挖矿太快了，请稍等");
                return false;
            }

            
            if (!CanDoAttack())
            {
                Say("无法执行挖矿动作");
                return false;
            }

            Direction = direction;

            
            SetAttackAction();

            
            if (CurrentMap is LogicMap logicMap)
            {
                
                if (!logicMap.IsFlagSeted(MapFlag.MF_MINE))
                {
                    Say("这个地图不能挖矿");
                    return false;
                }
            }

            
            _mineCounter++;

            
            UpdateMineEffect();

            
            if (_mineCounter % 10 == 0)
            {
                
                Say("挖到了高级矿石！");
                GiveMineReward(MineRewardType.High);
            }
            else if (_mineCounter % 5 == 0)
            {
                
                Say("挖到了中级矿石！");
                GiveMineReward(MineRewardType.Medium);
            }
            else
            {
                
                Say("挖到了普通矿石！");
                GiveMineReward(MineRewardType.Low);
            }

            
            _lastMineTime = DateTime.Now;

            return true;
        }

        
        
        
        private bool CanDoAttack()
        {
            
            if (IsInCombat())
            {
                Say("战斗中无法挖矿");
                return false;
            }

            
            if (IsInPrivateShop())
            {
                Say("摆摊中无法挖矿");
                return false;
            }

            
            if (InSafeArea())
            {
                Say("安全区无法挖矿");
                return false;
            }

            
            if (CurrentHP < 10)
            {
                Say("体力不足，无法挖矿");
                return false;
            }

            return true;
        }

        
        
        
        private void SetAttackAction()
        {
            
            StartAction(ActionType.Attack, 0);

            
            SendAttackActionMessage();
        }

        
        
        
        private void UpdateMineEffect()
        {
            
            SendMineEffectMessage();

            
            PlayMineSound();

            
            int staminaCost = 1;
            CurrentHP = Math.Max(0, CurrentHP - staminaCost);

            
            SendHPMPUpdate();
        }

        
        
        
        private void GiveMineReward(MineRewardType rewardType)
        {
            
            ItemDefinition definition;
            switch (rewardType)
            {
                case MineRewardType.High:
                    definition = new ItemDefinition(4002, "金矿石", ItemType.Material);
                    definition.SellPrice = 500;
                    break;
                case MineRewardType.Medium:
                    definition = new ItemDefinition(4001, "银矿石", ItemType.Material);
                    definition.SellPrice = 200;
                    break;
                case MineRewardType.Low:
                default:
                    definition = new ItemDefinition(4000, "铁矿石", ItemType.Material);
                    definition.SellPrice = 50;
                    break;
            }

            
            var item = new ItemInstance(definition, (long)ItemManager.Instance.AllocateTempMakeIndex());

            
            if (Inventory.AddItem(item))
            {
                
                LogManager.Default.Info($"{Name} 挖到了 {definition.Name}");

                
                AddMiningSkillExp(10);
            }
            else
            {
                Say("背包已满，矿石掉落到地上");

                
                if (CurrentMap != null)
                {
                    var mapItem = new MapItem(item)
                    {
                        OwnerPlayerId = ObjectId
                    };

                    
                    CurrentMap.AddObject(mapItem, X, Y);
                }
            }
        }

        
        
        
        private void SendAttackActionMessage()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28F); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(Direction);

            SendMessage(builder.Build());
        }

        
        
        
        private void SendMineEffectMessage()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x290); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(Direction);

            SendMessage(builder.Build());
        }

        
        
        
        private void PlayMineSound()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x291); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(1001); 

            SendMessage(builder.Build());
        }

        
        
        
        private void AddMiningSkillExp(int exp)
        {
            
            var miningSkill = SkillBook.GetSkill(1001); 
            if (miningSkill != null)
            {
                miningSkill.AddExp(exp);

                
                if (miningSkill.CanLevelUp())
                {
                    miningSkill.LevelUp();
                    Say($"挖矿技能升级到 {miningSkill.Level} 级！");
                }
            }
        }

        
        
        
        public bool GetMeal(byte direction)
        {
            Direction = direction;

            
            int targetX = X;
            int targetY = Y;

            switch (direction)
            {
                case 0: targetY--; break; 
                case 1: targetX++; targetY--; break; 
                case 2: targetX++; break; 
                case 3: targetX++; targetY++; break; 
                case 4: targetY++; break; 
                case 5: targetX--; targetY++; break; 
                case 6: targetX--; break; 
                case 7: targetX--; targetY--; break; 
            }

            
            if (CurrentMap == null)
                return false;

            var corpse = CurrentMap.GetObjectAt(targetX, targetY) as MonsterCorpse;
            if (corpse == null)
            {
                Say("这里没有怪物尸体");
                return false;
            }

            
            return GetMeat(corpse);
        }

        
        
        
        public bool DoTrainHorse(byte direction)
        {
            Direction = direction;

            
            var mount = Equipment.GetEquipment(EquipSlot.Mount);
            if (mount == null)
            {
                Say("你没有坐骑");
                return false;
            }

            
            if (mount.Durability >= mount.MaxDurability)
            {
                Say("坐骑不需要训练");
                return false;
            }

            
            uint trainCost = 100; 
            if (Gold < trainCost)
            {
                Say($"训练需要 {trainCost} 金币");
                return false;
            }

            
            if (!TakeGold(trainCost))
                return false;

            
            mount.Durability = Math.Min(mount.Durability + 10, mount.MaxDurability);

            
            LogManager.Default.Info($"{Name} 训练了坐骑，花费 {trainCost} 金币");

            
            SaySystem($"训练了坐骑，花费 {trainCost} 金币");

            
            SendEquipmentUpdate(EquipSlot.Mount, mount);

            return true;
        }

        
        
        
        private void StartAction(ActionType actionType, uint targetId)
        {
            
            _currentAction = actionType;
            _currentActionTarget = targetId;
            _actionStartTime = DateTime.Now;

            
            SendActionStart(actionType, targetId);
        }

        
        
        
        public override bool CompleteAction()
        {
            if (_currentAction == ActionType.None)
                return false;

            
            var elapsed = DateTime.Now - _actionStartTime;
            bool isComplete = false;

            switch (_currentAction)
            {
                case ActionType.Mining:
                    isComplete = elapsed.TotalSeconds >= 3.0; 
                    break;
                case ActionType.GetMeat:
                    isComplete = elapsed.TotalSeconds >= 2.0; 
                    break;
                default:
                    isComplete = elapsed.TotalSeconds >= 1.0; 
                    break;
            }

            if (isComplete)
            {
                
                switch (_currentAction)
                {
                    case ActionType.Mining:
                        CompleteMining(_currentActionTarget);
                        break;
                    case ActionType.GetMeat:
                        CompleteGetMeat(_currentActionTarget);
                        break;
                }

                
                _currentAction = ActionType.None;
                _currentActionTarget = 0;

                
                SendActionComplete();

                return true;
            }

            return false;
        }

        
        
        
        private void AddProcess(ProcessType processType, uint param1, uint param2)
        {
            
            
            
            var process = new GlobeProcess(GlobeProcessType.None, param1, param2)
            {
                
            };

            
            

            
            SendProcessStart(processType, param1, param2);
        }

        
        
        
        private void SendActionStart(ActionType actionType, uint targetId)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28B); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte((byte)actionType);
            builder.WriteUInt32(targetId);

            SendMessage(builder.Build());
        }

        
        
        
        private void SendActionComplete()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28C); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            SendMessage(builder.Build());
        }

        
        
        
        private void SendProcessStart(ProcessType processType, uint param1, uint param2)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28E); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte((byte)processType);
            builder.WriteUInt32(param1);
            builder.WriteUInt32(param2);

            SendMessage(builder.Build());
        }

        
        private ActionType _currentAction = ActionType.None;
        private uint _currentActionTarget = 0;
        private DateTime _actionStartTime = DateTime.MinValue;

        #endregion

        #region 系统标志和状态方法

        
        
        
        public void SetSystemFlag(int flag, bool value)
        {
            lock (_flagLock)
            {
                string flagKey = $"SF_{flag}";
                if (value)
                {
                    _flags.Add(flagKey);
                }
                else
                {
                    _flags.Remove(flagKey);
                }
            }
        }

        
        
        
        public bool GetSystemFlag(int flag)
        {
            lock (_flagLock)
            {
                string flagKey = $"SF_{flag}";
                return _flags.Contains(flagKey);
            }
        }

        
        
        
        public uint GetDBId()
        {
            return CharDBId;
        }

        
        
        
        public Inventory GetBag()
        {
            return Inventory;
        }

        
        
        
        public int GetEquipments(MirCommon.EQUIPMENT[] equipments)
        {
            if (equipments == null || equipments.Length < 20)
                return 0;

            int count = 0;
            for (int i = 0; i < (int)MirCommon.EquipPos.U_MAX && count < equipments.Length; i++)
            {
                var equip = Equipment.GetItem((EquipSlot)i);
                if (equip == null)
                    continue;

                var equipment = new MirCommon.EQUIPMENT();
                equipment.pos = (ushort)i;

                var itemClient = ItemPacketBuilder.BuildITEMCLIENT(equip);
                equipment.item = itemClient;
                equipments[count++] = equipment;
            }

            return count;
        }

        
        
        
        public void NotifyAppearanceChanged()
        {
            SendFeatureChanged();
        }

        
        
        
        public void SendFeatureChanged()
        {
            uint feather = GetFeather();
            ushort w1 = (ushort)(feather & 0xFFFF);
            ushort w2 = (ushort)((feather >> 16) & 0xFFFF);
            ushort w3 = (ushort)(GetSex() << 8);

            
            SendMsg(ObjectId, ProtocolCmd.SM_FEATURECHANGED, w1, w2, w3);

            
            if (CurrentMap == null)
                return;

            foreach (var viewer in CurrentMap.GetPlayersInRange(X, Y, 18))
            {
                if (viewer.ObjectId == ObjectId)
                    continue;

                viewer.SendMsg(ObjectId, ProtocolCmd.SM_FEATURECHANGED, w1, w2, w3);
            }
        }

        
        
        
        public void SendDuraChanged(int pos, int curDura, int maxDura)
        {
            ushort wPos = (ushort)Math.Clamp(pos, 0, ushort.MaxValue);
            uint dwCur = (uint)Math.Clamp(curDura, 0, ushort.MaxValue);
            ushort wMax = (ushort)Math.Clamp(maxDura, 0, ushort.MaxValue);
            SendMsg(dwCur, ProtocolCmd.SM_ITEMDURACHANGED, wPos, wMax, 0);
        }

        public void SendAllEquipmentDura()
        {
            for (int i = 0; i < (int)MirCommon.EquipPos.U_MAX; i++)
            {
                var item = Equipment.GetItem((EquipSlot)i);
                if (item == null)
                {
                    SendDuraChanged(i, 0, 0);
                    continue;
                }

                SendDuraChanged(i, item.Durability, item.MaxDurability);
            }
        }

        private ushort GetCurBagWeight() => Inventory?.CalcWeight() ?? 0;

        private byte GetCurHandWeight()
        {
            int weight = Equipment?.GetEquipment(EquipSlot.Weapon)?.Definition?.Weight ?? 0;
            return (byte)Math.Clamp(weight, 0, 255);
        }

        private byte GetCurBodyWeight()
        {
            int weight = Equipment?.CalcEquipmentsWeight(-1) ?? 0;
            return (byte)Math.Clamp(weight, 0, 255);
        }

        private ushort GetMaxBagWeight(ulong fallback) =>
            _curBagWeight != 0 ? _curBagWeight : (ushort)Math.Clamp((long)fallback, 0, ushort.MaxValue);

        private byte GetMaxBodyWeight(ulong fallback) =>
            _curBodyWeight != 0 ? _curBodyWeight : (byte)Math.Clamp((long)fallback, 0, 255);

        private byte GetMaxHandWeight(ulong fallback) =>
            _curHandWeight != 0 ? _curHandWeight : (byte)Math.Clamp((long)fallback, 0, 255);

        
        
        
        public void SendWeightChanged()
        {
            ushort curBag = GetCurBagWeight();
            ushort curBody = GetCurBodyWeight();
            ushort curHand = GetCurHandWeight();
            SendMsg(curBag, MirCommon.ProtocolCmd.SM_WEIGHTCHANGED, curBody, curHand, 0);
        }

        
        
        
        public void UpdateProp()
        {
            
            RecalcTotalStats();

            var humanData = GameWorld.Instance.GetHumanDataDesc(Job, Level);
            uint maxExp = humanData?.LevelupExp ?? 0;

            static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);
            static ushort ClampUShort(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);

            ushort curHp = ClampUShort(CurrentHP);
            ushort maxHp = ClampUShort(MaxHP);
            ushort curMp = ClampUShort(CurrentMP);
            ushort maxMp = ClampUShort(MaxMP);

            var prop = new MirCommon.HumanProp
            {
                wLevel = Level,

                btMinDef = ClampByte(Stats.MinAC),
                btMaxDef = ClampByte(Stats.MaxAC),
                btMinMagicDef = ClampByte(Stats.MinMAC),
                btMaxMagicDef = ClampByte(Stats.MaxMAC),
                btMinAtk = ClampByte(Stats.MinDC),
                btMaxAtk = ClampByte(Stats.MaxDC),
                btMinMagAtk = ClampByte(Stats.MinMC),
                btMaxMagAtk = ClampByte(Stats.MaxMC),
                btMinSprAtk = ClampByte(Stats.MinSC),
                btMaxSprAtk = ClampByte(Stats.MaxSC),

                wCurHp = curHp,
                wCurMp = curMp,
                wMaxHp = maxHp,
                wMaxMp = maxMp,

                btHpRecover = (byte)Math.Clamp((int)(humanData?.HpRecover ?? 0), 0, 255),
                btMagRecover = (byte)Math.Clamp((int)(humanData?.MagicRecover ?? 0), 0, 255),

                dwCurexp = Exp,
                dwMaxexp = maxExp,

                
                wCurBagWeight = GetCurBagWeight(),
                wMaxBagWeight = GetMaxBagWeight((ulong)(humanData?.BagWeight ?? 0)),
                btCurBodyWeight = GetCurBodyWeight(),
                btMaxBodyWeight = GetMaxBodyWeight((ulong)(humanData?.BodyWeight ?? 0)),
                btCurHandWeight = GetCurHandWeight(),
                btMaxHandWeight = GetMaxHandWeight((ulong)(humanData?.HandWeight ?? 0)),
            };

            byte[] payload = StructToBytes(prop);

            
            SendMsg(Gold, MirCommon.ProtocolCmd.SM_UPDATEPROP, (ushort)Job, 0, 0, payload);
        }

        
        
        
        public void UpdateSubProp()
        {
            
            
            
            RecalcTotalStats();

            var humanData = GameWorld.Instance.GetHumanDataDesc(Job, Level);

            byte escape = (byte)Math.Clamp(Stats.Agility, 0, 255);
            byte hitRate = (byte)Math.Clamp(Stats.Accuracy, 0, 255);
            byte poisonEscape = (byte)Math.Clamp((int)(humanData?.PoisonEscape ?? 0), 0, 255);
            byte mageEscape = (byte)Math.Clamp((int)(humanData?.MageEscape ?? 0), 0, 255);

            
            uint dwArr = ((_dbFlag0 & 0xffffu) << 8) | mageEscape;

            
            ushort w1 = (ushort)((escape << 8) | hitRate);
            ushort w2 = poisonEscape;
            ushort w3 = 4;

            ushort forgeLo = (ushort)(_forgePoint & 0xffff);
            ushort forgeHi = (ushort)((_forgePoint >> 16) & 0xffff);

            ushort[] wArray = { _huoli, HUOLI_MAX, 1, forgeLo, forgeHi };
            byte[] payload = new byte[wArray.Length * 2];
            Buffer.BlockCopy(wArray, 0, payload, 0, payload.Length);

            SendMsg(dwArr, 0x2f0, w1, w2, w3, payload);
        }

        
        
        
        public void SendStatusChanged()
        {
            
            
            
            
            uint status = GetStatus();
            ushort w1 = (ushort)(status & 0xffff);
            ushort w2 = (ushort)((status >> 16) & 0xffff);
            ushort w3 = 0; 

            
            SendMsg(ObjectId, MirCommon.ProtocolCmd.SM_CHARSTATUSCHANGED, w1, w2, w3);

            
            var msg = new MirCommon.MirMsgOrign
            {
                dwFlag = ObjectId,
                wCmd = MirCommon.ProtocolCmd.SM_CHARSTATUSCHANGED,
                wParam = new ushort[3] { w1, w2, w3 },
            };
            byte[] encoded = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(msg, null);
            if (encoded.Length > 0)
            {
                CurrentMap?.SendToNearbyPlayers(X, Y, 18, encoded, ObjectId);
            }
        }

        private static byte[] StructToBytes<T>(T structure) where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            byte[] bytes = new byte[size];
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(structure, ptr, false);
                System.Runtime.InteropServices.Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }

            return bytes;
        }

        
        
        
        private void SendSubPropChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x291); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            
            builder.WriteUInt16((ushort)Accuracy);
            builder.WriteUInt16((ushort)Agility);
            builder.WriteUInt16((ushort)Lucky);

            SendMessage(builder.Build());
        }

        
        
        
        private void SendStatsChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x292); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            
            builder.WriteUInt16((ushort)BaseDC);
            builder.WriteUInt16((ushort)BaseMC);
            builder.WriteUInt16((ushort)BaseSC);
            builder.WriteUInt16((ushort)BaseAC);
            builder.WriteUInt16((ushort)BaseMAC);

            SendMessage(builder.Build());
        }

        
        
        
        public bool CheckIsFirstLogin()
        {
            return IsFirstLogin;
        }

        
        
        
        public void AddProcess(int processType, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, object? data)
        {
            
            var process = new GlobeProcess((GlobeProcessType)processType, param1, param2, param3, param4, param5, (int)param6, data?.ToString());

            
            

            
            SendProcessStart((ProcessType)processType, param1, param2);
        }

        
        
        
        
        public void OnCommunityInfo(byte[] data)
        {
            _communityInfoRaw = data ?? Array.Empty<byte>();
            LogManager.Default.Debug($"[{Name}] OnCommunityInfo: len={_communityInfoRaw.Length}");

            
        }

        #endregion

        #region 数据库消息处理方法

        
        
        
        public void OnTaskInfo(MirCommon.Database.TaskInfo taskInfo)
        {
            try
            {
                LogManager.Default.Debug($"处理任务信息: 任务ID={taskInfo.dwTaskId}, 状态={taskInfo.dwState}");

                
                

                
                SendTaskUpdate(taskInfo);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理任务信息失败: {ex.Message}");
            }
        }

        
        
        
        public void SetUpgradeItem(MirCommon.Item item)
        {
            try
            {
                LogManager.Default.Debug($"设置升级物品: 物品ID={item.dwMakeIndex}");

                
                var itemDef = ItemManager.Instance.GetDefinition((int)item.baseitem.wImageIndex);
                if (itemDef == null)
                {
                    LogManager.Default.Error($"找不到物品定义: {item.baseitem.wImageIndex}");
                    return;
                }

                var itemInstance = new ItemInstance(itemDef, (long)item.dwMakeIndex)
                {
                    Durability = item.wCurDura,
                    MaxDurability = item.wMaxDura,
                    Count = 1
                };

                
                if (Inventory.AddItem(itemInstance))
                {
                    SaySystem($"获得了升级物品: {itemDef.Name}");
                    SendInventoryUpdate();
                }
                else
                {
                    SaySystem("背包已满，无法添加升级物品");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"设置升级物品失败: {ex.Message}");
            }
        }

        
        
        
        public void SetMagic(MirCommon.Database.MAGICDB magicDb, byte key)
        {
            try
            {
                LogManager.Default.Debug($"设置技能: 技能ID={magicDb.wMagicId}, 等级={magicDb.btCurLevel}, 快捷键={key}");

                
                var skillDef = SkillManager.Instance.GetDefinition((int)magicDb.wMagicId);
                if (skillDef == null)
                {
                    
                    
                    
                    if (MagicManager.Instance.GetMagicCount() == 0)
                    {
                        
                        MagicManager.Instance.LoadAll();
                    }

                    var magicClass = MagicManager.Instance.GetClassById((int)magicDb.wMagicId);
                    if (magicClass != null)
                    {
                        var dynamicDef = new SkillDefinition((int)magicDb.wMagicId, magicClass.szName, SkillType.Attack)
                        {
                            Description = magicClass.szDesc ?? string.Empty,
                            RequireJob = magicClass.btJob,
                            RequireLevel = magicClass.btNeedLv[0],
                            RequireSkill = magicClass.wNeedMagic[0],
                            Range = 7,
                            MaxLevel = 3
                        };

                        
                        dynamicDef.LevelData[1] = new SkillLevelData(1)
                        {
                            MPCost = magicClass.sSpell,
                            Power = magicClass.sPower,
                            Cooldown = magicClass.wDelay,
                            Range = 7,
                            LearnCost = 0
                        };

                        SkillManager.Instance.AddDefinition(dynamicDef);
                        skillDef = dynamicDef;
                        LogManager.Default.Warning($"未找到技能定义，已从basemagic动态补齐: id={magicDb.wMagicId}, name={magicClass.szName}");
                    }
                    else
                    {
                        
                        var placeholder = new SkillDefinition((int)magicDb.wMagicId, $"Skill{magicDb.wMagicId}", SkillType.Attack)
                        {
                            Description = "(placeholder)",
                            RequireJob = 0,
                            RequireLevel = 0,
                            RequireSkill = 0,
                            Range = 7,
                            MaxLevel = 3
                        };
                        placeholder.LevelData[1] = new SkillLevelData(1)
                        {
                            MPCost = 0,
                            Power = 0,
                            Cooldown = 0,
                            Range = 7,
                            LearnCost = 0
                        };

                        SkillManager.Instance.AddDefinition(placeholder);
                        skillDef = placeholder;
                        LogManager.Default.Warning($"未找到技能定义/魔法配置，已创建占位技能定义: id={magicDb.wMagicId}");
                    }
                }

                
                var skill = SkillBook.GetSkill((int)magicDb.wMagicId);
                if (skill == null)
                {
                    
                    SkillBook.LearnSkill(skillDef);
                    skill = SkillBook.GetSkill((int)magicDb.wMagicId);
                    if (skill == null)
                    {
                        LogManager.Default.Warning($"学习技能失败(技能栏已满?): id={magicDb.wMagicId}, name={skillDef.Name}");
                        return;
                    }
                    SaySystem($"学会了新技能: {skillDef.Name}");
                }

                
                skill.Level = magicDb.btCurLevel;
                skill.UseCount = magicDb.dwCurTrain > int.MaxValue ? int.MaxValue : (int)magicDb.dwCurTrain;
                skill.Key = magicDb.btUserKey != 0 ? magicDb.btUserKey : key;

                
                SendMagicList();

                
                RecalcHitSpeed();
                UpdateSubProp();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"设置技能失败: {ex.Message}");
            }
        }

        
        
        
        public void SendMagicList()
        {
            try
            {
                var skills = SkillBook.GetAllSkills();
                LogManager.Default.Debug($"发送技能列表: {skills.Count}个技能");

                if (MagicManager.Instance.GetMagicCount() == 0)
                {
                    MagicManager.Instance.LoadAll();
                }

                var buf = new MirCommon.MAGIC[Math.Min(255, skills.Count)];
                int count = 0;

                foreach (var skill in skills)
                {
                    if (count >= buf.Length)
                        break;

                    if (!MagicManager.Instance.CreateMagic((uint)skill.SkillId, out var magic))
                        continue;

                    var m = new MirCommon.MAGIC
                    {
                        cKey = skill.Key,
                        btLevel = (byte)Math.Clamp(skill.Level, 0, 255),
                        iCurExp = skill.UseCount,
                        wId = (ushort)skill.SkillId,
                        btEffectType = magic.btEffectType,
                        btEffect = magic.btEffect,
                        wSpell = magic.wSpell,
                        wPower = magic.wPower,
                        job = magic.job,
                        wDelayTime = magic.wDelayTime,
                        btDefSpell = magic.btDefSpell,
                        btDefPower = magic.btDefPower,
                        wMaxPower = magic.wMaxPower,
                        wDefMaxPower = magic.wDefMaxPower,
                    };

                    
                    string name = string.IsNullOrWhiteSpace(magic.szName) ? skill.Definition.Name : magic.szName;
                    var nameBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(name);
                    int nameLen = Math.Min(12, nameBytes.Length);
                    m.btNameLength = (byte)nameLen;
                    Array.Copy(nameBytes, 0, m.szName, 0, nameLen);

                    
                    Array.Copy(magic.btNeedLevel, 0, m.btNeedLevel, 0, Math.Min(4, magic.btNeedLevel.Length));
                    Array.Copy(magic.iLevelupExp, 0, m.iLevelupExp, 0, Math.Min(4, magic.iLevelupExp.Length));

                    buf[count++] = m;
                }

                
                byte[] payload = count > 0 ? StructArrayToBytes(buf, count) : Array.Empty<byte>();
                SendMsg(0, 0xD3, 0, 0, 0, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送技能列表失败: {ex.Message}");
            }
        }

        private static byte[] StructArrayToBytes<T>(T[] array, int count) where T : struct
        {
            if (array == null || count <= 0)
                return Array.Empty<byte>();

            if (count > array.Length)
                count = array.Length;

            int elementSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            byte[] result = new byte[elementSize * count];

            for (int i = 0; i < count; i++)
            {
                IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(elementSize);
                try
                {
                    System.Runtime.InteropServices.Marshal.StructureToPtr(array[i], ptr, false);
                    System.Runtime.InteropServices.Marshal.Copy(ptr, result, i * elementSize, elementSize);
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                }
            }

            return result;
        }

        
        
        
        public void OnPetBank(MirCommon.Item[] items, int count)
        {
            try
            {
                LogManager.Default.Debug($"处理宠物仓库物品: {count}个");

                
                var petBag = PetSystem.GetPetBag();
                

                
                for (int i = 0; i < count; i++)
                {
                    var item = items[i];
                    var itemDef = ItemManager.Instance.GetDefinition((int)item.baseitem.wImageIndex);
                    if (itemDef == null)
                        continue;

                    var itemInstance = new ItemInstance(itemDef, (long)item.dwMakeIndex)
                    {
                        Durability = item.wCurDura,
                        MaxDurability = item.wMaxDura,
                        Count = 1
                    };

                    petBag.AddItem(itemInstance);
                }

                
                SendPetBagUpdate();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理宠物仓库物品失败: {ex.Message}");
            }
        }

        
        
        
        private void SendTaskUpdate(MirCommon.Database.TaskInfo taskInfo)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x294); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(taskInfo.dwTaskId);
            builder.WriteByte((byte)taskInfo.dwState);
            builder.WriteString(""); 

            SendMessage(builder.Build());
        }

        
        
        
        private void SendPetBagUpdate()
        {
            var petBag = PetSystem.GetPetBag();
            var items = petBag.GetAllItems();

            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x295); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            
            builder.WriteUInt16((ushort)items.Count);

            
            foreach (var item in items.Values)
            {
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteUInt32((uint)item.ItemId);
                builder.WriteUInt16((ushort)item.Count);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteByte((byte)item.EnhanceLevel);
                builder.WriteByte(0); 

                builder.WriteUInt32(0); 
                builder.WriteUInt32(0); 
                builder.WriteUInt32(0); 
            }

            SendMessage(builder.Build());
        }

        #endregion

        #region 数据库保存方法

        
        
        
        public void UpdateToDB(MirCommon.Database.DBServerClient? dbClient)
        {
            try
            {
                if (dbClient == null)
                {
                    LogManager.Default.Warning($"UpdateToDB跳过：DBServerClient为空 player={Name}");
                    return;
                }

                
                
                
                bool bagLoaded = GetSystemFlag((int)MirCommon.SystemFlag.SF_BAGLOADED);
                bool equipLoaded = GetSystemFlag((int)MirCommon.SystemFlag.SF_EQUIPMENTLOADED);
                bool magicLoaded = IsMagicLoadedForSave();

                
                var info = BuildCharDbInfoForSave();
                dbClient.SendPutCharDBInfo(info).GetAwaiter().GetResult();

                
                if (magicLoaded)
                {
                    var magics = BuildMagicDbForSave();
                    dbClient.SendUpdateMagic(GetDBId(), magics).GetAwaiter().GetResult();
                }
                else
                {
                    LogManager.Default.Debug($"UpdateToDB跳过保存技能：技能数据未加载 player={Name}");
                }

                
                if (bagLoaded)
                {
                    var bagItems = BuildDbItemsForBagSave();
                    dbClient.SendUpdateItems(GetDBId(), (byte)ItemDataFlag.IDF_BAG, bagItems).GetAwaiter().GetResult();
                }
                else
                {
                    LogManager.Default.Debug($"UpdateToDB跳过保存背包：背包数据未加载 player={Name}");
                }

                if (equipLoaded)
                {
                    var equipItems = BuildDbItemsForEquipmentSave();
                    dbClient.SendUpdateItems(GetDBId(), (byte)ItemDataFlag.IDF_EQUIPMENT, equipItems).GetAwaiter().GetResult();
                }
                else
                {
                    LogManager.Default.Debug($"UpdateToDB跳过保存装备：装备数据未加载 player={Name}");
                }

                LogManager.Default.Info($"{Name} 数据已保存到数据库(dbid={GetDBId()})");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存玩家数据失败: {Name}, 错误: {ex.Message}");
            }
        }

        
        
        
        public void UpdateToDB()
        {
            UpdateToDB(null);
        }

        private MirCommon.Database.CHARDBINFO BuildCharDbInfoForSave()
        {
            
            
            int baseMaxHp = _dbBaseStatsLoaded ? _dbBaseMaxHP : MaxHP;
            int baseMaxMp = _dbBaseStatsLoaded ? _dbBaseMaxMP : MaxMP;
            baseMaxHp = Math.Max(1, baseMaxHp);
            baseMaxMp = Math.Max(1, baseMaxMp);

            
            int saveHp = Math.Clamp(CurrentHP, 0, 65535);
            int saveMp = Math.Clamp(CurrentMP, 0, 65535);

            if (!IsDead && saveHp == 0)
            {
                
                saveHp = Math.Clamp(baseMaxHp, 1, 65535);
            }
            else if (IsDead)
            {
                saveHp = 0;
            }

            int baseMinDC = _dbBaseStatsLoaded ? _dbBaseStats.MinDC : Stats.MinDC;
            int baseMaxDC = _dbBaseStatsLoaded ? _dbBaseStats.MaxDC : Stats.MaxDC;
            int baseMinMC = _dbBaseStatsLoaded ? _dbBaseStats.MinMC : Stats.MinMC;
            int baseMaxMC = _dbBaseStatsLoaded ? _dbBaseStats.MaxMC : Stats.MaxMC;
            int baseMinSC = _dbBaseStatsLoaded ? _dbBaseStats.MinSC : Stats.MinSC;
            int baseMaxSC = _dbBaseStatsLoaded ? _dbBaseStats.MaxSC : Stats.MaxSC;
            int baseMinAC = _dbBaseStatsLoaded ? _dbBaseStats.MinAC : Stats.MinAC;
            int baseMaxAC = _dbBaseStatsLoaded ? _dbBaseStats.MaxAC : Stats.MaxAC;
            int baseMinMAC = _dbBaseStatsLoaded ? _dbBaseStats.MinMAC : Stats.MinMAC;
            int baseMaxMAC = _dbBaseStatsLoaded ? _dbBaseStats.MaxMAC : Stats.MaxMAC;

            var info = new MirCommon.Database.CHARDBINFO
            {
                dwClientKey = 0,
                szName = Name ?? string.Empty,
                dwDBId = GetDBId(),
                mapid = (uint)Math.Max(0, MapId),
                x = X,
                y = Y,
                dwGold = Gold,
                dwYuanbao = Yuanbao,
                dwCurExp = Exp,
                wLevel = Level,
                btClass = Job,
                btHair = Hair,
                btSex = Sex,
                flag = 0,
                hp = (ushort)Math.Clamp(saveHp, 0, 65535),
                mp = (ushort)Math.Clamp(saveMp, 0, 65535),
                maxhp = (ushort)Math.Clamp(baseMaxHp, 0, 65535),
                maxmp = (ushort)Math.Clamp(baseMaxMp, 0, 65535),
                mindc = (byte)Math.Clamp(baseMinDC, 0, 255),
                maxdc = (byte)Math.Clamp(baseMaxDC, 0, 255),
                minmc = (byte)Math.Clamp(baseMinMC, 0, 255),
                maxmc = (byte)Math.Clamp(baseMaxMC, 0, 255),
                minsc = (byte)Math.Clamp(baseMinSC, 0, 255),
                maxsc = (byte)Math.Clamp(baseMaxSC, 0, 255),
                minac = (byte)Math.Clamp(baseMinAC, 0, 255),
                maxac = (byte)Math.Clamp(baseMaxAC, 0, 255),
                minmac = (byte)Math.Clamp(baseMinMAC, 0, 255),
                maxmac = (byte)Math.Clamp(baseMaxMAC, 0, 255),
                weight = _curBagWeight,
                handweight = _curHandWeight,
                bodyweight = _curBodyWeight,
                dwForgePoint = _forgePoint,
                szStartPoint = _startPointName,
                szGuildName = Guild?.Name ?? string.Empty,
            };

            
            
            info.dwFlag[0] = _dbFlag0;
            info.dwFlag[1] = PKSystem?.GetPkValue() ?? 0u; 
            info.dwFlag[2] = 0;
            info.dwFlag[3] = 0;

            
            for (int i = 0; i < info.dwProp.Length; i++)
                info.dwProp[i] = 0;

            
            _dbFlag0 = info.dwFlag[0];

            return info;
        }

        private MirCommon.Database.MAGICDB[] BuildMagicDbForSave()
        {
            var skills = SkillBook.GetAllSkills();
            if (skills.Count == 0)
                return Array.Empty<MirCommon.Database.MAGICDB>();

            var list = new List<MirCommon.Database.MAGICDB>(Math.Min(255, skills.Count));
            foreach (var s in skills)
            {
                if (list.Count >= 255)
                    break;

                var m = new MirCommon.Database.MAGICDB
                {
                    btUserKey = (byte)Math.Clamp((int)s.Key, 0, 255),
                    btCurLevel = (byte)Math.Clamp((int)s.Level, 0, 255),
                    wMagicId = (ushort)Math.Clamp(s.SkillId, 0, ushort.MaxValue),
                    dwCurTrain = (uint)Math.Clamp(s.UseCount, 0, int.MaxValue)
                };
                list.Add(m);
            }

            return list.ToArray();
        }

        private MirCommon.Database.DBITEM[] BuildDbItemsForBagSave()
        {
            var items = Inventory.GetAllItems();
            if (items.Count == 0)
                return Array.Empty<MirCommon.Database.DBITEM>();

            var list = new List<MirCommon.Database.DBITEM>(items.Count);
            foreach (var kvp in items)
            {
                int slot = kvp.Key;
                var inst = kvp.Value;
                if (inst == null)
                    continue;

                var item = BuildMirItemForDb(inst);
                list.Add(new MirCommon.Database.DBITEM
                {
                    item = item,
                    wPos = (ushort)Math.Clamp(slot, 0, ushort.MaxValue),
                    btFlag = (byte)ItemDataFlag.IDF_BAG
                });
            }

            return list.ToArray();
        }

        private MirCommon.Database.DBITEM[] BuildDbItemsForEquipmentSave()
        {
            var list = new List<MirCommon.Database.DBITEM>();

            for (int i = 0; i < (int)MirCommon.EquipPos.U_MAX; i++)
            {
                var inst = Equipment.GetEquipment((EquipSlot)i);
                if (inst == null)
                    continue;

                var item = BuildMirItemForDb(inst);
                list.Add(new MirCommon.Database.DBITEM
                {
                    item = item,
                    wPos = (ushort)i,
                    btFlag = (byte)ItemDataFlag.IDF_EQUIPMENT
                });
            }

            return list.ToArray();
        }

        private static MirCommon.Item BuildMirItemForDb(ItemInstance inst)
        {
            var baseItem = ItemPacketBuilder.BuildBaseItem(inst);

            return new MirCommon.Item
            {
                baseitem = baseItem,
                dwMakeIndex = unchecked((uint)inst.InstanceId),
                wCurDura = (ushort)Math.Clamp(inst.Durability, 0, ushort.MaxValue),
                wMaxDura = (ushort)Math.Clamp(inst.MaxDurability, 0, ushort.MaxValue),
                dwParam = new uint[4] { 0, 0, 0, 0 }
            };
        }

        
        
        
        private void SavePlayerInfoToDB()
        {
            
            

            
            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteString(Name);
            builder.WriteByte(Job);
            builder.WriteByte(Sex);
            builder.WriteByte(Hair);
            builder.WriteUInt16((ushort)Level);
            builder.WriteUInt32(Exp);
            builder.WriteUInt32(Gold);
            builder.WriteUInt32(Yuanbao);
            builder.WriteUInt16((ushort)CurrentHP);
            builder.WriteUInt16((ushort)MaxHP);
            builder.WriteUInt16((ushort)CurrentMP);
            builder.WriteUInt16((ushort)MaxMP);
            builder.WriteUInt16((ushort)X);
            builder.WriteUInt16((ushort)Y);
            builder.WriteUInt16((ushort)Direction);

            
            builder.WriteInt32(BaseDC);
            builder.WriteInt32(BaseMC);
            builder.WriteInt32(BaseSC);
            builder.WriteInt32(BaseAC);
            builder.WriteInt32(BaseMAC);
            builder.WriteInt32(Accuracy);
            builder.WriteInt32(Agility);
            builder.WriteInt32(Lucky);

            
            builder.WriteString(Guild?.Name ?? "");
            builder.WriteString(GuildGroupName);
            builder.WriteUInt32(GuildLevel);

            
            builder.WriteUInt32(PKSystem.GetPkValue());

            
            builder.WriteUInt64((ulong)LoginTime.Ticks);

            
            builder.WriteUInt64((ulong)LastActivity.Ticks);

            
            builder.WriteByte(IsFirstLogin ? (byte)1 : (byte)0);

            
            builder.WriteUInt32(GroupId);

            
            
            
            LogManager.Default.Debug($"保存玩家基本信息: {Name}");
        }

        
        
        
        private void UpdateItemsToDB()
        {
            try
            {
                
                SaveInventoryToDB();

                
                SaveEquipmentToDB();

                
                SavePetBagToDB();

                
                SaveBankToDB();

                LogManager.Default.Debug($"保存物品数据: {Name}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存物品数据失败: {Name}, 错误: {ex.Message}");
            }
        }

        
        
        
        private void SaveInventoryToDB()
        {
            var items = Inventory.GetAllItems();
            if (items.Count == 0)
                return;

            
            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x01); 

            
            builder.WriteUInt16((ushort)items.Count);

            
            foreach (var kvp in items)
            {
                var item = kvp.Value;
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteUInt32((uint)item.ItemId);
                builder.WriteUInt16((ushort)item.Count);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteByte((byte)item.EnhanceLevel);
                builder.WriteByte((byte)kvp.Key); 

                
                builder.WriteUInt32(0); 
                builder.WriteUInt32(0); 
                builder.WriteUInt32(0); 
            }

            
            
        }

        
        
        
        private void SaveEquipmentToDB()
        {
            var equipment = Equipment.GetAllEquipment();
            if (equipment.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x02); 

            builder.WriteUInt16((ushort)equipment.Count);

            foreach (var equip in equipment)
            {
                builder.WriteUInt64((ulong)equip.InstanceId);
                builder.WriteUInt32((uint)equip.ItemId);
                builder.WriteUInt16((ushort)equip.Durability);
                builder.WriteUInt16((ushort)equip.MaxDurability);
                builder.WriteByte((byte)equip.EnhanceLevel);
                builder.WriteByte((byte)0); 

                
                builder.WriteUInt32(0); 
                builder.WriteUInt32(0); 
                builder.WriteUInt32(0); 
            }
        }

        
        
        
        private void SavePetBagToDB()
        {
            var petBag = PetSystem.GetPetBag();
            var items = petBag.GetAllItems();
            if (items.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x03); 

            builder.WriteUInt16((ushort)items.Count);

            foreach (var item in items.Values)
            {
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteUInt32((uint)item.ItemId);
                builder.WriteUInt16((ushort)item.Count);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteByte((byte)item.EnhanceLevel);
                builder.WriteByte(0); 

                builder.WriteUInt32(0); 
                builder.WriteUInt32(0); 
                builder.WriteUInt32(0); 
            }
        }

        
        
        
        private void SaveBankToDB()
        {
            
            
        }

        
        
        
        private void UpdateSkillsToDB()
        {
            var skills = SkillBook.GetAllSkills();
            if (skills.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x04); 

            builder.WriteUInt16((ushort)skills.Count);

            foreach (var skill in skills)
            {
                builder.WriteUInt32((uint)skill.Definition.SkillId);
                builder.WriteUInt16((ushort)skill.Level);
                builder.WriteUInt32((uint)skill.UseCount); 
                builder.WriteUInt32((uint)skill.UseCount);
                builder.WriteByte(0); 
            }
        }

        
        
        
        private void UpdateTasksToDB()
        {
            
            
        }

        
        
        
        private void UpdateMailsToDB()
        {
            var mails = MailSystem.GetMails();
            if (mails.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x05); 

            builder.WriteUInt16((ushort)mails.Count);

            foreach (var mail in mails)
            {
                builder.WriteUInt32(mail.Id);
                builder.WriteString(mail.Sender);
                builder.WriteString(mail.Receiver);
                builder.WriteString(mail.Title);
                builder.WriteString(mail.Content);
                builder.WriteUInt64((ulong)mail.SendTime.Ticks);
                builder.WriteByte(mail.IsRead ? (byte)1 : (byte)0);
                builder.WriteByte(mail.AttachmentsClaimed ? (byte)1 : (byte)0);

                
                if (mail.Attachments != null && mail.Attachments.Count > 0)
                {
                    builder.WriteByte((byte)mail.Attachments.Count);
                    foreach (var attachment in mail.Attachments)
                    {
                        builder.WriteUInt64((ulong)attachment.InstanceId);
                        builder.WriteUInt32((uint)attachment.ItemId);
                        builder.WriteUInt16((ushort)attachment.Count);
                    }
                }
                else
                {
                    builder.WriteByte(0);
                }
            }
        }

        
        
        
        private void UpdateAchievementsToDB()
        {
            var achievements = AchievementSystem.GetAchievements();
            if (achievements.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x06); 

            builder.WriteUInt16((ushort)achievements.Count);

            foreach (var achievement in achievements)
            {
                builder.WriteUInt32(achievement.Id);
                builder.WriteByte(achievement.Completed ? (byte)1 : (byte)0);
                if (achievement.CompletedTime.HasValue)
                {
                    builder.WriteUInt64((ulong)achievement.CompletedTime.Value.Ticks);
                }
                else
                {
                    builder.WriteUInt64(0);
                }
            }
        }

        
        
        
        private void UpdatePetsToDB()
        {
            
            
        }

        
        
        
        private void UpdateMountToDB()
        {
            var mount = MountSystem.GetHorse();
            if (mount == null)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x07); 

            builder.WriteString(mount.Name);
            builder.WriteUInt16((ushort)mount.Level);
            builder.WriteUInt16((ushort)mount.CurrentHP);
            builder.WriteUInt16((ushort)mount.MaxHP);
            builder.WriteByte(MountSystem.IsRiding() ? (byte)1 : (byte)0);
            builder.WriteByte(MountSystem.IsHorseRest() ? (byte)1 : (byte)0);
        }

        
        
        
        private void UpdatePKDataToDB()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x08); 

            builder.WriteUInt32(PKSystem.GetPkValue());
            builder.WriteByte(PKSystem.IsSelfDefense() ? (byte)1 : (byte)0);
        }

        
        
        
        private void CheckAndSaveToDB()
        {
            
            if ((DateTime.Now - LastActivity).TotalMinutes >= 5)
            {
                UpdateToDB();
                LastActivity = DateTime.Now;
            }
        }

        #endregion
    }

}
