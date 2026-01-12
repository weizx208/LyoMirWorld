using System;
using System.Collections.Generic;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public static class GameWorldExtensions
    {
        
        
        
        
        public static void PostSystemMessage(this GameWorld gameWorld, string message)
        {
            LogManager.Default.Info($"[系统消息] {message}");
            
            
        }

        
        
        
        
        public static void HideSandCityNpc(this GameWorld gameWorld)
        {
            LogManager.Default.Info("隐藏沙城NPC");
            
            
        }

        
        
        
        
        public static void ShowSandCityNpc(this GameWorld gameWorld)
        {
            LogManager.Default.Info("显示沙城NPC");
            
            
        }

        
        
        
        
        public static void AddGlobeProcess(this GameWorld gameWorld, GlobeProcess process)
        {
            LogManager.Default.Info($"添加全局进程: {process}");
            
            
        }

        
        
        
        
        public static List<MapObject> GetObjList(this LogicMap logicMap)
        {
            
            return new List<MapObject>();
        }

        
        
        
        
        public static void SetMapEventFlagRect(this LogicMap logicMap, int x1, int y1, int x2, int y2, EventFlag flag, bool value)
        {
            
            
            
        }

        
        
        
        
        public static void SetFlag(this LogicMap logicMap, int flag, bool value)
        {
            
            
            
        }
    }

    
    
    
    public static class NpcExtensions
    {
        public static string GetName(this Npc npc)
        {
            return npc.Name;
        }

        public static void SetLongName(this Npc npc, string longName)
        {
            
            LogManager.Default.Info($"设置NPC长名称: {npc.Name} -> {longName}");
        }

        public static void SendChangeName(this Npc npc)
        {
            
            LogManager.Default.Info($"发送NPC名称变更: {npc.Name}");
        }

        public static void SetView(this Npc npc, uint view)
        {
            
            LogManager.Default.Info($"设置NPC外观: {npc.Name} -> {view}");
        }

        public static void SendFeatureChanged(this Npc npc)
        {
            
            LogManager.Default.Info($"发送NPC外观变更: {npc.Name}");
        }

        public static uint GetView(this Npc npc)
        {
            
            return 0;
        }

        public static int GetMapId(this Npc npc)
        {
            return npc.MapId;
        }

        public static int GetX(this Npc npc)
        {
            return npc.X;
        }

        public static int GetY(this Npc npc)
        {
            return npc.Y;
        }
    }

    
    
    
    public static class GuildExExtensions
    {
        public static string GetName(this GuildEx guild)
        {
            return guild.Name;
        }

        public static void SetAttackSabuk(this GuildEx guild, bool value)
        {
            
            LogManager.Default.Info($"设置行会攻击沙城标志: {guild.Name} -> {value}");
        }

        public static void RefreshMemberName(this GuildEx guild)
        {
            
            LogManager.Default.Info($"刷新行会成员名称: {guild.Name}");
        }

        public static bool IsMaster(this GuildEx guild, HumanPlayer player)
        {
            
            if (guild == null || player == null)
                return false;
            
            
            return guild.LeaderId == player.ObjectId;
        }

        public static bool IsAttackSabuk(this GuildEx guild)
        {
            
            return false;
        }

        public static bool IsFirstMaster(this GuildEx guild, HumanPlayer player)
        {
            
            return false;
        }
    }

    
    
    
    public static class HumanPlayerExtensions
    {
        public static GuildEx? GetGuild(this HumanPlayer player)
        {
            
            return null;
        }

        public static bool IsDeath(this HumanPlayer player)
        {
            return player.IsDead;
        }

        public static void FlyTo(this HumanPlayer player, uint mapId, uint x, uint y)
        {
            
            LogManager.Default.Info($"玩家飞往: {player.Name} -> ({mapId},{x},{y})");
        }

        public static int GetPro(this HumanPlayer player)
        {
            
            return 0;
        }

        public static int GetSex(this HumanPlayer player)
        {
            
            return 0;
        }

        public static uint GetDBId(this HumanPlayer player)
        {
            
            return player.ObjectId;
        }

        public static uint GetPropValue(this HumanPlayer player, PropIndex index)
        {
            
            return 0;
        }

        public static string GetName(this HumanPlayer player)
        {
            return player.Name;
        }
    }

    
    
    
    public static class TopManagerExtensions
    {
        public static uint GetTopView(this TopManager topManager)
        {
            
            return 0;
        }
    }

    
    
    
    public static class GuildManagerEx
    {
        public static GuildEx? GetGuild(uint guildId)
        {
            
            return null;
        }

        public static GuildEx? GetGuildByName(string name)
        {
            
            return null;
        }
    }

    
    
    
    public class SettingFile
    {
        public string GetString(string section, string key, string defaultValue = "")
        {
            return defaultValue;
        }

        public int GetInt(string section, string key, int defaultValue = 0)
        {
            return defaultValue;
        }

        public uint GetUInt(string section, string key, uint defaultValue = 0)
        {
            return defaultValue;
        }

        public bool GetBool(string section, string key, bool defaultValue = false)
        {
            return defaultValue;
        }
    }
}
