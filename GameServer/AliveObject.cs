using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    public abstract class AliveObject : MapObject, ICombatEntity
    {
        public string Name { get; set; } = "NONAME";
        public byte Level { get; set; } = 1;
        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }
        public int CurrentMP { get; set; }
        public int MaxMP { get; set; }

        public Direction CurrentDirection { get; set; }
        public ActionType CurrentAction { get; set; }
        public ushort ActionX { get; set; }
        public ushort ActionY { get; set; }
        public Direction ActionDirection { get; set; }
        public byte WalkSpeed { get; set; } = 2;
        public byte RunSpeed { get; set; } = 4;
        
        public bool IsDead { get; set; }
        public bool IsHidden { get; set; }
        public bool CanMove { get; set; } = true;
        
        public CombatStats Stats { get; set; }
        public BuffManager BuffManager { get; private set; }
        
        uint ICombatEntity.Id => ObjectId;
        string ICombatEntity.Name => Name;
        int ICombatEntity.CurrentHP { get => CurrentHP; set => CurrentHP = value; }
        int ICombatEntity.MaxHP { get => MaxHP; set => MaxHP = value; }
        int ICombatEntity.CurrentMP { get => CurrentMP; set => CurrentMP = value; }
        int ICombatEntity.MaxMP { get => MaxMP; set => MaxMP = value; }
        byte ICombatEntity.Level { get => Level; set => Level = value; }
        CombatStats ICombatEntity.Stats { get => Stats; set => Stats = value; }
        bool ICombatEntity.IsDead { get => IsDead; set => IsDead = value; }
        BuffManager ICombatEntity.BuffManager => BuffManager;

        int ICombatEntity.X { get => X; set => X = (ushort)Math.Clamp(value, 0, 65535); }
        int ICombatEntity.Y { get => Y; set => Y = (ushort)Math.Clamp(value, 0, 65535); }

        private readonly ConcurrentDictionary<uint, VisibleObject> _visibleObjects = new();
        private uint _visibleObjectUpdateFlag = 0;
        
        protected int _visibleObjectFlag = 0;
        
        protected ObjectReference<AliveObject> _targetRef = new();
        protected ObjectReference<AliveObject> _hitterRef = new();
        protected ObjectReference<AliveObject> _ownerRef = new();
        
        protected Dictionary<uint, AttackRecord> _attackRecords = new();
        protected readonly object _recordLock = new();
        
        private readonly Queue<ObjectProcess> _processQueue = new();
        private readonly object _processLock = new();
        
        private DateTime _actionCompleteTime;
        private DateTime _lastHpRecoverTime;
        private DateTime _lastMpRecoverTime;

        protected AliveObject()
        {
            Stats = new CombatStats();
            BuffManager = new BuffManager(this as ICombatEntity);
            CurrentDirection = Direction.Down;
            CurrentAction = ActionType.Stand;
            MaxHP = 100;
            CurrentHP = 100;
            MaxMP = 100;
            CurrentMP = 100;
            _lastHpRecoverTime = DateTime.Now;
            _lastMpRecoverTime = DateTime.Now;
        }

        public override void Update()
        {
            base.Update();
            
            BuffManager.Update();
            
            ProcessQueue();
            
            AutoRecover();
            
            CleanupOldRecords(TimeSpan.FromMinutes(5));
        }

        #region 移动相关

        public virtual bool Walk(Direction dir, uint delay = 0)
        {
            if (!CanDoAction(ActionType.Walk))
                return false;

            var (newX, newY) = GetNextPosition(X, Y, dir);
            return WalkXY((ushort)newX, (ushort)newY, delay);
        }

        public virtual bool WalkXY(ushort x, ushort y, uint delay = 0)
        {
            if (CurrentMap == null || !CurrentMap.CanWalk(x, y))
                return false;

            var dir = GetDirection(X, Y, x, y);
            if (!SetAction(ActionType.Walk, dir, x, y, delay))
                return false;

            return true;
        }

        public virtual bool Run(Direction dir, uint delay = 0)
        {
            if (!CanDoAction(ActionType.Run))
                return false;

            int x = X, y = Y;
            int steps = Math.Max(1, (int)GetRunSpeed());
            for (int i = 0; i < steps; i++)
            {
                var (nextX, nextY) = GetNextPosition(x, y, dir);
                if (CurrentMap == null || CurrentMap.IsBlocked((ushort)nextX, (ushort)nextY))
                    return false;
                x = nextX;
                y = nextY;
            }
            
            return RunXY((ushort)x, (ushort)y, delay);
        }

        public virtual bool RunXY(ushort x, ushort y, uint delay = 0)
        {
            if (CurrentMap == null || !CurrentMap.CanWalk(x, y))
                return false;

            var dir = GetDirection(X, Y, x, y);

            int mx = X, my = Y;
            int steps = Math.Max(1, (int)GetRunSpeed());
            for (int i = 0; i < steps; i++)
            {
                var (nx, ny) = GetNextPosition(mx, my, dir);
                if (CurrentMap.IsBlocked((ushort)nx, (ushort)ny))
                    return false;
                mx = nx;
                my = ny;
            }
            if (mx != x || my != y)
                return false;

            if (!SetAction(ActionType.Run, dir, x, y, delay))
                return false;

            return true;
        }

        public virtual bool Turn(Direction dir)
        {
            CurrentDirection = dir;
            ActionDirection = dir;
            return true;
        }

        public virtual bool Attack(Direction dir, uint delay = 0)
        {
            if (!CanDoAction(ActionType.Attack))
                return false;

            return SetAction(ActionType.Attack, dir, (ushort)X, (ushort)Y, delay);
        }

        protected virtual bool SetAction(ActionType action, Direction dir, ushort x, ushort y, uint delay)
        {
            if (!CanDoAction(action))
                return false;

            CurrentAction = action;
            ActionDirection = dir;
            CurrentDirection = dir;
            ActionX = x;
            ActionY = y;
            
            if ((action == ActionType.Walk || action == ActionType.Run) && CurrentMap != null)
            {
                if (X != x || Y != y)
                {
                    CurrentMap.MoveObject(this, x, y);
                }
            }

            uint actionTime = GetActionTime(action);
            _actionCompleteTime = DateTime.Now.AddMilliseconds(actionTime + delay);

            OnDoAction(action);
            return true;
        }

        public virtual bool CompleteAction()
        {
            if (DateTime.Now < _actionCompleteTime)
                return false;

            CurrentAction = ActionType.Stand;
            return true;
        }

        public virtual bool CanDoAction(ActionType action)
        {
            if (IsDead)
                return false;

            if (DateTime.Now < _actionCompleteTime)
                return false;

            if (action == ActionType.Walk || action == ActionType.Run)
            {
                if (!CanMove)
                    return false;
            }

            return true;
        }

        protected virtual uint GetActionTime(ActionType action)
        {
            static uint GetGameVarMs(int key, uint fallback)
            {
                float value = GameWorld.Instance.GetGameVar(key);
                if (value <= 0)
                    return fallback;
                if (value >= uint.MaxValue)
                    return uint.MaxValue;
                return (uint)value;
            }

            switch (action)
            {
                case ActionType.Walk:
                    return GetGameVarMs(GameVarConstants.WalkSpeed, 600);
                case ActionType.Run:
                    return GetGameVarMs(GameVarConstants.RunSpeed, 300);
                case ActionType.Attack:
                    return GetGameVarMs(GameVarConstants.AttackSpeed, 500);
                default:
                    return 0;
            }
        }

        public virtual byte GetRunSpeed()
        {
            return RunSpeed;
        }

        public virtual byte GetWalkSpeed()
        {
            return WalkSpeed;
        }

        #endregion

        #region 战斗相关

        public virtual bool BeAttack(AliveObject attacker, int damage, DamageType damageType = DamageType.Physics)
        {
            if (IsDead)
                return false;

            return TakeDamage(attacker as ICombatEntity, damage, damageType);
        }

        public virtual bool TakeDamage(ICombatEntity attacker, int damage, DamageType damageType)
        {
            if (IsDead)
                return false;

            CurrentHP -= damage;
            
            if (CurrentHP <= 0)
            {
                CurrentHP = 0;
                IsDead = true;
                OnDeath(attacker as AliveObject);
                return true;
            }

            OnDamaged(attacker as AliveObject, damage, damageType);
            return false;
        }

        public virtual bool TakeDamage(AliveObject attacker, int damage, DamageType damageType)
        {
            return TakeDamage(attacker as ICombatEntity, damage, damageType);
        }

        public virtual bool Damage(uint hitterId, int value)
        {
            return TakeDamage((ICombatEntity)null!, value, DamageType.Physics);
        }
        
        public virtual CombatResult Attack(ICombatEntity target, DamageType damageType = DamageType.Physics)
        {
            var result = new CombatResult
            {
                DamageType = damageType,
                Hit = true,
                Damage = 0,
                Critical = false
            };

            int baseDamage = damageType == DamageType.Magic ? Stats.MinMC : Stats.MinDC;
            result.Damage = Math.Max(1, baseDamage);
            result.TargetDied = target.TakeDamage(this as ICombatEntity, result.Damage, damageType);

            return result;
        }

        public virtual void Heal(int amount)
        {
            if (IsDead)
                return;
            
            CurrentHP = Math.Min(CurrentHP + amount, MaxHP);
            SendHpMpChanged();
        }

        public virtual void RestoreMP(int amount)
        {
            if (IsDead)
                return;
            
            CurrentMP = Math.Min(CurrentMP + amount, MaxMP);
            SendHpMpChanged();
        }

        public virtual bool ConsumeMP(int amount)
        {
            if (CurrentMP < amount)
                return false;
            
            CurrentMP -= amount;
            SendHpMpChanged();
            return true;
        }

        public virtual void ToDeath(uint killerId = 0)
        {
            if (IsDead)
                return;

            IsDead = true;
            CurrentHP = 0;
            CurrentAction = ActionType.Die;
            
            OnDeath(null!);
        }

        #endregion

        protected override void OnPositionChanged(ushort oldX, ushort oldY, ushort newX, ushort newY)
        {
            base.OnPositionChanged(oldX, oldY, newX, newY);

            UpdateViewRange(oldX, oldY);
        }

        #region 视野管理

        public virtual void UpdateViewRange(int oldX, int oldY)
        {
            if (CurrentMap == null)
                return;

            int viewRange = 18;
            
            var newObjects = CurrentMap.GetObjectsInRange(X, Y, viewRange);
            
            var oldObjects = CurrentMap.GetObjectsInRange(oldX, oldY, viewRange);

            foreach (var obj in newObjects)
            {
                if (obj.ObjectId == ObjectId)
                    continue;

                if (!_visibleObjects.ContainsKey(obj.ObjectId))
                {
                    AddVisibleObject(obj);
                }
            }

            foreach (var obj in oldObjects)
            {
                if (!newObjects.Contains(obj))
                {
                    RemoveVisibleObject(obj);
                }
            }
        }

        public virtual void AddVisibleObjectType(ObjectType type)
        {
            _visibleObjectFlag |= (1 << (int)type);
        }
        
        public virtual int GetVisibleObjectFlag()
        {
            return _visibleObjectFlag;
        }
        
        public virtual void AddVisibleObject(MapObject obj)
        {
            if ((_visibleObjectFlag & (1 << (int)obj.GetObjectType())) == 0)
            {
                return; 
            }
            
            var visObj = new VisibleObject
            {
                Object = obj,
                UpdateFlag = _visibleObjectUpdateFlag++
            };

            if (_visibleObjects.TryAdd(obj.ObjectId, visObj))
            {
                obj.AddRef();
                OnObjectEnterView(obj);
            }
        }

        public virtual void RemoveVisibleObject(MapObject obj)
        {
            if (_visibleObjects.TryRemove(obj.ObjectId, out var visObj))
            {
                OnObjectLeaveView(obj);
                obj.DecRef();
            }
        }

        public virtual void SearchViewRange()
        {
            if (CurrentMap == null)
                return;

            var objects = CurrentMap.GetObjectsInRange(X, Y, 18);
            foreach (var obj in objects)
            {
                if (obj.ObjectId != ObjectId && !_visibleObjects.ContainsKey(obj.ObjectId))
                {
                    AddVisibleObject(obj);
                }
            }
        }

        public virtual void CleanVisibleList()
        {
            foreach (var kvp in _visibleObjects.ToArray())
            {
                if (kvp.Value.Object != null)
                {
                    kvp.Value.Object.DecRef();
                }
                _visibleObjects.TryRemove(kvp.Key, out _);
            }
        }

        #endregion

        #region 消息发送

        public virtual void SendMessage(byte[] message)
        {
        }

        public virtual void SendAroundMsg(int v, int v1, byte[] message)
        {
            if (CurrentMap == null)
                return;

            CurrentMap.BroadcastMessageInRange(X, Y, 18, message);
        }

        public virtual void SendMapMsg(byte[] message)
        {
            CurrentMap?.BroadcastMessage(message);
        }

        public virtual void Say(string message)
        {
            LogManager.Default.Info($"[{Name}]: {message}");
        }

        protected virtual void SendHpMpChanged()
        {
        }

        #endregion

        #region 进程处理

        public virtual bool AddProcess(ProcessType type, uint param1 = 0, uint param2 = 0, 
            uint param3 = 0, uint param4 = 0, uint delay = 0, int repeatTimes = 0, string? stringParam = null)
        {
            var process = new ObjectProcess(type)
            {
                Param1 = param1,
                Param2 = param2,
                Param3 = param3,
                Param4 = param4,
                Delay = delay,
                RepeatTimes = repeatTimes,
                StringParam = stringParam
            };

            return AddProcess(process);
        }

        public virtual bool AddProcess(ObjectProcess process)
        {
            lock (_processLock)
            {
                _processQueue.Enqueue(process);
                return true;
            }
        }

        protected virtual void ProcessQueue()
        {
            lock (_processLock)
            {
                while (_processQueue.Count > 0)
                {
                    var process = _processQueue.Peek();
                    
                    if (!process.ShouldExecute())
                        break;

                    _processQueue.Dequeue();
                    DoProcess(process);

                    if (process.RepeatTimes > 0)
                    {
                        process.RepeatTimes--;
                        process.ExecuteTime = DateTime.Now;
                        _processQueue.Enqueue(process);
                    }
                }
            }
        }

        protected virtual void DoProcess(ObjectProcess process)
        {
            switch (process.Type)
            {
                case ProcessType.TakeDamage:
                    TakeDamage(null!, (int)process.Param1, (DamageType)process.Param2);
                    break;
                case ProcessType.Heal:
                    Heal((int)process.Param1);
                    break;
                case ProcessType.Die:
                    ToDeath(process.Param1);
                    break;
                case ProcessType.ChangeMap:
                    if (this is HumanPlayer player)
                    {
                        ushort x = (ushort)Math.Clamp((int)process.Param2, 0, ushort.MaxValue);
                        ushort y = (ushort)Math.Clamp((int)process.Param3, 0, ushort.MaxValue);
                        player.ChangeMap(process.Param1, x, y);
                    }
                    break;
            }
        }

        #endregion

        #region 辅助方法

        protected virtual void AutoRecover()
        {
            if (IsDead || CurrentAction != ActionType.Stand)
                return;

            var now = DateTime.Now;

            if ((now - _lastHpRecoverTime).TotalMilliseconds >= 5000) 
            {
                int recoverHp = GetAutoRecoverHp();
                if (recoverHp > 0 && CurrentHP < MaxHP)
                {
                    Heal(recoverHp);
                }
                _lastHpRecoverTime = now;
            }

            if ((now - _lastMpRecoverTime).TotalMilliseconds >= 5000)
            {
                int recoverMp = GetAutoRecoverMp();
                if (recoverMp > 0 && CurrentMP < MaxMP)
                {
                    RestoreMP(recoverMp);
                }
                _lastMpRecoverTime = now;
            }
        }

        protected virtual int GetAutoRecoverHp()
        {
            return MaxHP / 50; 
        }

        protected virtual int GetAutoRecoverMp()
        {
            return MaxMP / 50; 
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

        protected (int x, int y) GetNextPosition(int x, int y, Direction dir)
        {
            switch (dir)
            {
                case Direction.Up: return (x, y - 1);
                case Direction.UpRight: return (x + 1, y - 1);
                case Direction.Right: return (x + 1, y);
                case Direction.DownRight: return (x + 1, y + 1);
                case Direction.Down: return (x, y + 1);
                case Direction.DownLeft: return (x - 1, y + 1);
                case Direction.Left: return (x - 1, y);
                case Direction.UpLeft: return (x - 1, y - 1);
                default: return (x, y);
            }
        }

        protected Direction GetDirection(int x1, int y1, int x2, int y2)
        {
            int dx = x2 - x1;
            int dy = y2 - y1;

            if (dx == 0 && dy < 0) return Direction.Up;
            if (dx > 0 && dy < 0) return Direction.UpRight;
            if (dx > 0 && dy == 0) return Direction.Right;
            if (dx > 0 && dy > 0) return Direction.DownRight;
            if (dx == 0 && dy > 0) return Direction.Down;
            if (dx < 0 && dy > 0) return Direction.DownLeft;
            if (dx < 0 && dy == 0) return Direction.Left;
            if (dx < 0 && dy < 0) return Direction.UpLeft;
            
            return Direction.Down;
        }

        #endregion

        #region 对象引用

        public virtual void SetTarget(AliveObject? target)
        {
            var old = _targetRef.GetObject();
            _targetRef.SetObject(target);
            OnChangeTarget(old, target);
        }

        public virtual AliveObject? GetTarget() => _targetRef.GetObject();

        public virtual void SetHitter(AliveObject? hitter)
        {
            var old = _hitterRef.GetObject();
            _hitterRef.SetObject(hitter);
            OnChangeHitter(old, hitter);
        }

        public virtual AliveObject? GetHitter() => _hitterRef.GetObject();

        public virtual void SetOwner(AliveObject? owner)
        {
            var old = _ownerRef.GetObject();
            _ownerRef.SetObject(owner);
            OnChangeOwner(old, owner);
        }

        public virtual AliveObject? GetOwner() => _ownerRef.GetObject();

        #endregion

        #region 事件回调

        protected virtual void OnDeath(AliveObject killer)
        {
            try
            {
                if (CurrentMap == null)
                    return;

                uint[] dwView = new uint[2]
                {
                    GetFeather(),
                    GetStatus(),
                };

                byte[] payload = new byte[dwView.Length * 4];
                Buffer.BlockCopy(dwView, 0, payload, 0, payload.Length);

                ushort dir = (ushort)((int)CurrentDirection & 0xFF);
                var msg = new MirCommon.MirMsgOrign
                {
                    dwFlag = ObjectId,
                    wCmd = MirCommon.ProtocolCmd.SM_NOWDEATH,
                    wParam = new ushort[3] { X, Y, dir },
                };

                byte[] encoded = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(msg, payload);
                if (encoded.Length <= 0)
                    return;

                CurrentMap.SendToNearbyPlayers(X, Y, 18, encoded);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"OnDeath广播SM_NOWDEATH失败: {Name}({ObjectId:X8}) - {ex.Message}");
            }
        }

        protected virtual void OnDamaged(AliveObject attacker, int damage, DamageType damageType)
        {
            try
            {
                if (CurrentMap == null)
                    return;

                ushort curHp = (ushort)Math.Clamp(CurrentHP, 0, ushort.MaxValue);
                ushort maxHp = (ushort)Math.Clamp(MaxHP, 0, ushort.MaxValue);
                ushort dmg = (ushort)Math.Clamp(damage, 0, ushort.MaxValue);

                uint attackerId = attacker?.ObjectId ?? 0;
                uint[] dwView = new uint[4]
                {
                    GetFeather(),
                    GetStatus(),
                    attackerId,
                    unchecked((uint)Environment.TickCount),
                };

                byte[] payload = new byte[dwView.Length * 4];
                Buffer.BlockCopy(dwView, 0, payload, 0, payload.Length);

                var msg = new MirCommon.MirMsgOrign
                {
                    dwFlag = ObjectId,
                    wCmd = MirCommon.ProtocolCmd.SM_BEATTACK,
                    wParam = new ushort[3] { curHp, maxHp, dmg },
                };

                byte[] encoded = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(msg, payload);
                if (encoded.Length <= 0)
                    return;

                CurrentMap.SendToNearbyPlayers(X, Y, 18, encoded);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"OnDamaged广播SM_BEATTACK失败: {Name}({ObjectId:X8}) - {ex.Message}");
            }
        }
        protected virtual void OnKilledTarget(AliveObject target) { }
        protected virtual void OnDoAction(ActionType action) { }
        protected virtual void OnChangeTarget(AliveObject? old, AliveObject? newTarget) { }
        protected virtual void OnChangeHitter(AliveObject? old, AliveObject? newHitter) { }
        protected virtual void OnChangeOwner(AliveObject? old, AliveObject? newOwner) { }
        public virtual void OnObjectEnterView(MapObject obj) { }
        public virtual void OnObjectLeaveView(MapObject obj) { }

        #endregion

        public virtual uint GetFeather() => 0;

        public virtual uint GetStatus() => 0;

        public virtual uint GetHealth()
        {
            ushort cur = (ushort)Math.Clamp(CurrentHP, 0, 65535);
            ushort max = (ushort)Math.Clamp(MaxHP, 0, 65535);
            return ((uint)max << 16) | cur;
        }

        public virtual byte GetSex() => 0;

        public virtual string GetViewName() => Name;

        public virtual byte GetNameColor(MapObject? viewer = null) => 255;

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            msg = Array.Empty<byte>();

            if (IsHidden)
                return false;

            string tail = $"{GetViewName()}/{GetNameColor(viewer)}";
            byte[] tailBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(tail);

            byte[] data = new byte[12 + tailBytes.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(GetFeather()), 0, data, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(GetStatus()), 0, data, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(GetHealth()), 0, data, 8, 4);
            Buffer.BlockCopy(tailBytes, 0, data, 12, tailBytes.Length);

            ushort w3 = (ushort)(((ushort)GetSex() << 8) | ((ushort)CurrentDirection & 0xFF));
            ushort cmd = IsDead ? ProtocolCmd.SM_DIE : ProtocolCmd.SM_APPEAR;

            var outMsg = new MirCommon.MirMsgOrign
            {
                dwFlag = ObjectId,
                wCmd = cmd,
                wParam = new ushort[3] { X, Y, w3 },
            };

            msg = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(outMsg, data);
            return msg.Length > 0;
        }
    }

    public enum ActionType
    {
        None = -1,      
        Stand = 0,      
        Walk = 1,       
        Run = 2,        
        Attack = 3,     
        Hit = 4,        
        Die = 5,        
        Spell = 6,      
        Sit = 7,        
        Mining = 8,     
        GetMeat = 9,    
        Max = 10,
        SpellCast = 11,
        Pickup = 12,
        Drop = 13,
        UseItem = 14,
        Equip = 15,
        UnEquip = 16,
        Trade = 17,
        Shop = 18,
        Repair = 19,
        TrainHorse = 20
    }
}
