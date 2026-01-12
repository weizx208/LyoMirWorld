using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MirCommon;
using MirCommon.Utils;


using Player = GameServer.HumanPlayer;

namespace GameServer
{
    
    
    
    public enum QuestType
    {
        Main = 0,           
        Side = 1,           
        Daily = 2,          
        Weekly = 3,         
        Repeatable = 4,     
        Achievement = 5     
    }

    
    
    
    public enum QuestObjectiveType
    {
        KillMonster = 0,    
        CollectItem = 1,    
        TalkToNPC = 2,      
        ReachLevel = 3,     
        ReachLocation = 4,  
        UseItem = 5,        
        LearnSkill = 6,     
        EquipItem = 7,      
        KillPlayer = 8,     
        CompleteQuest = 9   
    }

    
    
    
    public enum QuestStatus
    {
        NotStarted = 0,     
        InProgress = 1,     
        Completed = 2,      
        Failed = 3,         
        Abandoned = 4       
    }

    
    
    
    public class QuestObjective
    {
        public int ObjectiveId { get; set; }
        public QuestObjectiveType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public int TargetId { get; set; }       
        public int RequiredCount { get; set; }  
        public int MapId { get; set; }          
        public bool Optional { get; set; }      

        public QuestObjective(int id, QuestObjectiveType type, string description, int targetId, int count)
        {
            ObjectiveId = id;
            Type = type;
            Description = description;
            TargetId = targetId;
            RequiredCount = count;
        }
    }

    
    
    
    public class QuestReward
    {
        public uint Exp { get; set; }           
        public uint Gold { get; set; }          
        public List<QuestItemReward> Items { get; set; } = new();

        public class QuestItemReward
        {
            public int ItemId { get; set; }
            public int Count { get; set; }
            public bool Optional { get; set; }  

            public QuestItemReward(int itemId, int count, bool optional = false)
            {
                ItemId = itemId;
                Count = count;
                Optional = optional;
            }
        }
    }

    
    
    
    public class QuestDefinition
    {
        public int QuestId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public QuestType Type { get; set; }
        
        
        public int RequireLevel { get; set; }
        public int RequireJob { get; set; } = -1;   
        public List<int> RequireQuests { get; set; } = new();  
        
        
        public int StartNPCId { get; set; }
        public int EndNPCId { get; set; }
        
        
        public List<QuestObjective> Objectives { get; set; } = new();
        
        
        public QuestReward Reward { get; set; } = new();
        
        
        public int TimeLimit { get; set; }      
        
        
        public bool AutoComplete { get; set; }  
        public bool Repeatable { get; set; }    
        public int RepeatInterval { get; set; } 

        public QuestDefinition(int questId, string name, QuestType type)
        {
            QuestId = questId;
            Name = name;
            Type = type;
        }

        public bool CanAccept(Player player)
        {
            
            if (player.Level < RequireLevel)
                return false;

            
            if (RequireJob != -1 && player.Job != RequireJob)
                return false;

            
            foreach (var questId in RequireQuests)
            {
                if (!player.QuestManager.HasCompletedQuest(questId))
                    return false;
            }

            return true;
        }
    }

    
    
    
    public class QuestProgress
    {
        public int QuestId { get; set; }
        public QuestDefinition Definition { get; set; }
        public QuestStatus Status { get; set; }
        public DateTime AcceptTime { get; set; }
        public DateTime? CompleteTime { get; set; }
        
        
        public Dictionary<int, int> ObjectiveProgress { get; set; } = new();

        public QuestProgress(QuestDefinition definition)
        {
            Definition = definition;
            QuestId = definition.QuestId;
            Status = QuestStatus.InProgress;
            AcceptTime = DateTime.Now;

            
            foreach (var objective in definition.Objectives)
            {
                ObjectiveProgress[objective.ObjectiveId] = 0;
            }
        }

        public void UpdateProgress(int objectiveId, int count)
        {
            if (ObjectiveProgress.ContainsKey(objectiveId))
            {
                ObjectiveProgress[objectiveId] += count;
                
                
                var objective = Definition.Objectives.FirstOrDefault(o => o.ObjectiveId == objectiveId);
                if (objective != null)
                {
                    ObjectiveProgress[objectiveId] = Math.Min(
                        ObjectiveProgress[objectiveId],
                        objective.RequiredCount
                    );
                }
            }
        }

        public bool IsObjectiveComplete(int objectiveId)
        {
            if (!ObjectiveProgress.TryGetValue(objectiveId, out var progress))
                return false;

            var objective = Definition.Objectives.FirstOrDefault(o => o.ObjectiveId == objectiveId);
            return objective != null && progress >= objective.RequiredCount;
        }

        public bool IsQuestComplete()
        {
            foreach (var objective in Definition.Objectives)
            {
                if (objective.Optional)
                    continue;

                if (!IsObjectiveComplete(objective.ObjectiveId))
                    return false;
            }
            return true;
        }

        public float GetCompletionRate()
        {
            if (Definition.Objectives.Count == 0)
                return 0f;

            int total = 0;
            int completed = 0;

            foreach (var objective in Definition.Objectives)
            {
                if (objective.Optional)
                    continue;

                total++;
                if (IsObjectiveComplete(objective.ObjectiveId))
                    completed++;
            }

            return total > 0 ? (float)completed / total : 0f;
        }

        public bool IsExpired()
        {
            if (Definition.TimeLimit <= 0)
                return false;

            return (DateTime.Now - AcceptTime).TotalSeconds > Definition.TimeLimit;
        }
    }

    
    
    
    public class PlayerQuestManager
    {
        private readonly Player _player;
        private readonly Dictionary<int, QuestProgress> _activeQuests = new();
        private readonly HashSet<int> _completedQuests = new();
        private readonly Dictionary<int, DateTime> _questCooldowns = new();
        private readonly object _lock = new();
        
        public int MaxActiveQuests { get; set; } = 20;

        public PlayerQuestManager(Player player)
        {
            _player = player;
        }

        public bool AcceptQuest(int questId)
        {
            lock (_lock)
            {
                var definition = QuestDefinitionManager.Instance.GetDefinition(questId);
                if (definition == null)
                    return false;

                
                if (_activeQuests.ContainsKey(questId))
                    return false;

                
                if (_activeQuests.Count >= MaxActiveQuests)
                    return false;

                
                if (!definition.CanAccept(_player))
                    return false;

                
                if (_questCooldowns.TryGetValue(questId, out var cooldownEnd))
                {
                    if (DateTime.Now < cooldownEnd)
                        return false;
                }

                
                var progress = new QuestProgress(definition);
                _activeQuests[questId] = progress;

                LogManager.Default.Info($"{_player.Name} 接取任务: {definition.Name}");
                return true;
            }
        }

        public bool AbandonQuest(int questId)
        {
            lock (_lock)
            {
                if (_activeQuests.TryGetValue(questId, out var progress))
                {
                    progress.Status = QuestStatus.Abandoned;
                    _activeQuests.Remove(questId);
                    
                    LogManager.Default.Info($"{_player.Name} 放弃任务: {progress.Definition.Name}");
                    return true;
                }
                return false;
            }
        }

        public bool CompleteQuest(int questId)
        {
            lock (_lock)
            {
                if (!_activeQuests.TryGetValue(questId, out var progress))
                    return false;

                if (!progress.IsQuestComplete())
                    return false;

                
                GiveRewards(progress.Definition);

                
                progress.Status = QuestStatus.Completed;
                progress.CompleteTime = DateTime.Now;
                _completedQuests.Add(questId);
                _activeQuests.Remove(questId);

                
                if (progress.Definition.Repeatable && progress.Definition.RepeatInterval > 0)
                {
                    _questCooldowns[questId] = DateTime.Now.AddSeconds(progress.Definition.RepeatInterval);
                }

                LogManager.Default.Info($"{_player.Name} 完成任务: {progress.Definition.Name}");
                return true;
            }
        }

        private void GiveRewards(QuestDefinition definition)
        {
            var reward = definition.Reward;

            
            if (reward.Exp > 0)
            {
                _player.AddExp(reward.Exp);
                LogManager.Default.Debug($"{_player.Name} 获得经验: {reward.Exp}");
            }

            
            if (reward.Gold > 0)
            {
                _player.Gold += reward.Gold;
                LogManager.Default.Debug($"{_player.Name} 获得金币: {reward.Gold}");
            }

            
            if (reward.Items.Count > 0)
            {
                var itemManager = ItemManager.Instance;
                var optionalItems = new List<QuestReward.QuestItemReward>();
                
                foreach (var itemReward in reward.Items)
                {
                    if (itemReward.Optional)
                    {
                        
                        optionalItems.Add(itemReward);
                        continue;
                    }

                    
                    GiveItemReward(itemReward);
                }

                
                if (optionalItems.Count > 0)
                {
                    
                    
                    var random = new Random();
                    var selectedReward = optionalItems[random.Next(optionalItems.Count)];
                    GiveItemReward(selectedReward);
                    
                    LogManager.Default.Debug($"{_player.Name} 获得可选奖励: {selectedReward.ItemId} x{selectedReward.Count}");
                }
            }
        }

        
        
        
        private void GiveItemReward(QuestReward.QuestItemReward itemReward)
        {
            var itemManager = ItemManager.Instance;
            
            
            var itemDef = itemManager.GetDefinition(itemReward.ItemId);
            if (itemDef == null)
            {
                LogManager.Default.Warning($"任务奖励物品不存在: {itemReward.ItemId}");
                return;
            }

            
            if (!HasSpaceForItem(itemDef, itemReward.Count))
            {
                
                SendRewardByMail(itemReward);
                return;
            }

            
            for (int i = 0; i < itemReward.Count; i++)
            {
                var item = itemManager.CreateItem(itemReward.ItemId);
                if (item != null)
                {
                    if (!_player.Inventory.AddItem(item))
                    {
                        
                        SendRewardByMail(itemReward);
                        break;
                    }
                }
            }

            LogManager.Default.Debug($"{_player.Name} 获得物品奖励: {itemDef.Name} x{itemReward.Count}");
        }

        
        
        
        private bool HasSpaceForItem(ItemDefinition itemDef, int count)
        {
            
            if (itemDef.MaxStack > 1)
            {
                int currentCount = _player.Inventory.GetItemCount(itemDef.ItemId);
                int maxStackable = itemDef.MaxStack;
                
                
                int existingSpace = 0;
                var allItems = _player.Inventory.GetAllItems();
                foreach (var kvp in allItems)
                {
                    var item = kvp.Value;
                    if (item.ItemId == itemDef.ItemId)
                    {
                        existingSpace += itemDef.MaxStack - item.Count;
                    }
                }
                
                if (existingSpace >= count)
                    return true;
                
                
                int neededSlots = (int)Math.Ceiling((double)(count - existingSpace) / itemDef.MaxStack);
                int freeSlots = _player.Inventory.MaxSlots - allItems.Count;
                return freeSlots >= neededSlots;
            }
            else
            {
                
                int freeSlots = _player.Inventory.MaxSlots - _player.Inventory.GetUsedSlots();
                return freeSlots >= count;
            }
        }

        
        
        
        private void SendRewardByMail(QuestReward.QuestItemReward itemReward)
        {
            
            
            LogManager.Default.Warning($"{_player.Name} 背包空间不足，无法获得任务奖励物品: {itemReward.ItemId} x{itemReward.Count}");
            
            
            
        }

        public void UpdateProgress(QuestObjectiveType type, int targetId, int count = 1)
        {
            lock (_lock)
            {
                foreach (var progress in _activeQuests.Values)
                {
                    if (progress.Status != QuestStatus.InProgress)
                        continue;

                    foreach (var objective in progress.Definition.Objectives)
                    {
                        if (objective.Type == type && objective.TargetId == targetId)
                        {
                            progress.UpdateProgress(objective.ObjectiveId, count);
                            
                            if (progress.IsObjectiveComplete(objective.ObjectiveId))
                            {
                                LogManager.Default.Debug(
                                    $"{_player.Name} 完成任务目标: {objective.Description}"
                                );
                            }

                            
                            if (progress.IsQuestComplete() && progress.Definition.AutoComplete)
                            {
                                CompleteQuest(progress.QuestId);
                            }
                        }
                    }
                }
            }
        }

        
        
        
        public void OnItemPickup(int itemId, int count = 1)
        {
            UpdateProgress(QuestObjectiveType.CollectItem, itemId, count);
        }

        
        
        
        public void OnMonsterKill(int monsterId)
        {
            UpdateProgress(QuestObjectiveType.KillMonster, monsterId, 1);
        }

        
        
        
        public void OnAttackMonster(AliveObject target, int damage)
        {
            if (target is Monster monster)
            {
                
                
            }
        }

        
        
        
        public void OnItemEquip(int itemId)
        {
            UpdateProgress(QuestObjectiveType.EquipItem, itemId, 1);
        }

        
        
        
        public void OnSkillLearn(int skillId)
        {
            UpdateProgress(QuestObjectiveType.LearnSkill, skillId, 1);
        }

        
        
        
        public void OnNPCTalk(int npcId)
        {
            UpdateProgress(QuestObjectiveType.TalkToNPC, npcId, 1);
        }

        
        
        
        public void OnLevelUp(int level)
        {
            UpdateProgress(QuestObjectiveType.ReachLevel, level, 1);
        }

        public void Update()
        {
            lock (_lock)
            {
                var expiredQuests = new List<int>();

                
                foreach (var kvp in _activeQuests)
                {
                    if (kvp.Value.IsExpired())
                    {
                        kvp.Value.Status = QuestStatus.Failed;
                        expiredQuests.Add(kvp.Key);
                    }
                }

                
                foreach (var questId in expiredQuests)
                {
                    _activeQuests.Remove(questId);
                    LogManager.Default.Info($"{_player.Name} 任务失败(超时): {questId}");
                }
            }
        }

        public QuestProgress? GetQuest(int questId)
        {
            lock (_lock)
            {
                _activeQuests.TryGetValue(questId, out var progress);
                return progress;
            }
        }

        public bool HasActiveQuest(int questId)
        {
            lock (_lock)
            {
                return _activeQuests.ContainsKey(questId);
            }
        }

        public bool HasCompletedQuest(int questId)
        {
            lock (_lock)
            {
                return _completedQuests.Contains(questId);
            }
        }

        public List<QuestProgress> GetActiveQuests()
        {
            lock (_lock)
            {
                return _activeQuests.Values.ToList();
            }
        }

        public List<int> GetCompletedQuests()
        {
            lock (_lock)
            {
                return _completedQuests.ToList();
            }
        }

        public List<QuestDefinition> GetAvailableQuests()
        {
            return QuestDefinitionManager.Instance.GetAllDefinitions()
                .Where(q => q.CanAccept(_player) && 
                           !HasActiveQuest(q.QuestId) &&
                           (!_completedQuests.Contains(q.QuestId) || q.Repeatable))
                .ToList();
        }

        
        
        
        public void UpdateTask(int taskId, int state, int param1 = 0, int param2 = 0, int param3 = 0)
        {
            lock (_lock)
            {
                LogManager.Default.Info($"更新任务: 任务ID={taskId}, 状态={state}, 参数1={param1}, 参数2={param2}, 参数3={param3}");
                
                
                if (!_activeQuests.TryGetValue(taskId, out var progress))
                {
                    LogManager.Default.Warning($"任务不存在: {taskId}");
                    return;
                }

                
                if (state >= 0)
                {
                    progress.Status = (QuestStatus)state;
                    LogManager.Default.Debug($"更新任务状态: {progress.Status}");
                }

                
                
                if (param1 > 0 || param2 > 0 || param3 > 0)
                {
                    
                    UpdateTaskProgress(progress, param1, param2, param3);
                }

                
                if (progress.IsQuestComplete())
                {
                    LogManager.Default.Info($"任务完成: {progress.Definition.Name}");
                    CompleteQuest(taskId);
                }
                else if (progress.IsExpired())
                {
                    LogManager.Default.Info($"任务过期: {progress.Definition.Name}");
                    progress.Status = QuestStatus.Failed;
                    _activeQuests.Remove(taskId);
                }
            }
        }

        
        
        
        public bool DeleteTask(int taskId)
        {
            lock (_lock)
            {
                LogManager.Default.Info($"删除任务: 任务ID={taskId}");
                
                
                if (!_activeQuests.TryGetValue(taskId, out var progress))
                {
                    LogManager.Default.Warning($"任务不存在: {taskId}");
                    return false;
                }

                
                _activeQuests.Remove(taskId);
                
                
                _completedQuests.Add(taskId);
                
                LogManager.Default.Info($"已删除任务: {progress.Definition.Name}");
                return true;
            }
        }

        
        
        
        private void UpdateTaskProgress(QuestProgress progress, int param1, int param2, int param3)
        {
            
            
            foreach (var objective in progress.Definition.Objectives)
            {
                
                if (param1 > 0 && objective.ObjectiveId == 1)
                {
                    progress.UpdateProgress(objective.ObjectiveId, param1);
                    LogManager.Default.Debug($"更新目标1进度: +{param1}");
                }
                
                
                if (param2 > 0 && objective.ObjectiveId == 2)
                {
                    progress.UpdateProgress(objective.ObjectiveId, param2);
                    LogManager.Default.Debug($"更新目标2进度: +{param2}");
                }
                
                
                if (param3 > 0 && objective.ObjectiveId == 3)
                {
                    progress.UpdateProgress(objective.ObjectiveId, param3);
                    LogManager.Default.Debug($"更新目标3进度: +{param3}");
                }
            }
        }

        
        
        
        
    }

    
    
    
    public class QuestDefinitionManager
    {
        private static QuestDefinitionManager? _instance;
        public static QuestDefinitionManager Instance => _instance ??= new QuestDefinitionManager();

        private readonly ConcurrentDictionary<int, QuestDefinition> _definitions = new();

        private QuestDefinitionManager()
        {
            InitializeDefaultQuests();
        }

        private void InitializeDefaultQuests()
        {
            
            var quest1 = new QuestDefinition(1001, "初入江湖", QuestType.Main)
            {
                Description = "与村长对话，了解这个世界",
                RequireLevel = 1,
                StartNPCId = 1001,
                EndNPCId = 1001
            };
            quest1.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.TalkToNPC, "与村长对话", 1001, 1
            ));
            quest1.Reward.Exp = 100;
            quest1.Reward.Gold = 50;
            AddDefinition(quest1);

            
            var quest2 = new QuestDefinition(1002, "清理害虫", QuestType.Main)
            {
                Description = "击杀5只骷髅",
                RequireLevel = 1,
                StartNPCId = 1001,
                EndNPCId = 1001
            };
            quest2.RequireQuests.Add(1001);
            quest2.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.KillMonster, "击杀骷髅", 1, 5
            ));
            quest2.Reward.Exp = 200;
            quest2.Reward.Gold = 100;
            quest2.Reward.Items.Add(new QuestReward.QuestItemReward(3001, 10)); 
            AddDefinition(quest2);

            
            var quest3 = new QuestDefinition(1003, "装备自己", QuestType.Main)
            {
                Description = "装备一把武器",
                RequireLevel = 1,
                StartNPCId = 1001,
                EndNPCId = 1001,
                AutoComplete = true
            };
            quest3.RequireQuests.Add(1002);
            quest3.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.EquipItem, "装备武器", 1001, 1
            ));
            quest3.Reward.Exp = 150;
            quest3.Reward.Gold = 200;
            AddDefinition(quest3);

            
            var quest4 = new QuestDefinition(1004, "学习技能", QuestType.Main)
            {
                Description = "学习一个技能",
                RequireLevel = 1,
                StartNPCId = 1007,
                EndNPCId = 1007,
                AutoComplete = true
            };
            quest4.RequireQuests.Add(1003);
            quest4.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.LearnSkill, "学习技能", 0, 1
            ));
            quest4.Reward.Exp = 300;
            quest4.Reward.Gold = 500;
            AddDefinition(quest4);

            
            var dailyQuest = new QuestDefinition(2001, "每日清理", QuestType.Daily)
            {
                Description = "每日击杀10只怪物",
                RequireLevel = 5,
                StartNPCId = 1001,
                EndNPCId = 1001,
                Repeatable = true,
                RepeatInterval = 86400 
            };
            dailyQuest.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.KillMonster, "击杀任意怪物", 0, 10
            ));
            dailyQuest.Reward.Exp = 500;
            dailyQuest.Reward.Gold = 1000;
            AddDefinition(dailyQuest);

            
            var collectQuest = new QuestDefinition(3001, "收集材料", QuestType.Side)
            {
                Description = "收集10个铁矿石",
                RequireLevel = 5,
                StartNPCId = 1006,
                EndNPCId = 1006
            };
            collectQuest.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.CollectItem, "收集铁矿石", 4001, 10
            ));
            collectQuest.Reward.Exp = 300;
            collectQuest.Reward.Gold = 500;
            AddDefinition(collectQuest);

            
            var levelQuest = new QuestDefinition(4001, "成长之路", QuestType.Achievement)
            {
                Description = "达到10级",
                RequireLevel = 1,
                StartNPCId = 0,
                EndNPCId = 0,
                AutoComplete = true
            };
            levelQuest.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.ReachLevel, "达到10级", 10, 1
            ));
            levelQuest.Reward.Exp = 1000;
            levelQuest.Reward.Gold = 2000;
            AddDefinition(levelQuest);

            LogManager.Default.Info($"已加载 {_definitions.Count} 个任务定义");
        }

        public void AddDefinition(QuestDefinition definition)
        {
            _definitions[definition.QuestId] = definition;
        }

        public QuestDefinition? GetDefinition(int questId)
        {
            _definitions.TryGetValue(questId, out var definition);
            return definition;
        }

        public List<QuestDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }

        public List<QuestDefinition> GetQuestsByType(QuestType type)
        {
            return _definitions.Values
                .Where(q => q.Type == type)
                .ToList();
        }

        public List<QuestDefinition> GetQuestsByNPC(int npcId)
        {
            return _definitions.Values
                .Where(q => q.StartNPCId == npcId || q.EndNPCId == npcId)
                .ToList();
        }
    }
}
