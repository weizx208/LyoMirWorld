using System;
using System.Collections.Generic;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    
    public class DownItemMgr
    {
        private static DownItemMgr? _instance;
        
        
        
        
        public static DownItemMgr Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DownItemMgr();
                }
                return _instance;
            }
        }

        private uint _currentFreeIndex;
        private readonly ObjectPool<DownItemObject> _downItemPool;
        private readonly Dictionary<uint, DownItemObject> _downItemList;
        private readonly Queue<DownItemObject> _deleteItemQueue;
        private readonly object _lock = new();

        
        
        
        private DownItemMgr()
        {
            _currentFreeIndex = 0;
            _downItemPool = new ObjectPool<DownItemObject>(() => new DownItemObject(), 1000);
            _downItemList = new Dictionary<uint, DownItemObject>();
            _deleteItemQueue = new Queue<DownItemObject>(2000);
        }

        
        
        
        
        public DownItemObject? NewDownItemObject(ItemInstance item, ushort x, ushort y, uint ownerId = 0)
        {
            lock (_lock)
            {
                var downItem = _downItemPool.Get();
                if (downItem == null)
                {
                    LogManager.Default.Warning("掉落物品对象池为空");
                    return null;
                }

                
                uint id = GetNextId();
                if (id == 0)
                {
                    _downItemPool.Return(downItem);
                    return null;
                }

                downItem.SetId(id);
                downItem.SetItem(item);
                downItem.SetPosition(x, y);
                downItem.SetOwnerId(ownerId);

                _downItemList[id] = downItem;
                LogManager.Default.Debug($"创建掉落物品对象: ID={id}, 物品={item.Definition.Name}, 位置=({x},{y})");
                return downItem;
            }
        }

        
        
        
        
        public bool DeleteDownItemObject(DownItemObject? downItem)
        {
            if (downItem == null)
                return false;

            lock (_lock)
            {
                downItem.SetDelTimer();
                if (_deleteItemQueue.Count < 2000)
                {
                    _deleteItemQueue.Enqueue(downItem);
                    return true;
                }
                else
                {
                    
                    return DeleteDownItemObjectImmediate(downItem);
                }
            }
        }

        
        
        
        
        public bool DeleteDownItemObjectImmediate(DownItemObject? downItem)
        {
            if (downItem == null)
                return false;

            lock (_lock)
            {
                uint id = downItem.GetId();
                if (id > 0 && _downItemList.ContainsKey(id))
                {
                    _downItemList.Remove(id);
                }

                downItem.Clean();
                _downItemPool.Return(downItem);
                LogManager.Default.Debug($"立即删除掉落物品对象: ID={id}");
                return true;
            }
        }

        
        
        
        
        public bool DropItem(LogicMap map, ItemInstance item, ushort x, ushort y, uint ownerId = 0)
        {
            try
            {
                var downItem = NewDownItemObject(item, x, y, ownerId);
                if (downItem == null)
                {
                    LogManager.Default.Warning($"创建掉落物品失败: 物品={item.Definition.Name}, 位置=({x},{y})");
                    return false;
                }

                if (!map.AddObject(downItem, x, y))
                {
                    DeleteDownItemObjectImmediate(downItem);
                    LogManager.Default.Warning($"无法将掉落物品添加到地图: 地图={map.MapId}, 位置=({x},{y})");
                    return false;
                }

                downItem.OnDroped();
                LogManager.Default.Debug($"掉落物品到地图: 地图={map.MapId}, 物品={item.Definition.Name}, 位置=({x},{y}), 所有者={ownerId}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"掉落物品失败: 地图={map.MapId}, 位置=({x},{y})", exception: ex);
                return false;
            }
        }

        
        
        
        
        public bool HumanDropItem(LogicMap map, ItemInstance item, ushort x, ushort y, HumanPlayer actionPlayer)
        {
            try
            {
                var downItem = NewDownItemObject(item, x, y, 0); 
                if (downItem == null)
                {
                    LogManager.Default.Warning($"创建玩家掉落物品失败: 物品={item.Definition.Name}, 位置=({x},{y})");
                    return false;
                }

                if (!map.AddObject(downItem, x, y))
                {
                    DeleteDownItemObjectImmediate(downItem);
                    LogManager.Default.Warning($"无法将玩家掉落物品添加到地图: 地图={map.MapId}, 位置=({x},{y})");
                    return false;
                }

                downItem.SetActionObject(actionPlayer);
                downItem.OnDroped();
                LogManager.Default.Debug($"玩家掉落物品: 地图={map.MapId}, 玩家={actionPlayer.Name}, 物品={item.Definition.Name}, 位置=({x},{y})");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"玩家掉落物品失败: 地图={map.MapId}, 玩家={actionPlayer.Name}", exception: ex);
                return false;
            }
        }

        
        
        
        
        public bool PickupItem(LogicMap map, DownItemObject downItem, HumanPlayer actionPlayer)
        {
            try
            {
                
                uint ownerId = downItem.GetOwnerId();
                if (ownerId != 0 && ownerId != actionPlayer.GetDBId())
                {
                    
                    return false;
                }

                if (!map.RemoveObject(downItem))
                {
                    LogManager.Default.Warning($"无法从地图移除掉落物品: 地图={map.MapId}, 位置=({downItem.X},{downItem.Y})");
                    return false;
                }

                var item = downItem.GetItem();
                bool success = true;
                uint x = downItem.X;
                uint y = downItem.Y;

                if (downItem.IsGold())
                {
                    uint gold = downItem.GetGoldAmount();
                    if (gold == 0) gold = 1;

                    LogManager.Default.Debug($"玩家拾取金币: 玩家={actionPlayer.Name}, 金额={gold}, 位置=({x},{y})");
                    success = actionPlayer.AddGold(gold);

                    if (success)
                    {
                        DeleteItemFromManager(item);
                    }
                }
                else
                {
                    
                    
                    if (!actionPlayer.Inventory.TryAddItemNoStack(item, out _))
                    {
                        success = false;
                    }
                }

                if (success)
                {
                    DeleteDownItemObject(downItem);
                    LogManager.Default.Debug($"拾取物品成功: 玩家={actionPlayer.Name}, 物品={item.GetName()}, 位置=({x},{y})");
                }
                else
                {
                    
                    if (!map.AddObject(downItem, (int)x, (int)y))
                    {
                        DeleteItemFromManager(item);
                        DeleteDownItemObject(downItem);
                    }
                    LogManager.Default.Warning($"拾取物品失败: 玩家={actionPlayer.Name}, 物品={item.GetName()}, 位置=({x},{y})");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"拾取物品失败: 玩家={actionPlayer.Name}, 位置=({downItem.X},{downItem.Y})", exception: ex);
                return false;
            }
        }

        
        
        
        
        public bool DeleteGroundItem(LogicMap? map, DownItemObject downItem, bool deleteItem = true)
        {
            if (map == null)
                return false;

            try
            {
                if (!map.RemoveObject(downItem))
                {
                    LogManager.Default.Warning($"无法从地图移除地面物品: 地图={map.MapId}, 位置=({downItem.X},{downItem.Y})");
                    return false;
                }

                if (deleteItem)
                {
                    
                    DeleteItemFromManager(downItem.GetItem());
                }

                DeleteDownItemObject(downItem);
                LogManager.Default.Debug($"删除地面物品: 地图={map.MapId}, 位置=({downItem.X},{downItem.Y})");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"删除地面物品失败: 地图={map.MapId}, 位置=({downItem.X},{downItem.Y})", exception: ex);
                return false;
            }
        }

        
        
        
        
        public void UpdateDeletedObject()
        {
            lock (_lock)
            {
                if (_deleteItemQueue.Count == 0)
                    return;

                var downItem = _deleteItemQueue.Dequeue();
                if (downItem != null)
                {
                    
                    if (downItem.IsDelTimerTimeOut(10000))
                    {
                        DeleteDownItemObjectImmediate(downItem);
                    }
                    else
                    {
                        
                        _deleteItemQueue.Enqueue(downItem);
                    }
                }
            }
        }

        
        
        
        
        public void UpdateDownItem()
        {
            lock (_lock)
            {
                if (_downItemList.Count == 0)
                    return;

                uint count = 0;
                var keys = new List<uint>(_downItemList.Keys);
                
                foreach (var id in keys)
                {
                    if (count >= 100) 
                        break;

                    if (_downItemList.TryGetValue(id, out var downItem))
                    {
                        if (downItem.CurrentMap != null)
                        {
                            downItem.UpdateValid();
                        }
                        count++;
                    }
                }
            }
        }

        
        
        
        
        public int GetCount()
        {
            lock (_lock)
            {
                return _downItemList.Count;
            }
        }

        
        
        
        private uint GetNextId()
        {
            
            uint id = ++_currentFreeIndex;
            while (_downItemList.ContainsKey(id) && id < uint.MaxValue)
            {
                id = ++_currentFreeIndex;
            }

            if (id >= uint.MaxValue)
            {
                _currentFreeIndex = 1;
                id = 1;
                
                
                for (uint i = 1; i < uint.MaxValue; i++)
                {
                    if (!_downItemList.ContainsKey(i))
                    {
                        id = i;
                        _currentFreeIndex = i;
                        break;
                    }
                }
            }

            return id;
        }

        
        
        
        public DownItemObject? GetDownItemObject(uint id)
        {
            lock (_lock)
            {
                _downItemList.TryGetValue(id, out var downItem);
                return downItem;
            }
        }

        
        
        
        public List<DownItemObject> GetAllDownItemObjects()
        {
            lock (_lock)
            {
                return new List<DownItemObject>(_downItemList.Values);
            }
        }

        
        
        
        public void ClearAll()
        {
            lock (_lock)
            {
                foreach (var downItem in _downItemList.Values)
                {
                    if (downItem.CurrentMap != null)
                    {
                        downItem.CurrentMap.RemoveObject(downItem);
                    }
                    downItem.Clean();
                    _downItemPool.Return(downItem);
                }
                
                _downItemList.Clear();
                _deleteItemQueue.Clear();
                _currentFreeIndex = 0;
                
                LogManager.Default.Info("清理所有掉落物品");
            }
        }

        
        
        
        
        private ItemClass? GetItemClass(ItemInstance item)
        {
            
            
            return new ItemClass();
        }

        
        
        
        
        private void DeleteItemFromManager(ItemInstance item)
        {
            
            
            LogManager.Default.Debug($"从物品管理器删除物品: {item.Definition?.Name}, MakeIndex={item.GetMakeIndex()}");
        }
    }
}
