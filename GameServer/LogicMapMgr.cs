using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class LogicMapMgr
    {
        private static LogicMapMgr? _instance;
        private readonly Dictionary<uint, LogicMap> _mapsById = new();
        private readonly Dictionary<string, LogicMap> _mapsByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingPhysicsMapsLogged = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();
        private const int MAX_LOGIC_MAP = 10240;

        
        
        
        public static LogicMapMgr Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LogicMapMgr();
                }
                return _instance;
            }
        }

        
        
        
        private LogicMapMgr()
        {
        }

        
        
        
        
        
        public void Load(string path)
        {
            lock (_lock)
            {
                try
                {
                    LogManager.Default.Info($"开始加载逻辑地图配置: {path}");

                    if (!Directory.Exists(path))
                    {
                        LogManager.Default.Warning($"逻辑地图配置目录不存在: {path}");
                        return;
                    }

                    
                    var iniFiles = Directory.GetFiles(path, "*.ini", SearchOption.AllDirectories);
                    int loadedCount = 0;

                    foreach (var iniFile in iniFiles)
                    {
                        try
                        {
                            var map = LoadMapFromIni(iniFile);
                            if (map != null)
                            {
                                
                                _mapsById[map.MapId] = map;
                                _mapsByName[map.MapName] = map;
                                loadedCount++;

                                
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Default.Error($"加载逻辑地图文件失败: {iniFile}", exception: ex);
                        }
                    }

                    
                    InitMapLinks();

                    LogManager.Default.Info($"成功加载 {loadedCount} 个逻辑地图配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载逻辑地图配置失败: {path}", exception: ex);
                }
            }
        }

        private static string NormalizeMissingPhysicsMapKey(string upperBlockmap)
        {
            
            if (upperBlockmap.StartsWith("RTG", StringComparison.OrdinalIgnoreCase))
                return "RTG*";

            return upperBlockmap;
        }

        
        
        
        
        
        private LogicMap? LoadMapFromIni(string iniFile)
        {
            try
            {
                
                var ini = new IniFile(iniFile);
                
                
                string blockmap = ini.GetString("define", "blockmap", "");
                if (string.IsNullOrEmpty(blockmap))
                {
                    LogManager.Default.Warning($"地图配置文件缺少blockmap: {iniFile}");
                    return null;
                }

                
                string mapName = ini.GetString("define", "name", "");
                if (string.IsNullOrEmpty(mapName))
                {
                    LogManager.Default.Warning($"地图配置文件缺少name: {iniFile}");
                    return null;
                }

                
                int miniMap = ini.GetInt("define", "minimap", 0);

                
                uint mapId = (uint)ini.GetInt("define", "mapid", 0);
                if (mapId == 0)
                {
                    LogManager.Default.Warning($"地图配置文件缺少mapid: {iniFile}");
                    return null;
                }

                
                int linkcount = ini.GetInt("define", "linkcount", 0);

                
                int expfactor = ini.GetInt("define", "expfactor", 100);
                float expFactor = expfactor / 100.0f;

                
                string flagStr = ini.GetString("define", "flag", "");

                
                string upperBlockmap = blockmap.ToUpper();

                
                var physicsMap = PhysicsMapMgr.Instance.GetPhysicsMapByName(upperBlockmap);
                if (physicsMap == null)
                {
                    
                    
                    string missingKey = NormalizeMissingPhysicsMapKey(upperBlockmap);
                    if (_missingPhysicsMapsLogged.Add(missingKey))
                    {
                        LogManager.Default.Warning(
                            $"物理地图资源缺失: {upperBlockmap}，将跳过加载逻辑地图 {mapName}(ID:{mapId}). " +
                            $"请补齐 ./data/maps/pm_cache/{upperBlockmap}.PMC 或 ./data/maps/physics/{upperBlockmap}.nmp"
                        );
                    }
                    return null;
                }

                
                int width = physicsMap.Width;
                int height = physicsMap.Height;

                
                var map = new LogicMap(mapId, mapName, width, height)
                {
                    
                    MapFile = blockmap
                };

                
                map.SetPhysicsMap(physicsMap);

                
                map.SetMiniMap(miniMap);

                
                map.ExpFactor = expFactor;

                
                if (!string.IsNullOrEmpty(flagStr))
                {
                    
                    string[] flags = flagStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string flag in flags)
                    {
                        string trimmedFlag = flag.Trim();
                        if (!string.IsNullOrEmpty(trimmedFlag))
                        {
                            
                            
                            map.SetFlag(trimmedFlag);
                        }
                    }
                }

                
                map.SetLinkCount(linkcount);

                
                map.InitMapCells();

                
                if (linkcount > 0)
                {
                    for (int i = 1; i <= linkcount; i++)
                    {
                        string key = $"linkpoint{i}";
                        string value = ini.GetString("linkpoint", key, "");
                        if (string.IsNullOrWhiteSpace(value))
                            continue;

                        if (!TryParseLinkPoint(value, out int sx, out int sy, out uint targetMapId, out int tx, out int ty))
                        {
                            LogManager.Default.Warning($"地图链接点解析失败: mapId={mapId}, key={key}, value='{value}'");
                            continue;
                        }

                        
                        try
                        {
                            var ev = new ChangeMapEvent(targetMapId, (ushort)tx, (ushort)ty);
                            if (!map.AddObject(ev, sx, sy))
                            {
                                LogManager.Default.Warning($"无法添加地图链接事件: mapId={mapId}, pos=({sx},{sy}), to=[{targetMapId}]({tx},{ty})");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Default.Warning($"创建地图链接事件失败: mapId={mapId}, pos=({sx},{sy}), to=[{targetMapId}]({tx},{ty}) - {ex.Message}");
                        }
                    }
                }

                
                return map;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析地图配置文件失败: {iniFile}", exception: ex);
                return null;
            }
        }

        private static bool TryParseLinkPoint(string value, out int sx, out int sy, out uint targetMapId, out int tx, out int ty)
        {
            sx = sy = tx = ty = 0;
            targetMapId = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            
            string s = value.Trim();

            int p1 = s.IndexOf('(');
            int p2 = s.IndexOf(')');
            if (p1 < 0 || p2 <= p1)
                return false;

            string src = s.Substring(p1 + 1, p2 - p1 - 1);
            var srcParts = src.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (srcParts.Length != 2)
                return false;
            if (!int.TryParse(srcParts[0], out sx) || !int.TryParse(srcParts[1], out sy))
                return false;

            int b1 = s.IndexOf('[', p2 + 1);
            int b2 = s.IndexOf(']', b1 + 1);
            if (b1 < 0 || b2 <= b1)
                return false;

            string mapPart = s.Substring(b1 + 1, b2 - b1 - 1).Trim();
            if (!uint.TryParse(mapPart, out targetMapId))
                return false;

            int p3 = s.IndexOf('(', b2 + 1);
            int p4 = s.IndexOf(')', p3 + 1);
            if (p3 < 0 || p4 <= p3)
                return false;

            string dst = s.Substring(p3 + 1, p4 - p3 - 1);
            var dstParts = dst.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (dstParts.Length != 2)
                return false;
            if (!int.TryParse(dstParts[0], out tx) || !int.TryParse(dstParts[1], out ty))
                return false;

            return true;
        }

        
        
        
        
        
        private void InitMapLinks()
        {
            int linkCount = 0;
            
            foreach (var map in _mapsById.Values)
            {
                try
                {
                    map.InitLinks();
                    linkCount += map.GetLinkCount();
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"初始化地图链接失败: {map.MapName}", exception: ex);
                }
            }
            
            LogManager.Default.Info($"地图链接初始化完成，共处理 {linkCount} 个链接");
        }

        
        
        
        
        public LogicMap? GetLogicMapById(uint mapId)
        {
            
            lock (_lock)
            {
                if (mapId == 0 || mapId > MAX_LOGIC_MAP)
                    return null;

                return _mapsById.TryGetValue(mapId, out var map) ? map : null;
            }
        }

        
        
        
        
        public LogicMap? GetLogicMapByName(string mapName)
        {
            lock (_lock)
            {
                return _mapsByName.TryGetValue(mapName, out var map) ? map : null;
            }
        }

        
        
        
        
        public int GetMapCount()
        {
            lock (_lock)
            {
                return _mapsById.Count;
            }
        }

        
        
        
        public List<LogicMap> GetAllMaps()
        {
            lock (_lock)
            {
                return new List<LogicMap>(_mapsById.Values);
            }
        }

        
        
        
        public bool HasMap(uint mapId)
        {
            lock (_lock)
            {
                return _mapsById.ContainsKey(mapId);
            }
        }

        
        
        
        public bool HasMap(string mapName)
        {
            lock (_lock)
            {
                return _mapsByName.ContainsKey(mapName);
            }
        }

        
        
        
        public void AddMap(LogicMap map)
        {
            lock (_lock)
            {
                if (map == null)
                    return;

                _mapsById[map.MapId] = map;
                _mapsByName[map.MapName] = map;
            }
        }

        
        
        
        public bool RemoveMap(uint mapId)
        {
            lock (_lock)
            {
                if (!_mapsById.TryGetValue(mapId, out var map))
                    return false;

                _mapsById.Remove(mapId);
                _mapsByName.Remove(map.MapName);
                return true;
            }
        }

        
        
        
        public void ClearAllMaps()
        {
            lock (_lock)
            {
                _mapsById.Clear();
                _mapsByName.Clear();
            }
        }

        
        
        
        public void Reload(string path)
        {
            lock (_lock)
            {
                ClearAllMaps();
                Load(path);
            }
        }

        
        
        
        public string GetMapName(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.MapName ?? "未知地图";
        }

        
        
        
        public uint GetMapId(string mapName)
        {
            var map = GetLogicMapByName(mapName);
            return map?.MapId ?? 0;
        }

        
        
        
        public bool CanTeleportTo(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowTeleport ?? false;
        }

        
        
        
        public bool CanRecallTo(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowRecall ?? false;
        }

        
        
        
        public bool IsSafeZone(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.IsSafeZone ?? false;
        }

        
        
        
        public bool AllowPK(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowPK ?? false;
        }

        
        
        
        public bool AllowPets(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowPets ?? false;
        }

        
        
        
        public bool AllowMounts(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowMounts ?? false;
        }

        
        
        
        public float GetExpFactor(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.ExpFactor ?? 1.0f;
        }

        
        
        
        public float GetDropFactor(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.DropFactor ?? 1.0f;
        }

        
        
        
        
        
        
        private MapFlag GetMapFlagFromString(string flagStr)
        {
            
            int paramStart = flagStr.IndexOf('(');
            if (paramStart > 0)
            {
                
                string flagName = flagStr.Substring(0, paramStart).Trim().ToUpper();
                return GetMapFlagFromName(flagName);
            }
            else
            {
                
                return GetMapFlagFromName(flagStr.ToUpper());
            }
        }
        
        
        
        
        private MapFlag GetMapFlagFromName(string flagName)
        {
            
            switch (flagName)
            {
                case "SABUKPALACE":
                    return MapFlag.MF_NONE; 
                case "FIGHTMAP":
                    return MapFlag.MF_FIGHT;
                case "NORANDOMMOVE":
                    return MapFlag.MF_NORUN;
                case "NORECONNECT":
                    return MapFlag.MF_NONE; 
                case "RIDEHORSE":
                    return MapFlag.MF_NOMOUNT;
                case "LEVELABOVE":
                case "LEVELBELOW":
                    return MapFlag.MF_NONE; 
                case "LIMITJOB":
                    return MapFlag.MF_NONE; 
                case "PKPOINTABOVE":
                case "PKPOINTBELOW":
                    return MapFlag.MF_NONE; 
                case "NOESCAPE":
                    return MapFlag.MF_NOTELEPORT;
                case "NOHOME":
                    return MapFlag.MF_NORECALL;
                case "MINE":
                    return MapFlag.MF_MINE;
                case "WEATHER":
                case "DAY":
                case "NIGHT":
                    return MapFlag.MF_NONE; 
                case "NOGROUPMOVE":
                    return MapFlag.MF_NONE; 
                case "SANDCITYHOME":
                    return MapFlag.MF_NONE; 
                case "NODMOVE":
                    return MapFlag.MF_NOWALK;
                case "NOFLASHMOVE":
                    return MapFlag.MF_NOTELEPORT;
                case "USERDEFINE1":
                case "USERDEFINE2":
                case "USERDEFINE3":
                case "USERDEFINE4":
                    return MapFlag.MF_NONE; 
                case "SAFE":
                    return MapFlag.MF_SAFE;
                case "NOPK":
                    return MapFlag.MF_NOPK;
                case "NOMONSTER":
                    return MapFlag.MF_NOMONSTER;
                case "NOPET":
                    return MapFlag.MF_NOPET;
                case "NODROP":
                    return MapFlag.MF_NODROP;
                case "NOGUILDWAR":
                    return MapFlag.MF_NOGUILDWAR;
                case "NODUEL":
                    return MapFlag.MF_NODUEL;
                case "NOSKILL":
                    return MapFlag.MF_NOSKILL;
                case "NOITEM":
                    return MapFlag.MF_NOITEM;
                case "NOSPELL":
                    return MapFlag.MF_NOSPELL;
                case "NOSIT":
                    return MapFlag.MF_NOSIT;
                case "NOSTAND":
                    return MapFlag.MF_NOSTAND;
                case "NODIE":
                    return MapFlag.MF_NODIE;
                case "NORESPAWN":
                    return MapFlag.MF_NORESPAWN;
                case "NOLOGOUT":
                    return MapFlag.MF_NOLOGOUT;
                case "NOSAVE":
                    return MapFlag.MF_NOSAVE;
                case "NOLOAD":
                    return MapFlag.MF_NOLOAD;
                case "NOSCRIPT":
                    return MapFlag.MF_NOSCRIPT;
                case "NOEVENT":
                    return MapFlag.MF_NOEVENT;
                case "NOMESSAGE":
                    return MapFlag.MF_NOMESSAGE;
                case "NOCHAT":
                    return MapFlag.MF_NOCHAT;
                case "NOWHISPER":
                    return MapFlag.MF_NOWHISPER;
                case "NOSHOUT":
                    return MapFlag.MF_NOSHOUT;
                case "NOTRADE":
                    return MapFlag.MF_NOTRADE;
                case "NOSTORE":
                    return MapFlag.MF_NOSTORE;
                default:
                    LogManager.Default.Warning($"未知的地图标志: {flagName}");
                    return MapFlag.MF_NONE;
            }
        }
    }
}
