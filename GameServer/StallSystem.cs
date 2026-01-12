namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    
    
    
    public enum StallStatus
    {
        Closed = 0,     
        Open = 1,       
        Busy = 2,       
        Suspended = 3   
    }

    
    
    
    public class StallItem
    {
        public int Slot { get; set; }
        public ItemInstance Item { get; set; }
        public uint Price { get; set; }
        public uint Stock { get; set; } 
        public uint SoldCount { get; set; }

        public StallItem(int slot, ItemInstance item, uint price, uint stock = 1)
        {
            Slot = slot;
            Item = item;
            Price = price;
            Stock = stock;
            SoldCount = 0;
        }
    }

    
    
    
    public class Stall
    {
        public uint StallId { get; set; }
        public uint OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public StallStatus Status { get; set; }
        public uint MapId { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public uint TotalSales { get; set; }
        public uint TotalIncome { get; set; }
        public uint TaxPaid { get; set; }
        
        private readonly Dictionary<int, StallItem> _items = new();
        private readonly object _itemLock = new();
        private const int MAX_STALL_SLOTS = 20;

        public Stall(uint stallId, uint ownerId, string ownerName, string name, uint mapId, ushort x, ushort y)
        {
            StallId = stallId;
            OwnerId = ownerId;
            OwnerName = ownerName;
            Name = name;
            Status = StallStatus.Closed;
            MapId = mapId;
            X = x;
            Y = y;
            CreateTime = DateTime.Now;
            TotalSales = 0;
            TotalIncome = 0;
            TaxPaid = 0;
        }

        
        
        
        public bool Open()
        {
            if (Status != StallStatus.Closed)
                return false;

            Status = StallStatus.Open;
            OpenTime = DateTime.Now;
            return true;
        }

        
        
        
        public bool Close()
        {
            if (Status == StallStatus.Closed)
                return false;

            Status = StallStatus.Closed;
            CloseTime = DateTime.Now;
            return true;
        }

        
        
        
        public bool Suspend()
        {
            if (Status != StallStatus.Open)
                return false;

            Status = StallStatus.Suspended;
            return true;
        }

        
        
        
        public bool Resume()
        {
            if (Status != StallStatus.Suspended)
                return false;

            Status = StallStatus.Open;
            return true;
        }

        
        
        
        public bool AddItem(int slot, ItemInstance item, uint price, uint stock = 1)
        {
            lock (_itemLock)
            {
                if (slot < 0 || slot >= MAX_STALL_SLOTS)
                    return false;

                if (_items.ContainsKey(slot))
                    return false;

                var stallItem = new StallItem(slot, item, price, stock);
                _items[slot] = stallItem;
                return true;
            }
        }

        
        
        
        public bool RemoveItem(int slot)
        {
            lock (_itemLock)
            {
                return _items.Remove(slot);
            }
        }

        
        
        
        public bool UpdateItemPrice(int slot, uint newPrice)
        {
            lock (_itemLock)
            {
                if (!_items.TryGetValue(slot, out var stallItem))
                    return false;

                stallItem.Price = newPrice;
                return true;
            }
        }

        
        
        
        public bool UpdateItemStock(int slot, uint newStock)
        {
            lock (_itemLock)
            {
                if (!_items.TryGetValue(slot, out var stallItem))
                    return false;

                stallItem.Stock = newStock;
                return true;
            }
        }

        
        
        
        public StallItem? GetItem(int slot)
        {
            lock (_itemLock)
            {
                _items.TryGetValue(slot, out var stallItem);
                return stallItem;
            }
        }

        
        
        
        public List<StallItem> GetAllItems()
        {
            lock (_itemLock)
            {
                return _items.Values.ToList();
            }
        }

        
        
        
        public int GetFreeSlots()
        {
            lock (_itemLock)
            {
                return MAX_STALL_SLOTS - _items.Count;
            }
        }

        
        
        
        public bool BuyItem(int slot, uint quantity, HumanPlayer buyer, out uint totalPrice)
        {
            totalPrice = 0;

            lock (_itemLock)
            {
                if (!_items.TryGetValue(slot, out var stallItem))
                    return false;

                if (stallItem.Stock < quantity)
                    return false;

                
                totalPrice = stallItem.Price * quantity;

                
                if (buyer.Gold < totalPrice)
                    return false;

                
                if (!buyer.TakeGold(totalPrice))
                    return false;

                
                stallItem.Stock -= quantity;
                stallItem.SoldCount += quantity;

                
                var item = ItemManager.Instance.CreateItem(stallItem.Item.ItemId, (int)quantity);
                if (item == null)
                    return false;

                if (!buyer.AddItem(item))
                {
                    
                    buyer.Gold += totalPrice;
                    return false;
                }

                
                TotalSales += quantity;
                TotalIncome += totalPrice;

                
                if (stallItem.Stock == 0)
                {
                    _items.Remove(slot);
                }

                return true;
            }
        }

        
        
        
        public uint CalculateTax()
        {
            
            
            uint baseTax = TotalIncome * 5 / 100;
            
            
            
            
            
            
            return Math.Max(baseTax, 1u);
        }

        
        
        
        public bool PayTax()
        {
            uint tax = CalculateTax();
            if (tax == 0)
                return true;

            
            if (TotalIncome < TaxPaid + tax)
            {
                
                Status = StallStatus.Closed;
                LogManager.Default.Warning($"摊位 {Name} 收入不足支付税收，已自动关闭");
                return false;
            }

            
            
            
            TaxPaid += tax;
            
            
            LogManager.Default.Info($"摊位 {Name} 支付税收 {tax}金币，累计支付 {TaxPaid}金币");
            
            return true;
        }

        
        
        
        public uint GetNetIncome()
        {
            if (TotalIncome > TaxPaid)
                return TotalIncome - TaxPaid;
            return 0;
        }
    }

    
    
    
    public class StallManager
    {
        private static StallManager? _instance;
        public static StallManager Instance => _instance ??= new StallManager();

        private readonly Dictionary<uint, Stall> _stalls = new();
        private readonly Dictionary<uint, uint> _playerStallMap = new(); 
        private readonly Dictionary<uint, List<uint>> _mapStalls = new(); 
        private readonly object _lock = new();
        
        private uint _nextStallId = 100000;

        private StallManager() { }

        
        
        
        public Stall? CreateStall(uint ownerId, string ownerName, string stallName, uint mapId, ushort x, ushort y)
        {
            if (string.IsNullOrWhiteSpace(stallName) || stallName.Length > 20)
                return null;

            
            if (GetPlayerStall(ownerId) != null)
                return null;

            
            if (!IsPositionAvailable(mapId, x, y))
                return null;

            lock (_lock)
            {
                uint stallId = _nextStallId++;
                var stall = new Stall(stallId, ownerId, ownerName, stallName, mapId, x, y);
                
                _stalls[stallId] = stall;
                _playerStallMap[ownerId] = stallId;
                
                
                if (!_mapStalls.ContainsKey(mapId))
                {
                    _mapStalls[mapId] = new List<uint>();
                }
                _mapStalls[mapId].Add(stallId);
                
                LogManager.Default.Info($"玩家 {ownerName} 创建了摊位 {stallName}");
                return stall;
            }
        }

        
        
        
        public bool DisbandStall(uint stallId, uint requesterId)
        {
            lock (_lock)
            {
                if (!_stalls.TryGetValue(stallId, out var stall))
                    return false;

                
                if (stall.OwnerId != requesterId)
                    return false;

                
                if (stall.Status != StallStatus.Closed)
                    return false;

                
                _playerStallMap.Remove(stall.OwnerId);
                
                
                if (_mapStalls.TryGetValue(stall.MapId, out var stallList))
                {
                    stallList.Remove(stallId);
                }

                
                _stalls.Remove(stallId);
                
                LogManager.Default.Info($"摊位 {stall.Name} 已解散");
                return true;
            }
        }

        
        
        
        public bool OpenStall(uint stallId, uint requesterId)
        {
            lock (_lock)
            {
                if (!_stalls.TryGetValue(stallId, out var stall))
                    return false;

                
                if (stall.OwnerId != requesterId)
                    return false;

                return stall.Open();
            }
        }

        
        
        
        public bool CloseStall(uint stallId, uint requesterId)
        {
            lock (_lock)
            {
                if (!_stalls.TryGetValue(stallId, out var stall))
                    return false;

                
                if (stall.OwnerId != requesterId)
                    return false;

                return stall.Close();
            }
        }

        
        
        
        public bool BuyItem(uint stallId, int slot, uint quantity, HumanPlayer buyer)
        {
            lock (_lock)
            {
                if (!_stalls.TryGetValue(stallId, out var stall))
                    return false;

                
                if (stall.Status != StallStatus.Open)
                    return false;

                
                if (buyer.CurrentMap == null || buyer.CurrentMap.MapId != stall.MapId)
                    return false;

                int distance = Math.Abs(buyer.X - stall.X) + Math.Abs(buyer.Y - stall.Y);
                if (distance > 5) 
                    return false;

                
                if (stall.BuyItem(slot, quantity, buyer, out var totalPrice))
                {
                    
                    var owner = HumanPlayerMgr.Instance.FindById(stall.OwnerId);
                    if (owner != null)
                    {
                        owner.SaySystem($"{buyer.Name} 购买了你的 {slot}号物品 x{quantity}，收入 {totalPrice}金币");
                    }
                    
                    LogManager.Default.Info($"{buyer.Name} 从 {stall.OwnerName} 的摊位购买了物品，花费 {totalPrice}金币");
                    return true;
                }

                return false;
            }
        }

        
        
        
        public Stall? GetStall(uint stallId)
        {
            lock (_lock)
            {
                _stalls.TryGetValue(stallId, out var stall);
                return stall;
            }
        }

        
        
        
        public Stall? GetPlayerStall(uint playerId)
        {
            lock (_lock)
            {
                if (_playerStallMap.TryGetValue(playerId, out var stallId))
                {
                    return GetStall(stallId);
                }
                return null;
            }
        }

        
        
        
        public List<Stall> GetStallsInMap(uint mapId)
        {
            lock (_lock)
            {
                if (_mapStalls.TryGetValue(mapId, out var stallIds))
                {
                    return stallIds
                        .Select(id => GetStall(id))
                        .Where(stall => stall != null)
                        .Cast<Stall>()
                        .ToList();
                }
                return new List<Stall>();
            }
        }

        
        
        
        public List<Stall> GetNearbyStalls(HumanPlayer player, int maxDistance = 20)
        {
            if (player.CurrentMap == null)
                return new List<Stall>();

            var stalls = GetStallsInMap(player.CurrentMap.MapId);
            return stalls
                .Where(stall => 
                    Math.Abs(player.X - stall.X) + Math.Abs(player.Y - stall.Y) <= maxDistance &&
                    stall.Status == StallStatus.Open)
                .ToList();
        }

        
        
        
        public List<Stall> SearchStalls(string keyword, uint? mapId = null)
        {
            lock (_lock)
            {
                var results = _stalls.Values
                    .Where(stall => 
                        (stall.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                         stall.OwnerName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) &&
                        stall.Status == StallStatus.Open &&
                        (!mapId.HasValue || stall.MapId == mapId.Value))
                    .Take(50)
                    .ToList();

                return results;
            }
        }

        
        
        
        public List<(Stall stall, StallItem item)> SearchItems(uint itemId, uint? maxPrice = null)
        {
            var results = new List<(Stall stall, StallItem item)>();

            lock (_lock)
            {
                foreach (var stall in _stalls.Values)
                {
                    if (stall.Status != StallStatus.Open)
                        continue;

                    var items = stall.GetAllItems()
                        .Where(item => item.Item.ItemId == itemId &&
                              (!maxPrice.HasValue || item.Price <= maxPrice.Value))
                        .ToList();

                    foreach (var item in items)
                    {
                        results.Add((stall: stall, item: item));
                    }
                }
            }

            return results
                .OrderBy(r => r.item.Price)
                .Take(100)
                .ToList();
        }

        
        
        
        private bool IsPositionAvailable(uint mapId, ushort x, ushort y)
        {
            lock (_lock)
            {
                if (_mapStalls.TryGetValue(mapId, out var stallIds))
                {
                    foreach (var stallId in stallIds)
                    {
                        if (_stalls.TryGetValue(stallId, out var stall))
                        {
                            
                            int distance = Math.Abs(x - stall.X) + Math.Abs(y - stall.Y);
                            if (distance < 3)
                                return false;
                        }
                    }
                }
                return true;
            }
        }

        
        
        
        public (int totalStalls, int openStalls, int totalSales, uint totalIncome) GetStatistics()
        {
            lock (_lock)
            {
                int totalStalls = _stalls.Count;
                int openStalls = _stalls.Values.Count(s => s.Status == StallStatus.Open);
                int totalSales = _stalls.Values.Sum(s => (int)s.TotalSales);
                uint totalIncome = _stalls.Values.Aggregate(0u, (sum, s) => sum + s.TotalIncome);
                
                return (totalStalls, openStalls, totalSales, totalIncome);
            }
        }

        
        
        
        public void CleanupExpiredStalls()
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.Now.AddHours(-24);
                var expiredStalls = _stalls.Values
                    .Where(s => s.Status == StallStatus.Closed && 
                               s.CloseTime.HasValue && 
                               s.CloseTime.Value < cutoffTime)
                    .ToList();
                
                foreach (var stall in expiredStalls)
                {
                    DisbandStall(stall.StallId, stall.OwnerId);
                }
            }
        }

        
        
        
        public void PlayerOffline(uint playerId)
        {
            lock (_lock)
            {
                var stall = GetPlayerStall(playerId);
                if (stall != null && stall.Status == StallStatus.Open)
                {
                    
                    stall.Close();
                }
            }
        }

        
        
        
        public void PlayerOnline(uint playerId)
        {
            
            
        }

        
        
        
        public List<Stall> GetStallRanking(int count = 10)
        {
            lock (_lock)
            {
                return _stalls.Values
                    .Where(s => s.Status == StallStatus.Open)
                    .OrderByDescending(s => s.TotalIncome)
                    .ThenByDescending(s => s.TotalSales)
                    .Take(count)
                    .ToList();
            }
        }

        
        
        
        public List<(uint itemId, string itemName, uint totalSold, uint totalIncome)> GetPopularItems(int count = 10)
        {
            var itemStats = new Dictionary<uint, (string name, uint sold, uint income)>();

            lock (_lock)
            {
                foreach (var stall in _stalls.Values)
                {
                    var items = stall.GetAllItems();
                    foreach (var stallItem in items)
                    {
                        uint itemId = (uint)stallItem.Item.ItemId;
                        string itemName = stallItem.Item.Name;
                        uint sold = stallItem.SoldCount;
                        uint income = stallItem.SoldCount * stallItem.Price;

                        if (itemStats.TryGetValue(itemId, out var stats))
                        {
                            stats.sold += sold;
                            stats.income += income;
                            itemStats[itemId] = stats;
                        }
                        else
                        {
                            itemStats[itemId] = (itemName, sold, income);
                        }
                    }
                }
            }

            return itemStats
                .Select(kv => (kv.Key, kv.Value.name, kv.Value.sold, kv.Value.income))
                .OrderByDescending(x => x.sold)
                .ThenByDescending(x => x.income)
                .Take(count)
                .ToList();
        }

        
        
        
        public int GetStallLimit(uint playerId)
        {
            
            var player = HumanPlayerMgr.Instance.FindById(playerId);
            if (player == null)
                return 1; 

            int limit = 1; 
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            return Math.Min(limit, 5);
        }

        
        
        
        public bool CanCreateStall(uint playerId)
        {
            lock (_lock)
            {
                
                var existingStalls = GetPlayerStalls(playerId);
                int stallLimit = GetStallLimit(playerId);
                if (existingStalls.Count >= stallLimit)
                {
                    LogManager.Default.Info($"玩家 {playerId} 已达到摊位数量限制 {stallLimit}");
                    return false;
                }

                
                int currentStalls = _playerStallMap.Count;
                int maxStalls = 1000; 
                
                
                if (currentStalls >= maxStalls)
                {
                    LogManager.Default.Warning($"服务器摊位数量已达上限 {maxStalls}");
                    return false;
                }

                
                var player = HumanPlayerMgr.Instance.FindById(playerId);
                if (player != null)
                {
                    int minLevel = 30; 
                    
                    
                    if (player.Level < minLevel)
                    {
                        LogManager.Default.Info($"玩家 {player.Name} 等级不足 {minLevel}，无法创建摊位");
                        return false;
                    }
                }

                return true;
            }
        }

        
        
        
        public List<Stall> GetPlayerStalls(uint playerId)
        {
            lock (_lock)
            {
                
                return _stalls.Values
                    .Where(s => s.OwnerId == playerId)
                    .ToList();
            }
        }

        
        
        
        public List<Stall> GetAllStalls()
        {
            lock (_lock)
            {
                return _stalls.Values.ToList();
            }
        }

        
        
        
        public int GetStallCount()
        {
            lock (_lock)
            {
                return _stalls.Count;
            }
        }

        
        
        
        public int GetOpenStallCount()
        {
            lock (_lock)
            {
                return _stalls.Values.Count(s => s.Status == StallStatus.Open);
            }
        }

        
        
        
        public (uint totalTax, int successCount, int failedCount) CollectDailyTax()
        {
            uint totalTax = 0;
            int successCount = 0;
            int failedCount = 0;

            lock (_lock)
            {
                foreach (var stall in _stalls.Values)
                {
                    if (stall.Status == StallStatus.Open)
                    {
                        if (stall.PayTax())
                        {
                            totalTax += stall.CalculateTax();
                            successCount++;
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                }
                
                LogManager.Default.Info($"每日税收收集完成：成功 {successCount}个摊位，失败 {failedCount}个摊位，总税收 {totalTax}金币");
            }
            
            return (totalTax, successCount, failedCount);
        }

        
        
        
        public void Maintenance()
        {
            
            CleanupExpiredStalls();
            
            
            var taxResult = CollectDailyTax();
            
            
            CheckStallStatus();
            
            
            GenerateMaintenanceReport(taxResult);
        }

        
        
        
        private void CheckStallStatus()
        {
            lock (_lock)
            {
                foreach (var stall in _stalls.Values)
                {
                    
                    if (stall.Status == StallStatus.Open)
                    {
                        var lastSaleTime = stall.CloseTime ?? stall.CreateTime;
                        var inactiveHours = (DateTime.Now - lastSaleTime).TotalHours;
                        
                        if (inactiveHours > 72) 
                        {
                            stall.Status = StallStatus.Suspended;
                            LogManager.Default.Info($"摊位 {stall.Name} 因长时间无交易已暂停");
                        }
                    }
                }
            }
        }

        
        
        
        private void GenerateMaintenanceReport((uint totalTax, int successCount, int failedCount) taxResult)
        {
            var stats = GetStatistics();
            var report = $@"
摊位系统维护报告：
- 总摊位数量：{stats.totalStalls}
- 开放摊位数量：{stats.openStalls}
- 总销售额：{stats.totalSales}
- 总收入：{stats.totalIncome}
- 税收收集：成功 {taxResult.successCount}个，失败 {taxResult.failedCount}个
- 总税收：{taxResult.totalTax}金币
- 摊位排名前3：{string.Join(", ", GetStallRanking(3).Select(s => s.Name))}
";
            
            LogManager.Default.Info(report);
        }
    }
}
