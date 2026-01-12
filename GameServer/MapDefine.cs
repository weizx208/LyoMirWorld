using System;

namespace GameServer
{
    
    
    
    
    
    
    
    [Flags]
    public enum MapFlag : uint
    {
        MF_NONE = 0x00000000,      
        MF_MINE = 0x00000001,      
        MF_FIGHT = 0x00000002,     
        MF_SAFE = 0x00000004,      
        MF_NOPK = 0x00000008,      
        MF_NOMONSTER = 0x00000010, 
        MF_NOPET = 0x00000020,     
        MF_NOMOUNT = 0x00000040,   
        MF_NOTELEPORT = 0x00000080, 
        MF_NORECALL = 0x00000100,  
        MF_NODROP = 0x00000200,    
        MF_NOGUILDWAR = 0x00000400, 
        MF_NODUEL = 0x00000800,    
        MF_NOSKILL = 0x00001000,   
        MF_NOITEM = 0x00002000,    
        MF_NOSPELL = 0x00004000,   
        MF_NORUN = 0x00008000,     
        MF_NOWALK = 0x00010000,    
        MF_NOSIT = 0x00020000,     
        MF_NOSTAND = 0x00040000,   
        MF_NODIE = 0x00080000,     
        MF_NORESPAWN = 0x00100000, 
        MF_NOLOGOUT = 0x00200000,  
        MF_NOSAVE = 0x00400000,    
        MF_NOLOAD = 0x00800000,    
        MF_NOSCRIPT = 0x01000000,  
        MF_NOEVENT = 0x02000000,   
        MF_NOMESSAGE = 0x04000000, 
        MF_NOCHAT = 0x08000000,    
        MF_NOWHISPER = 0x10000000, 
        MF_NOSHOUT = 0x20000000,   
        MF_NOTRADE = 0x40000000,   
        MF_NOSTORE = 0x80000000,   
        MF_DAY = 0x00000001,       
        MF_NIGHT = 0x00000002,     
        MF_WEATHER = 0x00000004    
    }

    
    
    
    public class WeatherInfo
    {
        public ushort WeatherIndex { get; set; }
        public ushort Flag { get; set; }
        public uint WeatherColor { get; set; }
        public uint BGColor { get; set; }

        public WeatherInfo()
        {
            WeatherIndex = 0;
            Flag = 0;
            WeatherColor = 0xFFFFFFFF;
            BGColor = 0;
        }
    }
}
