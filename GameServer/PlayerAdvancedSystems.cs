using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Network;

namespace GameServer
{
    
    
    
    public class PetSystem
    {
        private readonly HumanPlayer _owner;
        private readonly List<Monster> _pets = new();
        private Monster? _mainPet;
        private readonly object _petLock = new();
        
        
        private readonly Inventory _petBag = new() { MaxSlots = 10 };
        
        public PetSystem(HumanPlayer owner)
        {
            _owner = owner;
        }
        
        
        
        
        public int GetPetCount()
        {
            lock (_petLock)
            {
                return _pets.Count;
            }
        }
        
        
        
        
        public int MaxPets => 5; 
        
        
        
        
        public bool SummonPet(string petName, bool setOwner = true, int x = -1, int y = -1)
        {
            if (_pets.Count >= 5) 
            {
                _owner.Say("宠物数量已达上限");
                return false;
            }
            
            
            var pet = new Monster(0, petName) 
            {
                OwnerPlayerId = setOwner ? _owner.ObjectId : 0,
                IsPet = true
            };
            
            
            if (x == -1 || y == -1)
            {
                x = _owner.X;
                y = _owner.Y;
            }
            
            
            if (_owner.CurrentMap != null)
            {
                _owner.CurrentMap.AddObject(pet, (ushort)x, (ushort)y);
            }
            
            lock (_petLock)
            {
                _pets.Add(pet);
                if (_mainPet == null)
                {
                    _mainPet = pet;
                }
            }
            
            _owner.Say($"召唤了 {petName}");
            return true;
        }
        
        
        
        
        public bool ReleasePet(string petName)
        {
            lock (_petLock)
            {
                var pet = _pets.FirstOrDefault(p => p.Name == petName);
                if (pet == null)
                {
                    _owner.Say($"没有找到宠物 {petName}");
                    return false;
                }
                
                
                pet.CurrentMap?.RemoveObject(pet);
                _pets.Remove(pet);
                
                if (_mainPet == pet)
                {
                    _mainPet = _pets.FirstOrDefault();
                }
                
                _owner.Say($"释放了 {petName}");
                return true;
            }
        }
        
        
        
        
        public void SetPetTarget(AliveObject target)
        {
            lock (_petLock)
            {
                foreach (var pet in _pets)
                {
                    pet.SetTarget(target);
                }
            }
        }
        
        
        
        
        public void CleanPets()
        {
            lock (_petLock)
            {
                foreach (var pet in _pets)
                {
                    pet.CurrentMap?.RemoveObject(pet);
                }
                _pets.Clear();
                _mainPet = null;
            }
        }
        
        
        
        
        public Inventory GetPetBag() => _petBag;
        
        
        
        
        public bool SetPetBagSize(int size)
        {
            if (size != 5 && size != 10 && size != 0)
                return false;
                
            _petBag.MaxSlots = size;
            SendPetBagInfo();
            return true;
        }
        
        
        
        
        public bool GetItemFromPetBag(ulong makeIndex)
        {
            var item = _petBag.FindItem(makeIndex);
            if (item == null)
                return false;
                
            if (!_owner.Inventory.AddItem(item))
            {
                _owner.Say("背包已满");
                return false;
            }
            
            _petBag.RemoveItem(makeIndex, 1);
            SendPetBagInfo();
            return true;
        }
        
        
        
        
        public bool PutItemToPetBag(ulong makeIndex)
        {
            var item = _owner.Inventory.FindItem(makeIndex);
            if (item == null)
                return false;
                
            if (!_petBag.AddItem(item))
            {
                _owner.Say("宠物背包已满");
                return false;
            }
            
            _owner.Inventory.RemoveItem(makeIndex, 1);
            SendPetBagInfo();
            return true;
        }
        
        
        
        
        private void SendPetBagInfo()
        {
            
            
            
            
            
            SendSetPetBag((ushort)_petBag.MaxSlots);
            
            
            SendPetBag();
        }
        
        
        
        
        private void SendSetPetBag(ushort size)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x9602); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(size);
            
            _owner.SendMessage(builder.Build());
        }
        
        
        
        
        private void SendPetBag()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x9603); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            
            var items = _petBag.GetAllItems();
            builder.WriteUInt16((ushort)_petBag.MaxSlots);
            builder.WriteUInt16((ushort)items.Count);
            
            
            foreach (var item in items.Values)
            {
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteUInt16((ushort)item.Definition.ItemId);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteUInt32(item.Definition.SellPrice);
                builder.WriteByte(0); 
                builder.WriteByte(0); 
                builder.WriteByte(0); 
                builder.WriteByte(0); 
            }
            
            _owner.SendMessage(builder.Build());
        }
        
        
        
        
        public void ShowPetInfo()
        {
            lock (_petLock)
            {
                _owner.Say($"宠物数量: {_pets.Count}");
                foreach (var pet in _pets)
                {
                    _owner.Say($"{pet.Name} - 等级: {pet.Level} HP: {pet.CurrentHP}/{pet.MaxHP}");
                }
            }
        }

        
        
        
        public object GetPetInfo()
        {
            lock (_petLock)
            {
                
                var petInfo = new
                {
                    PetCount = _pets.Count,
                    MainPet = _mainPet?.Name ?? "无",
                    Pets = _pets.Select(p => new
                    {
                        Name = p.Name,
                        Level = p.Level,
                        HP = $"{p.CurrentHP}/{p.MaxHP}",
                        IsMain = p == _mainPet
                    }).ToList(),
                    PetBagSize = _petBag.MaxSlots,
                    PetBagUsed = _petBag.GetUsedSlots()
                };
                
                return petInfo;
            }
        }
        
        
        
        
        public void DistributePetExp(uint exp)
        {
            lock (_petLock)
            {
                if (_pets.Count == 0)
                    return;
                    
                uint expPerPet = exp / (uint)_pets.Count;
                foreach (var pet in _pets)
                {
                    
                    
                    _owner.Say($"{pet.Name} 获得 {expPerPet} 经验");
                }
            }
        }
    }
    
    
    
    
    public class MountSystem
    {
        private readonly HumanPlayer _owner;
        private MonsterEx? _horse;
        private bool _isRiding;
        private bool _horseRest;
        
        public MountSystem(HumanPlayer owner)
        {
            _owner = owner;
        }
        
        
        
        
        public MonsterEx? GetHorse() => _horse;
        
        
        
        
        public void SetHorse(MonsterEx? horse)
        {
            _horse = horse;
            if (_horse == null)
            {
                _isRiding = false;
            }
        }
        
        
        
        
        public bool RideHorse()
        {
            if (_horse == null)
            {
                _owner.Say("你没有坐骑");
                return false;
            }
            
            if (_horse.CurrentHP <= 0)
            {
                _owner.Say("坐骑已死亡");
                return false;
            }
            
            _isRiding = true;
            _owner.Say("骑乘坐骑");
            _owner.NotifyAppearanceChanged();
            return true;
        }
        
        
        
        
        public void Dismount()
        {
            _isRiding = false;
            _owner.Say("下马");
            _owner.NotifyAppearanceChanged();
        }
        
        
        
        
        public bool IsRiding() => _isRiding;
        
        
        
        
        public byte GetRunSpeed()
        {
            if (_isRiding) return 3; 
            return 2; 
        }
        
        
        
        
        public bool IsEquipedHorse()
        {
            
            var horseItem = _owner.Equipment.GetItem(EquipSlot.Mount);
            return horseItem != null;
        }
        
        
        
        
        public ItemInstance? GetEquipedHorseItem()
        {
            return _owner.Equipment.GetItem(EquipSlot.Mount);
        }
        
        
        
        
        public bool TrainHorse(int dir)
        {
            if (_horse == null)
            {
                _owner.Say("你没有坐骑");
                return false;
            }
            
            
            if (!_owner.CanDoAction(ActionType.Attack))
            {
                _owner.Say("当前不能执行动作");
                return false;
            }
            
            
            var weapon = _owner.Equipment.GetItem(EquipSlot.Weapon);
            if (weapon == null || weapon.Definition.Type != ItemType.Weapon) 
            {
                _owner.Say("需要装备马鞭才能训练坐骑");
                return false;
            }
            
            
            int targetX = _owner.X;
            int targetY = _owner.Y;
            
            switch (dir)
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
            
            
            if (_owner.CurrentMap == null)
                return false;
                
            var horse = _owner.CurrentMap.GetObjectAt(targetX, targetY) as MonsterEx;
            if (horse == null)
            {
                _owner.Say("目标位置没有马匹");
                return false;
            }
            
            
            
            
            var desc = horse.GetDesc();
            if (desc == null)
            {
                _owner.Say("这匹马不能训练");
                return false;
            }
            
            
            
            if (!desc.Base.ViewName.Contains("马"))
            {
                _owner.Say("这不是骑乘类型的马匹");
                return false;
            }
            
            
            _owner.Say("训练成功！");
            
            
            SetHorse(horse);
            
            
            SendTrainHorseSuccess();
            return true;
        }
        
        
        
        
        private void SendTrainHorseSuccess()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x28F); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            _owner.SendMessage(builder.Build());
        }
        
        
        
        
        public void ToggleHorseRest()
        {
            _horseRest = !_horseRest;
            _owner.Say(_horseRest ? "坐骑休息" : "坐骑工作");
        }
        
        
        
        
        public bool IsHorseRest() => _horseRest;
    }
    
    
    
    
    public class PKSystem
    {
        private readonly HumanPlayer _owner;
        private uint _pkValue;
        private DateTime _lastPkTime;
        private bool _justPk;
        private bool _isSelfDefense; 
        private DateTime _lastSelfDefenseTime;
        
        
        private const uint PK_VALUE_PURPLE = 10;  
        private const uint PK_VALUE_ORANGE = 50;  
        private const uint PK_VALUE_RED = 100;    
        
        
        private const int PK_DECAY_MINUTES = 5;
        
        public PKSystem(HumanPlayer owner)
        {
            _owner = owner;
            _pkValue = 0;
            _lastPkTime = DateTime.MinValue;
            _justPk = false;
            _isSelfDefense = false;
            _lastSelfDefenseTime = DateTime.MinValue;
        }
        
        
        
        
        public uint GetPkValue() => _pkValue;
        
        
        
        
        public void SetPkValue(uint value)
        {
            _pkValue = value;
            UpdateNameColor();
        }
        
        
        
        
        public void AddPkPoint(uint points = 1, bool isSelfDefense = false)
        {
            
            if (isSelfDefense)
            {
                _isSelfDefense = true;
                _lastSelfDefenseTime = DateTime.Now;
                return;
            }
            
            _pkValue += points;
            _lastPkTime = DateTime.Now;
            _justPk = true;
            
            
            UpdateNameColor();
            
            
            CheckWeaponCurse();
            
            
            SendPkValueChanged();
            
            _owner.Say($"PK值增加 {points}，当前PK值: {_pkValue}");
        }
        
        
        
        
        public void DecPkPoint(uint points = 1)
        {
            if (_pkValue >= points)
            {
                _pkValue -= points;
            }
            else
            {
                _pkValue = 0;
            }
            
            UpdateNameColor();
            SendPkValueChanged();
        }
        
        
        
        
        public byte GetNameColor(MapObject? viewer = null)
        {
            
            
            
            
            
            
            
            
            
            if (_pkValue >= PK_VALUE_RED) return 2; 
            if (_pkValue >= PK_VALUE_ORANGE) return 6; 
            if (_pkValue >= PK_VALUE_PURPLE) return 5; 
            
            
            if (_owner.GroupId != 0 && viewer is HumanPlayer viewerPlayer && viewerPlayer.GroupId == _owner.GroupId)
                return 1; 
                
            
            if (_owner.Guild != null && viewer is HumanPlayer viewerPlayer2 && viewerPlayer2.Guild == _owner.Guild)
                return 4; 
                
            return 0; 
        }
        
        
        
        
        public bool CheckPk(AliveObject target)
        {
            if (target is HumanPlayer targetPlayer)
            {
                
                if (targetPlayer.PKSystem._justPk || targetPlayer.PKSystem._isSelfDefense)
                {
                    
                    AddPkPoint(1, true);
                    return true;
                }
                
                
                if (_owner.GroupId != 0 && _owner.GroupId == targetPlayer.GroupId)
                {
                    _owner.Say("不能攻击队友");
                    return false;
                }
                
                
                if (_owner.Guild != null && _owner.Guild == targetPlayer.Guild)
                {
                    _owner.Say("不能攻击同公会成员");
                    return false;
                }
                
                
                AddPkPoint();
                return true;
            }
            
            return false;
        }
        
        
        
        
        private void CheckWeaponCurse()
        {
            
            if (_pkValue >= PK_VALUE_RED)
            {
                
                var weapon = _owner.Equipment.GetItem(EquipSlot.Weapon);
                if (weapon != null)
                {
                    
                    int curseProbability = 30; 
                    
                    
                    if (_pkValue >= PK_VALUE_RED * 2)
                        curseProbability = 50;
                    else if (_pkValue >= PK_VALUE_RED * 3)
                        curseProbability = 70;
                    
                    if (Random.Shared.Next(100) < curseProbability)
                    {
                        
                        CurseWeapon(weapon);
                    }
                }
            }
        }
        
        
        
        
        private void CurseWeapon(ItemInstance weapon)
        {
            if (weapon == null)
                return;
                
            
            
            
            
            int curseValue = weapon.ExtraStats.GetValueOrDefault("Curse", 0);
            curseValue++;
            weapon.ExtraStats["Curse"] = curseValue;
            
            
            int luckyValue = weapon.Definition.Lucky;
            if (luckyValue > 0)
                weapon.Definition.Lucky = luckyValue - 1;
            
            
            _owner.Say("你的武器被诅咒了！");
            
            
            SendWeaponCursed(weapon);
            
            
            Console.WriteLine($"{_owner.Name} 的武器被诅咒，当前诅咒值: {curseValue}");
        }
        
        
        
        
        private void SendWeaponCursed(ItemInstance weapon)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x290); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt64((ulong)weapon.InstanceId);
            builder.WriteUInt16((ushort)weapon.ExtraStats.GetValueOrDefault("Curse", 0));
            builder.WriteUInt16((ushort)weapon.Definition.Lucky);
            
            _owner.SendMessage(builder.Build());
        }
        
        
        
        
        public List<ItemInstance> GetDeathDropItems()
        {
            var dropItems = new List<ItemInstance>();
            
            
            if (_pkValue >= PK_VALUE_RED)
            {
                
                
                foreach (var slot in Enum.GetValues<EquipSlot>())
                {
                    var item = _owner.Equipment.GetItem(slot);
                    if (item != null && Random.Shared.Next(100) < 50) 
                    {
                        dropItems.Add(item);
                    }
                }
                
                
                var inventoryItems = _owner.Inventory.GetAllItems();
                foreach (var item in inventoryItems.Values)
                {
                    if (Random.Shared.Next(100) < 30) 
                    {
                        dropItems.Add(item);
                    }
                }
            }
            else if (_pkValue >= PK_VALUE_ORANGE)
            {
                
                var inventoryItems = _owner.Inventory.GetAllItems();
                int dropCount = Math.Min(3, inventoryItems.Count);
                for (int i = 0; i < dropCount; i++)
                {
                    if (inventoryItems.Count > 0)
                    {
                        var randomIndex = Random.Shared.Next(inventoryItems.Count);
                        dropItems.Add(inventoryItems.Values.ElementAt(randomIndex));
                    }
                }
            }
            else if (_pkValue >= PK_VALUE_PURPLE)
            {
                
                var inventoryItems = _owner.Inventory.GetAllItems();
                if (inventoryItems.Count > 0)
                {
                    var randomIndex = Random.Shared.Next(inventoryItems.Count);
                    dropItems.Add(inventoryItems.Values.ElementAt(randomIndex));
                }
            }
            
            return dropItems;
        }
        
        
        
        
        private void UpdateNameColor()
        {
            
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x285); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(GetNameColor());
            
            
            var packet = builder.Build();
            _owner.CurrentMap?.SendToNearbyPlayers(_owner.X, _owner.Y, packet);
        }
        
        
        
        
        private void SendPkValueChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x286); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(_pkValue);
            
            _owner.SendMessage(builder.Build());
        }
        
        
        
        
        public void SetJustPk(bool justPk = true)
        {
            _justPk = justPk;
        }
        
        
        
        
        public bool IsSelfDefense()
        {
            return _isSelfDefense && (DateTime.Now - _lastSelfDefenseTime).TotalMinutes < 5;
        }
        
        
        
        
        public void Update()
        {
            
            if (_pkValue > 0 && (DateTime.Now - _lastPkTime).TotalMinutes >= PK_DECAY_MINUTES)
            {
                DecPkPoint(1);
                _lastPkTime = DateTime.Now;
            }
            
            
            if (_justPk && (DateTime.Now - _lastPkTime).TotalSeconds >= 30)
            {
                _justPk = false;
            }
            
            
            if (_isSelfDefense && (DateTime.Now - _lastSelfDefenseTime).TotalMinutes >= 5)
            {
                _isSelfDefense = false;
            }
        }
        
        
        
        
        public string GetPkStatus()
        {
            if (_pkValue >= PK_VALUE_RED) return "红名（罪恶滔天）";
            if (_pkValue >= PK_VALUE_ORANGE) return "橙名（恶贯满盈）";
            if (_pkValue >= PK_VALUE_PURPLE) return "紫名（小有恶名）";
            return "白名（善良公民）";
        }
        
        
        
        
        public bool CanAttack(AliveObject target)
        {
            if (target is HumanPlayer targetPlayer)
            {
                
                if (_owner.GroupId != 0 && _owner.GroupId == targetPlayer.GroupId)
                    return false;
                    
                
                if (_owner.Guild != null && _owner.Guild == targetPlayer.Guild)
                    return false;
                    
                
                if (targetPlayer.Level < 10 && _owner.Level >= 10)
                {
                    _owner.Say("不能攻击新手玩家");
                    return false;
                }
                
                return true;
            }
            
            return true; 
        }
    }
    
    
    
    
    public class AchievementSystem
    {
        private readonly HumanPlayer _owner;
        private readonly Dictionary<uint, Achievement> _achievements = new();
        private readonly Dictionary<AchievementType, uint> _progress = new();
        
        public AchievementSystem(HumanPlayer owner)
        {
            _owner = owner;
            InitializeAchievements();
        }
        
        
        
        
        private void InitializeAchievements()
        {
            
            AddAchievement(new Achievement
            {
                Id = 1,
                Name = "初出茅庐",
                Description = "达到10级",
                Type = AchievementType.Level,
                TargetValue = 10,
                RewardExp = 1000,
                RewardGold = 1000
            });
            
            AddAchievement(new Achievement
            {
                Id = 2,
                Name = "小有所成",
                Description = "达到30级",
                Type = AchievementType.Level,
                TargetValue = 30,
                RewardExp = 5000,
                RewardGold = 5000
            });
            
            
            AddAchievement(new Achievement
            {
                Id = 101,
                Name = "怪物猎人",
                Description = "击杀100只怪物",
                Type = AchievementType.KillMonster,
                TargetValue = 100,
                RewardExp = 2000,
                RewardGold = 2000
            });
            
            
            AddAchievement(new Achievement
            {
                Id = 201,
                Name = "装备收集者",
                Description = "获得10件装备",
                Type = AchievementType.GetItem,
                TargetValue = 10,
                RewardExp = 1500,
                RewardGold = 1500
            });
        }
        
        
        
        
        private void AddAchievement(Achievement achievement)
        {
            _achievements[achievement.Id] = achievement;
        }
        
        
        
        
        public void UpdateProgress(AchievementType type, uint value = 1)
        {
            if (!_progress.ContainsKey(type))
            {
                _progress[type] = 0;
            }
            
            _progress[type] += value;
            CheckAchievements(type);
        }
        
        
        
        
        private void CheckAchievements(AchievementType type)
        {
            var currentValue = _progress.ContainsKey(type) ? _progress[type] : 0;
            
            foreach (var achievement in _achievements.Values)
            {
                if (achievement.Type == type && !achievement.Completed && currentValue >= achievement.TargetValue)
                {
                    CompleteAchievement(achievement.Id);
                }
            }
        }
        
        
        
        
        public bool CompleteAchievement(uint achievementId)
        {
            if (!_achievements.TryGetValue(achievementId, out var achievement) || achievement.Completed)
                return false;
            
            achievement.Completed = true;
            achievement.CompletedTime = DateTime.Now;
            
            
            _owner.AddExp(achievement.RewardExp);
            _owner.AddGold(achievement.RewardGold);
            
            _owner.Say($"成就达成: {achievement.Name} - {achievement.Description}");
            _owner.Say($"获得奖励: {achievement.RewardExp}经验, {achievement.RewardGold}金币");
            
            
            return true;
        }
        
        
        
        
        public List<Achievement> GetAchievements()
        {
            return _achievements.Values.ToList();
        }
        
        
        
        
        public uint GetProgress(AchievementType type)
        {
            return _progress.TryGetValue(type, out var value) ? value : 0;
        }
    }
    
    
    
    
    public class MailSystem
    {
        private readonly HumanPlayer _owner;
        private readonly List<Mail> _mails = new();
        private readonly object _mailLock = new();
        
        public MailSystem(HumanPlayer owner)
        {
            _owner = owner;
        }
        
        
        
        
        public bool SendMail(string receiverName, string title, string content, List<ItemInstance>? attachments = null)
        {
            if (string.IsNullOrEmpty(receiverName) || string.IsNullOrEmpty(title))
            {
                _owner.Say("收件人或标题不能为空");
                return false;
            }
            
            
            var receiver = HumanPlayerMgr.Instance.FindByName(receiverName);
            if (receiver == null)
            {
                _owner.Say($"玩家 {receiverName} 不存在或不在线");
                return false;
            }
            
            
            if (attachments != null && attachments.Count > 0)
            {
                
                if (attachments.Count > 5)
                {
                    _owner.Say("附件数量不能超过5个");
                    return false;
                }
                
                
                foreach (var attachment in attachments)
                {
                    if (!_owner.Inventory.HasItem((ulong)attachment.InstanceId))
                    {
                        _owner.Say("附件物品不属于你");
                        return false;
                    }
                }
            }
            
            
            var mail = new Mail
            {
                Id = GenerateMailId(),
                Sender = _owner.Name,
                Receiver = receiverName,
                Title = title,
                Content = content,
                SendTime = DateTime.Now,
                IsRead = false,
                Attachments = attachments,
                AttachmentsClaimed = false
            };
            
            
            if (!SaveMailToDatabase(mail))
            {
                _owner.Say("邮件发送失败，数据库错误");
                return false;
            }
            
            
            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    _owner.Inventory.RemoveItem((ulong)attachment.InstanceId, 1);
                }
            }
            
            
            receiver.MailSystem.ReceiveMail(mail);
            
            
            _owner.Say($"邮件已发送给 {receiverName}");
            
            
            Console.WriteLine($"{_owner.Name} 发送邮件给 {receiverName}，标题: {title}");
            
            return true;
        }
        
        
        
        
        private uint GenerateMailId()
        {
            
            return (uint)DateTime.Now.Ticks;
        }
        
        
        
        
        private bool SaveMailToDatabase(Mail mail)
        {
            
            
            return true;
        }
        
        
        
        
        public void ReceiveMail(Mail mail)
        {
            lock (_mailLock)
            {
                _mails.Add(mail);
            }
            
            
            _owner.Say("你有新邮件");
        }
        
        
        
        
        public List<Mail> GetMails()
        {
            lock (_mailLock)
            {
                return new List<Mail>(_mails);
            }
        }
        
        
        
        
        public Mail? ReadMail(uint mailId)
        {
            lock (_mailLock)
            {
                var mail = _mails.FirstOrDefault(m => m.Id == mailId);
                if (mail != null && !mail.IsRead)
                {
                    mail.IsRead = true;
                    mail.ReadTime = DateTime.Now;
                }
                return mail;
            }
        }
        
        
        
        
        public bool DeleteMail(uint mailId)
        {
            lock (_mailLock)
            {
                var mail = _mails.FirstOrDefault(m => m.Id == mailId);
                if (mail == null)
                    return false;
                    
                _mails.Remove(mail);
                return true;
            }
        }
        
        
        
        
        public bool ClaimAttachment(uint mailId)
        {
            lock (_mailLock)
            {
                var mail = _mails.FirstOrDefault(m => m.Id == mailId);
                if (mail == null || mail.Attachments == null || mail.Attachments.Count == 0)
                    return false;
                    
                if (mail.AttachmentsClaimed)
                {
                    _owner.Say("附件已领取");
                    return false;
                }
                
                
                foreach (var item in mail.Attachments)
                {
                    if (!_owner.Inventory.AddItem(item))
                    {
                        _owner.Say("背包空间不足");
                        return false;
                    }
                }
                
                mail.AttachmentsClaimed = true;
                mail.ClaimTime = DateTime.Now;
                _owner.Say("附件领取成功");
                return true;
            }
        }
    }
    
    
    
    
    public enum AchievementType
    {
        Level,
        KillMonster,
        GetItem,
        CompleteQuest,
        JoinGuild,
        PvPKill,
        UseSkill,
        CraftItem
    }
    
    
    
    
    public class Achievement
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AchievementType Type { get; set; }
        public uint TargetValue { get; set; }
        public uint RewardExp { get; set; }
        public uint RewardGold { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedTime { get; set; }
    }
    
    
    
    
    public class Mail
    {
        public uint Id { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SendTime { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadTime { get; set; }
        public List<ItemInstance>? Attachments { get; set; }
        public bool AttachmentsClaimed { get; set; }
        public DateTime? ClaimTime { get; set; }
    }
}
