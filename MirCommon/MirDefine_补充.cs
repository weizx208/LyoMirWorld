using System;
using System.Runtime.InteropServices;

namespace MirCommon
{
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LMirMsg
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool bUnCodedMsg;
        public int size;
        public MirMsg msg;
        
        public LMirMsg()
        {
            bUnCodedMsg = false;
            size = 0;
            msg = new MirMsg();
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct PrivateShopHeader
    {
        public ushort w1;
        public byte w2;
        public byte btFlag;
        public uint dw1;
        public ushort wCount;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 52)]
        public string szName;
        
        public PrivateShopHeader()
        {
            w1 = 0;
            w2 = 0;
            btFlag = 0;
            dw1 = 0;
            wCount = 0;
            szName = string.Empty;
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PrivateShopShow
    {
        public PrivateShopHeader header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public ItemClient[] items;
        
        public PrivateShopShow()
        {
            header = new PrivateShopHeader();
            items = new ItemClient[10];
            for (int i = 0; i < 10; i++)
            {
                items[i] = new ItemClient();
            }
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PrivateShopItemQuery
    {
        public uint dwMakeIndex;
        public uint dwPrice;
        public ushort wPriceType;
        
        public PrivateShopItemQuery()
        {
            dwMakeIndex = 0;
            dwPrice = 0;
            wPriceType = 0;
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct PrivateShopQuery
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 52)]
        public string szName;
        public PrivateShopItemQuery item;
        
        public PrivateShopQuery()
        {
            szName = string.Empty;
            item = new PrivateShopItemQuery();
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Equipment
    {
        public ushort pos;
        public ItemClient item;
        
        public Equipment()
        {
            pos = 0;
            item = new ItemClient();
        }
    }
    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DBItemPos
    {
        public Item item;
        public ushort pos;
        public byte btFlag;
        
        public DBItemPos()
        {
            item = new Item();
            pos = 0;
            btFlag = 0;
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct PlayerStruct
    {
        public HumanProp prop;
        public uint dwGold;
        public uint dwSuperGold;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MirDefine.ALLBAGSIZE)]
        public Item[] bagitems;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MirDefine.MAXEQUIPMENTPOS)]
        public Item[] equipments;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szName;
        public byte btState;
        public ushort x;
        public ushort y;
        public byte btDir;
        
        public PlayerStruct()
        {
            prop = new HumanProp();
            dwGold = 0;
            dwSuperGold = 0;
            bagitems = new Item[MirDefine.ALLBAGSIZE];
            for (int i = 0; i < MirDefine.ALLBAGSIZE; i++)
            {
                bagitems[i] = new Item();
            }
            equipments = new Item[MirDefine.MAXEQUIPMENTPOS];
            for (int i = 0; i < MirDefine.MAXEQUIPMENTPOS; i++)
            {
                equipments[i] = new Item();
            }
            szName = string.Empty;
            btState = 0;
            x = 0;
            y = 0;
            btDir = 0;
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct OtherPlayer
    {
        public ushort x;
        public ushort y;
        public byte btDir;
        public byte btState;
        public ushort wNouse;
        public uint outview;
        public uint feather;
        public ushort wCurHp;
        public ushort wMaxHp;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szName;
        public uint dwListId;
        public uint dwGameId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bDead;
        
        public OtherPlayer()
        {
            x = 0;
            y = 0;
            btDir = 0;
            btState = 0;
            wNouse = 0;
            outview = 0;
            feather = 0;
            wCurHp = 0;
            wMaxHp = 0;
            szName = string.Empty;
            dwListId = 0;
            dwGameId = 0;
            bDead = false;
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct RegisterAccount
    {
        public byte btAccount;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public string szAccount;
        public byte btPassword;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public string szPassword;
        public byte btName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szName;
        public byte btIdCard;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 19)]
        public string szIdCard;
        public byte btPhoneNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string szPhoneNumber;
        public byte btQ1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szQ1;
        public byte btA1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szA1;
        public byte btEmail;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
        public string szEmail;
        public byte btQ2;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szQ2;
        public byte btA2;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szA2;
        public byte btBirthday;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public string szBirthday;
        public byte btMobileNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        public string szMobileNumber;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 85)]
        public byte[] szUnknown;
        
        public RegisterAccount()
        {
            btAccount = 0;
            szAccount = string.Empty;
            btPassword = 0;
            szPassword = string.Empty;
            btName = 0;
            szName = string.Empty;
            btIdCard = 0;
            szIdCard = string.Empty;
            btPhoneNumber = 0;
            szPhoneNumber = string.Empty;
            btQ1 = 0;
            szQ1 = string.Empty;
            btA1 = 0;
            szA1 = string.Empty;
            btEmail = 0;
            szEmail = string.Empty;
            btQ2 = 0;
            szQ2 = string.Empty;
            btA2 = 0;
            szA2 = string.Empty;
            btBirthday = 0;
            szBirthday = string.Empty;
            btMobileNumber = 0;
            szMobileNumber = string.Empty;
            szUnknown = new byte[85];
        }
    }
    
    
    
    
    public enum RegisterAccountIndex
    {
        RAI_ACCOUNT = 0,
        RAI_PASSWORD = 11,
        RAI_NAME = 22,
        RAI_IDCARD = 43,
        RAI_PHONENUMBER = 63,
        RAI_Q1 = 78,
        RAI_A1 = 99,
        RAI_MAIL = 120,
        RAI_Q2 = 161,
        RAI_A2 = 182,
        RAI_BIRTHDAY = 203,
        RAI_MOBILEPHONENUMBER = 214,
        RAI_UNKNOWN = 226,
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CreateCharDesc
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
        
        public CreateCharDesc()
        {
            dwKey = 0;
            szName = string.Empty;
            szServer = string.Empty;
            szAccount = string.Empty;
            btClass = 0;
            btSex = 0;
            btHair = 0;
            btLevel = 0;
        }
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Feather
    {
        public byte btRace;
        public byte btWeapon;
        public byte btHair;
        public byte btDress;
        
        public Feather()
        {
            btRace = 0;
            btWeapon = 0;
            btHair = 0;
            btDress = 0;
        }
    }
    
    
    
    
    public enum ItemDbFlag
    {
        IDF_GROUND,
        IDF_BAG,
        IDF_EQUIPMENT,
        IDF_NPC,
        IDF_BANK,
        IDF_CACHE,
        IDF_PETBANK,
        IDF_UPGRADE,
    }
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UpgradeAddMask
    {
        public ushort wAddMask;
        public ushort wItemLimit;
        
        public UpgradeAddMask()
        {
            wAddMask = 0;
            wItemLimit = 0;
        }
    }
    
    
    
    
    public enum PropIndex
    {
        PI_MINAC,
        PI_MAXAC,
        PI_MINMAC,
        PI_MAXMAC,
        PI_MINDC,
        PI_MAXDC,
        PI_MINMC,
        PI_MAXMC,
        PI_MINSC,
        PI_MAXSC,
        PI_HITRATE,
        PI_ESCAPE,
        PI_MAGESCAPE,
        PI_POISONESCAPE,
        PI_ATTACKSPEED,
        PI_LUCKY,
        PI_DAWN,
        PI_HPRECOVER,
        PI_MPRECOVER,
        PI_POISONRECOVER,
        PI_HARD,
        PI_HOLLY,
        PI_LEVEL,
        PI_CURBAGWEIGHT,
        PI_MAXBAGWEIGHT,
        PI_CURHANDWEIGHT,
        PI_MAXHANDWEIGHT,
        PI_CURBODYWEIGHT,
        PI_MAXBODYWEIGHT,
        PI_CURHP,
        PI_CURMP,
        PI_MAXHP,
        PI_MAXMP,
        PI_EXP,
        PI_PROP_COUNT,
    }
    
    
    
    
    public enum ItemNeedType
    {
        INT_LEVEL,
        INT_DC,
        INT_MC,
        INT_SC,
        INT_PKVALUE,
        INT_CREDIT,
        INT_SABUKOWNER,
    }
    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CREATEITEM
    {
        public uint dwClientKey;     
        public Item item;            
        public ushort wPos;          
        public byte btFlag;          
        
        public CREATEITEM()
        {
            dwClientKey = 0;
            item = new Item();
            wPos = 0;
            btFlag = 0;
        }
    }
    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ITEMCLIENT
    {
        public BaseItem baseitem;    
        public uint dwMakeIndex;     
        public ushort wCurDura;      
        public ushort wMaxDura;      
        
        public ITEMCLIENT()
        {
            baseitem = new BaseItem();
            dwMakeIndex = 0;
            wCurDura = 0;
            wMaxDura = 0;
        }
    }
    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CREATEHUMANDESC
    {
        public Database.CHARDBINFO dbinfo;    
        public IntPtr pClientObj;    
        
        public CREATEHUMANDESC()
        {
            dbinfo = new Database.CHARDBINFO();
            pClientObj = IntPtr.Zero;
        }
    }
    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EQUIPMENT
    {
        public ushort pos;           
        public ITEMCLIENT item;      
        
        public EQUIPMENT()
        {
            pos = 0;
            item = new ITEMCLIENT();
        }
    }
    
    
    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BAGITEMPOS
    {
        public uint ItemId;          
        public ushort wPos;          
        
        public BAGITEMPOS()
        {
            ItemId = 0;
            wPos = 0;
        }
    }

    
    
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MAGIC
    {
        public byte cKey;                 
        public byte btLevel;              
        public ushort wUnknown;           
        public int iCurExp;               
        public ushort wId;                
        public byte btNameLength;         

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] szName;             

        public byte btEffectType;
        public byte btEffect;
        public byte btUnknown;
        public ushort wSpell;
        public ushort wPower;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] btNeedLevel;

        public ushort wUnknown2;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public int[] iLevelupExp;

        public byte btUnknown2;
        public byte job;
        public ushort wUnknown3;
        public ushort wDelayTime;
        public ushort wUnknown4;
        public byte btDefSpell;
        public byte btDefPower;
        public ushort wMaxPower;
        public ushort wDefMaxPower;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 18)]
        public byte[] btUnknown4;

        public MAGIC()
        {
            cKey = 0;
            btLevel = 0;
            wUnknown = 0;
            iCurExp = 0;
            wId = 0;
            btNameLength = 0;
            szName = new byte[12];
            btEffectType = 0;
            btEffect = 0;
            btUnknown = 0;
            wSpell = 0;
            wPower = 0;
            btNeedLevel = new byte[4];
            wUnknown2 = 0;
            iLevelupExp = new int[4];
            btUnknown2 = 0;
            job = 0;
            wUnknown3 = 0;
            wDelayTime = 0;
            wUnknown4 = 0;
            btDefSpell = 0;
            btDefPower = 0;
            wMaxPower = 0;
            wDefMaxPower = 0;
            btUnknown4 = new byte[18];
        }
    }

    
    
    
    public enum DbError
    {
        SE_OK = 0,                  
        SE_FAIL = 1,                
        SE_ALLOCMEMORYFAIL = 2,     
        SE_DB_NOMOREDATA = 3,       
        SE_DB_NOTINITED = 4,        
        SE_LOGIN_ACCOUNTEXIST = 100, 
        SE_LOGIN_ACCOUNTNOTEXIST = 101, 
        SE_LOGIN_PASSWORDERROR = 102, 
        SE_SELCHAR_CHAREXIST = 200, 
        SE_SELCHAR_NOTEXIST = 201,  
        SE_REG_INVALIDACCOUNT = 300, 
        SE_REG_INVALIDPASSWORD = 301, 
        SE_REG_INVALIDNAME = 302,   
        SE_REG_INVALIDBIRTHDAY = 303, 
        SE_REG_INVALIDPHONENUMBER = 304, 
        SE_REG_INVALIDMOBILEPHONE = 305, 
        SE_REG_INVALIDQUESTION = 306, 
        SE_REG_INVALIDANSWER = 307,  
        SE_REG_INVALIDIDCARD = 308,  
        SE_REG_INVALIDEMAIL = 309,   
        SE_CREATECHARACTER_INVALID_CHARNAME = 400, 
        SE_ODBC_SQLCONNECTFAIL = 500, 
        SE_ODBC_SQLEXECDIRECTFAIL = 501, 
    }

    
    
    
    public enum eColType
    {
        CT_STRING,      
        CT_TINYINT,     
        CT_SMALLINT,    
        CT_INTEGER,     
        CT_BIGINT,      
        CT_DATETIME,    
        CT_CODEDARRAY,  
    }

    
    
    
    public enum dbitemoperation
    {
        DIO_DELETE,
        DIO_UPDATEPOS,
        DIO_UPDATEOWNER,
        DIO_UPDATEDURA
    }

    
    
    
    public enum scmsg
    {
        SCM_START,
        
        
        
        
        
        
        
        
        
        SCM_REGISTERSERVER,
        
        
        
        
        
        
        
        SCM_GETSELCHARSERVERADDR,
        
        
        
        
        
        
        
        SCM_GETGAMESERVERADDR,
        
        
        
        
        
        SCM_UPDATESERVERINFO,
        
        
        
        
        
        
        
        SCM_FINDSERVER,
        
        
        
        
        
        
        
        
        
        
        
        
        
        SCM_MSGACROSSSERVER,
    }

    
    
    
    public enum MSG_ACROSS_SERVER
    {
        
        
        
        MAS_KICKCONNECTION,
        
        
        
        
        
        MAS_ENTERSELCHARSERVER,
        
        
        
        
        
        MAS_ENTERGAMESERVER,
        
        
        
        
        MAS_RESTARTGAME,
    }
}
