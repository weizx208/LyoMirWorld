using System;

namespace GameServer
{
    
    
    
    public class Magic
    {
        public char cKey;                    
        public byte btLevel;                 
        public ushort wUnknown;              
        public int iCurExp;                  
        public ushort wId;                   
        public byte btNameLength;            
        public string szName = "";           
        public byte btEffectType;            
        public byte btEffect;                
        public byte btUnknown;               
        public ushort wSpell;                
        public ushort wPower;                
        public byte[] btNeedLevel = new byte[4]; 
        public ushort wUnknown2;             
        public int[] iLevelupExp = new int[4];   
        public byte btUnknown2;              
        public byte job;                     
        public ushort wUnknown3;             
        public ushort wDelayTime;            
        public ushort wUnknown4;             
        public byte btDefSpell;              
        public byte btDefPower;              
        public ushort wMaxPower;             
        public ushort wDefMaxPower;          
        public byte[] btUnknown4 = new byte[18]; 

        public Magic()
        {
            
            for (int i = 0; i < 4; i++)
            {
                btNeedLevel[i] = 0;
                iLevelupExp[i] = 0;
            }
            for (int i = 0; i < 18; i++)
            {
                btUnknown4[i] = 0;
            }
        }
    }

    
    
    
    public class MagicClass
    {
        public string szName = "";           
        public uint id;                      
        public byte btJob;                   
        public byte btEffectType;            
        public byte btEffectValue;           
        public byte[] btNeedLv = new byte[4];    
        public uint[] dwNeedExp = new uint[4];   
        public short sSpell;                 
        public short sPower;                 
        public short sMaxPower;              
        public short sDefSpell;              
        public short sDefPower;              
        public short sDefMaxPower;           
        public ushort wDelay;                
        public string szDesc = "";           
        public ushort[] wNeedMagic = new ushort[3];  
        public ushort[] wMutexMagic = new ushort[3]; 
        public uint dwFlag;                  
        public ushort wCharmCount;           
        public ushort wRedPoisonCount;       
        public ushort wGreenPoisonCount;     
        public ushort wStrawManCount;        
        public ushort wStrawWomanCount;      
        public string szSpecial = "";        

        public MagicClass()
        {
            
            for (int i = 0; i < 4; i++)
            {
                btNeedLv[i] = 0;
                dwNeedExp[i] = 0;
            }
            for (int i = 0; i < 3; i++)
            {
                wNeedMagic[i] = 0;
                wMutexMagic[i] = 0;
            }
        }
    }

    
    
    
    [Flags]
    public enum MagicFlag : uint
    {
        MAGICFLAG_NOEFFECT = 0x00000001,     
        MAGICFLAG_ACTIVED = 0x00000002,      
        MAGICFLAG_FORCED = 0x00000004,       
        MAGICFLAG_FORCED_EXP = 0x00000008,   
        MAGICFLAG_USECHARM = 0x00000010,     
        MAGICFLAG_USEREDPOISON = 0x00000020, 
        MAGICFLAG_USEGREENPOISON = 0x00000040, 
        MAGICFLAG_USESTRAWMAN = 0x00000080,  
        MAGICFLAG_USESTRAWWOMAN = 0x00000100 
    }
}
