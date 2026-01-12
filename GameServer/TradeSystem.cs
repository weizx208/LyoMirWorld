using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Utils;


using Player = GameServer.HumanPlayer;

namespace GameServer
{
    
    
    
    public enum TradeState
    {
        PuttingItems = 0,      
        WaitingForOther = 1,   
        Completed = 2,         
        Cancelled = 3          
    }

    
    
    
    public enum TradeEndType
    {
        Cancel = 0,            
        Confirm = 1            
    }

    
    
    
    public class TradeSide
    {
        public Player Player { get; set; }
        public List<ItemInstance> Items { get; set; } = new List<ItemInstance>(10);
        public uint Gold { get; set; }
        public uint Yuanbao { get; set; }
        public bool Ready { get; set; }

        public TradeSide(Player player)
        {
            Player = player;
            
            for (int i = 0; i < 10; i++)
            {
                Items.Add(null);
            }
        }

        public int GetItemCount()
        {
            return Items.Count(item => !IsDefaultItem(item));
        }

        public bool AddItem(ItemInstance item)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (IsDefaultItem(Items[i]))
                {
                    Items[i] = item;
                    return true;
                }
            }
            return false;
        }

        public bool RemoveItem(int index)
        {
            if (index >= 0 && index < Items.Count && !IsDefaultItem(Items[index]))
            {
                Items[index] = null;
                return true;
            }
            return false;
        }

        public void ClearItems()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i] = null;
            }
        }

        private bool IsDefaultItem(ItemInstance item)
        {
            return item == null || item.InstanceId == 0;
        }

        private string GetItemName(ItemInstance item)
        {
            return item?.Definition?.Name ?? "";
        }

        private uint GetItemId(ItemInstance item)
        {
            return (uint)(item?.InstanceId ?? 0);
        }
    }

    
    
    
    public class TradeObject
    {
        private TradeSide[] _sides = new TradeSide[2];
        private TradeState _state = TradeState.PuttingItems;
        private string _errorMessage = "交易成功!";
        private DateTime _startTime;
        private const int TRADE_TIMEOUT_SECONDS = 60; 

        public TradeObject(Player player1, Player player2)
        {
            _sides[0] = new TradeSide(player1);
            _sides[1] = new TradeSide(player2);
            _state = TradeState.PuttingItems;
            _startTime = DateTime.Now;
        }

        public TradeState State => _state;
        public string ErrorMessage => _errorMessage;

        internal TradeSide GetSide(Player player)
        {
            if (_sides[0].Player == player) return _sides[0];
            if (_sides[1].Player == player) return _sides[1];
            return null;
        }

        internal TradeSide GetOtherSide(Player player)
        {
            if (_sides[0].Player == player) return _sides[1];
            if (_sides[1].Player == player) return _sides[0];
            return null;
        }

        private bool IsDefaultItem(Item item)
        {
            return item.dwMakeIndex == 0 && item.baseitem.btNameLength == 0;
        }

        private string GetItemName(Item item)
        {
            return item.baseitem.szName ?? "";
        }

        private uint GetItemId(Item item)
        {
            return item.dwMakeIndex;
        }

        
        
        
        public bool Begin()
        {
            var player1 = _sides[0].Player;
            var player2 = _sides[1].Player;

            if (player1 == null || player2 == null)
                return false;

            
            if (!IsPlayersInRange(player1, player2))
            {
                _errorMessage = "交易双方距离太远";
                return false;
            }

            
            if (!CanPlayerTrade(player1) || !CanPlayerTrade(player2))
            {
                _errorMessage = "玩家状态不允许交易";
                return false;
            }

            
            player1.CurrentTrade = this;
            player2.CurrentTrade = this;

            
            player1.SendTradeStart(player2.Name);
            player2.SendTradeStart(player1.Name);

            LogManager.Default.Info($"{player1.Name} 和 {player2.Name} 开始交易");
            return true;
        }

        
        
        
        private bool IsPlayersInRange(Player player1, Player player2)
        {
            if (player1.CurrentMap != player2.CurrentMap)
                return false;

            int dx = Math.Abs(player1.X - player2.X);
            int dy = Math.Abs(player1.Y - player2.Y);
            return dx <= 5 && dy <= 5; 
        }

        
        
        
        private bool CanPlayerTrade(Player player)
        {
            
            if (player.CurrentHP <= 0)
                return false;

            
            if (player.IsInCombat())
                return false;

            
            if (player.IsInPrivateShop())
                return false;

            return true;
        }

        
        
        
        public bool PutItem(Player player, ItemInstance item)
        {
            var side = GetSide(player);
            var otherSide = GetOtherSide(player);

            if (side == null || otherSide == null)
            {
                _errorMessage = "交易方不存在";
                return false;
            }

            if (_state != TradeState.PuttingItems)
            {
                _errorMessage = "无法放入物品，对方已经按下交易按钮！";
                
                player.SendTradePutItemFail();
                return false;
            }

            
            if (item == null || item.InstanceId == 0)
            {
                _errorMessage = "无效的物品";
                return false;
            }

            
            if (!CanItemBeTraded(item))
            {
                _errorMessage = "该物品不能交易";
                return false;
            }

            
            if (!PlayerHasItem(player, item))
            {
                _errorMessage = "您没有这个物品";
                return false;
            }

            
            if (!side.AddItem(item))
            {
                _errorMessage = "交易栏已满，无法放入新物品!";
                player.SendTradePutItemFail();
                return false;
            }

            
            RemoveItemFromPlayer(player, item);

            
            otherSide.Player.SendTradeOtherAddItem(player, item);

            LogManager.Default.Debug($"{player.Name} 放入物品: {item?.Definition?.Name ?? ""}");
            return true;
        }

        
        
        
        public bool PutMoney(Player player, MoneyType type, uint amount)
        {
            var side = GetSide(player);
            var otherSide = GetOtherSide(player);

            if (side == null || otherSide == null)
            {
                _errorMessage = "交易方不存在";
                return false;
            }

            uint current = type == MoneyType.Gold ? side.Gold : side.Yuanbao;
            if (current == amount)
                return true;

            if (amount > current)
            {
                var delta = amount - current;
                if (!TryCostMoney(player, type, delta))
                {
                    _errorMessage = type == MoneyType.Gold ? "金币不足" : "元宝不足";
                    return false;
                }
            }
            else
            {
                var delta = current - amount;
                if (!TryRefundMoney(player, type, delta))
                {
                    _errorMessage = "身上钱太多，无法拿回";
                    return false;
                }
            }

            if (type == MoneyType.Gold)
                side.Gold = amount;
            else
                side.Yuanbao = amount;

            
            otherSide.Player.SendTradeOtherAddMoney(player, type, amount);
            return true;
        }

        private static bool TryCostMoney(Player player, MoneyType type, uint delta)
        {
            if (delta == 0) return true;

            if (type == MoneyType.Gold)
            {
                if (player.Gold < delta) return false;
                player.TakeGold(delta);
                return true;
            }

            return player.TakeYuanbao(delta);
        }

        private static bool TryRefundMoney(Player player, MoneyType type, uint delta)
        {
            if (delta == 0) return true;

            if (type == MoneyType.Gold)
                return player.AddGold(delta);

            return player.AddYuanbao(delta);
        }

        
        
        
        public bool End(Player player, TradeEndType endType)
        {
            var side = GetSide(player);
            var otherSide = GetOtherSide(player);

            if (side == null || otherSide == null)
            {
                _errorMessage = "您现在不在交易状态！";
                return false;
            }

            
            if ((DateTime.Now - _startTime).TotalSeconds > TRADE_TIMEOUT_SECONDS)
            {
                _errorMessage = "交易超时";
                DoCancel(side, otherSide);
                return true;
            }

            bool tradeEnded = false;

            switch (endType)
            {
                case TradeEndType.Cancel:
                    
                    otherSide.Player.SaySystem("对方取消交易！");
                    DoCancel(side, otherSide);
                    tradeEnded = true;
                    break;

                case TradeEndType.Confirm:
                    if (side.Ready)
                    {
                        side.Player.SaySystemTrade("请让对方按下交易按钮");
                        otherSide.Player.SaySystemTrade("对方再次要求你确认交易，按下[交易]键确认");
                    }
                    else
                    {
                        side.Ready = true;
                        if (otherSide.Ready)
                        {
                            
                            if (!DoExchange(side, otherSide))
                            {
                                DoCancel(side, otherSide);
                            }
                            tradeEnded = true;
                        }
                        else
                        {
                            _state = TradeState.WaitingForOther;
                            side.Player.SaySystemTrade("请让对方按下交易按钮");
                            otherSide.Player.SaySystemTrade("对方再次要求你确认交易，按下[交易]键确认");
                        }
                    }
                    break;
            }

            if (tradeEnded)
            {
                
                side.Player.CurrentTrade = null;
                otherSide.Player.CurrentTrade = null;
                
                
                TradeManager.Instance.EndTrade(this);
            }

            return true;
        }

        
        
        
        private bool DoExchange(TradeSide actionSide, TradeSide otherSide)
        {
            
            int itemCount1 = actionSide.GetItemCount();
            int itemCount2 = otherSide.GetItemCount();

            if (itemCount1 > otherSide.Player.Inventory.MaxSlots - otherSide.Player.Inventory.GetUsedSlots())
            {
                actionSide.Player.SaySystem("对方的背包无法容纳这么多物品！");
                return false;
            }

            if (itemCount2 > actionSide.Player.Inventory.MaxSlots - actionSide.Player.Inventory.GetUsedSlots())
            {
                otherSide.Player.SaySystem("对方的背包无法容纳这么多物品！");
                return false;
            }

            
            if (actionSide.Gold > 0)
            {
                if (!otherSide.Player.CanAddGold(actionSide.Gold))
                {
                    actionSide.Player.SaySystem("钱币太多，对方拿不下！");
                    return false;
                }
            }

            if (actionSide.Yuanbao > 0)
            {
                if (!otherSide.Player.CanAddYuanbao(actionSide.Yuanbao))
                {
                    actionSide.Player.SaySystem("元宝太多，对方拿不下！");
                    return false;
                }
            }

            if (otherSide.Gold > 0)
            {
                if (!actionSide.Player.CanAddGold(otherSide.Gold))
                {
                    otherSide.Player.SaySystem("钱币太多，对方拿不下！");
                    return false;
                }
            }

            if (otherSide.Yuanbao > 0)
            {
                if (!actionSide.Player.CanAddYuanbao(otherSide.Yuanbao))
                {
                    otherSide.Player.SaySystem("元宝太多，对方拿不下！");
                    return false;
                }
            }

            
            
            for (int i = 0; i < 10; i++)
            {
                if (actionSide.Items[i] != null && actionSide.Items[i].InstanceId != 0)
                {
                    
                    if (!otherSide.Player.Inventory.AddItem(actionSide.Items[i]))
                    {
                        
                        return false;
                    }
                }
                if (otherSide.Items[i] != null && otherSide.Items[i].InstanceId != 0)
                {
                    
                    if (!actionSide.Player.Inventory.AddItem(otherSide.Items[i]))
                    {
                        
                        return false;
                    }
                }
            }

            
            actionSide.Player.AddGold(otherSide.Gold);
            otherSide.Player.AddGold(actionSide.Gold);
            
            
            if (actionSide.Yuanbao > 0)
            {
                if (!otherSide.Player.AddYuanbao(actionSide.Yuanbao))
                {
                    
                    return false;
                }
            }
            
            if (otherSide.Yuanbao > 0)
            {
                if (!actionSide.Player.AddYuanbao(otherSide.Yuanbao))
                {
                    
                    return false;
                }
            }

            
            actionSide.Player.SendTradeEnd();
            actionSide.Player.SaySystemTrade("交易成功");
            
            otherSide.Player.SendTradeEnd();
            otherSide.Player.SaySystemTrade("交易成功");

            _state = TradeState.Completed;
            LogManager.Default.Info($"{actionSide.Player.Name} 和 {otherSide.Player.Name} 交易成功");
            return true;
        }

        
        
        
        private void DoCancel(TradeSide actionSide, TradeSide otherSide)
        {
            
            for (int i = 0; i < 10; i++)
            {
                if (actionSide.Items[i] != null && actionSide.Items[i].InstanceId != 0)
                {
                    
                    actionSide.Player.Inventory.AddItem(actionSide.Items[i]);
                }
                if (otherSide.Items[i] != null && otherSide.Items[i].InstanceId != 0)
                {
                    
                    otherSide.Player.Inventory.AddItem(otherSide.Items[i]);
                }
            }

            
            actionSide.Player.AddGold(actionSide.Gold);
            if (actionSide.Yuanbao > 0)
            {
                actionSide.Player.AddYuanbao(actionSide.Yuanbao);
            }
            
            otherSide.Player.AddGold(otherSide.Gold);
            if (otherSide.Yuanbao > 0)
            {
                otherSide.Player.AddYuanbao(otherSide.Yuanbao);
            }

            
            actionSide.Player.SendTradeCancelled();
            actionSide.Player.SaySystemTrade("交易取消");
            
            otherSide.Player.SendTradeCancelled();
            otherSide.Player.SaySystemTrade("交易取消");

            _state = TradeState.Cancelled;
            LogManager.Default.Info($"{actionSide.Player.Name} 和 {otherSide.Player.Name} 交易取消");
        }

        
        
        
        public void Update()
        {
            
            if (_state == TradeState.PuttingItems || _state == TradeState.WaitingForOther)
            {
                if ((DateTime.Now - _startTime).TotalSeconds > TRADE_TIMEOUT_SECONDS)
                {
                    
                    var side1 = _sides[0];
                    var side2 = _sides[1];
                    DoCancel(side1, side2);
                    
                    
                    side1.Player.CurrentTrade = null;
                    side2.Player.CurrentTrade = null;
                    
                    
                    TradeManager.Instance.EndTrade(this);
                }
            }
        }

        
        private bool CanItemBeTraded(ItemInstance item)
        {
            
            return item != null && item.InstanceId != 0;
        }

        private bool PlayerHasItem(Player player, ItemInstance item)
        {
            
            return player != null && item != null && item.InstanceId != 0;
        }

        private void RemoveItemFromPlayer(Player player, ItemInstance item)
        {
            
            
        }
    }

    
    
    
    public class TradeManager
    {
        private static TradeManager _instance;
        public static TradeManager Instance => _instance ??= new TradeManager();

        private readonly List<TradeObject> _activeTrades = new List<TradeObject>();
        private readonly object _lock = new object();

        private TradeManager() { }

        
        
        
        public bool StartTrade(Player player1, Player player2)
        {
            lock (_lock)
            {
                
                if (player1.CurrentTrade != null || player2.CurrentTrade != null)
                {
                    return false;
                }

                
                var trade = new TradeObject(player1, player2);
                if (trade.Begin())
                {
                    _activeTrades.Add(trade);
                    return true;
                }

                return false;
            }
        }

        
        
        
        public void EndTrade(TradeObject trade)
        {
            lock (_lock)
            {
                _activeTrades.Remove(trade);
            }
        }

        
        
        
        public TradeObject GetPlayerTrade(Player player)
        {
            lock (_lock)
            {
                return _activeTrades.FirstOrDefault(t => 
                    t.GetSide(player) != null || t.GetOtherSide(player) != null);
            }
        }
    }

    
    
    
    public static class TradePlayerExtensions
    {
        public static void SendTradeStart(this Player player, string otherPlayerName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(TradeProtocol.SM_TRADESTART);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(otherPlayerName);
            player.SendMessage(builder.Build());
        }

        public static void SendTradeEnd(this Player player)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(TradeProtocol.SM_TRADEEND);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            player.SendMessage(builder.Build());
        }

        public static void SendTradeCancelled(this Player player)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(TradeProtocol.SM_TRADECANCELED);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            player.SendMessage(builder.Build());
        }

        public static void SendTradePutItemFail(this Player player)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(TradeProtocol.SM_TRADE_PUTITEM_FAIL);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            player.SendMessage(builder.Build());
        }

        public static void SendTradeOtherAddItem(this Player player, Player otherPlayer, ItemInstance item)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(0x292); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(otherPlayer.ObjectId);
            builder.WriteUInt64((ulong)(item?.InstanceId ?? 0));
            builder.WriteString(item?.Definition?.Name ?? "");
            
            player.SendMessage(builder.Build());
        }

        public static void SendTradeOtherAddMoney(this Player player, Player otherPlayer, MoneyType type, uint amount)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(0x293); 
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(otherPlayer.ObjectId);
            builder.WriteUInt16((ushort)type);
            builder.WriteUInt32(amount);
            
            player.SendMessage(builder.Build());
        }

        public static void SaySystemTrade(this Player player, string message)
        {
            player.SaySystem($"[交易] {message}");
        }
    }

    internal static class TradeProtocol
    {
        
        public const ushort SM_TRADESTART = 0x290;
        public const ushort SM_TRADEEND = 0x294;
        public const ushort SM_TRADECANCELED = 0x295;

        
        public const ushort SM_TRADE_PUTITEM_FAIL = 0x2a4;

        
        
        
    }
}
