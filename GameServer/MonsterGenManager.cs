using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class MonsterGenManager
    {
        private static MonsterGenManager? _instance;
        public static MonsterGenManager Instance => _instance ??= new MonsterGenManager();

        
        
        
        private static readonly IReadOnlyDictionary<int, int> MissingMonGensFallbackMapId = new Dictionary<int, int>
        {
            
            [47] = 43, 
            [48] = 44, 
            [49] = 45, 
            [50] = 45, 
            [51] = 45, 
            [52] = 46, 
        };

        
        private readonly MonsterGen?[] _monsterGens = new MonsterGen?[100000];
        private int _monsterGenCount = 0;
        private int _refreshMonGenIndex = 0;

        
        private readonly Dictionary<int, List<MonsterGen>> _monsterGensByMapId = new();
        private readonly HashSet<int> _initialSpawnEnsuredMaps = new();
        private readonly object _genIndexLock = new();

        
        private readonly Queue<MonsterGen> _monsterGenPool = new();
        private readonly object _poolLock = new();
        private int _poolCacheSize = 1000; 
        private int _poolUsedCount = 0;
        private int _poolFreeCount = 0;

        private MonsterGenManager()
        {
            
            CacheObjects(_poolCacheSize);
        }
        
        
        
        
        private void CacheObjects(int count)
        {
            lock (_poolLock)
            {
                for (int i = 0; i < count; i++)
                {
                    _monsterGenPool.Enqueue(new MonsterGen());
                    _poolFreeCount++;
                }
                LogManager.Default.Debug($"对象池缓存了 {count} 个MonsterGen对象");
            }
        }
        
        
        
        
        private MonsterGen AllocFromPool()
        {
            lock (_poolLock)
            {
                if (_monsterGenPool.Count == 0)
                {
                    
                    int expandSize = Math.Max(100, _poolCacheSize / 2);
                    CacheObjects(expandSize);
                    _poolCacheSize += expandSize;
                }
                
                var obj = _monsterGenPool.Dequeue();
                _poolFreeCount--;
                _poolUsedCount++;
                
                
                ResetMonsterGen(obj);
                
                return obj;
            }
        }
        
        
        
        
        private void ReturnToPool(MonsterGen gen)
        {
            if (gen == null)
                return;

            lock (_poolLock)
            {
                
                ResetMonsterGen(gen);
                
                
                _monsterGenPool.Enqueue(gen);
                _poolUsedCount--;
                _poolFreeCount++;
            }
        }
        
        
        
        
        private void ResetMonsterGen(MonsterGen gen)
        {
            gen.MonsterName = string.Empty;
            gen.MapId = 0;
            gen.X = 0;
            gen.Y = 0;
            gen.Range = 0;
            gen.MaxCount = 0;
            gen.RefreshDelay = 0;
            gen.CurrentCount = 0;
            gen.ErrorTime = 0;
            gen.LastRefreshTime = DateTime.MinValue;
            gen.ScriptPage = null;
            gen.StartWhenAllDead = false;
        }

        private void IndexMonsterGen(MonsterGen gen)
        {
            if (gen == null)
                return;

            lock (_genIndexLock)
            {
                if (!_monsterGensByMapId.TryGetValue(gen.MapId, out var list))
                {
                    list = new List<MonsterGen>();
                    _monsterGensByMapId[gen.MapId] = list;
                }
                list.Add(gen);
            }
        }

        
        
        
        
        public void EnsureInitialSpawnForMap(int mapId)
        {
            if (mapId <= 0)
                return;

            MonsterGen[] gens;
            lock (_genIndexLock)
            {
                if (!_initialSpawnEnsuredMaps.Add(mapId))
                    return;

                if (!_monsterGensByMapId.TryGetValue(mapId, out var list) || list.Count == 0)
                    return;

                gens = list.ToArray();
            }

            int spawned = 0;
            int total = gens.Length;
            for (int i = 0; i < gens.Length; i++)
            {
                var gen = gens[i];
                if (gen == null)
                    continue;

                
                if (gen.MapId != mapId || string.IsNullOrEmpty(gen.MonsterName))
                    continue;

                if (gen.CurrentCount >= gen.MaxCount)
                    continue;

                if (UpdateGenPtr(gen, gen.MaxCount + 1))
                {
                    gen.LastRefreshTime = DateTime.Now;
                    spawned++;
                }
            }

            if (spawned > 0)
                LogManager.Default.Debug($"地图初始怪物补齐生成: mapId={mapId}, gens={total}, touched={spawned}");
        }
        
        
        
        
        public (int total, int used, int free) GetPoolStats()
        {
            lock (_poolLock)
            {
                return (_poolCacheSize, _poolUsedCount, _poolFreeCount);
            }
        }

        
        
        
        
        public bool LoadMonGen(string path)
        {
            LogManager.Default.Info($"加载怪物生成配置文件: {path}");

            if (!Directory.Exists(path))
            {
                LogManager.Default.Error($"怪物生成配置目录不存在: {path}");
                return false;
            }

            try
            {
                
                var files = Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in files)
                {
                    if (LoadMonGenFile(file))
                    {
                        loadedCount++;
                    }
                }

                
                ApplyMissingMonGensFallbacks();

                LogManager.Default.Info($"成功加载 {loadedCount} 个怪物生成配置文件");
                return loadedCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物生成配置文件失败: {path}", exception: ex);
                return false;
            }
        }

        private void ApplyMissingMonGensFallbacks()
        {
            foreach (var kv in MissingMonGensFallbackMapId)
            {
                int targetMapId = kv.Key;
                int sourceMapId = kv.Value;

                if (HasAnyValidGenForMap(targetMapId))
                    continue;

                int added = CloneGensWithScale(sourceMapId, targetMapId);
                if (added <= 0)
                    continue;

                string targetName = GetMapNameForLog(targetMapId);
                string sourceName = GetMapNameForLog(sourceMapId);
                LogManager.Default.Warning(
                    $"地图刷怪点缺失，已自动映射补齐: {targetName}(ID:{targetMapId}) <= {sourceName}(ID:{sourceMapId}), gens={added}"
                );
            }
        }

        private bool HasAnyValidGenForMap(int mapId)
        {
            lock (_genIndexLock)
            {
                if (!_monsterGensByMapId.TryGetValue(mapId, out var list) || list.Count == 0)
                    return false;

                for (int i = 0; i < list.Count; i++)
                {
                    var g = list[i];
                    if (g != null && g.MapId == mapId && !string.IsNullOrEmpty(g.MonsterName))
                        return true;
                }

                return false;
            }
        }

        private int CloneGensWithScale(int sourceMapId, int targetMapId)
        {
            MonsterGen[] sourceGens;
            lock (_genIndexLock)
            {
                if (!_monsterGensByMapId.TryGetValue(sourceMapId, out var list) || list.Count == 0)
                    return 0;

                
                sourceGens = list.ToArray();
            }

            var srcMap = LogicMapMgr.Instance.GetLogicMapById((uint)sourceMapId);
            var dstMap = LogicMapMgr.Instance.GetLogicMapById((uint)targetMapId);
            if (srcMap == null || dstMap == null)
                return 0;

            if (srcMap.Width <= 0 || srcMap.Height <= 0 || dstMap.Width <= 0 || dstMap.Height <= 0)
                return 0;

            double scaleX = (dstMap.Width - 1) / (double)Math.Max(1, srcMap.Width - 1);
            double scaleY = (dstMap.Height - 1) / (double)Math.Max(1, srcMap.Height - 1);
            double scaleR = Math.Min(scaleX, scaleY);
            double areaRatio = (dstMap.Width * (double)dstMap.Height) / (srcMap.Width * (double)srcMap.Height);

            int added = 0;
            for (int i = 0; i < sourceGens.Length; i++)
            {
                var src = sourceGens[i];
                if (src == null)
                    continue;

                
                if (src.MapId != sourceMapId || string.IsNullOrEmpty(src.MonsterName))
                    continue;

                if (_monsterGenCount >= _monsterGens.Length)
                    break;

                var gen = AllocFromPool();
                gen.MonsterName = src.MonsterName;
                gen.MapId = targetMapId;

                int scaledX = ClampToMap((int)Math.Round(src.X * scaleX), dstMap.Width);
                int scaledY = ClampToMap((int)Math.Round(src.Y * scaleY), dstMap.Height);

                
                if (!TrySnapToWalkable(dstMap, ref scaledX, ref scaledY))
                {
                    ReturnToPool(gen);
                    continue;
                }

                gen.X = scaledX;
                gen.Y = scaledY;

                int scaledRange = (int)Math.Round(src.Range * scaleR);
                if (src.Range > 0 && scaledRange < 1)
                    scaledRange = 1;
                gen.Range = scaledRange;

                
                int scaledMaxCount = (int)Math.Round(src.MaxCount * areaRatio);
                if (scaledMaxCount < 1)
                    scaledMaxCount = 1;
                if (scaledMaxCount > src.MaxCount)
                    scaledMaxCount = src.MaxCount;

                gen.MaxCount = scaledMaxCount;
                gen.RefreshDelay = src.RefreshDelay;
                gen.CurrentCount = 0;
                gen.ErrorTime = 0;
                gen.LastRefreshTime = DateTime.MinValue;
                gen.ScriptPage = src.ScriptPage;
                gen.StartWhenAllDead = src.StartWhenAllDead;

                _monsterGens[_monsterGenCount++] = gen;
                IndexMonsterGen(gen);
                added++;
            }

            return added;
        }

        private bool TrySnapToWalkable(LogicMap map, ref int x, ref int y)
        {
            if (map == null)
                return false;

            
            if (!IsPhysicsBlocked(map, x, y))
                return true;

            
            const int snapRange = 80; 
            var pt = GetValidPoint(map, x, y, snapRange);
            if (pt != null)
            {
                x = pt.Value.X;
                y = pt.Value.Y;
                return true;
            }

            
            if (TryGetRandomWalkable(map, 5000, out int rx, out int ry))
            {
                x = rx;
                y = ry;
                return true;
            }

            return false;
        }

        private bool TryGetRandomWalkable(LogicMap map, int maxAttempts, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (map == null || map.Width <= 0 || map.Height <= 0)
                return false;

            if (maxAttempts < 1)
                maxAttempts = 1;

            var random = Random.Shared;
            for (int i = 0; i < maxAttempts; i++)
            {
                int tx = random.Next(0, map.Width);
                int ty = random.Next(0, map.Height);
                if (!IsPhysicsBlocked(map, tx, ty))
                {
                    x = tx;
                    y = ty;
                    return true;
                }
            }

            return false;
        }

        private static int ClampToMap(int value, int maxExclusive)
        {
            if (maxExclusive <= 0)
                return 0;
            if (value < 0)
                return 0;
            if (value >= maxExclusive)
                return maxExclusive - 1;
            return value;
        }

        private static string GetMapNameForLog(int mapId)
        {
            try
            {
                var map = LogicMapMgr.Instance.GetLogicMapById((uint)mapId);
                if (map != null && !string.IsNullOrWhiteSpace(map.MapName))
                    return map.MapName;
            }
            catch
            {
                
            }

            return $"map{mapId}";
        }

        
        
        
        private bool LoadMonGenFile(string fileName)
        {
            try
            {
                var lines = SmartReader.ReadAllLines(fileName);
                int count = 0;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    if (AddMonsterGen(trimmedLine))
                    {
                        count++;
                    }
                }

                
                return count > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物生成文件失败: {fileName}", exception: ex);
                return false;
            }
        }

        
        
        
        
        
        public bool AddMonsterGen(string genDesc)
        {
            if (_monsterGenCount >= 100000)
            {
                LogManager.Default.Error($"怪物生成点数量已达上限 (100000)");
                return false;
            }

            var parts = genDesc.Split('/');
            if (parts.Length < 7)
            {
                LogManager.Default.Warning($"怪物生成点格式错误: {genDesc}");
                return false;
            }

            string monsterName = parts[0].Trim();
            
            
            var monsterClass = MonsterManagerEx.Instance.GetClassByName(monsterName);
            if (monsterClass == null)
            {
                LogManager.Default.Warning($"怪物生成点中出现未设置的怪物: {monsterName}");
                return false;
            }

            
            if (!int.TryParse(parts[1], out int mapId) ||
                !int.TryParse(parts[2], out int x) ||
                !int.TryParse(parts[3], out int y) ||
                !int.TryParse(parts[4], out int range) ||
                !int.TryParse(parts[5], out int count) ||
                !int.TryParse(parts[6], out int refreshDelay))
            {
                LogManager.Default.Warning($"怪物生成点参数解析失败: {genDesc}");
                return false;
            }

            
            var map = LogicMapMgr.Instance.GetLogicMapById((uint)mapId);
            if (map == null || x < 0 || y < 0 || x >= map.Width || y >= map.Height)
            {
                LogManager.Default.Warning($"怪物生成点地图不存在或坐标越界: map({mapId})({x},{y})");
                return false;
            }

            
            MonsterGen gen = AllocFromPool();

            
            gen.MonsterName = monsterName;
            gen.MapId = mapId;
            gen.X = x;
            gen.Y = y;
            gen.Range = range;
            gen.MaxCount = count;
            gen.RefreshDelay = refreshDelay * 1000; 
            gen.CurrentCount = 0;
            gen.ErrorTime = 0;
            gen.LastRefreshTime = DateTime.MinValue;
            gen.StartWhenAllDead = false;

            
            string refreshDelayStr = parts[6];
            if (refreshDelayStr.StartsWith("*"))
            {
                gen.StartWhenAllDead = true;
                refreshDelayStr = refreshDelayStr.Substring(1);
                if (int.TryParse(refreshDelayStr, out int actualDelay))
                {
                    gen.RefreshDelay = actualDelay * 1000;
                }
            }

            
            if (parts.Length > 7)
            {
                gen.ScriptPage = parts[7];
            }

            
            _monsterGens[_monsterGenCount++] = gen;
            IndexMonsterGen(gen);

            
            return true;
        }

        
        
        
        
        public void UpdateGen()
        {
            if (_monsterGenCount == 0)
                return;

            
            if (_refreshMonGenIndex >= _monsterGenCount)
                _refreshMonGenIndex = 0;

            var gen = _monsterGens[_refreshMonGenIndex];
            int currentIndex = _refreshMonGenIndex;
            _refreshMonGenIndex++;

            if (gen == null)
                return;

            
            if (gen.StartWhenAllDead)
            {
                if (gen.CurrentCount > 0)
                {
                    gen.LastRefreshTime = DateTime.Now;
                    return;
                }
            }

            int needRefreshCount = gen.StartWhenAllDead ? gen.MaxCount : gen.MaxCount - gen.CurrentCount;
            if (needRefreshCount <= 0)
                return;

            
            if (gen.LastRefreshTime != DateTime.MinValue &&
                (DateTime.Now - gen.LastRefreshTime).TotalMilliseconds < gen.RefreshDelay)
                return;

            
            if (!UpdateGenPtr(gen, needRefreshCount))
            {
                
                _monsterGens[currentIndex] = null;
                ReturnToPool(gen);
            }

            gen.LastRefreshTime = DateTime.Now;
        }

        
        
        
        
        public void InitAllGen()
        {
            if (_monsterGenCount == 0)
                return;

            LogManager.Default.Info($"初始化怪物生成点... 总数: {_monsterGenCount}");

            for (int i = 0; i < _monsterGenCount; i++)
            {
                var gen = _monsterGens[i];
                if (gen == null)
                    continue;

                if (!UpdateGenPtr(gen, gen.MaxCount + 1))
                {
                    
                    ReturnToPool(gen);
                    _monsterGens[i] = null;
                }
                else
                {
                    gen.LastRefreshTime = DateTime.Now;
                }
            }

            LogManager.Default.Info($"怪物生成点初始化完成");
        }

        
        
        
        
        private bool UpdateGenPtr(MonsterGen gen, int maxCount, bool setGenPtr = true, bool gotoTarget = false, ushort targetX = 0, ushort targetY = 0)
        {
            if (gen == null)
                return false;

            var map = LogicMapMgr.Instance.GetLogicMapById((uint)gen.MapId);
            if (map == null)
            {
                LogManager.Default.Error($"怪物生成点地图不存在: map({gen.MapId})({gen.X},{gen.Y}) 怪物:{gen.MonsterName}");
                return false;
            }

            int mapWidth = map.Width;
            int mapHeight = map.Height;

            int successCount = 0;
            int startX = gen.X - gen.Range;
            int endX = gen.X + gen.Range;
            int startY = gen.Y - gen.Range;
            int endY = gen.Y + gen.Range;

            Random random = new();

            for (int i = 0; i < maxCount && gen.CurrentCount < gen.MaxCount; i++)
            {
                
                int tx = random.Next(startX, endX + 1);
                int ty = random.Next(startY, endY + 1);

                
                if (tx < 0) tx = 0;
                if (tx >= mapWidth) tx = mapWidth - 1;
                if (ty < 0) ty = 0;
                if (ty >= mapHeight) ty = mapHeight - 1;

                
                if (IsPhysicsBlocked(map, tx, ty))
                {
                    
                    var validPoint = GetValidPoint(map, tx, ty, 1);
                    if (validPoint == null)
                        continue;

                    tx = validPoint.Value.X;
                    ty = validPoint.Value.Y;
                }

                
                var monster = MonsterManagerEx.Instance.CreateMonster(gen.MonsterName, gen.MapId, tx, ty, gen);
                if (monster == null)
                    continue;

                
                successCount++;
                gen.CurrentCount++;

                if (successCount >= maxCount)
                    return true;
            }

            
            if (gen.CurrentCount == 0)
            {
                gen.ErrorTime++;
                if (gen.ErrorTime > 10)
                {
                    LogManager.Default.Error($"怪物生成点连续10次生成失败，被禁用: map({gen.MapId})({gen.X},{gen.Y}) 怪物:{gen.MonsterName}");
                    return false;
                }
            }
            else
            {
                gen.ErrorTime = 0;
            }

            return true;
        }

        
        
        
        public int GetGenCount()
        {
            return _monsterGenCount;
        }

        
        
        
        public List<MonsterGen> GetAllGens()
        {
            var list = new List<MonsterGen>();
            for (int i = 0; i < _monsterGenCount; i++)
            {
                if (_monsterGens[i] != null)
                {
                    list.Add(_monsterGens[i]!);
                }
            }
            return list;
        }

        
        
        
        public void ClearAllGens()
        {
            for (int i = 0; i < _monsterGenCount; i++)
            {
                if (_monsterGens[i] != null)
                {
                    ReturnToPool(_monsterGens[i]!);
                    _monsterGens[i] = null;
                }
            }
            _monsterGenCount = 0;
            _refreshMonGenIndex = 0;

            lock (_genIndexLock)
            {
                _monsterGensByMapId.Clear();
                _initialSpawnEnsuredMaps.Clear();
            }
            
            
            var stats = GetPoolStats();
            LogManager.Default.Info($"清理所有怪物生成点，对象池统计: 总数={stats.total}, 使用中={stats.used}, 空闲={stats.free}");
        }

        
        
        
        private bool IsPhysicsBlocked(LogicMap map, int x, int y)
        {
            
            if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
                return true;

            
            var physicsMap = map.GetPhysicsMap();
            if (physicsMap == null)
                return true;

            return physicsMap.IsBlocked(x, y);
        }

        
        
        
        private Point? GetValidPoint(LogicMap map, int centerX, int centerY, int range)
        {
            if (range <= 0)
                return null;

            
            for (int r = 0; r <= range; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                            continue;

                        int x = centerX + dx;
                        int y = centerY + dy;

                        
                        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
                            continue;

                        
                        if (!IsPhysicsBlocked(map, x, y))
                        {
                            return new Point(x, y);
                        }
                    }
                }
            }

            return null;
        }

        
        
        
        private bool AddMapObjectToWorld(MonsterEx monster)
        {
            if (monster == null)
                return false;

            
            var map = LogicMapMgr.Instance.GetLogicMapById((uint)monster.MapId);
            if (map == null)
                return false;

            
            return map.AddObject(monster, monster.X, monster.Y);
        }

        
        
        
        private struct Point
        {
            public int X { get; set; }
            public int Y { get; set; }

            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
