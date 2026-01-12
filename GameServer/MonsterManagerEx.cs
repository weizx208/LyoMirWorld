using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class MonsterManagerEx
    {
        private static MonsterManagerEx? _instance;
        public static MonsterManagerEx Instance => _instance ??= new MonsterManagerEx();

        
        private readonly Dictionary<string, MonsterClass> _monsterClassHash = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _classLock = new();

        
        private readonly List<MonsterEx> _monsterList = new();
        private readonly Dictionary<uint, MonsterEx> _monsterById = new();
        private readonly object _monsterLock = new();
        private uint _nextMonsterSeq = 0;

        
        private MonsterEx? _activeMonster;

        
        private readonly Queue<MonsterEx> _deleteQueue = new();
        private readonly object _deleteLock = new();
        private const int DeleteQueueLimit = 2000;

        private MonsterManagerEx()
        {
            
        }

        
        
        
        
        public bool LoadMonsters(string fileName)
        {
            LogManager.Default.Info($"加载怪物定义文件: {fileName}");

            if (!File.Exists(fileName))
            {
                LogManager.Default.Error($"怪物定义文件不存在: {fileName}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(fileName);
                MonsterClass? currentClass = null;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    if (trimmedLine.StartsWith("@"))
                    {
                        
                        if (currentClass != null)
                        {
                            
                            SaveMonsterClass(currentClass);
                        }

                        
                        currentClass = new MonsterClass();
                        
                        
                        string className = trimmedLine.Substring(1).Trim();
                        if (className.Length > 16)
                            className = className.Substring(0, 16);

                        currentClass.Base.ClassName = className;
                        currentClass.Base.ViewName = string.Empty;
                        currentClass.Base.Race = 0;
                        currentClass.Base.Image = 0;
                        currentClass.Base.Level = 0;
                        currentClass.Base.NameColor = 0;
                        currentClass.Base.Feature = 0;
                    }
                    else if (currentClass != null)
                    {
                        
                        ParsePropertyLine(currentClass, trimmedLine);
                    }
                }

                
                if (currentClass != null)
                {
                    SaveMonsterClass(currentClass);
                }

                LogManager.Default.Info($"成功加载 {_monsterClassHash.Count} 个怪物定义");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物定义文件失败: {fileName}", exception: ex);
                return false;
            }
        }

        
        
        
        
        public void LoadMonsterScript(string fileName)
        {
            LogManager.Default.Info($"加载怪物脚本文件: {fileName}");

            if (!File.Exists(fileName))
            {
                LogManager.Default.Warning($"怪物脚本文件不存在: {fileName}");
                return;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(fileName);
                int count = 0;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    
                    var parts = trimmedLine.Split('=', 2);
                    if (parts.Length != 2)
                        continue;

                    string monsterName = parts[0].Trim();
                    string scriptData = parts[1].Trim();

                    var monsterClass = GetClassByName(monsterName);
                    if (monsterClass == null)
                    {
                        LogManager.Default.Debug($"怪物类未找到: {monsterName}");
                        continue;
                    }

                    var scriptParts = scriptData.Split(',');
                    
                    
                    monsterClass.BornScript = null;
                    monsterClass.GotTargetScript = null;
                    monsterClass.KillTargetScript = null;
                    monsterClass.HurtScript = null;
                    monsterClass.DeathScript = null;

                    
                    if (scriptParts.Length > 0 && !string.IsNullOrEmpty(scriptParts[0]))
                        monsterClass.BornScript = scriptParts[0];
                    if (scriptParts.Length > 1 && !string.IsNullOrEmpty(scriptParts[1]))
                        monsterClass.GotTargetScript = scriptParts[1];
                    if (scriptParts.Length > 2 && !string.IsNullOrEmpty(scriptParts[2]))
                        monsterClass.KillTargetScript = scriptParts[2];
                    if (scriptParts.Length > 3 && !string.IsNullOrEmpty(scriptParts[3]))
                        monsterClass.HurtScript = scriptParts[3];
                    if (scriptParts.Length > 4 && !string.IsNullOrEmpty(scriptParts[4]))
                        monsterClass.DeathScript = scriptParts[4];

                    count++;
                }

                LogManager.Default.Info($"成功加载 {count} 个怪物脚本");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物脚本文件失败: {fileName}", exception: ex);
            }
        }

        
        
        
        
        public MonsterEx? CreateMonster(string monsterName, int mapId, int x, int y, MonsterGen? gen = null)
        {
            var monsterClass = GetClassByName(monsterName);
            if (monsterClass == null)
            {
                LogManager.Default.Warning($"怪物类未找到: {monsterName}");
                return null;
            }

            var monster = new MonsterEx();
            
            monster.SetId(GetNextObjectId());
            if (!monster.Init(monsterClass, mapId, x, y, gen))
            {
                LogManager.Default.Warning($"初始化怪物失败: {monsterName}");
                return null;
            }

            
            lock (_monsterLock)
            {
                _monsterList.Add(monster);
                _monsterById[monster.ObjectId] = monster;
            }

            
            monsterClass.Count++;

            
            GameWorld.Instance.AddUpdateMonster(monster);

            return monster;
        }

        
        
        
        public MonsterEx? CreateMonster(int monsterId)
        {
            
            var monsterClass = GetMonsterClass(monsterId);
            if (monsterClass == null)
            {
                LogManager.Default.Warning($"怪物类未找到: {monsterId}");
                return null;
            }

            return new MonsterEx();
        }

        
        
        
        
        public bool DeleteMonster(MonsterEx monster)
        {
            if (monster == null)
                return false;

            
            monster.ClearGen();

            
            lock (_monsterLock)
            {
                _monsterList.Remove(monster);
                _monsterById.Remove(monster.ObjectId);
            }

            
            monster.Clean();

            return true;
        }

        
        
        
        
        public bool DeleteMonsterDelayed(MonsterEx monster)
        {
            if (monster == null)
                return false;

            
            monster.ClearGen();
            monster.SetDelTimer();

            bool deleteNow = false;
            lock (_deleteLock)
            {
                if (_deleteQueue.Count >= DeleteQueueLimit)
                {
                    deleteNow = true;
                }
                else
                {
                    _deleteQueue.Enqueue(monster);
                }
            }

            return deleteNow ? DeleteMonster(monster) : true;
        }

        
        
        
        public void UpdateDeleteMonster()
        {
            MonsterEx? monster = null;
            lock (_deleteLock)
            {
                if (_deleteQueue.Count > 0)
                    monster = _deleteQueue.Dequeue();
            }

            if (monster == null)
                return;

            try
            {
                if (monster.IsDelTimerTimeOut(10000))
                {
                    DeleteMonster(monster);
                    return;
                }

                lock (_deleteLock)
                {
                    if (_deleteQueue.Count >= DeleteQueueLimit)
                    {
                        
                        DeleteMonster(monster);
                    }
                    else
                    {
                        _deleteQueue.Enqueue(monster);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"更新删除怪物队列异常: monster={monster.Name}({monster.ObjectId:X8})", exception: ex);
                try { DeleteMonster(monster); } catch { }
            }
        }

        
        
        
        
        public bool AddExtraMonster(MonsterEx monster)
        {
            if (monster == null)
                return false;

            
            
            if (monster.ObjectId == 0 || ObjectIdUtil.GetType(monster.ObjectId) != MirObjectType.Monster)
            {
                monster.SetId(GetNextObjectId());
            }

            lock (_monsterLock)
            {
                _monsterList.Add(monster);
                _monsterById[monster.ObjectId] = monster;
            }

            GameWorld.Instance.AddUpdateMonster(monster);
            return true;
        }

        
        
        
        
        public MonsterEx? GetMonsterById(uint id)
        {
            lock (_monsterLock)
            {
                return _monsterById.TryGetValue(id, out var monster) ? monster : null;
            }
        }

        
        
        
        
        public MonsterClass? GetClassByName(string name)
        {
            lock (_classLock)
            {
                return _monsterClassHash.TryGetValue(name, out var monsterClass) ? monsterClass : null;
            }
        }

        
        
        
        
        public int GetCount()
        {
            lock (_monsterLock)
            {
                return _monsterList.Count;
            }
        }

        
        
        
        
        public MonsterEx? GetCurrentActiveMonster()
        {
            return _activeMonster;
        }

        
        
        
        
        public void SetCurrentActiveMonster(MonsterEx? monster)
        {
            _activeMonster = monster;
        }

        
        
        
        public List<MonsterClass> GetAllMonsterClasses()
        {
            lock (_classLock)
            {
                return _monsterClassHash.Values.ToList();
            }
        }

        
        
        
        public List<MonsterEx> GetAllMonsters()
        {
            lock (_monsterLock)
            {
                return new List<MonsterEx>(_monsterList);
            }
        }

        
        
        
        public void ClearAllMonsters()
        {
            lock (_monsterLock)
            {
                foreach (var monster in _monsterList)
                {
                    monster.Clean();
                }
                _monsterList.Clear();
                _monsterById.Clear();
                _nextMonsterSeq = 0;
            }
        }

        
        
        
        private void SaveMonsterClass(MonsterClass monsterClass)
        {
            if (string.IsNullOrEmpty(monsterClass.Base.ClassName))
                return;

            lock (_classLock)
            {
                if (_monsterClassHash.TryGetValue(monsterClass.Base.ClassName, out var existingClass))
                {
                    
                    
                    monsterClass.BornScript = existingClass.BornScript;
                    monsterClass.GotTargetScript = existingClass.GotTargetScript;
                    monsterClass.KillTargetScript = existingClass.KillTargetScript;
                    monsterClass.HurtScript = existingClass.HurtScript;
                    monsterClass.DeathScript = existingClass.DeathScript;

                    
                    CopyMonsterClass(existingClass, monsterClass);
                    LogManager.Default.Info($"怪物 {monsterClass.Base.ClassName} 被更新");
                }
                else
                {
                    
                    _monsterClassHash[monsterClass.Base.ClassName] = monsterClass;
                }
            }
        }

        
        
        
        private void CopyMonsterClass(MonsterClass dest, MonsterClass src)
        {
            
            dest.Base.ViewName = src.Base.ViewName;
            dest.Base.Race = src.Base.Race;
            dest.Base.Image = src.Base.Image;
            dest.Base.Level = src.Base.Level;
            dest.Base.NameColor = src.Base.NameColor;
            dest.Base.Feature = src.Base.Feature;

            
            dest.Prop.HP = src.Prop.HP;
            dest.Prop.MP = src.Prop.MP;
            dest.Prop.Hit = src.Prop.Hit;
            dest.Prop.Speed = src.Prop.Speed;
            dest.Prop.AC1 = src.Prop.AC1;
            dest.Prop.AC2 = src.Prop.AC2;
            dest.Prop.DC1 = src.Prop.DC1;
            dest.Prop.DC2 = src.Prop.DC2;
            dest.Prop.MAC1 = src.Prop.MAC1;
            dest.Prop.MAC2 = src.Prop.MAC2;
            dest.Prop.MC1 = src.Prop.MC1;
            dest.Prop.MC2 = src.Prop.MC2;
            dest.Prop.Exp = src.Prop.Exp;
            dest.Prop.AIDelay = src.Prop.AIDelay;
            dest.Prop.WalkDelay = src.Prop.WalkDelay;
            dest.Prop.RecoverHP = src.Prop.RecoverHP;
            dest.Prop.RecoverHPTime = src.Prop.RecoverHPTime;
            dest.Prop.RecoverMP = src.Prop.RecoverMP;
            dest.Prop.RecoverMPTime = src.Prop.RecoverMPTime;

            
            dest.SProp.PFlag = src.SProp.PFlag;
            dest.SProp.CallRate = src.SProp.CallRate;
            dest.SProp.AntSoulWall = src.SProp.AntSoulWall;
            dest.SProp.AntTrouble = src.SProp.AntTrouble;
            dest.SProp.AntHolyWord = src.SProp.AntHolyWord;

            
            dest.AISet.MoveStyle = src.AISet.MoveStyle;
            dest.AISet.DieStyle = src.AISet.DieStyle;
            dest.AISet.TargetSelect = src.AISet.TargetSelect;
            dest.AISet.TargetFlag = src.AISet.TargetFlag;
            dest.AISet.ViewDistance = src.AISet.ViewDistance;
            dest.AISet.CoolEyes = src.AISet.CoolEyes;
            dest.AISet.EscapeDistance = src.AISet.EscapeDistance;
            dest.AISet.LockDir = src.AISet.LockDir;

            
            dest.PetSet.Type = src.PetSet.Type;
            dest.PetSet.StopAt = src.PetSet.StopAt;

            
            dest.AttackDesc.AttackStyle = src.AttackDesc.AttackStyle;
            dest.AttackDesc.AttackDistance = src.AttackDesc.AttackDistance;
            dest.AttackDesc.Delay = src.AttackDesc.Delay;
            dest.AttackDesc.DamageStyle = src.AttackDesc.DamageStyle;
            dest.AttackDesc.DamageRange = src.AttackDesc.DamageRange;
            dest.AttackDesc.DamageType = src.AttackDesc.DamageType;
            dest.AttackDesc.AppendEffect = src.AttackDesc.AppendEffect;
            dest.AttackDesc.AppendRate = src.AttackDesc.AppendRate;
            dest.AttackDesc.CostHP = src.AttackDesc.CostHP;
            dest.AttackDesc.CostMP = src.AttackDesc.CostMP;
            dest.AttackDesc.Action = src.AttackDesc.Action;
            dest.AttackDesc.AppendTime = src.AttackDesc.AppendTime;

            
            for (int i = 0; i < 3; i++)
            {
                dest.ChangeInto[i].Situation1.Situation = src.ChangeInto[i].Situation1.Situation;
                dest.ChangeInto[i].Situation1.Param = src.ChangeInto[i].Situation1.Param;
                dest.ChangeInto[i].Situation2.Situation = src.ChangeInto[i].Situation2.Situation;
                dest.ChangeInto[i].Situation2.Param = src.ChangeInto[i].Situation2.Param;
                dest.ChangeInto[i].ChangeInto = src.ChangeInto[i].ChangeInto;
                dest.ChangeInto[i].AppendEffect = src.ChangeInto[i].AppendEffect;
                dest.ChangeInto[i].Anim = src.ChangeInto[i].Anim;
                dest.ChangeInto[i].Enabled = src.ChangeInto[i].Enabled;
            }

            
            dest.DownItems = src.DownItems;
        }

        
        
        
        
        private void ParsePropertyLine(MonsterClass monsterClass, string line)
        {
            try
            {
                
                int commentIndex = line.IndexOf('#');
                if (commentIndex >= 0)
                {
                    line = line.Substring(0, commentIndex).Trim();
                }

                if (string.IsNullOrEmpty(line))
                    return;

                
                var parts = line.Split(':', 2);
                if (parts.Length != 2)
                {
                    LogManager.Default.Debug($"无效的属性行格式: {line}");
                    return;
                }

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                
                switch (key.ToLower())
                {
                    case "base":
                        ParseBaseProperty(monsterClass, value);
                        break;
                    case "prop":
                        ParsePropProperty(monsterClass, value);
                        break;
                    case "sprop":
                        ParseSPropProperty(monsterClass, value);
                        break;
                    case "aiset":
                        ParseAISetProperty(monsterClass, value);
                        break;
                    case "petset":
                        ParsePetSetProperty(monsterClass, value);
                        break;
                    case "attack":
                        ParseAttackProperty(monsterClass, value);
                        break;
                    case "append":
                        ParseAppendProperty(monsterClass, value);
                        break;
                    case "chg1":
                        ParseChangeIntoProperty(monsterClass, 0, value);
                        break;
                    case "chg2":
                        ParseChangeIntoProperty(monsterClass, 1, value);
                        break;
                    case "chg3":
                        ParseChangeIntoProperty(monsterClass, 2, value);
                        break;
                    case "viewname":
                        monsterClass.Base.ViewName = value;
                        break;
                    case "race":
                        if (byte.TryParse(value, out byte race))
                            monsterClass.Base.Race = race;
                        break;
                    case "image":
                        if (byte.TryParse(value, out byte image))
                            monsterClass.Base.Image = image;
                        break;
                    case "level":
                        if (byte.TryParse(value, out byte level))
                            monsterClass.Base.Level = level;
                        break;
                    case "namecolor":
                        if (byte.TryParse(value, out byte nameColor))
                            monsterClass.Base.NameColor = nameColor;
                        break;
                    case "feature":
                        if (uint.TryParse(value, out uint feature))
                            monsterClass.Base.Feature = feature;
                        break;
                    case "hp":
                        if (ushort.TryParse(value, out ushort hp))
                            monsterClass.Prop.HP = hp;
                        break;
                    case "mp":
                        if (ushort.TryParse(value, out ushort mp))
                            monsterClass.Prop.MP = mp;
                        break;
                    case "hit":
                        if (byte.TryParse(value, out byte hit))
                            monsterClass.Prop.Hit = hit;
                        break;
                    case "speed":
                        if (byte.TryParse(value, out byte speed))
                            monsterClass.Prop.Speed = speed;
                        break;
                    case "ac1":
                        if (byte.TryParse(value, out byte ac1))
                            monsterClass.Prop.AC1 = ac1;
                        break;
                    case "ac2":
                        if (byte.TryParse(value, out byte ac2))
                            monsterClass.Prop.AC2 = ac2;
                        break;
                    case "dc1":
                        if (byte.TryParse(value, out byte dc1))
                            monsterClass.Prop.DC1 = dc1;
                        break;
                    case "dc2":
                        if (byte.TryParse(value, out byte dc2))
                            monsterClass.Prop.DC2 = dc2;
                        break;
                    case "mac1":
                        if (byte.TryParse(value, out byte mac1))
                            monsterClass.Prop.MAC1 = mac1;
                        break;
                    case "mac2":
                        if (byte.TryParse(value, out byte mac2))
                            monsterClass.Prop.MAC2 = mac2;
                        break;
                    case "mc1":
                        if (byte.TryParse(value, out byte mc1))
                            monsterClass.Prop.MC1 = mc1;
                        break;
                    case "mc2":
                        if (byte.TryParse(value, out byte mc2))
                            monsterClass.Prop.MC2 = mc2;
                        break;
                    case "exp":
                        if (uint.TryParse(value, out uint exp))
                            monsterClass.Prop.Exp = exp;
                        break;
                    case "aidelay":
                        if (ushort.TryParse(value, out ushort aiDelay))
                            monsterClass.Prop.AIDelay = aiDelay;
                        break;
                    case "walkdelay":
                        if (ushort.TryParse(value, out ushort walkDelay))
                            monsterClass.Prop.WalkDelay = walkDelay;
                        break;
                    case "recoverhp":
                        if (ushort.TryParse(value, out ushort recoverHP))
                            monsterClass.Prop.RecoverHP = recoverHP;
                        break;
                    case "recoverhptime":
                        if (ushort.TryParse(value, out ushort recoverHPTime))
                            monsterClass.Prop.RecoverHPTime = recoverHPTime;
                        break;
                    case "recovermp":
                        if (ushort.TryParse(value, out ushort recoverMP))
                            monsterClass.Prop.RecoverMP = recoverMP;
                        break;
                    case "recovermptime":
                        if (ushort.TryParse(value, out ushort recoverMPTime))
                            monsterClass.Prop.RecoverMPTime = recoverMPTime;
                        break;
                    case "pflag":
                        if (uint.TryParse(value, out uint pFlag))
                            monsterClass.SProp.PFlag = pFlag;
                        break;
                    case "callrate":
                        if (byte.TryParse(value, out byte callRate))
                            monsterClass.SProp.CallRate = callRate;
                        break;
                    case "antsoulwall":
                        if (byte.TryParse(value, out byte antSoulWall))
                            monsterClass.SProp.AntSoulWall = antSoulWall;
                        break;
                    case "anttrouble":
                        if (byte.TryParse(value, out byte antTrouble))
                            monsterClass.SProp.AntTrouble = antTrouble;
                        break;
                    case "antholyword":
                        if (byte.TryParse(value, out byte antHolyWord))
                            monsterClass.SProp.AntHolyWord = antHolyWord;
                        break;
                    case "movestyle":
                        if (byte.TryParse(value, out byte moveStyle))
                            monsterClass.AISet.MoveStyle = moveStyle;
                        break;
                    case "diestyle":
                        if (byte.TryParse(value, out byte dieStyle))
                            monsterClass.AISet.DieStyle = dieStyle;
                        break;
                    case "targetselect":
                        if (byte.TryParse(value, out byte targetSelect))
                            monsterClass.AISet.TargetSelect = targetSelect;
                        break;
                    case "targetflag":
                        if (byte.TryParse(value, out byte targetFlag))
                            monsterClass.AISet.TargetFlag = targetFlag;
                        break;
                    case "viewdistance":
                        if (byte.TryParse(value, out byte viewDistance))
                            monsterClass.AISet.ViewDistance = viewDistance;
                        break;
                    case "cooleyes":
                        if (byte.TryParse(value, out byte coolEyes))
                            monsterClass.AISet.CoolEyes = coolEyes;
                        break;
                    case "escapedistance":
                        if (byte.TryParse(value, out byte escapeDistance))
                            monsterClass.AISet.EscapeDistance = escapeDistance;
                        break;
                    case "lockdir":
                        if (byte.TryParse(value, out byte lockDir))
                            monsterClass.AISet.LockDir = lockDir;
                        break;
                    case "pettype":
                        if (byte.TryParse(value, out byte petType))
                            monsterClass.PetSet.Type = petType;
                        break;
                    case "petstopat":
                        if (byte.TryParse(value, out byte petStopAt))
                            monsterClass.PetSet.StopAt = petStopAt;
                        break;
                    case "attackstyle":
                        if (int.TryParse(value, out int attackStyle))
                            monsterClass.AttackDesc.AttackStyle = attackStyle;
                        break;
                    case "attackdistance":
                        if (int.TryParse(value, out int attackDistance))
                            monsterClass.AttackDesc.AttackDistance = attackDistance;
                        break;
                    case "delay":
                        if (int.TryParse(value, out int delay))
                            monsterClass.AttackDesc.Delay = delay;
                        break;
                    case "damagestyle":
                        if (int.TryParse(value, out int damageStyle))
                            monsterClass.AttackDesc.DamageStyle = damageStyle;
                        break;
                    case "damagerange":
                        if (int.TryParse(value, out int damageRange))
                            monsterClass.AttackDesc.DamageRange = damageRange;
                        break;
                    case "damagetype":
                        if (int.TryParse(value, out int damageType))
                            monsterClass.AttackDesc.DamageType = damageType;
                        break;
                    case "appendeffect":
                        if (int.TryParse(value, out int appendEffect))
                            monsterClass.AttackDesc.AppendEffect = appendEffect;
                        break;
                    case "appendrate":
                        if (int.TryParse(value, out int appendRate))
                            monsterClass.AttackDesc.AppendRate = appendRate;
                        break;
                    case "costhp":
                        if (int.TryParse(value, out int costHP))
                            monsterClass.AttackDesc.CostHP = costHP;
                        break;
                    case "costmp":
                        if (int.TryParse(value, out int costMP))
                            monsterClass.AttackDesc.CostMP = costMP;
                        break;
                    case "action":
                        if (ushort.TryParse(value, out ushort action))
                            monsterClass.AttackDesc.Action = action;
                        break;
                    case "appendtime":
                        if (ushort.TryParse(value, out ushort appendTime))
                            monsterClass.AttackDesc.AppendTime = appendTime;
                        break;
                    case "bornscript":
                        monsterClass.BornScript = value;
                        break;
                    case "gottargetscript":
                        monsterClass.GotTargetScript = value;
                        break;
                    case "killtargetscript":
                        monsterClass.KillTargetScript = value;
                        break;
                    case "hurtscript":
                        monsterClass.HurtScript = value;
                        break;
                    case "deathscript":
                        monsterClass.DeathScript = value;
                        break;
                    default:
                        
                        if (key.StartsWith("changeinto", StringComparison.OrdinalIgnoreCase))
                        {
                            ParseChangeInto(monsterClass, key, value);
                        }
                        else
                        {
                            LogManager.Default.Debug($"未知的属性键: {key}");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析属性行失败: {line}", exception: ex);
            }
        }

        
        
        
        
        private void ParseBaseProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1)
                    monsterClass.Base.ViewName = parts[0];
                if (parts.Length >= 2 && byte.TryParse(parts[1], out byte race))
                    monsterClass.Base.Race = race;
                if (parts.Length >= 3 && byte.TryParse(parts[2], out byte image))
                    monsterClass.Base.Image = image;
                if (parts.Length >= 4 && byte.TryParse(parts[3], out byte level))
                    monsterClass.Base.Level = level;
                if (parts.Length >= 5 && byte.TryParse(parts[4], out byte nameColor))
                    monsterClass.Base.NameColor = nameColor;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析基础属性失败: {value}", exception: ex);
            }
        }

        
        
        
        
        private void ParsePropProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1 && ushort.TryParse(parts[0], out ushort hp))
                    monsterClass.Prop.HP = hp;
                if (parts.Length >= 2 && ushort.TryParse(parts[1], out ushort mp))
                    monsterClass.Prop.MP = mp;
                if (parts.Length >= 3 && byte.TryParse(parts[2], out byte hit))
                    monsterClass.Prop.Hit = hit;
                if (parts.Length >= 4 && byte.TryParse(parts[3], out byte speed))
                    monsterClass.Prop.Speed = speed;
                if (parts.Length >= 5 && byte.TryParse(parts[4], out byte ac1))
                    monsterClass.Prop.AC1 = ac1;
                if (parts.Length >= 6 && byte.TryParse(parts[5], out byte ac2))
                    monsterClass.Prop.AC2 = ac2;
                if (parts.Length >= 7 && byte.TryParse(parts[6], out byte dc1))
                    monsterClass.Prop.DC1 = dc1;
                if (parts.Length >= 8 && byte.TryParse(parts[7], out byte dc2))
                    monsterClass.Prop.DC2 = dc2;
                if (parts.Length >= 9 && byte.TryParse(parts[8], out byte mac1))
                    monsterClass.Prop.MAC1 = mac1;
                if (parts.Length >= 10 && byte.TryParse(parts[9], out byte mac2))
                    monsterClass.Prop.MAC2 = mac2;
                if (parts.Length >= 11 && byte.TryParse(parts[10], out byte mc1))
                    monsterClass.Prop.MC1 = mc1;
                if (parts.Length >= 12 && byte.TryParse(parts[11], out byte mc2))
                    monsterClass.Prop.MC2 = mc2;
                if (parts.Length >= 13 && uint.TryParse(parts[12], out uint exp))
                    monsterClass.Prop.Exp = exp;
                if (parts.Length >= 14 && ushort.TryParse(parts[13], out ushort aiDelay))
                    monsterClass.Prop.AIDelay = aiDelay;
                if (parts.Length >= 15 && ushort.TryParse(parts[14], out ushort walkDelay))
                    monsterClass.Prop.WalkDelay = walkDelay;
                if (parts.Length >= 16 && ushort.TryParse(parts[15], out ushort recoverHP))
                    monsterClass.Prop.RecoverHP = recoverHP;
                if (parts.Length >= 17 && ushort.TryParse(parts[16], out ushort recoverHPTime))
                    monsterClass.Prop.RecoverHPTime = recoverHPTime;
                if (parts.Length >= 18 && ushort.TryParse(parts[17], out ushort recoverMP))
                    monsterClass.Prop.RecoverMP = recoverMP;
                if (parts.Length >= 19 && ushort.TryParse(parts[18], out ushort recoverMPTime))
                    monsterClass.Prop.RecoverMPTime = recoverMPTime;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析怪物属性失败: {value}", exception: ex);
            }
        }

        
        
        
        
        private void ParseSPropProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1 && uint.TryParse(parts[0], out uint pFlag))
                    monsterClass.SProp.PFlag = pFlag;
                if (parts.Length >= 2 && byte.TryParse(parts[1], out byte callRate))
                    monsterClass.SProp.CallRate = callRate;
                if (parts.Length >= 3 && byte.TryParse(parts[2], out byte antSoulWall))
                    monsterClass.SProp.AntSoulWall = antSoulWall;
                if (parts.Length >= 4 && byte.TryParse(parts[3], out byte antTrouble))
                    monsterClass.SProp.AntTrouble = antTrouble;
                if (parts.Length >= 5 && byte.TryParse(parts[4], out byte antHolyWord))
                    monsterClass.SProp.AntHolyWord = antHolyWord;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析特殊属性失败: {value}", exception: ex);
            }
        }

        
        
        
        
        private void ParseAISetProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1 && byte.TryParse(parts[0], out byte moveStyle))
                    monsterClass.AISet.MoveStyle = moveStyle;
                if (parts.Length >= 2 && byte.TryParse(parts[1], out byte dieStyle))
                    monsterClass.AISet.DieStyle = dieStyle;
                if (parts.Length >= 3 && byte.TryParse(parts[2], out byte targetSelect))
                    monsterClass.AISet.TargetSelect = targetSelect;
                if (parts.Length >= 4 && byte.TryParse(parts[3], out byte targetFlag))
                    monsterClass.AISet.TargetFlag = targetFlag;
                if (parts.Length >= 5 && byte.TryParse(parts[4], out byte viewDistance))
                    monsterClass.AISet.ViewDistance = viewDistance;
                if (parts.Length >= 6 && byte.TryParse(parts[5], out byte coolEyes))
                    monsterClass.AISet.CoolEyes = coolEyes;
                if (parts.Length >= 7 && byte.TryParse(parts[6], out byte escapeDistance))
                    monsterClass.AISet.EscapeDistance = escapeDistance;
                if (parts.Length >= 8 && byte.TryParse(parts[7], out byte lockDir))
                    monsterClass.AISet.LockDir = lockDir;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析AI设置失败: {value}", exception: ex);
            }
        }

        
        
        
        
        private void ParsePetSetProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1 && byte.TryParse(parts[0], out byte type))
                    monsterClass.PetSet.Type = type;
                if (parts.Length >= 2 && byte.TryParse(parts[1], out byte stopAt))
                    monsterClass.PetSet.StopAt = stopAt;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析宠物设置失败: {value}", exception: ex);
            }
        }

        
        
        
        
        
        private void ParseAttackProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length < 10)
                {
                    LogManager.Default.Warning($"攻击属性字段不足10个: {value}");
                    return;
                }
                
                
                monsterClass.AttackDesc.AttackStyle = StringToInteger(parts[0]);
                monsterClass.AttackDesc.AttackDistance = StringToInteger(parts[1]);
                monsterClass.AttackDesc.Delay = StringToInteger(parts[2]);
                monsterClass.AttackDesc.DamageStyle = StringToInteger(parts[3]);
                monsterClass.AttackDesc.DamageRange = StringToInteger(parts[4]);
                monsterClass.AttackDesc.DamageType = StringToInteger(parts[5]);
                monsterClass.AttackDesc.AppendEffect = StringToInteger(parts[6]);
                monsterClass.AttackDesc.AppendRate = StringToInteger(parts[7]);
                monsterClass.AttackDesc.CostHP = StringToInteger(parts[8]);
                monsterClass.AttackDesc.CostMP = StringToInteger(parts[9]);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析攻击属性失败: {value}", exception: ex);
            }
        }

        
        
        
        
        
        private void ParseAppendProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length < 1)
                {
                    LogManager.Default.Warning($"附加属性字段不足: {value}");
                    return;
                }
                
                
                monsterClass.Base.Feature = (uint)StringToInteger(parts[0]);
                
                
                if (parts.Length > 1)
                    monsterClass.AttackDesc.Action = (ushort)StringToInteger(parts[1]);
                else
                    monsterClass.AttackDesc.Action = 0;
                
                
                if (parts.Length > 2)
                    monsterClass.AttackDesc.AppendTime = (ushort)StringToInteger(parts[2]);
                else
                    monsterClass.AttackDesc.AppendTime = 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析附加属性失败: {value}", exception: ex);
            }
        }

        
        
        
        
        private void ParseChangeIntoProperty(MonsterClass monsterClass, int index, string value)
        {
            try
            {
                if (index < 0 || index >= 3)
                    return;

                var parts = value.Split('/');
                var changeInto = monsterClass.ChangeInto[index];
                
                if (parts.Length >= 1 && int.TryParse(parts[0], out int situation1))
                    changeInto.Situation1.Situation = situation1;
                if (parts.Length >= 2 && int.TryParse(parts[1], out int param1))
                    changeInto.Situation1.Param = param1;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int situation2))
                    changeInto.Situation2.Situation = situation2;
                if (parts.Length >= 4 && int.TryParse(parts[3], out int param2))
                    changeInto.Situation2.Param = param2;
                if (parts.Length >= 5)
                    changeInto.ChangeInto = parts[4];
                if (parts.Length >= 6 && int.TryParse(parts[5], out int appendEffect))
                    changeInto.AppendEffect = appendEffect;
                if (parts.Length >= 7 && bool.TryParse(parts[6], out bool anim))
                    changeInto.Anim = anim;
                if (parts.Length >= 8 && bool.TryParse(parts[7], out bool enabled))
                    changeInto.Enabled = enabled;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析变身属性失败: index={index}, value={value}", exception: ex);
            }
        }

        
        
        
        private void ParseChangeInto(MonsterClass monsterClass, string key, string value)
        {
            try
            {
                
                if (key.Length < 10)
                    return;

                string indexStr = key.Substring(10);
                if (!int.TryParse(indexStr, out int index) || index < 0 || index >= 3)
                    return;

                var parts = value.Split(',');
                if (parts.Length < 8)
                    return;

                var changeInto = monsterClass.ChangeInto[index];
                
                
                if (int.TryParse(parts[0], out int situation1))
                    changeInto.Situation1.Situation = situation1;
                
                
                if (int.TryParse(parts[1], out int param1))
                    changeInto.Situation1.Param = param1;
                
                
                if (int.TryParse(parts[2], out int situation2))
                    changeInto.Situation2.Situation = situation2;
                
                
                if (int.TryParse(parts[3], out int param2))
                    changeInto.Situation2.Param = param2;
                
                
                changeInto.ChangeInto = parts[4];
                
                
                if (int.TryParse(parts[5], out int appendEffect))
                    changeInto.AppendEffect = appendEffect;
                
                
                if (bool.TryParse(parts[6], out bool anim))
                    changeInto.Anim = anim;
                
                
                if (bool.TryParse(parts[7], out bool enabled))
                    changeInto.Enabled = enabled;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析变身设置失败: {key}={value}", exception: ex);
            }
        }

        
        
        
        public uint GetNextObjectId()
        {
            lock (_monsterLock)
            {
                
                uint seq = ++_nextMonsterSeq;
                if (_nextMonsterSeq > 0x00FFFFFFu)
                {
                    _nextMonsterSeq = 1;
                    seq = 1;
                }
                return ObjectIdUtil.MakeObjectId(MirObjectType.Monster, seq);
            }
        }

        
        
        
        public MonsterClass? GetMonsterClass(int monsterId)
        {
            lock (_classLock)
            {
                
                foreach (var monsterClass in _monsterClassHash.Values)
                {
                    
                    
                    return monsterClass;
                }
                return null;
            }
        }

        
        
        
        private int StringToInteger(string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;
            
            
            str = str.Trim();
            
            
            if (int.TryParse(str, out int result))
                return result;
            
            
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                str = str.Substring(2);
                if (int.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, out result))
                    return result;
            }
            
            
            LogManager.Default.Warning($"无法将字符串转换为整数: '{str}'");
            return 0;
        }
    }
}
