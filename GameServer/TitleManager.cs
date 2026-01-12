using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    public class Title
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RequiredLevel { get; set; }
        public int RequiredAchievement { get; set; }
        public string RequiredItem { get; set; } = string.Empty;
        public int RequiredItemCount { get; set; }
        public int BuffId { get; set; }
        public int Duration { get; set; } 
        public string Icon { get; set; } = string.Empty;
    }

    
    
    
    public class PlayerTitle
    {
        public int TitleId { get; set; }
        public DateTime AcquireTime { get; set; }
        public DateTime ExpireTime { get; set; }
        public bool IsActive { get; set; }
    }

    
    
    
    public class TitleManager
    {
        private static TitleManager? _instance;
        public static TitleManager Instance => _instance ??= new TitleManager();

        private readonly Dictionary<int, Title> _titles = new();
        private readonly Dictionary<uint, List<PlayerTitle>> _playerTitles = new();

        private TitleManager() { }

        
        
        
        public bool Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"称号配置文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 9)
                    {
                        if (int.TryParse(parts[0], out int id))
                        {
                            var title = new Title
                            {
                                Id = id,
                                Name = parts[1].Trim(),
                                Description = parts[2].Trim(),
                                RequiredLevel = int.Parse(parts[3].Trim()),
                                RequiredAchievement = int.Parse(parts[4].Trim()),
                                RequiredItem = parts[5].Trim(),
                                RequiredItemCount = int.Parse(parts[6].Trim()),
                                BuffId = int.Parse(parts[7].Trim()),
                                Duration = int.Parse(parts[8].Trim()),
                                Icon = parts.Length > 9 ? parts[9].Trim() : string.Empty
                            };

                            _titles[id] = title;
                            count++;
                        }
                    }
                }

                LogManager.Default.Info($"加载称号配置: {count} 个称号");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载称号配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        
        
        
        public Title? GetTitle(int id)
        {
            return _titles.TryGetValue(id, out var title) ? title : null;
        }

        
        
        
        public IEnumerable<Title> GetAllTitles()
        {
            return _titles.Values;
        }

        
        
        
        public bool AddPlayerTitle(uint playerId, int titleId)
        {
            if (!_titles.ContainsKey(titleId))
                return false;

            var title = _titles[titleId];
            if (!_playerTitles.ContainsKey(playerId))
                _playerTitles[playerId] = new List<PlayerTitle>();

            var playerTitle = new PlayerTitle
            {
                TitleId = titleId,
                AcquireTime = DateTime.Now,
                ExpireTime = title.Duration > 0 ? DateTime.Now.AddSeconds(title.Duration) : DateTime.MaxValue,
                IsActive = false
            };

            _playerTitles[playerId].Add(playerTitle);
            return true;
        }

        
        
        
        public bool RemovePlayerTitle(uint playerId, int titleId)
        {
            if (!_playerTitles.ContainsKey(playerId))
                return false;

            var removed = _playerTitles[playerId].RemoveAll(t => t.TitleId == titleId);
            return removed > 0;
        }

        
        
        
        public List<PlayerTitle> GetPlayerTitles(uint playerId)
        {
            return _playerTitles.TryGetValue(playerId, out var titles) ? titles : new List<PlayerTitle>();
        }

        
        
        
        public bool ActivatePlayerTitle(uint playerId, int titleId)
        {
            if (!_playerTitles.ContainsKey(playerId))
                return false;

            var titles = _playerTitles[playerId];
            foreach (var title in titles)
            {
                if (title.TitleId == titleId)
                {
                    
                    foreach (var t in titles)
                    {
                        t.IsActive = false;
                    }
                    
                    
                    title.IsActive = true;
                    return true;
                }
            }

            return false;
        }

        
        
        
        public bool DeactivatePlayerTitle(uint playerId, int titleId)
        {
            if (!_playerTitles.ContainsKey(playerId))
                return false;

            var titles = _playerTitles[playerId];
            foreach (var title in titles)
            {
                if (title.TitleId == titleId && title.IsActive)
                {
                    title.IsActive = false;
                    return true;
                }
            }

            return false;
        }

        
        
        
        public bool HasTitle(uint playerId, int titleId)
        {
            if (!_playerTitles.ContainsKey(playerId))
                return false;

            return _playerTitles[playerId].Exists(t => t.TitleId == titleId);
        }

        
        
        
        public bool CheckTitleRequirements(uint playerId, int titleId)
        {
            var player = GameWorld.Instance.GetPlayer(playerId);
            if (player == null || !_titles.ContainsKey(titleId))
                return false;

            var title = _titles[titleId];
            
            
            if (player.Level < title.RequiredLevel)
                return false;

            
            
            

            
            if (!string.IsNullOrEmpty(title.RequiredItem) && title.RequiredItemCount > 0)
            {
                
                
                
                
            }

            return true;
        }

        
        
        
        public void Update()
        {
            var now = DateTime.Now;
            var expiredPlayers = new List<uint>();

            foreach (var kvp in _playerTitles)
            {
                var playerId = kvp.Key;
                var titles = kvp.Value;
                
                
                titles.RemoveAll(t => t.ExpireTime < now && t.ExpireTime != DateTime.MaxValue);
                
                if (titles.Count == 0)
                    expiredPlayers.Add(playerId);
            }

            
            foreach (var playerId in expiredPlayers)
            {
                _playerTitles.Remove(playerId);
            }
        }
    }
}
