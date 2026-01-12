using System;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public enum SystemFlag
    {
        NoDamage = 0,       
        NoTarget = 1,       
        Invisible = 2,      
        NoMove = 3,         
        NoAttack = 4,       
        NoMagic = 5,        
        NoItem = 6,         
        NoChat = 7,         
        NoTrade = 8,        
        NoDrop = 9,         
        NoPickup = 10,      
        NoRecall = 11,      
        NoTrans = 12,       
        NoFly = 13,         
        NoDie = 14,         
        NoExp = 15,         
        NoPK = 16,          
        NoGuild = 17,       
        NoParty = 18,       
        NoFriend = 19,      
        NoMail = 20,        
        NoAuction = 21,     
        NoMarket = 22,      
        NoStall = 23,       
        NoQuest = 24,       
        NoSkill = 25,       
        NoBuff = 26,        
        NoDebuff = 27,      
        NoHeal = 28,        
        NoResurrect = 29,   
        NoSummon = 30,      
        NoPet = 31,         
        NoMount = 32,       
        NoTransform = 33,   
        NoDisguise = 34,    
        NoStealth = 35,     
        NoSneak = 36,       
        NoBackstab = 37,    
        NoCritical = 38,    
        NoDodge = 39,       
        NoBlock = 40,       
        NoParry = 41,       
        NoCounter = 42,     
        NoReflect = 43,     
        NoAbsorb = 44,      
        NoReduce = 45,      
        NoIgnore = 46,      
        NoPenetrate = 47,   
        NoSplash = 48,      
        NoChain = 49,       
        NoBounce = 50,      
        NoSpread = 51,      
        NoExplode = 52,     
        NoImplode = 53,     
        NoVortex = 54,      
        NoBlackhole = 55,   
        NoGravity = 56,     
        NoTime = 57,        
        NoSpace = 58,       
        NoDimension = 59,   
        NoReality = 60,     
        NoDream = 61,       
        NoIllusion = 62,    
        NoMirage = 63,      
        NoPhantom = 64,     
        NoGhost = 65,       
        NoSpirit = 66,      
        NoDemon = 67,       
        NoAngel = 68,       
        NoGod = 69,         
        NoDevil = 70,       
        NoDragon = 71,      
        NoPhoenix = 72,     
        NoUnicorn = 73,     
        NoGriffin = 74,     
        NoPegasus = 75,     
        NoMermaid = 76,     
        NoSiren = 77,       
        NoHydra = 78,       
        NoChimera = 79,     
        NoCerberus = 80,    
        NoMinotaur = 81,    
        NoCyclops = 82,     
        NoGiant = 83,       
        NoTitan = 84,       
        NoGolem = 85,       
        NoElemental = 86,   
        NoUndead = 87,      
        NoConstruct = 88,   
        NoAberration = 89,  
        NoBeast = 90,       
        NoHumanoid = 91,    
        NoMonstrosity = 92, 
        NoOoze = 93,        
        NoPlant = 94,       
        NoVermin = 95,      
        NoMax = 96
    }

    
    
    
    
    public class CSCPalaceDoor
    {
        private uint _fromMapId;
        private uint _fromX;
        private uint _fromY;
        private uint _toMapId;
        private uint _toX;
        private uint _toY;

        public CSCPalaceDoor()
        {
        }

        
        
        
        public bool Create(uint fromMapId, uint fromX, uint fromY, uint toMapId, uint toX, uint toY)
        {
            _fromMapId = fromMapId;
            _fromX = fromX;
            _fromY = fromY;
            _toMapId = toMapId;
            _toX = toX;
            _toY = toY;

            LogManager.Default.Info($"创建沙城皇宫入口门点: 从({fromMapId},{fromX},{fromY}) 到({toMapId},{toX},{toY})");
            return true;
        }

        
        
        
        public uint GetFromMapId() => _fromMapId;

        
        
        
        public uint GetFromX() => _fromX;

        
        
        
        public uint GetFromY() => _fromY;

        
        
        
        public uint GetToMapId() => _toMapId;

        
        
        
        public uint GetToX() => _toX;

        
        
        
        public uint GetToY() => _toY;
    }

    
    
    
    
    public class CSCDoor : AliveObject
    {
        private string _name = string.Empty;
        private uint _hp;
        private bool _isOpened;

        public CSCDoor()
        {
        }

        
        
        
        public bool Init(string name, uint mapId, uint x, uint y, uint hp, bool isOpened = false)
        {
            _name = name;
            _hp = hp;
            _isOpened = isOpened;

            
            MapId = (int)mapId;
            X = (ushort)x;
            Y = (ushort)y;

            LogManager.Default.Info($"初始化城门: {name}, 位置({mapId},{x},{y}), HP={hp}, 状态={(isOpened ? "开启" : "关闭")}");
            return true;
        }

        
        
        
        public void Open()
        {
            _isOpened = true;
            LogManager.Default.Info($"城门 {_name} 已打开");
        }

        
        
        
        public void Close()
        {
            _isOpened = false;
            LogManager.Default.Info($"城门 {_name} 已关闭");
        }

        
        
        
        public void Repair()
        {
            _hp = GetMaxHp();
            LogManager.Default.Info($"城门 {_name} 已修复，HP={_hp}");
        }

        
        
        
        public bool IsOpened() => _isOpened;

        
        
        
        public string GetName() => _name;

        
        
        
        public uint GetHp() => _hp;

        
        
        
        public uint GetMaxHp() => 10000; 

        
        
        
        public void SetSystemFlag(SystemFlag flag, bool value)
        {
            
            LogManager.Default.Debug($"城门 {_name} 设置系统标志 {flag} = {value}");
        }

        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.Event; 
        }

        
        
        
        public uint GetPropValue(PropIndex index)
        {
            switch (index)
            {
                case PropIndex.CurHp:
                    return _hp;
                default:
                    return 0;
            }
        }

        
        
        
        public object GetDesc()
        {
            return new { Base = new { ClassName = _name } };
        }

        
        
        
        public int GetX() => X;

        
        
        
        public int GetY() => Y;
    }

    
    
    
    
    public class CPalaceWall : AliveObject
    {
        private string _name = string.Empty;
        private uint _hp;

        public CPalaceWall()
        {
        }

        
        
        
        public bool Init(string name, uint mapId, uint x, uint y, uint hp)
        {
            _name = name;
            _hp = hp;

            
            MapId = (int)mapId;
            X = (ushort)x;
            Y = (ushort)y;

            LogManager.Default.Info($"初始化城墙: {name}, 位置({mapId},{x},{y}), HP={hp}");
            return true;
        }

        
        
        
        public void Repair()
        {
            _hp = GetMaxHp();
            LogManager.Default.Info($"城墙 {_name} 已修复，HP={_hp}");
        }

        
        
        
        public string GetName() => _name;

        
        
        
        public uint GetHp() => _hp;

        
        
        
        public uint GetMaxHp() => 5000; 

        
        
        
        public void SetSystemFlag(SystemFlag flag, bool value)
        {
            
            LogManager.Default.Debug($"城墙 {_name} 设置系统标志 {flag} = {value}");
        }

        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.Event; 
        }

        
        
        
        public uint GetPropValue(PropIndex index)
        {
            switch (index)
            {
                case PropIndex.CurHp:
                    return _hp;
                default:
                    return 0;
            }
        }

        
        
        
        public object GetDesc()
        {
            return new { Base = new { ClassName = _name } };
        }

        
        
        
        public int GetX() => X;

        
        
        
        public int GetY() => Y;
    }

    
    
    
    
    public class CSCArcher : AliveObject
    {
        private string _name = string.Empty;
        private uint _hp;

        public CSCArcher()
        {
        }

        
        
        
        public bool Init(string name, uint mapId, uint x, uint y, uint hp)
        {
            _name = name;
            _hp = hp;

            
            MapId = (int)mapId;
            X = (ushort)x;
            Y = (ushort)y;

            LogManager.Default.Info($"初始化弓箭手: {name}, 位置({mapId},{x},{y}), HP={hp}");
            return true;
        }

        
        
        
        public string GetName() => _name;

        
        
        
        public uint GetHp() => _hp;

        
        
        
        public uint GetMaxHp() => 1000; 

        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.NPC; 
        }

        
        
        
        public uint GetPropValue(PropIndex index)
        {
            switch (index)
            {
                case PropIndex.CurHp:
                    return _hp;
                default:
                    return 0;
            }
        }

        
        
        
        public object GetDesc()
        {
            return new { Base = new { ClassName = _name } };
        }

        
        
        
        public int GetX() => X;

        
        
        
        public int GetY() => Y;
    }

    
    
    
    public enum PropIndex
    {
        CurHp = 0,      
        MaxHp = 1,      
        CurMp = 2,      
        MaxMp = 3,      
        Level = 4,      
        Exp = 5,        
        AC = 6,         
        MAC = 7,        
        DC = 8,         
        MC = 9,         
        SC = 10,        
        Hit = 11,       
        Speed = 12,     
        Luck = 13,      
        Curse = 14,     
        Accuracy = 15,  
        Agility = 16,   
        Max = 17
    }
}
