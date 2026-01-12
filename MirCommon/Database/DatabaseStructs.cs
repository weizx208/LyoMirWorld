using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MirCommon.Database
{
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CREATECHARDESC
    {
        public uint dwKey;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szName;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szServer;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string szAccount;
        
        public byte btClass;
        public byte btSex;
        public byte btHair;
        public byte btLevel;
        
        public CREATECHARDESC(uint key, string account, string server, string name, byte job, byte sex, byte hair, byte level)
        {
            dwKey = key;
            szAccount = account;
            szServer = server;
            szName = name;
            btClass = job;
            btSex = sex;
            btHair = hair;
            btLevel = level;
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct tQueryCharList_Result
    {
        public int count;
        public IntPtr pData; 
        
        public SelectCharList[] GetCharacters()
        {
            if (count == 0 || pData == IntPtr.Zero)
                return Array.Empty<SelectCharList>();
                
            var result = new SelectCharList[count];
            int size = Marshal.SizeOf<SelectCharList>();
            
            for (int i = 0; i < count; i++)
            {
                IntPtr ptr = new IntPtr(pData.ToInt64() + i * size);
                result[i] = Marshal.PtrToStructure<SelectCharList>(ptr);
            }
            
            return result;
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct tQueryMapPosition_Result
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szMapName;
        
        public short x;
        public short y;
    }

    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CHARDBINFO
    {
        public uint dwClientKey;         
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szName;            
        
        public uint dwDBId;              
        public uint mapid;               
        public ushort x;                 
        public ushort y;                 
        public uint dwGold;              
        public uint dwYuanbao;           
        public uint dwCurExp;            
        public ushort wLevel;            
        public byte btClass;             
        public byte btHair;              
        public byte btSex;               
        public byte flag;                
        
        public ushort hp;                
        public ushort mp;                
        public ushort maxhp;             
        public ushort maxmp;             
        
        public byte mindc;               
        public byte maxdc;               
        public byte minmc;               
        public byte maxmc;               
        public byte minsc;               
        public byte maxsc;               
        public byte minac;               
        public byte maxac;               
        public byte minmac;              
        public byte maxmac;              
        
        public ushort weight;            
        public byte handweight;          
        public byte bodyweight;          
        
        public uint dwForgePoint;        
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] dwProp;            
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] dwFlag;            
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
        public string szStartPoint;      
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szGuildName;       
        
        public static int Size => Marshal.SizeOf<CHARDBINFO>();
        
        
        
        
        public CHARDBINFO()
        {
            dwProp = new uint[8];
            dwFlag = new uint[4];
            szName = "";
            szStartPoint = "";
            szGuildName = "";
        }
        
        
        
        
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[Size];
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.StructureToPtr(this, ptr, false);
                Marshal.Copy(ptr, buffer, 0, Size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
        
        
        
        
        public static CHARDBINFO FromBytes(byte[] data)
        {
            if (data.Length < Size)
                throw new ArgumentException($"数据长度不足，需要{Size}字节，实际{data.Length}字节");
                
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.Copy(data, 0, ptr, Size);
                return Marshal.PtrToStructure<CHARDBINFO>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DBITEM
    {
        public Item item;                
        public ushort wPos;              
        public byte btFlag;              
        
        public static int Size => Marshal.SizeOf<DBITEM>();
        
        
        
        
        public static DBITEM FromItem(Item item, uint ownerId, byte flag, ushort pos, uint findKey = 0)
        {
            
            
            return new DBITEM
            {
                item = item,
                wPos = pos,
                btFlag = flag
            };
        }
        
        
        
        
        public Item ToItem()
        {
            return item;
        }
        
        
        
        
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[Size];
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.StructureToPtr(this, ptr, false);
                Marshal.Copy(ptr, buffer, 0, Size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
        
        
        
        
        public static DBITEM FromBytes(byte[] data)
        {
            if (data.Length < Size)
                throw new ArgumentException($"数据长度不足，需要{Size}字节，实际{data.Length}字节");
                
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.Copy(data, 0, ptr, Size);
                return Marshal.PtrToStructure<DBITEM>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MAGICDB
    {
        public byte btUserKey;           
        public byte btCurLevel;          
        public ushort wMagicId;          
        public uint dwCurTrain;          
        
        public static int Size => Marshal.SizeOf<MAGICDB>();
        
        
        
        
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[Size];
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.StructureToPtr(this, ptr, false);
                Marshal.Copy(ptr, buffer, 0, Size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
        
        
        
        
        public static MAGICDB FromBytes(byte[] data)
        {
            if (data.Length < Size)
                throw new ArgumentException($"数据长度不足，需要{Size}字节，实际{data.Length}字节");
                
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.Copy(data, 0, ptr, Size);
                return Marshal.PtrToStructure<MAGICDB>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BAGITEMPOS
    {
        public uint dwItemIndex;         
        public byte btFlag;              
        public ushort wPos;              
        
        public static int Size => Marshal.SizeOf<BAGITEMPOS>();
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ExecSqlRecord
    {
        public int fieldCount;           
        public IntPtr fieldTypes;        
        public IntPtr fieldNames;        
        
        public static int Size => Marshal.SizeOf<ExecSqlRecord>();
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TaskInfo
    {
        public uint dwOwner;             
        public uint dwTaskId;            
        public uint dwState;             
        public uint dwParam1;            
        public uint dwParam2;            
        public uint dwParam3;            
        public uint dwParam4;            
        
        public static int Size => Marshal.SizeOf<TaskInfo>();
        
        
        
        
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[Size];
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.StructureToPtr(this, ptr, false);
                Marshal.Copy(ptr, buffer, 0, Size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
        
        
        
        
        public static TaskInfo FromBytes(byte[] data)
        {
            if (data.Length < Size)
                throw new ArgumentException($"数据长度不足，需要{Size}字节，实际{data.Length}字节");
                
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.Copy(data, 0, ptr, Size);
                return Marshal.PtrToStructure<TaskInfo>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    
    
    
    public static class DatabaseSerializer
    {
        
        
        
        public static byte[] SerializeCharDbInfos(CHARDBINFO[] infos)
        {
            if (infos == null || infos.Length == 0)
                return Array.Empty<byte>();
                
            int size = CHARDBINFO.Size;
            byte[] buffer = new byte[size * infos.Length];
            
            for (int i = 0; i < infos.Length; i++)
            {
                byte[] charData = infos[i].ToBytes();
                Array.Copy(charData, 0, buffer, i * size, size);
            }
            
            return buffer;
        }
        
        
        
        
        public static CHARDBINFO[] DeserializeCharDbInfos(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<CHARDBINFO>();
                
            int size = CHARDBINFO.Size;
            if (data.Length % size != 0)
                throw new ArgumentException($"数据长度必须是{size}的倍数");
                
            int count = data.Length / size;
            var result = new CHARDBINFO[count];
            
            for (int i = 0; i < count; i++)
            {
                byte[] charData = new byte[size];
                Array.Copy(data, i * size, charData, 0, size);
                result[i] = CHARDBINFO.FromBytes(charData);
            }
            
            return result;
        }
        
        
        
        
        public static byte[] SerializeDbItems(DBITEM[] items)
        {
            if (items == null || items.Length == 0)
                return Array.Empty<byte>();
                
            int size = DBITEM.Size;
            byte[] buffer = new byte[size * items.Length];
            
            for (int i = 0; i < items.Length; i++)
            {
                byte[] itemData = items[i].ToBytes();
                Array.Copy(itemData, 0, buffer, i * size, size);
            }
            
            return buffer;
        }
        
        
        
        
        public static DBITEM[] DeserializeDbItems(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<DBITEM>();
                
            int size = DBITEM.Size;
            if (data.Length % size != 0)
                throw new ArgumentException($"数据长度必须是{size}的倍数");
                
            int count = data.Length / size;
            var result = new DBITEM[count];
            
            for (int i = 0; i < count; i++)
            {
                byte[] itemData = new byte[size];
                Array.Copy(data, i * size, itemData, 0, size);
                result[i] = DBITEM.FromBytes(itemData);
            }
            
            return result;
        }
        
        
        
        
        public static byte[] SerializeMagicDbs(MAGICDB[] magics)
        {
            if (magics == null || magics.Length == 0)
                return Array.Empty<byte>();
                
            int size = MAGICDB.Size;
            byte[] buffer = new byte[size * magics.Length];
            
            for (int i = 0; i < magics.Length; i++)
            {
                byte[] magicData = magics[i].ToBytes();
                Array.Copy(magicData, 0, buffer, i * size, size);
            }
            
            return buffer;
        }
        
        
        
        
        public static MAGICDB[] DeserializeMagicDbs(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<MAGICDB>();
                
            int size = MAGICDB.Size;
            if (data.Length % size != 0)
                throw new ArgumentException($"数据长度必须是{size}的倍数");
                
            int count = data.Length / size;
            var result = new MAGICDB[count];
            
            for (int i = 0; i < count; i++)
            {
                byte[] magicData = new byte[size];
                Array.Copy(data, i * size, magicData, 0, size);
                result[i] = MAGICDB.FromBytes(magicData);
            }
            
            return result;
        }
        
        
        
        
        public static byte[] SerializeTaskInfos(TaskInfo[] tasks)
        {
            if (tasks == null || tasks.Length == 0)
                return Array.Empty<byte>();
                
            int size = TaskInfo.Size;
            byte[] buffer = new byte[size * tasks.Length];
            
            for (int i = 0; i < tasks.Length; i++)
            {
                byte[] taskData = tasks[i].ToBytes();
                Array.Copy(taskData, 0, buffer, i * size, size);
            }
            
            return buffer;
        }
        
        
        
        
        public static TaskInfo[] DeserializeTaskInfos(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<TaskInfo>();
                
            int size = TaskInfo.Size;
            if (data.Length % size != 0)
                throw new ArgumentException($"数据长度必须是{size}的倍数");
                
            int count = data.Length / size;
            var result = new TaskInfo[count];
            
            for (int i = 0; i < count; i++)
            {
                byte[] taskData = new byte[size];
                Array.Copy(data, i * size, taskData, 0, size);
                result[i] = TaskInfo.FromBytes(taskData);
            }
            
            return result;
        }
    }
}
