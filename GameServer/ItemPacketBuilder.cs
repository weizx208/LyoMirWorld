using System;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    internal static class ItemPacketBuilder
    {
        internal static BaseItem BuildBaseItem(ItemInstance instance)
        {
            if (instance == null)
                return new BaseItem();

            string name = instance.Definition?.Name ?? instance.Name ?? string.Empty;
            int nameLen = GameEncoding.GBK.GetByteCount(name);
            if (nameLen > 13) nameLen = 13;
            if (nameLen < 0) nameLen = 0;

            static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);
            static ushort ClampUShort(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);

            var def = instance.Definition;
            if (def == null)
                return new BaseItem { btNameLength = (byte)nameLen, szName = name };

            var baseItem = new BaseItem
            {
                btNameLength = (byte)nameLen,
                szName = name,

                btStdMode = def.StdMode,
                btShape = (byte)Math.Clamp(def.Shape, 0, 255),
                btWeight = def.Weight,
                btAniCount = 0,
                btSpecialpower = unchecked((byte)def.SpecialPower),
                bNeedIdentify = 0,
                btPriceType = (def.StdMode == (byte)MirCommon.ItemStdMode.ISM_WEAPON0 ||
                               def.StdMode == (byte)MirCommon.ItemStdMode.ISM_WEAPON1)
                    ? def.StateView
                    : (byte)0,
                wImageIndex = instance.GetImageIndex(),
                
                wMaxDura = ClampUShort(instance.MaxDurability),

                Ac1 = ClampByte(def.MinAC),
                Ac2 = ClampByte(def.MaxAC),
                Mac1 = ClampByte(def.MinMAC),
                Mac2 = ClampByte(def.MaxMAC),
                Dc1 = ClampByte(def.MinDC),
                Dc2 = ClampByte(def.MaxDC),
                Mc1 = ClampByte(def.MinMC),
                Mc2 = ClampByte(def.MaxMC),
                Sc1 = ClampByte(def.MinSC),
                Sc2 = ClampByte(def.MaxSC),

                needtype = def.NeedType,
                needvalue = def.NeedLevel != 0 ? def.NeedLevel : ClampByte(def.RequireLevel),
                
                btFlag = (def.StdMode == (byte)MirCommon.ItemStdMode.ISM_DRESS_MALE ||
                          def.StdMode == (byte)MirCommon.ItemStdMode.ISM_DRESS_FEMALE)
                    ? (byte)(instance.DressColor & 0x0F)
                    : (byte)0,
                btUpgradeTimes = ClampByte(instance.EnhanceLevel),
                nPrice = (int)Math.Clamp((long)def.BuyPrice, int.MinValue, int.MaxValue),
            };

            
            if (instance.UsingStartTime != 0)
            {
                uint t = instance.UsingStartTime;
                baseItem.Ac1 = (byte)(t & 0xFF);
                baseItem.Ac2 = (byte)((t >> 8) & 0xFF);
                baseItem.Mac1 = (byte)((t >> 16) & 0xFF);
                baseItem.Mac2 = (byte)((t >> 24) & 0xFF);
            }

            return baseItem;
        }

        internal static ItemClient BuildItemClient(ItemInstance instance)
        {
            return new ItemClient
            {
                baseitem = BuildBaseItem(instance),
                dwMakeIndex = unchecked((uint)instance.InstanceId),
                wCurDura = (ushort)Math.Clamp(instance.Durability, 0, ushort.MaxValue),
                wMaxDura = (ushort)Math.Clamp(instance.MaxDurability, 0, ushort.MaxValue),
            };
        }

        internal static ITEMCLIENT BuildITEMCLIENT(ItemInstance instance)
        {
            return new ITEMCLIENT
            {
                baseitem = BuildBaseItem(instance),
                dwMakeIndex = unchecked((uint)instance.InstanceId),
                wCurDura = (ushort)Math.Clamp(instance.Durability, 0, ushort.MaxValue),
                wMaxDura = (ushort)Math.Clamp(instance.MaxDurability, 0, ushort.MaxValue),
            };
        }
    }
}
