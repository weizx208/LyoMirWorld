using System;

namespace GameServer
{
    
    
    
    public class MapItem : MapObject
    {
        
        
        
        public ItemInstance Item { get; set; }
        
        
        
        
        public uint OwnerPlayerId { get; set; }
        
        
        
        
        public DateTime DropTime { get; set; }
        
        
        
        
        public DateTime? ExpireTime { get; set; }
        
        
        
        
        public bool CanBePicked { get; set; } = true;
        public int ProtectTime { get; internal set; }

        
        
        
        public MapItem(ItemInstance item)
        {
            Item = item;
            DropTime = DateTime.Now;
            
            ExpireTime = DropTime.AddMinutes(30);
        }
        
        
        
        
        public bool CanPickup(uint playerId)
        {
            if (!CanBePicked)
                return false;
                
            
            if (OwnerPlayerId > 0 && OwnerPlayerId != playerId)
            {
                
                if (DateTime.Now < DropTime.AddSeconds(30))
                    return false;
            }
            
            return true;
        }
        
        
        
        
        public override ObjectType GetObjectType()
        {
            return ObjectType.Item;
        }
        
        
        
        
        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            
            
            msg = Array.Empty<byte>();
            return false;
        }
        
        
        
        
        public override void Update()
        {
            base.Update();
            
            
            if (ExpireTime.HasValue && DateTime.Now >= ExpireTime.Value)
            {
                
                CurrentMap?.RemoveObject(this);
            }
        }

        internal int GetRemainingProtectTime()
        {
            throw new NotImplementedException();
        }
    }
}
