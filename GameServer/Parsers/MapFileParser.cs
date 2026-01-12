using System;
using System.IO;
using MirCommon.Utils;

namespace GameServer.Parsers
{
    
    
    
    
    public class MapFileParser
    {
        
        
        
        public class MapData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[,]? Tiles { get; set; }  
            public byte[,]? Blocks { get; set; } 
            public string FileName { get; set; } = "";
        }

        
        
        
        
        
        
        
        
        
        
        
        public MapData? LoadMapFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"地图文件不存在: {filePath}");
                return null;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                var mapData = new MapData
                {
                    FileName = Path.GetFileNameWithoutExtension(filePath)
                };

                
                uint dataOffset = br.ReadUInt32();
                
                
                br.ReadInt32(); 
                mapData.Width = br.ReadInt32();
                
                
                mapData.Height = br.ReadInt32();

                
                int totalCells = mapData.Width * mapData.Height;
                int maxBlockElements = (totalCells + 31) / 32;
                uint[] blockLayer = new uint[maxBlockElements];

                
                fs.Seek(dataOffset, SeekOrigin.Begin);

                
                int elemIndex = 0;
                int bitIndex = 0;
                int remainingCells = totalCells;

                while (remainingCells > 0)
                {
                    byte flag = br.ReadByte();

                    
                    if ((flag & 1) != 0)
                    {
                        blockLayer[elemIndex] |= (uint)(1 << bitIndex);
                    }

                    
                    if ((flag & 2) != 0)
                        br.ReadUInt16();

                    
                    if ((flag & 4) != 0)
                        br.ReadUInt16();

                    
                    if ((flag & 8) != 0)
                        br.ReadUInt32();

                    
                    if ((flag & 16) != 0)
                        br.ReadByte();

                    
                    if ((flag & 32) != 0)
                        br.ReadByte();

                    
                    if ((flag & 64) != 0)
                        br.ReadByte();

                    
                    if ((flag & 128) != 0)
                        br.ReadByte();

                    bitIndex++;
                    if (bitIndex >= 32)
                    {
                        bitIndex = 0;
                        elemIndex++;
                        if (elemIndex >= maxBlockElements)
                            break;
                    }

                    remainingCells--;
                }

                
                mapData.Blocks = new byte[mapData.Width, mapData.Height];
                for (int y = 0; y < mapData.Height; y++)
                {
                    for (int x = 0; x < mapData.Width; x++)
                    {
                        int cellIndex = y * mapData.Width + x;
                        int arrayIndex = cellIndex / 32;
                        int bitPos = cellIndex % 32;
                        
                        if ((blockLayer[arrayIndex] & (1 << bitPos)) != 0)
                        {
                            mapData.Blocks[x, y] = 1; 
                        }
                        else
                        {
                            mapData.Blocks[x, y] = 0; 
                        }
                    }
                }

                LogManager.Default.Info($"成功加载地图: {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                return mapData;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载地图文件失败: {filePath}", exception: ex);
                return null;
            }
        }

        
        
        
        
        
        
        
        
        
        
        
        public MapData? LoadNMPFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"NMP地图文件不存在: {filePath}");
                return null;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                var mapData = new MapData
                {
                    FileName = Path.GetFileNameWithoutExtension(filePath)
                };

                
                mapData.Width = br.ReadInt32();
                mapData.Height = br.ReadInt32();

                
                int totalCells = mapData.Width * mapData.Height;
                
                
                long remainingBytes = fs.Length - fs.Position;
                
                
                if (remainingBytes == totalCells)
                {
                    
                    mapData.Blocks = new byte[mapData.Width, mapData.Height];
                    for (int y = 0; y < mapData.Height; y++)
                    {
                        for (int x = 0; x < mapData.Width; x++)
                        {
                            byte block = br.ReadByte();
                            mapData.Blocks[x, y] = block != 0 ? (byte)1 : (byte)0;
                        }
                    }
                    LogManager.Default.Info($"加载NMP地图(字节格式): {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                }
                else if (remainingBytes == (totalCells + 7) / 8)
                {
                    
                    mapData.Blocks = new byte[mapData.Width, mapData.Height];
                    int byteIndex = 0;
                    int bitIndex = 0;
                    byte currentByte = 0;
                    
                    for (int y = 0; y < mapData.Height; y++)
                    {
                        for (int x = 0; x < mapData.Width; x++)
                        {
                            if (bitIndex == 0)
                            {
                                currentByte = br.ReadByte();
                            }
                            
                            mapData.Blocks[x, y] = (currentByte & (1 << bitIndex)) != 0 ? (byte)1 : (byte)0;
                            
                            bitIndex++;
                            if (bitIndex >= 8)
                            {
                                bitIndex = 0;
                                byteIndex++;
                            }
                        }
                    }
                    LogManager.Default.Info($"加载NMP地图(位图格式): {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                }
                else
                {
                    
                    
                    fs.Seek(0, SeekOrigin.Begin);
                    uint dataOffset = br.ReadUInt32();
                    mapData.Width = br.ReadInt32();
                    mapData.Height = br.ReadInt32();
                    
                    
                    fs.Seek(dataOffset, SeekOrigin.Begin);
                    
                    totalCells = mapData.Width * mapData.Height;
                    mapData.Blocks = new byte[mapData.Width, mapData.Height];
                    
                    
                    for (int i = 0; i < totalCells; i++)
                    {
                        byte flag = br.ReadByte();
                        int x = i % mapData.Width;
                        int y = i / mapData.Width;
                        mapData.Blocks[x, y] = (flag & 1) != 0 ? (byte)1 : (byte)0;
                        
                        
                        if ((flag & 2) != 0) br.ReadBytes(2);
                        if ((flag & 4) != 0) br.ReadBytes(2);
                        if ((flag & 8) != 0) br.ReadBytes(4);
                        if ((flag & 16) != 0) br.ReadByte();
                        if ((flag & 32) != 0) br.ReadByte();
                        if ((flag & 64) != 0) br.ReadByte();
                        if ((flag & 128) != 0) br.ReadByte();
                    }
                    LogManager.Default.Info($"加载NMP地图(扩展格式): {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                }

                return mapData;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载NMP地图文件失败: {filePath}", exception: ex);
                return null;
            }
        }

        
        
        
        
        
        
        
        
        
        public bool SaveMapCache(MapData mapData, string cachePath)
        {
            try
            {
                string directory = Path.GetDirectoryName(cachePath) ?? "";
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var fs = new FileStream(cachePath, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);

                
                bw.Write((byte)'D');
                bw.Write((byte)'M');
                bw.Write((byte)'C');
                bw.Write((byte)'0');

                
                bw.Write(mapData.Width);
                bw.Write(mapData.Height);

                
                int totalCells = mapData.Width * mapData.Height;
                int maxBlockElements = (totalCells + 31) / 32;
                bw.Write(maxBlockElements);

                
                uint[] blockLayer = new uint[maxBlockElements];
                if (mapData.Blocks != null)
                {
                    for (int y = 0; y < mapData.Height; y++)
                    {
                        for (int x = 0; x < mapData.Width; x++)
                        {
                            if (mapData.Blocks[x, y] != 0)
                            {
                                int cellIndex = y * mapData.Width + x;
                                int arrayIndex = cellIndex / 32;
                                int bitPos = cellIndex % 32;
                                blockLayer[arrayIndex] |= (uint)(1 << bitPos);
                            }
                        }
                    }
                }

                
                foreach (var block in blockLayer)
                {
                    bw.Write(block);
                }

                LogManager.Default.Info($"成功保存地图缓存: {cachePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存地图缓存失败: {cachePath}", exception: ex);
                return false;
            }
        }

        
        
        
        
        
        
        
        
        
        public MapData? LoadMapCache(string cachePath)
        {
            if (!File.Exists(cachePath))
                return null;

            try
            {
                using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                
                byte[] magic = br.ReadBytes(4);
                if (magic[0] != 'D' || magic[1] != 'M' || magic[2] != 'C' || magic[3] != '0')
                {
                    LogManager.Default.Warning($"无效的地图缓存文件: {cachePath}");
                    return null;
                }

                var mapData = new MapData
                {
                    FileName = Path.GetFileNameWithoutExtension(cachePath)
                };

                
                mapData.Width = br.ReadInt32();
                mapData.Height = br.ReadInt32();

                
                int maxBlockElements = br.ReadInt32();

                
                int expectedElements = (mapData.Width * mapData.Height + 31) / 32;
                if (maxBlockElements < expectedElements)
                {
                    maxBlockElements = expectedElements;
                }

                
                uint[] blockLayer = new uint[maxBlockElements];
                for (int i = 0; i < maxBlockElements; i++)
                {
                    blockLayer[i] = br.ReadUInt32();
                }

                
                mapData.Blocks = new byte[mapData.Width, mapData.Height];
                for (int y = 0; y < mapData.Height; y++)
                {
                    for (int x = 0; x < mapData.Width; x++)
                    {
                        int cellIndex = y * mapData.Width + x;
                        int arrayIndex = cellIndex / 32;
                        int bitPos = cellIndex % 32;
                        
                        if (arrayIndex < blockLayer.Length &&
                            (blockLayer[arrayIndex] & (1 << bitPos)) != 0)
                        {
                            mapData.Blocks[x, y] = 1; 
                        }
                        else
                        {
                            mapData.Blocks[x, y] = 0; 
                        }
                    }
                }

                LogManager.Default.Info($"成功加载地图缓存: {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                return mapData;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载地图缓存失败: {cachePath}", exception: ex);
                return null;
            }
        }
    }

    
    
    
    
    public class LogicMapConfigParser
    {
        public class LogicMapConfig
        {
            public int MapID { get; set; }
            public string MapName { get; set; } = "";
            public string PhysicsMapFile { get; set; } = "";
            public string MiniMapFile { get; set; } = "";
            public int MinX { get; set; }
            public int MinY { get; set; }
            public int MaxX { get; set; }
            public int MaxY { get; set; }
        }

        private readonly System.Collections.Generic.List<LogicMapConfig> _maps = new();

        
        
        
        public bool LoadMapConfigs(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                LogManager.Default.Warning($"逻辑地图配置目录不存在: {directoryPath}");
                return false;
            }

            try
            {
                var files = Directory.GetFiles(directoryPath, "*.txt");
                int count = 0;

                foreach (var file in files)
                {
                    if (LoadMapConfig(file))
                        count++;
                }

                LogManager.Default.Info($"成功加载 {count} 个逻辑地图配置");
                return count > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载逻辑地图配置失败: {directoryPath}", exception: ex);
                return false;
            }
        }

        
        
        
        private bool LoadMapConfig(string filePath)
        {
            try
            {
                var parser = new TextFileParser();
                var kvPairs = parser.LoadKeyValue(filePath);

                var config = new LogicMapConfig();
                
                if (kvPairs.TryGetValue("MapID", out string? mapId))
                    config.MapID = int.Parse(mapId);
                if (kvPairs.TryGetValue("MapName", out string? mapName))
                    config.MapName = mapName;
                if (kvPairs.TryGetValue("PhysicsMap", out string? physicsMap))
                    config.PhysicsMapFile = physicsMap;

                _maps.Add(config);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Debug($"加载地图配置失败: {filePath} - {ex.Message}");
                return false;
            }
        }

        public System.Collections.Generic.IEnumerable<LogicMapConfig> GetAllMaps() => _maps;
    }
}
