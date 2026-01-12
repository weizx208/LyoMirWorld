using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameServer
{
    public class ChatFilter
    {
        private static ChatFilter _instance;
        public static ChatFilter Instance => _instance ??= new ChatFilter();

        private readonly HashSet<string> _sensitiveWords = new(StringComparer.OrdinalIgnoreCase);
        
        private const char REPLACE_CHAR = '*';
        
        private readonly List<Regex> _regexPatterns = new();

        private ChatFilter()
        {
            InitializeSensitiveWords();
            InitializeRegexPatterns();
        }

        private void InitializeSensitiveWords()
        {
            string[] defaultSensitiveWords = {
                "fuck", "shit", "asshole", "bitch", "bastard",
                "操", "傻逼", "脑残", "垃圾", "废物",
                "共产党", "政府", "领导人", "政治", "敏感词"
            };

            foreach (var word in defaultSensitiveWords)
            {
                _sensitiveWords.Add(word);
            }
        }

        private void InitializeRegexPatterns()
        {
            _regexPatterns.Add(new Regex(@"\d{11}", RegexOptions.Compiled)); 
            _regexPatterns.Add(new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled)); 
            _regexPatterns.Add(new Regex(@"https?://[^\s]+", RegexOptions.Compiled)); 
        }

        public bool ContainsSensitiveWords(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            string lowerMessage = message.ToLowerInvariant();

            foreach (var word in _sensitiveWords)
            {
                if (lowerMessage.Contains(word.ToLowerInvariant()))
                {
                    return true;
                }
            }

            foreach (var pattern in _regexPatterns)
            {
                if (pattern.IsMatch(message))
                {
                    return true;
                }
            }

            return false;
        }

        public string FilterSensitiveWords(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            string filteredMessage = message;
            
            foreach (var word in _sensitiveWords)
            {
                if (filteredMessage.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    string replacement = new string(REPLACE_CHAR, word.Length);
                    filteredMessage = Regex.Replace(filteredMessage, word, replacement, RegexOptions.IgnoreCase);
                }
            }

            foreach (var pattern in _regexPatterns)
            {
                filteredMessage = pattern.Replace(filteredMessage, "***");
            }

            return filteredMessage;
        }

        public void AddSensitiveWord(string word)
        {
            if (!string.IsNullOrEmpty(word))
            {
                _sensitiveWords.Add(word);
            }
        }

        public bool RemoveSensitiveWord(string word)
        {
            return _sensitiveWords.Remove(word);
        }

        public List<string> GetAllSensitiveWords()
        {
            return _sensitiveWords.ToList();
        }

        public void ClearSensitiveWords()
        {
            _sensitiveWords.Clear();
        }

        public void ReloadSensitiveWords()
        {
            _sensitiveWords.Clear();
            InitializeSensitiveWords();
        }

        public bool CheckMessageLength(string message, int maxLength = 120)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            return message.Length <= maxLength;
        }

        public string TruncateMessage(string message, int maxLength = 120)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
                return message;

            return message.Substring(0, maxLength);
        }

        public bool ContainsIllegalCharacters(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            foreach (char c in message)
            {
                if (char.IsControl(c) && c != '\n' && c != '\r')
                {
                    return true;
                }
            }

            return false;
        }

        public string RemoveIllegalCharacters(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var chars = message.Where(c => !char.IsControl(c) || c == '\n' || c == '\r').ToArray();
            return new string(chars);
        }

        public string ProcessChatMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            string processedMessage = RemoveIllegalCharacters(message);

            if (!CheckMessageLength(processedMessage))
            {
                processedMessage = TruncateMessage(processedMessage);
            }

            if (ContainsSensitiveWords(processedMessage))
            {
                processedMessage = FilterSensitiveWords(processedMessage);
            }

            return processedMessage;
        }

        public bool CanSendMessage(string message, out string reason)
        {
            reason = string.Empty;

            if (string.IsNullOrEmpty(message))
            {
                reason = "消息内容为空";
                return false;
            }

            if (!CheckMessageLength(message))
            {
                reason = $"消息过长（最大{120}字符）";
                return false;
            }

            if (ContainsIllegalCharacters(message))
            {
                reason = "消息包含非法字符";
                return false;
            }

            if (ContainsSensitiveWords(message))
            {
                reason = "消息包含敏感词";
                return false;
            }

            return true;
        }
    }
}
