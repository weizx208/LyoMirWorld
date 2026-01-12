using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;

namespace GameServer
{
    public partial class HumanPlayer : AliveObject, ScriptTarget
    {

        #region 经验和升级

        
        
        
        public void AddExp(uint exp, bool noBonus = false, uint killerId = 0)
        {
            
            
            
            
            

            uint finalExp = exp;

            if (!noBonus)
            {
                
                float expFactor = 1.0f; 

                
                
                float globalExpFactor = GameWorld.Instance.GetGameVar(GameVarConstants.ExpFactor);
                if (globalExpFactor <= 0.0f) globalExpFactor = 1.0f;
                expFactor += globalExpFactor - 1.0f;

                
                if (CurrentMap != null && CurrentMap is LogicMap logicMap)
                {
                    float mapExpFactor = logicMap.GetExpFactor();
                    expFactor += mapExpFactor - 1.0f;
                }

                
                if (_expMagic != null)
                {
                    
                    
                    
                    
                    uint addExp = exp; 
                    
                    SaySystem("经验加成技能生效");
                    TrainMagic(_expMagic);
                }

                
                if (killerId > 0 && IsGodBlessEffective(GodBlessType.DoubleExp))
                {
                    expFactor += 1.0f;
                    
                    
                    
                }

                
                finalExp = (uint)Math.Round(expFactor * exp);

                
                finalExp = (uint)Math.Round(GetExpFactor() * finalExp);
            }

            Exp += finalExp;

            
            uint requiredExp = GetRequiredExp();
            while (Exp >= requiredExp && Level < 255)
            {
                Exp -= requiredExp;
                LevelUp();
                requiredExp = GetRequiredExp();
            }

            
            CheckAndUpgradeTitle();

            
            SendExpChanged(finalExp);

            
            uint petExp = finalExp / 10;
            if (petExp == 0) petExp = 1;
            
        }

        

        
        
        
        private bool IsGodBlessEffective(GodBlessType type)
        {
            
            return false;
        }

        
        
        
        private void CheckAndUpgradeTitle()
        {
            
            
            

            
            int newTitleIndex = GetTitleIndexByExp(Exp);
            if (newTitleIndex != _currentTitleIndex)
            {
                _currentTitleIndex = newTitleIndex;
                _currentTitle = GetTitleByIndex(newTitleIndex);
                
                SendTitleChanged();
            }
        }

        
        
        
        private int GetTitleIndexByExp(uint exp)
        {
            
            
            return (int)(exp / 1000000);
        }

        
        
        
        private string GetTitleByIndex(int index)
        {
            
            string[] titles = {
                "新手", "学徒", "见习", "初级", "中级",
                "高级", "精英", "大师", "宗师", "传奇"
            };

            if (index < 0) return titles[0];
            if (index >= titles.Length) return titles[titles.Length - 1];
            return titles[index];
        }

        
        
        
        private void TrainMagic(PlayerSkill skill)
        {
            if (skill == null)
                return;

            
            if (skill.Level >= 3)
                return;

            if (MagicManager.Instance.GetMagicCount() == 0)
            {
                MagicManager.Instance.LoadAll();
            }

            int oldLevel = skill.Level;

            
            int exp = Random.Shared.Next(1, 4);
            skill.AddExp(exp);

            
            uint train = (uint)Math.Max(0, skill.UseCount);
            SendMsg((uint)skill.SkillId,
                MirCommon.ProtocolCmd.SM_SKILLEXPCHANGED,
                (ushort)Math.Clamp(skill.Level, 0, ushort.MaxValue),
                (ushort)(train & 0xffff),
                (ushort)((train & 0xffff0000) >> 16));

            if (skill.Level != oldLevel)
            {
                
                RecalcHitSpeed();
                UpdateSubProp();
            }
        }

        
        
        
        private void SendTitleChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x283); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(_currentTitle);

            SendMessage(builder.Build());
        }

        
        
        
        private void SendSkillUpgraded(PlayerSkill skill)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x284); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32((uint)skill.Definition.SkillId);
            builder.WriteUInt16((ushort)skill.Level);

            SendMessage(builder.Build());
        }

        
        
        
        private void LevelUp()
        {
            if (Level >= 255)
                return;

            int newLevel = Math.Min(255, (int)Level + 1);

            
            if (!_dbBaseStatsLoaded)
            {
                
                _dbBaseStats.MinDC = Stats.MinDC;
                _dbBaseStats.MaxDC = Stats.MaxDC;
                _dbBaseStats.MinMC = Stats.MinMC;
                _dbBaseStats.MaxMC = Stats.MaxMC;
                _dbBaseStats.MinSC = Stats.MinSC;
                _dbBaseStats.MaxSC = Stats.MaxSC;
                _dbBaseStats.MinAC = Stats.MinAC;
                _dbBaseStats.MaxAC = Stats.MaxAC;
                _dbBaseStats.MinMAC = Stats.MinMAC;
                _dbBaseStats.MaxMAC = Stats.MaxMAC;
                _dbBaseStats.Accuracy = Stats.Accuracy;
                _dbBaseStats.Agility = Stats.Agility;
                _dbBaseStats.Lucky = Stats.Lucky;
                _dbBaseMaxHP = MaxHP;
                _dbBaseMaxMP = MaxMP;
                _dbBaseStatsLoaded = true;
            }

            
            var desc = GameWorld.Instance.GetHumanDataDesc(Job, newLevel);
            if (desc != null)
            {
                _dbBaseMaxHP = desc.Hp;
                _dbBaseMaxMP = desc.Mp;

                _curBagWeight = desc.BagWeight;
                _curBodyWeight = (byte)Math.Clamp((int)desc.BodyWeight, 0, 255);
                _curHandWeight = (byte)Math.Clamp((int)desc.HandWeight, 0, 255);

                _dbBaseStats.MinAC = desc.MinAc;
                _dbBaseStats.MaxAC = desc.MaxAc;
                _dbBaseStats.MinMAC = desc.MinMac;
                _dbBaseStats.MaxMAC = desc.MaxMac;
                _dbBaseStats.MinDC = desc.MinDc;
                _dbBaseStats.MaxDC = desc.MaxDc;
                _dbBaseStats.MinMC = desc.MinMc;
                _dbBaseStats.MaxMC = desc.MaxMc;
                _dbBaseStats.MinSC = desc.MinSc;
                _dbBaseStats.MaxSC = desc.MaxSc;
            }
            else
            {
                
                switch (Job)
                {
                    case 0: 
                        _dbBaseMaxHP += 30;
                        _dbBaseMaxMP += 5;
                        _dbBaseStats.MinDC += 2;
                        _dbBaseStats.MaxDC += 3;
                        _dbBaseStats.MinAC += 1;
                        _dbBaseStats.MaxAC += 2;
                        break;
                    case 1: 
                        _dbBaseMaxHP += 15;
                        _dbBaseMaxMP += 20;
                        _dbBaseStats.MinMC += 2;
                        _dbBaseStats.MaxMC += 3;
                        break;
                    case 2: 
                        _dbBaseMaxHP += 20;
                        _dbBaseMaxMP += 15;
                        _dbBaseStats.MinSC += 2;
                        _dbBaseStats.MaxSC += 3;
                        _dbBaseStats.MinAC += 1;
                        _dbBaseStats.MaxAC += 1;
                        break;
                }
            }

            Level = (byte)newLevel;

            
            CombatStats equip = Equipment?.GetTotalStats() ?? new CombatStats();

            Stats.MinDC = _dbBaseStats.MinDC + equip.MinDC;
            Stats.MaxDC = _dbBaseStats.MaxDC + equip.MaxDC;
            Stats.MinMC = _dbBaseStats.MinMC + equip.MinMC;
            Stats.MaxMC = _dbBaseStats.MaxMC + equip.MaxMC;
            Stats.MinSC = _dbBaseStats.MinSC + equip.MinSC;
            Stats.MaxSC = _dbBaseStats.MaxSC + equip.MaxSC;
            Stats.MinAC = _dbBaseStats.MinAC + equip.MinAC;
            Stats.MaxAC = _dbBaseStats.MaxAC + equip.MaxAC;
            Stats.MinMAC = _dbBaseStats.MinMAC + equip.MinMAC;
            Stats.MaxMAC = _dbBaseStats.MaxMAC + equip.MaxMAC;
            Stats.Accuracy = _dbBaseStats.Accuracy + equip.Accuracy;
            Stats.Agility = _dbBaseStats.Agility + equip.Agility;
            Stats.Lucky = _dbBaseStats.Lucky + equip.Lucky;

            MaxHP = _dbBaseMaxHP + equip.MaxHP;
            MaxMP = _dbBaseMaxMP + equip.MaxMP;
            MaxHP = Math.Max(1, MaxHP);
            MaxMP = Math.Max(1, MaxMP);

            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
            Stats.MaxHP = MaxHP;
            Stats.HP = CurrentHP;
            Stats.MaxMP = MaxMP;
            Stats.MP = CurrentMP;

            
            _equipStatsCache.MaxHP = equip.MaxHP;
            _equipStatsCache.MaxMP = equip.MaxMP;
            _equipStatsCache.MinDC = equip.MinDC;
            _equipStatsCache.MaxDC = equip.MaxDC;
            _equipStatsCache.MinMC = equip.MinMC;
            _equipStatsCache.MaxMC = equip.MaxMC;
            _equipStatsCache.MinSC = equip.MinSC;
            _equipStatsCache.MaxSC = equip.MaxSC;
            _equipStatsCache.MinAC = equip.MinAC;
            _equipStatsCache.MaxAC = equip.MaxAC;
            _equipStatsCache.MinMAC = equip.MinMAC;
            _equipStatsCache.MaxMAC = equip.MaxMAC;
            _equipStatsCache.Accuracy = equip.Accuracy;
            _equipStatsCache.Agility = equip.Agility;
            _equipStatsCache.Lucky = equip.Lucky;

            
            SendLevelUp();
            UpdateProp();
            UpdateSubProp();
            
            SendHpMpChanged();
            SendWeightChanged();

            Say($"恭喜！你升到了 {Level} 级！");
            LogManager.Default.Info($"{Name} 升级到 {Level} 级");
        }

        
        
        
        private uint GetRequiredExp()
        {
            
            
            
            if (Level >= 255)
                return uint.MaxValue;

            var desc = GameWorld.Instance.GetHumanDataDesc(Job, Level);
            if (desc != null && desc.LevelupExp > 0)
                return desc.LevelupExp;

            
            if (Level <= 1) return 100;
            if (Level <= 10) return (uint)(Level * 100);
            if (Level <= 20) return (uint)(Level * 200);
            if (Level <= 30) return (uint)(Level * 400);
            if (Level <= 40) return (uint)(Level * 800);
            if (Level <= 50) return (uint)(Level * 1600);
            if (Level <= 60) return (uint)(Level * 3200);
            if (Level <= 70) return (uint)(Level * 6400);
            if (Level <= 80) return (uint)(Level * 12800);
            if (Level <= 90) return (uint)(Level * 25600);
            return (uint)(Level * 51200);
        }

        private void SendExpChanged(uint gainedExp)
        {
            
            
            ushort w1 = (ushort)(gainedExp & 0xFFFF);
            ushort w2 = (ushort)((gainedExp >> 16) & 0xFFFF);
            SendMsg(Exp, 0x2c, w1, w2, 0);
        }

        private void SendLevelUp()
        {
            
            
            ushort level = (ushort)Level;
            ushort idLow = (ushort)(ObjectId & 0xFFFF);
            ushort idHigh = (ushort)((ObjectId >> 16) & 0xFFFF);

            SendMsg(Exp, 0x2d, level, idLow, idHigh);

            if (CurrentMap != null)
            {
                var msg = new MirCommon.MirMsgOrign
                {
                    dwFlag = Exp,
                    wCmd = 0x2d,
                    wParam = new ushort[3] { level, idLow, idHigh }
                };
                byte[] encoded = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(msg, null);
                if (encoded.Length > 0)
                {
                    CurrentMap.SendToNearbyPlayers(X, Y, 18, encoded, ObjectId);
                }
            }
        }

        #endregion

        #region 金钱管理

        
        
        
        public bool AddGold(uint amount)
        {
            if (amount > uint.MaxValue - Gold)
                return false;

            Gold += amount;
            SendMoneyChanged(MoneyType.Gold);
            return true;
        }

        
        
        
        public bool TakeGold(uint amount)
        {
            if (Gold < amount)
                return false;

            Gold -= amount;
            SendMoneyChanged(MoneyType.Gold);
            return true;
        }

        
        
        
        public bool CanAddGold(uint amount)
        {
            return amount <= uint.MaxValue - Gold;
        }

        
        
        
        public bool AddYuanbao(uint amount)
        {
            if (amount > uint.MaxValue - Yuanbao)
                return false;

            Yuanbao += amount;
            SendMoneyChanged(MoneyType.Yuanbao);
            return true;
        }

        
        
        
        public bool TakeYuanbao(uint amount)
        {
            if (Yuanbao < amount)
                return false;

            Yuanbao -= amount;
            SendMoneyChanged(MoneyType.Yuanbao);
            return true;
        }

        
        
        
        public bool CanAddYuanbao(uint amount)
        {
            return amount <= uint.MaxValue - Yuanbao;
        }

        
        
        
        public bool CanTakeYuanbao(uint amount)
        {
            return Yuanbao >= amount;
        }

        

        #endregion

        #region 物品管理

        
        
        
        public bool PickupItem(MapItem mapItem)
        {
            if (mapItem == null || mapItem.CurrentMap != CurrentMap)
                return false;

            
            if (!mapItem.CanPickup(ObjectId))
            {
                Say("这个物品还不能拾取");
                return false;
            }

            
            if (!Inventory.AddItem(mapItem.Item))
            {
                Say("背包已满");
                return false;
            }

            
            CurrentMap?.RemoveObject(mapItem);

            Say($"你获得了 {mapItem.Item.Definition.Name}");
            OnPickupItem(mapItem.Item);
            return true;
        }

        
        
        
        public bool DropItem(int slot)
        {
            var item = Inventory.GetItem(slot);
            if (item == null || CurrentMap == null)
                return false;

            if (!item.Definition.CanDrop)
            {
                Say("这个物品不能丢弃");
                return false;
            }

            
            if (!Inventory.RemoveItem(slot, 1))
                return false;

            
            var mapItem = new MapItem(item)
            {
                OwnerPlayerId = ObjectId
            };

            
            CurrentMap.AddObject(mapItem, X, Y);

            OnDropItem(item);
            return true;
        }

        
        
        
        public bool UseItem(int slot)
        {
            var item = Inventory.GetItem(slot);
            if (item == null)
                return false;

            switch (item.Definition.Type)
            {
                case ItemType.Potion:
                    return UsePotion(item);
                case ItemType.Book:
                    return LearnSkill(item);
                default:
                    Say("这个物品不能使用");
                    return false;
            }
        }

        
        
        
        private bool UsePotion(ItemInstance item)
        {
            if (item.Definition.HP > 0)
            {
                Heal(item.Definition.HP);
            }

            if (item.Definition.MP > 0)
            {
                RestoreMP(item.Definition.MP);
            }

            
            var slot = Inventory.GetAllItems().FirstOrDefault(kvp => kvp.Value == item).Key;
            if (slot >= 0)
            {
                Inventory.RemoveItem(slot, 1);
            }

            return true;
        }

        
        
        
        private bool LearnSkill(ItemInstance item)
        {
            
            if (item.Definition.Type != ItemType.Book)
            {
                Say("这不是技能书");
                return false;
            }

            
            
            
            int skillId = 0; 
            if (skillId == 0)
            {
                Say("这本技能书无法使用");
                return false;
            }

            
            if (SkillBook.HasSkill(skillId))
            {
                Say("你已经学会了这个技能");
                return false;
            }

            
            bool success = SkillExecutor.Instance.LearnSkill(this, skillId);
            if (success)
            {
                
                var slot = Inventory.GetAllItems().FirstOrDefault(kvp => kvp.Value == item).Key;
                if (slot >= 0)
                {
                    Inventory.RemoveItem(slot, 1);
                }
                Say($"你学会了 {item.Definition.Name}！");
            }
            else
            {
                Say("学习技能失败");
            }

            return success;
        }

        
        
        
        private void OnPickupItem(ItemInstance item)
        {
            
            
        }

        
        
        
        private void OnDropItem(ItemInstance item)
        {
            
        }

        #endregion

        #region 技能系统

        
        
        
        public bool LearnNewSkill(uint skillId)
        {
            var definition = SkillManager.Instance.GetDefinition((int)skillId);
            if (definition == null)
                return false;

            return SkillBook.LearnSkill(definition);
        }

        
        
        
        public bool UseSkill(uint skillId, uint targetId = 0)
        {
            var skill = SkillBook.GetSkill((int)skillId);
            if (skill == null)
            {
                Say("未学习此技能");
                return false;
            }

            
            ICombatEntity? target = null;
            if (targetId > 0)
            {
                target = CurrentMap?.GetObject(targetId) as ICombatEntity;
                if (target == null)
                {
                    Say("目标不存在");
                    return false;
                }
            }

            
            var result = SkillExecutor.Instance.UseSkill(this, (int)skillId, target);
            if (!result.Success)
            {
                Say(result.Message);
                return false;
            }

            return true;
        }

        #endregion

        #region 交易系统

        
        
        
        public bool StartTrade(uint targetPlayerId)
        {
            if (_tradingWithPlayerId != 0)
            {
                Say("你已经在交易中");
                return false;
            }

            
            var targetPlayer = HumanPlayerMgr.Instance.FindById(targetPlayerId);
            if (targetPlayer == null)
            {
                Say("目标玩家不存在");
                return false;
            }

            _tradingWithPlayerId = targetPlayerId;
            CurrentTrade = new TradeObject(this, targetPlayer);
            return true;
        }

        
        
        
        public bool AddTradeItem(int slot, uint count)
        {
            if (CurrentTrade == null)
                return false;

            var item = Inventory.GetItem(slot);
            if (item == null)
                return false;

            
            var tradeItem = new ItemInstance(new ItemDefinition(0, "", ItemType.Other), 0);
            
            return CurrentTrade.PutItem(this, tradeItem);
        }

        
        
        
        public bool SetTradeGold(uint amount)
        {
            if (CurrentTrade == null || Gold < amount)
                return false;

            return CurrentTrade.PutMoney(this, MoneyType.Gold, amount);
        }

        
        
        
        public bool ConfirmTrade()
        {
            if (CurrentTrade == null)
                return false;

            return CurrentTrade.End(this, TradeEndType.Confirm);
        }

        
        
        
        public void CancelTrade()
        {
            if (CurrentTrade != null)
            {
                CurrentTrade.End(this, TradeEndType.Cancel);
                CurrentTrade = null;
            }
            _tradingWithPlayerId = 0;
        }

        #endregion

        #region 挖矿/挖肉系统

        
        private DateTime _lastMineTime = DateTime.MinValue;
        private uint _mineCounter = 0;

        
        private DateTime _lastGetMeatTime = DateTime.MinValue;

        
        
        
        public bool Mine(MineSpot mineSpot)
        {
            if (mineSpot == null || mineSpot.CurrentMap != CurrentMap)
                return false;

            
            if (!IsInRange(mineSpot, 1))
            {
                Say("距离太远");
                return false;
            }

            
            if ((DateTime.Now - _lastMineTime).TotalSeconds < 3.0)
            {
                Say("挖矿太快了，请稍等");
                return false;
            }

            
            if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
            {
                Say("背包已满");
                return false;
            }

            
            StartAction(ActionType.Mining, mineSpot.ObjectId);

            
            _lastMineTime = DateTime.Now;
            _mineCounter++;

            return true;
        }

        
        
        
        public bool GetMeat(MonsterCorpse corpse)
        {
            if (corpse == null || corpse.CurrentMap != CurrentMap)
                return false;

            
            if (!IsInRange(corpse, 1))
            {
                Say("距离太远");
                return false;
            }

            
            if ((DateTime.Now - _lastGetMeatTime).TotalSeconds < 2.0)
            {
                Say("挖肉太快了，请稍等");
                return false;
            }

            
            if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
            {
                Say("背包已满");
                return false;
            }

            
            StartAction(ActionType.GetMeat, corpse.ObjectId);

            
            _lastGetMeatTime = DateTime.Now;

            return true;
        }

        
        
        
        private void CompleteMining(uint mineSpotId)
        {
            
            var mineSpot = CurrentMap?.GetObject(mineSpotId) as MineSpot;
            if (mineSpot == null)
                return;

            
            
            ItemDefinition definition;
            if (_mineCounter % 10 == 0)
            {
                
                definition = new ItemDefinition(4002, "金矿石", ItemType.Material);
                definition.SellPrice = 500; 
            }
            else if (_mineCounter % 5 == 0)
            {
                
                definition = new ItemDefinition(4001, "银矿石", ItemType.Material);
                definition.SellPrice = 200;
            }
            else
            {
                
                definition = new ItemDefinition(4000, "铁矿石", ItemType.Material);
                definition.SellPrice = 50;
            }

            
            var item = new ItemInstance(definition, (long)ItemManager.Instance.AllocateTempMakeIndex());

            
            if (Inventory.AddItem(item))
            {
                Say($"你挖到了一块{definition.Name}");

                
                

                
                SaySystem("挖矿完成");
            }
            else
            {
                Say("背包已满");
            }

            
            
            CurrentMap?.RemoveObject(mineSpot);
        }

        
        
        
        private void CompleteGetMeat(uint corpseId)
        {
            
            var corpse = CurrentMap?.GetObject(corpseId) as MonsterCorpse;
            if (corpse == null)
                return;

            
            
            Random rand = new Random();
            int meatCount = rand.Next(1, 4);

            ItemDefinition definition = new ItemDefinition(4003, "肉", ItemType.Material);
            definition.SellPrice = 10;

            bool success = false;
            for (int i = 0; i < meatCount; i++)
            {
                var item = new ItemInstance(definition, (long)ItemManager.Instance.AllocateTempMakeIndex());
                if (Inventory.AddItem(item))
                {
                    success = true;
                }
                else
                {
                    break;
                }
            }

            if (success)
            {
                if (meatCount > 1)
                {
                    Say($"你获得了{meatCount}块肉");
                }
                else
                {
                    Say("你获得了一块肉");
                }

                

                
                SaySystem("挖肉完成");
            }
            else
            {
                Say("背包已满");
            }

            
            CurrentMap?.RemoveObject(corpse);
        }

        #endregion

        #region 移动和动作方法

        
        
        
        public bool WalkXY(int x, int y)
        {
            
            return base.WalkXY((ushort)x, (ushort)y);
        }

        
        
        
        public bool Walk(byte direction)
        {
            return base.Walk((Direction)direction);
        }

        
        
        
        public bool RunXY(int x, int y)
        {
            return base.RunXY((ushort)x, (ushort)y);
        }

        
        
        
        public bool Run(byte direction)
        {
            return base.Run((Direction)direction);
        }

        
        
        
        public bool Turn(byte direction)
        {
            Direction = direction;
            return true;
        }

        
        
        
        public bool Attack(byte direction)
        {
            Direction = direction;

            
            int targetX = X;
            int targetY = Y;

            switch (direction)
            {
                case 0: targetY--; break; 
                case 1: targetX++; targetY--; break; 
                case 2: targetX++; break; 
                case 3: targetX++; targetY++; break; 
                case 4: targetY++; break; 
                case 5: targetX--; targetY++; break; 
                case 6: targetX--; break; 
                case 7: targetX--; targetY--; break; 
            }

            
            if (CurrentMap == null)
                return false;

            
            var target = CurrentMap.GetObjectAt(targetX, targetY) as ICombatEntity;
            if (target == null)
            {
                Say("没有可攻击的目标");
                return false;
            }

            
            if (!CanAttackTarget(target))
            {
                Say("不能攻击此目标");
                return false;
            }

            
            PerformAttack(target);
            return true;
        }

        
        
        
        private bool CanAttackTarget(ICombatEntity target)
        {
            if (target == null || target == this)
                return false;

            
            if (target is HumanPlayer targetPlayer)
            {
                
                
                
                return true;
            }
            else if (target is MonsterEx)
            {
                
                return true;
            }
            else if (target is Npc)
            {
                
                return false;
            }

            return false;
        }

        
        
        
        private void PerformAttack(ICombatEntity target)
        {
            if (target == null || CurrentMap == null)
                return;

            
            var combatResult = CombatSystemManager.Instance.ExecuteCombat(this, target, DamageType.Physics);

            if (!combatResult.Hit)
            {
                Say("攻击未命中");
                return;
            }

            
            DamageWeaponDurability();

            
            if (target is HumanPlayer targetPlayer)
            {
                CheckPk(targetPlayer);
            }

            
            if (combatResult.TargetDied)
            {
                OnKillTarget(target);
            }

            
            UpdateAutoMagic();
        }

        
        
        
        private void DamageWeaponDurability()
        {
            
            var weapon = Equipment.GetWeapon();
            if (weapon == null)
                return;

            
            
            

            
            if (weapon.Durability > 0)
            {
                
                weapon.Durability--;

                
                if (weapon.Durability <= 0)
                {
                    Say("你的武器已经损坏！");
                    
                    
                }

                
                
            }
        }

        
        
        
        private void CheckPk(HumanPlayer target)
        {
            if (target == null)
                return;

            
            if (InSafeArea() || target.InSafeArea())
                return;

            
            if (CurrentMap != null && CurrentMap is LogicMap logicMap && logicMap.IsFightMap())
                return;

            
            if (Guild != null && target.Guild != null)
            {
                
                if (Guild.IsKillGuild(target.Guild))
                    return;

                
                if (Guild.IsAllyGuild(target.Guild))
                    return;
            }

            
            
            
            

            
            
        }

        
        
        
        private bool InSafeArea()
        {
            if (CurrentMap == null)
                return false;

            
            
            
            return false;
        }

        
        
        
        private void OnKillTarget(ICombatEntity target)
        {
            if (target == null)
                return;

            
            int exp = CombatSystemManager.Instance.CalculateExp(this, target);
            if (exp > 0)
            {
                AddExp((uint)exp, false, target.Id);
            }

            
            
        }

        
        
        
        public bool SpellCast(int x, int y, uint magicId, ushort targetId)
        {
            try
            {
                if (CurrentMap is LogicMap map && map.IsFlagSeted(MapFlag.MF_NOSPELL))
                {
                    SaySystem("当前地图禁止施法");
                    return false;
                }

                var skill = SkillBook.GetSkill((int)magicId);
                if (skill == null)
                {
                    SaySystem("未学习该技能");
                    return false;
                }

                if (!skill.CanUse())
                {
                    SaySystem("技能冷却中");
                    return false;
                }

                if (MagicManager.Instance.GetMagicCount() == 0)
                {
                    MagicManager.Instance.LoadAll();
                }

                var magicClass = MagicManager.Instance.GetClassById((int)magicId);
                if (magicClass != null)
                {
                    int mpCost = Math.Max(0, (int)magicClass.sSpell);
                    if (mpCost > 0 && !ConsumeMP(mpCost))
                    {
                        SaySystem("魔法不足");
                        return false;
                    }
                }

                
                skill.Use();
                TrainMagic(skill);
                UpdateAutoMagic();
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Warning($"SpellCast异常: player={Name}, magicId={magicId}, err={ex.Message}");
                return false;
            }
        }

        
        
        
        public bool UseItem(uint makeIndex)
        {
            try
            {
                var item = Inventory.GetItemByMakeIndex(makeIndex);
                if (item == null)
                {
                    SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                    return false;
                }

                
                SetUsingItem(item);

                
                string pageScript = item.Definition.PageScript ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(pageScript))
                {
                    bool executed = CxxScriptExecutor.Execute(this, pageScript, silent: true);
                    var r = GetUsingItemResult();
                    SetUsingItem(null);

                    if (!executed)
                    {
                        SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                        return false;
                    }

                    
                    if (r == UsingItemResult.Deleted)
                    {
                        Inventory.RemoveItemByMakeIndex(makeIndex, 1);
                        SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_OK, 0, 0, 0);
                        SendWeightChanged();
                        return true;
                    }

                    
                    if (r == UsingItemResult.Updated)
                    {
                        SendUpdateItem(item);
                    }

                    
                    SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                    return true;
                }

                
                bool success = false;
                switch ((MirCommon.ItemStdMode)item.Definition.StdMode)
                {
                    case MirCommon.ItemStdMode.ISM_DRUG:
                        {
                            
                            if (item.Definition.HP > 0)
                                Heal(item.Definition.HP);
                            if (item.Definition.MP > 0)
                                RestoreMP(item.Definition.MP);

                            success = true;
                        }
                        break;
                    case MirCommon.ItemStdMode.ISM_BOOK:
                        success = UseSkillBook(item);
                        break;
                    case MirCommon.ItemStdMode.ISM_USABLEITEM:
                        {
                            SetUsingItem(null);
                            return UseUsableItem(makeIndex, item);
                        }
                    default:
                        success = false;
                        break;
                }

                SetUsingItem(null);

                if (success)
                {
                    Inventory.RemoveItemByMakeIndex(makeIndex, 1);
                    SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_OK, 0, 0, 0);
                    SendWeightChanged();
                    return true;
                }

                SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                return false;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"使用物品失败: player={Name}, makeIndex={makeIndex}, err={ex.Message}");
                try { SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0); } catch { }
                return false;
            }
        }

        private bool UseUsableItem(uint makeIndex, ItemInstance item)
        {
            try
            {
                
                if (CurrentMap == null)
                {
                    SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                    return false;
                }

                uint targetMapId = (uint)Math.Max(0, MapId);
                ushort targetX = X;
                ushort targetY = Y;

                switch (item.Definition.Shape)
                {
                    case 2: 
                        {
                            
                            if (CurrentMap.IsFlagSeted(MapFlag.MF_NORUN))
                            {
                                SaySystem("当前地图不能使用随机");
                                SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                                return false;
                            }

                            if (!TryGetRandomTeleportPoint(CurrentMap, out targetX, out targetY))
                            {
                                SaySystem("无法找到可传送的位置");
                                SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                                return false;
                            }
                        }
                        break;
                    case 3: 
                        {
                            
                            if (CurrentMap.IsFlagSeted(MapFlag.MF_NORECALL))
                            {
                                SaySystem("当前地图不能使用回城");
                                SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                                return false;
                            }

                            if (!GameWorld.Instance.GetBornPoint(Job, out int mapId, out int x, out int y, _startPointName))
                            {
                                SaySystem("回城点无效");
                                SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                                return false;
                            }

                            targetMapId = (uint)Math.Max(0, mapId);
                            targetX = (ushort)Math.Clamp(x, 0, ushort.MaxValue);
                            targetY = (ushort)Math.Clamp(y, 0, ushort.MaxValue);
                        }
                        break;
                    default:
                        SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0);
                        return false;
                }

                
                Inventory.RemoveItemByMakeIndex(makeIndex, 1);
                SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_OK, 0, 0, 0);
                SendWeightChanged();

                
                _ = ChangeMap(targetMapId, targetX, targetY);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Warning($"UseUsableItem失败: player={Name}, makeIndex={makeIndex}, err={ex.Message}");
                try { SendMsg(0, MirCommon.ProtocolCmd.SM_EAT_FAIL, 0, 0, 0); } catch { }
                return false;
            }
        }

        private static bool TryGetRandomTeleportPoint(LogicMap map, out ushort x, out ushort y)
        {
            x = 0;
            y = 0;

            if (map.Width <= 0 || map.Height <= 0)
                return false;

            var rnd = Random.Shared;
            var pts = new System.Drawing.Point[1];

            for (int i = 0; i < 200; i++)
            {
                int px = rnd.Next(0, map.Width);
                int py = rnd.Next(0, map.Height);

                if (!map.IsBlocked(px, py))
                {
                    x = (ushort)px;
                    y = (ushort)py;
                    return true;
                }

                if (map.GetValidPoint(px, py, pts, 1) > 0)
                {
                    x = (ushort)Math.Clamp(pts[0].X, 0, ushort.MaxValue);
                    y = (ushort)Math.Clamp(pts[0].Y, 0, ushort.MaxValue);
                    return true;
                }
            }

            return false;
        }

        private bool UseSkillBook(ItemInstance item)
        {
            
            
            if (item.Definition.Shape != Job)
            {
                SaySystem("职业不符，无法学习该技能");
                return false;
            }

            
            if (item.Definition.MaxDura > Level)
            {
                SaySystem("等级不足，无法学习该技能");
                return false;
            }

            
            if (MagicManager.Instance.GetMagicCount() == 0)
            {
                MagicManager.Instance.LoadAll();
            }

            string magicName = item.Definition.Name;
            var magicClass = MagicManager.Instance.GetClassByName(magicName);
            if (magicClass == null)
            {
                SaySystem("技能物品未配置");
                return false;
            }

            
            bool hasNeedMagicRule = magicClass.wNeedMagic.Any(id => id != 0);
            if (hasNeedMagicRule)
            {
                bool ok = false;
                var needNames = new List<string>();

                foreach (var needId in magicClass.wNeedMagic)
                {
                    if (needId == 0) continue;
                    if (SkillBook.HasMagic(needId))
                    {
                        ok = true;
                        break;
                    }

                    var needClass = MagicManager.Instance.GetClassById(needId);
                    if (needClass != null && !string.IsNullOrWhiteSpace(needClass.szName))
                    {
                        needNames.Add(needClass.szName);
                    }
                }

                if (!ok)
                {
                    SaySystem($"你不能学习{magicClass.szName}，在这之前你需要学习{string.Join(",", needNames)}");
                    return false;
                }
            }

            
            foreach (var mutexId in magicClass.wMutexMagic)
            {
                if (mutexId == 0) continue;
                if (SkillBook.HasMagic(mutexId))
                {
                    var mutexClass = MagicManager.Instance.GetClassById(mutexId);
                    string mutexName = mutexClass?.szName ?? mutexId.ToString();
                    SaySystem($"你已经学会{mutexName}，无法学习{magicClass.szName}");
                    return false;
                }
            }

            
            if (SkillBook.HasMagic(magicClass.id))
            {
                SaySystem($"你已经学会{magicClass.szName}，无法重复学习");
                return false;
            }

             
             var magicDb = new MirCommon.Database.MAGICDB
             {
                 btUserKey = 0,
                 
                 btCurLevel = 0,
                 wMagicId = (ushort)magicClass.id,
                 dwCurTrain = 0
             };

            SetMagic(magicDb, 0);
            return true;
        }

        
        
        
        public bool DropGold(uint amount)
        {
            if (Gold < amount)
                return false;

            Gold -= amount;
            return true;
        }

        
        
        
        public bool BuyItem(uint npcInstanceId, int itemIndex)
        {
            
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Shop))
            {
                SaySystem("这个NPC不提供购买服务");
                return false;
            }

            
            if (itemIndex < 0 || itemIndex >= npc.Definition.ShopItems.Count)
            {
                SaySystem("无效的物品索引");
                return false;
            }

            int itemId = npc.Definition.ShopItems[itemIndex];
            var itemDef = ItemManager.Instance.GetDefinition(itemId);
            if (itemDef == null)
            {
                SaySystem("物品不存在");
                return false;
            }

            
            uint price = (uint)(itemDef.BuyPrice * npc.Definition.SellRate);
            if (price == 0) price = 1; 

            
            if (Gold < price)
            {
                SaySystem($"金币不足，需要 {price} 金币");
                return false;
            }

            
            if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
            {
                SaySystem("背包已满");
                return false;
            }

            
            var item = ItemManager.Instance.CreateItem(itemId);
            if (item == null)
            {
                SaySystem("无法创建物品");
                return false;
            }

            
            if (!TakeGold(price))
            {
                SaySystem("金币扣除失败");
                return false;
            }

            
            if (!Inventory.AddItem(item))
            {
                
                AddGold(price);
                SaySystem("背包空间不足");
                return false;
            }

            
            LogManager.Default.Info($"{Name} 从 {npc.Definition.Name} 购买了 {item.Definition.Name}，花费 {price} 金币");

            
            SaySystem($"购买了 {item.Definition.Name}，花费 {price} 金币");

            
            SendInventoryUpdate();

            return true;
        }

        
        
        
        public bool SellItem(uint npcInstanceId, int bagSlot)
        {
            
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Shop))
            {
                SaySystem("这个NPC不提供出售服务");
                return false;
            }

            
            var item = Inventory.GetItem(bagSlot);
            if (item == null)
            {
                SaySystem("该位置没有物品");
                return false;
            }

            
            if (!item.Definition.CanTrade)
            {
                SaySystem("这个物品不能出售");
                return false;
            }

            
            if (item.IsBound)
            {
                SaySystem("绑定物品不能出售");
                return false;
            }

            
            uint price = (uint)(item.Definition.SellPrice * npc.Definition.BuyRate * item.Count);
            if (price == 0) price = 1; 

            
            SaySystem($"出售 {item.Definition.Name} x{item.Count}，获得 {price} 金币？");

            
            if (!Inventory.RemoveItem(bagSlot, item.Count))
            {
                SaySystem("移除物品失败");
                return false;
            }

            
            if (!AddGold(price))
            {
                
                Inventory.AddItem(item);
                SaySystem("金币添加失败");
                return false;
            }

            
            LogManager.Default.Info($"{Name} 向 {npc.Definition.Name} 出售了 {item.Definition.Name} x{item.Count}，获得 {price} 金币");

            
            SaySystem($"出售了 {item.Definition.Name} x{item.Count}，获得 {price} 金币");

            
            SendInventoryUpdate();

            return true;
        }

        
        
        
        public bool RepairItem(uint npcInstanceId, int bagSlot)
        {
            
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Repair))
            {
                SaySystem("这个NPC不提供修理服务");
                return false;
            }

            
            ItemInstance? item = null;

            if (bagSlot >= 0)
            {
                
                item = Inventory.GetItem(bagSlot);
                if (item == null)
                {
                    SaySystem("该位置没有物品");
                    return false;
                }
            }
            else
            {
                
                int equipSlot = -bagSlot - 1;
                if (equipSlot < 0 || equipSlot >= (int)EquipSlot.Max)
                {
                    SaySystem("无效的装备槽");
                    return false;
                }

                item = Equipment.GetEquipment((EquipSlot)equipSlot);
                if (item == null)
                {
                    SaySystem("该位置没有装备");
                    return false;
                }
            }

            
            if (item.Durability >= item.MaxDurability)
            {
                SaySystem("物品不需要修理");
                return false;
            }

            
            uint repairCost = CalculateRepairCost(item);
            if (repairCost == 0)
            {
                SaySystem("无法计算修理费用");
                return false;
            }

            
            if (Gold < repairCost)
            {
                SaySystem($"金币不足，需要 {repairCost} 金币");
                return false;
            }

            
            if (!TakeGold(repairCost))
            {
                SaySystem("金币扣除失败");
                return false;
            }

            
            item.Durability = item.MaxDurability;

            
            LogManager.Default.Info($"{Name} 修理了 {item.Definition.Name}，花费 {repairCost} 金币");

            
            SaySystem($"修理了 {item.Definition.Name}，花费 {repairCost} 金币");

            
            if (bagSlot >= 0)
            {
                SendInventoryUpdate();
            }
            else
            {
                SendEquipmentUpdate((EquipSlot)(-bagSlot - 1), item);
            }

            return true;
        }

        
        
        
        public bool QueryRepairPrice(uint npcInstanceId, int bagSlot)
        {
            
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Repair))
            {
                SaySystem("这个NPC不提供修理服务");
                return false;
            }

            
            ItemInstance? item = null;

            if (bagSlot >= 0)
            {
                
                item = Inventory.GetItem(bagSlot);
                if (item == null)
                {
                    SaySystem("该位置没有物品");
                    return false;
                }
            }
            else
            {
                
                int equipSlot = -bagSlot - 1;
                if (equipSlot < 0 || equipSlot >= (int)EquipSlot.Max)
                {
                    SaySystem("无效的装备槽");
                    return false;
                }

                item = Equipment.GetEquipment((EquipSlot)equipSlot);
                if (item == null)
                {
                    SaySystem("该位置没有装备");
                    return false;
                }
            }

            
            if (item.Durability >= item.MaxDurability)
            {
                SaySystem("物品不需要修理");
                return true;
            }

            
            uint repairCost = CalculateRepairCost(item);
            if (repairCost == 0)
            {
                SaySystem("无法计算修理费用");
                return false;
            }

            
            SaySystem($"修理 {item.Definition.Name} 需要 {repairCost} 金币");

            return true;
        }

        
        
        
        public bool ViewEquipment(uint targetPlayerId)
        {
            
            var targetPlayer = HumanPlayerMgr.Instance.FindById(targetPlayerId);
            if (targetPlayer == null)
            {
                SaySystem("目标玩家不存在");
                return false;
            }

            
            if (CurrentMap != targetPlayer.CurrentMap || !IsInRange(targetPlayer, 5))
            {
                SaySystem("距离太远");
                return false;
            }

            
            var builder = new PacketBuilder();
            builder.WriteUInt32(targetPlayer.ObjectId);
            builder.WriteUInt16(0x28A); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            
            builder.WriteString(targetPlayer.Name);
            builder.WriteUInt16((ushort)targetPlayer.Level);
            builder.WriteByte(targetPlayer.Job);

            
            var allEquipment = targetPlayer.Equipment.GetAllEquipment();
            builder.WriteByte((byte)allEquipment.Count);

            foreach (var equip in allEquipment)
            {
                builder.WriteUInt32((uint)equip.InstanceId);
                builder.WriteInt32(equip.ItemId);
                builder.WriteString(equip.Definition.Name);
                builder.WriteUInt16((ushort)equip.Durability);
                builder.WriteUInt16((ushort)equip.MaxDurability);
                builder.WriteByte((byte)equip.EnhanceLevel);
            }

            
            SendMessage(builder.Build());

            
            LogManager.Default.Info($"{Name} 查看了 {targetPlayer.Name} 的装备");

            return true;
        }


        
        
        
        
        public bool SpecialHit(byte direction, int skillType)
        {
            Direction = direction;

            
            switch (skillType)
            {
                case 7:  
                    return ExecuteKill(direction);
                case 12: 
                    return ExecuteAssassinate(direction);
                case 25: 
                    return ExecuteHalfMoon(direction);
                case 26: 
                    return ExecuteFireSword(direction);
                default:
                    SaySystem("未知的特殊攻击类型");
                    return false;
            }
        }

        
        
        
        private bool ExecuteKill(byte direction)
        {
            if (!SkillBook.HasSkill(7))
            {
                SaySystem("未学习攻杀剑法");
                return false;
            }

            var skill = SkillBook.GetSkill(7);
            if (skill == null)
                return false;

            
            if (!skill.Activated)
            {
                return Attack(direction);
            }

            skill.Activated = false;

            if (CurrentMap == null)
                return false;

            
            int targetX = X;
            int targetY = Y;
            switch (direction)
            {
                case 0: targetY--; break;
                case 1: targetX++; targetY--; break;
                case 2: targetX++; break;
                case 3: targetX++; targetY++; break;
                case 4: targetY++; break;
                case 5: targetX--; targetY++; break;
                case 6: targetX--; break;
                case 7: targetX--; targetY--; break;
            }

            var target = CurrentMap.GetObjectAt(targetX, targetY) as ICombatEntity;
            if (target == null || !CanAttackTarget(target))
                return false;

            if (MagicManager.Instance.GetMagicCount() == 0)
                MagicManager.Instance.LoadAll();

            int bonus = 0;
            var magicClass = MagicManager.Instance.GetClassById(7);
            if (magicClass != null)
            {
                bonus = magicClass.sDefPower + magicClass.sDefMaxPower * skill.Level;
                if (bonus < 0) bonus = 0;
            }

            var combatResult = CombatSystemManager.Instance.ExecuteCombat(this, target, DamageType.Physics);
            if (!combatResult.Hit)
                return false;

            if (bonus > 0 && !combatResult.TargetDied)
            {
                combatResult.TargetDied = target.TakeDamage(this, bonus, DamageType.Physics);
            }

            DamageWeaponDurability();

            if (target is HumanPlayer targetPlayer)
            {
                CheckPk(targetPlayer);
            }

            if (combatResult.TargetDied)
            {
                OnKillTarget(target);
            }

            skill.Use();
            TrainMagic(skill);
            UpdateAutoMagic();
            SaySystem("攻杀剑法！");
            return true;
        }

        
        
        
        private bool ExecuteAssassinate(byte direction)
        {
            
            if (!SkillBook.HasSkill(12))
            {
                SaySystem("未学习刺杀剑术");
                return false;
            }

            
            var skill = SkillBook.GetSkill(12);
            if (skill == null || !skill.CanUse())
            {
                SaySystem("刺杀剑术技能不可用");
                return false;
            }

            
            int targetX = X;
            int targetY = Y;
            int secondTargetX = X;
            int secondTargetY = Y;

            
            switch (direction)
            {
                case 0: 
                    targetY--;
                    secondTargetY -= 2;
                    break;
                case 1: 
                    targetX++; targetY--;
                    secondTargetX += 2; secondTargetY -= 2;
                    break;
                case 2: 
                    targetX++;
                    secondTargetX += 2;
                    break;
                case 3: 
                    targetX++; targetY++;
                    secondTargetX += 2; secondTargetY += 2;
                    break;
                case 4: 
                    targetY++;
                    secondTargetY += 2;
                    break;
                case 5: 
                    targetX--; targetY++;
                    secondTargetX -= 2; secondTargetY += 2;
                    break;
                case 6: 
                    targetX--;
                    secondTargetX -= 2;
                    break;
                case 7: 
                    targetX--; targetY--;
                    secondTargetX -= 2; secondTargetY -= 2;
                    break;
            }

            
            bool hitFirst = AttackTargetAt(targetX, targetY);

            
            bool hitSecond = AttackTargetAt(secondTargetX, secondTargetY);

            
            if (hitFirst || hitSecond)
            {
                skill.Use();
                TrainMagic(skill);
                SaySystem("刺杀剑术！");
                return true;
            }

            SaySystem("没有可攻击的目标");
            return false;
        }

        
        
        
        private bool ExecuteHalfMoon(byte direction)
        {
            
            if (!SkillBook.HasSkill(25))
            {
                SaySystem("未学习半月弯刀");
                return false;
            }

            
            var skill = SkillBook.GetSkill(25);
            if (skill == null || !skill.CanUse())
            {
                SaySystem("半月弯刀技能不可用");
                return false;
            }

            
            bool hitAny = false;

            
            for (int i = 0; i < 8; i++)
            {
                int targetX = X;
                int targetY = Y;

                switch (i)
                {
                    case 0: targetY--; break; 
                    case 1: targetX++; targetY--; break; 
                    case 2: targetX++; break; 
                    case 3: targetX++; targetY++; break; 
                    case 4: targetY++; break; 
                    case 5: targetX--; targetY++; break; 
                    case 6: targetX--; break; 
                    case 7: targetX--; targetY--; break; 
                }

                if (AttackTargetAt(targetX, targetY))
                {
                    hitAny = true;
                }
            }

            
            if (hitAny)
            {
                skill.Use();
                TrainMagic(skill);
                SaySystem("半月弯刀！");
                return true;
            }

            SaySystem("没有可攻击的目标");
            return false;
        }

        
        
        
        private bool ExecuteFireSword(byte direction)
        {
            
            if (!SkillBook.HasSkill(26))
            {
                SaySystem("未学习烈火剑法");
                return false;
            }

            
            var skill = SkillBook.GetSkill(26);
            if (skill == null || !skill.CanUse())
            {
                SaySystem("烈火剑法技能不可用");
                return false;
            }

            
            int targetX = X;
            int targetY = Y;

            switch (direction)
            {
                case 0: targetY--; break; 
                case 1: targetX++; targetY--; break; 
                case 2: targetX++; break; 
                case 3: targetX++; targetY++; break; 
                case 4: targetY++; break; 
                case 5: targetX--; targetY++; break; 
                case 6: targetX--; break; 
                case 7: targetX--; targetY--; break; 
            }

            
            bool hit = AttackTargetAt(targetX, targetY);

            
            if (hit)
            {
                skill.Use();
                TrainMagic(skill);
                SaySystem("烈火剑法！");

                
                
                return true;
            }

            SaySystem("没有可攻击的目标");
            return false;
        }

        
        
        
        private bool ExecuteRush(byte direction)
        {
            
            if (!SkillBook.HasSkill(1005)) 
            {
                SaySystem("未学习野蛮冲撞");
                return false;
            }

            
            var skill = SkillBook.GetSkill(1005);
            if (skill == null || !skill.CanUse())
            {
                SaySystem("野蛮冲撞技能不可用");
                return false;
            }

            
            int targetX = X;
            int targetY = Y;

            
            int maxDistance = 3; 
            bool hitTarget = false;

            for (int i = 1; i <= maxDistance; i++)
            {
                
                int currentX = X;
                int currentY = Y;

                switch (direction)
                {
                    case 0: currentY -= i; break; 
                    case 1: currentX += i; currentY -= i; break; 
                    case 2: currentX += i; break; 
                    case 3: currentX += i; currentY += i; break; 
                    case 4: currentY += i; break; 
                    case 5: currentX -= i; currentY += i; break; 
                    case 6: currentX -= i; break; 
                    case 7: currentX -= i; currentY -= i; break; 
                }

                
                if (CurrentMap == null || !CurrentMap.CanMoveTo(currentX, currentY))
                {
                    
                    break;
                }

                
                var target = CurrentMap.GetObjectAt(currentX, currentY) as ICombatEntity;
                if (target != null && CanAttackTarget(target))
                {
                    
                    PerformAttack(target);
                    hitTarget = true;

                    
                    break;
                }

                
                X = (ushort)currentX;
                Y = (ushort)currentY;
                targetX = currentX;
                targetY = currentY;
            }

            
            if (hitTarget)
            {
                skill.Use();
                TrainMagic(skill);
                SaySystem("野蛮冲撞！");
                return true;
            }

            
            if (targetX != X || targetY != Y)
            {
                X = (ushort)targetX;
                Y = (ushort)targetY;
                skill.Use();
                TrainMagic(skill);
                SaySystem("野蛮冲撞！");
                return true;
            }

            SaySystem("无法冲撞");
            return false;
        }

        
        
        
        private bool AttackTargetAt(int x, int y)
        {
            if (CurrentMap == null)
                return false;

            var target = CurrentMap.GetObjectAt(x, y) as ICombatEntity;
            if (target == null)
                return false;

            if (!CanAttackTarget(target))
                return false;

            PerformAttack(target);
            return true;
        }

        #endregion

        #region 状态检查方法

        
        
        
        public bool IsInCombat()
        {
            
            return false;
        }

        
        
        
        public bool IsInPrivateShop()
        {
            
            return false;
        }

        
        
        
        public void RecalcTotalStats()
        {
            
            
            
            
            
            

            if (!_dbBaseStatsLoaded)
            {
                
                _dbBaseStats.MinDC = Stats.MinDC;
                _dbBaseStats.MaxDC = Stats.MaxDC;
                _dbBaseStats.MinMC = Stats.MinMC;
                _dbBaseStats.MaxMC = Stats.MaxMC;
                _dbBaseStats.MinSC = Stats.MinSC;
                _dbBaseStats.MaxSC = Stats.MaxSC;
                _dbBaseStats.MinAC = Stats.MinAC;
                _dbBaseStats.MaxAC = Stats.MaxAC;
                _dbBaseStats.MinMAC = Stats.MinMAC;
                _dbBaseStats.MaxMAC = Stats.MaxMAC;
                _dbBaseStats.Accuracy = Stats.Accuracy;
                _dbBaseStats.Agility = Stats.Agility;
                _dbBaseStats.Lucky = Stats.Lucky;
                _dbBaseMaxHP = MaxHP;
                _dbBaseMaxMP = MaxMP;
                _dbBaseStatsLoaded = true;
            }

            CombatStats newEquip = Equipment?.GetTotalStats() ?? new CombatStats();

            
            int deltaMinDC = Stats.MinDC - (_dbBaseStats.MinDC + _equipStatsCache.MinDC);
            int deltaMaxDC = Stats.MaxDC - (_dbBaseStats.MaxDC + _equipStatsCache.MaxDC);
            int deltaMinMC = Stats.MinMC - (_dbBaseStats.MinMC + _equipStatsCache.MinMC);
            int deltaMaxMC = Stats.MaxMC - (_dbBaseStats.MaxMC + _equipStatsCache.MaxMC);
            int deltaMinSC = Stats.MinSC - (_dbBaseStats.MinSC + _equipStatsCache.MinSC);
            int deltaMaxSC = Stats.MaxSC - (_dbBaseStats.MaxSC + _equipStatsCache.MaxSC);
            int deltaMinAC = Stats.MinAC - (_dbBaseStats.MinAC + _equipStatsCache.MinAC);
            int deltaMaxAC = Stats.MaxAC - (_dbBaseStats.MaxAC + _equipStatsCache.MaxAC);
            int deltaMinMAC = Stats.MinMAC - (_dbBaseStats.MinMAC + _equipStatsCache.MinMAC);
            int deltaMaxMAC = Stats.MaxMAC - (_dbBaseStats.MaxMAC + _equipStatsCache.MaxMAC);
            int deltaAcc = Stats.Accuracy - (_dbBaseStats.Accuracy + _equipStatsCache.Accuracy);
            int deltaAgi = Stats.Agility - (_dbBaseStats.Agility + _equipStatsCache.Agility);
            int deltaLucky = Stats.Lucky - (_dbBaseStats.Lucky + _equipStatsCache.Lucky);

            int deltaMaxHp = MaxHP - (_dbBaseMaxHP + _equipStatsCache.MaxHP);
            int deltaMaxMp = MaxMP - (_dbBaseMaxMP + _equipStatsCache.MaxMP);

            
            Stats.MinDC = _dbBaseStats.MinDC + newEquip.MinDC + deltaMinDC;
            Stats.MaxDC = _dbBaseStats.MaxDC + newEquip.MaxDC + deltaMaxDC;
            Stats.MinMC = _dbBaseStats.MinMC + newEquip.MinMC + deltaMinMC;
            Stats.MaxMC = _dbBaseStats.MaxMC + newEquip.MaxMC + deltaMaxMC;
            Stats.MinSC = _dbBaseStats.MinSC + newEquip.MinSC + deltaMinSC;
            Stats.MaxSC = _dbBaseStats.MaxSC + newEquip.MaxSC + deltaMaxSC;
            Stats.MinAC = _dbBaseStats.MinAC + newEquip.MinAC + deltaMinAC;
            Stats.MaxAC = _dbBaseStats.MaxAC + newEquip.MaxAC + deltaMaxAC;
            Stats.MinMAC = _dbBaseStats.MinMAC + newEquip.MinMAC + deltaMinMAC;
            Stats.MaxMAC = _dbBaseStats.MaxMAC + newEquip.MaxMAC + deltaMaxMAC;
            Stats.Accuracy = _dbBaseStats.Accuracy + newEquip.Accuracy + deltaAcc;
            Stats.Agility = _dbBaseStats.Agility + newEquip.Agility + deltaAgi;
            Stats.Lucky = _dbBaseStats.Lucky + newEquip.Lucky + deltaLucky;

            MaxHP = _dbBaseMaxHP + newEquip.MaxHP + deltaMaxHp;
            MaxMP = _dbBaseMaxMP + newEquip.MaxMP + deltaMaxMp;

            
            if (Stats.MinDC < 0) Stats.MinDC = 0;
            if (Stats.MaxDC < Stats.MinDC) Stats.MaxDC = Stats.MinDC;
            if (Stats.MinMC < 0) Stats.MinMC = 0;
            if (Stats.MaxMC < Stats.MinMC) Stats.MaxMC = Stats.MinMC;
            if (Stats.MinSC < 0) Stats.MinSC = 0;
            if (Stats.MaxSC < Stats.MinSC) Stats.MaxSC = Stats.MinSC;
            if (Stats.MinAC < 0) Stats.MinAC = 0;
            if (Stats.MaxAC < Stats.MinAC) Stats.MaxAC = Stats.MinAC;
            if (Stats.MinMAC < 0) Stats.MinMAC = 0;
            if (Stats.MaxMAC < Stats.MinMAC) Stats.MaxMAC = Stats.MinMAC;
            if (Stats.Accuracy < 0) Stats.Accuracy = 0;
            if (Stats.Agility < 0) Stats.Agility = 0;
            

            MaxHP = Math.Max(1, MaxHP);
            MaxMP = Math.Max(1, MaxMP);
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            if (CurrentHP < 0) CurrentHP = 0;
            if (CurrentMP > MaxMP) CurrentMP = MaxMP;
            if (CurrentMP < 0) CurrentMP = 0;

            
            Stats.MaxHP = MaxHP;
            Stats.HP = CurrentHP;
            Stats.MaxMP = MaxMP;
            Stats.MP = CurrentMP;

            
            _equipStatsCache.MaxHP = newEquip.MaxHP;
            _equipStatsCache.MaxMP = newEquip.MaxMP;
            _equipStatsCache.MinDC = newEquip.MinDC;
            _equipStatsCache.MaxDC = newEquip.MaxDC;
            _equipStatsCache.MinMC = newEquip.MinMC;
            _equipStatsCache.MaxMC = newEquip.MaxMC;
            _equipStatsCache.MinSC = newEquip.MinSC;
            _equipStatsCache.MaxSC = newEquip.MaxSC;
            _equipStatsCache.MinAC = newEquip.MinAC;
            _equipStatsCache.MaxAC = newEquip.MaxAC;
            _equipStatsCache.MinMAC = newEquip.MinMAC;
            _equipStatsCache.MaxMAC = newEquip.MaxMAC;
            _equipStatsCache.Accuracy = newEquip.Accuracy;
            _equipStatsCache.Agility = newEquip.Agility;
            _equipStatsCache.Lucky = newEquip.Lucky;
        }

        
        
        
        public bool AddItem(ItemInstance item)
        {
            return Inventory.AddItem(item);
        }

        #endregion

        #region 宠物仓库方法

        
        
        
        public void PutItemToPetBag(uint itemId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 放入宠物仓库物品: {itemId}");
                
                
                var item = Inventory.GetItem((int)itemId);
                if (item == null)
                {
                    SaySystem("物品不存在");
                    return;
                }
                
                
                var petBag = PetSystem.GetPetBag();
                if (petBag.GetUsedSlots() >= petBag.MaxSlots)
                {
                    SaySystem("宠物背包已满");
                    return;
                }
                
                
                if (!Inventory.RemoveItem((int)itemId, 1))
                {
                    SaySystem("移除物品失败");
                    return;
                }
                
                
                if (!petBag.AddItem(item))
                {
                    
                    Inventory.AddItem(item);
                    SaySystem("放入宠物背包失败");
                    return;
                }
                
                
                SendInventoryUpdate();
                
                
                SendPetBagUpdate();
                
                SaySystem($"已将 {item.Definition.Name} 放入宠物背包");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"放入宠物仓库失败: {ex.Message}");
                SaySystem("放入宠物仓库失败");
            }
        }

        
        
        
        public void GetItemFromPetBag(uint itemId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 从宠物仓库取出物品: {itemId}");
                
                
                var petBag = PetSystem.GetPetBag();
                var item = petBag.GetItem((int)itemId);
                if (item == null)
                {
                    SaySystem("物品不存在");
                    return;
                }
                
                
                if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
                {
                    SaySystem("背包已满");
                    return;
                }
                
                
                if (!petBag.RemoveItem((int)itemId, 1))
                {
                    SaySystem("移除物品失败");
                    return;
                }
                
                
                if (!Inventory.AddItem(item))
                {
                    
                    petBag.AddItem(item);
                    SaySystem("放入背包失败");
                    return;
                }
                
                
                SendPetBagUpdate();
                
                
                SendInventoryUpdate();
                
                SaySystem($"已将 {item.Definition.Name} 从宠物背包取出");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"从宠物仓库取出失败: {ex.Message}");
                SaySystem("从宠物仓库取出失败");
            }
        }

        #endregion

        #region 任务系统方法

        
        
        
        public void DeleteTask(uint taskId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 删除任务: {taskId}");
                
                
                bool success = QuestManager.DeleteTask((int)taskId);
                
                if (success)
                {
                    SaySystem($"已删除任务: {taskId}");
                    
                    
                    SendTaskDeleted(taskId);
                }
                else
                {
                    SaySystem("删除任务失败");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"删除任务失败: {ex.Message}");
                SaySystem("删除任务失败");
            }
        }

        
        
        
        private void SendTaskDeleted(uint taskId)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x296); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(taskId);
            
            SendMessage(builder.Build());
        }

        #endregion

        #region 好友系统方法

        
        
        
        public void DeleteFriend(string friendName)
        {
            try
            {
                LogManager.Default.Info($"{Name} 删除好友: {friendName}");
                
                
                
                SaySystem($"已删除好友: {friendName}");
                
                
                SendFriendDeleted(friendName);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"删除好友失败: {ex.Message}");
                SaySystem("删除好友失败");
            }
        }

        
        
        
        public void ReplyAddFriendRequest(uint requestId, string replyData)
        {
            try
            {
                LogManager.Default.Info($"{Name} 回复添加好友请求: {requestId}, {replyData}");
                
                
                bool accept = replyData.Contains("accept", StringComparison.OrdinalIgnoreCase);
                
                
                
                if (accept)
                {
                    SaySystem("已接受好友请求");
                }
                else
                {
                    SaySystem("已拒绝好友请求");
                }
                
                
                SendFriendRequestReply(requestId, accept);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"回复添加好友请求失败: {ex.Message}");
                SaySystem("回复添加好友请求失败");
            }
        }

        
        
        
        private void SendFriendDeleted(string friendName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x297); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(friendName);
            
            SendMessage(builder.Build());
        }

        
        
        
        private void SendFriendRequestReply(uint requestId, bool accept)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x298); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(requestId);
            builder.WriteByte(accept ? (byte)1 : (byte)0);
            
            SendMessage(builder.Build());
        }

        
        
        
        public void SendFriendSystemError(byte error, string friendName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x299); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(error);
            builder.WriteString(friendName);
            
            SendMessage(builder.Build());
        }

        
        
        
        public void PostAddFriendRequest(HumanPlayer requester)
        {
            try
            {
                LogManager.Default.Info($"{Name} 收到来自 {requester.Name} 的好友请求");
                
                
                SendFriendRequest(requester);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送好友请求失败: {ex.Message}");
            }
        }

        
        
        
        private void SendFriendRequest(HumanPlayer requester)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29A); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(requester.ObjectId);
            builder.WriteString(requester.Name);
            
            SendMessage(builder.Build());
        }

        #endregion

        #region 行会系统方法

        
        
        
        public void ReplyAddToGuildRequest(bool accept)
        {
            try
            {
                LogManager.Default.Info($"{Name} 回复加入行会请求: {(accept ? "接受" : "拒绝")}");
                
                
                
                if (accept)
                {
                    SaySystem("已接受加入行会请求");
                }
                else
                {
                    SaySystem("已拒绝加入行会请求");
                }
                
                
                SendGuildRequestReply(accept);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"回复加入行会请求失败: {ex.Message}");
                SaySystem("回复加入行会请求失败");
            }
        }

        
        
        
        public void PostAddToGuildRequest(HumanPlayer inviter)
        {
            try
            {
                LogManager.Default.Info($"{Name} 收到来自 {inviter.Name} 的加入行会请求");
                
                
                SendGuildInvite(inviter);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送加入行会请求失败: {ex.Message}");
            }
        }

        
        
        
        private void SendGuildRequestReply(bool accept)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29B); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(accept ? (byte)1 : (byte)0);
            
            SendMessage(builder.Build());
        }

        
        
        
        private void SendGuildInvite(HumanPlayer inviter)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29C); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(inviter.ObjectId);
            builder.WriteString(inviter.Name);
            builder.WriteString(inviter.Guild?.Name ?? "");
            
            SendMessage(builder.Build());
        }

        #endregion

        #region 仓库系统方法

        
        
        
        public bool TakeBankItem(uint itemId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 从仓库取出物品: {itemId}");
                
                
                
                
                
                if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
                {
                    SaySystem("背包已满");
                    return false;
                }
                
                
                SaySystem("已从仓库取出物品");
                
                
                SendInventoryUpdate();
                
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"从仓库取出物品失败: {ex.Message}");
                SaySystem("从仓库取出物品失败");
                return false;
            }
        }

        
        
        
        public bool PutBankItem(uint itemId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 放入仓库物品: {itemId}");
                
                
                
                
                
                
                
                
                SaySystem("已放入仓库物品");
                
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"放入仓库物品失败: {ex.Message}");
                SaySystem("放入仓库物品失败");
                return false;
            }
        }

        #endregion

        #region 技能快捷键方法

        
        
        
        public void SetMagicKey(uint skillId, ushort key1, ushort key2)
        {
            try
            {
                LogManager.Default.Info($"{Name} 设置技能快捷键: 技能ID={skillId}, 快捷键={key1},{key2}");
                
                
                var skill = SkillBook.GetSkill((int)skillId);
                if (skill == null)
                {
                    SaySystem("技能不存在");
                    return;
                }
                
                
                
                
                
                
                SendMagicKeyUpdated(skillId, key1, key2);
                
                SaySystem("已设置技能快捷键");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"设置技能快捷键失败: {ex.Message}");
                SaySystem("设置技能快捷键失败");
            }
        }

        
        
        
        private void SendMagicKeyUpdated(uint skillId, ushort key1, ushort key2)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29D); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(skillId);
            builder.WriteUInt16(key1);
            builder.WriteUInt16(key2);
            
            SendMessage(builder.Build());
        }

        #endregion

        #region 其他方法

        
        
        
        public bool CutBody(uint corpseId, ushort param1, ushort param2, ushort param3)
        {
            try
            {
                LogManager.Default.Info($"{Name} 切割尸体: 尸体ID={corpseId}, 参数={param1},{param2},{param3}");
                
                
                var corpse = CurrentMap?.GetObject(corpseId) as MonsterCorpse;
                if (corpse == null)
                {
                    SaySystem("尸体不存在");
                    return false;
                }
                
                
                if (!IsInRange(corpse, 1))
                {
                    SaySystem("距离太远");
                    return false;
                }
                
                
                StartAction(ActionType.GetMeat, corpseId);
                
                
                _lastGetMeatTime = DateTime.Now;
                
                
                SendCutBodyEffect(corpseId);
                
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"切割尸体失败: {ex.Message}");
                SaySystem("切割尸体失败");
                return false;
            }
        }

        
        
        
        private void SendCutBodyEffect(uint corpseId)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29E); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(corpseId);
            
            SendMessage(builder.Build());
        }

        
        
        
        public void OnPutItem(uint itemId, uint param)
        {
            try
            {
                LogManager.Default.Info($"{Name} 放入物品: 物品ID={itemId}, 参数={param}");
                
                
                
                SaySystem("已放入物品");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"放入物品失败: {ex.Message}");
                SaySystem("放入物品失败");
            }
        }

        
        
        
        public void ShowPetInfo()
        {
            try
            {
                LogManager.Default.Info($"{Name} 显示宠物信息");
                
                
                var petInfo = PetSystem.GetPetInfo();
                
                
                SendPetInfo(petInfo);
                
                SaySystem("已显示宠物信息");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"显示宠物信息失败: {ex.Message}");
                SaySystem("显示宠物信息失败");
            }
        }

        
        
        
        private void SendPetInfo(object petInfo)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29F); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            
            
            builder.WriteString("宠物信息");
            
            SendMessage(builder.Build());
        }

        #endregion

        
        
        
        
        public void LoadVars()
        {
            
        }

        
        
        
        
        public bool CreateBagItem(string itemName, bool silence = false)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return false;

            
            
            if (!GetSystemFlag((int)MirCommon.SystemFlag.SF_BAGLOADED))
            {
                if (!silence)
                    SaySystem("背包数据加载中，请稍后再试");
                return false;
            }

            var def = ItemManager.Instance.GetDefinitionByName(itemName.Trim());
            if (def == null)
                return false;

            var instance = ItemManager.Instance.CreateItem(def.ItemId, 1);
            if (instance == null)
                return false;

            
            if (!Inventory.TryAddItemNoStack(instance, out _))
                return false;

            if (!silence)
            {
                SendAddBagItem(instance);
                SendWeightChanged();
            }

            return true;
        }

        
        
        
        public void SendAddBagItem(ItemInstance item)
        {
            try
            {
                if (item == null)
                    return;

                var itemClient = ItemPacketBuilder.BuildItemClient(item);

                byte[] payload = StructToBytes(itemClient);
                SendMsg(ObjectId, MirCommon.ProtocolCmd.SM_ADDBAGITEM, 0, 0, 1, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送新增背包物品失败: player={Name}, err={ex.Message}");
            }
        }

        
        
        
        public void SendUpdateItem(ItemInstance item)
        {
            try
            {
                if (item == null)
                    return;

                var itemClient = ItemPacketBuilder.BuildItemClient(item);

                byte[] payload = StructToBytes(itemClient);
                SendMsg(ObjectId, MirCommon.ProtocolCmd.SM_UPDATEITEM, 0, 0, 1, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送更新背包物品失败: player={Name}, err={ex.Message}");
            }
        }

        
        
        
        public void SetBagItemPos(MirCommon.BAGITEMPOS[] itempos, int count)
        {
            
        }

        #region 被动/自动技能（对齐C++）

        
        
        
        public void PostMsg(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (_stream == null || _tcpClient == null || !_tcpClient.Connected)
                return;

            try
            {
                byte[] bytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(message);
                lock (_stream)
                {
                    _stream.Write(bytes, 0, bytes.Length);
                    _stream.Flush();
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Warning($"PostMsg发送失败: player={Name}, msg={message}, err={ex.Message}");
            }
        }

        private static int CalcAutoAddPower(int skillLevel)
        {
            int level = Math.Clamp(skillLevel, 0, 7);
            int span = Math.Max(1, 7 - level);
            return span - Random.Shared.Next(span); 
        }

        
        
        
        public void RecalcHitSpeed()
        {
            int bonus = 0;

            
            var s3 = SkillBook.GetSkill(3);
            if (s3 != null && s3.Level > 0)
                bonus += (8 * s3.Level) / 3;

            
            var s4 = SkillBook.GetSkill(4);
            if (s4 != null && s4.Level > 0)
                bonus += (9 * s4.Level) / 3;

            
            var s7 = SkillBook.GetSkill(7);
            if (s7 != null)
            {
                s7.Activated = true;
                if (s7.Level > 0)
                    bonus += (3 * s7.Level) / 3;
            }

            
            var s74 = SkillBook.GetSkill(74);
            if (s74 != null && s74.Level > 0)
                bonus += s74.Level;

            int delta = bonus - _hitPointBonus;
            if (delta != 0)
            {
                Stats.Accuracy += delta;
                Accuracy += delta;
                if (Stats.Accuracy < 0) Stats.Accuracy = 0;
                if (Accuracy < 0) Accuracy = 0;
            }

            _hitPointBonus = bonus;
        }

        
        
        
        private void UpdateAutoMagic()
        {
            

            if (MagicManager.Instance.GetMagicCount() == 0)
            {
                MagicManager.Instance.LoadAll();
            }

            foreach (var skill in SkillBook.GetAllSkills())
            {
                var magicClass = MagicManager.Instance.GetClassById(skill.SkillId);
                if (magicClass == null)
                    continue;

                bool forced = (magicClass.dwFlag & (uint)MagicFlag.MAGICFLAG_FORCED) != 0;
                bool actived = (magicClass.dwFlag & (uint)MagicFlag.MAGICFLAG_ACTIVED) != 0;

                
                if (!forced || actived)
                    continue;

                if (skill.AutoAddPower <= 0)
                    skill.AutoAddPower = CalcAutoAddPower(skill.Level);

                skill.AutoAddPower--;
                if (skill.AutoAddPower > 0)
                    continue;

                skill.AutoAddPower = CalcAutoAddPower(skill.Level);

                TrainMagic(skill);

                
                switch (skill.SkillId)
                {
                    case 7:  
                        skill.Activated = true;
                        PostMsg("#+PWR!");
                        break;
                    case 40: 
                        skill.Activated = true;
                        PostMsg("#+VIS!");
                        break;
                    case 41: 
                        skill.Activated = true;
                        PostMsg("#+SHAD!");
                        break;
                }
            }
        }

        #endregion
    }
}
