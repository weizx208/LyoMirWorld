using System;
using System.Collections.Generic;

namespace GameServer
{
    
    
    
    public class MonsterClass
    {
        
        public MonsterBaseInfo Base { get; set; } = new MonsterBaseInfo();
        
        
        public MonsterProp Prop { get; set; } = new MonsterProp();
        
        
        public MonsterSpecialProp SProp { get; set; } = new MonsterSpecialProp();
        
        
        public MonsterAISet AISet { get; set; } = new MonsterAISet();
        
        
        public MonsterPetSet PetSet { get; set; } = new MonsterPetSet();
        
        
        public MonsterAttackDesc AttackDesc { get; set; } = new MonsterAttackDesc();
        
        
        public MonsterChangeInto[] ChangeInto { get; set; } = new MonsterChangeInto[3];
        
        
        public string? BornScript { get; set; }
        public string? GotTargetScript { get; set; }
        public string? KillTargetScript { get; set; }
        public string? HurtScript { get; set; }
        public string? DeathScript { get; set; }
        
        
        public object? DownItems { get; set; }
        
        
        public int Count { get; set; }
        
        public MonsterClass()
        {
            for (int i = 0; i < 3; i++)
            {
                ChangeInto[i] = new MonsterChangeInto();
            }
        }
    }

    
    
    
    public class MonsterBaseInfo
    {
        public string ClassName { get; set; } = string.Empty;  
        public string ViewName { get; set; } = string.Empty;   
        public byte Race { get; set; }                         
        public byte Image { get; set; }                        
        public byte Level { get; set; }                        
        public byte NameColor { get; set; }                    
        public uint Feature { get; set; }                      
        public int MonsterId { get; set; }                     
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

    
    
    
    public class MonsterSpecialProp
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

    
    
    
    public class MonsterChangeInto
    {
        public AttackSituation Situation1 { get; set; } = new AttackSituation();
        public AttackSituation Situation2 { get; set; } = new AttackSituation();
        public string ChangeInto { get; set; } = string.Empty; 
        public int AppendEffect { get; set; }                  
        public bool Anim { get; set; }                         
        public bool Enabled { get; set; }                      
    }

    
    
    
    public class AttackSituation
    {
        public int Situation { get; set; }                     
        public int Param { get; set; }                         
    }

    
    
    
    public class MonsterGen
    {
        public string MonsterName { get; set; } = string.Empty; 
        public int MapId { get; set; }                         
        public int X { get; set; }                             
        public int Y { get; set; }                             
        public int Range { get; set; }                         
        public int MaxCount { get; set; }                      
        public int RefreshDelay { get; set; }                  
        public int CurrentCount { get; set; }                  
        public int ErrorTime { get; set; }                     
        public DateTime LastRefreshTime { get; set; }          
        public string? ScriptPage { get; set; }                
        public bool StartWhenAllDead { get; set; }             

        public MonsterGen()
        {
            MonsterName = string.Empty;
            MapId = 0;
            X = 0;
            Y = 0;
            Range = 0;
            MaxCount = 0;
            RefreshDelay = 0;
            CurrentCount = 0;
            ErrorTime = 0;
            LastRefreshTime = DateTime.MinValue;
            ScriptPage = null;
            StartWhenAllDead = false;
        }
    }
}
