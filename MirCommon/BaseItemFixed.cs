using System;
using System.Runtime.InteropServices;

namespace MirCommon
{
    
    
    
    
    
    
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct BaseItemFixed
    {
        
        [FieldOffset(0)]
        public byte btNameLength;
        
        
        [FieldOffset(1)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public byte[] szName;
        
        
        [FieldOffset(15)]
        public byte btStdMode;
        
        
        [FieldOffset(16)]
        public byte btShape;
        
        
        [FieldOffset(17)]
        public byte btWeight;
        
        
        [FieldOffset(18)]
        public byte btAniCount;
        
        
        [FieldOffset(19)]
        public byte btSpecialpower;
        
        
        [FieldOffset(20)]
        public byte bNeedIdentify;
        
        [FieldOffset(21)]
        public byte btPriceType;
        
        
        [FieldOffset(20)]
        public ushort wMapId;
        
        
        [FieldOffset(22)]
        public ushort wImageIndex;
        
        
        [FieldOffset(24)]
        public ushort wMaxDura;
        
        
        [FieldOffset(26)]
        public byte btMinDef;
        
        [FieldOffset(27)]
        public byte btMaxDef;
        
        [FieldOffset(28)]
        public byte btMinMagDef;
        
        [FieldOffset(29)]
        public byte btMaxMagDef;
        
        [FieldOffset(30)]
        public byte btMinAtk;
        
        [FieldOffset(31)]
        public byte btMaxAtk;
        
        [FieldOffset(32)]
        public byte btMinMagAtk;
        
        [FieldOffset(33)]
        public byte btMaxMagAtk;
        
        [FieldOffset(34)]
        public byte btMinSouAtk;
        
        [FieldOffset(35)]
        public byte btMaxSouAtk;
        
        
        [FieldOffset(26)]
        public byte Ac1;
        
        [FieldOffset(27)]
        public byte Ac2;
        
        [FieldOffset(28)]
        public byte Mac1;
        
        [FieldOffset(29)]
        public byte Mac2;
        
        [FieldOffset(30)]
        public byte Dc1;
        
        [FieldOffset(31)]
        public byte Dc2;
        
        [FieldOffset(32)]
        public byte Mc1;
        
        [FieldOffset(33)]
        public byte Mc2;
        
        [FieldOffset(34)]
        public byte Sc1;
        
        [FieldOffset(35)]
        public byte Sc2;
        
        
        [FieldOffset(26)]
        public ushort wAc;
        
        [FieldOffset(28)]
        public ushort wMac;
        
        [FieldOffset(30)]
        public ushort wDc;
        
        [FieldOffset(32)]
        public ushort wMc;
        
        [FieldOffset(34)]
        public ushort wSc;
        
        
        [FieldOffset(36)]
        public byte needtype;
        
        
        [FieldOffset(37)]
        public byte needvalue;
        
        
        [FieldOffset(38)]
        public ushort wUnknown;
        
        [FieldOffset(38)]
        public byte btFlag;
        
        [FieldOffset(39)]
        public byte btUpgradeTimes;
        
        
        [FieldOffset(40)]
        public int nPrice;
        
        
        
        
        public BaseItemFixed()
        {
            btNameLength = 0;
            szName = new byte[14];
            btStdMode = 0;
            btShape = 0;
            btWeight = 0;
            btAniCount = 0;
            btSpecialpower = 0;
            bNeedIdentify = 0;
            btPriceType = 0;
            wMapId = 0;
            wImageIndex = 0;
            wMaxDura = 0;
            btMinDef = 0;
            btMaxDef = 0;
            btMinMagDef = 0;
            btMaxMagDef = 0;
            btMinAtk = 0;
            btMaxAtk = 0;
            btMinMagAtk = 0;
            btMaxMagAtk = 0;
            btMinSouAtk = 0;
            btMaxSouAtk = 0;
            Ac1 = 0;
            Ac2 = 0;
            Mac1 = 0;
            Mac2 = 0;
            Dc1 = 0;
            Dc2 = 0;
            Mc1 = 0;
            Mc2 = 0;
            Sc1 = 0;
            Sc2 = 0;
            wAc = 0;
            wMac = 0;
            wDc = 0;
            wMc = 0;
            wSc = 0;
            needtype = 0;
            needvalue = 0;
            wUnknown = 0;
            btFlag = 0;
            btUpgradeTimes = 0;
            nPrice = 0;
        }
        
        
        
        
        public void SetName(string name)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(name);
            int length = Math.Min(bytes.Length, 13); 
            Array.Copy(bytes, 0, szName, 0, length);
            if (length < 14) szName[length] = 0; 
            btNameLength = (byte)length;
        }
        
        
        
        
        public string GetName()
        {
            int nullIndex = Array.IndexOf(szName, (byte)0);
            if (nullIndex < 0) nullIndex = Math.Min(szName.Length, btNameLength);
            return StringEncoding.GetGBKString(szName, 0, nullIndex);
        }
        
        
        
        
        public static int Size => 44;
        
        
        
        
        public static bool ValidateSize()
        {
            int csharpSize = Marshal.SizeOf<BaseItemFixed>();
            int cppSize = 44; 
            return csharpSize == cppSize;
        }
        
        
        
        
        public static bool ValidateFieldOffsets()
        {
            try
            {
                
                int btNameLengthOffset = Marshal.OffsetOf<BaseItemFixed>("btNameLength").ToInt32();
                int szNameOffset = Marshal.OffsetOf<BaseItemFixed>("szName").ToInt32();
                int btStdModeOffset = Marshal.OffsetOf<BaseItemFixed>("btStdMode").ToInt32();
                int nPriceOffset = Marshal.OffsetOf<BaseItemFixed>("nPrice").ToInt32();
                
                return btNameLengthOffset == 0 &&
                       szNameOffset == 1 &&
                       btStdModeOffset == 15 &&
                       nPriceOffset == 40;
            }
            catch
            {
                return false;
            }
        }
        
        
        
        
        public override string ToString()
        {
            return $"BaseItemFixed: Name={GetName()}, StdMode={btStdMode}, Price={nPrice}";
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ItemFixed
    {
        public BaseItemFixed baseitem;      
        public uint dwMakeIndex;            
        public ushort wCurDura;             
        public ushort wMaxDura;             
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] dwParam;              
        
        public ItemFixed()
        {
            baseitem = new BaseItemFixed();
            dwMakeIndex = 0;
            wCurDura = 0;
            wMaxDura = 0;
            dwParam = new uint[4];
        }
        
        
        
        
        public static int Size => 68;
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ItemClientFixed
    {
        public BaseItemFixed baseitem;      
        public uint dwMakeIndex;            
        public ushort wCurDura;             
        public ushort wMaxDura;             
        
        public ItemClientFixed()
        {
            baseitem = new BaseItemFixed();
            dwMakeIndex = 0;
            wCurDura = 0;
            wMaxDura = 0;
        }
        
        
        
        
        public static int Size => 52;
    }
    
    
    
    
    public static class DataStructureValidator
    {
        
        
        
        public static void ValidateAllStructures()
        {
            Console.WriteLine("=== 数据结构大小验证 ===");
            
            
            int baseItemSize = Marshal.SizeOf<BaseItemFixed>();
            Console.WriteLine($"BaseItemFixed: C#={baseItemSize}字节, C++=44字节, 匹配: {baseItemSize == 44}");
            
            
            int itemSize = Marshal.SizeOf<ItemFixed>();
            Console.WriteLine($"ItemFixed: C#={itemSize}字节, C++=68字节, 匹配: {itemSize == 68}");
            
            
            int itemClientSize = Marshal.SizeOf<ItemClientFixed>();
            Console.WriteLine($"ItemClientFixed: C#={itemClientSize}字节, C++=52字节, 匹配: {itemClientSize == 52}");
            
            
            Console.WriteLine($"\n=== 字段偏移量验证 ===");
            Console.WriteLine($"BaseItemFixed字段偏移量验证: {BaseItemFixed.ValidateFieldOffsets()}");
            
            
            ValidateKeyFields();
        }
        
        
        
        
        private static void ValidateKeyFields()
        {
            Console.WriteLine($"\n=== 关键字段验证 ===");
            
            var item = new ItemFixed();
            item.baseitem.SetName("TestItem");
            item.baseitem.btStdMode = 5;
            item.baseitem.nPrice = 1000;
            item.dwMakeIndex = 12345;
            item.wCurDura = 50;
            item.wMaxDura = 100;
            
            Console.WriteLine($"物品名称: {item.baseitem.GetName()}");
            Console.WriteLine($"标准模式: {item.baseitem.btStdMode}");
            Console.WriteLine($"价格: {item.baseitem.nPrice}");
            Console.WriteLine($"制造索引: {item.dwMakeIndex}");
            Console.WriteLine($"耐久: {item.wCurDura}/{item.wMaxDura}");
            
            
            item.baseitem.bNeedIdentify = 1;
            item.baseitem.btPriceType = 2;
            Console.WriteLine($"需要鉴定: {item.baseitem.bNeedIdentify}, 价格类型: {item.baseitem.btPriceType}");
            
            
            item.baseitem.wMapId = 1001;
            Console.WriteLine($"地图ID: {item.baseitem.wMapId}");
        }
        
        
        
        
        public static byte[] CreateTestBinaryData()
        {
            var item = new ItemFixed();
            
            
            item.baseitem.SetName("TestItem");
            item.baseitem.btStdMode = 5;
            item.baseitem.btShape = 1;
            item.baseitem.btWeight = 10;
            item.baseitem.btAniCount = 3;
            item.baseitem.btSpecialpower = 0;
            item.baseitem.bNeedIdentify = 1;
            item.baseitem.btPriceType = 0;
            item.baseitem.wImageIndex = 100;
            item.baseitem.wMaxDura = 100;
            item.baseitem.btMinDef = 5;
            item.baseitem.btMaxDef = 10;
            item.baseitem.btMinMagDef = 3;
            item.baseitem.btMaxMagDef = 6;
            item.baseitem.btMinAtk = 10;
            item.baseitem.btMaxAtk = 20;
            item.baseitem.btMinMagAtk = 5;
            item.baseitem.btMaxMagAtk = 10;
            item.baseitem.btMinSouAtk = 2;
            item.baseitem.btMaxSouAtk = 4;
            item.baseitem.needtype = 0;
            item.baseitem.needvalue = 10;
            item.baseitem.btFlag = 1;
            item.baseitem.btUpgradeTimes = 0;
            item.baseitem.nPrice = 1000;
            
            item.dwMakeIndex = 123456;
            item.wCurDura = 75;
            item.wMaxDura = 100;
            item.dwParam[0] = 1;
            item.dwParam[1] = 2;
            item.dwParam[2] = 3;
            item.dwParam[3] = 4;
            
            
            int size = Marshal.SizeOf<ItemFixed>();
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(item, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
    }
}
