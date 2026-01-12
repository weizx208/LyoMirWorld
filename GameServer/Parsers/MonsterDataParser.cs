using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer.Parsers
{
    
    
    
    public class MonsterBase
    {
        public string ClassName { get; set; } = "";
        public string ViewName { get; set; } = "";
        public byte Race { get; set; }
        public byte Image { get; set; }
        public byte Level { get; set; }
        public byte NameColor { get; set; }
        public uint Feature { get; set; }
    }

    
    
    
    public class MonsterProp
    {
        public ushort HP { get; set; }
        public ushort MP { get; set; }
        public byte Hit { get; set; }
        public byte Speed { get; set; }
        public byte AC1 { get; set; }
        public byte AC2 { get; set; }
        public byte DC1 { get; set; }
        public byte DC2 { get; set; }
        public byte MAC1 { get; set; }
        public byte MAC2 { get; set; }
        public byte MC1 { get; set; }
        public byte MC2 { get; set; }
        public uint Exp { get; set; }
        public ushort AIDelay { get; set; }
        public ushort WalkDelay { get; set; }
        public ushort RecoverHP { get; set; }
        public ushort RecoverHPTime { get; set; }
        public ushort RecoverMP { get; set; }
        public ushort RecoverMPTime { get; set; }
    }

    
    
    
    public class MonsterSProp
    {
        public uint PFlag { get; set; }
        public byte CallRate { get; set; }
        public byte AntSoulWall { get; set; }
        public byte AntTrouble { get; set; }
        public byte AntHolyWord { get; set; }
    }

    
    
    
    public class MonsterAISet
    {
        public byte MoveStyle { get; set; }
        public byte DieStyle { get; set; }
        public byte TargetSelect { get; set; }
        public byte TargetFlag { get; set; }
        public byte ViewDistance { get; set; }
        public byte CoolEyes { get; set; }
        public byte EscapeDistance { get; set; }
        public byte LockDir { get; set; }
    }

    
    
    
    public class MonsterPetSet
    {
        public byte Type { get; set; }
        public byte StopAt { get; set; }
    }

    
    
    
    public class MonsterAttackDesc
    {
        public int AttackStyle { get; set; }
        public int AttackDistance { get; set; }
        public int Delay { get; set; }
        public int DamageStyle { get; set; }
        public int DamageRange { get; set; }
        public int DamageType { get; set; }
        public int AppendEffect { get; set; }
        public int AppendRate { get; set; }
        public int CostHP { get; set; }
        public int CostMP { get; set; }
        public ushort Action { get; set; }
        public ushort AppendTime { get; set; }
    }

    
    
    
    public class MonsterChangeSituation
    {
        public int Situation { get; set; }
        public int Param { get; set; }
    }

    
    
    
    public class MonsterChangeInto
    {
        public bool Enabled { get; set; }
        public MonsterChangeSituation Situation1 { get; set; } = new();
        public MonsterChangeSituation Situation2 { get; set; } = new();
        public string ChangeInto { get; set; } = "";
        public int AppendEffect { get; set; }
        public bool Anim { get; set; }
    }

    
    
    
    public class MonsterClass
    {
        public MonsterBase Base { get; set; } = new();
        public MonsterProp Prop { get; set; } = new();
        public MonsterSProp SProp { get; set; } = new();
        public MonsterAISet AISet { get; set; } = new();
        public MonsterPetSet PetSet { get; set; } = new();
        public MonsterAttackDesc AttackDesc { get; set; } = new();
        public MonsterChangeInto[] ChangeInto { get; set; } = new MonsterChangeInto[3];

        
        public string BornScript { get; set; } = "";
        public string GotTargetScript { get; set; } = "";
        public string KillTargetScript { get; set; } = "";
        public string HurtScript { get; set; } = "";
        public string DeathScript { get; set; } = "";

        public MonsterClass()
        {
            for (int i = 0; i < 3; i++)
            {
                ChangeInto[i] = new MonsterChangeInto();
            }
        }
    }

    
    
    
    
    
    
    
    
    
    
    
    
    
    public class MonsterDataParser
    {
        private readonly Dictionary<string, MonsterClass> _monsters = new();

        public int MonsterCount => _monsters.Count;

        
        
        
        public bool Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"怪物数据文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                MonsterClass? currentMonster = null;
                int successCount = 0;

                foreach (var line in lines)
                {
                    string trimmedLine = line.Trim();
                    
                    if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    
                    if (trimmedLine.StartsWith("@"))
                    {
                        
                        if (currentMonster != null && !string.IsNullOrEmpty(currentMonster.Base.ClassName))
                        {
                            if (AddMonster(currentMonster))
                            {
                                successCount++;
                            }
                            else
                            {
                                
                                _monsters[currentMonster.Base.ClassName] = currentMonster;
                                LogManager.Default.Debug($"更新怪物数据: {currentMonster.Base.ClassName}");
                            }
                        }

                        
                        currentMonster = new MonsterClass();
                        currentMonster.Base.ClassName = trimmedLine.Substring(1).Trim();
                        continue;
                    }

                    
                    if (currentMonster != null)
                    {
                        ParseMonsterProperty(currentMonster, trimmedLine);
                    }
                }

                
                if (currentMonster != null && !string.IsNullOrEmpty(currentMonster.Base.ClassName))
                {
                    if (AddMonster(currentMonster))
                    {
                        successCount++;
                    }
                    else
                    {
                        _monsters[currentMonster.Base.ClassName] = currentMonster;
                    }
                }

                LogManager.Default.Info($"成功加载 {successCount} 个怪物数据");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物数据失败: {filePath}", exception: ex);
                return false;
            }
        }

        
        
        
        private void ParseMonsterProperty(MonsterClass monster, string line)
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex < 0) return;

            string key = line.Substring(0, colonIndex).Trim().ToLower();
            string value = line.Substring(colonIndex + 1).Trim();

            try
            {
                var parts = value.Split('/');

                switch (key)
                {
                    case "base":
                        if (parts.Length >= 5)
                        {
                            monster.Base.ViewName = parts[0].Trim();
                            if (monster.Base.ViewName == "<e>") monster.Base.ViewName = "";
                            monster.Base.Race = byte.Parse(parts[1].Trim());
                            monster.Base.Image = byte.Parse(parts[2].Trim());
                            monster.Base.Level = byte.Parse(parts[3].Trim());
                            monster.Base.NameColor = byte.Parse(parts[4].Trim());
                        }
                        break;

                    case "prop":
                        if (parts.Length >= 19)
                        {
                            monster.Prop.HP = ushort.Parse(parts[0].Trim());
                            monster.Prop.MP = ushort.Parse(parts[1].Trim());
                            monster.Prop.Hit = byte.Parse(parts[2].Trim());
                            monster.Prop.Speed = byte.Parse(parts[3].Trim());
                            monster.Prop.AC1 = byte.Parse(parts[4].Trim());
                            monster.Prop.AC2 = byte.Parse(parts[5].Trim());
                            monster.Prop.DC1 = byte.Parse(parts[6].Trim());
                            monster.Prop.DC2 = byte.Parse(parts[7].Trim());
                            monster.Prop.MAC1 = byte.Parse(parts[8].Trim());
                            monster.Prop.MAC2 = byte.Parse(parts[9].Trim());
                            monster.Prop.MC1 = byte.Parse(parts[10].Trim());
                            monster.Prop.MC2 = byte.Parse(parts[11].Trim());
                            monster.Prop.Exp = uint.Parse(parts[12].Trim());
                            monster.Prop.AIDelay = ushort.Parse(parts[13].Trim());
                            monster.Prop.WalkDelay = ushort.Parse(parts[14].Trim());
                            monster.Prop.RecoverHP = ushort.Parse(parts[15].Trim());
                            monster.Prop.RecoverHPTime = ushort.Parse(parts[16].Trim());
                            monster.Prop.RecoverMP = ushort.Parse(parts[17].Trim());
                            monster.Prop.RecoverMPTime = ushort.Parse(parts[18].Trim());
                        }
                        break;

                    case "sprop":
                        if (parts.Length >= 5)
                        {
                            monster.SProp.PFlag = uint.Parse(parts[0].Trim());
                            monster.SProp.CallRate = byte.Parse(parts[1].Trim());
                            monster.SProp.AntSoulWall = byte.Parse(parts[2].Trim());
                            monster.SProp.AntTrouble = byte.Parse(parts[3].Trim());
                            monster.SProp.AntHolyWord = byte.Parse(parts[4].Trim());
                        }
                        break;

                    case "aiset":
                        if (parts.Length >= 7)
                        {
                            monster.AISet.MoveStyle = byte.Parse(parts[0].Trim());
                            monster.AISet.DieStyle = byte.Parse(parts[1].Trim());
                            monster.AISet.TargetSelect = byte.Parse(parts[2].Trim());
                            monster.AISet.TargetFlag = byte.Parse(parts[3].Trim());
                            monster.AISet.ViewDistance = byte.Parse(parts[4].Trim());
                            monster.AISet.CoolEyes = byte.Parse(parts[5].Trim());
                            monster.AISet.EscapeDistance = byte.Parse(parts[6].Trim());
                            if (parts.Length > 7)
                                monster.AISet.LockDir = byte.Parse(parts[7].Trim());
                        }
                        break;

                    case "petset":
                        if (parts.Length >= 2)
                        {
                            monster.PetSet.Type = byte.Parse(parts[0].Trim());
                            monster.PetSet.StopAt = byte.Parse(parts[1].Trim());
                        }
                        break;

                    case "attack":
                        if (parts.Length >= 10)
                        {
                            monster.AttackDesc.AttackStyle = int.Parse(parts[0].Trim());
                            monster.AttackDesc.AttackDistance = int.Parse(parts[1].Trim());
                            monster.AttackDesc.Delay = int.Parse(parts[2].Trim());
                            monster.AttackDesc.DamageStyle = int.Parse(parts[3].Trim());
                            monster.AttackDesc.DamageRange = int.Parse(parts[4].Trim());
                            monster.AttackDesc.DamageType = int.Parse(parts[5].Trim());
                            monster.AttackDesc.AppendEffect = int.Parse(parts[6].Trim());
                            monster.AttackDesc.AppendRate = int.Parse(parts[7].Trim());
                            monster.AttackDesc.CostHP = int.Parse(parts[8].Trim());
                            monster.AttackDesc.CostMP = int.Parse(parts[9].Trim());
                        }
                        break;

                    case "chg1":
                        ParseChangeInto(monster.ChangeInto[0], parts);
                        break;

                    case "chg2":
                        ParseChangeInto(monster.ChangeInto[1], parts);
                        break;

                    case "chg3":
                        ParseChangeInto(monster.ChangeInto[2], parts);
                        break;

                    case "append":
                        if (parts.Length >= 1)
                        {
                            monster.Base.Feature = uint.Parse(parts[0].Trim());
                            if (parts.Length > 1)
                                monster.AttackDesc.Action = ushort.Parse(parts[1].Trim());
                            if (parts.Length > 2)
                                monster.AttackDesc.AppendTime = ushort.Parse(parts[2].Trim());
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Debug($"解析怪物属性失败 [{key}]: {ex.Message}");
            }
        }

        
        
        
        private void ParseChangeInto(MonsterChangeInto change, string[] parts)
        {
            if (parts.Length >= 7)
            {
                change.Situation1.Situation = int.Parse(parts[0].Trim());
                change.Situation1.Param = int.Parse(parts[1].Trim());
                change.Situation2.Situation = int.Parse(parts[2].Trim());
                change.Situation2.Param = int.Parse(parts[3].Trim());
                change.ChangeInto = parts[4].Trim();
                change.AppendEffect = int.Parse(parts[5].Trim());
                change.Anim = int.Parse(parts[6].Trim()) > 0;
                change.Enabled = !string.IsNullOrEmpty(change.ChangeInto);
            }
        }

        
        
        
        public bool AddMonster(MonsterClass monster)
        {
            if (_monsters.ContainsKey(monster.Base.ClassName))
            {
                return false;
            }

            _monsters[monster.Base.ClassName] = monster;
            return true;
        }

        
        
        
        public MonsterClass? GetMonster(string name)
        {
            return _monsters.TryGetValue(name, out var monster) ? monster : null;
        }

        
        
        
        public IEnumerable<MonsterClass> GetAllMonsters()
        {
            return _monsters.Values;
        }

        
        
        
        
        public bool LoadMonsterScript(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"怪物脚本文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('=');
                    if (parts.Length < 2)
                        continue;

                    string monsterName = parts[0].Trim();
                    var monster = GetMonster(monsterName);
                    if (monster == null)
                    {
                        LogManager.Default.Debug($"怪物不存在，无法设置脚本: {monsterName}");
                        continue;
                    }

                    var scripts = parts[1].Split(',');
                    if (scripts.Length > 0 && !string.IsNullOrWhiteSpace(scripts[0]))
                        monster.BornScript = scripts[0].Trim();
                    if (scripts.Length > 1 && !string.IsNullOrWhiteSpace(scripts[1]))
                        monster.GotTargetScript = scripts[1].Trim();
                    if (scripts.Length > 2 && !string.IsNullOrWhiteSpace(scripts[2]))
                        monster.KillTargetScript = scripts[2].Trim();
                    if (scripts.Length > 3 && !string.IsNullOrWhiteSpace(scripts[3]))
                        monster.HurtScript = scripts[3].Trim();
                    if (scripts.Length > 4 && !string.IsNullOrWhiteSpace(scripts[4]))
                        monster.DeathScript = scripts[4].Trim();

                    count++;
                }

                LogManager.Default.Info($"成功加载 {count} 个怪物脚本配置");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物脚本配置失败: {filePath}", exception: ex);
                return false;
            }
        }
    }
}
