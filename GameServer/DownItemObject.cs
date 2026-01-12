using System;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class DownItemObject : MapObject
    {
        private HumanPlayer? _actionObject;
        private uint _actionObjectInstanceKey;
        private ItemClass? _itemClass;
        private uint _scriptTimes;
        private readonly ServerTimer _timer;
        private uint _ownerId;
        private ItemInstance _item;
        private uint _id;

        
        
        
        public DownItemObject()
        {
            _id = 0;
            _timer = new ServerTimer();
            Clean();
        }

        
        
        
        
        public void Clean()
        {
            _id = 0;
            _itemClass = null;
            _item = null!;
            SetActionObject(null);
            _scriptTimes = 0;
        }

        
        
        
        
        public ItemInstance GetItem()
        {
            return _item;
        }

        
        
        
        
        public void SetItem(ItemInstance item)
        {
            _item = item;
        }

        
        
        
        
        public uint GetId()
        {
            return _id;
        }

        
        
        
        
        public void SetId(uint id)
        {
            _id = id;
        }

        
        
        
        
        public uint GetOwnerId()
        {
            return _ownerId;
        }

        
        
        
        
        public void SetOwnerId(uint id)
        {
            _ownerId = id;
        }

        
        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.DownItem;
        }

        
        
        
        
        public ServerTimer GetTimer()
        {
            return _timer;
        }

        
        
        
        
        public void OnDroped()
        {
            _timer.SaveTime();
            UpdateValid();
        }

        
        
        
        
        public void SetActionObject(HumanPlayer? player)
        {
            _actionObject = player;
            if (player != null)
            {
                _actionObjectInstanceKey = player.InstanceKey;
            }
            else
            {
                _actionObjectInstanceKey = 0;
            }
        }

        
        
        
        
        public bool IsGold()
        {
            if (_item == null)
                return false;

            
            
            try
            {
                if (((_item.GetMakeIndex() & 0x80000000) != 0) && _item.Definition != null)
                {
                    if (_item.Definition.StdMode == 255 && _item.Definition.Shape == 0)
                        return true;
                }
            }
            catch
            {
                
            }

            
            string itemName = _item.GetName() ?? string.Empty;
            return itemName.Contains("金币") || itemName.Contains("Gold", StringComparison.OrdinalIgnoreCase);
        }

        
        
        
        
        public uint GetGoldAmount()
        {
            if (!IsGold() || _item == null)
                return 0;

            
            
            try
            {
                return (uint)((_item.Durability & 0xffff) | ((_item.MaxDurability & 0xffff) << 16));
            }
            catch
            {
                return 0;
            }
        }

        
        
        
        
        public bool UpdateValid()
        {
            
            uint itemUpdateTime = 60000; 
            
            if (_timer.IsTimeOut(itemUpdateTime))
            {
                if (_ownerId == 0)
                {
                    
                    DownItemMgr.Instance?.DeleteGroundItem(CurrentMap as LogicMap, this);
                    return false;
                }
                else
                {
                    _ownerId = 0;
                    _timer.SaveTime();
                }
            }
            
            return true;
        }

        
        
        
        
        protected override void OnEnterMap(LogicMap map)
        {
            base.OnEnterMap(map);
            
            
            LogManager.Default.Debug($"掉落物品进入地图: 地图={map.MapId}, 位置=({X},{Y}), 物品={_item?.GetName()}");
            
            
            _itemClass = new ItemClass();
        }

        
        
        
        
        protected override void OnLeaveMap(LogicMap map)
        {
            
            LogManager.Default.Debug($"掉落物品离开地图: 地图={map.MapId}, 位置=({X},{Y}), 物品={_item?.GetName()}");
            
            base.OnLeaveMap(map);
        }

        
        
        
        
        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            
            
            
            
            
            
            if (_item == null)
            {
                msg = new byte[0];
                return false;
            }

            try
            {
                
                string itemName = _item.GetName() ?? string.Empty;
                byte[] nameBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(itemName);
                
                
                var outMsg = new MirCommon.MirMsgOrign
                {
                    dwFlag = _item.GetMakeIndex(),
                    wCmd = MirCommon.ProtocolCmd.SM_DOWNITEMAPPEAR,
                    wParam = new ushort[3] { X, Y, _item.GetImageIndex() },
                };

                msg = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(outMsg, nameBytes);
                return msg.Length > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取掉落物品可视消息失败: {ex.Message}");
                msg = new byte[0];
                return false;
            }
        }
        
        
        
        
        
        public bool GetOutViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            
            
            
            
            
            if (_item == null)
            {
                msg = new byte[0];
                return false;
            }

            try
            {
                var outMsg = new MirCommon.MirMsgOrign
                {
                    dwFlag = _item.GetMakeIndex(),
                    wCmd = MirCommon.ProtocolCmd.SM_DOWNITEMDISAPPEAR,
                    wParam = new ushort[3] { X, Y, 0 },
                };

                msg = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(outMsg, null);
                return msg.Length > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取掉落物品离开视野消息失败: {ex.Message}");
                msg = new byte[0];
                return false;
            }
        }

        
        
        
        
        public void SetDelTimer()
        {
            _timer.SetInterval(10000); 
        }

        
        
        
        
        public bool IsDelTimerTimeOut(uint timeout)
        {
            return _timer.IsTimeOut(timeout);
        }
    }

    
    
    
    public class ItemClass
    {
        public string DropPage { get; set; } = string.Empty;
        public uint DropPageDelay { get; set; }
        public uint DropPageExecuteTimes { get; set; }
        public string PickupPage { get; set; } = string.Empty;
    }
}
