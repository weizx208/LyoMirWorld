using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    public struct BundleInfo
    {
        public string Name;
        
        public string ExtractName;
        
        public int Count;
        
        public BundleInfo(string name, string extractName, int count)
        {
            Name = name;
            ExtractName = extractName;
            Count = count;
        }
    }

    public class BundleManager
    {
        private static BundleManager? _instance;
        
        public static BundleManager Instance => _instance ??= new BundleManager();
        
        private readonly Dictionary<string, BundleInfo> _bundleHash = new Dictionary<string, BundleInfo>(StringComparer.OrdinalIgnoreCase);
        
        private BundleManager()
        {
        }
        
        public void LoadBundle(string bundleFile, bool isCsv = false)
        {
            if (!File.Exists(bundleFile))
            {
                LogManager.Default.Warning($"捆绑配置文件不存在: {bundleFile}");
                return;
            }
            
            try
            {
                string[] lines = SmartReader.ReadAllLines(bundleFile);
                
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;
                    
                    string[] parts;
                    
                    if (isCsv)
                    {
                        parts = line.Split(',');
                    }
                    else
                    {
                        parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    
                    if (parts.Length < 3)
                    {
                        LogManager.Default.Warning($"捆绑配置行格式错误: {line}");
                        continue;
                    }
                    
                    string name = parts[0].Trim();
                    string extractName = parts[1].Trim();
                    
                    if (!int.TryParse(parts[2].Trim(), out int count))
                    {
                        LogManager.Default.Warning($"捆绑配置数量解析失败: {line}");
                        continue;
                    }
                    
                    if (name.Length > 20) name = name.Substring(0, 20);
                    if (extractName.Length > 20) extractName = extractName.Substring(0, 20);
                    
                    var bundleInfo = new BundleInfo(name, extractName, count);
                    
                    _bundleHash[name] = bundleInfo;
                }
                
                LogManager.Default.Info($"成功加载 {_bundleHash.Count} 个捆绑配置");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载捆绑配置文件失败: {bundleFile}", exception: ex);
            }
        }
        
        public bool GetBundleInfo(string name, out string extractItemName, out int count)
        {
            extractItemName = string.Empty;
            count = 0;
            
            if (string.IsNullOrEmpty(name))
                return false;
            
            if (_bundleHash.TryGetValue(name, out BundleInfo bundleInfo))
            {
                extractItemName = bundleInfo.ExtractName;
                count = bundleInfo.Count;
                return true;
            }
            
            return false;
        }
        
        public BundleInfo? GetBundleInfo(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            
            if (_bundleHash.TryGetValue(name, out BundleInfo bundleInfo))
            {
                return bundleInfo;
            }
            
            return null;
        }
        
        public bool ContainsBundle(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            
            return _bundleHash.ContainsKey(name);
        }
        
        public List<string> GetAllBundleNames()
        {
            return new List<string>(_bundleHash.Keys);
        }
        
        public List<BundleInfo> GetAllBundleInfos()
        {
            return new List<BundleInfo>(_bundleHash.Values);
        }
        
        public int GetBundleCount()
        {
            return _bundleHash.Count;
        }
        
        public void Clear()
        {
            _bundleHash.Clear();
            LogManager.Default.Info("已清空所有捆绑配置");
        }
        
        public void ReloadBundle(string bundleFile, bool isCsv = false)
        {
            Clear();
            LoadBundle(bundleFile, isCsv);
        }
        
        public bool AddOrUpdateBundle(string name, string extractName, int count)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(extractName) || count <= 0)
                return false;
            
            if (name.Length > 20) name = name.Substring(0, 20);
            if (extractName.Length > 20) extractName = extractName.Substring(0, 20);
            
            var bundleInfo = new BundleInfo(name, extractName, count);
            _bundleHash[name] = bundleInfo;
            
            return true;
        }
        
        public bool RemoveBundle(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            
            return _bundleHash.Remove(name);
        }
    }
}
