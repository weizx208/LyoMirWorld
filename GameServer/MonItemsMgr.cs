using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class MonItemsMgr
    {
        private static MonItemsMgr? _instance;
        public static MonItemsMgr Instance => _instance ??= new MonItemsMgr();

        
        private readonly Dictionary<string, MonItems> _monItemsHash = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _hashLock = new();
        
        

        private MonItemsMgr()
        {
            
        }

        
        
        
        
        public bool LoadMonItems(string path)
        {
            LogManager.Default.Info($"加载怪物掉落配置文件: {path}");

            if (!Directory.Exists(path))
            {
                LogManager.Default.Error($"怪物掉落配置目录不存在: {path}");
                return false;
            }

            try
            {
                
                var files = Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in files)
                {
                    if (LoadMonItemsFile(file))
                    {
                        loadedCount++;
                    }
                }

                LogManager.Default.Info($"成功加载 {loadedCount} 个怪物掉落配置文件");
                return loadedCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物掉落配置文件失败: {path}", exception: ex);
                return false;
            }
        }

        
        
        
        
        private bool LoadMonItemsFile(string fileName)
        {
            try
            {
                
                string monsterName = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrEmpty(monsterName))
                {
                    LogManager.Default.Warning($"无法从文件名获取怪物名称: {fileName}");
                    return false;
                }

                
                MonItems? monItems;
                lock (_hashLock)
                {
                    if (_monItemsHash.TryGetValue(monsterName, out monItems))
                    {
                        
                        ClearDownItems(monItems);
                        LogManager.Default.Info($"更新怪物 {monsterName} 的物品掉落文件: {Path.GetFileName(fileName)}");
                    }
                    else
                    {
                        
                        monItems = new MonItems
                        {
                            MonsterName = monsterName,
                            FileName = fileName
                        };
                    }
                }

                
                var lines = SmartReader.ReadAllLines(fileName);
                int itemCount = 0;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    if (ParseDownItemLine(trimmedLine, out var downItem))
                    {
                        
                        downItem.Next = monItems.Items;
                        monItems.Items = downItem;
                        itemCount++;
                    }
                }

                
                lock (_hashLock)
                {
                    _monItemsHash[monsterName] = monItems;
                }

                
                return itemCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物掉落文件失败: {fileName}", exception: ex);
                return false;
            }
        }

        
        
        
        
        
        private bool ParseDownItemLine(string line, out DownItem downItem)
        {
            downItem = new DownItem();

            try
            {
                
                var parts = line.Split(new[] { ' ', '\t', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    LogManager.Default.Warning($"掉落物品格式错误: {line}");
                    return false;
                }

                
                if (!int.TryParse(parts[0], out int min) || !int.TryParse(parts[1], out int max))
                {
                    LogManager.Default.Warning($"掉落物品数量解析失败: {line}");
                    return false;
                }

                string itemName = parts[2];

                
                bool randomDura = false;
                if (itemName.StartsWith("*"))
                {
                    randomDura = true;
                    itemName = itemName.Substring(1);
                }

                
                bool isGold = false;
                string goldName = GameWorld.Instance.GetGameName("GoldName");
                if (string.Equals(itemName, goldName, StringComparison.OrdinalIgnoreCase))
                {
                    isGold = true;
                }
                else
                {
                    
                    var itemDefinitions = ItemManager.Instance.GetAllDefinitions();
                    var itemDefinition = itemDefinitions.FirstOrDefault(d => 
                        string.Equals(d.Name, itemName, StringComparison.OrdinalIgnoreCase));
                    
                    if (itemDefinition == null)
                    {
                        LogManager.Default.Warning($"掉落物品中出现未定义的物品: {itemName}");
                        return false;
                    }
                }

                
                int count = 1;
                int countMax = 1;

                if (parts.Length > 3)
                {
                    if (!int.TryParse(parts[3], out count))
                    {
                        count = 1;
                    }
                }

                if (parts.Length > 4)
                {
                    if (!int.TryParse(parts[4], out countMax))
                    {
                        countMax = count;
                    }
                }

                
                downItem.Name = itemName;
                downItem.Min = min;
                downItem.Max = max;
                downItem.Count = count;
                downItem.CountMax = countMax;
                downItem.RandomDura = randomDura;
                downItem.IsGold = isGold;
                downItem.Current = 0;

                
                Random random = new();
                downItem.CycleMax = random.Next((int)(max * 0.8), (int)(max * 1.3) + 1);
                downItem.Current = random.Next(downItem.CycleMax);

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析掉落物品行失败: {line}", exception: ex);
                return false;
            }
        }

        
        
        
        
        public MonItems? GetMonItems(string monsterName)
        {
            lock (_hashLock)
            {
                return _monItemsHash.TryGetValue(monsterName, out var monItems) ? monItems : null;
            }
        }

        
        
        
        
        public bool CreateDownItem(DownItem downItem, out ItemInstance item)
        {
            item = null!;

            try
            {
                if (downItem.IsGold)
                {
                    
                    
                    
                    int minCount = Math.Min(downItem.Count, downItem.CountMax);
                    int maxCount = Math.Max(downItem.Count, downItem.CountMax);
                    if (maxCount < 0) { minCount = 0; maxCount = 0; }

                    int count = maxCount == int.MaxValue
                        ? maxCount
                        : Random.Shared.Next(minCount, maxCount + 1);
                    if (count <= 0) count = 1;

                    string goldName = GameWorld.Instance.GetGameName("GoldName");
                    ushort imageIndex = GetGoldImageIndex(count);

                    
                    var goldDefinition = new ItemDefinition(0, goldName, ItemType.Other)
                    {
                        MaxStack = 1,
                        CanTrade = false,
                        CanDrop = true,
                        CanDestroy = false,
                        BuyPrice = 0,
                        SellPrice = 0,
                        StdMode = 255,
                        Shape = 0,
                        Image = imageIndex,
                        MaxDura = 0
                    };

                    uint makeIndex = ItemManager.Instance.AllocateTempMakeIndex();
                    item = new ItemInstance(goldDefinition, (long)makeIndex)
                    {
                        Count = 1,
                        Name = goldName
                    };

                    uint dwCount = (uint)count;
                    item.Durability = (int)(dwCount & 0xFFFF);
                    item.MaxDurability = (int)((dwCount >> 16) & 0xFFFF);

                    return true;
                }
                else
                {
                    
                    var itemDefinition = ItemManager.Instance.GetDefinitionByName(downItem.Name)
                        ?? ItemManager.Instance.GetAllDefinitions()
                            .FirstOrDefault(d => string.Equals(d.Name, downItem.Name, StringComparison.OrdinalIgnoreCase));
                    
                    if (itemDefinition == null)
                    {
                        LogManager.Default.Warning($"找不到物品定义: {downItem.Name}");
                        return false;
                    }
                    
                    int minCount = Math.Min(downItem.Count, downItem.CountMax);
                    int maxCount = Math.Max(downItem.Count, downItem.CountMax);
                    if (maxCount < 0) { minCount = 0; maxCount = 0; }

                    int count = maxCount == int.MaxValue
                        ? maxCount
                        : Random.Shared.Next(minCount, maxCount + 1);
                    if (count <= 0) count = 1;

                    
                    item = ItemManager.Instance.CreateItem(itemDefinition.ItemId, count);
                    if (item == null)
                    {
                        LogManager.Default.Warning($"创建掉落物品失败(ItemManager.CreateItem返回空): {downItem.Name}");
                        return false;
                    }

                    item.Name = downItem.Name;
                    
                    
                    if (downItem.RandomDura)
                    {
                        
                        int maxDura = itemDefinition.MaxDura > 0 ? itemDefinition.MaxDura : item.MaxDurability;
                        int minDura = Math.Min(downItem.Count, downItem.CountMax);
                        int maxDuraCfg = Math.Max(downItem.Count, downItem.CountMax);
                        if (maxDuraCfg < 0) { minDura = 0; maxDuraCfg = 0; }

                        int dura = maxDuraCfg == int.MaxValue
                            ? maxDuraCfg
                            : Random.Shared.Next(minDura, maxDuraCfg + 1);
                        item.MaxDurability = maxDura > 0 ? maxDura : 100;
                        item.Durability = Math.Clamp(dura, 1, item.MaxDurability);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"创建掉落物品失败: {downItem.Name}", exception: ex);
                return false;
            }
        }

        
        
        
        private ushort GetGoldImageIndex(int count)
        {
            if (count > 1000)
                return 0xE5;
            else if (count > 500)
                return 0xE4;
            else if (count > 300)
                return 0xE3;
            else if (count > 100)
                return 0xE2;
            else
                return 0xE1;
        }

        
        
        
        
        public bool UpdateDownItemCycle(DownItem downItem)
        {
            if (downItem == null)
                return false;

            downItem.Current++;
            if (downItem.Current >= downItem.CycleMax)
            {
                if (downItem.Max < 5)
                {
                    downItem.CycleMax = downItem.Max;
                }
                else
                {
                    Random random = new();
                    downItem.CycleMax = random.Next((int)(downItem.Max * 0.7f), (int)(downItem.Max * 1.3f) + 1);
                }
                downItem.Current = 0;
                return true;
            }
            return false;
        }

        
        
        
        public List<MonItems> GetAllMonItems()
        {
            lock (_hashLock)
            {
                return _monItemsHash.Values.ToList();
            }
        }

        
        
        
        public int GetMonItemsCount()
        {
            lock (_hashLock)
            {
                return _monItemsHash.Count;
            }
        }

        
        
        
        public void ClearAllMonItems()
        {
            lock (_hashLock)
            {
                foreach (var monItems in _monItemsHash.Values)
                {
                    ClearDownItems(monItems);
                }
                _monItemsHash.Clear();
            }
        }

        
        
        
        private void ClearDownItems(MonItems monItems)
        {
            if (monItems == null)
                return;

            var current = monItems.Items;
            while (current != null)
            {
                var next = current.Next;
                current.Next = null;
                current = next;
            }
            monItems.Items = null;
        }
    }

    
    
    
    public class DownItem
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public int CountMax { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int Current { get; set; }
        public int CycleMax { get; set; }
        public bool RandomDura { get; set; }
        public bool IsGold { get; set; }
        public byte[] Flag { get; set; } = new byte[2];
        public DownItem? Next { get; set; }
    }

    
    
    
    public class MonItems
    {
        public DownItem? Items { get; set; }
        public string MonsterName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
