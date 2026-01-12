namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    
    
    
    public enum RelationshipType
    {
        MasterApprentice = 0,    
        Married = 1,             
        SwornBrother = 2,        
        Friend = 3               
    }

    
    
    
    public enum RelationshipStatus
    {
        Pending = 0,     
        Active = 1,      
        Broken = 2,      
        Expired = 3      
    }

    
    
    
    public class Relationship
    {
        public uint RelationshipId { get; set; }
        public RelationshipType Type { get; set; }
        public uint Player1Id { get; set; }
        public string Player1Name { get; set; } = string.Empty;
        public uint Player2Id { get; set; }
        public string Player2Name { get; set; } = string.Empty;
        public RelationshipStatus Status { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime? ConfirmTime { get; set; }
        public DateTime? BreakTime { get; set; }
        public uint Experience { get; set; }
        public uint Level { get; set; }
        public string CustomName { get; set; } = string.Empty; 

        public Relationship(uint relationshipId, RelationshipType type, uint player1Id, string player1Name, uint player2Id, string player2Name)
        {
            RelationshipId = relationshipId;
            Type = type;
            Player1Id = player1Id;
            Player1Name = player1Name;
            Player2Id = player2Id;
            Player2Name = player2Name;
            Status = RelationshipStatus.Pending;
            CreateTime = DateTime.Now;
            Level = 1;
            Experience = 0;
        }

        
        
        
        public void Confirm()
        {
            Status = RelationshipStatus.Active;
            ConfirmTime = DateTime.Now;
        }

        
        
        
        public void Break()
        {
            Status = RelationshipStatus.Broken;
            BreakTime = DateTime.Now;
        }

        
        
        
        public void AddExperience(uint amount)
        {
            Experience += amount;
            
            
            uint requiredExp = GetRequiredExperience();
            while (Experience >= requiredExp && Level < GetMaxLevel())
            {
                Experience -= requiredExp;
                Level++;
                requiredExp = GetRequiredExperience();
            }
        }

        
        
        
        private uint GetRequiredExperience()
        {
            return Level * 1000;
        }

        
        
        
        private uint GetMaxLevel()
        {
            return Type switch
            {
                RelationshipType.MasterApprentice => 10,
                RelationshipType.Married => 20,
                RelationshipType.SwornBrother => 15,
                RelationshipType.Friend => 5,
                _ => 5
            };
        }

        
        
        
        public RelationshipBonus GetBonus()
        {
            return new RelationshipBonus
            {
                ExpBonus = GetExpBonus(),
                DropBonus = GetDropBonus(),
                DamageBonus = GetDamageBonus(),
                DefenseBonus = GetDefenseBonus()
            };
        }

        
        
        
        private float GetExpBonus()
        {
            return Type switch
            {
                RelationshipType.MasterApprentice => 0.05f + Level * 0.01f, 
                RelationshipType.Married => 0.10f + Level * 0.005f,         
                RelationshipType.SwornBrother => 0.03f + Level * 0.008f,    
                RelationshipType.Friend => 0.01f + Level * 0.002f,          
                _ => 0
            };
        }

        
        
        
        private float GetDropBonus()
        {
            return Type switch
            {
                RelationshipType.Married => 0.05f + Level * 0.002f,         
                RelationshipType.SwornBrother => 0.03f + Level * 0.0015f,   
                _ => 0
            };
        }

        
        
        
        private float GetDamageBonus()
        {
            return Type switch
            {
                RelationshipType.Married => 0.03f + Level * 0.001f,         
                RelationshipType.SwornBrother => 0.05f + Level * 0.002f,    
                _ => 0
            };
        }

        
        
        
        private float GetDefenseBonus()
        {
            return Type switch
            {
                RelationshipType.Married => 0.03f + Level * 0.001f,         
                RelationshipType.SwornBrother => 0.04f + Level * 0.0015f,   
                _ => 0
            };
        }
    }

    
    
    
    public class RelationshipBonus
    {
        public float ExpBonus { get; set; }      
        public float DropBonus { get; set; }     
        public float DamageBonus { get; set; }   
        public float DefenseBonus { get; set; }  
    }

    
    
    
    public class MasterApprenticeInfo
    {
        public uint MasterId { get; set; }
        public string MasterName { get; set; } = string.Empty;
        public List<uint> ApprenticeIds { get; set; } = new();
        public uint MaxApprentices { get; set; } = 3;
        public DateTime LastRewardTime { get; set; }
        public uint TotalTaught { get; set; }    
    }

    
    
    
    public class MarriageInfo
    {
        public uint HusbandId { get; set; }
        public string HusbandName { get; set; } = string.Empty;
        public uint WifeId { get; set; }
        public string WifeName { get; set; } = string.Empty;
        public DateTime WeddingTime { get; set; }
        public uint WeddingMap { get; set; }
        public uint WeddingX { get; set; }
        public uint WeddingY { get; set; }
        public string Vows { get; set; } = string.Empty; 
    }

    
    
    
    public class RelationshipManager
    {
        private static RelationshipManager? _instance;
        public static RelationshipManager Instance => _instance ??= new RelationshipManager();

        private readonly Dictionary<uint, Relationship> _relationships = new();
        private readonly Dictionary<uint, List<uint>> _playerRelationships = new(); 
        private readonly Dictionary<uint, MasterApprenticeInfo> _masterInfo = new();
        private readonly Dictionary<uint, MarriageInfo> _marriageInfo = new();
        private readonly object _lock = new();
        
        private uint _nextRelationshipId = 10000;

        private RelationshipManager() { }

        
        
        
        public Relationship? RequestRelationship(RelationshipType type, uint requesterId, string requesterName, uint targetId, string targetName)
        {
            if (requesterId == targetId)
                return null;

            
            if (HasRelationship(type, requesterId, targetId))
                return null;

            
            if (!CanHaveRelationship(type, requesterId, targetId))
                return null;

            lock (_lock)
            {
                uint relationshipId = _nextRelationshipId++;
                var relationship = new Relationship(relationshipId, type, requesterId, requesterName, targetId, targetName);
                
                _relationships[relationshipId] = relationship;
                
                
                AddToPlayerRelationships(requesterId, relationshipId);
                AddToPlayerRelationships(targetId, relationshipId);
                
                LogManager.Default.Info($"{requesterName} 向 {targetName} 发起{GetTypeName(type)}关系请求");
                return relationship;
            }
        }

        
        
        
        public bool ConfirmRelationship(uint relationshipId, uint confirmerId)
        {
            lock (_lock)
            {
                if (!_relationships.TryGetValue(relationshipId, out var relationship))
                    return false;

                
                if (relationship.Player2Id != confirmerId)
                    return false;

                
                if (relationship.Status != RelationshipStatus.Pending)
                    return false;

                
                if (!CanHaveRelationship(relationship.Type, relationship.Player1Id, relationship.Player2Id))
                    return false;

                relationship.Confirm();
                
                
                if (relationship.Type == RelationshipType.MasterApprentice)
                {
                    UpdateMasterApprenticeInfo(relationship);
                }
                
                else if (relationship.Type == RelationshipType.Married)
                {
                    UpdateMarriageInfo(relationship);
                }
                
                LogManager.Default.Info($"{relationship.Player2Name} 确认了与 {relationship.Player1Name} 的{GetTypeName(relationship.Type)}关系");
                return true;
            }
        }

        
        
        
        public bool RejectRelationship(uint relationshipId, uint rejecterId)
        {
            lock (_lock)
            {
                if (!_relationships.TryGetValue(relationshipId, out var relationship))
                    return false;

                
                if (relationship.Player2Id != rejecterId)
                    return false;

                
                if (relationship.Status != RelationshipStatus.Pending)
                    return false;

                relationship.Break();
                
                LogManager.Default.Info($"{relationship.Player2Name} 拒绝了与 {relationship.Player1Name} 的{GetTypeName(relationship.Type)}关系请求");
                return true;
            }
        }

        
        
        
        public bool BreakRelationship(uint relationshipId, uint breakerId)
        {
            lock (_lock)
            {
                if (!_relationships.TryGetValue(relationshipId, out var relationship))
                    return false;

                
                if (relationship.Player1Id != breakerId && relationship.Player2Id != breakerId)
                    return false;

                
                if (relationship.Status != RelationshipStatus.Active)
                    return false;

                relationship.Break();
                
                
                if (relationship.Type == RelationshipType.MasterApprentice)
                {
                    RemoveFromMasterApprenticeInfo(relationship);
                }
                
                else if (relationship.Type == RelationshipType.Married)
                {
                    RemoveMarriageInfo(relationship);
                }
                
                LogManager.Default.Info($"{GetPlayerName(breakerId)} 解除了与 {GetOtherPlayerName(relationship, breakerId)} 的{GetTypeName(relationship.Type)}关系");
                return true;
            }
        }

        
        
        
        public List<Relationship> GetPlayerRelationships(uint playerId, RelationshipType? type = null, RelationshipStatus? status = null)
        {
            lock (_lock)
            {
                if (!_playerRelationships.TryGetValue(playerId, out var relationshipIds))
                    return new List<Relationship>();

                var relationships = relationshipIds
                    .Select(id => _relationships.TryGetValue(id, out var rel) ? rel : null)
                    .Where(rel => rel != null)
                    .Cast<Relationship>()
                    .ToList();

                
                if (type.HasValue)
                {
                    relationships = relationships.Where(r => r.Type == type.Value).ToList();
                }

                
                if (status.HasValue)
                {
                    relationships = relationships.Where(r => r.Status == status.Value).ToList();
                }

                return relationships;
            }
        }

        
        
        
        public Relationship? GetActiveRelationship(uint playerId, RelationshipType type)
        {
            var relationships = GetPlayerRelationships(playerId, type, RelationshipStatus.Active);
            return relationships.FirstOrDefault();
        }

        
        
        
        public bool HasRelationship(RelationshipType type, uint player1Id, uint player2Id)
        {
            var relationships = GetPlayerRelationships(player1Id, type, RelationshipStatus.Active);
            return relationships.Any(r => (r.Player1Id == player2Id || r.Player2Id == player2Id));
        }

        
        
        
        public void AddRelationshipExperience(uint relationshipId, uint amount)
        {
            lock (_lock)
            {
                if (_relationships.TryGetValue(relationshipId, out var relationship))
                {
                    relationship.AddExperience(amount);
                }
            }
        }

        
        
        
        public RelationshipBonus GetRelationshipBonus(uint playerId)
        {
            var bonus = new RelationshipBonus();
            var activeRelationships = GetPlayerRelationships(playerId, status: RelationshipStatus.Active);
            
            foreach (var relationship in activeRelationships)
            {
                var relBonus = relationship.GetBonus();
                bonus.ExpBonus += relBonus.ExpBonus;
                bonus.DropBonus += relBonus.DropBonus;
                bonus.DamageBonus += relBonus.DamageBonus;
                bonus.DefenseBonus += relBonus.DefenseBonus;
            }
            
            return bonus;
        }

        
        
        
        public MasterApprenticeInfo? GetMasterApprenticeInfo(uint masterId)
        {
            lock (_lock)
            {
                _masterInfo.TryGetValue(masterId, out var info);
                return info;
            }
        }

        
        
        
        public MarriageInfo? GetMarriageInfo(uint playerId)
        {
            lock (_lock)
            {
                
                return _marriageInfo.Values.FirstOrDefault(m => m.HusbandId == playerId || m.WifeId == playerId);
            }
        }

        
        
        
        private bool CanHaveRelationship(RelationshipType type, uint player1Id, uint player2Id)
        {
            
            if (HasRelationship(type, player1Id, player2Id))
                return false;

            
            switch (type)
            {
                case RelationshipType.MasterApprentice:
                    
                    var masterInfo = GetMasterApprenticeInfo(player1Id);
                    if (masterInfo != null && masterInfo.ApprenticeIds.Count >= masterInfo.MaxApprentices)
                        return false;
                    
                    
                    var apprenticeRelationships = GetPlayerRelationships(player2Id, RelationshipType.MasterApprentice, RelationshipStatus.Active);
                    if (apprenticeRelationships.Count > 0)
                        return false;
                    break;
                    
                case RelationshipType.Married:
                    
                    var marriage1 = GetActiveRelationship(player1Id, RelationshipType.Married);
                    var marriage2 = GetActiveRelationship(player2Id, RelationshipType.Married);
                    if (marriage1 != null || marriage2 != null)
                        return false;
                    
                    
                    var player1 = HumanPlayerMgr.Instance.FindById(player1Id);
                    var player2 = HumanPlayerMgr.Instance.FindById(player2Id);
                    
                    if (player1 == null || player2 == null)
                        return false; 
                    
                    if (player1.Sex == player2.Sex)
                        return false; 
                    
                    break;
                    
                case RelationshipType.SwornBrother:
                    
                    var brotherRelationships = GetPlayerRelationships(player1Id, RelationshipType.SwornBrother, RelationshipStatus.Active);
                    if (brotherRelationships.Count >= 5) 
                        return false;
                    break;
                    
                case RelationshipType.Friend:
                    
                    var friendRelationships = GetPlayerRelationships(player1Id, RelationshipType.Friend, RelationshipStatus.Active);
                    if (friendRelationships.Count >= 100) 
                        return false;
                    break;
            }
            
            return true;
        }

        
        
        
        private void UpdateMasterApprenticeInfo(Relationship relationship)
        {
            if (relationship.Type != RelationshipType.MasterApprentice)
                return;

            lock (_lock)
            {
                if (!_masterInfo.TryGetValue(relationship.Player1Id, out var masterInfo))
                {
                    masterInfo = new MasterApprenticeInfo
                    {
                        MasterId = relationship.Player1Id,
                        MasterName = relationship.Player1Name
                    };
                    _masterInfo[relationship.Player1Id] = masterInfo;
                }

                
                if (!masterInfo.ApprenticeIds.Contains(relationship.Player2Id))
                {
                    masterInfo.ApprenticeIds.Add(relationship.Player2Id);
                }
            }
        }

        
        
        
        private void RemoveFromMasterApprenticeInfo(Relationship relationship)
        {
            if (relationship.Type != RelationshipType.MasterApprentice)
                return;

            lock (_lock)
            {
                if (_masterInfo.TryGetValue(relationship.Player1Id, out var masterInfo))
                {
                    masterInfo.ApprenticeIds.Remove(relationship.Player2Id);
                    
                    
                    if (masterInfo.ApprenticeIds.Count == 0)
                    {
                        _masterInfo.Remove(relationship.Player1Id);
                    }
                }
            }
        }

        
        
        
        private void UpdateMarriageInfo(Relationship relationship)
        {
            if (relationship.Type != RelationshipType.Married)
                return;

            lock (_lock)
            {
                
                var marriageInfo = new MarriageInfo
                {
                    HusbandId = relationship.Player1Id,
                    HusbandName = relationship.Player1Name,
                    WifeId = relationship.Player2Id,
                    WifeName = relationship.Player2Name,
                    WeddingTime = relationship.ConfirmTime ?? DateTime.Now,
                    Vows = "执子之手，与子偕老"
                };
                
                _marriageInfo[relationship.Player1Id] = marriageInfo;
            }
        }

        
        
        
        private void RemoveMarriageInfo(Relationship relationship)
        {
            if (relationship.Type != RelationshipType.Married)
                return;

            lock (_lock)
            {
                _marriageInfo.Remove(relationship.Player1Id);
            }
        }

        
        
        
        private void AddToPlayerRelationships(uint playerId, uint relationshipId)
        {
            if (!_playerRelationships.TryGetValue(playerId, out var relationships))
            {
                relationships = new List<uint>();
                _playerRelationships[playerId] = relationships;
            }
            
            if (!relationships.Contains(relationshipId))
            {
                relationships.Add(relationshipId);
            }
        }

        
        
        
        private string GetTypeName(RelationshipType type)
        {
            return type switch
            {
                RelationshipType.MasterApprentice => "师徒",
                RelationshipType.Married => "夫妻",
                RelationshipType.SwornBrother => "结拜兄弟",
                RelationshipType.Friend => "好友",
                _ => "未知"
            };
        }

        
        
        
        private string GetPlayerName(uint playerId)
        {
            
            var player = HumanPlayerMgr.Instance.FindById(playerId);
            if (player != null)
                return player.Name;
            
            
            lock (_lock)
            {
                
                foreach (var relationship in _relationships.Values)
                {
                    if (relationship.Player1Id == playerId)
                        return relationship.Player1Name;
                    if (relationship.Player2Id == playerId)
                        return relationship.Player2Name;
                }
            }
            
            
            return $"玩家{playerId}";
        }

        
        
        
        private string GetOtherPlayerName(Relationship relationship, uint playerId)
        {
            if (relationship.Player1Id == playerId)
                return relationship.Player2Name;
            else
                return relationship.Player1Name;
        }

        
        
        
        public (int totalRelationships, int activeRelationships, int pendingRelationships) GetStatistics()
        {
            lock (_lock)
            {
                int total = _relationships.Count;
                int active = _relationships.Values.Count(r => r.Status == RelationshipStatus.Active);
                int pending = _relationships.Values.Count(r => r.Status == RelationshipStatus.Pending);
                
                return (total, active, pending);
            }
        }

        
        
        
        public void CleanupExpiredRelationships()
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                var expiredRelationships = _relationships.Values
                    .Where(r => r.Status == RelationshipStatus.Pending && 
                               (now - r.CreateTime).TotalDays > 7) 
                    .ToList();
                
                foreach (var relationship in expiredRelationships)
                {
                    relationship.Status = RelationshipStatus.Expired;
                    LogManager.Default.Info($"关系 {relationship.RelationshipId} 已过期");
                }
            }
        }
    }
}
