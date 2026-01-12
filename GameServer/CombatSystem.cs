using System;
using System.Collections.Generic;
using MirCommon;

namespace GameServer
{
    public interface ICombatEntity
    {
        uint Id { get; }
        string Name { get; }
        int CurrentHP { get; set; }
        int MaxHP { get; set; }
        int CurrentMP { get; set; }
        int MaxMP { get; set; }
        byte Level { get; set; }
        CombatStats Stats { get; set; }
        bool IsDead { get; set; }
        BuffManager BuffManager { get; }
        int X { get; set; }
        int Y { get; set; }

        void Heal(int amount);
        void RestoreMP(int amount);
        bool ConsumeMP(int amount);
        bool TakeDamage(ICombatEntity attacker, int damage, DamageType damageType);
        CombatResult Attack(ICombatEntity target, DamageType damageType = DamageType.Physics);
    }

    public enum DamageType
    {
        Physics = 0,  
        Magic = 1,   
        Poison = 2, 
    }

    public class CombatResult
    {
        public bool Hit { get; set; }      
        public int Damage { get; set; }    
        public bool Critical { get; set; }  
        public DamageType DamageType { get; set; }
        public bool TargetDied { get; set; }   
    }

    public class AttackRecord
    {
        public uint AttackerId { get; set; }
        public int TotalDamage { get; set; }
        public int HitCount { get; set; }
        public DateTime LastAttackTime { get; set; }

        public AttackRecord(uint attackerId)
        {
            AttackerId = attackerId;
            TotalDamage = 0;
            HitCount = 0;
            LastAttackTime = DateTime.Now;
        }

        public void AddDamage(int damage)
        {
            TotalDamage += damage;
            HitCount++;
            LastAttackTime = DateTime.Now;
        }
    }


    public abstract class CombatEntity : ICombatEntity
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }
        public int CurrentMP { get; set; }
        public int MaxMP { get; set; }
        public byte Level { get; set; }
        public CombatStats Stats { get; set; }
        public bool IsDead { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        protected Dictionary<uint, AttackRecord> _attackRecords = new();
        protected readonly object _recordLock = new();

        protected uint _targetId = 0;
        
        public BuffManager BuffManager { get; private set; }
        int ICombatEntity.X { get => X; set => X = value; }
        int ICombatEntity.Y { get => Y; set => Y = value; }

        public CombatEntity()
        {
            Stats = new CombatStats();
            Level = 1;
            MaxHP = 100;
            CurrentHP = 100;
            MaxMP = 100;
            CurrentMP = 100;
            BuffManager = new BuffManager(this);
        }

        public virtual CombatResult Attack(ICombatEntity target, DamageType damageType = DamageType.Physics)
        {
            var result = new CombatResult
            {
                DamageType = damageType,
                Hit = false,
                Damage = 0,
                Critical = false
            };

            if (!CheckHit(target))
            {
                return result;
            }

            result.Hit = true;

            int baseDamage = CalculateDamage(damageType);
            
            if (CheckCritical())
            {
                result.Critical = true;
                baseDamage = (int)(baseDamage * 1.5f);
            }

            int finalDamage = ApplyDefence(target, baseDamage, damageType);
            result.Damage = Math.Max(1, finalDamage); 

            result.TargetDied = target.TakeDamage(this, result.Damage, damageType);

            RecordAttack(target.Id, result.Damage);

            return result;
        }

        public virtual bool TakeDamage(ICombatEntity attacker, int damage, DamageType damageType)
        {
            if (IsDead) return false;

            CurrentHP -= damage;
            
            if (CurrentHP <= 0)
            {
                CurrentHP = 0;
                IsDead = true;
                OnDeath(attacker);
                return true;
            }

            OnDamaged(attacker, damage, damageType);
            return false;
        }

        protected virtual bool CheckHit(ICombatEntity target)
        {
            int hitRate = Stats.Accuracy; 
            int escape = target.Stats.Agility; 
            if (escape <= 0)
                return true;  
            
            int minEscape = Math.Max(1, escape / 15);
            int randomEscape = Random.Shared.Next(minEscape, escape + 1);
            
            return hitRate >= randomEscape;
        }

        protected virtual bool CheckCritical()
        {
            int baseCritRate = Stats.CriticalRate;
            
            int luckyBonus = Stats.Lucky / 2;
            
            int totalCritRate = baseCritRate + luckyBonus;
            
            totalCritRate = Math.Clamp(totalCritRate, 0, 50);
            
            if (Stats.Curse > 0)
            {
                totalCritRate -= Stats.Curse;
                totalCritRate = Math.Max(0, totalCritRate);
            }
            
            return Random.Shared.Next(100) < totalCritRate;
        }

        protected virtual int CalculateDamage(DamageType damageType)
        {
            int minDamage, maxDamage;
            
            switch (damageType)
            {
                case DamageType.Magic:
                    minDamage = Stats.MinMC;
                    maxDamage = Stats.MaxMC;
                    if (Stats.Lucky > 0)
                    {
                        minDamage += Stats.Lucky / 3;
                    }
                    break;
                case DamageType.Physics:
                default:
                    minDamage = Stats.MinDC;
                    maxDamage = Stats.MaxDC;
                    if (Stats.Lucky > 0)
                    {
                        minDamage += Stats.Lucky / 2;
                    }
                    break;
                case DamageType.Poison:
                    minDamage = Stats.MinSC;
                    maxDamage = Stats.MaxSC;
                    if (Stats.Lucky > 0)
                    {
                        minDamage += Stats.Lucky / 4;
                    }
                    break;
            }

            if (minDamage > maxDamage)
                minDamage = maxDamage;
                
            if (minDamage == maxDamage)
                return minDamage;
                
            return Random.Shared.Next(minDamage, maxDamage + 1);
        }

        protected virtual int ApplyDefence(ICombatEntity target, int damage, DamageType damageType)
        {
            int defence = 0;
            
            switch (damageType)
            {
                case DamageType.Magic:
                    if (target.Stats.MinMAC < target.Stats.MaxMAC)
                        defence = Random.Shared.Next(target.Stats.MinMAC, target.Stats.MaxMAC + 1);
                    else
                        defence = target.Stats.MinMAC;
                    break;
                    
                case DamageType.Physics:
                    if (target.Stats.MinAC < target.Stats.MaxAC)
                        defence = Random.Shared.Next(target.Stats.MinAC, target.Stats.MaxAC + 1);
                    else
                        defence = target.Stats.MinAC;
                    break;
                    
                case DamageType.Poison:
                    defence = target.Stats.PoisonResistance / 10; 
                    break;
                    
                default:
                    defence = 0;
                    break;
            }
            
            float damageReduction = 0;
            switch (damageType)
            {
                case DamageType.Magic:
                    damageReduction = target.Stats.MagicResistance / 100.0f;
                    break;
                case DamageType.Physics:
                    damageReduction = target.Stats.PhysicalResistance / 100.0f;
                    break;
                case DamageType.Poison:
                    damageReduction = target.Stats.PoisonResistance / 100.0f;
                    break;
            }
            
            int damageAfterDefence = Math.Max(0, damage - defence);
            int finalDamage = (int)(damageAfterDefence * (1.0f - damageReduction));
            
            if (damage > 0 && finalDamage <= 0)
                finalDamage = 1;
                
            return finalDamage;
        }

        protected void RecordAttack(uint targetId, int damage)
        {
            lock (_recordLock)
            {
                if (!_attackRecords.TryGetValue(targetId, out var record))
                {
                    record = new AttackRecord(Id);
                    _attackRecords[targetId] = record;
                }
                record.AddDamage(damage);
            }
        }

        public AttackRecord? GetAttackRecord(uint targetId)
        {
            lock (_recordLock)
            {
                return _attackRecords.TryGetValue(targetId, out var record) ? record : null;
            }
        }

        public void CleanupOldRecords(TimeSpan timeout)
        {
            lock (_recordLock)
            {
                var now = DateTime.Now;
                var toRemove = new List<uint>();
                
                foreach (var kvp in _attackRecords)
                {
                    if (now - kvp.Value.LastAttackTime > timeout)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var id in toRemove)
                {
                    _attackRecords.Remove(id);
                }
            }
        }

        public virtual void Heal(int amount)
        {
            if (IsDead) return;
            
            CurrentHP = Math.Min(CurrentHP + amount, MaxHP);
        }

        public virtual void RestoreMP(int amount)
        {
            if (IsDead) return;
            
            CurrentMP = Math.Min(CurrentMP + amount, MaxMP);
        }

        public virtual bool ConsumeMP(int amount)
        {
            if (CurrentMP < amount) return false;
            
            CurrentMP -= amount;
            return true;
        }

        protected virtual void OnDeath(ICombatEntity killer) { }
        protected virtual void OnDamaged(ICombatEntity attacker, int damage, DamageType damageType) { }
        protected virtual void OnKilledTarget(ICombatEntity target) { }
    }

    public class CombatSystemManager
    {
        private static CombatSystemManager? _instance;
        public static CombatSystemManager Instance => _instance ??= new CombatSystemManager();

        private CombatSystemManager() { }

        public CombatResult ExecuteCombat(ICombatEntity attacker, ICombatEntity target, DamageType damageType = DamageType.Physics)
        {
            if (attacker.IsDead || target.IsDead)
            {
                return new CombatResult { Hit = false };
            }

            var result = attacker.Attack(target, damageType);
            
            if (result.Hit)
            {
                LogCombat(attacker, target, result);
            }

            return result;
        }

        public List<CombatResult> ExecuteAreaAttack(ICombatEntity attacker, List<ICombatEntity> targets, DamageType damageType = DamageType.Physics)
        {
            var results = new List<CombatResult>();
            
            foreach (var target in targets)
            {
                if (target.IsDead) continue;
                
                var result = ExecuteCombat(attacker, target, damageType);
                results.Add(result);
            }

            return results;
        }

        private void LogCombat(ICombatEntity attacker, ICombatEntity target, CombatResult result)
        {
            string msg = $"{attacker.Name} 攻击 {target.Name} ";
            
            if (result.Hit)
            {
                msg += $"命中! 造成 {result.Damage} 点";
                msg += result.DamageType == DamageType.Magic ? "魔法" : "物理";
                msg += "伤害";
                
                if (result.Critical)
                {
                    msg += " (暴击!)";
                }

                if (result.TargetDied)
                {
                    msg += $" {target.Name} 已死亡!";
                }
            }
            else
            {
                msg += "未命中!";
            }

            Console.WriteLine($"[战斗] {msg}");
        }

        public int CalculateExp(ICombatEntity killer, ICombatEntity target)
        {
            if (target is MonsterEx monster)
            {
                uint exp = monster.ExpValue;
                if (exp > int.MaxValue)
                    return int.MaxValue;
                return (int)exp;
            }

            int baseExp = target.Level * 10;
            
            int levelDiff = target.Level - killer.Level;
            float modifier = 1.0f + (levelDiff * 0.1f);
            modifier = Math.Clamp(modifier, 0.1f, 2.0f);

            return (int)(baseExp * modifier);
        }
    }
}
