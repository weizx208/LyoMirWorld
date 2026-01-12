namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    
    
    
    public enum ResourceType
    {
        Ore = 0,        
        Meat = 1,       
        Herb = 2,       
        Wood = 3,       
        Gem = 4         
    }

    
    
    
    public class ResourceNode
    {
        public uint NodeId { get; set; }
        public ResourceType Type { get; set; }
        public uint MapId { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public uint ResourceId { get; set; }    
        public string ResourceName { get; set; } = string.Empty;
        public uint MaxQuantity { get; set; }   
        public uint CurrentQuantity { get; set; } 
        public uint RespawnTime { get; set; }   
        public DateTime LastHarvestTime { get; set; }
        public uint HarvestCount { get; set; }  
        public bool IsActive { get; set; }

        public ResourceNode(uint nodeId, ResourceType type, uint mapId, ushort x, ushort y, uint resourceId, string resourceName)
        {
            NodeId = nodeId;
            Type = type;
            MapId = mapId;
            X = x;
            Y = y;
            ResourceId = resourceId;
            ResourceName = resourceName;
            MaxQuantity = 100;
            CurrentQuantity = MaxQuantity;
            RespawnTime = 300; 
            LastHarvestTime = DateTime.MinValue;
            HarvestCount = 0;
            IsActive = true;
        }

        
        
        
        public uint Harvest(uint amount)
        {
            if (!IsActive || CurrentQuantity == 0)
                return 0;

            uint harvested = Math.Min(amount, CurrentQuantity);
            CurrentQuantity -= harvested;
            LastHarvestTime = DateTime.Now;
            HarvestCount++;

            
            if (CurrentQuantity == 0)
            {
                IsActive = false;
            }

            return harvested;
        }

        
        
        
        public void Update()
        {
            if (!IsActive && (DateTime.Now - LastHarvestTime).TotalSeconds >= RespawnTime)
            {
                
                CurrentQuantity = MaxQuantity;
                IsActive = true;
                HarvestCount = 0;
            }
        }
    }

    
    
    
    public class HarvestingTool
    {
        public uint ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public ResourceType[] SupportedTypes { get; set; } = Array.Empty<ResourceType>();
        public float Efficiency { get; set; } = 1.0f;  
        public uint Durability { get; set; } = 100;    
        public uint MaxDurability { get; set; } = 100;

        public HarvestingTool(uint itemId, string name, ResourceType[] supportedTypes, float efficiency = 1.0f)
        {
            ItemId = itemId;
            Name = name;
            SupportedTypes = supportedTypes;
            Efficiency = efficiency;
        }

        
        
        
        public bool SupportsType(ResourceType type)
        {
            return SupportedTypes.Contains(type);
        }

        
        
        
        public bool Use()
        {
            if (Durability == 0)
                return false;

            Durability = Math.Max(0, Durability - 1);
            return true;
        }

        
        
        
        public void Repair()
        {
            Durability = MaxDurability;
        }
    }

    
    
    
    public class HarvestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public uint ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public uint Quantity { get; set; }
        public uint Experience { get; set; }
        public bool ToolBroken { get; set; }

        public HarvestResult(bool success, string message = "")
        {
            Success = success;
            Message = message;
        }
    }

    
    
    
    public class MiningSystem
    {
        private static MiningSystem? _instance;
        public static MiningSystem Instance => _instance ??= new MiningSystem();

        private readonly Dictionary<uint, ResourceNode> _resourceNodes = new();
        private readonly Dictionary<ResourceType, List<HarvestingTool>> _tools = new();
        private readonly Dictionary<uint, DateTime> _playerLastHarvestTime = new();
        private readonly object _lock = new();
        
        private uint _nextNodeId = 10000;
        private const uint HARVEST_COOLDOWN_SECONDS = 2; 

        private MiningSystem()
        {
            InitializeDefaultTools();
            InitializeDefaultResourceNodes();
        }

        
        
        
        private void InitializeDefaultTools()
        {
            
            var pickaxe = new HarvestingTool(5001, "矿镐", 
                new[] { ResourceType.Ore, ResourceType.Gem }, 1.2f);
            AddTool(pickaxe);

            
            var axe = new HarvestingTool(5002, "斧头", 
                new[] { ResourceType.Wood }, 1.1f);
            AddTool(axe);

            
            var knife = new HarvestingTool(5003, "小刀", 
                new[] { ResourceType.Meat }, 1.0f);
            AddTool(knife);

            
            var herbTool = new HarvestingTool(5004, "药锄", 
                new[] { ResourceType.Herb }, 1.3f);
            AddTool(herbTool);

            
            var universalTool = new HarvestingTool(5005, "万能工具", 
                Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().ToArray(), 0.8f);
            AddTool(universalTool);
        }

        
        
        
        private void InitializeDefaultResourceNodes()
        {
            
            AddResourceNode(ResourceType.Ore, 0, 100, 100, 6001, "铜矿石");
            AddResourceNode(ResourceType.Ore, 0, 120, 110, 6002, "铁矿石");
            AddResourceNode(ResourceType.Ore, 0, 140, 120, 6003, "银矿石");
            AddResourceNode(ResourceType.Ore, 0, 160, 130, 6004, "金矿石");

            
            AddResourceNode(ResourceType.Meat, 1, 200, 200, 6101, "猪肉");
            AddResourceNode(ResourceType.Meat, 1, 220, 210, 6102, "牛肉");
            AddResourceNode(ResourceType.Meat, 1, 240, 220, 6103, "羊肉");
            AddResourceNode(ResourceType.Meat, 1, 260, 230, 6104, "鸡肉");

            
            AddResourceNode(ResourceType.Herb, 2, 300, 300, 6201, "止血草");
            AddResourceNode(ResourceType.Herb, 2, 320, 310, 6202, "回蓝草");
            AddResourceNode(ResourceType.Herb, 2, 340, 320, 6203, "解毒草");
            AddResourceNode(ResourceType.Herb, 2, 360, 330, 6204, "经验草");

            
            AddResourceNode(ResourceType.Wood, 3, 400, 400, 6301, "松木");
            AddResourceNode(ResourceType.Wood, 3, 420, 410, 6302, "橡木");
            AddResourceNode(ResourceType.Wood, 3, 440, 420, 6303, "红木");
            AddResourceNode(ResourceType.Wood, 3, 460, 430, 6304, "紫檀木");

            
            AddResourceNode(ResourceType.Gem, 4, 500, 500, 6401, "红宝石");
            AddResourceNode(ResourceType.Gem, 4, 520, 510, 6402, "蓝宝石");
            AddResourceNode(ResourceType.Gem, 4, 540, 520, 6403, "绿宝石");
            AddResourceNode(ResourceType.Gem, 4, 560, 530, 6404, "钻石");

            LogManager.Default.Info($"已初始化 {_resourceNodes.Count} 个资源点");
        }

        
        
        
        private void AddResourceNode(ResourceType type, uint mapId, ushort x, ushort y, uint resourceId, string resourceName)
        {
            uint nodeId = _nextNodeId++;
            var node = new ResourceNode(nodeId, type, mapId, x, y, resourceId, resourceName);
            _resourceNodes[nodeId] = node;
        }

        
        
        
        private void AddTool(HarvestingTool tool)
        {
            foreach (var type in tool.SupportedTypes)
            {
                if (!_tools.ContainsKey(type))
                {
                    _tools[type] = new List<HarvestingTool>();
                }
                _tools[type].Add(tool);
            }
        }

        
        
        
        public HarvestResult Harvest(HumanPlayer player, uint nodeId, uint toolItemId = 0)
        {
            if (player == null)
                return new HarvestResult(false, "玩家不存在");

            
            if (!CanHarvest(player.ObjectId))
            {
                return new HarvestResult(false, "采集冷却中");
            }

            lock (_lock)
            {
                if (!_resourceNodes.TryGetValue(nodeId, out var node))
                    return new HarvestResult(false, "资源点不存在");

                
                if (!node.IsActive)
                    return new HarvestResult(false, "资源已耗尽");

                
                if (player.CurrentMap == null || player.CurrentMap.MapId != node.MapId)
                    return new HarvestResult(false, "距离太远");

                int distance = Math.Abs(player.X - node.X) + Math.Abs(player.Y - node.Y);
                if (distance > 2) 
                    return new HarvestResult(false, "距离太远");

                
                HarvestingTool? tool = null;
                if (toolItemId > 0)
                {
                    tool = GetTool(toolItemId, node.Type);
                    if (tool == null)
                        return new HarvestResult(false, "工具不支持此资源类型");
                }

                
                bool toolBroken = false;
                if (tool != null)
                {
                    if (!tool.Use())
                    {
                        toolBroken = true;
                        return new HarvestResult(false, "工具已损坏");
                    }
                }

                
                uint baseAmount = GetBaseHarvestAmount(player, node.Type);
                if (tool != null)
                {
                    baseAmount = (uint)(baseAmount * tool.Efficiency);
                }

                
                uint harvested = node.Harvest(baseAmount);
                if (harvested == 0)
                    return new HarvestResult(false, "采集失败");

                
                var item = ItemManager.Instance.CreateItem((int)node.ResourceId, (int)harvested);
                if (item == null)
                    return new HarvestResult(false, "物品创建失败");

                if (!player.AddItem(item))
                {
                    
                    return new HarvestResult(false, "背包已满");
                }

                
                uint exp = GetHarvestExperience(node.Type, harvested);
                player.AddExp(exp);

                
                _playerLastHarvestTime[player.ObjectId] = DateTime.Now;

                
                LogManager.Default.Info($"{player.Name} 采集了 {node.ResourceName} x{harvested}");

                return new HarvestResult(true, $"采集成功，获得 {node.ResourceName} x{harvested}")
                {
                    ItemId = node.ResourceId,
                    ItemName = node.ResourceName,
                    Quantity = harvested,
                    Experience = exp,
                    ToolBroken = toolBroken
                };
            }
        }

        
        
        
        public HarvestResult Mine(HumanPlayer player, uint nodeId, uint toolItemId = 0)
        {
            return Harvest(player, nodeId, toolItemId);
        }

        
        
        
        public HarvestResult GetMeat(HumanPlayer player, uint nodeId, uint toolItemId = 0)
        {
            return Harvest(player, nodeId, toolItemId);
        }

        
        
        
        private uint GetBaseHarvestAmount(HumanPlayer player, ResourceType type)
        {
            uint baseAmount = 1;

            
            switch (type)
            {
                case ResourceType.Ore:
                    baseAmount += (uint)(player.Level / 10);
                    break;
                case ResourceType.Meat:
                    baseAmount += (uint)(player.Level / 15);
                    break;
                case ResourceType.Herb:
                    baseAmount += (uint)(player.Level / 20);
                    break;
                case ResourceType.Wood:
                    baseAmount += (uint)(player.Level / 12);
                    break;
                case ResourceType.Gem:
                    baseAmount = 1; 
                    break;
            }

            
            Random rand = new Random();
            int randomBonus = rand.Next(0, 3); 
            baseAmount += (uint)randomBonus;

            return Math.Max(1, baseAmount);
        }

        
        
        
        private uint GetHarvestExperience(ResourceType type, uint quantity)
        {
            return type switch
            {
                ResourceType.Ore => 10 * quantity,
                ResourceType.Meat => 8 * quantity,
                ResourceType.Herb => 12 * quantity,
                ResourceType.Wood => 6 * quantity,
                ResourceType.Gem => 50 * quantity,
                _ => 5 * quantity
            };
        }

        
        
        
        private bool CanHarvest(uint playerId)
        {
            lock (_lock)
            {
                if (!_playerLastHarvestTime.TryGetValue(playerId, out var lastTime))
                    return true;

                var timeSinceLastHarvest = (DateTime.Now - lastTime).TotalSeconds;
                return timeSinceLastHarvest >= HARVEST_COOLDOWN_SECONDS;
            }
        }

        
        
        
        private HarvestingTool? GetTool(uint itemId, ResourceType type)
        {
            if (_tools.TryGetValue(type, out var toolList))
            {
                return toolList.FirstOrDefault(t => t.ItemId == itemId);
            }
            return null;
        }

        
        
        
        public List<ResourceNode> GetNearbyResourceNodes(HumanPlayer player, ResourceType? type = null, int maxDistance = 10)
        {
            if (player.CurrentMap == null)
                return new List<ResourceNode>();

            lock (_lock)
            {
                return _resourceNodes.Values
                    .Where(node => 
                        node.MapId == player.CurrentMap.MapId &&
                        node.IsActive &&
                        (type == null || node.Type == type) &&
                        Math.Abs(player.X - node.X) + Math.Abs(player.Y - node.Y) <= maxDistance)
                    .ToList();
            }
        }

        
        
        
        public ResourceNode? GetResourceNode(uint nodeId)
        {
            lock (_lock)
            {
                _resourceNodes.TryGetValue(nodeId, out var node);
                return node;
            }
        }

        
        
        
        public List<ResourceNode> GetAllResourceNodes()
        {
            lock (_lock)
            {
                return _resourceNodes.Values.ToList();
            }
        }

        
        
        
        public void UpdateResourceNodes()
        {
            lock (_lock)
            {
                foreach (var node in _resourceNodes.Values)
                {
                    node.Update();
                }
            }
        }

        
        
        
        public bool AddResourceNode(ResourceNode node)
        {
            lock (_lock)
            {
                if (_resourceNodes.ContainsKey(node.NodeId))
                    return false;

                _resourceNodes[node.NodeId] = node;
                return true;
            }
        }

        
        
        
        public bool RemoveResourceNode(uint nodeId)
        {
            lock (_lock)
            {
                return _resourceNodes.Remove(nodeId);
            }
        }

        
        
        
        public List<HarvestingTool> GetToolsForType(ResourceType type)
        {
            lock (_lock)
            {
                if (_tools.TryGetValue(type, out var toolList))
                {
                    return new List<HarvestingTool>(toolList);
                }
                return new List<HarvestingTool>();
            }
        }

        
        
        
        public List<HarvestingTool> GetAllTools()
        {
            lock (_lock)
            {
                var allTools = new List<HarvestingTool>();
                foreach (var toolList in _tools.Values)
                {
                    allTools.AddRange(toolList);
                }
                return allTools.DistinctBy(t => t.ItemId).ToList();
            }
        }

        
        
        
        public (int totalNodes, int activeNodes, int totalHarvests) GetStatistics()
        {
            lock (_lock)
            {
                int totalNodes = _resourceNodes.Count;
                int activeNodes = _resourceNodes.Values.Count(n => n.IsActive);
                int totalHarvests = _resourceNodes.Values.Sum(n => (int)n.HarvestCount);
                
                return (totalNodes, activeNodes, totalHarvests);
            }
        }

        
        
        
        public void ResetPlayerCooldown(uint playerId)
        {
            lock (_lock)
            {
                _playerLastHarvestTime.Remove(playerId);
            }
        }

        
        
        
        public DateTime? GetPlayerLastHarvestTime(uint playerId)
        {
            lock (_lock)
            {
                if (_playerLastHarvestTime.TryGetValue(playerId, out var lastTime))
                {
                    return lastTime;
                }
                return null;
            }
        }

        
        
        
        public void Cleanup()
        {
            lock (_lock)
            {
                
                var cutoffTime = DateTime.Now.AddHours(-24);
                var expiredPlayers = _playerLastHarvestTime
                    .Where(kv => kv.Value < cutoffTime)
                    .Select(kv => kv.Key)
                    .ToList();
                
                foreach (var playerId in expiredPlayers)
                {
                    _playerLastHarvestTime.Remove(playerId);
                }
            }
        }
    }
}
