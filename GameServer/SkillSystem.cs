namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    
    using Player = HumanPlayer;
    
    
    
    public enum SkillType
    {
        Passive = 0,        
        Active = 1,         
        Buff = 2,           
        Debuff = 3,         
        Summon = 4,         
        Teleport = 5,       
        Attack = 6,         
        Heal = 7            
    }

    
    
    
    public enum SkillTargetType
    {
        Self = 0,           
        Enemy = 1,          
        Friend = 2,         
        Ground = 3,         
        Area = 4            
    }

    
    
    
    public enum SkillEffectType
    {
        Damage = 0,         
        Heal = 1,           
        Buff = 2,           
        Debuff = 3,         
        Stun = 4,           
        Slow = 5,           
        Poison = 6,         
        Shield = 7,         
        Teleport = 8,       
        Summon = 9          
    }

    
    
    
    public class SkillDefinition
    {
        public int SkillId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SkillType Type { get; set; }
        public SkillTargetType TargetType { get; set; }
        
        
        public int RequireLevel { get; set; }
        public int RequireJob { get; set; } = -1;  
        public int RequireSkill { get; set; }      
        
        
        public int MPCost { get; set; }
        public int HPCost { get; set; }
        public uint GoldCost { get; set; }
        
        
        public int Cooldown { get; set; }          
        public int CastTime { get; set; }          
        
        
        public int Range { get; set; }             
        public int Radius { get; set; }            
        
        
        public List<SkillEffect> Effects { get; set; } = new();
        
        
        public int MaxLevel { get; set; } = 3;
        public Dictionary<int, SkillLevelData> LevelData { get; set; } = new();

        public SkillDefinition(int skillId, string name, SkillType type)
        {
            SkillId = skillId;
            Name = name;
            Type = type;
            MaxLevel = 3;
        }

        public SkillLevelData? GetLevelData(int level)
        {
            LevelData.TryGetValue(level, out var data);
            return data;
        }
    }

    
    
    
    public class SkillLevelData
    {
        public int Level { get; set; }
        public int MPCost { get; set; }
        public int Power { get; set; }          
        public int Duration { get; set; }       
        public int Cooldown { get; set; }       
        public int Range { get; set; }          
        public uint LearnCost { get; set; }     

        public SkillLevelData(int level)
        {
            Level = level;
        }
    }

    
    
    
    public class SkillEffect
    {
        public SkillEffectType Type { get; set; }
        public int Value { get; set; }
        public int Duration { get; set; }
        public float Chance { get; set; } = 1.0f;   

        public SkillEffect(SkillEffectType type, int value, int duration = 0)
        {
            Type = type;
            Value = value;
            Duration = duration;
        }
    }

    
    
    
    public class PlayerSkill
    {
        public int SkillId { get; set; }
        public SkillDefinition Definition { get; set; }
        
        
        
        public int Level { get; set; }
        public DateTime LearnTime { get; set; }
        public DateTime LastUseTime { get; set; }
        
        
        
        public int UseCount { get; set; }
        public byte Key { get; set; } 

        
        public int AutoAddPower { get; set; } = 0;  
        public bool Activated { get; set; } = false; 

        public PlayerSkill(SkillDefinition definition)
        {
            Definition = definition;
            SkillId = definition.SkillId;
            
            Level = 0;
            LearnTime = DateTime.Now;
            LastUseTime = DateTime.MinValue;
            Key = 0; 
        }

        
        
        
        public SkillLevelData? GetCurrentLevelData()
        {
            if (Definition.LevelData.Count == 0)
                return null;

            
            int wanted = Definition.LevelData.ContainsKey(0) ? Level : Level + 1;
            if (Definition.LevelData.TryGetValue(wanted, out var data))
                return data;

            
            int fallbackKey = Definition.LevelData.Keys.Min();
            return Definition.LevelData.TryGetValue(fallbackKey, out var fallback) ? fallback : null;
        }

        public bool CanUse()
        {
            var levelData = GetCurrentLevelData();
            if (levelData == null) return true;

            var cooldownMs = (DateTime.Now - LastUseTime).TotalMilliseconds;
            return cooldownMs >= levelData.Cooldown;
        }

        public TimeSpan GetRemainingCooldown()
        {
            var levelData = GetCurrentLevelData();
            if (levelData == null) return TimeSpan.Zero;

            var elapsed = (DateTime.Now - LastUseTime).TotalMilliseconds;
            var remaining = levelData.Cooldown - elapsed;
            return remaining > 0 ? TimeSpan.FromMilliseconds(remaining) : TimeSpan.Zero;
        }

        public bool CanLevelUp()
        {
            
            if (Definition.MaxLevel <= 0)
                return false;

            int maxInternal = Definition.LevelData.ContainsKey(0) ? Definition.MaxLevel : (Definition.MaxLevel - 1);
            if (Level >= maxInternal)
                return false;

            int nextKey = Definition.LevelData.ContainsKey(0) ? (Level + 1) : (Level + 2);
            return Definition.LevelData.Count == 0 || Definition.LevelData.ContainsKey(nextKey);
        }

        public void LevelUp()
        {
            if (CanLevelUp())
            {
                Level++;
                LogManager.Default.Info($"技能 {Definition.Name} 升级到 {Level} 级");
            }
        }

        public void Use()
        {
            LastUseTime = DateTime.Now;
        }

        
        
        
        public void AddExp(int exp)
        {
            if (exp <= 0)
                return;

            if (UseCount < 0)
                UseCount = 0;

            
            long next = (long)UseCount + exp;
            if (next > int.MaxValue)
                next = int.MaxValue;
            UseCount = (int)next;

            try
            {
                
                if (MagicManager.Instance.GetMagicCount() == 0)
                {
                    MagicManager.Instance.LoadAll();
                }

                var magicClass = MagicManager.Instance.GetClassById(SkillId);
                if (magicClass != null)
                {
                    if (Level >= 3)
                        return;

                    int idx = Math.Clamp(Level, 0, 3);
                    uint need = magicClass.dwNeedExp[idx];
                    if (need > 0 && (uint)UseCount >= need)
                    {
                        Level = Math.Min(Level + 1, 3);
                    }
                    return;
                }
            }
            catch
            {
                
            }

            
            if (UseCount % 50 == 0 && CanLevelUp())
            {
                LevelUp();
            }
        }
    }

    
    
    
    public class SkillBook
    {
        public PlayerSkill Skill { get; }
        private readonly Dictionary<int, PlayerSkill> _skills = new();
        private readonly object _lock = new();
        public int MaxSkills { get; set; } = 20;

        public SkillBook()
        {
            Skill = null!;
        }

        public bool LearnSkill(SkillDefinition definition)
        {
            lock (_lock)
            {
                if (_skills.ContainsKey(definition.SkillId))
                    return false;

                if (_skills.Count >= MaxSkills)
                    return false;

                var skill = new PlayerSkill(definition);
                _skills[definition.SkillId] = skill;
                
                LogManager.Default.Info($"学习技能: {definition.Name}");
                return true;
            }
        }

        public bool ForgetSkill(int skillId)
        {
            lock (_lock)
            {
                if (_skills.Remove(skillId))
                {
                    LogManager.Default.Info($"遗忘技能ID: {skillId}");
                    return true;
                }
                return false;
            }
        }

        public PlayerSkill? GetSkill(int skillId)
        {
            lock (_lock)
            {
                _skills.TryGetValue(skillId, out var skill);
                return skill;
            }
        }

        public bool HasSkill(int skillId)
        {
            lock (_lock)
            {
                return _skills.ContainsKey(skillId);
            }
        }

        public List<PlayerSkill> GetAllSkills()
        {
            lock (_lock)
            {
                return _skills.Values.ToList();
            }
        }

        public bool LevelUpSkill(int skillId)
        {
            lock (_lock)
            {
                var skill = GetSkill(skillId);
                if (skill == null || !skill.CanLevelUp())
                    return false;

                skill.LevelUp();
                return true;
            }
        }

        public PlayerSkill? GetMagic(uint magicId)
        {
            lock (_lock)
            {
                return _skills.Values.FirstOrDefault(skill => skill.SkillId == magicId);
            }
        }

        public PlayerSkill? GetMagicByKey(byte key)
        {
            
            
            lock (_lock)
            {
                return _skills.Values.FirstOrDefault();
            }
        }

        public bool HasMagic(uint magicId)
        {
            lock (_lock)
            {
                return _skills.ContainsKey((int)magicId);
            }
        }

        public void SetMagicKey(uint magicId, byte key)
        {
            
            
            LogManager.Default.Info($"设置技能 {magicId} 的快捷键为 {key}");
        }
    }

    
    
    
    public class SkillManager
    {
        private static SkillManager? _instance;
        public static SkillManager Instance => _instance ??= new SkillManager();

        private readonly ConcurrentDictionary<int, SkillDefinition> _definitions = new();

        private SkillManager()
        {
            InitializeDefaultSkills();
        }

        private void InitializeDefaultSkills()
        {
            
            var basicSword = new SkillDefinition(1001, "基础剑法", SkillType.Attack)
            {
                Description = "基础的剑术攻击",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 0, 
                RequireLevel = 1,
                Range = 1,
                MaxLevel = 3
            };
            basicSword.Effects.Add(new SkillEffect(SkillEffectType.Damage, 10));
            basicSword.LevelData[1] = new SkillLevelData(1) { MPCost = 2, Power = 10, Cooldown = 1000, LearnCost = 100 };
            basicSword.LevelData[2] = new SkillLevelData(2) { MPCost = 3, Power = 15, Cooldown = 900, LearnCost = 500 };
            basicSword.LevelData[3] = new SkillLevelData(3) { MPCost = 4, Power = 20, Cooldown = 800, LearnCost = 1000 };
            AddDefinition(basicSword);

            var assassinate = new SkillDefinition(1002, "刺杀剑术", SkillType.Attack)
            {
                Description = "强力的突刺攻击",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 0,
                RequireLevel = 7,
                RequireSkill = 1001,
                Range = 2,
                MaxLevel = 3
            };
            assassinate.Effects.Add(new SkillEffect(SkillEffectType.Damage, 30));
            assassinate.LevelData[1] = new SkillLevelData(1) { MPCost = 5, Power = 30, Cooldown = 3000, LearnCost = 1000 };
            assassinate.LevelData[2] = new SkillLevelData(2) { MPCost = 7, Power = 45, Cooldown = 2500, LearnCost = 5000 };
            assassinate.LevelData[3] = new SkillLevelData(3) { MPCost = 10, Power = 60, Cooldown = 2000, LearnCost = 10000 };
            AddDefinition(assassinate);

            var halfMoon = new SkillDefinition(1003, "半月弯刀", SkillType.Attack)
            {
                Description = "攻击周围所有敌人",
                TargetType = SkillTargetType.Area,
                RequireJob = 0,
                RequireLevel = 19,
                Range = 1,
                Radius = 2,
                MaxLevel = 3
            };
            halfMoon.Effects.Add(new SkillEffect(SkillEffectType.Damage, 25));
            halfMoon.LevelData[1] = new SkillLevelData(1) { MPCost = 8, Power = 25, Cooldown = 5000, LearnCost = 5000 };
            halfMoon.LevelData[2] = new SkillLevelData(2) { MPCost = 12, Power = 40, Cooldown = 4000, LearnCost = 20000 };
            halfMoon.LevelData[3] = new SkillLevelData(3) { MPCost = 15, Power = 55, Cooldown = 3000, LearnCost = 50000 };
            AddDefinition(halfMoon);

            
            var fireball = new SkillDefinition(2001, "火球术", SkillType.Attack)
            {
                Description = "发射火球攻击敌人",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 1, 
                RequireLevel = 1,
                Range = 7,
                MaxLevel = 3
            };
            fireball.Effects.Add(new SkillEffect(SkillEffectType.Damage, 15));
            fireball.LevelData[1] = new SkillLevelData(1) { MPCost = 4, Power = 15, Cooldown = 1500, LearnCost = 100 };
            fireball.LevelData[2] = new SkillLevelData(2) { MPCost = 6, Power = 25, Cooldown = 1200, LearnCost = 500 };
            fireball.LevelData[3] = new SkillLevelData(3) { MPCost = 8, Power = 35, Cooldown = 1000, LearnCost = 1000 };
            AddDefinition(fireball);

            var lightning = new SkillDefinition(2002, "雷电术", SkillType.Attack)
            {
                Description = "召唤雷电攻击敌人",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 1,
                RequireLevel = 17,
                Range = 7,
                MaxLevel = 3
            };
            lightning.Effects.Add(new SkillEffect(SkillEffectType.Damage, 40));
            lightning.LevelData[1] = new SkillLevelData(1) { MPCost = 12, Power = 40, Cooldown = 3000, LearnCost = 5000 };
            lightning.LevelData[2] = new SkillLevelData(2) { MPCost = 18, Power = 60, Cooldown = 2500, LearnCost = 20000 };
            lightning.LevelData[3] = new SkillLevelData(3) { MPCost = 25, Power = 80, Cooldown = 2000, LearnCost = 50000 };
            AddDefinition(lightning);

            var hellFire = new SkillDefinition(2003, "地狱火", SkillType.Attack)
            {
                Description = "范围火焰攻击",
                TargetType = SkillTargetType.Area,
                RequireJob = 1,
                RequireLevel = 35,
                Range = 7,
                Radius = 3,
                MaxLevel = 3
            };
            hellFire.Effects.Add(new SkillEffect(SkillEffectType.Damage, 50));
            hellFire.LevelData[1] = new SkillLevelData(1) { MPCost = 30, Power = 50, Cooldown = 8000, LearnCost = 50000 };
            hellFire.LevelData[2] = new SkillLevelData(2) { MPCost = 45, Power = 80, Cooldown = 7000, LearnCost = 200000 };
            hellFire.LevelData[3] = new SkillLevelData(3) { MPCost = 60, Power = 110, Cooldown = 6000, LearnCost = 500000 };
            AddDefinition(hellFire);

            
            var heal = new SkillDefinition(3001, "治愈术", SkillType.Heal)
            {
                Description = "恢复生命值",
                TargetType = SkillTargetType.Friend,
                RequireJob = 2, 
                RequireLevel = 1,
                Range = 7,
                MaxLevel = 3
            };
            heal.Effects.Add(new SkillEffect(SkillEffectType.Heal, 20));
            heal.LevelData[1] = new SkillLevelData(1) { MPCost = 6, Power = 20, Cooldown = 2000, LearnCost = 100 };
            heal.LevelData[2] = new SkillLevelData(2) { MPCost = 9, Power = 35, Cooldown = 1800, LearnCost = 500 };
            heal.LevelData[3] = new SkillLevelData(3) { MPCost = 12, Power = 50, Cooldown = 1500, LearnCost = 1000 };
            AddDefinition(heal);

            var poison = new SkillDefinition(3002, "施毒术", SkillType.Debuff)
            {
                Description = "对敌人施加毒素",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 2,
                RequireLevel = 14,
                Range = 7,
                MaxLevel = 3
            };
            poison.Effects.Add(new SkillEffect(SkillEffectType.Poison, 5, 10000));
            poison.LevelData[1] = new SkillLevelData(1) { MPCost = 8, Power = 5, Duration = 10000, Cooldown = 3000, LearnCost = 2000 };
            poison.LevelData[2] = new SkillLevelData(2) { MPCost = 12, Power = 8, Duration = 15000, Cooldown = 2500, LearnCost = 10000 };
            poison.LevelData[3] = new SkillLevelData(3) { MPCost = 16, Power = 12, Duration = 20000, Cooldown = 2000, LearnCost = 30000 };
            AddDefinition(poison);

            var summonSkeleton = new SkillDefinition(3003, "召唤骷髅", SkillType.Summon)
            {
                Description = "召唤骷髅协助战斗",
                TargetType = SkillTargetType.Self,
                RequireJob = 2,
                RequireLevel = 19,
                MaxLevel = 3
            };
            summonSkeleton.Effects.Add(new SkillEffect(SkillEffectType.Summon, 1));
            summonSkeleton.LevelData[1] = new SkillLevelData(1) { MPCost = 20, Power = 1, Cooldown = 10000, LearnCost = 5000 };
            summonSkeleton.LevelData[2] = new SkillLevelData(2) { MPCost = 30, Power = 2, Cooldown = 8000, LearnCost = 20000 };
            summonSkeleton.LevelData[3] = new SkillLevelData(3) { MPCost = 40, Power = 3, Cooldown = 6000, LearnCost = 50000 };
            AddDefinition(summonSkeleton);

            LogManager.Default.Info($"已加载 {_definitions.Count} 个技能定义");
        }

        public void AddDefinition(SkillDefinition definition)
        {
            _definitions[definition.SkillId] = definition;
        }

        public SkillDefinition? GetDefinition(int skillId)
        {
            _definitions.TryGetValue(skillId, out var definition);
            return definition;
        }

        public List<SkillDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }

        public List<SkillDefinition> GetSkillsByJob(int job)
        {
            return _definitions.Values
                .Where(s => s.RequireJob == -1 || s.RequireJob == job)
                .OrderBy(s => s.RequireLevel)
                .ToList();
        }

        public List<SkillDefinition> GetLearnableSkills(Player player)
        {
            var skillBook = player.SkillBook;
            
            return _definitions.Values
                .Where(s => 
                    (s.RequireJob == -1 || s.RequireJob == player.Job) &&
                    s.RequireLevel <= player.Level &&
                    !skillBook.HasSkill(s.SkillId) &&
                    (s.RequireSkill == 0 || skillBook.HasSkill(s.RequireSkill))
                )
                .OrderBy(s => s.RequireLevel)
                .ToList();
        }
    }

    
    
    
    public class SkillUseResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ICombatEntity> AffectedTargets { get; set; } = new();
        public int TotalDamage { get; set; }
        public int TotalHeal { get; set; }

        public SkillUseResult(bool success, string message = "")
        {
            Success = success;
            Message = message;
        }
    }

    
    
    
    public class SkillExecutor
    {
        private static SkillExecutor? _instance;
        public static SkillExecutor Instance => _instance ??= new SkillExecutor();

        private SkillExecutor() { }

        
        
        
        public SkillUseResult UseSkill(Player caster, int skillId, ICombatEntity? target = null, int x = 0, int y = 0)
        {
            
            var playerSkill = caster.SkillBook.GetSkill(skillId);
            if (playerSkill == null)
                return new SkillUseResult(false, "未学习此技能");

            
            if (!playerSkill.CanUse())
            {
                var remaining = playerSkill.GetRemainingCooldown();
                return new SkillUseResult(false, $"冷却中，还需{remaining.TotalSeconds:F1}秒");
            }

            
            var levelData = playerSkill.GetCurrentLevelData();
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            
            if (!caster.ConsumeMP(levelData.MPCost))
                return new SkillUseResult(false, "魔法不足");

            
            if (playerSkill.Definition.HPCost > 0 && caster.CurrentHP <= playerSkill.Definition.HPCost)
                return new SkillUseResult(false, "生命值不足");

            
            if (target != null && caster.CurrentMap != null)
            {
                int distance = Math.Abs(caster.X - target.X) + Math.Abs(caster.Y - target.Y);
                if (distance > playerSkill.Definition.Range)
                    return new SkillUseResult(false, "距离太远");
            }

            
            if (playerSkill.Definition.HPCost > 0)
                caster.CurrentHP -= playerSkill.Definition.HPCost;
            
            if (playerSkill.Definition.GoldCost > 0)
            {
                if (!caster.TakeGold(playerSkill.Definition.GoldCost))
                    return new SkillUseResult(false, "金币不足");
            }

            
            playerSkill.Use();

            
            var result = ExecuteSkill(caster, playerSkill, target, x, y);
            
            if (result.Success)
            {
                
                SendSkillUseMessage(caster, playerSkill, target, x, y);
                
                
                TrainSkill(caster, playerSkill);
                
                LogManager.Default.Info($"{caster.Name} 使用技能 {playerSkill.Definition.Name} (等级{playerSkill.Level})");
            }

            return result;
        }

        
        
        
        private SkillUseResult ExecuteSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "技能释放成功");
            var levelData = skill.GetCurrentLevelData();
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            
            switch (skill.Definition.Type)
            {
                case SkillType.Attack:
                    result = ExecuteAttackSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Heal:
                    result = ExecuteHealSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Buff:
                    result = ExecuteBuffSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Debuff:
                    result = ExecuteDebuffSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Summon:
                    result = ExecuteSummonSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Teleport:
                    result = ExecuteTeleportSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Passive:
                    
                    result = new SkillUseResult(false, "被动技能不能主动释放");
                    break;
            }

            return result;
        }

        
        
        
        private SkillUseResult ExecuteAttackSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "攻击成功");
            var levelData = skill.GetCurrentLevelData();
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            
            var targets = GetSkillTargets(caster, skill, target, x, y);
            
            foreach (var t in targets)
            {
                
                int baseDamage = levelData.Power;
                
                
                int damageBonus = 0;
                switch (caster.Job)
                {
                    case 0: 
                        damageBonus = caster.Stats.MinDC;
                        break;
                    case 1: 
                        damageBonus = caster.Stats.MinMC;
                        break;
                    case 2: 
                        damageBonus = caster.Stats.MinSC;
                        break;
                }
                
                int totalDamage = baseDamage + damageBonus;
                
                
                var combatResult = CombatSystemManager.Instance.ExecuteCombat(caster, t, DamageType.Magic);
                result.TotalDamage += combatResult.Damage;
                result.AffectedTargets.Add(t);
                
                
                SendDamageMessage(caster, t, combatResult.Damage);
            }

            return result;
        }

        
        
        
        private SkillUseResult ExecuteHealSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "治疗成功");
            var levelData = skill.GetCurrentLevelData();
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            
            var targets = GetSkillTargets(caster, skill, target, x, y);
            
            foreach (var t in targets)
            {
                int healAmount = levelData.Power;
                t.Heal(healAmount);
                result.TotalHeal += healAmount;
                result.AffectedTargets.Add(t);
                
                
                SendHealMessage(caster, t, healAmount);
            }

            return result;
        }

        
        
        
        private SkillUseResult ExecuteBuffSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "增益效果生效");
            var levelData = skill.GetCurrentLevelData();
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            
            var targets = GetSkillTargets(caster, skill, target, x, y);
            
            foreach (var t in targets)
            {
                
                
                foreach (var effect in skill.Definition.Effects)
                {
                    switch (effect.Type)
                    {
                        case SkillEffectType.Buff:
                            
                            ApplyBuffEffect(caster, t, skill, effect, levelData);
                            break;
                        case SkillEffectType.Shield:
                            
                            ApplyShieldEffect(caster, t, skill, effect, levelData);
                            break;
                        case SkillEffectType.Heal:
                            
                            ApplyHealEffect(caster, t, skill, effect, levelData);
                            break;
                    }
                }
                
                result.AffectedTargets.Add(t);
                
                
                SendBuffMessage(caster, t, skill.Definition.Name);
            }

            return result;
        }

        
        
        
        private SkillUseResult ExecuteDebuffSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "减益效果生效");
            var levelData = skill.GetCurrentLevelData();
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            
            var targets = GetSkillTargets(caster, skill, target, x, y);
            
            foreach (var t in targets)
            {
                
                
                foreach (var effect in skill.Definition.Effects)
                {
                    ApplyDebuffEffect(caster, t, skill, effect, levelData);
                }
                
                result.AffectedTargets.Add(t);
                
                
                SendDebuffMessage(caster, t, skill.Definition.Name);
            }

            return result;
        }

        
        
        
        private SkillUseResult ExecuteSummonSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "召唤成功");
            var levelData = skill.GetCurrentLevelData();
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            
            if (caster.PetSystem.GetPetCount() >= caster.PetSystem.MaxPets)
                return new SkillUseResult(false, "召唤数量已达上限");

            
            
            string petName = GetSummonPetName(skill.SkillId, skill.Level);
            int summonCount = levelData.Power; 
            
            for (int i = 0; i < summonCount; i++)
            {
                
                int summonX = x;
                int summonY = y;
                
                if (summonX == 0 && summonY == 0)
                {
                    
                    summonX = caster.X + Random.Shared.Next(-2, 3);
                    summonY = caster.Y + Random.Shared.Next(-2, 3);
                }
                
                
                bool success = caster.PetSystem.SummonPet(petName, true, summonX, summonY);
                if (!success)
                {
                    return new SkillUseResult(false, "召唤失败");
                }
            }
            
            
            SendSummonMessage(caster, skill.Definition.Name);
            
            return result;
        }

        
        
        
        private SkillUseResult ExecuteTeleportSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "传送成功");
            var levelData = skill.GetCurrentLevelData();
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            
            if (caster.CurrentMap == null || !caster.CurrentMap.CanMoveTo(x, y))
                return new SkillUseResult(false, "无法传送到该位置");

            
            caster.X = (ushort)x;
            caster.Y = (ushort)y;
            
            
            SendTeleportMessage(caster, x, y);
            
            return result;
        }

        
        
        
        private List<ICombatEntity> GetSkillTargets(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var targets = new List<ICombatEntity>();
            
            switch (skill.Definition.TargetType)
            {
                case SkillTargetType.Self:
                    targets.Add(caster);
                    break;
                    
                case SkillTargetType.Enemy:
                    if (target != null)
                        targets.Add(target);
                    break;
                    
                case SkillTargetType.Friend:
                    if (target != null)
                        targets.Add(target);
                    else
                        targets.Add(caster);
                    break;
                    
                case SkillTargetType.Area:
                    
                    if (caster.CurrentMap != null)
                    {
                        var areaTargets = caster.CurrentMap.GetObjectsInRange(x, y, skill.Definition.Radius);
                        foreach (var obj in areaTargets)
                        {
                            if (obj is ICombatEntity combatEntity)
                            {
                                
                                if (skill.Definition.Type == SkillType.Attack && combatEntity != caster)
                                    targets.Add(combatEntity);
                                else if (skill.Definition.Type == SkillType.Heal && combatEntity == caster)
                                    targets.Add(combatEntity);
                            }
                        }
                    }
                    break;
                    
                case SkillTargetType.Ground:
                    
                    break;
            }

            return targets;
        }


        
        
        
        private void SendSkillUseMessage(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x285); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32((uint)skill.SkillId);
            builder.WriteUInt16((ushort)skill.Level);
            builder.WriteUInt32(target?.Id ?? 0);
            builder.WriteUInt16((ushort)x);
            builder.WriteUInt16((ushort)y);
            
            caster.SendMessage(builder.Build());
        }

        
        
        
        private void SendDamageMessage(Player caster, ICombatEntity target, int damage)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x286); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(target.Id);
            builder.WriteUInt32((uint)damage);
            
            caster.SendMessage(builder.Build());
        }

        
        
        
        private void SendHealMessage(Player caster, ICombatEntity target, int healAmount)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x287); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(target.Id);
            builder.WriteUInt32((uint)healAmount);
            
            caster.SendMessage(builder.Build());
        }

        
        
        
        private void SendBuffMessage(Player caster, ICombatEntity target, string buffName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x288); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(target.Id);
            builder.WriteString(buffName);
            
            caster.SendMessage(builder.Build());
        }

        
        
        
        private void SendDebuffMessage(Player caster, ICombatEntity target, string debuffName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x289); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(target.Id);
            builder.WriteString(debuffName);
            
            caster.SendMessage(builder.Build());
        }

        
        
        
        private void SendSummonMessage(Player caster, string summonName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x28A); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(summonName);
            
            caster.SendMessage(builder.Build());
        }

        
        
        
        private void SendTeleportMessage(Player caster, int x, int y)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x28B); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16((ushort)x);
            builder.WriteUInt16((ushort)y);
            
            caster.SendMessage(builder.Build());
        }

        
        
        
        private void SendSkillLevelUpMessage(Player caster, PlayerSkill skill)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x28C); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32((uint)skill.SkillId);
            builder.WriteUInt16((ushort)skill.Level);
            
            caster.SendMessage(builder.Build());
        }

        public bool LearnSkill(Player player, int skillId)
        {
            var definition = SkillManager.Instance.GetDefinition(skillId);
            if (definition == null)
                return false;

            
            if (definition.RequireLevel > player.Level)
                return false;

            if (definition.RequireJob != -1 && definition.RequireJob != player.Job)
                return false;

            if (definition.RequireSkill != 0 && !player.SkillBook.HasSkill(definition.RequireSkill))
                return false;

            
            var levelData = definition.GetLevelData(1);
            if (levelData != null && player.Gold < levelData.LearnCost)
                return false;

            
            if (player.SkillBook.LearnSkill(definition))
            {
                if (levelData != null)
                {
                    player.Gold -= levelData.LearnCost;
                }
                LogManager.Default.Info($"{player.Name} 学习了技能 {definition.Name}");
                return true;
            }

            return false;
        }

        public bool LevelUpSkill(Player player, int skillId)
        {
            var playerSkill = player.SkillBook.GetSkill(skillId);
            if (playerSkill == null || !playerSkill.CanLevelUp())
                return false;

            int nextInternalLevel = playerSkill.Level + 1;
            int nextKey = playerSkill.Definition.LevelData.ContainsKey(0) ? nextInternalLevel : (nextInternalLevel + 1);
            var levelData = playerSkill.Definition.GetLevelData(nextKey);
            if (levelData == null)
                return false;

            
            if (player.Gold < levelData.LearnCost)
                return false;

            
            player.Gold -= levelData.LearnCost;
            player.SkillBook.LevelUpSkill(skillId);
            
            LogManager.Default.Info($"{player.Name} 的技能 {playerSkill.Definition.Name} 升级到 {nextInternalLevel} 级");
            return true;
        }

        
        
        
        private void ApplyBuffEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            
            var buffDefinition = BuffDefinitionManager.Instance.GetDefinition(1001); 
            if (buffDefinition != null)
            {
                
                buffDefinition.Value = levelData.Power;
                buffDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                
                target.BuffManager?.AddBuff(buffDefinition, caster);
            }
        }

        
        
        
        private void ApplyShieldEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            
            var shieldDefinition = BuffDefinitionManager.Instance.GetDefinition(1007); 
            if (shieldDefinition != null)
            {
                shieldDefinition.Value = levelData.Power;
                shieldDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                
                target.BuffManager?.AddBuff(shieldDefinition, caster);
            }
        }

        
        
        
        private void ApplyHealEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            
            int healAmount = levelData.Power;
            target.Heal(healAmount);
            
            
            SendHealMessage(caster, target, healAmount);
        }

        
        
        
        private void ApplyDebuffEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            
            switch (effect.Type)
            {
                case SkillEffectType.Poison:
                    ApplyPoisonEffect(caster, target, skill, effect, levelData);
                    break;
                case SkillEffectType.Slow:
                    ApplySlowEffect(caster, target, skill, effect, levelData);
                    break;
                case SkillEffectType.Stun:
                    ApplyStunEffect(caster, target, skill, effect, levelData);
                    break;
            }
        }

        
        
        
        private void ApplyPoisonEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            var poisonDefinition = BuffDefinitionManager.Instance.GetDefinition(2001); 
            if (poisonDefinition != null)
            {
                poisonDefinition.Value = levelData.Power;
                poisonDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                
                target.BuffManager?.AddBuff(poisonDefinition, caster);
            }
        }

        
        
        
        private void ApplySlowEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            var slowDefinition = BuffDefinitionManager.Instance.GetDefinition(2006); 
            if (slowDefinition != null)
            {
                slowDefinition.Value = levelData.Power;
                slowDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                
                target.BuffManager?.AddBuff(slowDefinition, caster);
            }
        }

        
        
        
        private void ApplyStunEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            var stunDefinition = BuffDefinitionManager.Instance.GetDefinition(3001); 
            if (stunDefinition != null)
            {
                stunDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                
                target.BuffManager?.AddBuff(stunDefinition, caster);
            }
        }

        
        
        
        private string GetSummonPetName(int skillId, int skillLevel)
        {
            
            switch (skillId)
            {
                case 3003: 
                    return skillLevel >= 3 ? "骷髅精灵" : 
                           skillLevel >= 2 ? "骷髅战士" : "骷髅";
                default:
                    return "未知宠物";
            }
        }

        
        
        
        private void TrainSkill(Player caster, PlayerSkill skill)
        {
            
            skill.UseCount++;
            
            
            if (skill.UseCount % 10 == 0)
            {
                
                
                int expGain = CalculateSkillExpGain(caster, skill);
                
                
                
                LogManager.Default.Info($"{caster.Name} 的技能 {skill.Definition.Name} 获得 {expGain} 经验");
                
                
                if (skill.CanLevelUp())
                {
                    
                    if (CheckSkillLevelUpConditions(caster, skill))
                    {
                        
                        skill.LevelUp();
                        
                        
                        SendSkillLevelUpMessage(caster, skill);
                        
                        
                        caster.Say($"恭喜！你的技能 {skill.Definition.Name} 升级到 {skill.Level} 级");
                    }
                }
            }
        }

        
        
        
        private int CalculateSkillExpGain(Player caster, PlayerSkill skill)
        {
            
            int baseExp = 10;
            
            
            int levelBonus = skill.Level * 5;
            
            
            int jobBonus = 0;
            switch (caster.Job)
            {
                case 0: 
                    jobBonus = skill.Definition.Type == SkillType.Attack ? 10 : 5;
                    break;
                case 1: 
                    jobBonus = skill.Definition.Type == SkillType.Attack || skill.Definition.Type == SkillType.Buff ? 10 : 5;
                    break;
                case 2: 
                    jobBonus = skill.Definition.Type == SkillType.Heal || skill.Definition.Type == SkillType.Summon ? 10 : 5;
                    break;
            }
            
            return baseExp + levelBonus + jobBonus;
        }

        
        
        
        private bool CheckSkillLevelUpConditions(Player caster, PlayerSkill skill)
        {
            
            if (caster.Level < skill.Definition.RequireLevel * skill.Level)
            {
                return false;
            }
            
            
            int requiredUses = skill.Level * 50; 
            if (skill.UseCount < requiredUses)
            {
                return false;
            }
            
            
            int nextKey = skill.Definition.LevelData.ContainsKey(0) ? (skill.Level + 1) : (skill.Level + 2);
            var nextLevelData = skill.Definition.GetLevelData(nextKey);
            if (nextLevelData != null && caster.Gold < nextLevelData.LearnCost)
            {
                return false;
            }
            
            return true;
        }
    }
}
