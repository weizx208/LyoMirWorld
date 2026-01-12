using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MirCommon;
using MirCommon.Utils;


namespace GameServer
{
    
    
    
    
    public class Npc : MapObject
    {
        
        public int NpcId { get; set; }
        public string Name { get; set; }
        public NpcType Type { get; set; }
        public int ImageIndex { get; set; }
        
        
        public bool CanTalk { get; set; } = true;
        public bool CanTrade { get; set; } = false;
        public bool CanRepair { get; set; } = false;
        public bool CanStore { get; set; } = false;
        
        
        public string? ScriptFile { get; set; }
        
        
        private readonly List<ItemInstance> _shopItems = new();
        private readonly object _shopLock = new();

        public Npc(int npcId, string name, NpcType type)
        {
            NpcId = npcId;
            Name = name;
            Type = type;
        }

        public override ObjectType GetObjectType() => ObjectType.NPC;

        
        
        
        public void OnTalk(HumanPlayer player)
        {
            if (!CanTalk)
                return;

            LogManager.Default.Info($"{player.Name} 与 {Name} 对话");
            
            
            switch (Type)
            {
                case NpcType.Merchant:
                    OpenShop(player);
                    break;
                case NpcType.Warehouse:
                    OpenWarehouse(player);
                    break;
                case NpcType.Quest:
                    ShowQuests(player);
                    break;
                case NpcType.Teleporter:
                    ShowTeleportMenu(player);
                    break;
                case NpcType.Script:
                    ExecuteScript(player);
                    break;
                default:
                    SendGreeting(player);
                    break;
            }
        }

        
        
        
        private void OpenShop(HumanPlayer player)
        {
            if (!CanTrade)
            {
                player.Say("我现在不能交易");
                return;
            }

            lock (_shopLock)
            {
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(ObjectId);
                builder.WriteUInt16(ProtocolCmd.SM_OPENSHOP);
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                
                
                builder.WriteUInt16((ushort)_shopItems.Count);
                foreach (var item in _shopItems)
                {
                    builder.WriteUInt32((uint)item.ItemId);
                    builder.WriteUInt32((uint)item.Definition.BuyPrice);
                    builder.WriteString(item.Definition.Name);
                }
                
                byte[] packet = builder.Build();
                player.SendMessage(packet);
                
                LogManager.Default.Debug($"{player.Name} 打开了 {Name} 的商店");
            }
        }

        
        
        
        private void OpenWarehouse(HumanPlayer player)
        {
            if (!CanStore)
            {
                player.Say("我现在不能提供仓库服务");
                return;
            }

            
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(ProtocolCmd.SM_OPENSTORAGE);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
            
            LogManager.Default.Debug($"{player.Name} 打开了仓库");
        }

        
        
        
        private void ShowQuests(HumanPlayer player)
        {
            
            var availableQuests = QuestDefinitionManager.Instance.GetAllDefinitions()
                .Where(q => q.CanAccept(player) && 
                           !player.QuestManager.HasActiveQuest(q.QuestId) &&
                           (!player.QuestManager.HasCompletedQuest(q.QuestId) || q.Repeatable))
                .ToList();
            
            if (availableQuests.Count == 0)
            {
                player.Say("我这里暂时没有适合你的任务。");
                return;
            }

            
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(ProtocolCmd.SM_DIALOG);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString("我这里有些任务，你愿意帮忙吗？");
            
            
            builder.WriteByte((byte)availableQuests.Count);
            for (int i = 0; i < availableQuests.Count; i++)
            {
                var quest = availableQuests[i];
                builder.WriteInt32(i + 1);
                builder.WriteString($"{quest.Name} (等级要求: {quest.RequireLevel})");
            }
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }

        
        
        
        private void ShowTeleportMenu(HumanPlayer player)
        {
            
            var destinations = new List<(string name, int mapId, ushort x, ushort y, uint cost)>
            {
                ("比奇城", 0, 300, 300, 100),
                ("银杏山谷", 1, 200, 200, 200),
                ("毒蛇山谷", 2, 150, 150, 300),
                ("盟重省", 3, 400, 400, 500)
            };

            
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(ProtocolCmd.SM_TELEPORTLIST);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            
            builder.WriteUInt16((ushort)destinations.Count);
            foreach (var dest in destinations)
            {
                builder.WriteString(dest.name);
                builder.WriteUInt32((uint)dest.mapId);
                builder.WriteUInt16(dest.x);
                builder.WriteUInt16(dest.y);
                builder.WriteUInt32(dest.cost);
            }
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }

        
        
        
        private void ExecuteScript(HumanPlayer player)
        {
            if (string.IsNullOrEmpty(ScriptFile))
            {
                SendGreeting(player);
                return;
            }

            
            if (!NpcScriptEngine.TryHandleTalk(player, this))
            {
                LogManager.Default.Warning($"NPC脚本对象不存在或未加载: '{ScriptFile}' (NPC: {Name})");
                SendGreeting(player);
            }
        }

        
        
        
        private void SendGreeting(HumanPlayer player)
        {
            player.Say($"{Name}: 你好，{player.Name}！");
        }

        
        
        
        public void AddShopItem(ItemInstance item)
        {
            lock (_shopLock)
            {
                _shopItems.Add(item);
            }
        }

        
        
        
        public bool BuyItem(HumanPlayer player, int itemIndex)
        {
            if (!CanTrade)
                return false;

            lock (_shopLock)
            {
                if (itemIndex < 0 || itemIndex >= _shopItems.Count)
                    return false;

                var item = _shopItems[itemIndex];
                
                
                if (player.Gold < item.Definition.BuyPrice)
                {
                    player.Say("你的金币不足");
                    return false;
                }

                
                if (!player.TakeGold(item.Definition.BuyPrice))
                    return false;

                
                var newItem = ItemManager.Instance.CreateItem(item.ItemId);
                if (newItem != null && player.Inventory.AddItem(newItem))
                {
                    player.Say($"购买了 {item.Definition.Name}");
                    return true;
                }
                else
                {
                    
                    player.AddGold(item.Definition.BuyPrice);
                    player.Say("背包已满");
                    return false;
                }
            }
        }

        
        
        
        public bool SellItem(HumanPlayer player, int bagSlot)
        {
            if (!CanTrade)
                return false;

            var item = player.Inventory.GetItem(bagSlot);
            if (item == null)
                return false;

            if (!item.Definition.CanTrade)
            {
                player.Say("这个物品不能出售");
                return false;
            }

            
            if (!player.Inventory.RemoveItem(bagSlot, 1))
                return false;

            
            player.AddGold(item.Definition.SellPrice);
            player.Say($"出售了 {item.Definition.Name}，获得 {item.Definition.SellPrice} 金币");
            
            return true;
        }

        
        
        
        public bool RepairItem(HumanPlayer player, EquipSlot slot)
        {
            if (!CanRepair)
            {
                player.Say("我不能修理物品");
                return false;
            }

            var item = player.Equipment.GetEquipment(slot);
            if (item == null)
            {
                player.Say("没有装备可以修理");
                return false;
            }

            if (item.Durability >= item.MaxDurability)
            {
                player.Say("这个物品不需要修理");
                return false;
            }

            
            int repairCost = CalculateRepairCost(item);
            
            if (player.Gold < repairCost)
            {
                player.Say($"修理需要 {repairCost} 金币，你的金币不足");
                return false;
            }

            
            if (!player.TakeGold((uint)repairCost))
                return false;

            
            item.Durability = item.MaxDurability;
            player.Say($"修理完成，花费 {repairCost} 金币");
            
            return true;
        }

        
        
        
        private int CalculateRepairCost(ItemInstance item)
        {
            int damageCost = (item.MaxDurability - item.Durability) * 10;
            int baseCost = (int)(item.Definition.BuyPrice * 0.1);
            return Math.Max(damageCost, baseCost);
        }

        
        
        
        public bool TeleportPlayer(HumanPlayer player, int targetMapId, ushort targetX, ushort targetY, uint cost = 0)
        {
            
            if (cost > 0)
            {
                if (player.Gold < cost)
                {
                    player.Say($"传送需要 {cost} 金币");
                    return false;
                }
                
                if (!player.TakeGold(cost))
                    return false;
            }

            
            var targetMap = MapManager.Instance.GetMap((uint)targetMapId);
            if (targetMap == null)
            {
                player.Say("传送目标不存在");
                if (cost > 0) player.AddGold(cost); 
                return false;
            }

            
            bool ok = player.ChangeMap((uint)targetMapId, targetX, targetY);
            if (!ok && cost > 0)
            {
                player.AddGold(cost); 
            }
            return ok;
        }

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            msg = Array.Empty<byte>();

            
            uint feather = GetFeather();
            uint status = 0;
            uint health = GetHealth();

            string tail = $"{Name}/255";
            byte[] tailBytes = Encoding.GetEncoding("GBK").GetBytes(tail);

            byte[] data = new byte[12 + tailBytes.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(feather), 0, data, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(status), 0, data, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(health), 0, data, 8, 4);
            Buffer.BlockCopy(tailBytes, 0, data, 12, tailBytes.Length);

            ushort w3 = (ushort)((GetSex() << 8) | 0); 
            var outMsg = new MirCommon.MirMsgOrign
            {
                dwFlag = ObjectId,
                wCmd = ProtocolCmd.SM_APPEAR,
                wParam = new ushort[3] { (ushort)X, (ushort)Y, w3 },
            };

            msg = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(outMsg, data);
            return msg.Length > 0;
        }

        public uint GetFeather()
        {
            
            byte view = (byte)(ImageIndex & 0xFF);
            return (uint)((0 << 24) | (view << 16) | (0 << 8) | 0x32);
        }

        public uint GetHealth() => 0x00640064;

        public byte GetSex() => 0;
    }


    
    
    
    public enum NpcType
    {
        Normal = 0,      
        Merchant = 1,    
        Warehouse = 2,   
        Quest = 3,       
        Teleporter = 4,  
        Repair = 5,      
        Script = 6       
    }

    
    
    
    public class NpcManager
    {
        private static NpcManager? _instance;
        public static NpcManager Instance => _instance ??= new NpcManager();

        private readonly Dictionary<int, Npc> _npcs = new();
        private readonly object _lock = new();

        private NpcManager()
        {
        }

        
        
        
        public void Initialize()
        {
            CreateDefaultNpcs();
        }

        private void CreateDefaultNpcs()
        {
            
            
            
            var merchant1 = new Npc(1001, "武器店老板", NpcType.Merchant)
            {
                CanTrade = true
            };
            AddShopItems(merchant1, ItemType.Weapon);
            AddNpc(merchant1);

            
            var merchant2 = new Npc(1002, "药店老板", NpcType.Merchant)
            {
                CanTrade = true
            };
            AddShopItems(merchant2, ItemType.Potion);
            AddNpc(merchant2);

            
            var warehouse = new Npc(1003, "仓库管理员", NpcType.Warehouse)
            {
                CanStore = true
            };
            AddNpc(warehouse);

            
            var repair = new Npc(1004, "铁匠", NpcType.Repair)
            {
                CanRepair = true
            };
            AddNpc(repair);

            
            var teleporter = new Npc(1005, "传送员", NpcType.Teleporter);
            AddNpc(teleporter);

            LogManager.Default.Info($"已创建 {_npcs.Count} 个NPC");
        }

        
        
        
        private void AddShopItems(Npc npc, ItemType type)
        {
            var items = ItemManager.Instance.GetItemsByType(type);
            foreach (var itemDef in items.Take(20)) 
            {
                var item = ItemManager.Instance.CreateItem(itemDef.ItemId);
                if (item != null)
                {
                    npc.AddShopItem(item);
                }
            }
        }

        
        
        
        public void AddNpc(Npc npc)
        {
            lock (_lock)
            {
                _npcs[npc.NpcId] = npc;
            }
        }

        
        
        
        public Npc? GetNpc(int npcId)
        {
            lock (_lock)
            {
                return _npcs.TryGetValue(npcId, out var npc) ? npc : null;
            }
        }

        
        
        
        public bool PlaceNpc(int npcId, LogicMap map, ushort x, ushort y)
        {
            var npc = GetNpc(npcId);
            if (npc == null)
                return false;

            return map.AddObject(npc, x, y);
        }

        
        
        
        public Npc CreateNpc(int npcId, string name, NpcType type)
        {
            var npc = new Npc(npcId, name, type);
            AddNpc(npc);
            return npc;
        }
    }
}
