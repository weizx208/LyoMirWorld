using System;

namespace GameServer
{
    
    
    
    
    public class GlobeProcess
    {
        public uint ProcessId { get; set; }
        public GlobeProcessType Type { get; set; }
        public uint Param1 { get; set; }
        public uint Param2 { get; set; }
        public uint Param3 { get; set; }
        public uint Param4 { get; set; }
        public uint Delay { get; set; }
        public int RepeatTimes { get; set; }
        public string? StringParam { get; set; }
        public DateTime ExecuteTime { get; set; }
        public DateTime CreateTime { get; set; }

        public GlobeProcess(GlobeProcessType type)
        {
            ProcessId = 0;
            Type = type;
            ExecuteTime = DateTime.Now;
            CreateTime = DateTime.Now;
        }

        public GlobeProcess(GlobeProcessType type, uint param1, uint param2 = 0, uint param3 = 0, uint param4 = 0, 
            uint delay = 0, int repeatTimes = 0, string? stringParam = null)
        {
            ProcessId = 0;
            Type = type;
            Param1 = param1;
            Param2 = param2;
            Param3 = param3;
            Param4 = param4;
            Delay = delay;
            RepeatTimes = repeatTimes;
            StringParam = stringParam;
            ExecuteTime = DateTime.Now.AddMilliseconds(delay);
            CreateTime = DateTime.Now;
        }

        public bool ShouldExecute()
        {
            return DateTime.Now >= ExecuteTime;
        }

        public void Execute()
        {
            
            
        }

        public override string ToString()
        {
            return $"GlobeProcess[Id={ProcessId}, Type={Type}, ExecuteTime={ExecuteTime}, Delay={Delay}ms]";
        }
    }

    
    
    
    
    public enum GlobeProcessType
    {
        None = 0,
        SandCityWarStart = 1,           
        SandCityWarEnd = 2,             
        SandCityNpcShow = 3,            
        SandCityNpcHide = 4,            
        SystemMessage = 5,              
        BroadcastMessage = 6,           
        PlayerKick = 7,                 
        PlayerBan = 8,                  
        ServerShutdown = 9,             
        ServerRestart = 10,             
        DatabaseSave = 11,              
        LogCleanup = 12,                
        MonsterGen = 13,                
        ItemCleanup = 14,               
        GuildWarStart = 15,             
        GuildWarEnd = 16,               
        QuestUpdate = 17,               
        BuffUpdate = 18,                
        TimeSystemUpdate = 19,          
        EventManagerUpdate = 20,        
        AutoScriptUpdate = 21,          
        MapScriptUpdate = 22,           
        ScriptVariableUpdate = 23,      
        TopManagerUpdate = 24,          
        MarketManagerUpdate = 25,       
        SpecialEquipmentUpdate = 26,    
        TitleManagerUpdate = 27,        
        TaskManagerUpdate = 28,         
        Max = 29
    }
}
