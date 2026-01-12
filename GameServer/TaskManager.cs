using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    public enum TaskType
    {
        Main = 0,       
        Side = 1,       
        Daily = 2,      
        Weekly = 3,     
        Achievement = 4, 
        Event = 5       
    }

    
    
    
    public enum TaskStatus
    {
        NotStarted = 0, 
        InProgress = 1, 
        Completed = 2,  
        Failed = 3,     
        Abandoned = 4   
    }

    
    
    
    public class TaskObjective
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty; 
        public string TargetName { get; set; } = string.Empty;
        public int RequiredCount { get; set; }
        public int CurrentCount { get; set; }
        public bool IsCompleted => CurrentCount >= RequiredCount;
    }

    
    
    
    public class TaskReward
    {
        public int Exp { get; set; }
        public int Gold { get; set; }
        public List<TaskRewardItem> Items { get; set; } = new();
        public List<int> Buffs { get; set; } = new();
        public string? NextTask { get; set; }
    }

    
    
    
    public class TaskRewardItem
    {
        public string ItemName { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Chance { get; set; } = 100; 
    }

    
    
    
    public class TaskDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public int RequiredLevel { get; set; }
        public string? PredecessorTask { get; set; } 
        public List<TaskObjective> Objectives { get; set; } = new();
        public TaskReward Reward { get; set; } = new();
        public int TimeLimit { get; set; } 
        public int MaxAttempts { get; set; } = 1; 
        public bool Repeatable { get; set; } 
        public int RepeatInterval { get; set; } 
    }

    
    
    
    public class PlayerTask
    {
        public int TaskId { get; set; }
        public TaskStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompleteTime { get; set; }
        public DateTime? ExpireTime { get; set; }
        public int Attempts { get; set; }
        public List<TaskObjective> Objectives { get; set; } = new();
        public bool RewardClaimed { get; set; }
    }

    
    
    
    public class TaskManager
    {
        private static TaskManager? _instance;
        public static TaskManager Instance => _instance ??= new TaskManager();

        private readonly Dictionary<int, TaskDefinition> _taskDefinitions = new();
        private readonly Dictionary<uint, List<PlayerTask>> _playerTasks = new();

        private TaskManager() { }

        
        
        
        public bool Load(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                LogManager.Default.Warning($"任务目录不存在: {directoryPath}");
                return false;
            }

            try
            {
                var taskFiles = Directory.GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in taskFiles)
                {
                    if (LoadTaskFile(file))
                        loadedCount++;
                }

                LogManager.Default.Info($"加载任务配置: {loadedCount} 个任务文件, {_taskDefinitions.Count} 个任务定义");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载任务配置失败: {directoryPath}", exception: ex);
                return false;
            }
        }

        
        
        
        private bool LoadTaskFile(string filePath)
        {
            try
            {
                var lines = SmartReader.ReadAllLines(filePath);

                
                if (Array.Exists(lines, l => string.Equals(l.Trim(), "[setup]", StringComparison.OrdinalIgnoreCase)))
                {
                    return LoadLegacySetupTaskFile(filePath, lines);
                }

                
                
                
                
                
                TaskDefinition? currentTask = null;
                int taskCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var trimmedLine = line.Trim();

                    
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        var taskDef = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        var parts = taskDef.Split(':');

                        if (parts.Length >= 2 && int.TryParse(parts[0], out int taskId))
                        {
                            currentTask = new TaskDefinition
                            {
                                Id = taskId,
                                Name = parts[1].Trim()
                            };

                            if (parts.Length > 2)
                                currentTask.Description = parts[2].Trim();

                            _taskDefinitions[taskId] = currentTask;
                            taskCount++;
                        }
                    }
                    
                    else if (currentTask != null && trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();

                            ParseTaskProperty(currentTask, key, value);
                        }
                    }
                    
                    else if (currentTask != null && trimmedLine.StartsWith("objective:"))
                    {
                        var objectiveStr = trimmedLine.Substring(10).Trim();
                        var objective = ParseTaskObjective(objectiveStr);
                        if (objective != null)
                            currentTask.Objectives.Add(objective);
                    }
                    
                    else if (currentTask != null && trimmedLine.StartsWith("reward:"))
                    {
                        var rewardStr = trimmedLine.Substring(7).Trim();
                        ParseTaskReward(currentTask.Reward, rewardStr);
                    }
                }

                
                return taskCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载任务文件失败: {filePath}", exception: ex);
                return false;
            }
        }

        private static string NormalizeLegacyText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            
            return value.Replace("\\", "\n");
        }

        private bool LoadLegacySetupTaskFile(string filePath, string[] lines)
        {
            
            var setup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool inSetup = false;

            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith("#"))
                    continue;

                var line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    inSetup = string.Equals(line, "[setup]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inSetup)
                    continue;

                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                    continue;

                setup[parts[0].Trim()] = parts[1].Trim();
            }

            if (!setup.TryGetValue("ID", out var idStr) || !int.TryParse(idStr, out var id))
            {
                LogManager.Default.Warning($"任务文件缺少ID或格式不正确: {Path.GetFileName(filePath)}");
                return false;
            }

            setup.TryGetValue("title", out var title);
            setup.TryGetValue("stepDESC1", out var stepDesc1);

            var task = new TaskDefinition
            {
                Id = id,
                Name = (title ?? Path.GetFileNameWithoutExtension(filePath)).Trim(),
                Description = NormalizeLegacyText(stepDesc1 ?? string.Empty),
                Type = TaskType.Side
            };

            
            
            var text = task.Description;
            var m = System.Text.RegularExpressions.Regex.Match(text, @"(?<cur>\d+)\s*/\s*(?<req>\d+)\s*个\s*(?<name>[^\s。\.\r\n]+)");
            if (m.Success && int.TryParse(m.Groups["req"].Value, out var reqCount))
            {
                var targetName = m.Groups["name"].Value.Trim();
                if (!string.IsNullOrEmpty(targetName) && reqCount > 0)
                {
                    task.Objectives.Add(new TaskObjective
                    {
                        Id = 1,
                        Description = $"消灭 {reqCount} 个 {targetName}",
                        TargetType = "monster",
                        TargetName = targetName,
                        RequiredCount = reqCount,
                        CurrentCount = 0
                    });
                }
            }

            _taskDefinitions[id] = task;

            
            return true;
        }

        
        
        
        private void ParseTaskProperty(TaskDefinition task, string key, string value)
        {
            switch (key.ToLower())
            {
                case "type":
                    if (Enum.TryParse<TaskType>(value, true, out var type))
                        task.Type = type;
                    break;
                case "requiredlevel":
                    if (int.TryParse(value, out int level))
                        task.RequiredLevel = level;
                    break;
                case "predecessor":
                    task.PredecessorTask = value;
                    break;
                case "timelimit":
                    if (int.TryParse(value, out int timeLimit))
                        task.TimeLimit = timeLimit;
                    break;
                case "maxattempts":
                    if (int.TryParse(value, out int maxAttempts))
                        task.MaxAttempts = maxAttempts;
                    break;
                case "repeatable":
                    task.Repeatable = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                    break;
                case "repeatinterval":
                    if (int.TryParse(value, out int repeatInterval))
                        task.RepeatInterval = repeatInterval;
                    break;
            }
        }

        
        
        
        private TaskObjective? ParseTaskObjective(string objectiveStr)
        {
            var parts = objectiveStr.Split(',');
            if (parts.Length < 4)
                return null;

            if (int.TryParse(parts[0], out int id) && int.TryParse(parts[3], out int requiredCount))
            {
                return new TaskObjective
                {
                    Id = id,
                    Description = parts[1].Trim(),
                    TargetType = parts[2].Trim(),
                    TargetName = parts.Length > 4 ? parts[4].Trim() : string.Empty,
                    RequiredCount = requiredCount,
                    CurrentCount = 0
                };
            }

            return null;
        }

        
        
        
        private void ParseTaskReward(TaskReward reward, string rewardStr)
        {
            var parts = rewardStr.Split(';');
            
            foreach (var part in parts)
            {
                var rewardParts = part.Split('=');
                if (rewardParts.Length == 2)
                {
                    var key = rewardParts[0].Trim();
                    var value = rewardParts[1].Trim();
                    
                    switch (key.ToLower())
                    {
                        case "exp":
                            if (int.TryParse(value, out int exp))
                                reward.Exp = exp;
                            break;
                        case "gold":
                            if (int.TryParse(value, out int gold))
                                reward.Gold = gold;
                            break;
                        case "item":
                            var itemParts = value.Split('*');
                            if (itemParts.Length == 2 && int.TryParse(itemParts[1], out int count))
                            {
                                reward.Items.Add(new TaskRewardItem
                                {
                                    ItemName = itemParts[0].Trim(),
                                    Count = count
                                });
                            }
                            break;
                        case "buff":
                            if (int.TryParse(value, out int buffId))
                                reward.Buffs.Add(buffId);
                            break;
                        case "nexttask":
                            reward.NextTask = value;
                            break;
                    }
                }
            }
        }

        
        
        
        public TaskDefinition? GetTaskDefinition(int taskId)
        {
            return _taskDefinitions.TryGetValue(taskId, out var definition) ? definition : null;
        }

        
        
        
        public IEnumerable<TaskDefinition> GetAllTaskDefinitions()
        {
            return _taskDefinitions.Values;
        }

        
        
        
        public bool StartTask(uint playerId, int taskId)
        {
            if (!_taskDefinitions.ContainsKey(taskId))
                return false;

            var taskDef = _taskDefinitions[taskId];
            
            
            if (!string.IsNullOrEmpty(taskDef.PredecessorTask))
            {
                if (!HasCompletedTask(playerId, taskDef.PredecessorTask))
                    return false;
            }

            
            var player = GameWorld.Instance.GetPlayer(playerId);
            if (player == null || player.Level < taskDef.RequiredLevel)
                return false;

            
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask != null && playerTask.Attempts >= taskDef.MaxAttempts)
                return false;

            
            if (!taskDef.Repeatable && playerTask != null && playerTask.Status == TaskStatus.Completed)
                return false;

            
            if (playerTask == null)
            {
                playerTask = new PlayerTask
                {
                    TaskId = taskId,
                    Status = TaskStatus.InProgress,
                    StartTime = DateTime.Now,
                    Attempts = 1,
                    Objectives = new List<TaskObjective>()
                };

                
                foreach (var objective in taskDef.Objectives)
                {
                    playerTask.Objectives.Add(new TaskObjective
                    {
                        Id = objective.Id,
                        Description = objective.Description,
                        TargetType = objective.TargetType,
                        TargetName = objective.TargetName,
                        RequiredCount = objective.RequiredCount,
                        CurrentCount = 0
                    });
                }

                
                if (taskDef.TimeLimit > 0)
                    playerTask.ExpireTime = DateTime.Now.AddSeconds(taskDef.TimeLimit);

                if (!_playerTasks.ContainsKey(playerId))
                    _playerTasks[playerId] = new List<PlayerTask>();
                
                _playerTasks[playerId].Add(playerTask);
            }
            else
            {
                
                playerTask.Status = TaskStatus.InProgress;
                playerTask.StartTime = DateTime.Now;
                playerTask.CompleteTime = null;
                playerTask.Attempts++;
                playerTask.RewardClaimed = false;

                
                foreach (var objective in playerTask.Objectives)
                {
                    objective.CurrentCount = 0;
                }

                
                if (taskDef.TimeLimit > 0)
                    playerTask.ExpireTime = DateTime.Now.AddSeconds(taskDef.TimeLimit);
            }

            LogManager.Default.Info($"玩家 {playerId} 开始任务: {taskDef.Name} (ID: {taskId})");
            return true;
        }

        
        
        
        public bool CompleteTask(uint playerId, int taskId)
        {
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask == null || playerTask.Status != TaskStatus.InProgress)
                return false;

            
            foreach (var objective in playerTask.Objectives)
            {
                if (!objective.IsCompleted)
                    return false;
            }

            
            if (playerTask.ExpireTime.HasValue && DateTime.Now > playerTask.ExpireTime.Value)
            {
                playerTask.Status = TaskStatus.Failed;
                return false;
            }

            playerTask.Status = TaskStatus.Completed;
            playerTask.CompleteTime = DateTime.Now;

            LogManager.Default.Info($"玩家 {playerId} 完成任务: {taskId}");
            return true;
        }

        
        
        
        public bool AbandonTask(uint playerId, int taskId)
        {
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask == null || playerTask.Status != TaskStatus.InProgress)
                return false;

            playerTask.Status = TaskStatus.Abandoned;

            LogManager.Default.Info($"玩家 {playerId} 放弃任务: {taskId}");
            return true;
        }

        
        
        
        public bool UpdateTaskObjective(uint playerId, int taskId, int objectiveId, int progress)
        {
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask == null || playerTask.Status != TaskStatus.InProgress)
                return false;

            var objective = playerTask.Objectives.Find(o => o.Id == objectiveId);
            if (objective == null)
                return false;

            objective.CurrentCount = Math.Min(objective.CurrentCount + progress, objective.RequiredCount);
            return true;
        }

        
        
        
        public bool ClaimTaskReward(uint playerId, int taskId)
        {
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask == null || playerTask.Status != TaskStatus.Completed || playerTask.RewardClaimed)
                return false;

            var taskDef = GetTaskDefinition(taskId);
            if (taskDef == null)
                return false;

            
            
            LogManager.Default.Info($"玩家 {playerId} 领取任务奖励: {taskDef.Name} (经验: {taskDef.Reward.Exp}, 金币: {taskDef.Reward.Gold})");

            playerTask.RewardClaimed = true;
            return true;
        }

        
        
        
        public PlayerTask? GetPlayerTask(uint playerId, int taskId)
        {
            if (!_playerTasks.ContainsKey(playerId))
                return null;

            return _playerTasks[playerId].Find(t => t.TaskId == taskId);
        }

        
        
        
        public List<PlayerTask> GetPlayerTasks(uint playerId)
        {
            return _playerTasks.TryGetValue(playerId, out var tasks) ? tasks : new List<PlayerTask>();
        }

        
        
        
        public bool HasCompletedTask(uint playerId, string taskName)
        {
            if (!_playerTasks.ContainsKey(playerId))
                return false;

            foreach (var task in _playerTasks[playerId])
            {
                var taskDef = GetTaskDefinition(task.TaskId);
                if (taskDef != null && taskDef.Name == taskName && task.Status == TaskStatus.Completed)
                    return true;
            }

            return false;
        }

        
        
        
        public void Update()
        {
            var now = DateTime.Now;
            var expiredPlayers = new List<uint>();

            foreach (var kvp in _playerTasks)
            {
                var playerId = kvp.Key;
                var tasks = kvp.Value;
                var tasksToRemove = new List<PlayerTask>();

                foreach (var task in tasks)
                {
                    
                    if (task.ExpireTime.HasValue && now > task.ExpireTime.Value && task.Status == TaskStatus.InProgress)
                    {
                        task.Status = TaskStatus.Failed;
                        LogManager.Default.Debug($"任务过期: 玩家 {playerId}, 任务 {task.TaskId}");
                    }

                    
                    if (task.Status == TaskStatus.Completed && task.RewardClaimed)
                    {
                        var taskDef = GetTaskDefinition(task.TaskId);
                        if (taskDef != null && !taskDef.Repeatable)
                        {
                            tasksToRemove.Add(task);
                        }
                    }

                    
                    if (task.Status == TaskStatus.Failed || task.Status == TaskStatus.Abandoned)
                    {
                        tasksToRemove.Add(task);
                    }
                }

                
                foreach (var task in tasksToRemove)
                {
                    tasks.Remove(task);
                }

                if (tasks.Count == 0)
                    expiredPlayers.Add(playerId);
            }

            
            foreach (var playerId in expiredPlayers)
            {
                _playerTasks.Remove(playerId);
            }
        }

        
        
        
        public List<TaskDefinition> GetAvailableTasks(uint playerId)
        {
            var availableTasks = new List<TaskDefinition>();
            var player = GameWorld.Instance.GetPlayer(playerId);
            
            if (player == null)
                return availableTasks;

            foreach (var taskDef in _taskDefinitions.Values)
            {
                
                if (player.Level < taskDef.RequiredLevel)
                    continue;

                
                if (!string.IsNullOrEmpty(taskDef.PredecessorTask))
                {
                    if (!HasCompletedTask(playerId, taskDef.PredecessorTask))
                        continue;
                }

                
                var playerTask = GetPlayerTask(playerId, taskDef.Id);
                if (playerTask != null && playerTask.Attempts >= taskDef.MaxAttempts)
                    continue;

                
                if (!taskDef.Repeatable && playerTask != null && playerTask.Status == TaskStatus.Completed)
                    continue;

                availableTasks.Add(taskDef);
            }

            return availableTasks;
        }

        
        
        
        public List<PlayerTask> GetInProgressTasks(uint playerId)
        {
            var tasks = GetPlayerTasks(playerId);
            return tasks.FindAll(t => t.Status == TaskStatus.InProgress);
        }

        
        
        
        public List<PlayerTask> GetCompletedTasks(uint playerId)
        {
            var tasks = GetPlayerTasks(playerId);
            return tasks.FindAll(t => t.Status == TaskStatus.Completed);
        }

        
        
        
        public void Reset()
        {
            _taskDefinitions.Clear();
            _playerTasks.Clear();
            LogManager.Default.Info("任务管理器已重置");
        }
    }
}
