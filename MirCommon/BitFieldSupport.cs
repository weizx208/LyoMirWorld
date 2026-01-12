using System;
using System.Runtime.InteropServices;

namespace MirCommon
{
    
    
    
    
    public static class BitFieldHelper
    {
        
        
        
        
        
        
        
        public static uint ExtractBits(uint value, int offset, uint mask)
        {
            return (value >> offset) & mask;
        }
        
        
        
        
        
        
        
        
        
        public static uint SetBits(uint original, uint bits, int offset, uint mask)
        {
            
            uint cleared = original & ~(mask << offset);
            
            return cleared | ((bits & mask) << offset);
        }
        
        
        
        
        
        
        public static uint CreateMask(int bitCount)
        {
            if (bitCount >= 32) return uint.MaxValue;
            return (1u << bitCount) - 1;
        }
    }
    
    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DeletedDateFixed
    {
        private uint _data;
        
        
        private const int YEAR_OFFSET = 0;
        private const uint YEAR_MASK = 0xFFFu;      
        private const int YEAR_BITS = 12;
        
        private const int MONTH_OFFSET = 12;
        private const uint MONTH_MASK = 0xFu;       
        private const int MONTH_BITS = 4;
        
        private const int DAY_OFFSET = 16;
        private const uint DAY_MASK = 0x1Fu;        
        private const int DAY_BITS = 5;
        
        private const int HOUR_OFFSET = 21;
        private const uint HOUR_MASK = 0xFu;        
        private const int HOUR_BITS = 4;
        
        private const int MINUTE_OFFSET = 25;
        private const uint MINUTE_MASK = 0x3Fu;     
        private const int MINUTE_BITS = 6;
        
        private const int BFLAG_OFFSET = 31;
        private const uint BFLAG_MASK = 0x1u;       
        private const int BFLAG_BITS = 1;
        
        
        
        
        public uint Year
        {
            get => BitFieldHelper.ExtractBits(_data, YEAR_OFFSET, YEAR_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, YEAR_OFFSET, YEAR_MASK);
        }
        
        
        
        
        public uint Month
        {
            get => BitFieldHelper.ExtractBits(_data, MONTH_OFFSET, MONTH_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, MONTH_OFFSET, MONTH_MASK);
        }
        
        
        
        
        public uint Day
        {
            get => BitFieldHelper.ExtractBits(_data, DAY_OFFSET, DAY_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, DAY_OFFSET, DAY_MASK);
        }
        
        
        
        
        public uint Hour
        {
            get => BitFieldHelper.ExtractBits(_data, HOUR_OFFSET, HOUR_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, HOUR_OFFSET, HOUR_MASK);
        }
        
        
        
        
        public uint Minute
        {
            get => BitFieldHelper.ExtractBits(_data, MINUTE_OFFSET, MINUTE_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, MINUTE_OFFSET, MINUTE_MASK);
        }
        
        
        
        
        public bool BFlag
        {
            get => BitFieldHelper.ExtractBits(_data, BFLAG_OFFSET, BFLAG_MASK) == 1;
            set => _data = BitFieldHelper.SetBits(_data, value ? 1u : 0u, BFLAG_OFFSET, BFLAG_MASK);
        }
        
        
        
        
        public uint RawData => _data;
        
        
        
        
        public void SetRawData(uint data)
        {
            _data = data;
        }
        
        
        
        
        public DeletedDateFixed()
        {
            _data = 0;
        }
        
        
        
        
        public DeletedDateFixed(uint year, uint month, uint day, uint hour, uint minute, bool bFlag = false)
        {
            _data = 0;
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
            BFlag = bFlag;
        }
        
        
        
        
        public DeletedDateFixed(uint rawData)
        {
            _data = rawData;
        }
        
        
        
        
        public DateTime? ToDateTime()
        {
            try
            {
                
                int year = (int)Year;
                int month = (int)Month;
                int day = (int)Day;
                int hour = (int)Hour;
                int minute = (int)Minute;
                
                
                if (year < 100) year += 2000;
                
                return new DateTime(year, month, day, hour, minute, 0);
            }
            catch
            {
                return null;
            }
        }
        
        
        
        
        public static DeletedDateFixed FromDateTime(DateTime dateTime, bool bFlag = false)
        {
            uint year = (uint)(dateTime.Year % 4096); 
            uint month = (uint)dateTime.Month;
            uint day = (uint)dateTime.Day;
            uint hour = (uint)dateTime.Hour;
            uint minute = (uint)dateTime.Minute;
            
            return new DeletedDateFixed(year, month, day, hour, minute, bFlag);
        }
        
        
        
        
        public bool Validate()
        {
            return Year <= YEAR_MASK &&
                   Month <= MONTH_MASK &&
                   Day <= DAY_MASK &&
                   Hour <= HOUR_MASK &&
                   Minute <= MINUTE_MASK;
        }
        
        
        
        
        public override string ToString()
        {
            return $"{Year:0000}-{Month:00}-{Day:00} {Hour:00}:{Minute:00} (BFlag={BFlag})";
        }
    }
    
    
    
    
    
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct UpgradeAddMaskFixed
    {
        
        [FieldOffset(0)]
        private uint _dwValue;
        
        
        [FieldOffset(0)]
        public ushort wAddMask;
        
        [FieldOffset(2)]
        public ushort wItemLimit;
        
        
        
        private const int ADDTYPE1_OFFSET = 0;
        private const uint ADDTYPE1_MASK = 0x7u;
        
        
        private const int ADDTYPE2_OFFSET = 3;
        private const uint ADDTYPE2_MASK = 0x7u;
        
        
        private const int ADDVALUE1_OFFSET = 6;
        private const uint ADDVALUE1_MASK = 0x3u;
        
        
        private const int ADDVALUE2_OFFSET = 8;
        private const uint ADDVALUE2_MASK = 0x3u;
        
        
        private const int ADDDURA_OFFSET = 10;
        private const uint ADDDURA_MASK = 0x3u;
        
        
        private const int BADDDURA_OFFSET = 12;
        private const uint BADDDURA_MASK = 0x1u;
        
        
        private const int FLAG_OFFSET = 13;
        private const uint FLAG_MASK = 0x7u;
        
        
        private const int LEFT_OFFSET = 16;
        private const uint LEFT_MASK = 0xFFFFu;
        
        
        
        
        public uint AddType1
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDTYPE1_OFFSET, ADDTYPE1_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDTYPE1_OFFSET, ADDTYPE1_MASK);
        }
        
        
        
        
        public uint AddType2
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDTYPE2_OFFSET, ADDTYPE2_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDTYPE2_OFFSET, ADDTYPE2_MASK);
        }
        
        
        
        
        public uint AddValue1
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDVALUE1_OFFSET, ADDVALUE1_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDVALUE1_OFFSET, ADDVALUE1_MASK);
        }
        
        
        
        
        public uint AddValue2
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDVALUE2_OFFSET, ADDVALUE2_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDVALUE2_OFFSET, ADDVALUE2_MASK);
        }
        
        
        
        
        public uint AddDura
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDDURA_OFFSET, ADDDURA_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDDURA_OFFSET, ADDDURA_MASK);
        }
        
        
        
        
        public bool BAddDura
        {
            get => BitFieldHelper.ExtractBits(_dwValue, BADDDURA_OFFSET, BADDDURA_MASK) == 1;
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value ? 1u : 0u, BADDDURA_OFFSET, BADDDURA_MASK);
        }
        
        
        
        
        public uint Flag
        {
            get => BitFieldHelper.ExtractBits(_dwValue, FLAG_OFFSET, FLAG_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, FLAG_OFFSET, FLAG_MASK);
        }
        
        
        
        
        public uint Left
        {
            get => BitFieldHelper.ExtractBits(_dwValue, LEFT_OFFSET, LEFT_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, LEFT_OFFSET, LEFT_MASK);
        }
        
        
        
        
        public UpgradeAddMaskFixed()
        {
            _dwValue = 0;
            wAddMask = 0;
            wItemLimit = 0;
        }
        
        
        
        
        public UpgradeAddMaskFixed(uint dwValue)
        {
            _dwValue = dwValue;
            wAddMask = (ushort)(dwValue & 0xFFFF);
            wItemLimit = (ushort)(dwValue >> 16);
        }
        
        
        
        
        public bool Validate()
        {
            return AddType1 <= ADDTYPE1_MASK &&
                   AddType2 <= ADDTYPE2_MASK &&
                   AddValue1 <= ADDVALUE1_MASK &&
                   AddValue2 <= ADDVALUE2_MASK &&
                   AddDura <= ADDDURA_MASK &&
                   Flag <= FLAG_MASK;
        }
        
        
        
        
        public override string ToString()
        {
            return $"AddType1={AddType1}, AddType2={AddType2}, AddValue1={AddValue1}, AddValue2={AddValue2}, " +
                   $"AddDura={AddDura}, BAddDura={BAddDura}, Flag={Flag}, Left={Left}";
        }
    }
    
    
    
    
    
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ServerIdFixed
    {
        [FieldOffset(0)]
        public uint dwId;
        
        [FieldOffset(0)]
        public byte bType;      
        
        [FieldOffset(1)]
        public byte bGroup;     
        
        [FieldOffset(2)]
        public byte bId;        
        
        [FieldOffset(3)]
        public byte bIndex;     
        
        
        
        
        public ServerIdFixed()
        {
            dwId = 0;
            bType = 0;
            bGroup = 0;
            bId = 0;
            bIndex = 0;
        }
        
        
        
        
        public ServerIdFixed(byte type, byte group, byte id, byte index)
        {
            dwId = 0;
            bType = type;
            bGroup = group;
            bId = id;
            bIndex = index;
        }
        
        
        
        
        public ServerIdFixed(uint id)
        {
            bType = 0;
            bGroup = 0;
            bId = 0;
            bIndex = 0;
            dwId = id;
        }
        
        
        
        
        public bool Validate()
        {
            
            return true;
        }
        
        
        
        
        public override string ToString()
        {
            return $"Type={bType}, Group={bGroup}, Id={bId}, Index={bIndex} (dwId=0x{dwId:X8})";
        }
    }
    
    
    
    
    public static class BitFieldTester
    {
        
        
        
        public static void TestAll()
        {
            Console.WriteLine("=== 位字段结构测试 ===");
            
            TestDeletedDate();
            TestUpgradeAddMask();
            TestServerId();
            TestBitFieldHelper();
        }
        
        
        
        
        private static void TestDeletedDate()
        {
            Console.WriteLine("\n--- 测试删除日期结构 ---");
            
            
            var date = new DeletedDateFixed(2025, 12, 17, 22, 53, true);
            Console.WriteLine($"创建日期: {date}");
            Console.WriteLine($"原始数据: 0x{date.RawData:X8}");
            Console.WriteLine($"验证: {date.Validate()}");
            
            
            uint rawData = 0x8C2B5A1F; 
            var date2 = new DeletedDateFixed(rawData);
            Console.WriteLine($"\n从原始数据创建: 0x{rawData:X8}");
            Console.WriteLine($"解析结果: {date2}");
            
            
            var dateTime = date.ToDateTime();
            Console.WriteLine($"\n转换为DateTime: {dateTime}");
            
            
            var now = DateTime.Now;
            var date3 = DeletedDateFixed.FromDateTime(now, true);
            Console.WriteLine($"\n从DateTime创建: {now}");
            Console.WriteLine($"创建结果: {date3}");
        }
        
        
        
        
        private static void TestUpgradeAddMask()
        {
            Console.WriteLine("\n--- 测试升级添加掩码结构 ---");
            
            
            var mask = new UpgradeAddMaskFixed();
            mask.AddType1 = 3;
            mask.AddType2 = 5;
            mask.AddValue1 = 2;
            mask.AddValue2 = 1;
            mask.AddDura = 3;
            mask.BAddDura = true;
            mask.Flag = 4;
            mask.Left = 0x1234;
            
            Console.WriteLine($"创建掩码: {mask}");
            Console.WriteLine($"wAddMask: 0x{mask.wAddMask:X4}");
            Console.WriteLine($"wItemLimit: 0x{mask.wItemLimit:X4}");
            Console.WriteLine($"验证: {mask.Validate()}");
            
            
            uint rawValue = 0x87654321;
            var mask2 = new UpgradeAddMaskFixed(rawValue);
            Console.WriteLine($"\n从原始值创建: 0x{rawValue:X8}");
            Console.WriteLine($"解析结果: {mask2}");
        }
        
        
        
        
        private static void TestServerId()
        {
            Console.WriteLine("\n--- 测试服务器ID结构 ---");
            
            
            var serverId1 = new ServerIdFixed(1, 2, 3, 4);
            Console.WriteLine($"从组件创建: {serverId1}");
            
            
            uint id = 0x04030201;
            var serverId2 = new ServerIdFixed(id);
            Console.WriteLine($"从DWORD创建 (0x{id:X8}): {serverId2}");
            
            
            serverId2.bType = 10;
            serverId2.bGroup = 20;
            Console.WriteLine($"修改后: {serverId2}");
            Console.WriteLine($"新的dwId: 0x{serverId2.dwId:X8}");
        }
        
        
        
        
        private static void TestBitFieldHelper()
        {
            Console.WriteLine("\n--- 测试位字段帮助类 ---");
            
            
            uint value = 0x12345678;
            uint extracted = BitFieldHelper.ExtractBits(value, 8, 0xFF);
            Console.WriteLine($"提取位字段: 0x{value:X8} >> 8 & 0xFF = 0x{extracted:X2}");
            
            
            uint modified = BitFieldHelper.SetBits(value, 0xAA, 16, 0xFF);
            Console.WriteLine($"设置位字段: 0x{value:X8} 设置16-23位为0xAA = 0x{modified:X8}");
            
            
            uint mask5 = BitFieldHelper.CreateMask(5);
            uint mask12 = BitFieldHelper.CreateMask(12);
            Console.WriteLine($"创建5位掩码: 0x{mask5:X8} ({mask5})");
            Console.WriteLine($"创建12位掩码: 0x{mask12:X8} ({mask12})");
        }
        
        
        
        
        public static void RunAllTests()
        {
            try
            {
                TestAll();
                Console.WriteLine("\n=== 所有测试完成 ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n=== 测试失败: {ex.Message} ===");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
