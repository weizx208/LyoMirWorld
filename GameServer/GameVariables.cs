namespace GameServer
{
    
    
    
    
    public static class GameVariables
    {
        
        public static uint SandCityTakeTime { get; set; } = 0;
        public static uint WarTimeLong { get; set; } = 7200; 
        public static uint WarStartTime { get; set; } = 0;
        
        
        public static uint MaxPlayers { get; set; } = 1000;
        public static uint MaxMonsters { get; set; } = 5000;
        public static uint MaxNPCs { get; set; } = 1000;
        public static uint MaxItems { get; set; } = 10000;
        
        
        public static uint GameTime { get; set; } = 0;
        public static uint ServerStartTime { get; set; } = 0;
        
        
        public static bool IsServerRunning { get; set; } = true;
        public static bool IsMaintenance { get; set; } = false;
        
        
        public static void Initialize()
        {
            SandCityTakeTime = 0;
            WarTimeLong = 7200; 
            WarStartTime = 0;
            MaxPlayers = 1000;
            MaxMonsters = 5000;
            MaxNPCs = 1000;
            MaxItems = 10000;
            GameTime = 0;
            ServerStartTime = (uint)DateTimeOffset.Now.ToUnixTimeSeconds();
            IsServerRunning = true;
            IsMaintenance = false;
        }
        
        
        public static uint GetGameTime()
        {
            return (uint)DateTimeOffset.Now.ToUnixTimeSeconds() - ServerStartTime;
        }
        
        
        public static void UpdateGameTime()
        {
            GameTime = GetGameTime();
        }
        
        
        public static bool IsSandCityWarTime()
        {
            if (WarStartTime == 0 || WarTimeLong == 0)
                return false;
                
            uint currentTime = GetGameTime();
            return currentTime >= WarStartTime && currentTime <= WarStartTime + WarTimeLong;
        }
        
        
        public static uint GetSandCityWarRemainingTime()
        {
            if (!IsSandCityWarTime())
                return 0;
                
            uint currentTime = GetGameTime();
            return WarStartTime + WarTimeLong - currentTime;
        }
        
        
        public static void StartSandCityWar()
        {
            WarStartTime = GetGameTime();
        }
        
        
        public static void EndSandCityWar()
        {
            WarStartTime = 0;
        }
    }
}
