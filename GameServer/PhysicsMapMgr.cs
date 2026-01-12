using System;
using System.Collections.Generic;
using System.IO;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class PhysicsMapMgr
    {
        private static PhysicsMapMgr _instance;
        private readonly Dictionary<string, PhysicsMap> _mapDictionary;
        private string _physicsMapPath = string.Empty;
        private string _physicsCachePath = string.Empty;
        private bool _useCache = false;

        
        
        
        public static PhysicsMapMgr Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PhysicsMapMgr();
                }
                return _instance;
            }
        }

        
        
        
        private PhysicsMapMgr()
        {
            _mapDictionary = new Dictionary<string, PhysicsMap>(StringComparer.OrdinalIgnoreCase);
            _useCache = false;
        }

        
        
        
        
        
        public void Init(string physicsMapPath, string physicsCachePath)
        {
            _physicsMapPath = physicsMapPath ?? string.Empty;
            _physicsCachePath = physicsCachePath ?? string.Empty;

            
            if (Directory.Exists(_physicsCachePath))
            {
                _useCache = true;
                LogManager.Default.Info("地图CACHE功能启用，将大大提高地图读取速度！");
            }
            else
            {
                _useCache = false;
                LogManager.Default.Warning("地图CACHE路径不可用，CACHE被禁用，可能导致读取时间过长！");
                
                
                if (!Directory.Exists(_physicsMapPath))
                {
                    LogManager.Default.Error("物理地图路径不可用，物理地图无法正常读取！");
                }
            }
        }

        
        
        
        
        
        public PhysicsMap Load(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                return null;

            
            if (_mapDictionary.TryGetValue(mapName, out var existingMap))
                return existingMap;

            PhysicsMap map = new PhysicsMap();
            bool loaded = false;

            
            if (_useCache)
            {
                string cacheFilename = Path.Combine(_physicsCachePath, mapName + ".PMC");
                if (File.Exists(cacheFilename))
                {
                    loaded = map.LoadCache(cacheFilename);
                    if (loaded)
                    {
                        
                    }
                }
            }

            
            if (!loaded)
            {
                string mapFilename = Path.Combine(_physicsMapPath, mapName + ".nmp");
                if (File.Exists(mapFilename))
                {
                    loaded = map.LoadMap(mapFilename);
                    if (loaded)
                    {
                        LogManager.Default.Debug($"从原始文件加载物理地图: {mapName}");
                        
                        
                        if (_useCache)
                        {
                            map.SaveCache(_physicsCachePath);
                        }
                    }
                }
            }

            if (loaded)
            {
                _mapDictionary[map.Name] = map;
                return map;
            }
            else
            {
                
                return null;
            }
        }

        
        
        
        
        
        public PhysicsMap GetPhysicsMapByName(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                return null;

            
            if (_mapDictionary.TryGetValue(mapName, out var map))
                return map;

            
            return Load(mapName);
        }

        
        
        
        
        
        public bool IsMapLoaded(string mapName)
        {
            return _mapDictionary.ContainsKey(mapName);
        }

        
        
        
        public int LoadedMapCount => _mapDictionary.Count;

        
        
        
        public void ClearAllMaps()
        {
            _mapDictionary.Clear();
            LogManager.Default.Info("已清除所有物理地图");
        }

        
        
        
        public List<string> GetAllMapNames()
        {
            return new List<string>(_mapDictionary.Keys);
        }

        
        
        
        
        
        
        
        public bool IsBlocked(string mapName, int x, int y)
        {
            var map = GetPhysicsMapByName(mapName);
            if (map == null)
                return true; 

            return map.IsBlocked(x, y);
        }

        
        
        
        
        
        public int PreloadMaps(string[] mapNames)
        {
            int successCount = 0;
            foreach (var mapName in mapNames)
            {
                if (string.IsNullOrEmpty(mapName))
                    continue;

                var map = Load(mapName);
                if (map != null)
                    successCount++;
            }
            
            LogManager.Default.Info($"预加载地图完成，成功: {successCount}/{mapNames.Length}");
            return successCount;
        }
    }
}
