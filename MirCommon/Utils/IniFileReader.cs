using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MirCommon.Utils
{
    
    
    
    
    public class IniFileReader
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections = new();
        private readonly string _filePath;

        public IniFileReader(string filePath)
        {
            _filePath = filePath;
        }

        
        
        
        public bool Open()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    Console.WriteLine($"配置文件不存在: {_filePath}");
                    return false;
                }

                var lines = SmartReader.ReadAllLines(_filePath);
                string currentSection = "";

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    
                    if (string.IsNullOrWhiteSpace(trimmedLine) || 
                        trimmedLine.StartsWith(";") || 
                        trimmedLine.StartsWith("#"))
                        continue;

                    
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                        if (!_sections.ContainsKey(currentSection))
                        {
                            _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }
                        continue;
                    }

                    
                    var equalIndex = trimmedLine.IndexOf('=');
                    if (equalIndex > 0)
                    {
                        var key = trimmedLine.Substring(0, equalIndex).Trim();
                        var value = trimmedLine.Substring(equalIndex + 1).Trim();

                        
                        var section = string.IsNullOrEmpty(currentSection) ? "" : currentSection;
                        
                        if (!_sections.ContainsKey(section))
                        {
                            _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }

                        _sections[section][key] = value;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取配置文件失败: {ex.Message}");
                return false;
            }
        }

        
        
        
        
        
        
        public string GetString(string? section, string key, string defaultValue = "")
        {
            section = section ?? "";
            
            if (_sections.TryGetValue(section, out var sectionData))
            {
                if (sectionData.TryGetValue(key, out var value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        
        
        
        
        
        
        public int GetInteger(string? section, string key, int defaultValue = 0)
        {
            var strValue = GetString(section, key, defaultValue.ToString());
            
            if (int.TryParse(strValue, out var result))
            {
                return result;
            }

            return defaultValue;
        }

        
        
        
        
        
        
        public bool GetBoolean(string? section, string key, bool defaultValue = false)
        {
            var strValue = GetString(section, key, defaultValue.ToString());
            
            
            strValue = strValue.ToLower().Trim();
            if (strValue == "1" || strValue == "true" || strValue == "yes" || strValue == "on")
                return true;
            if (strValue == "0" || strValue == "false" || strValue == "no" || strValue == "off")
                return false;

            return defaultValue;
        }

        
        
        
        public bool HasSection(string section)
        {
            return _sections.ContainsKey(section ?? "");
        }

        
        
        
        public bool HasKey(string? section, string key)
        {
            section = section ?? "";
            if (_sections.TryGetValue(section, out var sectionData))
            {
                return sectionData.ContainsKey(key);
            }
            return false;
        }

        
        
        
        public IEnumerable<string> GetSections()
        {
            return _sections.Keys;
        }

        
        
        
        public IEnumerable<string> GetKeys(string? section)
        {
            section = section ?? "";
            if (_sections.TryGetValue(section, out var sectionData))
            {
                return sectionData.Keys;
            }
            return Array.Empty<string>();
        }
    }
}
