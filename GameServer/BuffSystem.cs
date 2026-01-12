using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{

    public enum GodBlessType
    {
        DoubleExp = 0,      
        DoubleDrop = 1,     
        NoPk = 2,          
        NoDeath = 3,       
        Max
    }
    public enum BuffType
    {
        Buff = 0,           
        Debuff = 1,         
        Control = 2,        
        Special = 3         
    }

    public enum BuffEffectType
    {
        IncreaseHP = 0,         
        IncreaseMP = 1,         
        IncreaseDC = 2,         
        IncreaseAC = 3,         
        IncreaseMAC = 4,        
        IncreaseAccuracy = 5,   
        IncreaseAgility = 6,    
        IncreaseSpeed = 7,      
        IncreaseLucky = 8,      
        
        DecreaseHP = 10,        
        DecreaseMP = 11,        
        DecreaseDC = 12,        
        DecreaseAC = 13,        
        DecreaseMAC = 14,       
        DecreaseAccuracy = 15,  
        DecreaseAgility = 16,   
        DecreaseSpeed = 17,     
        
        HPRegen = 20,           
        MPRegen = 21,           
        Poison = 22,            
        Burn = 23,              
        Bleed = 24,             
        
        Stun = 30,              
        Freeze = 31,            
        Sleep = 32,             
        Silence = 33,           
        Root = 34,              
        Slow = 35,              
        
        Invincible = 40,        
        Shield = 41,            
        Invisible = 42,         
        DamageReflect = 43,     
        Vampire = 44,           
        CriticalUp = 45,        
        ExpUp = 46              
    }

    public class BuffDefinition
    {
        public int BuffId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public BuffType Type { get; set; }
        public BuffEffectType EffectType { get; set; }
        public int IconId { get; set; }
        
        public int Value { get; set; }
        public float Percentage { get; set; }
        
        public int Duration { get; set; }          
        public int TickInterval { get; set; }      
        
        public int MaxStack { get; set; } = 1;
        public bool RefreshDuration { get; set; } = true;  
        
        public List<int> MutexBuffs { get; set; } = new();  
        
        public bool CanDispel { get; set; } = true;
        public bool CanPurge { get; set; } = true;
        
        public BuffDefinition(int buffId, string name, BuffEffectType effectType)
        {
            BuffId = buffId;
            Name = name;
            EffectType = effectType;
            Type = DetermineType(effectType);
        }

        private BuffType DetermineType(BuffEffectType effectType)
        {
            int value = (int)effectType;
            if (value >= 0 && value < 10) return BuffType.Buff;
            if (value >= 10 && value < 20) return BuffType.Debuff;
            if (value >= 30 && value < 40) return BuffType.Control;
            return BuffType.Special;
        }
    }

    public class BuffInstance
    {
        public long InstanceId { get; set; }
        public BuffDefinition Definition { get; set; }
        public ICombatEntity Caster { get; set; }     
        public ICombatEntity Target { get; set; }     
        public int StackCount { get; set; } = 1;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime LastTickTime { get; set; }
        public bool IsExpired { get; set; }

        public BuffInstance(BuffDefinition definition, ICombatEntity caster, ICombatEntity target, long instanceId)
        {
            Definition = definition;
            Caster = caster;
            Target = target;
            InstanceId = instanceId;
            StartTime = DateTime.Now;
            EndTime = StartTime.AddMilliseconds(definition.Duration);
            LastTickTime = StartTime;
        }

        public TimeSpan GetRemainingTime()
        {
            var remaining = EndTime - DateTime.Now;
            return remaining.TotalMilliseconds > 0 ? remaining : TimeSpan.Zero;
        }

        public bool ShouldTick()
        {
            if (Definition.TickInterval <= 0)
                return false;

            var elapsed = (DateTime.Now - LastTickTime).TotalMilliseconds;
            return elapsed >= Definition.TickInterval;
        }

        public void Tick()
        {
            LastTickTime = DateTime.Now;
        }

        public bool CheckExpired()
        {
            if (DateTime.Now >= EndTime)
            {
                IsExpired = true;
                return true;
            }
            return false;
        }

        public void Refresh()
        {
            if (Definition.RefreshDuration)
            {
                StartTime = DateTime.Now;
                EndTime = StartTime.AddMilliseconds(Definition.Duration);
            }
        }

        public void AddStack()
        {
            if (StackCount < Definition.MaxStack)
            {
                StackCount++;
            }
        }
    }

    public class BuffManager
    {
        private readonly ICombatEntity _owner;
        private readonly Dictionary<int, BuffInstance> _buffs = new();
        private readonly object _lock = new();
        private long _nextInstanceId = 1;

        public BuffManager(ICombatEntity owner)
        {
            _owner = owner;
        }

        public bool AddBuff(BuffDefinition definition, ICombatEntity caster)
        {
            lock (_lock)
            {
                foreach (var mutexBuffId in definition.MutexBuffs)
                {
                    if (_buffs.ContainsKey(mutexBuffId))
                    {
                        RemoveBuff(mutexBuffId);
                    }
                }

                if (_buffs.TryGetValue(definition.BuffId, out var existingBuff))
                {
                    if (definition.MaxStack > 1)
                    {
                        existingBuff.AddStack();
                    }
                    existingBuff.Refresh();
                    
                    LogManager.Default.Debug($"{_owner.Name} 刷新Buff: {definition.Name}");
                    return true;
                }

                var instanceId = System.Threading.Interlocked.Increment(ref _nextInstanceId);
                var buff = new BuffInstance(definition, caster, _owner, instanceId);
                _buffs[definition.BuffId] = buff;

                ApplyBuffEffect(buff);

                LogManager.Default.Info($"{_owner.Name} 获得Buff: {definition.Name} 持续{definition.Duration/1000}秒");
                return true;
            }
        }

        public bool RemoveBuff(int buffId)
        {
            lock (_lock)
            {
                if (_buffs.TryGetValue(buffId, out var buff))
                {
                    RemoveBuffEffect(buff);
                    
                    _buffs.Remove(buffId);
                    LogManager.Default.Debug($"{_owner.Name} 移除Buff: {buff.Definition.Name}");
                    return true;
                }
                return false;
            }
        }

        public void Update()
        {
            lock (_lock)
            {
                var expiredBuffs = new List<int>();

                foreach (var buff in _buffs.Values)
                {
                    if (buff.CheckExpired())
                    {
                        expiredBuffs.Add(buff.Definition.BuffId);
                        continue;
                    }

                    if (buff.ShouldTick())
                    {
                        ApplyTickEffect(buff);
                        buff.Tick();
                    }
                }

                foreach (var buffId in expiredBuffs)
                {
                    RemoveBuff(buffId);
                }
            }
        }

        private void ApplyBuffEffect(BuffInstance buff)
        {
            var def = buff.Definition;
            int value = def.Value * buff.StackCount;

            switch (def.EffectType)
            {
                case BuffEffectType.IncreaseDC:
                    _owner.Stats.MinDC += value;
                    _owner.Stats.MaxDC += value;
                    break;
                case BuffEffectType.IncreaseAC:
                    _owner.Stats.MinAC += value;
                    _owner.Stats.MaxAC += value;
                    break;
                case BuffEffectType.IncreaseMAC:
                    _owner.Stats.MinMAC += value;
                    _owner.Stats.MaxMAC += value;
                    break;
                case BuffEffectType.IncreaseAccuracy:
                    _owner.Stats.Accuracy += value;
                    break;
                case BuffEffectType.IncreaseAgility:
                    _owner.Stats.Agility += value;
                    break;
                case BuffEffectType.IncreaseLucky:
                    _owner.Stats.Lucky += value;
                    break;
                    
                case BuffEffectType.DecreaseDC:
                    _owner.Stats.MinDC -= value;
                    _owner.Stats.MaxDC -= value;
                    break;
                case BuffEffectType.DecreaseAC:
                    _owner.Stats.MinAC -= value;
                    _owner.Stats.MaxAC -= value;
                    break;
                case BuffEffectType.DecreaseMAC:
                    _owner.Stats.MinMAC -= value;
                    _owner.Stats.MaxMAC -= value;
                    break;
            }
        }

        private void RemoveBuffEffect(BuffInstance buff)
        {
            var def = buff.Definition;
            int value = def.Value * buff.StackCount;

            switch (def.EffectType)
            {
                case BuffEffectType.IncreaseDC:
                    _owner.Stats.MinDC -= value;
                    _owner.Stats.MaxDC -= value;
                    break;
                case BuffEffectType.IncreaseAC:
                    _owner.Stats.MinAC -= value;
                    _owner.Stats.MaxAC -= value;
                    break;
                case BuffEffectType.IncreaseMAC:
                    _owner.Stats.MinMAC -= value;
                    _owner.Stats.MaxMAC -= value;
                    break;
                case BuffEffectType.IncreaseAccuracy:
                    _owner.Stats.Accuracy -= value;
                    break;
                case BuffEffectType.IncreaseAgility:
                    _owner.Stats.Agility -= value;
                    break;
                case BuffEffectType.IncreaseLucky:
                    _owner.Stats.Lucky -= value;
                    break;
                    
                case BuffEffectType.DecreaseDC:
                    _owner.Stats.MinDC += value;
                    _owner.Stats.MaxDC += value;
                    break;
                case BuffEffectType.DecreaseAC:
                    _owner.Stats.MinAC += value;
                    _owner.Stats.MaxAC += value;
                    break;
                case BuffEffectType.DecreaseMAC:
                    _owner.Stats.MinMAC += value;
                    _owner.Stats.MaxMAC += value;
                    break;
            }
        }

        private void ApplyTickEffect(BuffInstance buff)
        {
            var def = buff.Definition;
            int value = def.Value * buff.StackCount;

            switch (def.EffectType)
            {
                case BuffEffectType.HPRegen:
                    _owner.Heal(value);
                    break;
                case BuffEffectType.MPRegen:
                    _owner.RestoreMP(value);
                    break;
                case BuffEffectType.Poison:
                case BuffEffectType.Burn:
                case BuffEffectType.Bleed:
                    _owner.TakeDamage(buff.Caster, value, DamageType.Poison);
                    break;
            }
        }

        public BuffInstance? GetBuff(int buffId)
        {
            lock (_lock)
            {
                _buffs.TryGetValue(buffId, out var buff);
                return buff;
            }
        }

        public bool HasBuff(int buffId)
        {
            lock (_lock)
            {
                return _buffs.ContainsKey(buffId);
            }
        }

        public bool HasBuffType(BuffType type)
        {
            lock (_lock)
            {
                return _buffs.Values.Any(b => b.Definition.Type == type);
            }
        }

        public List<BuffInstance> GetAllBuffs()
        {
            lock (_lock)
            {
                return _buffs.Values.ToList();
            }
        }

        public void ClearAllBuffs()
        {
            lock (_lock)
            {
                var buffIds = _buffs.Keys.ToList();
                foreach (var buffId in buffIds)
                {
                    RemoveBuff(buffId);
                }
            }
        }

        public void DispelDebuffs(int count = 1)
        {
            lock (_lock)
            {
                var debuffs = _buffs.Values
                    .Where(b => b.Definition.Type == BuffType.Debuff && b.Definition.CanDispel)
                    .OrderBy(b => b.EndTime)
                    .Take(count)
                    .ToList();

                foreach (var debuff in debuffs)
                {
                    RemoveBuff(debuff.Definition.BuffId);
                }
            }
        }

        public void PurgeBuffs(int count = 1)
        {
            lock (_lock)
            {
                var buffs = _buffs.Values
                    .Where(b => b.Definition.Type == BuffType.Buff && b.Definition.CanPurge)
                    .OrderBy(b => b.EndTime)
                    .Take(count)
                    .ToList();

                foreach (var buff in buffs)
                {
                    RemoveBuff(buff.Definition.BuffId);
                }
            }
        }

        public bool IsStunned()
        {
            lock (_lock)
            {
                return _buffs.Values.Any(b => 
                    b.Definition.EffectType == BuffEffectType.Stun ||
                    b.Definition.EffectType == BuffEffectType.Freeze ||
                    b.Definition.EffectType == BuffEffectType.Sleep);
            }
        }

        public bool IsSilenced()
        {
            lock (_lock)
            {
                return _buffs.Values.Any(b => b.Definition.EffectType == BuffEffectType.Silence);
            }
        }

        public bool IsInvincible()
        {
            lock (_lock)
            {
                return _buffs.Values.Any(b => b.Definition.EffectType == BuffEffectType.Invincible);
            }
        }
    }

    public class BuffDefinitionManager
    {
        private static BuffDefinitionManager? _instance;
        public static BuffDefinitionManager Instance => _instance ??= new BuffDefinitionManager();

        private readonly ConcurrentDictionary<int, BuffDefinition> _definitions = new();

        private BuffDefinitionManager()
        {
            InitializeDefaultBuffs();
        }

        private void InitializeDefaultBuffs()
        {
            AddDefinition(new BuffDefinition(1001, "力量祝福", BuffEffectType.IncreaseDC)
            {
                Description = "增加攻击力",
                Value = 5,
                Duration = 300000, 
                IconId = 1
            });

            AddDefinition(new BuffDefinition(1002, "防御祝福", BuffEffectType.IncreaseAC)
            {
                Description = "增加防御力",
                Value = 5,
                Duration = 300000,
                IconId = 2
            });

            AddDefinition(new BuffDefinition(1003, "魔法祝福", BuffEffectType.IncreaseMAC)
            {
                Description = "增加魔法防御",
                Value = 5,
                Duration = 300000,
                IconId = 3
            });

            AddDefinition(new BuffDefinition(1004, "幸运祝福", BuffEffectType.IncreaseLucky)
            {
                Description = "增加幸运值",
                Value = 3,
                Duration = 600000, 
                IconId = 4
            });

            AddDefinition(new BuffDefinition(1005, "生命恢复", BuffEffectType.HPRegen)
            {
                Description = "持续恢复生命值",
                Value = 5,
                Duration = 30000,
                TickInterval = 1000, 
                IconId = 5
            });

            AddDefinition(new BuffDefinition(1006, "魔法恢复", BuffEffectType.MPRegen)
            {
                Description = "持续恢复魔法值",
                Value = 5,
                Duration = 30000,
                TickInterval = 1000,
                IconId = 6
            });

            AddDefinition(new BuffDefinition(1007, "护盾", BuffEffectType.Shield)
            {
                Description = "吸收伤害的护盾",
                Value = 100,
                Duration = 10000,
                IconId = 7
            });

            AddDefinition(new BuffDefinition(1008, "无敌", BuffEffectType.Invincible)
            {
                Description = "免疫所有伤害",
                Duration = 3000,
                IconId = 8,
                CanDispel = false,
                CanPurge = false
            });

            AddDefinition(new BuffDefinition(1009, "暴击强化", BuffEffectType.CriticalUp)
            {
                Description = "提高暴击率",
                Value = 20, 
                Duration = 60000,
                IconId = 9
            });

            AddDefinition(new BuffDefinition(1010, "经验加成", BuffEffectType.ExpUp)
            {
                Description = "增加获得的经验",
                Percentage = 1.5f, 
                Duration = 1800000, 
                IconId = 10
            });

            AddDefinition(new BuffDefinition(2001, "中毒", BuffEffectType.Poison)
            {
                Description = "持续损失生命值",
                Value = 3,
                Duration = 10000,
                TickInterval = 1000,
                MaxStack = 5,
                IconId = 21
            });

            AddDefinition(new BuffDefinition(2002, "燃烧", BuffEffectType.Burn)
            {
                Description = "火焰灼烧",
                Value = 5,
                Duration = 8000,
                TickInterval = 1000,
                MaxStack = 3,
                IconId = 22
            });

            AddDefinition(new BuffDefinition(2003, "流血", BuffEffectType.Bleed)
            {
                Description = "流血不止",
                Value = 4,
                Duration = 15000,
                TickInterval = 1000,
                IconId = 23
            });

            AddDefinition(new BuffDefinition(2004, "虚弱", BuffEffectType.DecreaseDC)
            {
                Description = "降低攻击力",
                Value = 5,
                Duration = 30000,
                IconId = 24
            });

            AddDefinition(new BuffDefinition(2005, "破甲", BuffEffectType.DecreaseAC)
            {
                Description = "降低防御力",
                Value = 5,
                Duration = 30000,
                IconId = 25
            });

            AddDefinition(new BuffDefinition(2006, "减速", BuffEffectType.Slow)
            {
                Description = "移动速度降低",
                Value = 30, 
                Duration = 5000,
                IconId = 26
            });

            AddDefinition(new BuffDefinition(3001, "眩晕", BuffEffectType.Stun)
            {
                Description = "无法移动和攻击",
                Duration = 2000,
                IconId = 31,
                CanDispel = false
            });

            AddDefinition(new BuffDefinition(3002, "冰冻", BuffEffectType.Freeze)
            {
                Description = "被冰冻住",
                Duration = 3000,
                IconId = 32
            });

            AddDefinition(new BuffDefinition(3003, "沉默", BuffEffectType.Silence)
            {
                Description = "无法使用技能",
                Duration = 5000,
                IconId = 33
            });

            AddDefinition(new BuffDefinition(3004, "定身", BuffEffectType.Root)
            {
                Description = "无法移动",
                Duration = 4000,
                IconId = 34
            });

            LogManager.Default.Info($"已加载 {_definitions.Count} 个Buff定义");
        }

        public void AddDefinition(BuffDefinition definition)
        {
            _definitions[definition.BuffId] = definition;
        }

        public BuffDefinition? GetDefinition(int buffId)
        {
            _definitions.TryGetValue(buffId, out var definition);
            return definition;
        }

        public List<BuffDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }

        public List<BuffDefinition> GetBuffsByType(BuffType type)
        {
            return _definitions.Values
                .Where(b => b.Type == type)
                .ToList();
        }
    }
}
