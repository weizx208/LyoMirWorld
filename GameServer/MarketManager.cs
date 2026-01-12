using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    public class MarketItem
    {
        public uint Id { get; set; }
        public uint MarketId { get; set; }
        public uint SubMarketId { get; set; }

        public int Image { get; set; }
        public int ShowImage { get; set; }

        public string Name { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Tips { get; set; } = string.Empty;
        internal string ProcessedTips { get; set; } = string.Empty;

        public uint Price { get; set; }
        public uint Count { get; set; } = 1;
        public uint Stock { get; set; }
        public uint MaxStock { get; set; }
        public uint RefreshInterval { get; set; }
        public uint LastRefreshTime { get; set; }
    }

    
    
    
    public class SubMarket
    {
        public uint MarketId { get; }
        public uint Id { get; }
        public string Name { get; }
        public List<MarketItem> Items { get; } = new();

        public SubMarket(uint marketId, uint id, string name)
        {
            MarketId = marketId;
            Id = id;
            Name = name ?? string.Empty;
        }
    }

    
    
    
    public class Market
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<MarketItem> Items { get; set; } = new();
        public List<SubMarket> SubMarkets { get; } = new();

        public SubMarket? GetSubMarket(uint subMarketId)
        {
            return SubMarkets.FirstOrDefault(s => s.Id == subMarketId);
        }
    }

    
    
    
    public class MarketManager
    {
        private static MarketManager? _instance;
        public static MarketManager Instance => _instance ??= new MarketManager();

        private readonly Dictionary<uint, Market> _markets = new();
        private readonly List<Market> _marketOrder = new();
        private readonly Dictionary<uint, MarketItem> _items = new();
        private string _scrollText = string.Empty;
        private string _marketDir = string.Empty;
        private uint _nextItemId = 1;
        private readonly object _lock = new();

        private MarketManager() { }

        
        
        
        public string GetMarketScrollText() => _scrollText;

        
        
        
        public MarketItem? GetItem(uint id)
        {
            return _items.TryGetValue(id, out var item) ? item : null;
        }

        
        
        
        public void LoadScrollText(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"市场滚动文字文件不存在: {filePath}");
                return;
            }

            try
            {
                
                _scrollText = SmartReader.ReadTextFile(filePath)
                    .Replace('\r', '\\')
                    .Replace('\n', '\\');
                LogManager.Default.Info($"加载市场滚动文字: {filePath} (长度: {_scrollText.Length})");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载市场滚动文字失败: {filePath}", exception: ex);
            }
        }

        
        
        
        public bool LoadMarkets(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"市场配置文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                Market? currentMarket = null;
                int marketCount = 0;
                int itemCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var trimmedLine = line.Trim();
                    
                    
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        var marketDef = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        var parts = marketDef.Split(':');
                        
                        if (parts.Length >= 2 && uint.TryParse(parts[0], out uint marketId))
                        {
                            currentMarket = new Market
                            {
                                Id = marketId,
                                Name = parts[1].Trim()
                            };
                            
                            if (parts.Length > 2)
                                currentMarket.Description = parts[2].Trim();
                            
                            _markets[marketId] = currentMarket;
                            marketCount++;
                        }
                    }
                    
                    else if (currentMarket != null && trimmedLine.Contains(","))
                    {
                        var parts = trimmedLine.Split(',');
                        if (parts.Length >= 6 && 
                            uint.TryParse(parts[0], out uint itemId) &&
                            uint.TryParse(parts[2], out uint price) &&
                            uint.TryParse(parts[3], out uint stock) &&
                            uint.TryParse(parts[4], out uint maxStock) &&
                            uint.TryParse(parts[5], out uint refreshInterval))
                        {
                            var item = new MarketItem
                            {
                                Id = itemId,
                                Name = parts[1].Trim(),
                                Price = price,
                                Stock = stock,
                                MaxStock = maxStock,
                                RefreshInterval = refreshInterval,
                                LastRefreshTime = 0
                            };
                            
                            currentMarket.Items.Add(item);
                            _items[itemId] = item;
                            itemCount++;
                        }
                    }
                }

                LogManager.Default.Info($"加载市场配置: {marketCount} 个市场, {itemCount} 个物品");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载市场配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        
        
        
        public bool LoadMainDirectory(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"市场主目录文件不存在: {filePath}");
                return false;
            }

            try
            {
                lock (_lock)
                {
                    _marketDir = Path.GetDirectoryName(filePath) ?? string.Empty;
                    _markets.Clear();
                    _marketOrder.Clear();
                    _items.Clear();
                    _nextItemId = 1;

                    int marketCount = 0;
                    int subCount = 0;
                    int itemCount = 0;

                    foreach (var raw in SmartReader.ReadAllLines(filePath))
                    {
                        if (string.IsNullOrWhiteSpace(raw))
                            continue;
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#"))
                            continue;

                        var eq = line.Split('=', 2);
                        if (eq.Length < 2)
                            continue;

                        string pageName = eq[0].Trim();
                        string subDef = eq[1].Trim();

                        uint marketId = GetIdFromPageName(pageName);
                        if (!_markets.TryGetValue(marketId, out var market))
                        {
                            market = new Market { Id = marketId, Name = pageName };
                            _markets[marketId] = market;
                            _marketOrder.Add(market);
                            marketCount++;
                        }

                        foreach (var token in subDef.Split('&', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var t = token.Trim();
                            if (t.Length == 0)
                                continue;

                            var parts = t.Split('|', 2);
                            if (parts.Length < 2)
                                continue;

                            if (!uint.TryParse(parts[0].Trim(), out uint subId))
                                continue;

                            string subName = parts[1].Trim();
                            var sub = new SubMarket(marketId, subId, subName);
                            market.SubMarkets.Add(sub);
                            subCount++;

                            itemCount += LoadSubMarketFile(sub);
                        }
                    }

                    LogManager.Default.Info($"加载市场主目录: {marketCount} 个市场, {subCount} 个子目录, {itemCount} 个物品");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载市场主目录失败: {filePath}", exception: ex);
                return false;
            }
        }

        private static uint GetIdFromPageName(string pageName)
        {
            
            return pageName switch
            {
                "首页" => 10,
                "喜庆" => 20,
                "百变" => 30,
                "礼包" => 40,
                "其他" => 50,
                _ => 60,
            };
        }

        private int LoadSubMarketFile(SubMarket subMarket)
        {
            if (string.IsNullOrEmpty(_marketDir))
                return 0;

            string fileName = $"{subMarket.MarketId:00}{subMarket.Id:00}.txt";
            string filePath = Path.Combine(_marketDir, fileName);
            if (!File.Exists(filePath))
                return 0; 

            int added = 0;
            foreach (var raw in SmartReader.ReadAllLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                var parts = line.Split('|');
                if (parts.Length < 7)
                    continue;

                if (!int.TryParse(parts[0].Trim(), out int image))
                    continue;
                int.TryParse(parts[1].Trim(), out int showImage);

                string viewName = parts[2].Trim();
                uint.TryParse(parts[3].Trim(), out uint price);
                uint.TryParse(parts[4].Trim(), out uint count);
                if (count == 0) count = 1;
                string itemName = parts[5].Trim();

                string tips = parts.Length == 7 ? parts[6].Trim() : string.Join("|", parts.Skip(6)).Trim();

                var item = new MarketItem
                {
                    Id = _nextItemId++,
                    MarketId = subMarket.MarketId,
                    SubMarketId = subMarket.Id,
                    Image = image,
                    ShowImage = showImage,
                    Name = viewName,
                    ItemName = itemName,
                    Price = price,
                    Count = count,
                    Tips = tips,
                };
                item.ProcessedTips = ProcItemTips(item.Tips);

                subMarket.Items.Add(item);
                _items[item.Id] = item;
                added++;
            }

            return added;
        }

        private static string ProcItemTips(string tips)
        {
            if (string.IsNullOrEmpty(tips))
                return string.Empty;

            
            var encoding = Encoding.GetEncoding("GBK");
            byte[] src = encoding.GetBytes(tips.Replace("\r", string.Empty).Replace("\n", string.Empty));

            var output = new List<byte>(src.Length + (src.Length / 20 + 1) * 4);
            int width = 0;
            bool bHz = false;
            for (int i = 0; i < src.Length; i++)
            {
                byte b = src[i];
                if (bHz) bHz = false;
                else if ((sbyte)b < 0) bHz = true;

                output.Add(b);
                width++;

                if (width == 20)
                {
                    if (bHz)
                    {
                        output[output.Count - 1] = (byte)'\\';
                        output.Add((byte)'\\');
                        output.Add(b);
                    }
                    else
                    {
                        output.Add((byte)'\\');
                        output.Add((byte)'\\');
                    }
                    width = 0;
                }
            }

            return encoding.GetString(output.ToArray());
        }

        
        
        
        public Market? AddMarket(uint id)
        {
            if (_markets.ContainsKey(id))
                return _markets[id];

            var market = new Market { Id = id };
            _markets[id] = market;
            return market;
        }

        
        
        
        public Market? GetMarket(uint marketId)
        {
            return _markets.TryGetValue(marketId, out var market) ? market : null;
        }

        
        
        
        public MarketItem NewItem()
        {
            return new MarketItem();
        }

        
        
        
        public void DeleteItem(MarketItem item)
        {
            if (item == null) return;
            
            _items.Remove(item.Id);
            
            
            foreach (var market in _markets.Values)
            {
                market.Items.RemoveAll(i => i.Id == item.Id);
                foreach (var sub in market.SubMarkets)
                {
                    sub.Items.RemoveAll(i => i.Id == item.Id);
                }
            }
        }

        
        
        
        public IEnumerable<Market> GetAllMarkets()
        {
            return _markets.Values;
        }

        
        
        
        public IEnumerable<MarketItem> GetAllItems()
        {
            return _items.Values;
        }

        
        
        
        
        public void OnClientMsg(HumanPlayer player, ushort wCmd, ushort wParam1, ushort wParam2, byte[] data, int dataSize)
        {
            if (player == null) return;

            string text = string.Empty;
            if (data != null && dataSize > 0)
            {
                try
                {
                    text = Encoding.GetEncoding("GBK")
                        .GetString(data, 0, Math.Min(dataSize, data.Length))
                        .TrimEnd('\0');
                }
                catch
                {
                    text = string.Empty;
                }
            }

            switch (wCmd)
            {
                case 1: 
                    OpenMarket(player);
                    break;
                case 2: 
                    if (uint.TryParse(text, out var marketId))
                        QueryMarket(player, marketId);
                    else
                        player.SendMsg(player.ObjectId, 0x1000, 2, 1, 0, "参数错误");
                    break;
                case 3: 
                    if (uint.TryParse(text, out var nId))
                        QuerySubMarket(player, nId / 100, nId % 100);
                    else
                        player.SendMsg(player.ObjectId, 0x1000, 3, 1, 0, "参数错误");
                    break;
                case 4: 
                    if (uint.TryParse(text, out var itemIdTips))
                        QueryItemTips(player, itemIdTips);
                    else
                        player.SendMsg(player.ObjectId, 0x1000, 4, 1, 0, "参数错误");
                    break;
                case 5: 
                    if (uint.TryParse(text, out var itemIdBuy))
                        QueryBuyItem(player, itemIdBuy);
                    else
                        player.SendMsg(player.ObjectId, 0x1000, 5, 0x1b, 0, "参数错误");
                    break;
            }
        }

        private void OpenMarket(HumanPlayer player)
        {
            lock (_lock)
            {
                uint id = player.ObjectId;

                
                player.SendMsg(id, 0x1000, 0, 0, 0, GetMarketScrollText());

                
                var sb = new StringBuilder(1024);
                sb.Append('&');
                for (uint i = 1; i <= 5; i++)
                {
                    var item = GetItem(i);
                    if (item == null) continue;
                    sb.Append($"{item.MarketId}{item.SubMarketId}{item.Id}|{item.Image}|100|{item.Name}|{item.Price}&");
                }
                player.SendMsg(id, 0x1000, 1, 0, 0, sb.ToString());

                
                if (_marketOrder.Count > 0)
                    QueryMarket(player, _marketOrder[0].Id);
            }
        }

        private void QueryMarket(HumanPlayer player, uint marketId)
        {
            lock (_lock)
            {
                if (!_markets.TryGetValue(marketId, out var market))
                {
                    player.SendMsg(player.ObjectId, 0x1000, 2, 1, 0, $"{marketId}商城不存在");
                    return;
                }

                var sb = new StringBuilder(1024);
                sb.Append($"{market.Id:00}&");
                foreach (var sub in market.SubMarkets)
                {
                    sb.Append($"{sub.Id:00}|{sub.Name}&");
                }
                player.SendMsg(player.ObjectId, 0x1000, 2, 0, 0, sb.ToString());

                if (market.SubMarkets.Count > 0)
                    QuerySubMarket(player, marketId, market.SubMarkets[0].Id);
            }
        }

        private void QuerySubMarket(HumanPlayer player, uint marketId, uint subMarketId)
        {
            lock (_lock)
            {
                if (!_markets.TryGetValue(marketId, out var market))
                {
                    player.SendMsg(player.ObjectId, 0x1000, 3, 1, 0, "该商城道具不存在");
                    return;
                }

                var sub = market.GetSubMarket(subMarketId);
                if (sub == null)
                {
                    player.SendMsg(player.ObjectId, 0x1000, 3, 1, 0, "该商城道具不存在");
                    return;
                }

                var sb = new StringBuilder(4096);
                sb.Append($"{marketId:00}{sub.Id:00}&");

                uint firstItemId = 0;
                foreach (var item in sub.Items)
                {
                    if (firstItemId == 0) firstItemId = item.Id;
                    sb.Append($"{item.Id}|{item.Image}|{item.ShowImage:00000}|{item.Name}|{item.Price}|{item.Count}&");
                }

                player.SendMsg(player.ObjectId, 0x1000, 3, 0, 0, sb.ToString());

                if (firstItemId != 0)
                    QueryItemTips(player, firstItemId);
            }
        }

        private void QueryItemTips(HumanPlayer player, uint itemId)
        {
            lock (_lock)
            {
                var item = GetItem(itemId);
                if (item == null)
                {
                    player.SendMsg(player.ObjectId, 0x1000, 4, 1, 0, "该商城道具不存在");
                    return;
                }

                player.SendMsg(player.ObjectId, 0x1000, 4, 0, 0, $"{item.Id}&{item.ProcessedTips}");
            }
        }

        private void QueryBuyItem(HumanPlayer player, uint itemId)
        {
            lock (_lock)
            {
                var item = GetItem(itemId);
                if (item == null)
                {
                    player.SendMsg(player.ObjectId, 0x1000, 5, 0x1b, 0, "该商城道具不存在");
                    return;
                }

                if (item.Price > player.Yuanbao)
                {
                    player.SendMsg(player.ObjectId, 0x1000, 5, 0x1b, 0, "你身上的元宝不够！");
                    return;
                }

                uint count = item.Count == 0 ? 1u : item.Count;

                
                int freeSlots = player.Inventory.MaxSlots - player.Inventory.GetUsedSlots();
                if (freeSlots < 0) freeSlots = 0;
                if (count > (uint)freeSlots)
                {
                    player.SendMsg(player.ObjectId, 0x1000, 5, 0x1b, 0, "你的包裹没有足够空间！");
                    return;
                }

                
                var def = ItemManager.Instance.GetDefinitionByName(item.ItemName)
                          ?? ItemManager.Instance.GetAllDefinitions()
                              .FirstOrDefault(d => string.Equals(d.Name, item.ItemName, StringComparison.OrdinalIgnoreCase));
                if (def == null)
                {
                    LogManager.Default.Warning($"商城物品模板不存在: itemId={item.Id}, itemName='{item.ItemName}'");
                    player.SendMsg(player.ObjectId, 0x1000, 5, 0x1b, 0, "该商城道具不存在");
                    return;
                }

                
                if (!player.TakeYuanbao(item.Price))
                {
                    player.SendMsg(player.ObjectId, 0x1000, 5, 0x1b, 0, "你身上的元宝不够！");
                    return;
                }

                
                for (uint i = 0; i < count; i++)
                {
                    var inst = ItemManager.Instance.CreateItem(def.ItemId, 1);
                    if (inst == null)
                    {
                        LogManager.Default.Warning($"商城创建物品失败: defId={def.ItemId}, name='{def.Name}'");
                        continue;
                    }

                    if (!player.Inventory.TryAddItemNoStack(inst, out _))
                    {
                        
                        LogManager.Default.Warning($"商城发放失败(背包满): player={player.Name}, item='{def.Name}'");
                        break;
                    }

                    player.SendAddBagItem(inst);
                }

                
                player.SendWeightChanged();

                player.SendMsg(player.ObjectId, 0x1000, 5, 0, 0, "恭喜购买成功");
            }
        }

        
        
        
        public void Update()
        {
            
            UpdateMarketItems();
        }

        
        
        
        private void UpdateMarketItems()
        {
            uint currentTime = (uint)Environment.TickCount;
            int refreshedCount = 0;

            foreach (var item in _items.Values)
            {
                
                if (item.RefreshInterval > 0 && 
                    currentTime - item.LastRefreshTime >= item.RefreshInterval)
                {
                    
                    item.Stock = item.MaxStock;
                    item.LastRefreshTime = currentTime;
                    refreshedCount++;
                }
            }

            if (refreshedCount > 0)
            {
                LogManager.Default.Debug($"刷新市场物品库存: {refreshedCount} 个物品");
            }
        }
    }
}
