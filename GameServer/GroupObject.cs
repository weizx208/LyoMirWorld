using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Network;

namespace GameServer
{
    
    
    
    public class GroupObject
    {
        private static uint _nextGroupId = 1;
        private readonly List<HumanPlayer> _members = new();
        private readonly object _lock = new();

        public uint GroupId { get; private set; }
        public HumanPlayer Leader => _members.Count > 0 ? _members[0] : null;

        public GroupObject()
        {
            GroupId = _nextGroupId++;
        }

        
        
        
        public bool Create(HumanPlayer leader, HumanPlayer firstMember)
        {
            lock (_lock)
            {
                if (leader.GroupId != 0 || firstMember.GroupId != 0)
                    return false;

                _members.Add(leader);
                leader.GroupId = GroupId;
                
                _members.Add(firstMember);
                firstMember.GroupId = GroupId;
                
                UpdateNameList();
                SaySystemAttrib(0xfcff00, $"-{leader.Name}加入编组");
                SaySystemAttrib(0xfcff00, $"-{firstMember.Name}加入编组");
                
                return true;
            }
        }

        
        
        
        public bool AddMember(HumanPlayer member)
        {
            lock (_lock)
            {
                if (member.GroupId != 0)
                    return false;

                if (_members.Count >= GetMaxGroupMemberCount())
                {
                    Leader?.SaySystem("编组已满，无法添加新成员！");
                    return false;
                }

                _members.Add(member);
                member.GroupId = GroupId;
                UpdateNameList();
                SaySystemAttrib(0xfcff00, $"-{member.Name}加入编组");
                return true;
            }
        }

        
        
        
        public bool IsMember(HumanPlayer player)
        {
            lock (_lock)
            {
                return _members.Contains(player);
            }
        }

        
        
        
        public bool IsLeader(HumanPlayer player)
        {
            lock (_lock)
            {
                return _members.Count > 0 && _members[0] == player;
            }
        }

        
        
        
        public void DelMember(HumanPlayer member)
        {
            lock (_lock)
            {
                if (member == Leader)
                {
                    DestroyGroup();
                    return;
                }

                if (_members.Remove(member))
                {
                    member.GroupId = 0;
                    member.SendGroupDestroyed();
                    SaySystemAttrib(0xfcff00, $"-{member.Name}退出小组");
                    
                    if (_members.Count <= 1)
                    {
                        DestroyGroup();
                    }
                    else
                    {
                        UpdateNameList();
                    }
                }
            }
        }

        
        
        
        public void LeaveMember(HumanPlayer member)
        {
            lock (_lock)
            {
                if (member == Leader)
                {
                    SaySystemAttrib(0xfcff00, $"-{member.Name}离开小组");
                    DestroyGroup();
                    return;
                }

                if (_members.Remove(member))
                {
                    member.GroupId = 0;
                    member.SendGroupDestroyed();
                    SaySystemAttrib(0xfcff00, $"-{member.Name}离开小组");
                    
                    if (_members.Count <= 1)
                    {
                        DestroyGroup();
                    }
                    else
                    {
                        UpdateNameList();
                    }
                }
            }
        }

        
        
        
        public void DestroyGroup()
        {
            lock (_lock)
            {
                SendGroupDestroyed();
                foreach (var member in _members)
                {
                    member.GroupId = 0;
                    member.SaySystemAttrib(0xfcff00, "-小组被解散了");
                }
                _members.Clear();
                
                
                GroupObjectManager.Instance?.DestroyGroup(this);
            }
        }

        
        
        
        public void SendMessage(HumanPlayer sender, ushort cmd, ushort param1 = 0, ushort param2 = 0, ushort param3 = 0, byte[] data = null)
        {
            lock (_lock)
            {
                foreach (var member in _members)
                {
                    if (member != sender)
                    {
                        var builder = new PacketBuilder();
                        builder.WriteUInt32(member.ObjectId);
                        builder.WriteUInt16(cmd);
                        builder.WriteUInt16(param1);
                        builder.WriteUInt16(param2);
                        builder.WriteUInt16(param3);
                        if (data != null && data.Length > 0)
                        {
                            builder.WriteBytes(data);
                        }
                        member.SendMessage(builder.Build());
                    }
                }
            }
        }

        
        
        
        public void SendChatMessage(HumanPlayer sender, string message)
        {
            lock (_lock)
            {
                var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.TEAM, message);
                
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0);
                builder.WriteUInt16(0x290); 
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteUInt32(chatMessage.SenderId);
                builder.WriteString(chatMessage.SenderName);
                builder.WriteUInt16((ushort)chatMessage.Channel);
                builder.WriteString(chatMessage.Message);
                builder.WriteUInt32(chatMessage.TargetId);
                builder.WriteString(chatMessage.TargetName);
                
                byte[] packet = builder.Build();
                
                foreach (var member in _members)
                {
                    if (member != sender)
                    {
                        
                        var memberPacket = new byte[packet.Length];
                        Array.Copy(packet, memberPacket, packet.Length);
                        BitConverter.GetBytes(member.ObjectId).CopyTo(memberPacket, 0);
                        member.SendMessage(memberPacket);
                    }
                }
                
                
                var senderPacket = new byte[packet.Length];
                Array.Copy(packet, senderPacket, packet.Length);
                BitConverter.GetBytes(sender.ObjectId).CopyTo(senderPacket, 0);
                sender.SendMessage(senderPacket);
            }
        }

        
        
        
        private void UpdateNameList()
        {
            lock (_lock)
            {
                string nameList = string.Join("/", _members.Select(m => m.Name));
                if (!string.IsNullOrEmpty(nameList))
                {
                    nameList += "/";
                }
                
                var data = System.Text.Encoding.GetEncoding("GBK").GetBytes(nameList);
                SendMessage(null, 0x28E, 0, 0, 0, data); 
            }
        }

        
        
        
        private void SaySystemAttrib(uint attrib, string message)
        {
            lock (_lock)
            {
                var data = System.Text.Encoding.GetEncoding("GBK").GetBytes(message);
                SendMessage(null, 0x64, (ushort)(attrib & 0xFFFF), (ushort)(attrib >> 16), 0, data); 
            }
        }

        
        
        
        private void SendGroupDestroyed()
        {
            lock (_lock)
            {
                SendMessage(null, 0x28F, 0, 0, 0); 
            }
        }

        
        
        
        private int GetMaxGroupMemberCount()
        {
            
            var gameVar = GameWorld.Instance?.GetGameVar(GameVarConstants.MaxGroupMember);
            if (gameVar.HasValue)
            {
                return (int)gameVar.Value;
            }
            return 10;
        }

        
        
        
        public int GetMemberCount()
        {
            lock (_lock)
            {
                return _members.Count;
            }
        }

        
        
        
        public List<HumanPlayer> GetAllMembers()
        {
            lock (_lock)
            {
                return new List<HumanPlayer>(_members);
            }
        }

        
        
        
        public void AdjustGroupExp(HumanPlayer killer, uint exp, uint monsterId)
        {
            lock (_lock)
            {
                
                int aroundCount = 0;
                var aroundList = new List<HumanPlayer>();
                
                foreach (var member in _members)
                {
                    if (member.CurrentMap == killer.CurrentMap && !member.IsDead)
                    {
                        int distance = Math.Abs(killer.X - member.X) + Math.Abs(killer.Y - member.Y);
                        if (distance <= 12)
                        {
                            aroundList.Add(member);
                            aroundCount++;
                        }
                    }
                }
                
                if (aroundCount <= 1)
                {
                    killer.AddExp(exp, false, monsterId);
                }
                else
                {
                    uint expPerMember = exp / (uint)aroundCount;
                    foreach (var member in aroundList)
                    {
                        member.AddExp(expPerMember, false, monsterId);
                    }
                }
            }
        }
    }

    
    
    
    public class GroupObjectManager
    {
        private static GroupObjectManager _instance;
        public static GroupObjectManager Instance => _instance ??= new GroupObjectManager();

        private readonly Dictionary<uint, GroupObject> _groups = new();
        private readonly object _lock = new();

        private GroupObjectManager() { }

        
        
        
        public GroupObject CreateGroup(HumanPlayer leader, HumanPlayer firstMember)
        {
            lock (_lock)
            {
                var group = new GroupObject();
                if (group.Create(leader, firstMember))
                {
                    _groups[group.GroupId] = group;
                    return group;
                }
                return null;
            }
        }

        
        
        
        public GroupObject GetGroup(uint groupId)
        {
            lock (_lock)
            {
                return _groups.TryGetValue(groupId, out var group) ? group : null;
            }
        }

        
        
        
        public void DestroyGroup(GroupObject group)
        {
            lock (_lock)
            {
                _groups.Remove(group.GroupId);
            }
        }

        
        
        
        public GroupObject GetPlayerGroup(HumanPlayer player)
        {
            lock (_lock)
            {
                if (player.GroupId == 0)
                    return null;

                return GetGroup(player.GroupId);
            }
        }

        
        
        
        public List<GroupObject> GetAllGroups()
        {
            lock (_lock)
            {
                return new List<GroupObject>(_groups.Values);
            }
        }
    }
}
