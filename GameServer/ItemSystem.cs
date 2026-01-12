using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    public enum ItemType
    {
        Weapon = 0,         
        Armor = 1,          
        Helmet = 2,         
        Necklace = 3,       
        Ring = 4,           
        Bracelet = 5,       
        Belt = 6,           
        Boots = 7,          
        Potion = 8,         
        Scroll = 9,         
        Book = 10,          
        Material = 11,      
        Quest = 12,         
        Other = 99,          
        Food = 100,
        Charm = 101
    }

    
    
    
    public enum ItemQuality
    {
        Normal = 0,         
        Fine = 1,           
        Rare = 2,           
        Epic = 3,           
        Legendary = 4,      
        Mythic = 5          
    }

    
    
    
    public enum EquipSlot
    {
        
        Dress = 0,          
        Weapon = 1,         
        Charm = 2,          
        Necklace = 3,       
        Helmet = 4,         
        BraceletLeft = 5,   
        BraceletRight = 6,  
        RingLeft = 7,       
        RingRight = 8,      
        Shoes = 9,          
        Belt = 10,          
        Stone = 11,         
        Poison = 12,        
        Reserved = 13,      
        Max = 14,

        
        Armor = Dress,
        Boots = Shoes,
        Mount = Charm
    }

    
    
    
    public class ItemDefinition
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ItemType Type { get; set; }
        public ItemQuality Quality { get; set; }
        public int Level { get; set; }
        public int MaxStack { get; set; } = 1;
        public bool CanTrade { get; set; } = true;
        public bool CanDrop { get; set; } = true;
        public bool CanDestroy { get; set; } = true;
        public uint BuyPrice { get; set; }
        public uint SellPrice { get; set; }

        
        public byte StdMode { get; set; }
        public int Shape { get; set; }
        public ushort Image { get; set; }
        public ushort MaxDura { get; set; }
        
        
        
        
        
        public ushort DuraTimes { get; set; } = 1000;
        public byte Weight { get; set; }
        public sbyte SpecialPower { get; set; }
        public byte NeedType { get; set; }
        public byte NeedLevel { get; set; }
        
        
        
        public byte StateView { get; set; }

        
        public string PageScript { get; set; } = string.Empty;
        public string PickupScript { get; set; } = string.Empty;
        public string DropScript { get; set; } = string.Empty;
        public uint DropScriptDelay { get; set; }
        public uint DropScriptExecuteTimes { get; set; }

        
        public ushort ItemLimit { get; set; }

        
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
        public int HP { get; set; }         
        public int MP { get; set; }         
        public int Lucky { get; set; }      

        
        public int RequireLevel { get; set; }
        public int RequireJob { get; set; } = -1; 
        public int RequireSex { get; set; } = -1; 
        public bool CanDropInSafeArea { get; internal set; }
        public bool CanUse { get; internal set; }
        public bool IsConsumable { get; internal set; }
        public int SubType { get; internal set; }

        public ItemDefinition(int itemId, string name, ItemType type)
        {
            ItemId = itemId;
            Name = name;
            Type = type;
            Quality = ItemQuality.Normal;
            MaxStack = type == ItemType.Potion || type == ItemType.Material ? 100 : 1;
        }
    }

    
    
    
    public class ItemInstance
    {
        public long InstanceId { get; set; }
        public int ItemId { get; set; }
        public ItemDefinition Definition { get; set; }
        public int Count { get; set; } = 1;
        public int Durability { get; set; }
        public int MaxDurability { get; set; }
        public bool IsBound { get; set; }
        public DateTime CreateTime { get; set; }

        
        public int EnhanceLevel { get; set; }

        
        
        
        public byte DressColor { get; set; }

        
        public Dictionary<string, int> ExtraStats { get; set; } = new();
        public string Name { get; internal set; }
        public uint BoundPlayerId { get; internal set; }
        public bool IsExpired { get; internal set; }

        
        
        
        public uint UsingStartTime { get; set; }

        public ItemInstance(ItemDefinition definition, long instanceId)
        {
            Definition = definition;
            ItemId = definition.ItemId;
            InstanceId = instanceId;
            Count = 1;
            MaxDurability = 100;
            Durability = MaxDurability;
            CreateTime = DateTime.Now;
            Name = definition.Name;
            UsingStartTime = 0;
            DressColor = 0;
        }

        
        
        
        public uint GetMakeIndex()
        {
            return (uint)InstanceId;
        }

        
        
        
        
        public ushort GetImageIndex()
        {
            
            return Definition.Image != 0 ? Definition.Image : (ushort)ItemId;
        }

        
        
        
        public string GetName()
        {
            return Name ?? Definition.Name;
        }

        public bool CanStackWith(ItemInstance other)
        {
            return ItemId == other.ItemId &&
                   !IsBound && !other.IsBound &&
                   Definition.MaxStack > 1 &&
                   Count < Definition.MaxStack;
        }

        public int GetTotalMinDC() => Definition.MinDC + ExtraStats.GetValueOrDefault("DC", 0) + EnhanceLevel * 2;
        public int GetTotalMaxDC() => Definition.MaxDC + ExtraStats.GetValueOrDefault("DC", 0) + EnhanceLevel * 2;
        public int GetTotalMinMC() => Definition.MinMC + ExtraStats.GetValueOrDefault("MC", 0);
        public int GetTotalMaxMC() => Definition.MaxMC + ExtraStats.GetValueOrDefault("MC", 0);
        public int GetTotalMinSC() => Definition.MinSC + ExtraStats.GetValueOrDefault("SC", 0);
        public int GetTotalMaxSC() => Definition.MaxSC + ExtraStats.GetValueOrDefault("SC", 0);
        public int GetTotalMinAC() => Definition.MinAC + ExtraStats.GetValueOrDefault("AC", 0) + EnhanceLevel;
        public int GetTotalMaxAC() => Definition.MaxAC + ExtraStats.GetValueOrDefault("AC", 0) + EnhanceLevel;
        public int GetTotalMinMAC() => Definition.MinMAC + ExtraStats.GetValueOrDefault("MAC", 0) + EnhanceLevel;
        public int GetTotalMaxMAC() => Definition.MaxMAC + ExtraStats.GetValueOrDefault("MAC", 0) + EnhanceLevel;

        
        public int GetTotalDC() => GetTotalMinDC() + GetTotalMaxDC();
        public int GetTotalMC() => GetTotalMinMC() + GetTotalMaxMC();
        public int GetTotalSC() => GetTotalMinSC() + GetTotalMaxSC();
        public int GetTotalAC() => GetTotalMinAC() + GetTotalMaxAC();
        public int GetTotalMAC() => GetTotalMinMAC() + GetTotalMaxMAC();
        public int GetTotalHP() => Definition.HP + ExtraStats.GetValueOrDefault("HP", 0);
        public int GetTotalMP() => Definition.MP + ExtraStats.GetValueOrDefault("MP", 0);
        public int GetTotalAccuracy() => Definition.Accuracy + ExtraStats.GetValueOrDefault("Accuracy", 0);
        public int GetTotalAgility() => Definition.Agility + ExtraStats.GetValueOrDefault("Agility", 0);
        public int GetTotalLucky() => Definition.Lucky + ExtraStats.GetValueOrDefault("Lucky", 0);
    }

    
    
    
    public class Inventory
    {
        private readonly Dictionary<int, ItemInstance> _items = new();
        private readonly object _lock = new();
        public int MaxSlots { get; set; } = 40;

        public bool AddItem(ItemInstance item)
        {
            lock (_lock)
            {
                
                if (item.Definition.MaxStack > 1)
                {
                    foreach (var existingItem in _items.Values)
                    {
                        if (existingItem.CanStackWith(item))
                        {
                            int canAdd = Math.Min(
                                item.Count,
                                item.Definition.MaxStack - existingItem.Count
                            );
                            existingItem.Count += canAdd;
                            item.Count -= canAdd;

                            if (item.Count == 0)
                                return true;
                        }
                    }
                }

                
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (!_items.ContainsKey(i))
                    {
                        _items[i] = item;
                        return true;
                    }
                }

                return false; 
            }
        }

        
        
        
        public bool TrySetItem(int slot, ItemInstance item, bool overwrite = false)
        {
            lock (_lock)
            {
                if (slot < 0 || slot >= MaxSlots)
                    return false;

                if (!overwrite && _items.ContainsKey(slot))
                    return false;

                _items[slot] = item;
                return true;
            }
        }

        
        
        
        public bool TryAddItemNoStack(ItemInstance item, out int slot)
        {
            lock (_lock)
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (!_items.ContainsKey(i))
                    {
                        _items[i] = item;
                        slot = i;
                        return true;
                    }
                }

                slot = -1;
                return false;
            }
        }

        public bool RemoveItem(int slot, int count = 1)
        {
            lock (_lock)
            {
                if (!_items.TryGetValue(slot, out var item))
                    return false;

                if (item.Count < count)
                    return false;

                item.Count -= count;
                if (item.Count == 0)
                {
                    _items.Remove(slot);
                }

                return true;
            }
        }

        public ItemInstance? GetItem(int slot)
        {
            lock (_lock)
            {
                _items.TryGetValue(slot, out var item);
                return item;
            }
        }

        public bool MoveItem(int fromSlot, int toSlot)
        {
            lock (_lock)
            {
                if (!_items.ContainsKey(fromSlot))
                    return false;

                var item = _items[fromSlot];
                _items.Remove(fromSlot);

                if (_items.ContainsKey(toSlot))
                {
                    var targetItem = _items[toSlot];
                    _items[fromSlot] = targetItem;
                }

                _items[toSlot] = item;
                return true;
            }
        }

        public int GetItemCount(int itemId)
        {
            lock (_lock)
            {
                return _items.Values
                    .Where(i => i.ItemId == itemId)
                    .Sum(i => i.Count);
            }
        }

        public int GetUsedSlots()
        {
            lock (_lock)
            {
                return _items.Count;
            }
        }

        public Dictionary<int, ItemInstance> GetAllItems()
        {
            lock (_lock)
            {
                return new Dictionary<int, ItemInstance>(_items);
            }
        }

        
        
        
        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
                LogManager.Default.Info($"背包已清空");
            }
        }

        public ItemInstance? FindItem(ulong makeIndex)
        {
            lock (_lock)
            {
                return _items.Values.FirstOrDefault(item => item.InstanceId == (long)makeIndex);
            }
        }

        public int FindSlotByMakeIndex(ulong makeIndex)
        {
            lock (_lock)
            {
                var item = _items.Values.FirstOrDefault(item => item.InstanceId == (long)makeIndex);
                if (item == null)
                    return -1;

                return _items.FirstOrDefault(kvp => kvp.Value == item).Key;
            }
        }

        public bool HasItem(ulong makeIndex)
        {
            lock (_lock)
            {
                return _items.Values.Any(item => item.InstanceId == (long)makeIndex);
            }
        }

        public ItemInstance? GetItemByMakeIndex(ulong makeIndex)
        {
            return FindItem(makeIndex);
        }

        public bool RemoveItemByMakeIndex(ulong makeIndex, int count = 1)
        {
            return RemoveItem(makeIndex, count);
        }

        public bool RemoveItem(ulong makeIndex, int count = 1)
        {
            lock (_lock)
            {
                var item = _items.Values.FirstOrDefault(item => item.InstanceId == (long)makeIndex);
                if (item == null)
                    return false;

                if (item.Count < count)
                    return false;

                item.Count -= count;
                if (item.Count == 0)
                {
                    var slot = _items.FirstOrDefault(kvp => kvp.Value == item).Key;
                    _items.Remove(slot);
                }

                return true;
            }
        }

        
        
        
        public ushort CalcWeight()
        {
            lock (_lock)
            {
                long total = 0;
                foreach (var item in _items.Values)
                {
                    if (item == null)
                        continue;

                    int weight = item.Definition?.Weight ?? 0;
                    if (weight < 0) weight = 0;
                    int count = Math.Max(1, item.Count);

                    total += (long)weight * count;
                    if (total >= ushort.MaxValue)
                        return ushort.MaxValue;
                }

                return (ushort)total;
            }
        }
    }

    
    
    
    public class Equipment
    {
        private readonly ItemInstance?[] _slots = new ItemInstance[(int)EquipSlot.Max];
        private readonly object _lock = new();
        private readonly HumanPlayer _owner;

        public Equipment(HumanPlayer owner)
        {
            _owner = owner;
        }

        
        
        
        public bool Equip(EquipSlot slot, ItemInstance item)
        {
            lock (_lock)
            {
                if (!CanEquip(item))
                    return false;

                
                if (!IsCorrectSlot(slot, item))
                {
                    _owner.Say("这个装备不能放在这个位置");
                    return false;
                }

                
                var oldItem = _slots[(int)slot];
                if (oldItem != null)
                {
                    
                    if (!_owner.Inventory.AddItem(oldItem))
                    {
                        _owner.Say("背包空间不足，无法卸下旧装备");
                        return false;
                    }

                    _slots[(int)slot] = null;
                }

                
                _slots[(int)slot] = item;

                
                _owner.RecalcTotalStats();

                _owner.Say($"装备了 {item.Definition.Name}");
                return true;
            }
        }

        
        
        
        public ItemInstance? Unequip(EquipSlot slot)
        {
            lock (_lock)
            {
                var item = _slots[(int)slot];
                if (item == null)
                    return null;

                
                if (!_owner.Inventory.AddItem(item))
                {
                    _owner.Say("背包空间不足");
                    return null;
                }

                
                _slots[(int)slot] = null;

                
                _owner.RecalcTotalStats();

                _owner.Say($"卸下了 {item.Definition.Name}");
                return item;
            }
        }

        
        
        
        private bool UnequipToInventory(EquipSlot slot)
        {
            var item = _slots[(int)slot];
            if (item == null)
                return true;

            if (!_owner.Inventory.AddItem(item))
                return false;

            _slots[(int)slot] = null;
            return true;
        }

        
        
        
        public ItemInstance? GetEquipment(EquipSlot slot)
        {
            lock (_lock)
            {
                return _slots[(int)slot];
            }
        }

        
        
        
        public ItemInstance? GetItem(EquipSlot slot)
        {
            return GetEquipment(slot);
        }

        
        
        
        public bool CanEquip(ItemInstance item)
        {
            
            if (_owner.Level < item.Definition.RequireLevel)
            {
                _owner.Say($"需要等级 {item.Definition.RequireLevel}");
                return false;
            }

            
            if (item.Definition.RequireJob != -1 && _owner.Job != item.Definition.RequireJob)
            {
                _owner.Say("职业不符");
                return false;
            }

            
            if (item.Definition.RequireSex != -1 && _owner.Sex != item.Definition.RequireSex)
            {
                _owner.Say("性别不符");
                return false;
            }

            
            if (item.IsBound && item.IsBound)
            {
                _owner.Say("绑定物品不能装备");
                return false;
            }

            return true;
        }

        
        
        
        private bool IsCorrectSlot(EquipSlot slot, ItemInstance item)
        {
            
            byte stdMode = item.Definition.StdMode;

            switch (slot)
            {
                case EquipSlot.Charm:
                    return stdMode == 30 || stdMode == 32 || stdMode == 33; 
                case EquipSlot.Weapon:
                    return stdMode == 5 || stdMode == 6;
                case EquipSlot.Dress:
                    return stdMode == (byte)(_owner.Sex + 10); 
                case EquipSlot.Necklace:
                    return stdMode == 19 || stdMode == 20 || stdMode == 21;
                case EquipSlot.RingLeft:
                case EquipSlot.RingRight:
                    return stdMode == 22 || stdMode == 23;
                case EquipSlot.BraceletLeft:
                    if (stdMode == 25 || stdMode == 34) return true; 
                    goto case EquipSlot.BraceletRight;
                case EquipSlot.BraceletRight:
                    return stdMode == 24 || stdMode == 26;
                case EquipSlot.Helmet:
                    return stdMode == 15;
                case EquipSlot.Shoes:
                    return stdMode == 81; 
                case EquipSlot.Belt:
                    return stdMode == 58; 
                case EquipSlot.Stone:
                    return stdMode == 59 || stdMode == 60 || stdMode == 61;
                case EquipSlot.Poison:
                    return stdMode == 25 || stdMode == 34 || stdMode == 33; 
                default:
                    return false;
            }
        }

        
        
        
        public CombatStats GetTotalStats()
        {
            var stats = new CombatStats();

            lock (_lock)
            {
                foreach (var item in _slots)
                {
                    if (item == null) continue;

                    stats.MinDC += item.GetTotalMinDC();
                    stats.MaxDC += item.GetTotalMaxDC();
                    stats.MinMC += item.GetTotalMinMC();
                    stats.MaxMC += item.GetTotalMaxMC();
                    stats.MinSC += item.GetTotalMinSC();
                    stats.MaxSC += item.GetTotalMaxSC();
                    stats.MinAC += item.GetTotalMinAC();
                    stats.MaxAC += item.GetTotalMaxAC();
                    stats.MinMAC += item.GetTotalMinMAC();
                    stats.MaxMAC += item.GetTotalMaxMAC();
                    stats.Accuracy += item.GetTotalAccuracy();
                    stats.Agility += item.GetTotalAgility();
                    stats.Lucky += item.GetTotalLucky();
                    stats.MaxHP += item.GetTotalHP();
                    stats.MaxMP += item.GetTotalMP();
                }
            }

            return stats;
        }

        
        
        
        
        public int CalcEquipmentsWeight(int excludePos = -1)
        {
            int total = 0;

            lock (_lock)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (i == (int)EquipSlot.Weapon)
                        continue;
                    if (excludePos >= 0 && i == excludePos)
                        continue;

                    var item = _slots[i];
                    if (item == null)
                        continue;

                    total += item.Definition?.Weight ?? 0;
                }
            }

            return total;
        }

        
        
        
        public void CheckDurability()
        {
            bool changed = false;
            lock (_lock)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    var item = _slots[i];
                    if (item == null) continue;

                    if (item.Durability <= 0)
                    {
                        
                        _owner.Say($"{item.Definition.Name} 已损坏，需要修理");

                        
                        SendEquipmentBrokenMessage(item);

                        
                        _slots[i] = null;
                        changed = true;

                    }
                    else if (item.Durability <= item.MaxDurability * 0.2)
                    {
                        
                        _owner.Say($"{item.Definition.Name} 耐久度低，请及时修理");

                        
                        SendDurabilityWarningMessage(item);
                    }
                }
            }

            if (changed)
            {
                _owner.RecalcTotalStats();
            }
        }

        
        
        
        private void SendEquipmentBrokenMessage(ItemInstance item)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x28F); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(item.Definition.Name);

            _owner.SendMessage(builder.Build());

            
            _owner.NotifyAppearanceChanged();
        }

        
        
        
        private void SendDurabilityWarningMessage(ItemInstance item)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x290); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(item.Definition.Name);
            builder.WriteUInt16((ushort)item.Durability);
            builder.WriteUInt16((ushort)item.MaxDurability);

            _owner.SendMessage(builder.Build());

            
            _owner.NotifyAppearanceChanged();
        }

        
        
        
        public void ReduceDurability(int amount = 1)
        {
            bool changed = false;
            lock (_lock)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    var item = _slots[i];
                    if (item == null) continue;

                    
                    int durabilityLoss = amount;

                    
                    if (item.Definition.Type == ItemType.Weapon)
                        durabilityLoss *= 2;

                    
                    if (item.Definition.Type == ItemType.Armor ||
                        item.Definition.Type == ItemType.Helmet ||
                        item.Definition.Type == ItemType.Boots)
                        durabilityLoss = 1; 

                    
                    if (item.Definition.Type == ItemType.Necklace ||
                        item.Definition.Type == ItemType.Ring ||
                        item.Definition.Type == ItemType.Bracelet ||
                        item.Definition.Type == ItemType.Belt)
                        durabilityLoss = (int)(amount * 0.5); 

                    item.Durability = Math.Max(0, item.Durability - durabilityLoss);

                    if (item.Durability <= 0)
                    {
                        
                        _owner.Say($"{item.Definition.Name} 已损坏");

                        
                        SendEquipmentBrokenMessage(item);

                        
                        _slots[i] = null;
                        changed = true;
                    }
                    else if (item.Durability <= item.MaxDurability * 0.2)
                    {
                        
                        _owner.Say($"{item.Definition.Name} 耐久度低，请及时修理");

                        
                        SendDurabilityWarningMessage(item);
                    }
                }
            }

            if (changed)
            {
                _owner.RecalcTotalStats();
            }
        }

        
        
        
        public bool RepairEquipment(EquipSlot slot)
        {
            lock (_lock)
            {
                var item = _slots[(int)slot];
                if (item == null)
                {
                    _owner.Say("该位置没有装备");
                    return false;
                }

                
                uint repairCost = CalculateRepairCost(item);
                if (_owner.Gold < repairCost)
                {
                    _owner.Say($"修理需要 {repairCost} 金币，金币不足");
                    return false;
                }

                
                _owner.TakeGold(repairCost);

                
                item.Durability = item.MaxDurability;

                _owner.Say($"修理了 {item.Definition.Name}，花费 {repairCost} 金币");
                return true;
            }
        }

        
        
        
        private uint CalculateRepairCost(ItemInstance item)
        {
            
            float durabilityLossRatio = 1.0f - ((float)item.Durability / item.MaxDurability);
            uint baseCost = (uint)(item.Definition.SellPrice * durabilityLossRatio);

            
            if (item.EnhanceLevel > 0)
                baseCost += (uint)(baseCost * item.EnhanceLevel * 0.1f);

            return Math.Max(10, baseCost); 
        }

        
        
        
        public void ShowEquipmentInfo()
        {
            lock (_lock)
            {
                _owner.Say("=== 装备信息 ===");

                for (int i = 0; i < _slots.Length; i++)
                {
                    var item = _slots[i];
                    var slotName = ((EquipSlot)i).ToString();

                    if (item != null)
                    {
                        _owner.Say($"{slotName}: {item.Definition.Name} (Lv.{item.Definition.Level})");
                        _owner.Say($"  耐久: {item.Durability}/{item.MaxDurability}");
                        _owner.Say($"  强化: +{item.EnhanceLevel}");

                        if (item.Definition.MinDC > 0 || item.Definition.MaxDC > 0)
                            _owner.Say($"  攻击: {item.GetTotalMinDC()}-{item.GetTotalMaxDC()}");
                        if (item.Definition.MinAC > 0 || item.Definition.MaxAC > 0)
                            _owner.Say($"  防御: {item.GetTotalMinAC()}-{item.GetTotalMaxAC()}");
                        if (item.Definition.MinMAC > 0 || item.Definition.MaxMAC > 0)
                            _owner.Say($"  魔防: {item.GetTotalMinMAC()}-{item.GetTotalMaxMAC()}");
                    }
                    else
                    {
                        _owner.Say($"{slotName}: 空");
                    }
                }
            }
        }

        
        
        
        public List<ItemInstance> GetAllEquipment()
        {
            lock (_lock)
            {
                return _slots.Where(item => item != null).ToList()!;
            }
        }

        
        
        
        public ItemInstance? GetWeapon()
        {
            lock (_lock)
            {
                return _slots[(int)EquipSlot.Weapon];
            }
        }

        
        
        
        public void CheckSpecialEffects()
        {
            lock (_lock)
            {
                foreach (var item in _slots)
                {
                    if (item == null) continue;

                    
                    CheckItemSpecialEffects(item);
                }
            }
        }

        
        
        
        private void CheckItemSpecialEffects(ItemInstance item)
        {
            
            if (item.Definition.Lucky > 0)
            {
                
                _owner.Lucky += item.Definition.Lucky;
                
                if (item.Definition.Lucky >= 3)
                {
                    SendSpecialEffectMessage(item, "幸运+3：大幅增加暴击率");
                }
                else if (item.Definition.Lucky >= 2)
                {
                    SendSpecialEffectMessage(item, "幸运+2：增加暴击率");
                }
            }

            
            if (item.Definition.Lucky < 0)
            {
                
                _owner.Lucky += item.Definition.Lucky; 
                SendSpecialEffectMessage(item, $"诅咒：减少幸运{Math.Abs(item.Definition.Lucky)}点");
            }

            
            CheckSetBonusEffects();

            
            CheckUniqueItemEffects(item);
        }

        
        
        
        private void CheckSetBonusEffects()
        {
            
            Dictionary<string, int> setCounts = new();

            foreach (var item in _slots)
            {
                if (item == null) continue;

                
                
                
                
                
                
                
                
            }

            
            foreach (var kvp in setCounts)
            {
                string setName = kvp.Key;
                int count = kvp.Value;

                if (count >= 2)
                {
                    
                    ApplySetBonus(setName, 2);
                }
                if (count >= 4)
                {
                    
                    ApplySetBonus(setName, 4);
                }
                if (count >= 6)
                {
                    
                    ApplySetBonus(setName, 6);
                }
            }
        }

        
        
        
        private void ApplySetBonus(string setName, int pieceCount)
        {
            
            
            string effectMessage = $"{setName} {pieceCount}件套效果激活";
            SendSpecialEffectMessage(null, effectMessage);
        }

        
        
        
        private void CheckUniqueItemEffects(ItemInstance item)
        {
            
            switch (item.ItemId)
            {
                case 5001: 
                    
                    
                    SendSpecialEffectMessage(item, "麻痹戒指：攻击时有概率麻痹目标");
                    break;

                case 5002: 
                    
                    
                    SendSpecialEffectMessage(item, "复活戒指：死亡后自动复活");
                    break;

                case 5003: 
                    
                    
                    SendSpecialEffectMessage(item, "护身戒指：受到伤害时优先消耗MP");
                    break;

                case 5004: 
                    
                    
                    SendSpecialEffectMessage(item, "传送戒指：可以使用传送功能");
                    break;

                case 5005: 
                    
                    
                    SendSpecialEffectMessage(item, "隐身戒指：可以隐身");
                    break;

                default:
                    
                    break;
            }
        }

        
        
        
        private void SendSpecialEffectMessage(ItemInstance? item, string effectMessage)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x291); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            if (item != null)
            {
                builder.WriteString(item.Definition.Name);
            }
            else
            {
                builder.WriteString("");
            }

            builder.WriteString(effectMessage);

            _owner.SendMessage(builder.Build());

            
            _owner.NotifyAppearanceChanged();
        }
    }

    
    
    
    public class ItemManager
    {
        private static ItemManager? _instance;
        public static ItemManager Instance => _instance ??= new ItemManager();

        private readonly Parsers.ItemDataParser _itemDataParser = new();

        private readonly ConcurrentDictionary<int, ItemDefinition> _definitions = new();
        private readonly ConcurrentDictionary<string, ItemDefinition> _definitionsByName = new();
        private int _nextTempMakeIndex = 0;
        private bool _isLoaded = false;

        private ItemManager()
        {
            
        }

        
        
        
        public bool Load(string filePath)
        {
            if (_isLoaded)
            {
                LogManager.Default.Warning("物品数据已加载，跳过重复加载");
                return true;
            }

            try
            {
                if (_itemDataParser.Load(filePath))
                {
                    int loadedCount = 0;
                    foreach (var itemClass in _itemDataParser.GetAllItems())
                    {
                        if (AddItemClass(itemClass))
                        {
                            loadedCount++;
                        }
                    }

                    LogManager.Default.Info($"成功加载 {loadedCount} 个物品定义");
                    _isLoaded = true;
                    return true;
                }
                else
                {
                    LogManager.Default.Error($"加载物品数据文件失败: {filePath}");
                    
                    InitializeDefaultItems();
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物品数据时发生异常: {filePath}", exception: ex);
                
                InitializeDefaultItems();
                return false;
            }
        }

        
        
        
        public bool LoadLimit(string filePath)
        {
            try
            {
                if (!_isLoaded)
                {
                    LogManager.Default.Warning("物品限制加载失败：物品数据尚未加载");
                    return false;
                }

                bool ok = _itemDataParser.LoadItemLimit(filePath);
                if (!ok)
                    return false;

                
                foreach (var itemClass in _itemDataParser.GetAllItems())
                {
                    if (_definitionsByName.TryGetValue(itemClass.Name, out var def))
                        def.ItemLimit = itemClass.ItemLimit;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物品限制配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        
        
        
        public bool LoadScriptLink(string filePath)
        {
            try
            {
                if (!_isLoaded)
                {
                    LogManager.Default.Warning("物品脚本链接加载失败：物品数据尚未加载");
                    return false;
                }

                bool ok = _itemDataParser.LoadItemScript(filePath);
                if (!ok)
                    return false;

                
                foreach (var itemClass in _itemDataParser.GetAllItems())
                {
                    if (_definitionsByName.TryGetValue(itemClass.Name, out var def))
                    {
                        def.PickupScript = itemClass.PickupScript ?? string.Empty;
                        def.DropScript = itemClass.DropScript ?? string.Empty;
                        def.DropScriptDelay = itemClass.DropScriptDelay;
                        def.DropScriptExecuteTimes = itemClass.DropScriptExecuteTimes;
                        if (!string.IsNullOrWhiteSpace(itemClass.PageScript))
                            def.PageScript = itemClass.PageScript.Trim();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物品脚本链接失败: {filePath}", exception: ex);
                return false;
            }
        }

        
        
        
        private bool AddItemClass(Parsers.ItemClass itemClass)
        {
            try
            {
                
                ItemType itemType = ConvertStdModeToItemType(itemClass.StdMode);

                
                int itemId = Math.Abs(itemClass.Name.GetHashCode()) % 1000000;

                
                while (_definitions.ContainsKey(itemId))
                {
                    itemId = (itemId + 1) % 1000000;
                }

                var definition = new ItemDefinition(itemId, itemClass.Name, itemType)
                {
                    
                    StdMode = itemClass.StdMode,
                    Shape = itemClass.Shape,
                    Image = itemClass.Image,
                    MaxDura = itemClass.MaxDura,
                    DuraTimes = itemClass.DuraTimes,
                    Weight = itemClass.Weight,
                    SpecialPower = itemClass.SpecialPower,
                    NeedType = itemClass.NeedType,
                    NeedLevel = itemClass.NeedLevel,
                    StateView = (byte)Math.Clamp(itemClass.StateView, 0, 255),

                    
                    PageScript = itemClass.PageScript ?? string.Empty,
                    PickupScript = itemClass.PickupScript ?? string.Empty,
                    DropScript = itemClass.DropScript ?? string.Empty,
                    DropScriptDelay = itemClass.DropScriptDelay,
                    DropScriptExecuteTimes = itemClass.DropScriptExecuteTimes,
                    ItemLimit = itemClass.ItemLimit,

                    
                    Level = itemClass.NeedLevel,
                    MinDC = itemClass.DC[0],
                    MaxDC = itemClass.DC[1],
                    MinMC = itemClass.MC[0],
                    MaxMC = itemClass.MC[1],
                    MinSC = itemClass.SC[0],
                    MaxSC = itemClass.SC[1],
                    MinAC = itemClass.AC[0],
                    MaxAC = itemClass.AC[1],
                    MinMAC = itemClass.MAC[0],
                    MaxMAC = itemClass.MAC[1],

                    
                    MaxStack = GetMaxStackByType(itemType),
                    BuyPrice = (uint)itemClass.Price,
                    SellPrice = (uint)(itemClass.Price / 2), 
                    RequireLevel = itemClass.NeedLevel,

                    
                    RequireJob = ConvertNeedTypeToJob(itemClass.NeedType),
                };

                
                SetSpecialProperties(definition, itemClass);

                AddDefinition(definition);
                _definitionsByName[itemClass.Name] = definition;

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"添加物品类失败: {itemClass.Name}", exception: ex);
                return false;
            }
        }

        
        
        
        private ItemType ConvertStdModeToItemType(byte stdMode)
        {
            
            switch (stdMode)
            {
                case (byte)MirCommon.ItemStdMode.ISM_WEAPON0:
                case (byte)MirCommon.ItemStdMode.ISM_WEAPON1:
                    return ItemType.Weapon;
                case (byte)MirCommon.ItemStdMode.ISM_DRESS_MALE:
                case (byte)MirCommon.ItemStdMode.ISM_DRESS_FEMALE:
                    return ItemType.Armor;
                case (byte)MirCommon.ItemStdMode.ISM_HELMENT:
                    return ItemType.Helmet;
                case (byte)MirCommon.ItemStdMode.ISM_NECKLACE0:
                case (byte)MirCommon.ItemStdMode.ISM_NECKLACE1:
                case (byte)MirCommon.ItemStdMode.ISM_NECKLACE2:
                    return ItemType.Necklace;
                case (byte)MirCommon.ItemStdMode.ISM_RING0:
                case (byte)MirCommon.ItemStdMode.ISM_RING1:
                    return ItemType.Ring;
                case (byte)MirCommon.ItemStdMode.ISM_BRACELET0:
                case (byte)MirCommon.ItemStdMode.ISM_BRACELET1:
                    return ItemType.Bracelet;
                case (byte)MirCommon.ItemStdMode.ISM_BELT:
                    return ItemType.Belt;
                case (byte)MirCommon.ItemStdMode.ISM_SHOES:
                    return ItemType.Boots;
                case (byte)MirCommon.ItemStdMode.ISM_DRUG:
                    return ItemType.Potion;
                case (byte)MirCommon.ItemStdMode.ISM_USABLEITEM:
                case (byte)MirCommon.ItemStdMode.ISM_SCROLL0:
                case (byte)MirCommon.ItemStdMode.ISM_SCROLL1:
                case (byte)MirCommon.ItemStdMode.ISM_CANDLE:
                    return ItemType.Scroll;
                case (byte)MirCommon.ItemStdMode.ISM_BOOK:
                    return ItemType.Book;
                case (byte)MirCommon.ItemStdMode.ISM_MEAT:
                case (byte)MirCommon.ItemStdMode.ISM_FOOD0:
                case (byte)MirCommon.ItemStdMode.ISM_FOOD1:
                    return ItemType.Food;
                case (byte)MirCommon.ItemStdMode.ISM_MATERIAL:
                case (byte)MirCommon.ItemStdMode.ISM_MINE:
                    return ItemType.Material;
                case (byte)MirCommon.ItemStdMode.ISM_CHARM:
                    return ItemType.Charm;
                default:
                    return ItemType.Other;
            }
        }

        
        
        
        private int GetMaxStackByType(ItemType type)
        {
            return type == ItemType.Potion || type == ItemType.Material || type == ItemType.Scroll ? 100 : 1;
        }

        
        
        
        private int ConvertNeedTypeToJob(byte needType)
        {
            
            switch (needType)
            {
                case 0: return 0; 
                case 1: return 1; 
                case 2: return 2; 
                default: return -1; 
            }
        }

        
        
        
        private void SetSpecialProperties(ItemDefinition definition, Parsers.ItemClass itemClass)
        {
            
            if (itemClass.StdMode == (byte)MirCommon.ItemStdMode.ISM_DRUG)
            {
                definition.HP = itemClass.AC[0];
                definition.MP = itemClass.MAC[0];
                definition.IsConsumable = true;
                definition.CanUse = true;
                return;
            }

            
            
            if (itemClass.StdMode == 5 || itemClass.StdMode == 6) 
            {
                definition.Accuracy = itemClass.SpecialPower; 
            }

            
            definition.Lucky = itemClass.SpecialPower;
        }

        
        
        
        private void InitializeDefaultItems()
        {
            
            AddDefinition(new ItemDefinition(1001, "木剑", ItemType.Weapon)
            {
                Level = 1,
                MinDC = 0,
                MaxDC = 2,
                RequireLevel = 1,
                BuyPrice = 50,
                SellPrice = 10
            });

            AddDefinition(new ItemDefinition(1002, "铁剑", ItemType.Weapon)
            {
                Level = 5,
                MinDC = 0,
                MaxDC = 5,
                RequireLevel = 5,
                Quality = ItemQuality.Fine,
                BuyPrice = 200,
                SellPrice = 40
            });

            AddDefinition(new ItemDefinition(1003, "钢剑", ItemType.Weapon)
            {
                Level = 10,
                MinDC = 0,
                MaxDC = 10,
                RequireLevel = 10,
                Quality = ItemQuality.Rare,
                BuyPrice = 1000,
                SellPrice = 200
            });

            
            AddDefinition(new ItemDefinition(2001, "布衣", ItemType.Armor)
            {
                Level = 1,
                MinAC = 0,
                MaxAC = 2,
                RequireLevel = 1,
                BuyPrice = 50,
                SellPrice = 10
            });

            AddDefinition(new ItemDefinition(2002, "皮甲", ItemType.Armor)
            {
                Level = 5,
                MinAC = 0,
                MaxAC = 5,
                RequireLevel = 5,
                Quality = ItemQuality.Fine,
                BuyPrice = 200,
                SellPrice = 40
            });

            
            AddDefinition(new ItemDefinition(3001, "小红药", ItemType.Potion)
            {
                MaxStack = 100,
                HP = 50,
                BuyPrice = 10,
                SellPrice = 2
            });

            AddDefinition(new ItemDefinition(3002, "大红药", ItemType.Potion)
            {
                MaxStack = 100,
                HP = 150,
                BuyPrice = 30,
                SellPrice = 6
            });

            AddDefinition(new ItemDefinition(3003, "小蓝药", ItemType.Potion)
            {
                MaxStack = 100,
                MP = 50,
                BuyPrice = 10,
                SellPrice = 2
            });

            
            AddDefinition(new ItemDefinition(4001, "铁矿石", ItemType.Material)
            {
                MaxStack = 100,
                BuyPrice = 5,
                SellPrice = 1
            });

            AddDefinition(new ItemDefinition(4002, "布料", ItemType.Material)
            {
                MaxStack = 100,
                BuyPrice = 3,
                SellPrice = 1
            });

            LogManager.Default.Info($"已加载 {_definitions.Count} 个默认物品定义");
            _isLoaded = true;
        }

        public void AddDefinition(ItemDefinition definition)
        {
            _definitions[definition.ItemId] = definition;
            if (!string.IsNullOrWhiteSpace(definition.Name))
            {
                _definitionsByName[definition.Name.Trim()] = definition;
            }
        }

        public ItemDefinition? GetDefinition(int itemId)
        {
            _definitions.TryGetValue(itemId, out var definition);
            return definition;
        }

        public ItemDefinition? GetDefinitionByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return _definitionsByName.TryGetValue(name.Trim(), out var definition) ? definition : null;
        }

        
        
        
        
        public uint AllocateTempMakeIndex()
        {
            uint seq = (uint)(System.Threading.Interlocked.Increment(ref _nextTempMakeIndex) & 0x7FFFFFFF);
            if (seq == 0) seq = 1;
            return 0x80000000u | seq;
        }

        public ItemInstance? CreateItem(int itemId, int count = 1)
        {
            var definition = GetDefinition(itemId);
            if (definition == null)
                return null;

            uint makeIndex = AllocateTempMakeIndex();
            int maxDura;
            if (definition.StdMode == (byte)MirCommon.ItemStdMode.ISM_BOOK)
            {
                maxDura = definition.MaxDura;
            }
            else
            {
                long scaled = (long)definition.DuraTimes * definition.MaxDura;
                maxDura = (int)Math.Clamp(scaled, 0, ushort.MaxValue);
            }

            byte dressColor = 0;
            if (definition.StdMode == (byte)MirCommon.ItemStdMode.ISM_DRESS_MALE ||
                definition.StdMode == (byte)MirCommon.ItemStdMode.ISM_DRESS_FEMALE)
            {
                
                int initDressColor = (int)GameWorld.Instance.GetGameVar(GameVarConstants.InitDressColor);
                dressColor = initDressColor < 0 ? (byte)Random.Shared.Next(0, 16) : (byte)(initDressColor & 0x0F);
            }

            var item = new ItemInstance(definition, (long)makeIndex)
            {
                Count = count,
                MaxDurability = maxDura > 0 ? maxDura : 100,
                Durability = maxDura > 0 ? maxDura : 100,
                DressColor = dressColor
            };

            return item;
        }

        public List<ItemDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }

        public List<ItemDefinition> GetItemsByType(ItemType type)
        {
            return _definitions.Values
                .Where(d => d.Type == type)
                .ToList();
        }

        public List<ItemDefinition> GetItemsByQuality(ItemQuality quality)
        {
            return _definitions.Values
                .Where(d => d.Quality == quality)
                .ToList();
        }
    }

    
    
    
    public class LootSystem
    {
        public class LootEntry
        {
            public int ItemId { get; set; }
            public float DropRate { get; set; } 
            public int MinCount { get; set; } = 1;
            public int MaxCount { get; set; } = 1;
        }

        private readonly Dictionary<int, List<LootEntry>> _monsterLoots = new();

        public void AddMonsterLoot(int monsterId, int itemId, float dropRate, int minCount = 1, int maxCount = 1)
        {
            if (!_monsterLoots.ContainsKey(monsterId))
            {
                _monsterLoots[monsterId] = new List<LootEntry>();
            }

            _monsterLoots[monsterId].Add(new LootEntry
            {
                ItemId = itemId,
                DropRate = dropRate,
                MinCount = minCount,
                MaxCount = maxCount
            });
        }

        public List<ItemInstance> GenerateLoot(int monsterId)
        {
            var loot = new List<ItemInstance>();

            if (!_monsterLoots.TryGetValue(monsterId, out var entries))
                return loot;

            foreach (var entry in entries)
            {
                if (Random.Shared.NextDouble() < entry.DropRate)
                {
                    int count = Random.Shared.Next(entry.MinCount, entry.MaxCount + 1);
                    var item = ItemManager.Instance.CreateItem(entry.ItemId, count);
                    if (item != null)
                    {
                        loot.Add(item);
                    }
                }
            }

            return loot;
        }

        public void InitializeDefaultLoots()
        {
            
            AddMonsterLoot(1, 3001, 0.3f);  
            AddMonsterLoot(1, 4001, 0.2f);  
            AddMonsterLoot(1, 1001, 0.05f); 

            LogManager.Default.Info("掉落表已初始化");
        }
    }

    
    
    
    
    public class BankStorage
    {
        private readonly Dictionary<int, ItemInstance> _items = new();
        private readonly object _lock = new();

        public int MaxSlots { get; set; } = 100;

        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
            }
        }

        public bool AddItem(ItemInstance item)
        {
            lock (_lock)
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (!_items.ContainsKey(i))
                    {
                        _items[i] = item;
                        return true;
                    }
                }
                return false;
            }
        }

        public bool RemoveByMakeIndex(ulong makeIndex)
        {
            lock (_lock)
            {
                var kv = _items.FirstOrDefault(p => (ulong)p.Value.InstanceId == makeIndex);
                if (kv.Value == null)
                    return false;
                _items.Remove(kv.Key);
                return true;
            }
        }

        public ItemInstance? FindByMakeIndex(ulong makeIndex)
        {
            lock (_lock)
            {
                return _items.Values.FirstOrDefault(i => (ulong)i.InstanceId == makeIndex);
            }
        }

        public Dictionary<int, ItemInstance> GetAllItems()
        {
            lock (_lock)
            {
                return new Dictionary<int, ItemInstance>(_items);
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _items.Count;
                }
            }
        }
    }
}
