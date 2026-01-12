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
    
    
    
    
    public class NpcManagerEx
    {
        private static NpcManagerEx? _instance;
        public static NpcManagerEx Instance => _instance ??= new NpcManagerEx();

        
        private readonly Dictionary<int, NpcDefinitionEx> _definitions = new();
        private readonly Dictionary<uint, NpcInstanceEx> _instances = new();
        private readonly Dictionary<int, List<uint>> _mapNpcs = new();
        private readonly Dictionary<uint, NpcInstanceEx> _dynamicNpcs = new();
        
        
        private readonly Queue<NpcInstanceEx> _updateQueue = new();
        private int _updateIndex = 0;
        
        
        private readonly Queue<NpcGoodsListEx> _goodsListPool = new();
        private readonly Queue<NpcGoodsItemListEx> _goodsItemListPool = new();
        
        private uint _nextInstanceId = 10000;
        private uint _nextDynamicId = 0x70000000;
        private readonly object _lock = new();

        private NpcManagerEx()
        {
            
        }

        
        
        
        
        public bool Load(string filename)
        {
            if (!File.Exists(filename))
            {
                LogManager.Default.Warning($"NPC配置文件不存在: {filename}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filename);
                int loadedCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    if (AddNpcFromString(line))
                        loadedCount++;
                }

                LogManager.Default.Info($"从 {filename} 加载了 {loadedCount} 个NPC");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载NPC配置文件失败 {filename}: {ex.Message}");
                return false;
            }
        }

        
        
        
        
        
        public bool AddNpcFromString(string npcString)
        {
            var parts = npcString.Split('/');
            if (parts.Length < 7)
            {
                LogManager.Default.Warning($"NPC字符串格式错误: {npcString}");
                return false;
            }

            string name = parts[0].Trim();
            if (!int.TryParse(parts[1].Trim(), out int dbId) ||
                !Helper.TryHexToInt(parts[2].Trim(), out int view) ||      
                !int.TryParse(parts[3].Trim(), out int mapId) ||
                !int.TryParse(parts[4].Trim(), out int x) ||
                !int.TryParse(parts[5].Trim(), out int y) ||
                !int.TryParse(parts[6].Trim(), out int canTalk))
            {
                LogManager.Default.Warning($"NPC字符串解析失败: {npcString}");
                return false;
            }

            
            if (canTalk == 0)
                return false;

            string scriptFile = parts.Length > 7 ? parts[7].Trim() : string.Empty;

            
            ScriptObject? scriptObject = null;
            if (!string.IsNullOrEmpty(scriptFile))
            {
                string scriptKey = Path.GetFileNameWithoutExtension(scriptFile);
                scriptObject = ScriptObjectMgr.Instance.GetScriptObject(scriptKey);
                if (scriptObject == null)
                {
                    
                    var allNames = ScriptObjectMgr.Instance.GetAllScriptObjectNames();
                    var found = allNames.FirstOrDefault(n => string.Equals(n, scriptKey, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(found))
                        scriptObject = ScriptObjectMgr.Instance.GetScriptObject(found);
                }

                if (scriptObject == null)
                {
                    
                    var available = ScriptObjectMgr.Instance.GetAllScriptObjectNames();
                    string sample = available.Count > 0 ? string.Join(", ", available.Take(20)) : "(none)";
                    LogManager.Default.Warning($"NPC脚本对象不存在: '{scriptFile}' (NPC: {name}). 已加载脚本样例: {sample}");
                    
                    
                }
            }

            
            var definition = new NpcDefinitionEx
            {
                NpcId = dbId,
                Name = name,
                ViewId = view,
                ScriptFile = scriptFile,
                ScriptObject = scriptObject
            };

            
            if (parts.Length > 8)
            {
                if (int.TryParse(parts[8], out int buyPercent))
                    definition.BuyPercent = buyPercent / 100.0f;
                
                if (parts.Length > 9 && int.TryParse(parts[9], out int sellPercent))
                    definition.SellPercent = sellPercent / 100.0f;
            }

            
            AddDefinition(definition);

            
            var npc = CreateNpc(dbId, mapId, x, y);
            if (npc == null)
                return false;

            
            return true;
        }

        
        
        
        public void AddDefinition(NpcDefinitionEx definition)
        {
            lock (_lock)
            {
                _definitions[definition.NpcId] = definition;
            }
        }

        
        
        
        public NpcDefinitionEx? GetDefinition(int npcId)
        {
            lock (_lock)
            {
                return _definitions.TryGetValue(npcId, out var definition) ? definition : null;
            }
        }

        
        
        
        public NpcInstanceEx? CreateNpc(int npcId, int mapId, int x, int y)
        {
            var definition = GetDefinition(npcId);
            if (definition == null)
                return null;

            lock (_lock)
            {
                
                uint seq = Interlocked.Increment(ref _nextInstanceId);
                uint instanceId = ObjectIdUtil.MakeObjectId(MirObjectType.NPC, seq);

                var npc = new NpcInstanceEx(definition, instanceId, mapId, x, y);

                _instances[instanceId] = npc;

                
                if (!_mapNpcs.ContainsKey(mapId))
                    _mapNpcs[mapId] = new List<uint>();
                _mapNpcs[mapId].Add(instanceId);

                
                var map = LogicMapMgr.Instance.GetLogicMapById((uint)mapId);
                if (map != null)
                {
                    map.AddObject(npc, x, y);
                }

                return npc;
            }
        }

        
        
        
        public NpcInstanceEx? GetNpc(uint instanceId)
        {
            lock (_lock)
            {
                return _instances.TryGetValue(instanceId, out var npc) ? npc : null;
            }
        }

        
        
        
        public List<NpcInstanceEx> GetMapNpcs(int mapId)
        {
            lock (_lock)
            {
                if (!_mapNpcs.TryGetValue(mapId, out var npcIds))
                    return new List<NpcInstanceEx>();

                return npcIds
                    .Select(id => GetNpc(id))
                    .Where(npc => npc != null)
                    .Cast<NpcInstanceEx>()
                    .ToList();
            }
        }

        
        
        
        public bool RemoveNpc(uint instanceId)
        {
            lock (_lock)
            {
                if (!_instances.TryGetValue(instanceId, out var npc))
                    return false;

                
                if (_mapNpcs.TryGetValue(npc.MapId, out var list))
                    list.Remove(instanceId);

                
                var map = LogicMapMgr.Instance.GetLogicMapById((uint)npc.MapId);
                if (map != null)
                {
                    map.RemoveObject(npc);
                }

                
                _instances.Remove(instanceId);

                return true;
            }
        }

        
        
        
        
        public bool AddDynamicNpc(uint ident, string name, uint viewId, uint mapId, uint x, uint y, string scriptFile)
        {
            var scriptObject = ScriptObjectMgr.Instance.GetScriptObject(scriptFile);
            if (scriptObject == null)
                return false;

            var map = LogicMapMgr.Instance.GetLogicMapById(mapId);
            if (map == null)
                return false;

            lock (_lock)
            {
                uint dynamicId = _nextDynamicId | ident;
                var definition = new NpcDefinitionEx
                {
                    NpcId = (int)ident,
                    Name = name,
                    ViewId = (int)viewId,
                    ScriptFile = scriptFile,
                    ScriptObject = scriptObject,
                    IsDynamic = true
                };

                var npc = new NpcInstanceEx(definition, dynamicId, (int)mapId, (int)x, (int)y);
                
                
                _dynamicNpcs[dynamicId] = npc;
                
                
                if (!map.AddObject(npc, (int)x, (int)y))
                {
                    _dynamicNpcs.Remove(dynamicId);
                    return false;
                }

                LogManager.Default.Debug($"动态NPC {name} 进入世界在({mapId})({x},{y})");
                return true;
            }
        }

        
        
        
        
        public bool RemoveDynamicNpc(uint ident)
        {
            lock (_lock)
            {
                uint dynamicId = _nextDynamicId | ident;
                if (!_dynamicNpcs.TryGetValue(dynamicId, out var npc))
                    return false;

                
                if (npc.CurrentMap != null)
                    npc.CurrentMap.RemoveObject(npc);

                
                npc.SaveItems();

                
                _dynamicNpcs.Remove(dynamicId);

                return true;
            }
        }

        
        
        
        
        public NpcInstanceEx? GetDynamicNpc(uint ident)
        {
            lock (_lock)
            {
                uint dynamicId = _nextDynamicId | ident;
                return _dynamicNpcs.TryGetValue(dynamicId, out var npc) ? npc : null;
            }
        }

        
        
        
        
        
        public void Update()
        {
            lock (_lock)
            {
                
                if (_updateQueue.Count == 0)
                {
                    foreach (var npc in _instances.Values)
                    {
                        _updateQueue.Enqueue(npc);
                    }
                    foreach (var npc in _dynamicNpcs.Values)
                    {
                        _updateQueue.Enqueue(npc);
                    }
                }
                
                
                if (_updateQueue.Count > 0)
                {
                    var npc = _updateQueue.Dequeue();
                    if (npc != null && npc.IsActive)
                    {
                        npc.Update();
                        
                        _updateQueue.Enqueue(npc);
                    }
                }
            }
        }

        
        
        
        
        public int GetCount()
        {
            lock (_lock)
            {
                return _instances.Count + _dynamicNpcs.Count;
            }
        }

        
        
        
        
        public NpcGoodsListEx AllocGoodsList()
        {
            lock (_lock)
            {
                if (_goodsListPool.Count > 0)
                    return _goodsListPool.Dequeue();
                
                return new NpcGoodsListEx();
            }
        }

        
        
        
        
        public void FreeGoodsList(NpcGoodsListEx goodsList)
        {
            lock (_lock)
            {
                goodsList.Clear();
                _goodsListPool.Enqueue(goodsList);
            }
        }

        
        
        
        
        public NpcGoodsItemListEx AllocGoodsItemList()
        {
            lock (_lock)
            {
                if (_goodsItemListPool.Count > 0)
                    return _goodsItemListPool.Dequeue();
                
                return new NpcGoodsItemListEx();
            }
        }

        
        
        
        
        public void FreeGoodsItemList(NpcGoodsItemListEx goodsItemList)
        {
            lock (_lock)
            {
                goodsItemList.Clear();
                _goodsItemListPool.Enqueue(goodsItemList);
            }
        }

        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        

        
        
        
        private void CreateDefaultNpcs()
        {
            
            var defaultNpcs = new[]
            {
                new NpcDefinitionEx { NpcId = 1001, Name = "武器商人", ViewId = 100, ScriptFile = "weaponshop" },
                new NpcDefinitionEx { NpcId = 1002, Name = "防具商人", ViewId = 101, ScriptFile = "armorshop" },
                new NpcDefinitionEx { NpcId = 1003, Name = "药店老板", ViewId = 102, ScriptFile = "potionshop" },
                new NpcDefinitionEx { NpcId = 1004, Name = "仓库管理员", ViewId = 103, ScriptFile = "storage" },
                new NpcDefinitionEx { NpcId = 1005, Name = "传送员", ViewId = 104, ScriptFile = "teleporter" },
                new NpcDefinitionEx { NpcId = 1006, Name = "铁匠", ViewId = 105, ScriptFile = "blacksmith" },
                new NpcDefinitionEx { NpcId = 1007, Name = "技能训练师", ViewId = 106, ScriptFile = "trainer" }
            };

            foreach (var definition in defaultNpcs)
            {
                AddDefinition(definition);
            }

            LogManager.Default.Info($"已加载 {_definitions.Count} 个NPC定义");
        }
    }

    
    
    
    public class NpcDefinitionEx
    {
        public int NpcId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ViewId { get; set; }
        public string ScriptFile { get; set; } = string.Empty;
        public ScriptObject? ScriptObject { get; set; }
        public float BuyPercent { get; set; } = 1.0f;
        public float SellPercent { get; set; } = 0.5f; 
        public bool IsDynamic { get; set; }
    }

    
    
    
    public class NpcInstanceEx : Npc
    {
        public uint InstanceId { get; set; }
        public NpcDefinitionEx Definition { get; set; }
        public bool IsActive { get; set; } = true;

        public NpcInstanceEx(NpcDefinitionEx definition, uint instanceId, int mapId, int x, int y)
            : base(definition.NpcId, definition.Name, ConvertToNpcType(definition))
        {
            Definition = definition;
            InstanceId = instanceId;
            ObjectId = instanceId;
            MapId = mapId;
            X = (ushort)x;
            Y = (ushort)y;
            ScriptFile = definition.ScriptFile;
            ImageIndex = definition.ViewId; 
        }

        private static NpcType ConvertToNpcType(NpcDefinitionEx definition)
        {
            
            if (!string.IsNullOrEmpty(definition.ScriptFile))
                return NpcType.Script;

            
            if (definition.ScriptFile.Contains("shop", StringComparison.OrdinalIgnoreCase))
                return NpcType.Merchant;
            if (definition.ScriptFile.Contains("storage", StringComparison.OrdinalIgnoreCase))
                return NpcType.Warehouse;
            if (definition.ScriptFile.Contains("teleport", StringComparison.OrdinalIgnoreCase))
                return NpcType.Teleporter;
            if (definition.ScriptFile.Contains("blacksmith", StringComparison.OrdinalIgnoreCase))
                return NpcType.Repair;
            
            return NpcType.Normal;
        }

        
        
        
        
        public new void Update()
        {
            
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (currentTime - _lastUpdateTime > 10000) 
            {
                _lastUpdateTime = currentTime;
                
                
                
                
                
                
                if (_hasChanged)
                {
                    SaveItems();
                    _hasChanged = false;
                }
            }
            
            
            base.Update();
        }

        
        
        
        
        public void SaveItems()
        {
            if (!_hasChanged)
                return;
                
            try
            {
                
                string saveDir = Path.Combine(".", "data", "Market_Save");
                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                }
                
                
                string filename = Path.Combine(saveDir, $"market_{Definition.NpcId:X8}.dat");
                
                
                
                File.WriteAllText(filename, $"NPC {Definition.Name} 的商品数据 - 保存时间: {DateTime.Now}", Encoding.GetEncoding("GBK"));
                
                LogManager.Default.Debug($"保存NPC {Definition.Name} 的商品数据到 {filename}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存NPC物品失败: {Definition.Name}, 错误: {ex.Message}");
            }
        }
        
        
        private long _lastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private bool _hasChanged = false;
    }

    
    
    
    public class NpcGoodsListEx
    {
        public List<int> ItemIds { get; set; } = new();

        public void Clear()
        {
            ItemIds.Clear();
        }
    }

    
    
    
    public class NpcGoodsItemListEx
    {
        public List<NpcGoodsItemEx> Items { get; set; } = new();

        public void Clear()
        {
            Items.Clear();
        }
    }

    
    
    
    public class NpcGoodsItemEx
    {
        public int ItemId { get; set; }
        public int Price { get; set; }
        public int Stock { get; set; }
    }

}
