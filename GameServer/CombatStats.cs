using System;

namespace GameServer
{
    public class CombatStats
    {
        public int HP { get; set; }
        
        public int MaxHP { get; set; }
        
        public int MP { get; set; }
        
        public int MaxMP { get; set; }
        
        public int MinDC { get; set; }
        
        public int MaxDC { get; set; }
        
        public int MinMC { get; set; }
        
        public int MaxMC { get; set; }
        
        public int MinSC { get; set; }
        
        public int MaxSC { get; set; }
        
        public int MinAC { get; set; }
        
        public int MaxAC { get; set; }
        
        public int MinMAC { get; set; }
        
        public int MaxMAC { get; set; }
        
        public int Accuracy { get; set; }
        
        public int Agility { get; set; }
        
        public int Lucky { get; set; }
        
        public int Curse { get; set; }
        
        public int AttackSpeed { get; set; }
        
        public int CastSpeed { get; set; }
        
        public int MoveSpeed { get; set; }
        
        public int CriticalRate { get; set; }
        
        public int CriticalDamage { get; set; }
        
        public int DodgeRate { get; set; }
        
        public int HitRate { get; set; }
        
        public int PhysicalResistance { get; set; }
        
        public int MagicResistance { get; set; }
        
        public int PoisonResistance { get; set; }
        
        public int FreezeResistance { get; set; }
        
        public int StunResistance { get; set; }
        
        public int SilenceResistance { get; set; }
        
        public int HPRegen { get; set; }
        
        public int MPRegen { get; set; }
        
        public int ExpBonus { get; set; }
        
        public int DropBonus { get; set; }
        
        public int GoldBonus { get; set; }
        
        public int DamageBonus { get; set; }
        
        public int DamageReduction { get; set; }
        
        public CombatStats()
        {
            HP = 100;
            MaxHP = 100;
            MP = 100;
            MaxMP = 100;
            MinDC = 1;
            MaxDC = 3;
            Accuracy = 5;
            Agility = 5;
            AttackSpeed = 1000; 
            MoveSpeed = 400;    
            CriticalRate = 5;
            CriticalDamage = 150;
            DodgeRate = 5;
            HitRate = 95;
            HPRegen = 5;
            MPRegen = 5;
        }
        
        public int GetTotalDC()
        {
            return MinDC + MaxDC;
        }
        
        public int GetTotalMC()
        {
            return MinMC + MaxMC;
        }
        
        public int GetTotalSC()
        {
            return MinSC + MaxSC;
        }
        
        public int GetTotalAC()
        {
            return MinAC + MaxAC;
        }
        
        public int GetTotalMAC()
        {
            return MinMAC + MaxMAC;
        }
        
        public float GetAverageDC()
        {
            return (MinDC + MaxDC) / 2.0f;
        }
        
        public float GetAverageMC()
        {
            return (MinMC + MaxMC) / 2.0f;
        }
        
        public float GetAverageSC()
        {
            return (MinSC + MaxSC) / 2.0f;
        }
        
        public float GetAverageAC()
        {
            return (MinAC + MaxAC) / 2.0f;
        }
        
        public float GetAverageMAC()
        {
            return (MinMAC + MaxMAC) / 2.0f;
        }
        
        public string GetDCRange()
        {
            return $"{MinDC}-{MaxDC}";
        }
        
        public string GetMCRange()
        {
            return $"{MinMC}-{MaxMC}";
        }
        
        public string GetSCRange()
        {
            return $"{MinSC}-{MaxSC}";
        }
        
        public string GetACRange()
        {
            return $"{MinAC}-{MaxAC}";
        }
        
        public string GetMACRange()
        {
            return $"{MinMAC}-{MaxMAC}";
        }
        
        public float GetHPPercentage()
        {
            if (MaxHP <= 0) return 0;
            return (float)HP / MaxHP * 100;
        }
        
        public float GetMPPercentage()
        {
            if (MaxMP <= 0) return 0;
            return (float)MP / MaxMP * 100;
        }
        
        public bool IsAlive()
        {
            return HP > 0;
        }
        
        public bool HasMP()
        {
            return MP > 0;
        }
        
        public void Heal(int amount)
        {
            HP = Math.Min(HP + amount, MaxHP);
        }
        
        public void RestoreMP(int amount)
        {
            MP = Math.Min(MP + amount, MaxMP);
        }
        
        public void TakeDamage(int damage)
        {
            HP = Math.Max(HP - damage, 0);
        }
        
        public void ConsumeMP(int amount)
        {
            MP = Math.Max(MP - amount, 0);
        }
        
        public void IncreaseMaxHP(int amount)
        {
            MaxHP += amount;
            HP += amount; 
        }
        
        public void IncreaseMaxMP(int amount)
        {
            MaxMP += amount;
            MP += amount; 
        }
        
        public void Reset()
        {
            HP = MaxHP;
            MP = MaxMP;
        }
        
        public CombatStats Clone()
        {
            return (CombatStats)MemberwiseClone();
        }
        
        public void Merge(CombatStats other)
        {
            if (other == null) return;
            
            MaxHP += other.MaxHP;
            MaxMP += other.MaxMP;
            MinDC += other.MinDC;
            MaxDC += other.MaxDC;
            MinMC += other.MinMC;
            MaxMC += other.MaxMC;
            MinSC += other.MinSC;
            MaxSC += other.MaxSC;
            MinAC += other.MinAC;
            MaxAC += other.MaxAC;
            MinMAC += other.MinMAC;
            MaxMAC += other.MaxMAC;
            Accuracy += other.Accuracy;
            Agility += other.Agility;
            Lucky += other.Lucky;
            Curse += other.Curse;
            AttackSpeed += other.AttackSpeed;
            CastSpeed += other.CastSpeed;
            MoveSpeed += other.MoveSpeed;
            CriticalRate += other.CriticalRate;
            CriticalDamage += other.CriticalDamage;
            DodgeRate += other.DodgeRate;
            HitRate += other.HitRate;
            PhysicalResistance += other.PhysicalResistance;
            MagicResistance += other.MagicResistance;
            PoisonResistance += other.PoisonResistance;
            FreezeResistance += other.FreezeResistance;
            StunResistance += other.StunResistance;
            SilenceResistance += other.SilenceResistance;
            HPRegen += other.HPRegen;
            MPRegen += other.MPRegen;
            ExpBonus += other.ExpBonus;
            DropBonus += other.DropBonus;
            GoldBonus += other.GoldBonus;
            DamageBonus += other.DamageBonus;
            DamageReduction += other.DamageReduction;
        }
        
        public void Remove(CombatStats other)
        {
            if (other == null) return;
            
            MaxHP -= other.MaxHP;
            MaxMP -= other.MaxMP;
            MinDC -= other.MinDC;
            MaxDC -= other.MaxDC;
            MinMC -= other.MinMC;
            MaxMC -= other.MaxMC;
            MinSC -= other.MinSC;
            MaxSC -= other.MaxSC;
            MinAC -= other.MinAC;
            MaxAC -= other.MaxAC;
            MinMAC -= other.MinMAC;
            MaxMAC -= other.MaxMAC;
            Accuracy -= other.Accuracy;
            Agility -= other.Agility;
            Lucky -= other.Lucky;
            Curse -= other.Curse;
            AttackSpeed -= other.AttackSpeed;
            CastSpeed -= other.CastSpeed;
            MoveSpeed -= other.MoveSpeed;
            CriticalRate -= other.CriticalRate;
            CriticalDamage -= other.CriticalDamage;
            DodgeRate -= other.DodgeRate;
            HitRate -= other.HitRate;
            PhysicalResistance -= other.PhysicalResistance;
            MagicResistance -= other.MagicResistance;
            PoisonResistance -= other.PoisonResistance;
            FreezeResistance -= other.FreezeResistance;
            StunResistance -= other.StunResistance;
            SilenceResistance -= other.SilenceResistance;
            HPRegen -= other.HPRegen;
            MPRegen -= other.MPRegen;
            ExpBonus -= other.ExpBonus;
            DropBonus -= other.DropBonus;
            GoldBonus -= other.GoldBonus;
            DamageBonus -= other.DamageBonus;
            DamageReduction -= other.DamageReduction;
            
            HP = Math.Min(HP, MaxHP);
            MP = Math.Min(MP, MaxMP);
            MinDC = Math.Max(MinDC, 0);
            MaxDC = Math.Max(MaxDC, 0);
            MinMC = Math.Max(MinMC, 0);
            MaxMC = Math.Max(MaxMC, 0);
            MinSC = Math.Max(MinSC, 0);
            MaxSC = Math.Max(MaxSC, 0);
            MinAC = Math.Max(MinAC, 0);
            MaxAC = Math.Max(MaxAC, 0);
            MinMAC = Math.Max(MinMAC, 0);
            MaxMAC = Math.Max(MaxMAC, 0);
            Accuracy = Math.Max(Accuracy, 0);
            Agility = Math.Max(Agility, 0);
            Lucky = Math.Max(Lucky, 0);
            Curse = Math.Max(Curse, 0);
        }
    }
}
