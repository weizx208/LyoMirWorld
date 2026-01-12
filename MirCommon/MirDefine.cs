using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MirCommon
{
    
    
    
    public static class StringEncoding
    {
        private static Encoding? _gbkEncoding;

        
        
        
        public static Encoding GBK
        {
            get
            {
                if (_gbkEncoding == null)
                {
                    try
                    {
                        
                        _gbkEncoding = Encoding.GetEncoding("GBK");
                    }
                    catch
                    {
                        
                        _gbkEncoding = Encoding.ASCII;
                    }
                }
                return _gbkEncoding;
            }
        }

        
        
        
        public static byte[] GetGBKBytes(string str)
        {
            return GBK.GetBytes(str);
        }

        
        
        
        public static string GetGBKString(byte[] bytes, int index, int count)
        {
            return GBK.GetString(bytes, index, count);
        }

        
        
        
        public static string GetGBKString(byte[] bytes)
        {
            int nullIndex = Array.IndexOf(bytes, (byte)0);
            if (nullIndex < 0) nullIndex = bytes.Length;
            return GBK.GetString(bytes, 0, nullIndex);
        }
    }

    
    
    
    public static class MirDefine
    {
        public const int MSGHEADERSIZE = 12;
        public const int BAGSIZE = 40;
        public const int BELTSIZE = 6;
        public const int ALLBAGSIZE = BAGSIZE + BELTSIZE;
        public const int MAXEQUIPMENTPOS = 9;
    }

    
    
    
    public enum MirWorldError
    {
        ME_FAIL,
        ME_OK,
        ME_SOCKETWOULDBLOCK,
        ME_SOCKETCLOSED,
    }

    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MirMsg
    {
        public uint dwFlag;
        public ushort wCmd;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public ushort[] wParam;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] data;

        public MirMsg()
        {
            dwFlag = 0;
            wCmd = 0;
            wParam = new ushort[3];
            data = new byte[4];
        }

        public static int Size => 16; 
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MirMsgHeader
    {
        public uint dwFlag;
        public ushort wCmd;
        public ushort w1;
        public ushort w2;
        public ushort w3;

        public MirMsgHeader(uint flag, ushort cmd, ushort w1, ushort w2, ushort w3)
        {
            this.dwFlag = flag;
            this.wCmd = cmd;
            this.w1 = w1;
            this.w2 = w2;
            this.w3 = w3;
        }
    }

    #region C++原结构（客户端通讯专用）

    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MirMsgOrign
    {
        public uint dwFlag;
        public ushort wCmd;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public ushort[] wParam;

        public MirMsgOrign()
        {
            dwFlag = 0;
            wCmd = 0;
            wParam = new ushort[3];
        }

        public static int Size => 12; 
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MirMsgHeaderOrign
    {
        public uint dwFlag;
        public ushort wCmd;
        public ushort w1;
        public ushort w2;
        public ushort w3;

        public MirMsgHeaderOrign(uint flag, ushort cmd, ushort w1, ushort w2, ushort w3)
        {
            this.dwFlag = flag;
            this.wCmd = cmd;
            this.w1 = w1;
            this.w2 = w2;
            this.w3 = w3;
        }
    }

    #endregion

    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct LoginEnter
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] szAccount;  
        
        public uint nLid;         
        public uint nSid;         
        public uint dwEnterTime;  
        public uint nListId;      

        public LoginEnter()
        {
            szAccount = new byte[12];
            nLid = 0;
            nSid = 0;
            dwEnterTime = 0;
            nListId = 0;
        }

        
        
        
        public void SetAccount(string account)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(account);
            int length = Math.Min(bytes.Length, 11); 
            Array.Copy(bytes, 0, szAccount, 0, length);
            if (length < 12) szAccount[length] = 0; 
        }

        
        
        
        public string GetAccount()
        {
            int nullIndex = Array.IndexOf(szAccount, (byte)0);
            if (nullIndex < 0) nullIndex = szAccount.Length;
            return StringEncoding.GetGBKString(szAccount, 0, nullIndex);
        }
    }

    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DeletedDate
    {
        private uint _data;

        
        
        
        public uint Year
        {
            get => _data & 0xFFF;
            set => _data = (_data & ~0xFFFu) | (value & 0xFFF);
        }

        
        
        
        public uint Month
        {
            get => (_data >> 12) & 0xF;
            set => _data = (_data & ~(0xFu << 12)) | ((value & 0xF) << 12);
        }

        
        
        
        public uint Day
        {
            get => (_data >> 16) & 0x1F;
            set => _data = (_data & ~(0x1Fu << 16)) | ((value & 0x1F) << 16);
        }

        
        
        
        public uint Hour
        {
            get => (_data >> 21) & 0xF;
            set => _data = (_data & ~(0xFu << 21)) | ((value & 0xF) << 21);
        }

        
        
        
        public uint Minute
        {
            get => (_data >> 25) & 0x3F;
            set => _data = (_data & ~(0x3Fu << 25)) | ((value & 0x3F) << 25);
        }

        
        
        
        public bool BFlag
        {
            get => ((_data >> 31) & 1) == 1;
            set => _data = (_data & ~(1u << 31)) | ((value ? 1u : 0u) << 31);
        }

        
        
        
        public uint RawData => _data;

        
        
        
        public void SetRawData(uint data)
        {
            _data = data;
        }

        
        
        
        public DeletedDate(uint year, uint month, uint day, uint hour, uint minute, bool bFlag = false)
        {
            _data = 0;
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
            BFlag = bFlag;
        }

        
        
        
        public DeletedDate(uint rawData)
        {
            _data = rawData;
        }
    }

    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct SelectCharList
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 19)]
        public byte[] szName;  
        public byte btHair;    
        public byte btSex;     
        public byte btClass;   
        public ushort wLevel;  
        public DeletedDate date; 

        public SelectCharList()
        {
            szName = new byte[19];
            btHair = 0;
            btSex = 0;
            btClass = 0;
            wLevel = 0;
            date = new DeletedDate();
        }

        
        
        
        public void SetName(string name)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(name);
            int length = Math.Min(bytes.Length, 18); 
            Array.Copy(bytes, 0, szName, 0, length);
            if (length < 19) szName[length] = 0; 
        }

        
        
        
        public string GetName()
        {
            int nullIndex = Array.IndexOf(szName, (byte)0);
            if (nullIndex < 0) nullIndex = szName.Length;
            return StringEncoding.GetGBKString(szName, 0, nullIndex);
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DeletedCharList
    {
        public SelectCharList character;
        public DeletedDate datetime;
    }

    
    
    
    public enum PlayerState
    {
        PS_STANDING,
        PS_TURNING,
        PS_WALKING,
        PS_RUNNING,
        PS_ATTACKING,
        PS_GETMEAL,
        PS_SPELLING,
        PS_ZUOYI,
    }

    
    
    
    public enum ClientState
    {
        CS_NOSTATE,
        CS_LOGIN,
        CS_SELECTCHAR,
        CS_GAMEWORLD,
    }

    
    
    
    public enum PriceType
    {
        PT_GOLD,
        PT_YUANBAO,
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct PetInfo
    {
        public byte btLevel;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string sName;
        public ushort wCurHp;
        public ushort wMaxHp;
        public byte dc1;
        public byte dc2;
        public byte ac;
        public byte mac;
        public byte flag;
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct BaseItem
    {
        public byte btNameLength;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string szName;
        public byte btStdMode;
        public byte btShape;
        public byte btWeight;
        public byte btAniCount;
        public byte btSpecialpower;
        public byte bNeedIdentify;
        public byte btPriceType;
        public ushort wImageIndex;
        public ushort wMaxDura;
        public byte Ac1;
        public byte Ac2;
        public byte Mac1;
        public byte Mac2;
        public byte Dc1;
        public byte Dc2;
        public byte Mc1;
        public byte Mc2;
        public byte Sc1;
        public byte Sc2;
        public byte needtype;
        public byte needvalue;
        public byte btFlag;
        public byte btUpgradeTimes;
        public int nPrice;
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PlayerAction
    {
        public uint dwStartTime;
        public PlayerState action;
        public ushort x;
        public ushort y;
        public byte dir;

        public PlayerAction()
        {
            action = PlayerState.PS_STANDING;
            x = 0;
            y = 0;
            dir = 0;
            dwStartTime = (uint)Environment.TickCount;
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Item
    {
        public BaseItem baseitem;
        public uint dwMakeIndex;
        public ushort wCurDura;
        public ushort wMaxDura;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] dwParam;

        public Item()
        {
            baseitem = new BaseItem();
            dwMakeIndex = 0;
            wCurDura = 0;
            wMaxDura = 0;
            dwParam = new uint[4];
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ItemClient
    {
        public BaseItem baseitem;
        public uint dwMakeIndex;
        public ushort wCurDura;
        public ushort wMaxDura;

        public ItemClient()
        {
            baseitem = new BaseItem();
            dwMakeIndex = 0;
            wCurDura = 0;
            wMaxDura = 0;
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HumanProp
    {
        public ushort wLevel;
        public byte btMinDef;
        public byte btMaxDef;
        public byte btMinMagicDef;
        public byte btMaxMagicDef;
        public byte btMinAtk;
        public byte btMaxAtk;
        public byte btMinMagAtk;
        public byte btMaxMagAtk;
        public byte btMinSprAtk;
        public byte btMaxSprAtk;
        public ushort wCurHp;
        public ushort wCurMp;
        public ushort wMaxHp;
        public ushort wMaxMp;
        public byte b1;
        public byte btHpRecover;
        public byte btMagRecover;
        public byte b2;
        public uint dwCurexp;
        public uint dwMaxexp;
        public ushort wCurBagWeight;
        public ushort wMaxBagWeight;
        public byte btCurBodyWeight;
        public byte btMaxBodyWeight;
        public byte btCurHandWeight;
        public byte btMaxHandWeight;

        public HumanProp()
        {
            wLevel = 0;
            btMinDef = 0;
            btMaxDef = 0;
            btMinMagicDef = 0;
            btMaxMagicDef = 0;
            btMinAtk = 0;
            btMaxAtk = 0;
            btMinMagAtk = 0;
            btMaxMagAtk = 0;
            btMinSprAtk = 0;
            btMaxSprAtk = 0;
            wCurHp = 0;
            wCurMp = 0;
            wMaxHp = 0;
            wMaxMp = 0;
            b1 = 0;
            btHpRecover = 0;
            btMagRecover = 0;
            b2 = 0;
            dwCurexp = 0;
            dwMaxexp = 0;
            wCurBagWeight = 0;
            wMaxBagWeight = 0;
            btCurBodyWeight = 0;
            btMaxBodyWeight = 0;
            btCurHandWeight = 0;
            btMaxHandWeight = 0;
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MirPosition
    {
        public ushort x;
        public ushort y;

        public MirPosition(ushort x, ushort y)
        {
            this.x = x;
            this.y = y;
        }
    }

    
    
    
    public enum EquipPos
    {
        U_DRESS = 0,
        U_WEAPON = 1,
        U_CHARM = 2,
        U_NECKLACE = 3,
        U_HELMET = 4,
        U_ARMRINGL = 5,
        U_ARMRINGR = 6,
        U_RINGL = 7,
        U_RINGR = 8,
        U_SHOE = 9,
        U_BELT = 10,
        U_STONE = 11,
        U_POISON = 12,
        U_MAX = 13,
    }

    
    
    
    public enum ItemStdMode
    {
        ISM_DRUG = 0,
        ISM_FOOD0 = 1,
        ISM_FOOD1 = 2,
        ISM_USABLEITEM = 3,
        ISM_BOOK = 4,
        ISM_WEAPON0 = 5,
        ISM_WEAPON1 = 6,
        ISM_DRESS_MALE = 10,
        ISM_DRESS_FEMALE = 11,
        ISM_HELMENT = 15,
        ISM_NECKLACE0 = 19,
        ISM_NECKLACE1 = 20,
        ISM_NECKLACE2 = 21,
        ISM_RING0 = 22,
        ISM_RING1 = 23,
        ISM_BRACELET0 = 24,
        ISM_POISON = 25,
        ISM_BRACELET1 = 26,
        ISM_CANDLE = 30,
        ISM_BUNDLE = 31,
        ISM_CHARM = 34,
        ISM_OTHERBUNDLE = 35,
        ISM_SCROLL0 = 36,
        ISM_MEAT = 40,
        ISM_MISSION = 41,
        ISM_MATERIAL = 42,
        ISM_MINE = 43,
        ISM_SPECIAL = 44,
        ISM_DICE = 45,
        ISM_SPECIAL1 = 46,
        ISM_GOLDBUNDLE = 47,
        ISM_SCROLL1 = 50,
        ISM_BELT = 58,
        ISM_SHOES = 81,
    }

    
    
    
    public enum Profession
    {
        PRO_WARRIOR = 0,
        PRO_MAGICIAN = 1,
        PRO_TAOSHI = 2,
        PRO_MAX = 3,
    }

    
    
    
    public enum Sex
    {
        SEX_MALE = 0,
        SEX_FEMALE = 1,
    }

    
    
    
    public enum DbMsg
    {
        DM_START,
        DM_CHECKACCOUNT,
        DM_CHECKACCOUNTEXIST,
        DM_CREATEACCOUNT,
        DM_CHANGEPASSWORD,
        DM_QUERYCHARLIST,
        DM_CREATECHARACTER,
        DM_DELETECHARACTER,
        DM_DELETEDCHARLIST,
        DM_RESTORECHARACTER,
        DM_GETCHARPOSITIONFORSELCHAR,
        DM_GETCHARDBINFO,
        DM_PUTCHARDBINFO,
        DM_CREATEITEM,
        DM_DELETEITEM,
        DM_UPDATEITEM,
        DM_UPDATEITEMS,
        DM_UPDATEITEMPOS,
        DM_UPDATEITEMOWNER,
        DM_QUERYITEMS,
        DM_UPDATEMAGIC,
        DM_QUERYMAGIC,
        DM_UPDATEACCOUNTSTATE,
        DM_UPDATEITEMPOSEX,
        DM_UPDATEITEMPOSEX2,
        DM_UPDATEITEMOWNEREX,
        DM_UPDATEITEMEX,
        DM_UPDATECOMMUNITY,
        DM_QUERYCOMMUNITY,
        DM_BREAKFRIEND,
        DM_BREAKMARRIAGE,
        DM_BREAKMASTER,
        DM_CACHECHARDATA,
        DM_UPGRADEITEM,
        DM_QUERYUPGRADEITEM,
        DM_RESTOREGUILDNAME,
        DM_EXECSQL,
        DM_ADDCREDIT,
        DM_CHECKFREE,
        DM_DELETEMAGIC,
        DM_QUERYTASKINFO,
        DM_UPDATETASKINFO,
        DM_CHECKCHARACTERNAMEEXISTS,
        DM_END,
    }

    
    
    
    public enum SystemFlag
    {
        SF_BAGLOADED = 0,           
        SF_EQUIPMENTLOADED = 1,     
        SF_ALLDATALOADED = 2,       
        SF_ENTERGAMESENT = 3,       
        SF_FIRSTLOGIN = 4,          
        SF_GM = 5,                  
        SF_SCROLLTEXTMODE = 6,      
        SF_NOTICEMODE = 7,          
        SF_COMPLETELYQUIT = 8,      
        SF_HLTIMER = 9,             
        SF_MAX = 10                 
    }

    
    
    
    public static class GameCodec
    {
        
        
        
        public static int UnGameCode(byte[] input, byte[] output)
        {
            if (input == null || output == null || input.Length == 0)
                return 0;

            int ilen = input.Length;
            int iptr = 0;
            int optr = 0;
            byte b1, b2, b3, b4;
            int i = 0;

            
            int maxOutputSize = ((ilen + 3) / 4) * 3; 
            if (output.Length < maxOutputSize)
                return 0;

            for (i = 0; i < ilen / 4; i++)
            {
                
                b1 = (byte)(input[iptr++] - 0x3b);
                b2 = (byte)(input[iptr++] - 0x3b);
                b3 = (byte)(input[iptr++] - 0x3b);
                b4 = (byte)(input[iptr++] - 0x3b);

                output[optr++] = (byte)(((b1 & 3) | ((b1 & 0x3c) << 2) | (b4 & 0x0c)) ^ 0xeb);
                output[optr++] = (byte)(((b2 & 3) | ((b2 & 0x3c) << 2) | ((b4 & 0x03) << 2)) ^ 0xeb);
                output[optr++] = (byte)(((b3 & 0x3f) | ((b4 & 0x30) << 2)) ^ 0xeb);
            }

            ilen -= i * 4;

            
            if (ilen == 2)
            {
                b1 = (byte)(input[iptr++] - 0x3b);
                b2 = (byte)(input[iptr++] - 0x3b);
                output[optr++] = (byte)(((b1 & 3) | ((b1 & 0x3c) << 2) | ((b2 & 0x03) << 2)) ^ 0xeb);
            }
            else if (ilen == 3)
            {
                b1 = (byte)(input[iptr++] - 0x3b);
                b2 = (byte)(input[iptr++] - 0x3b);
                b3 = (byte)(input[iptr++] - 0x3b);
                output[optr++] = (byte)(((b1 & 3) | ((b1 & 0x3c) << 2) | (b3 & 0x0c)) ^ 0xeb);
                output[optr++] = (byte)(((b2 & 3) | ((b2 & 0x3c) << 2) | ((b3 & 0x03) << 2)) ^ 0xeb);
            }

            
            if (optr < output.Length)
                output[optr] = 0;

            return optr;
        }

        
        
        
        public static int CodeGameCode(byte[] input, int size, byte[] output)
        {
            byte b1, bcal, bflag1 = 0, bflag2 = 0;
            int i = 0;
            int iptr = 0;
            int optr = 0;

            while (iptr < size)
            {
                b1 = (byte)(input[iptr++] ^ 0xeb);

                if (i < 2)
                {
                    bcal = (byte)(b1 >> 2);
                    bflag1 = bcal;
                    bcal &= 0x3c;
                    b1 &= 3;
                    bcal |= b1;
                    bcal += 0x3b;
                    output[optr++] = bcal;
                    bflag2 = (byte)((bflag1 & 3) | (bflag2 << 2));
                }
                else
                {
                    bcal = (byte)(b1 & 0x3f);
                    bcal += 0x3b;
                    output[optr++] = bcal;
                    b1 >>= 2;
                    b1 &= 0x30;
                    b1 |= bflag2;
                    b1 += 0x3b;
                    output[optr++] = b1;
                    bflag2 = 0;
                }

                i++;
                i %= 3;
            }

            
            if (optr < output.Length)
                output[optr] = 0;

            if (i == 0)
                return optr;

            output[optr++] = (byte)(bflag2 + 0x3b);
            
            if (optr < output.Length)
                output[optr] = 0;

            return optr;
        }

        
        
        
        public static int EncodeMsg(byte[] buffer, uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, byte[]? lpdata = null, int datasize = -1)
        {
            
            int codedsize = 0;
            buffer[codedsize++] = (byte)'#';

            
            byte[] headerBytes = new byte[12]; 
            unsafe
            {
                fixed (byte* pHeader = headerBytes)
                {
                    uint* pUint = (uint*)pHeader;
                    *pUint = dwFlag;

                    ushort* pUshort = (ushort*)(pHeader + 4);
                    *pUshort = wCmd;
                    *(pUshort + 1) = w1;
                    *(pUshort + 2) = w2;
                    *(pUshort + 3) = w3;
                }
            }

            
            byte[] tempBuffer1 = new byte[headerBytes.Length * 2];
            int codedSize1 = CodeGameCode(headerBytes, headerBytes.Length, tempBuffer1);
            Array.Copy(tempBuffer1, 0, buffer, codedsize, codedSize1);
            codedsize += codedSize1;

            if (lpdata != null && datasize != 0)
            {
                if (datasize < 0)
                    datasize = lpdata.Length;

                if (datasize > 0)
                {
                    
                    byte[] tempBuffer2 = new byte[datasize * 2];
                    int codedSize2 = CodeGameCode(lpdata, datasize, tempBuffer2);
                    Array.Copy(tempBuffer2, 0, buffer, codedsize, codedSize2);
                    codedsize += codedSize2;
                }
            }

            buffer[codedsize++] = (byte)'!';
            if (codedsize < buffer.Length)
                buffer[codedsize] = 0;

            return codedsize;
        }

        
        
        
        public static bool DecodeMsg(byte[] input, int inputSize, out MirMsgHeader header, out byte[]? payload)
        {
            header = new MirMsgHeader();
            payload = null;

            if (input == null || inputSize < 3) 
                return false;

            
            if (input[0] != '#' || input[inputSize - 1] != '!')
                return false;

            
            int encodedSize = inputSize - 2;
            if (encodedSize <= 0)
                return false;

            byte[] encodedData = new byte[encodedSize];
            Array.Copy(input, 1, encodedData, 0, encodedSize);

            
            byte[] decodedData = new byte[encodedSize * 2]; 
            int decodedSize = UnGameCode(encodedData, decodedData);
            if (decodedSize < 12) 
                return false;

            
            unsafe
            {
                fixed (byte* pDecoded = decodedData)
                {
                    uint* pUint = (uint*)pDecoded;
                    header.dwFlag = *pUint;

                    ushort* pUshort = (ushort*)(pDecoded + 4);
                    header.wCmd = *pUshort;
                    header.w1 = *(pUshort + 1);
                    header.w2 = *(pUshort + 2);
                    header.w3 = *(pUshort + 3);
                }
            }

            
            if (decodedSize > 12)
            {
                payload = new byte[decodedSize - 12];
                Array.Copy(decodedData, 12, payload, 0, payload.Length);
            }

            return true;
        }
    }

    #region C++原结构（客户端通讯专用）

    
    
    
    public static class GameCodecOrign
    {
        
        
        
        public static int UnGameCodeOrign(byte[] input, byte[] output)
        {
            if (input == null || output == null || input.Length == 0)
                return 0;
                
            int ilen = input.Length;
            int iptr = 0;
            int optr = 0;
            byte b1, b2, b3, b4;
            int i = 0;

            
            int maxOutputSize = ((ilen + 3) / 4) * 3; 
            if (output.Length < maxOutputSize)
                return 0;

            for (i = 0; i < ilen / 4; i++)
            {
                
                b1 = (byte)(input[iptr++] - 0x3b);
                b2 = (byte)(input[iptr++] - 0x3b);
                b3 = (byte)(input[iptr++] - 0x3b);
                b4 = (byte)(input[iptr++] - 0x3b);

                output[optr++] = (byte)(((b1 & 3) | ((b1 & 0x3c) << 2) | (b4 & 0x0c)) ^ 0xeb);
                output[optr++] = (byte)(((b2 & 3) | ((b2 & 0x3c) << 2) | ((b4 & 0x03) << 2)) ^ 0xeb);
                output[optr++] = (byte)(((b3 & 0x3f) | ((b4 & 0x30) << 2)) ^ 0xeb);
            }

            ilen -= i * 4;

            
            if (ilen == 2)
            {
                b1 = (byte)(input[iptr++] - 0x3b);
                b2 = (byte)(input[iptr++] - 0x3b);
                output[optr++] = (byte)(((b1 & 3) | ((b1 & 0x3c) << 2) | ((b2 & 0x03) << 2)) ^ 0xeb);
            }
            else if (ilen == 3)
            {
                b1 = (byte)(input[iptr++] - 0x3b);
                b2 = (byte)(input[iptr++] - 0x3b);
                b3 = (byte)(input[iptr++] - 0x3b);
                output[optr++] = (byte)(((b1 & 3) | ((b1 & 0x3c) << 2) | (b3 & 0x0c)) ^ 0xeb);
                output[optr++] = (byte)(((b2 & 3) | ((b2 & 0x3c) << 2) | ((b3 & 0x03) << 2)) ^ 0xeb);
            }

            
            if (optr < output.Length)
                output[optr] = 0;

            return optr;
        }

        
        
        
        public static int CodeGameCodeOrign(byte[] input, int size, byte[] output)
        {
            if (input == null || output == null || size <= 0)
                return 0;
                
            byte b1, bcal, bflag1 = 0, bflag2 = 0;
            int i = 0;
            int iptr = 0;
            int optr = 0;
            
            
            while (iptr < size)
            {
                b1 = (byte)(input[iptr++] ^ 0xeb);
                
                if (i < 2)
                {
                    bcal = (byte)(b1 >> 2);
                    bflag1 = bcal;
                    bcal &= 0x3c;
                    b1 &= 3;
                    bcal |= b1;
                    bcal += 0x3b;
                    output[optr++] = bcal;
                    bflag2 = (byte)((bflag1 & 3) | (bflag2 << 2));
                }
                else
                {
                    bcal = (byte)(b1 & 0x3f);
                    bcal += 0x3b;
                    output[optr++] = bcal;
                    b1 >>= 2;
                    b1 &= 0x30;
                    b1 |= bflag2;
                    b1 += 0x3b;
                    output[optr++] = b1;
                    bflag2 = 0;
                }
                
                i++;
                i %= 3;
            }
            
            
            if (i != 0)
            {
                output[optr++] = (byte)(bflag2 + 0x3b);
            }
            
            
            if (optr < output.Length)
                output[optr] = 0;
            
            return optr;
        }

        
        
        
        public static int EncodeMsgOrign(byte[] buffer, uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, byte[]? lpdata = null, int datasize = -1)
        {
            
            int codedsize = 0;
            buffer[codedsize++] = (byte)'#';

            
            byte[] headerBytes = new byte[12]; 
            unsafe
            {
                fixed (byte* pHeader = headerBytes)
                {
                    uint* pUint = (uint*)pHeader;
                    *pUint = dwFlag;
                    
                    ushort* pUshort = (ushort*)(pHeader + 4);
                    *pUshort = wCmd;
                    *(pUshort + 1) = w1;
                    *(pUshort + 2) = w2;
                    *(pUshort + 3) = w3;
                }
            }

            
            byte[] tempBuffer1 = new byte[headerBytes.Length * 2];
            int codedSize1 = CodeGameCodeOrign(headerBytes, headerBytes.Length, tempBuffer1);
            Array.Copy(tempBuffer1, 0, buffer, codedsize, codedSize1);
            codedsize += codedSize1;

            if (lpdata != null && datasize != 0)
            {
                if (datasize < 0)
                    datasize = lpdata.Length;
                    
                if (datasize > 0)
                {
                    
                    byte[] tempBuffer2 = new byte[datasize * 2];
                    int codedSize2 = CodeGameCodeOrign(lpdata, datasize, tempBuffer2);
                    Array.Copy(tempBuffer2, 0, buffer, codedsize, codedSize2);
                    codedsize += codedSize2;
                }
            }

            buffer[codedsize++] = (byte)'!';
            if (codedsize < buffer.Length)
                buffer[codedsize] = 0;

            return codedsize;
        }

        
        
        
        public static bool DecodeMsgOrign(byte[] input, int inputSize, out MirMsgHeaderOrign header, out byte[]? payload)
        {
            header = new MirMsgHeaderOrign();
            payload = null;

            if (input == null || inputSize < 3) 
                return false;

            
            if (input[0] != '#' || input[inputSize - 1] != '!')
                return false;

            
            int encodedSize = inputSize - 2;
            if (encodedSize <= 0)
                return false;

            byte[] encodedData = new byte[encodedSize];
            Array.Copy(input, 1, encodedData, 0, encodedSize);

            
            byte[] decodedData = new byte[encodedSize * 2]; 
            int decodedSize = UnGameCodeOrign(encodedData, decodedData);
            if (decodedSize < 12) 
                return false;

            
            unsafe
            {
                fixed (byte* pDecoded = decodedData)
                {
                    uint* pUint = (uint*)pDecoded;
                    header.dwFlag = *pUint;
                    
                    ushort* pUshort = (ushort*)(pDecoded + 4);
                    header.wCmd = *pUshort;
                    header.w1 = *(pUshort + 1);
                    header.w2 = *(pUshort + 2);
                    header.w3 = *(pUshort + 3);
                }
            }

            
            if (decodedSize > 12)
            {
                payload = new byte[decodedSize - 12];
                Array.Copy(decodedData, 12, payload, 0, payload.Length);
            }

            return true;
        }
    }

    #endregion

    
    
    
    public enum ServerType
    {
        ST_UNKNOWN = 0,
        ST_DATABASESERVER = 1,
        ST_LOGINSERVER = 2,
        ST_LOGINSERVER_ = 3,
        ST_SELCHARSERVER = 4,
        ST_GAMESERVER_ = 5,
        ST_GAMESERVER = 6,
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ServerIdent
    {
        public uint Id;
        public byte Type;
        public byte Group;
        public byte Index;

        public ServerIdent()
        {
            Id = 0;
            Type = 0;
            Group = 0;
            Index = 0;
        }
    }

    
    
    
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ServerId
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

        public ServerId()
        {
            dwId = 0;
            bType = 0;
            bGroup = 0;
            bId = 0;
            bIndex = 0;
        }
    }

    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct ServerAddr
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] addr;  
        public uint nPort;   

        public ServerAddr()
        {
            addr = new byte[16];
            nPort = 0;
        }

        
        
        
        public void SetAddress(string address)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(address);
            int length = Math.Min(bytes.Length, 15); 
            Array.Copy(bytes, 0, addr, 0, length);
            if (length < 16) addr[length] = 0; 
        }

        
        
        
        public string GetAddress()
        {
            int nullIndex = Array.IndexOf(addr, (byte)0);
            if (nullIndex < 0) nullIndex = addr.Length;
            return StringEncoding.GetGBKString(addr, 0, nullIndex);
        }
    }

    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct RegisterServerInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] szName;  
        public ServerId Id;    
        public ServerAddr addr; 

        public RegisterServerInfo()
        {
            szName = new byte[64];
            Id = new ServerId();
            addr = new ServerAddr();
        }

        
        
        
        public void SetName(string name)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(name);
            int length = Math.Min(bytes.Length, 63); 
            Array.Copy(bytes, 0, szName, 0, length);
            if (length < 64) szName[length] = 0; 
        }

        
        
        
        public string GetName()
        {
            int nullIndex = Array.IndexOf(szName, (byte)0);
            if (nullIndex < 0) nullIndex = szName.Length;
            return StringEncoding.GetGBKString(szName, 0, nullIndex);
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RegisterServerResult
    {
        public ServerId Id;
        public int nDbCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public ServerAddr[] DbAddr;

        public RegisterServerResult()
        {
            Id = new ServerId();
            nDbCount = 0;
            DbAddr = new ServerAddr[2];
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FindServerResult
    {
        public ServerAddr addr;
        public ServerId Id;

        public FindServerResult()
        {
            addr = new ServerAddr();
            Id = new ServerId();
        }
    }

    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct QueryCharList
    {
        public uint dwKey;  
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] szAccount;  
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] szServerName;  

        public QueryCharList()
        {
            dwKey = 0;
            szAccount = new byte[12];
            szServerName = new byte[20];
        }

        
        
        
        public void SetAccount(string account)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(account);
            int length = Math.Min(bytes.Length, 11); 
            Array.Copy(bytes, 0, szAccount, 0, length);
            if (length < 12) szAccount[length] = 0; 
        }

        
        
        
        public string GetAccount()
        {
            int nullIndex = Array.IndexOf(szAccount, (byte)0);
            if (nullIndex < 0) nullIndex = szAccount.Length;
            return StringEncoding.GetGBKString(szAccount, 0, nullIndex);
        }

        
        
        
        public void SetServerName(string serverName)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(serverName);
            int length = Math.Min(bytes.Length, 19); 
            Array.Copy(bytes, 0, szServerName, 0, length);
            if (length < 20) szServerName[length] = 0; 
        }

        
        
        
        public string GetServerName()
        {
            int nullIndex = Array.IndexOf(szServerName, (byte)0);
            if (nullIndex < 0) nullIndex = szServerName.Length;
            return StringEncoding.GetGBKString(szServerName, 0, nullIndex);
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct QueryCharListResult
    {
        public uint dwKey;
        public int count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public SelectCharList[] charlist;

        public QueryCharListResult()
        {
            dwKey = 0;
            count = 0;
            charlist = new SelectCharList[2];
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct QueryMapPositionResult
    {
        public uint dwKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szName;
        public ushort x;
        public ushort y;

        public QueryMapPositionResult()
        {
            dwKey = 0;
            szName = string.Empty;
            x = 0;
            y = 0;
        }
    }

    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct EnterSelCharServer
    {
        public uint nClientId;    
        public uint nLoginId;     
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] szAccount;  
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] reserved;   

        public EnterSelCharServer()
        {
            nClientId = 0;
            nLoginId = 0;
            szAccount = new byte[12];
            reserved = new byte[4];
        }

        
        
        
        public void SetAccount(string account)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(account);
            int length = Math.Min(bytes.Length, 11); 
            Array.Copy(bytes, 0, szAccount, 0, length);
            if (length < 12) szAccount[length] = 0; 
        }

        
        
        
        public string GetAccount()
        {
            int nullIndex = Array.IndexOf(szAccount, (byte)0);
            if (nullIndex < 0) nullIndex = szAccount.Length;
            return StringEncoding.GetGBKString(szAccount, 0, nullIndex);
        }
    }

    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct EnterGameServer
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] szAccount;  
        
        public uint nLoginId;     
        
        public uint nSelCharId;   
        
        
        
        private uint _unionField;
        
        public uint nClientId
        {
            get => _unionField;
            set => _unionField = value;
        }
        
        public uint result
        {
            get => _unionField;
            set => _unionField = value;
        }
        
        public uint dwEnterTime;  
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] szName;     
        
        public uint dwSelectCharServerId; 

        public EnterGameServer()
        {
            szAccount = new byte[12];
            nLoginId = 0;
            nSelCharId = 0;
            _unionField = 0;
            dwEnterTime = 0;
            szName = new byte[32];
            dwSelectCharServerId = 0;
        }

        
        
        
        public void SetAccount(string account)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(account);
            int length = Math.Min(bytes.Length, 11); 
            Array.Copy(bytes, 0, szAccount, 0, length);
            if (length < 12) szAccount[length] = 0; 
        }

        
        
        
        public string GetAccount()
        {
            int nullIndex = Array.IndexOf(szAccount, (byte)0);
            if (nullIndex < 0) nullIndex = szAccount.Length;
            return StringEncoding.GetGBKString(szAccount, 0, nullIndex);
        }

        
        
        
        public void SetName(string name)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(name);
            int length = Math.Min(bytes.Length, 31); 
            Array.Copy(bytes, 0, szName, 0, length);
            if (length < 32) szName[length] = 0; 
        }

        
        
        
        public string GetName()
        {
            int nullIndex = Array.IndexOf(szName, (byte)0);
            if (nullIndex < 0) nullIndex = szName.Length;
            return StringEncoding.GetGBKString(szName, 0, nullIndex);
        }
    }

    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MsgAcrossServer
    {
        
        
        
        public ServerId SenderId;
        
        
        
        
        public byte SenderType;
        
        
        
        
        public byte SenderIndex;
        
        
        
        
        public byte Reserved1;
        
        
        
        
        public byte Reserved2;
        
        
        
        
        public ServerId TargetId;
        
        
        
        
        public ushort TargetGroup;
        
        
        
        
        public ushort TargetType;
        
        
        
        
        public ushort Command;
        
        
        
        
        public byte SendType;
        
        
        
        
        public ushort DataLength;
        
        
        
        
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] Data;

        public MsgAcrossServer()
        {
            SenderId = new ServerId();
            SenderType = 0;
            SenderIndex = 0;
            Reserved1 = 0;
            Reserved2 = 0;
            TargetId = new ServerId();
            TargetGroup = 0;
            TargetType = 0;
            Command = 0;
            SendType = 0;
            DataLength = 0;
            Data = new byte[1];
        }

        
        
        
        public static int FixedSize => 24; 
    }

    
    
    
    public static class ProtocolCmd
    {
        
        public const ushort CM_REGISTERACCOUNT = 2002;
        public const ushort CM_CHECKACCOUNTEXIST = 118;
        public const ushort CM_LEAVESERVER = 106;
        public const ushort CM_LOGIN = 2001;
        public const ushort CM_PTLOGIN = 2008;
        public const ushort CM_CHANGEPASSWORD = 2003;
        public const ushort CM_SELECTSERVER = 104;
        public const ushort CM_QUERYCHARLIST = 100;
        public const ushort CM_QUERYDELCHARLIST = 104;
        public const ushort CM_QUERYCREATECHAR = 101;
        public const ushort CM_QUERYDELCHAR = 102;
        public const ushort CM_QUERYUNDELCHAR = 105;
        public const ushort CM_QUERYSELECTCHAR = 103;
        public const ushort CM_WALK = 0xbc3;
        public const ushort CM_RUN = 0xbc5;
        public const ushort CM_ATTACK = 0xbc6;
        public const ushort CM_TURN = 0xbc2;
        public const ushort CM_GETMEAL = 0xbc4;
        public const ushort CM_ZUOYI = 0xbcd;
        public const ushort CM_SPELLSKILL = 0xbc9;
        public const ushort CM_STOP = 0xbcc;
        public const ushort CM_SAY = 0xbd6;
        public const ushort CM_ENTERGAME = 0x0f;

        
        public const ushort SM_REGISTERACCOUNTOK = 0x1f8;
        public const ushort SM_REGISTERACCOUNTFAIL = 0x1f9;
        public const ushort SM_CHECKACCOUNTEXISTRET = 0x1fc;
        public const ushort SM_LOGINFAIL = 503;
        public const ushort SM_CHANGEPASSWORDOK = 0x1fa;
        public const ushort SM_CHANGEPASSWORDFAIL = 0x1fb;
        public const ushort SM_TIPWINDOW = 2810;
        public const ushort SM_LOGINOK = 529;
        public const ushort SM_SELECTSERVEROK = 530;
        public const ushort SM_CHARLIST = 520;
        public const ushort SM_QUERYCHR_FAIL = 527;
        public const ushort SM_DELCHARLIST = 534;
        public const ushort SM_CREATECHAROK = 521;
        public const ushort SM_CREATECHARFAIL = 522;
        public const ushort SM_DELCHAROK = 523;
        public const ushort SM_UNDELCHAROK = 533;
        public const ushort SM_SELECTCHAROK = 525;
        public const ushort SM_SETMAP = 0x33;
        public const ushort SM_SETMAPNAME = 0x36;
        public const ushort SM_SETPLAYERNAME = 0x2a;
        public const ushort SM_SETPLAYER = 0x32;
        public const ushort SM_WALK = 0x0b;
        public const ushort SM_TURN = 0x0c;
        public const ushort SM_RUN = 0x0d;

        public const ushort SM_ATTACK = 0x0e;
        public const ushort SM_APPEAR = 0x0a;
        public const ushort SM_DISAPPEAR = 0x1e;
        public const ushort SM_DIE = 0x22;
        public const ushort SM_STOP = 0xcc;
        public const ushort SM_CHAT = 0x28;
        public const ushort SM_SYSCHAT = 0x64;
        public const ushort SM_HPMPCHANGED = 0x35;
        public const ushort SM_GOLDCHANGED = 0x28d;
        public const ushort SM_ENTERGAMEOK = 0x0f;

        
        public const ushort SCM_REGISTERSERVER = 5001;
        public const ushort SCM_FINDSERVER = 5002;
        public const ushort SCM_GETGAMESERVERADDR = 5003;
        public const ushort SCM_MSGACROSSSERVER = 5004;     
        public const ushort MAS_ENTERSELCHARSERVER = 6001;   
        public const ushort MAS_ENTERGAMESERVER = 6002;      
        public const ushort MAS_KICKCONNECTION = 6000;       
        public const ushort MAS_RESTARTGAME = 6003;          
        
        
        public const int MST_SINGLE = 0;     
        public const int MST_GROUP = 1;      
        public const int MST_TYPE = 2;       
        
        
        public const ushort CM_REGISTERSERVER = 5001;
        public const ushort SM_REGISTERSERVEROK = 5002;
        public const ushort CM_QUERYSERVER = 5003;
        public const ushort SM_QUERYSERVEROK = 5004;
        public const ushort CM_UNREGISTERSERVER = 5005;
        public const ushort SM_UNREGISTERSERVEROK = 5006;

        
        public const ushort CM_NPCTALK = 0xbd0;          
        public const ushort CM_DIALOGCHOICE = 0xbd1;     
        public const ushort CM_SHOPBUY = 0xbd2;          
        public const ushort CM_SHOPSELL = 0xbd3;         
        public const ushort CM_REPAIR = 0xbd4;           
        public const ushort CM_ENHANCE = 0xbd5;          
        public const ushort CM_STORAGE = 0xbd7;          
        public const ushort CM_TELEPORT = 0xbd8;         

        public const ushort SM_NPCTALK = 0x2b;           
        public const ushort SM_OPENSHOP = 0x2c;          
        public const ushort SM_OPENENHANCE = 0x2e;       
        public const ushort SM_OPENSTORAGE = 0x2f;       
        public const ushort SM_TELEPORTLIST = 0x30;      
        public const ushort SM_DIALOG = 0x31;            
        
        
        
        
        public const ushort SM_DOWNITEMAPPEAR = 0x262;   
        public const ushort SM_DOWNITEMDISAPPEAR = 0x263; 
        
        
        public const ushort CM_CONFIRMFIRSTDIALOG = 0x3fa; 
        public const ushort SM_FIRSTDIALOG = 0x292;        
        public const ushort SM_READY = 0x452;              
        
        
        public const ushort CM_MAPLOADED = 0x409;          
        public const ushort SM_DEATH = 32;                 
        public const ushort SM_SKELETON = 33;              
        public const ushort SM_NOWDEATH = 34;              
        public const ushort SM_CLEAROBJECTS = 633;         
        public const ushort SM_CHANGEMAP = 0x27a;          
        public const ushort SM_SETITEMPOSITION = 0x46;     
        public const ushort CM_SETITEMPOSITION = 0x45;     
        public const ushort SM_SETGAMEDATETIME = 0x2e;     
        public const ushort SM_CHANGEOUTVIEW = 0x29;       
        public const ushort SM_BEATTACK = 0x1f;            
        public const ushort SM_FEATURECHANGED = 41;        
        public const ushort SM_CHANGENAMECOLOR = 656;      
        public const ushort SM_CHARSTATUSCHANGED = 657;    
        public const ushort SM_SCROLLTEXT = 0x22ed;        
        public const ushort SM_STARTBODYEFFECT = 0x1d;     
        public const ushort SM_SKILLEXPCHANGED = 0x280;    
        public const ushort SM_ITEMDURACHANGED = 0x282;    
        public const ushort SM_FRIENDLIST = 0x1c1;         
        public const ushort SM_WEIGHTCHANGED = 0x26e;      
        public const ushort SM_CHANGESERVER = 0x322;       
        public const ushort SM_NPCPAGE = 0x283;            
        public const ushort SM_ERRORDIALOG = 0x2fb;        
        public const ushort SM_BACK = 0x9;                 
        public const ushort SM_UPDATEPROP = 0x34;          
        public const ushort SM_EQUIPMENTS = 0x26d;         
        public const ushort SM_EAT_OK = 635;               
        public const ushort SM_EAT_FAIL = 636;             
        public const ushort SM_REPAIROK = 0x29d;           
        public const ushort SM_OPENREPAIR = 0x29c;         
        public const ushort SM_PUTREPAIRITEMOK = 0x29f;    
        
        
        public const ushort CM_QUERYTRADE = 0x401;         
        public const ushort SM_TRADESTART = 0x2a1;         
        public const ushort CM_PUTTRADEITEM = 0x402;       
        public const ushort SM_PUTTRADEITEMOK = 0x2a3;     
        public const ushort SM_PUTTRADEITEMFAIL = 0x2a4;   
        public const ushort CM_QUERYTRADEEND = 0x406;      
        public const ushort SM_TRADEEND = 0x2af;           
        public const ushort CM_PUTTRADEGOLD = 0x405;       
        public const ushort SM_PUTTRADEGOLDOK = 0x2ac;     
        public const ushort SM_PUTTRADEGOLDFAIL = 0x2ad;   
        public const ushort SM_SETSUPERGOLD = 0xe679;      
        public const ushort SM_SETGOLD = 0x28d;            
        public const ushort SM_OTHERPUTTRADEGOLD = 0x2ae;  
        public const ushort SM_OTHERPUTTRADEITEM = 0x2aa;  
        public const ushort SM_ADDBAGITEM = 0xc8;          
        public const ushort SM_BAGINFO = 0xc9;             
        public const ushort SM_UPDATEITEM = 0xcb;          
        public const ushort CM_CANCELTRADE = 0x404;        
        public const ushort SM_TRADECANCELED = 0x2a9;      
        
        
        public const ushort CM_CHANGEGROUPMODE = 0x3fb;    
        public const ushort SM_GROUPMODE = 0x293;          
        public const ushort CM_QUERYADDGROUPMEMBER = 0x3fc;
        public const ushort SM_GROUPCREATE = 0x294;        
        public const ushort SM_UPDATEMEMBERINFO = 0x2e0;   
        public const ushort SM_GROUPMEMBERLIST = 0x29b;    
        public const ushort CM_DELETEGROUPMEMBER = 0x3fe;  
        public const ushort SM_DELETECHARACTEROK = 0x297;  
        public const ushort SM_GROUPDESTROYED = 0x29a;     
    }
}
