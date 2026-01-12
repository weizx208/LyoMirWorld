using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class Monster : AliveObject
    {
        
        public int MonsterId { get; set; }
        public MonsterDefinition Definition { get; private set; }
        
        
        public uint ExpReward { get; set; }
        public uint GoldReward { get; set; }
        
        
        public MonsterAIType AIType { get; set; }
        public AIState CurrentAIState { get; set; }
        private ushort _homeX;
        private ushort _homeY;
        public int WanderRange { get; set; } = 10;
        
        
        private DateTime _lastAttackTime;
        private DateTime _lastThinkTime;
        public int AttackRange { get; set; } = 1;
        public int ViewRange { get; set; } = 10;
        
        
        public bool CanRespawn { get; set; } = true;
        public int RespawnTime { get; set; } = 60; 
        public DateTime DeathTime { get; set; }
        
        
        public uint OwnerPlayerId { get; set; }
        public bool IsPet { get; set; }
        
        
        private readonly Dictionary<uint, int> _hatredList = new();
        private readonly object _hatredLock = new();

        public Monster(int monsterId, string name)
        {
            MonsterId = monsterId;
            Name = name;
            
            
            Definition = MonsterManager.Instance.GetDefinition(monsterId) 
                ?? new MonsterDefinition(monsterId, name);
            
            LoadFromDefinition();
            
            AIType = MonsterAIType.Passive;
            CurrentAIState = AIState.Idle;
            _lastThinkTime = DateTime.Now;
            _lastAttackTime = DateTime.Now;
        }

        private void LoadFromDefinition()
        {
            Level = Definition.Level;
            MaxHP = Definition.HP;
            CurrentHP = MaxHP;
            MaxMP = Definition.MP;
            CurrentMP = MaxMP;
            
            Stats.MinDC = Definition.MinDC;
            Stats.MaxDC = Definition.MaxDC;
            Stats.MinAC = Definition.AC;
            Stats.MaxAC = Definition.AC + 5;
            Stats.MinMAC = Definition.MAC;
            Stats.MaxMAC = Definition.MAC + 5;
            Stats.Accuracy = Definition.Accuracy;
            Stats.Agility = Definition.Agility;
            
            ExpReward = Definition.ExpReward;
            GoldReward = Definition.GoldReward;
            AttackRange = Definition.AttackRange;
            ViewRange = Definition.ViewRange;
            WalkSpeed = Definition.WalkSpeed;
            RunSpeed = Definition.RunSpeed;
            AIType = Definition.AIType;
            WanderRange = Definition.WanderRange;
        }

        public override ObjectType GetObjectType() => ObjectType.Monster;

        
        
        
        public override uint GetFeather()
        {
            
            
            return (uint)(((MonsterId & 0xFFFF) << 16) | 0x13);
        }

        
        
        
        public void SetHomePosition(ushort x, ushort y)
        {
            _homeX = x;
            _homeY = y;
        }

        public override void Update()
        {
            base.Update();
            
            if (IsDead)
            {
                
                if (CanRespawnNow())
                {
                    Respawn();
                }
                return;
            }

            
            if ((DateTime.Now - _lastThinkTime).TotalMilliseconds >= 500) 
            {
                Think();
                _lastThinkTime = DateTime.Now;
            }
        }

        #region AI系统

        
        
        
        private void Think()
        {
            
            switch (CurrentAIState)
            {
                case AIState.Idle:
                    ThinkIdle();
                    break;
                case AIState.Wander:
                    ThinkWander();
                    break;
                case AIState.Chase:
                    ThinkChase();
                    break;
                case AIState.Attack:
                    ThinkAttack();
                    break;
                case AIState.Return:
                    ThinkReturn();
                    break;
            }
        }

        
        
        
        private void ThinkIdle()
        {
            
            var target = FindTarget();
            if (target != null)
            {
                SetTarget(target);
                CurrentAIState = AIState.Chase;
                return;
            }

            
            if (AIType != MonsterAIType.Passive && Random.Shared.Next(100) < 10) 
            {
                CurrentAIState = AIState.Wander;
            }
        }

        
        
        
        private void ThinkWander()
        {
            
            var target = FindTarget();
            if (target != null)
            {
                SetTarget(target);
                CurrentAIState = AIState.Chase;
                return;
            }

            
            if (CurrentAction == ActionType.Stand)
            {
                
                int distanceFromHome = Math.Abs(X - _homeX) + Math.Abs(Y - _homeY);
                if (distanceFromHome > WanderRange)
                {
                    CurrentAIState = AIState.Return;
                    return;
                }

                
                var dir = (Direction)Random.Shared.Next(8);
                Walk(dir);
            }
        }

        
        
        
        private void ThinkChase()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
            {
                SetTarget(null);
                CurrentAIState = AIState.Return;
                return;
            }

            
            int distanceFromHome = Math.Abs(X - _homeX) + Math.Abs(Y - _homeY);
            if (distanceFromHome > WanderRange * 2)
            {
                SetTarget(null);
                CurrentAIState = AIState.Return;
                return;
            }

            int distance = Math.Abs(X - target.X) + Math.Abs(Y - target.Y);

            
            if (distance <= AttackRange)
            {
                CurrentAIState = AIState.Attack;
                return;
            }

            
            if (CurrentAction == ActionType.Stand)
            {
                var dir = GetDirection(X, Y, target.X, target.Y);
                
                
                if (distance > 3 && CanDoAction(ActionType.Run))
                {
                    Run(dir);
                }
                else
                {
                    Walk(dir);
                }
            }
        }

        
        
        
        private void ThinkAttack()
        {
            var target = GetTarget();
            if (target == null || target.IsDead)
            {
                SetTarget(null);
                CurrentAIState = AIState.Return;
                return;
            }

            int distance = Math.Abs(X - target.X) + Math.Abs(Y - target.Y);

            
            if (distance > AttackRange)
            {
                CurrentAIState = AIState.Chase;
                return;
            }

            
            if (CurrentAction == ActionType.Stand)
            {
                var now = DateTime.Now;
                if ((now - _lastAttackTime).TotalMilliseconds >= 1000) 
                {
                    var dir = GetDirection(X, Y, target.X, target.Y);
                    if (Attack(dir))
                    {
                        
                        DoAttackTarget(target);
                        _lastAttackTime = now;
                    }
                }
            }
        }

        
        
        
        private void ThinkReturn()
        {
            
            if (X == _homeX && Y == _homeY)
            {
                CurrentAIState = AIState.Idle;
                
                
                ClearHatred();
                
                
                CurrentHP = MaxHP;
                CurrentMP = MaxMP;
                return;
            }

            
            if (CurrentAction == ActionType.Stand)
            {
                var dir = GetDirection(X, Y, _homeX, _homeY);
                Run(dir);
            }
        }

        
        
        
        private AliveObject? FindTarget()
        {
            if (CurrentMap == null)
                return null;

            
            if (AIType == MonsterAIType.Passive)
            {
                
                var topHatred = GetTopHatredTarget();
                if (topHatred != null)
                    return topHatred;
                return null;
            }

            
            var players = CurrentMap.GetPlayersInRange(X, Y, ViewRange);
            
            
            var hatredTarget = GetTopHatredTarget();
            if (hatredTarget != null && players.Contains(hatredTarget as HumanPlayer))
                return hatredTarget;

            
            HumanPlayer? nearest = null;
            int minDistance = int.MaxValue;
            
            foreach (var player in players)
            {
                if (player.IsDead)
                    continue;

                int distance = Math.Abs(X - player.X) + Math.Abs(Y - player.Y);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = player;
                }
            }

            return nearest;
        }

        
        
        
        private void DoAttackTarget(AliveObject target)
        {
            if (target == null || target.IsDead)
                return;

            
            int damage = Random.Shared.Next(Stats.MinDC, Stats.MaxDC + 1);
            
            
            if (CheckHit(target))
            {
                target.BeAttack(this, damage, DamageType.Physics);
                
                if (target.IsDead)
                {
                    OnKilledTarget(target);
                }
            }
        }

        
        
        
        private bool CheckHit(AliveObject target)
        {
            int hitRate = Stats.Accuracy + Level;
            int dodgeRate = target.Stats.Agility + target.Level;
            
            int hitChance = 70 + (hitRate - dodgeRate) * 2;
            hitChance = Math.Clamp(hitChance, 30, 95);

            return Random.Shared.Next(100) < hitChance;
        }

        #endregion

        #region 仇恨系统

        
        
        
        public void AddHatred(uint objectId, int value)
        {
            lock (_hatredLock)
            {
                if (!_hatredList.ContainsKey(objectId))
                {
                    _hatredList[objectId] = 0;
                }
                _hatredList[objectId] += value;
            }
        }

        
        
        
        private AliveObject? GetTopHatredTarget()
        {
            lock (_hatredLock)
            {
                if (_hatredList.Count == 0)
                    return null;

                var topEntry = _hatredList.OrderByDescending(kvp => kvp.Value).First();
                
                
                if (CurrentMap != null)
                {
                    var players = CurrentMap.GetPlayersInRange(X, Y, ViewRange * 2);
                    return players.FirstOrDefault(p => p.ObjectId == topEntry.Key);
                }

                return null;
            }
        }

        
        
        
        private void ClearHatred()
        {
            lock (_hatredLock)
            {
                _hatredList.Clear();
            }
        }

        #endregion

        #region 重生系统

        
        
        
        public bool CanRespawnNow()
        {
            if (!CanRespawn || !IsDead)
                return false;

            return (DateTime.Now - DeathTime).TotalSeconds >= RespawnTime;
        }

        
        
        
        public void Respawn()
        {
            IsDead = false;
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
            CurrentAction = ActionType.Stand;
            CurrentAIState = AIState.Idle;
            
            
            if (CurrentMap != null)
            {
                CurrentMap.MoveObject(this, _homeX, _homeY);
            }
            else
            {
                X = _homeX;
                Y = _homeY;
            }
            
            
            ClearHatred();
            
            LogManager.Default.Debug($"怪物 {Name} 在 ({_homeX},{_homeY}) 重生");
        }

        #endregion

        #region 事件回调

        protected override void OnDeath(AliveObject killer)
        {
            base.OnDeath(killer);
            
            DeathTime = DateTime.Now;
            CurrentAIState = AIState.Idle;
            
            LogManager.Default.Info($"怪物 {Name} 被 {killer?.Name ?? "未知"} 击杀");
        }

        protected override void OnDamaged(AliveObject attacker, int damage, DamageType damageType)
        {
            base.OnDamaged(attacker, damage, damageType);
            
            
            if (attacker != null)
            {
                AddHatred(attacker.ObjectId, damage);
                
                
                if (AIType == MonsterAIType.Passive && CurrentAIState == AIState.Idle)
                {
                    SetTarget(attacker);
                    CurrentAIState = AIState.Chase;
                }
            }
        }

        #endregion

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            
            return base.GetViewMsg(out msg, viewer);
        }
    }

    
    
    
    public class MonsterDefinition
    {
        public int MonsterId { get; set; }
        public string Name { get; set; }
        public byte Level { get; set; }
        public int HP { get; set; }
        public int MP { get; set; }
        public int MinDC { get; set; }
        public int MaxDC { get; set; }
        public int AC { get; set; }
        public int MAC { get; set; }
        public int Accuracy { get; set; }
        public int Agility { get; set; }
        public uint ExpReward { get; set; }
        public uint GoldReward { get; set; }
        public int AttackRange { get; set; } = 1;
        public int ViewRange { get; set; } = 10;
        public byte WalkSpeed { get; set; } = 2;
        public byte RunSpeed { get; set; } = 2;
        public MonsterAIType AIType { get; set; } = MonsterAIType.Active;
        public int WanderRange { get; set; } = 10;

        public MonsterDefinition(int monsterId, string name)
        {
            MonsterId = monsterId;
            Name = name;
            Level = 1;
            HP = 100;
            MP = 0;
            MinDC = 1;
            MaxDC = 3;
            AC = 0;
            MAC = 0;
            Accuracy = 5;
            Agility = 5;
            ExpReward = 10;
            GoldReward = 5;
        }
    }

    
    
    
    public enum MonsterAIType
    {
        Passive = 0,    
        Active = 1,     
        Guard = 2,      
        Boss = 3        
    }

    
    
    
    public enum AIState
    {
        Idle = 0,       
        Wander = 1,     
        Chase = 2,      
        Attack = 3,     
        Return = 4,     
        Flee = 5        
    }

    
    
    
    public class MonsterManager
    {
        private static MonsterManager? _instance;
        public static MonsterManager Instance => _instance ??= new MonsterManager();

        private readonly Dictionary<int, MonsterDefinition> _definitions = new();
        private readonly object _lock = new();

        private MonsterManager()
        {
            InitializeDefaultMonsters();
        }

        
        
        
        private void InitializeDefaultMonsters()
        {
            
            AddDefinition(new MonsterDefinition(1, "鸡")
            {
                Level = 1,
                HP = 15,
                MinDC = 1,
                MaxDC = 2,
                ExpReward = 5,
                GoldReward = 1,
                AIType = MonsterAIType.Passive,
                WalkSpeed = 3
            });

            
            AddDefinition(new MonsterDefinition(2, "鹿")
            {
                Level = 5,
                HP = 80,
                MinDC = 3,
                MaxDC = 6,
                AC = 2,
                ExpReward = 20,
                GoldReward = 5,
                AIType = MonsterAIType.Passive
            });

            
            AddDefinition(new MonsterDefinition(3, "森林雪人")
            {
                Level = 10,
                HP = 200,
                MinDC = 8,
                MaxDC = 15,
                AC = 5,
                MAC = 3,
                ExpReward = 50,
                GoldReward = 15,
                AIType = MonsterAIType.Active
            });

            
            AddDefinition(new MonsterDefinition(4, "骷髅")
            {
                Level = 15,
                HP = 350,
                MinDC = 12,
                MaxDC = 20,
                AC = 8,
                MAC = 5,
                ExpReward = 100,
                GoldReward = 25,
                AIType = MonsterAIType.Active
            });

            
            AddDefinition(new MonsterDefinition(5, "骷髅战士")
            {
                Level = 20,
                HP = 600,
                MinDC = 18,
                MaxDC = 30,
                AC = 12,
                MAC = 8,
                ExpReward = 200,
                GoldReward = 50,
                AIType = MonsterAIType.Active
            });

            LogManager.Default.Info($"已加载 {_definitions.Count} 个怪物定义");
        }

        
        
        
        public void AddDefinition(MonsterDefinition definition)
        {
            lock (_lock)
            {
                _definitions[definition.MonsterId] = definition;
            }
        }

        
        
        
        public MonsterDefinition? GetDefinition(int monsterId)
        {
            lock (_lock)
            {
                return _definitions.TryGetValue(monsterId, out var def) ? def : null;
            }
        }

        
        
        
        public Monster? CreateMonster(int monsterId)
        {
            var definition = GetDefinition(monsterId);
            if (definition == null)
                return null;

            return new Monster(monsterId, definition.Name);
        }

        
        
        
        public Monster? SpawnMonster(int monsterId, LogicMap map, ushort x, ushort y)
        {
            var monster = CreateMonster(monsterId);
            if (monster == null)
                return null;

            monster.SetHomePosition(x, y);
            
            if (map.AddObject(monster, x, y))
            {
                return monster;
            }

            return null;
        }
    }
}
