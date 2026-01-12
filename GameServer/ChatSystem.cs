namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Network;
    using MirCommon.Utils;

    public class ChatMessage
    {
        public uint SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public ChatChannel Channel { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public uint TargetId { get; set; }
        public string TargetName { get; set; } = string.Empty;

        public ChatMessage(uint senderId, string senderName, ChatChannel channel, string message)
        {
            SenderId = senderId;
            SenderName = senderName;
            Channel = channel;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    public class ChatManager
    {
        private static ChatManager? _instance;
        public static ChatManager Instance => _instance ??= new ChatManager();

        private readonly Dictionary<ChatChannel, List<ChatMessage>> _channelHistory = new();
        private readonly Dictionary<uint, List<ChatMessage>> _playerHistory = new();
        private readonly object _lock = new();

        private readonly Dictionary<uint, DateTime> _lastChatTime = new();
        private readonly Dictionary<uint, int> _chatSpamCount = new();
        private const int MAX_CHAT_SPAM = 5;
        private const int CHAT_SPAM_INTERVAL_SECONDS = 10;
        private const int CHAT_COOLDOWN_SECONDS = 1;

        private ChatManager()
        {
            foreach (ChatChannel channel in Enum.GetValues(typeof(ChatChannel)))
            {
                _channelHistory[channel] = new List<ChatMessage>();
            }
        }

        public bool SendMessage(HumanPlayer sender, ChatChannel channel, string message, uint targetId = 0, string targetName = "")
        {
            if (sender == null || string.IsNullOrWhiteSpace(message))
                return false;

            if (!CanChat(sender.ObjectId))
            {
                sender.SaySystem("发言过于频繁，请稍后再试");
                return false;
            }

            if (message.Length > 200)
            {
                sender.SaySystem("消息过长，最多200个字符");
                return false;
            }

            if (ContainsSensitiveWords(message))
            {
                sender.SaySystem("消息包含敏感词汇");
                return false;
            }

            switch (channel)
            {
                case ChatChannel.AREA:  
                    return SendNormalMessage(sender, message);
                    
                case ChatChannel.PRIVATE:  
                    return SendWhisperMessage(sender, targetId, targetName, message);
                    
                case ChatChannel.GUILD:  
                    return SendGuildMessage(sender, message);
                    
                case ChatChannel.TEAM:  
                    return SendGroupMessage(sender, message);
                    
                case ChatChannel.WORLD:  
                    return SendWorldMessage(sender, message);
                    
                case ChatChannel.TRADE:  
                    return SendTradeMessage(sender, message);
                    
                case ChatChannel.HORN:  
                    return SendShoutMessage(sender, message);
                    
                case ChatChannel.HELP:  
                    return SendHelpMessage(sender, message);
                    
                default:
                    return false;
            }
        }

        private bool SendNormalMessage(HumanPlayer sender, string message)
        {
            var nearbyPlayers = GetNearbyPlayers(sender);
            if (nearbyPlayers.Count == 0)
                return true;

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.AREA, message);
            
            foreach (var player in nearbyPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[普通] {sender.Name}: {message}");
            return true;
        }

        private bool SendWhisperMessage(HumanPlayer sender, uint targetId, string targetName, string message)
        {
            var targetPlayer = HumanPlayerMgr.Instance.FindById(targetId);
            if (targetPlayer == null)
            {
                sender.SaySystem($"玩家 {targetName} 不在线");
                return false;
            }

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.PRIVATE, message)
            {
                TargetId = targetId,
                TargetName = targetName
            };

            SendChatMessageToPlayer(sender, chatMessage);
            
            SendChatMessageToPlayer(targetPlayer, chatMessage);
            
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[私聊] {sender.Name} -> {targetName}: {message}");
            return true;
        }

        private bool SendGuildMessage(HumanPlayer sender, string message)
        {
            if (sender.Guild == null)
            {
                sender.SaySystem("你还没有加入行会");
                return false;
            }

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.GUILD, message);
            
            var guildMembers = sender.Guild.GetAllMembers();
            foreach (var member in guildMembers)
            {
                var player = HumanPlayerMgr.Instance.FindById(member.PlayerId);
                if (player != null)
                {
                    SendChatMessageToPlayer(player, chatMessage);
                }
            }

            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[行会] {sender.Name}: {message}");
            return true;
        }

        private bool SendGroupMessage(HumanPlayer sender, string message)
        {
            if (sender.GroupId == 0)
            {
                sender.SaySystem("你还没有加入队伍");
                return false;
            }

            sender.SaySystem("组队功能暂未实现");
            return false;
        }

        private bool SendWorldMessage(HumanPlayer sender, string message)
        {
            if (sender.Level < 20)
            {
                sender.SaySystem("需要20级才能使用世界频道");
                return false;
            }

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.WORLD, message);
            
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[世界] {sender.Name}: {message}");
            return true;
        }

        private bool SendTradeMessage(HumanPlayer sender, string message)
        {
            if (sender.Level < 15)
            {
                sender.SaySystem("需要15级才能使用交易频道");
                return false;
            }

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.TRADE, message);
            
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[交易] {sender.Name}: {message}");
            return true;
        }

        private bool SendShoutMessage(HumanPlayer sender, string message)
        {
            var shoutPlayers = GetShoutRangePlayers(sender);
            if (shoutPlayers.Count == 0)
                return true;

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.HORN, message);
            
            foreach (var player in shoutPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[喊话] {sender.Name}: {message}");
            return true;
        }

        private bool SendHelpMessage(HumanPlayer sender, string message)
        {
            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.HELP, message);
            
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[帮助] {sender.Name}: {message}");
            return true;
        }

        public void SendSystemMessage(string message, ChatChannel channel = ChatChannel.SYSTEM)
        {
            var chatMessage = new ChatMessage(0, "系统", channel, message);
            
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[系统] {message}");
        }

        public void SendAnnouncement(string message)
        {
            var chatMessage = new ChatMessage(0, "公告", ChatChannel.ANNOUNCEMENT, message);
            
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[公告] {message}");
        }

        private void SendChatMessageToPlayer(HumanPlayer player, ChatMessage chatMessage)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write(chatMessage.SenderId);
                
                byte[] senderNameBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(chatMessage.SenderName);
                writer.Write(senderNameBytes);
                writer.Write((byte)0); 
                
                writer.Write((ushort)chatMessage.Channel);
                
                byte[] messageBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(chatMessage.Message);
                writer.Write(messageBytes);
                writer.Write((byte)0); 
                
                writer.Write(chatMessage.TargetId);
                
                byte[] targetNameBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(chatMessage.TargetName);
                writer.Write(targetNameBytes);
                writer.Write((byte)0); 
                
                byte[] payload = ms.ToArray();
                
                player.SendMsg(player.ObjectId, GameMessageHandler.ServerCommands.SM_CHAT, 0, 0, 0, payload);
            }
        }

        private List<HumanPlayer> GetNearbyPlayers(HumanPlayer sender)
        {
            var nearbyPlayers = new List<HumanPlayer>();
            
            if (sender.CurrentMap == null)
                return nearbyPlayers;

            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player.ObjectId == sender.ObjectId)
                    continue;

                if (player.CurrentMap != sender.CurrentMap)
                    continue;

                int distance = Math.Abs(sender.X - player.X) + Math.Abs(sender.Y - player.Y);
                if (distance <= 10)
                {
                    nearbyPlayers.Add(player);
                }
            }

            return nearbyPlayers;
        }

        private List<HumanPlayer> GetShoutRangePlayers(HumanPlayer sender)
        {
            var shoutPlayers = new List<HumanPlayer>();
            
            if (sender.CurrentMap == null)
                return shoutPlayers;

            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player.ObjectId == sender.ObjectId)
                    continue;

                if (player.CurrentMap != sender.CurrentMap)
                    continue;

                int distance = Math.Abs(sender.X - player.X) + Math.Abs(sender.Y - player.Y);
                if (distance <= 20)
                {
                    shoutPlayers.Add(player);
                }
            }

            return shoutPlayers;
        }

        private bool CanChat(uint playerId)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                
                if (_lastChatTime.TryGetValue(playerId, out var lastTime))
                {
                    var timeSinceLastChat = (now - lastTime).TotalSeconds;
                    if (timeSinceLastChat < CHAT_COOLDOWN_SECONDS)
                        return false;
                }

                if (!_chatSpamCount.ContainsKey(playerId))
                    _chatSpamCount[playerId] = 0;

                if ((now - lastTime).TotalSeconds > CHAT_SPAM_INTERVAL_SECONDS)
                {
                    _chatSpamCount[playerId] = 0;
                }

                if (_chatSpamCount[playerId] >= MAX_CHAT_SPAM)
                    return false;

                _chatSpamCount[playerId]++;
                _lastChatTime[playerId] = now;
                
                return true;
            }
        }

        private bool ContainsSensitiveWords(string message)
        {
            string[] sensitiveWords = {
                "fuck", "shit", "asshole", "bitch", "damn",
                "操", "傻逼", "垃圾", "废物", "妈的"
            };

            string lowerMessage = message.ToLower();
            foreach (var word in sensitiveWords)
            {
                if (lowerMessage.Contains(word))
                    return true;
            }

            return false;
        }

        private void RecordMessage(ChatMessage message)
        {
            lock (_lock)
            {
                if (_channelHistory.ContainsKey(message.Channel))
                {
                    var history = _channelHistory[message.Channel];
                    history.Add(message);
                    
                    if (history.Count > 1000)
                    {
                        history.RemoveAt(0);
                    }
                }

                if (!_playerHistory.ContainsKey(message.SenderId))
                {
                    _playerHistory[message.SenderId] = new List<ChatMessage>();
                }
                
                var playerHistory = _playerHistory[message.SenderId];
                playerHistory.Add(message);
                
                if (playerHistory.Count > 500)
                {
                    playerHistory.RemoveAt(0);
                }
            }
        }

        public List<ChatMessage> GetChannelHistory(ChatChannel channel, int count = 50)
        {
            lock (_lock)
            {
                if (_channelHistory.TryGetValue(channel, out var history))
                {
                    int startIndex = Math.Max(0, history.Count - count);
                    return history.Skip(startIndex).Take(count).ToList();
                }
                return new List<ChatMessage>();
            }
        }

        public List<ChatMessage> GetPlayerHistory(uint playerId, int count = 100)
        {
            lock (_lock)
            {
                if (_playerHistory.TryGetValue(playerId, out var history))
                {
                    int startIndex = Math.Max(0, history.Count - count);
                    return history.Skip(startIndex).Take(count).ToList();
                }
                return new List<ChatMessage>();
            }
        }

        public void ClearPlayerHistory(uint playerId)
        {
            lock (_lock)
            {
                _playerHistory.Remove(playerId);
            }
        }

        public void ClearChannelHistory(ChatChannel channel)
        {
            lock (_lock)
            {
                if (_channelHistory.ContainsKey(channel))
                {
                    _channelHistory[channel].Clear();
                }
            }
        }

        public DateTime? GetLastChatTime(uint playerId)
        {
            lock (_lock)
            {
                if (_lastChatTime.TryGetValue(playerId, out var lastTime))
                {
                    return lastTime;
                }
                return null;
            }
        }

        public void ResetChatLimits(uint playerId)
        {
            lock (_lock)
            {
                _lastChatTime.Remove(playerId);
                _chatSpamCount.Remove(playerId);
            }
        }
    }
}
