using System;

namespace GameServer
{
    public enum UsingItemResult
    {
        NoResult = 0,
        Updated = 1,
        Deleted = 2,
    }

    public partial class HumanPlayer
    {
        private ItemInstance? _usingItem;
        private UsingItemResult _usingItemResult = UsingItemResult.NoResult;
        private float _personalExpFactor = 1.0f; 

        public string GetTargetName() => Name ?? string.Empty;

        public uint GetTargetId() => ObjectId;

        public void ExecuteScriptAction(string action, params string[] parameters)
        {
            
        }

        public void SetUsingItem(ItemInstance? item)
        {
            _usingItem = item;
            _usingItemResult = UsingItemResult.NoResult;
        }

        public ItemInstance? GetUsingItem() => _usingItem;

        public UsingItemResult GetUsingItemResult() => _usingItemResult;

        public bool MarkUsingItemDeleted()
        {
            if (_usingItem == null)
                return false;

            _usingItemResult = UsingItemResult.Deleted;
            return true;
        }

        public bool DamageUsingItemDura(int damage)
        {
            if (_usingItem == null)
                return false;

            if (damage <= 0)
                return false;

            if (_usingItem.Durability > damage)
            {
                _usingItem.Durability -= damage;
                _usingItemResult = UsingItemResult.Updated;
            }
            else
            {
                _usingItem.Durability = 0;
                _usingItemResult = UsingItemResult.Deleted;
            }

            return true;
        }

        
        
        
        
        
        
        public uint CheckAndUpdateUsingItemTime()
        {
            if (_usingItem == null)
                return 0;

            uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_usingItem.UsingStartTime == 0)
            {
                _usingItem.UsingStartTime = now;
                _usingItemResult = UsingItemResult.Updated;
                return 1;
            }

            uint start = _usingItem.UsingStartTime;
            uint duration = (uint)Math.Max(0, _usingItem.Durability) * 86400u;
            if (now > start && duration > 0 && now - start >= duration)
            {
                _usingItemResult = UsingItemResult.Deleted;
                return 3;
            }

            return 2;
        }

        public void SetExpFactor(float factor)
        {
            
            if (float.IsNaN(factor) || float.IsInfinity(factor))
                factor = 1.0f;

            if (factor < 0.0f)
                factor = 0.0f;

            _personalExpFactor = factor;
        }

        public float GetExpFactor() => _personalExpFactor;

        public int GetExpFactor100()
        {
            
            return (int)MathF.Round(_personalExpFactor * 100.0f);
        }
    }
}
