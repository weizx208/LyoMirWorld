using System;
using System.Collections.Generic;
using System.Drawing;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class MonsterEx : AliveObject
    {
        
        private MonsterClass? _desc;

        public uint ExpValue => _desc?.Prop.Exp ?? 0;

        
        private const byte ATF_HUMAN = 1;
        private const byte ATF_MONSTER = 2;
        private const byte ATF_ANIMAL = 4;
        private const byte ATF_CRIMER = 8;
        private const byte ATF_ATTACKSANDCITY = 16;
        private const byte ATF_PETS = 32;
        
        
        private MonsterGen? _gen;
        
        
        private DateTime _lastAITime;
        private DateTime _lastAttackTime;
        private DateTime _delTimer;
        
        
        private byte _type;
        
        
        private bool _cuted;
        
        
        private bool _gotoPoint;
        private ushort _gotoX;
        private ushort _gotoY;
        
        
        private uint _updateKey;

        public MonsterEx()
        {
            _lastAITime = DateTime.Now;
            _lastAttackTime = DateTime.Now;
            _delTimer = DateTime.Now;
            _type = 0;
            _cuted = false;
            _gotoPoint = false;
            _gotoX = 0;
            _gotoY = 0;
            _updateKey = 0;
        }

        private static int TileDistance(int x1, int y1, int x2, int y2)
        {
            
            return Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));
        }

        public override uint GetFeather()
        {
            
            
            
            if (_desc == null)
                return 0;

            if (_desc.Base.Image == 0)
                return _desc.Base.Feature & 0xffffff00;

            return (uint)((_desc.Base.Image << 16) | _type);
        }

        public override byte GetSex()
        {
            if (_desc == null)
                return 0;

            if (_desc.Base.Image != 0)
                return 0;

            return (byte)(_desc.Base.Feature & 1);
        }

        public override byte GetNameColor(MapObject? viewer = null)
        {
            return _desc?.Base.NameColor ?? (byte)255;
        }

        
        
        
        
        public bool Init(MonsterClass desc, int mapId, int x, int y, MonsterGen? gen = null)
        {
            if (desc == null)
                return false;

            _desc = desc;
            _gen = gen;
            
            
            Name = desc.Base.ViewName;
            Level = desc.Base.Level;
            
            
            MaxHP = desc.Prop.HP;
            CurrentHP = MaxHP;
            MaxMP = desc.Prop.MP;
            CurrentMP = MaxMP;
            
            
            Stats.MinDC = desc.Prop.DC1;
            Stats.MaxDC = desc.Prop.DC2;
            Stats.MinAC = desc.Prop.AC1;
            Stats.MaxAC = desc.Prop.AC2;
            Stats.MinMAC = desc.Prop.MAC1;
            Stats.MaxMAC = desc.Prop.MAC2;
            Stats.MinMC = desc.Prop.MC1;
            Stats.MaxMC = desc.Prop.MC2;
            Stats.Accuracy = desc.Prop.Hit;
            
            
            var map = LogicMapMgr.Instance.GetLogicMapById((uint)mapId);
            if (map == null)
                return false;

            
            if (!map.AddObject(this, x, y))
                return false;

            
            _type = 0x13;
            
            
            if (!string.IsNullOrEmpty(desc.BornScript))
            {
                ExecuteScript(desc.BornScript);
            }
            
            
            return true;
        }

        
        
        
        
        public new void Clean()
        {
            
            if (CurrentMap != null)
            {
                CurrentMap.RemoveObject(this);
            }
            
            
            ClearGen();
            
            
            _desc = null;
            
            Console.WriteLine($"怪物 {Name} 被清理");
        }

        
        
        
        
        public void ClearGen()
        {
            if (_gen != null)
            {
                _gen.CurrentCount--;
                _gen = null;
            }
        }

        
        
        
        
        public void SetDelTimer()
        {
            _delTimer = DateTime.Now;
        }

        
        
        
        
        public bool IsDelTimerTimeOut(int timeout)
        {
            return (DateTime.Now - _delTimer).TotalMilliseconds >= timeout;
        }

        
        
        
        
        public uint GetUpdateKey()
        {
            return _updateKey;
        }

        
        
        
        public void SetId(uint id)
        {
            ObjectId = id;
        }

        
        
        
        public LogicMap? GetMap()
        {
            return CurrentMap;
        }

        
        
        
        public bool IsDeath()
        {
            return IsDead;
        }

        
        
        
        
        public override void Update()
        {
            base.Update();
            
            if (IsDead)
                return;
            
            
            if ((DateTime.Now - _lastAITime).TotalMilliseconds >= _desc?.Prop.AIDelay)
            {
                UpdateAI();
                _lastAITime = DateTime.Now;
            }
            
            
            UpdateRecover();
            
            
            _updateKey = (uint)Environment.TickCount;
        }

        
        
        
        private void UpdateAI()
        {
            if (_desc == null || CurrentMap == null)
                return;

            
            AcquireTarget();
             
            
            switch (_desc.AISet.MoveStyle)
            {
                case 0: 
                    AiStatic();
                    break;
                case 1: 
                    AiFollow();
                    break;
                case 2: 
                    AiEscape();
                    break;
                case 3: 
                    AiKeepDistance();
                    break;
                case 4: 
                    AiKeepLine();
                    break;
                case 5: 
                default:
                    AiStupidMove();
                    break;
            }
            
            
            if ((DateTime.Now - _lastAttackTime).TotalMilliseconds >= _desc.AttackDesc.Delay)
            {
                CheckAttack();
                _lastAttackTime = DateTime.Now;
            }
        }

        private void AcquireTarget()
        {
            if (_desc == null || CurrentMap == null)
                return;

            var target = GetTarget();
            if (target != null)
            {
                if (target.IsDead || target.CurrentMap != CurrentMap)
                {
                    SetTarget(null);
                    target = null;
                }
            }

            if (target != null)
                return;

            
            if ((_desc.SProp.PFlag & 1u) == 0)
                return;

            int view = _desc.AISet.ViewDistance;
            if (view <= 0) view = 10;

            HumanPlayer? best = null;
            int bestDist = int.MaxValue;

            foreach (var player in CurrentMap.GetPlayersInRange(X, Y, view))
            {
                if (player == null || player.IsDead)
                    continue;

                if (!IsTargetSelectable(player))
                    continue;

                int dist = TileDistance(X, Y, player.X, player.Y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = player;
                }
            }

            if (best != null)
            {
                SetTarget(best);

                
                if (!string.IsNullOrEmpty(_desc.GotTargetScript))
                {
                    ExecuteScript(_desc.GotTargetScript);
                }
            }
        }

        
        
        
        private bool IsTargetSelectable(AliveObject target)
        {
            if (_desc == null || target == null)
                return false;

            byte targetFlag = _desc.AISet.TargetFlag;
            if (targetFlag == 0)
                return false;

            if (target is HumanPlayer hp)
            {
                
                uint redPoint = (uint)Math.Max(0, GameWorld.Instance.GetGameVar(GameVarConstants.RedPkPoint));

                
                uint pk = 0;
                try { pk = hp.PKSystem?.GetPkValue() ?? 0; } catch { pk = 0; }
                if (pk == 0 && hp.PkValue != 0) pk = hp.PkValue;

                if (pk >= redPoint)
                    return (targetFlag & ATF_CRIMER) != 0;

                return (targetFlag & ATF_HUMAN) != 0;
            }

            
            return false;
        }

        private void AiStatic()
        {
            
        }

        private void AiFollow()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
            {
                AiStupidMove();
                return;
            }

            ChaseTarget();
        }

        private void AiEscape()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
            {
                AiStupidMove();
                return;
            }

            Escape();
        }

        private void AiKeepDistance()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
            {
                AiStupidMove();
                return;
            }

            int dist = TileDistance(X, Y, target.X, target.Y);
            int atkDist = _desc.AttackDesc.AttackDistance;
            int escDist = _desc.AISet.EscapeDistance;

            if (dist > atkDist)
            {
                ChaseTarget();
                return;
            }

            if (escDist > 0 && dist <= escDist)
            {
                Escape();
            }
        }

        private void AiKeepLine()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
            {
                AiStupidMove();
                return;
            }

            int dist = TileDistance(X, Y, target.X, target.Y);
            int atkDist = _desc.AttackDesc.AttackDistance;
            if (dist > atkDist)
            {
                ChaseTarget();
                return;
            }

            int dx = X - target.X;
            int dy = Y - target.Y;
            bool onLine = dx == 0 || dy == 0 || Math.Abs(dx) == Math.Abs(dy);
            if (onLine)
                return;

            
            if (CurrentAction == ActionType.Stand)
            {
                var dir = GetDirection(X, Y, target.X, target.Y);
                Walk(dir);
            }
        }

        private void AiStupidMove()
        {
            
            if (CurrentMap == null)
                return;

            if (CurrentAction != ActionType.Stand)
                return;

            if (Random.Shared.Next(150) != 0)
                return;

            var start = Random.Shared.Next(8);
            for (int i = 0; i < 8; i++)
            {
                var dir = (Direction)((i + start) % 8);
                if (Walk(dir))
                    break;
            }
        }

        
        
        
        private void RandomMove()
        {
            if (CurrentAction != ActionType.Stand)
                return;
                
            var dir = (Direction)Random.Shared.Next(8);
            Walk(dir);
        }

        
        
        
        private void ChaseTarget()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
                return;
                
            int distance = TileDistance(X, Y, target.X, target.Y);
            
            if (distance <= _desc.AttackDesc.AttackDistance)
            {
                
                return;
            }
            
            
            if (CurrentAction == ActionType.Stand)
            {
                var dir = GetDirection(X, Y, target.X, target.Y);
                Walk(dir);
            }
        }

        
        
        
        private void Escape()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
                return;
                
            int distance = TileDistance(X, Y, target.X, target.Y);
            
            if (distance > _desc.AISet.EscapeDistance)
            {
                
                return;
            }
            
            
            if (CurrentAction == ActionType.Stand)
            {
                var dir = GetDirection(target.X, target.Y, X, Y);
                Run(dir);
            }
        }

        
        
        
        private void CheckAttack()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
                return;
                
            int distance = TileDistance(X, Y, target.X, target.Y);
            
            if (distance <= _desc.AttackDesc.AttackDistance)
            {
                
                AttackTarget(target);
            }
        }

        
        
        
        private void AttackTarget(AliveObject target)
        {
            if (target == null || target.IsDead)
                return;

            
            if (CurrentMap != null)
            {
                var dir = GetDirection(X, Y, target.X, target.Y);
                CurrentDirection = dir;

                byte[] attackPayload = new byte[8];
                Buffer.BlockCopy(BitConverter.GetBytes(GetFeather()), 0, attackPayload, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(GetStatus()), 0, attackPayload, 4, 4);

                var attackMsg = new MirCommon.MirMsgOrign
                {
                    dwFlag = ObjectId,
                    wCmd = MirCommon.ProtocolCmd.SM_ATTACK,
                    wParam = new ushort[3] { X, Y, (ushort)((int)dir & 0xFF) },
                };

                byte[] encodedAttack = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(attackMsg, attackPayload);
                if (encodedAttack.Length > 0)
                {
                    CurrentMap.SendToNearbyPlayers(X, Y, 18, encodedAttack);
                }
            }
                
            
            int damage = Random.Shared.Next(Stats.MinDC, Stats.MaxDC + 1);
            
            
            target.BeAttack(this, damage, DamageType.Physics);
             
            
            if (target.IsDead && !string.IsNullOrEmpty(_desc.KillTargetScript))
            {
                ExecuteScript(_desc.KillTargetScript);
            }
        }

        
        
        
        private void UpdateRecover()
        {
            if (_desc == null)
                return;
                
            
            if (CurrentHP < MaxHP && _desc.Prop.RecoverHP > 0)
            {
                CurrentHP = Math.Min(MaxHP, CurrentHP + _desc.Prop.RecoverHP);
            }
            
            
            if (CurrentMP < MaxMP && _desc.Prop.RecoverMP > 0)
            {
                CurrentMP = Math.Min(MaxMP, CurrentMP + _desc.Prop.RecoverMP);
            }
        }

        
        
        
        private void ExecuteScript(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName))
                return;
            
            
            var scriptObject = ScriptObjectMgr.Instance.GetScriptObject(scriptName);
            if (scriptObject == null)
            {
                Console.WriteLine($"怪物 {Name} 执行脚本失败: 脚本 {scriptName} 不存在");
                return;
            }
            
            
            Console.WriteLine($"怪物 {Name} 执行脚本: {scriptName}");
            
            
            ExecuteScriptContent(scriptObject);
        }
        
        
        
        
        private void ExecuteScriptContent(ScriptObject scriptObject)
        {
            if (scriptObject == null)
                return;
                
            foreach (var line in scriptObject.Lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("//"))
                    continue;
                    
                
                ParseAndExecuteCommand(trimmedLine);
            }
        }
        
        
        
        
        private void ParseAndExecuteCommand(string command)
        {
            
            var parts = command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;
                
            var cmd = parts[0].ToLower();
            
            switch (cmd)
            {
                case "say":
                    if (parts.Length > 1)
                    {
                        string message = string.Join(" ", parts, 1, parts.Length - 1);
                        Say(message);
                    }
                    break;
                    
                case "move":
                    if (parts.Length >= 3 && ushort.TryParse(parts[1], out ushort x) && ushort.TryParse(parts[2], out ushort y))
                    {
                        MoveTo(x, y);
                    }
                    break;
                    
                case "attack":
                    if (parts.Length > 1)
                    {
                        string targetName = parts[1];
                        AttackTargetByName(targetName);
                    }
                    break;
                    
                case "spawn":
                    if (parts.Length >= 4 && int.TryParse(parts[1], out int monsterId) && 
                        ushort.TryParse(parts[2], out ushort spawnX) && ushort.TryParse(parts[3], out ushort spawnY))
                    {
                        SpawnMonster(monsterId, spawnX, spawnY);
                    }
                    break;
                    
                case "teleport":
                    if (parts.Length >= 3 && ushort.TryParse(parts[1], out ushort teleX) && ushort.TryParse(parts[2], out ushort teleY))
                    {
                        Teleport(teleX, teleY);
                    }
                    break;
                    
                case "setvar":
                    if (parts.Length >= 3)
                    {
                        string varName = parts[1];
                        string varValue = parts[2];
                        SetVariable(varName, varValue);
                    }
                    break;
                    
                case "call":
                    if (parts.Length > 1)
                    {
                        string subScriptName = parts[1];
                        ExecuteScript(subScriptName);
                    }
                    break;
                    
                default:
                    Console.WriteLine($"怪物 {Name} 未知脚本命令: {command}");
                    break;
            }
        }
        
        
        
        
        private void MoveTo(ushort x, ushort y)
        {
            if (CurrentMap == null)
                return;
                
            
            _gotoPoint = true;
            _gotoX = x;
            _gotoY = y;
            
            Console.WriteLine($"怪物 {Name} 移动到 ({x},{y})");
        }
        
        
        
        
        private void AttackTargetByName(string targetName)
        {
            if (CurrentMap == null)
                return;
                
            var players = CurrentMap.GetPlayersInRange(X, Y, 10); 
            var target = players.FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            
            if (target != null)
            {
                SetTarget(target);
                Console.WriteLine($"怪物 {Name} 攻击目标: {targetName}");
            }
        }
        
        
        
        
        private void SpawnMonster(int monsterId, ushort x, ushort y)
        {
            if (CurrentMap == null)
                return;
                
            var monster = MonsterManagerEx.Instance.CreateMonster(monsterId);
            if (monster != null)
            {
                monster.SetId(MonsterManagerEx.Instance.GetNextObjectId());
                monster.Init(MonsterManagerEx.Instance.GetMonsterClass(monsterId), (int)CurrentMap.MapId, x, y);
                Console.WriteLine($"怪物 {Name} 刷出怪物 {monsterId} 在 ({x},{y})");
            }
        }
        
        
        
        
        private void Teleport(ushort x, ushort y)
        {
            if (CurrentMap == null)
                return;
                
            CurrentMap.MoveObject(this, x, y);
            Console.WriteLine($"怪物 {Name} 传送到 ({x},{y})");
        }
        
        
        
        
        private void SetVariable(string varName, string varValue)
        {
            
            Console.WriteLine($"怪物 {Name} 设置变量 {varName} = {varValue}");
        }

        
        
        
        public MonsterClass? GetDesc()
        {
            return _desc;
        }

        
        
        
        public MonsterGen? GetGen()
        {
            return _gen;
        }

        
        
        
        public void SetGen(MonsterGen? gen)
        {
            _gen = gen;
        }

        
        
        
        public byte GetSType()
        {
            return _type;
        }

        
        
        
        public void SetSType(byte type)
        {
            _type = type;
        }

        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.Monster;
        }

        
        
        
        protected override void OnDamaged(AliveObject attacker, int damage, DamageType damageType)
        {
            base.OnDamaged(attacker, damage, damageType);

            
            if (attacker != null && attacker != this && !attacker.IsDead && attacker.CurrentMap == CurrentMap)
            {
                SetTarget(attacker);
                SetHitter(attacker);
            }
            
            
            if (_desc != null && !string.IsNullOrEmpty(_desc.HurtScript))
            {
                ExecuteScript(_desc.HurtScript);
            }
        }

        
        
        
        protected override void OnDeath(AliveObject killer)
        {
            base.OnDeath(killer);
            
            
            if (_desc != null && !string.IsNullOrEmpty(_desc.DeathScript))
            {
                ExecuteScript(_desc.DeathScript);
            }

            
            try
            {
                var map = CurrentMap;
                if (map != null && _desc != null)
                {
                    
                    var monItems = MonItemsMgr.Instance.GetMonItems(_desc.Base.ViewName)
                                  ?? MonItemsMgr.Instance.GetMonItems(_desc.Base.ClassName)
                                  ?? MonItemsMgr.Instance.GetMonItems(Name);

                    if (monItems?.Items != null)
                    {
                        Point[] pts = new Point[64];
                        int dropCount = map.GetDropItemPoint(X, Y, pts, pts.Length);

                        if (dropCount > 0)
                        {
                            uint ownerId = 0;
                            if (killer is HumanPlayer hp)
                                ownerId = hp.GetDBId();

                            int dropIndex = 0;
                            var downItem = monItems.Items;
                            while (downItem != null)
                            {
                                if (MonItemsMgr.Instance.UpdateDownItemCycle(downItem) &&
                                    MonItemsMgr.Instance.CreateDownItem(downItem, out var dropItem))
                                {
                                    var p = pts[dropIndex];
                                    DownItemMgr.Instance.DropItem(map, dropItem, (ushort)p.X, (ushort)p.Y, ownerId);

                                    dropIndex++;
                                    if (dropIndex >= dropCount)
                                        dropIndex = 0;
                                }

                                downItem = downItem.Next;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"怪物死亡掉落处理异常: {Name}", exception: ex);
            }

            
            try
            {
                MonsterManagerEx.Instance.DeleteMonsterDelayed(this);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"怪物死亡入删除队列失败: {Name}", exception: ex);
            }
            
            Console.WriteLine($"怪物 {Name} 被 {killer?.Name ?? "未知"} 击杀");
        }

        
        
        
        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            
            
            return base.GetViewMsg(out msg, viewer);
        }
    }
}
