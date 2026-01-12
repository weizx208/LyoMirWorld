using System;
using System.IO;
using System.Collections.Generic;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    public class PhysicsMap
    {
        private int _width;
        private int _height;
        private uint[] _blockLayer;
        private int _maxBlockElements;
        private string _mapName = string.Empty;

        
        
        
        public PhysicsMap()
        {
            _width = 0;
            _height = 0;
            _blockLayer = null;
            _maxBlockElements = 0;
        }

        
        
        
        public int Width => _width;

        
        
        
        public int Height => _height;

        
        
        
        public string Name => _mapName;

        
        
        
        
        
        public bool LoadMap(string filename)
        {
            try
            {
                
                _mapName = Path.GetFileNameWithoutExtension(filename);
                
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    
                    uint dwTemp = reader.ReadUInt32();
                    _width = reader.ReadInt32();
                    _width = reader.ReadInt32(); 
                    _height = reader.ReadInt32();
                    
                    
                    int totalCells = _width * _height;
                    _maxBlockElements = (totalCells + 31) / 32;
                    _blockLayer = new uint[_maxBlockElements];
                    Array.Clear(_blockLayer, 0, _blockLayer.Length);
                    
                    
                    fs.Seek(dwTemp, SeekOrigin.Begin);
                    
                    int elemIndex = 0;
                    int bitIndex = 0;
                    
                    for (int i = 0; i < totalCells; i++)
                    {
                        byte flag = reader.ReadByte();
                        
                        
                        if ((flag & 1) != 0)
                        {
                            _blockLayer[elemIndex] |= (1u << bitIndex);
                        }
                        
                        
                        if ((flag & 2) != 0) reader.ReadBytes(2);
                        if ((flag & 4) != 0) reader.ReadBytes(2);
                        if ((flag & 8) != 0) reader.ReadBytes(4);
                        if ((flag & 16) != 0) reader.ReadByte();
                        if ((flag & 32) != 0) reader.ReadByte();
                        if ((flag & 64) != 0) reader.ReadByte();
                        if ((flag & 128) != 0) reader.ReadByte();
                        
                        bitIndex++;
                        if (bitIndex >= 32)
                        {
                            bitIndex = 0;
                            elemIndex++;
                            if (elemIndex >= _maxBlockElements)
                                break;
                        }
                    }
                }
                
                
                _mapName = _mapName.ToUpper();
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物理地图失败: {filename}, 错误: {ex.Message}");
                return false;
            }
        }

        
        
        
        
        
        public bool LoadCache(string cacheFilename)
        {
            try
            {
                
                _mapName = Path.GetFileNameWithoutExtension(cacheFilename);
                
                using (FileStream fs = new FileStream(cacheFilename, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    
                    uint header = reader.ReadUInt32();
                    if (header != 0x30434D44) 
                    {
                        LogManager.Default.Error($"物理地图缓存文件头错误: {cacheFilename}");
                        return false;
                    }
                    
                    _width = reader.ReadInt32();
                    _height = reader.ReadInt32();
                    _maxBlockElements = reader.ReadInt32();
                    
                    int totalCells = _width * _height;
                    int requiredElements = (totalCells + 31) / 32;
                    if (_maxBlockElements < requiredElements)
                        _maxBlockElements = requiredElements;
                    
                    _blockLayer = new uint[_maxBlockElements];
                    for (int i = 0; i < _maxBlockElements; i++)
                    {
                        _blockLayer[i] = reader.ReadUInt32();
                    }
                }
                
                
                _mapName = _mapName.ToUpper();
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物理地图缓存失败: {cacheFilename}, 错误: {ex.Message}");
                return false;
            }
        }

        
        
        
        
        
        public bool SaveCache(string path)
        {
            try
            {
                string cacheFilename = Path.Combine(path, _mapName + ".PMC");
                string directory = Path.GetDirectoryName(cacheFilename);
                if (!Directory.Exists(directory) && directory != null)
                {
                    Directory.CreateDirectory(directory);
                }
                
                using (FileStream fs = new FileStream(cacheFilename, FileMode.Create, FileAccess.Write))
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    
                    writer.Write(0x30434D44);
                    
                    writer.Write(_width);
                    writer.Write(_height);
                    writer.Write(_maxBlockElements);
                    
                    int totalCells = _width * _height;
                    int requiredElements = (totalCells + 31) / 32;
                    int writeElements = Math.Max(_maxBlockElements, requiredElements);
                    
                    for (int i = 0; i < writeElements; i++)
                    {
                        writer.Write(_blockLayer[i]);
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存物理地图缓存失败: {path}, 错误: {ex.Message}");
                return false;
            }
        }

        
        
        
        
        
        
        public bool IsBlocked(int x, int y)
        {
            
            if (!VerifyPos(x, y))
                return true;
            
            
            if (_blockLayer == null)
                return false;
            
            int cellIndex = y * _width + x;
            int elementIndex = cellIndex / 32;
            int bitIndex = cellIndex % 32;
            
            return ((_blockLayer[elementIndex] & (1u << bitIndex)) != 0);
        }

        
        
        
        private bool VerifyPos(int x, int y)
        {
            if (x < 0 || x >= _width)
                return false;
            if (y < 0 || y >= _height)
                return false;
            return true;
        }

        
        
        
        public bool AddRefMap(LogicMap map)
        {
            
            return true;
        }
    }
}
