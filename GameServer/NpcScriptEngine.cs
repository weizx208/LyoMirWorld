using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    
    
    public static class NpcScriptEngine
    {
        private const ushort SM_NPCPAGE = MirCommon.ProtocolCmd.SM_NPCPAGE; 
        private const ushort SM_CLOSEPAGE = 0x284;
        private const ushort SM_SENDGOODSLIST = 0x285; 
        private const ushort SM_OPENSELL = 0x286;      
        private const ushort SM_OPENREPAIR = MirCommon.ProtocolCmd.SM_OPENREPAIR; 

        public static bool TryHandleTalk(HumanPlayer player, Npc npc)
        {
            if (player == null || npc == null)
                return false;

            var scriptObject = TryGetScriptObject(npc);
            if (scriptObject == null)
                return false;

            SendPage(player, npc, scriptObject, "@main");
            return true;
        }

        public static bool TryHandleSelectLink(HumanPlayer player, uint npcId, string link)
        {
            if (player == null)
                return false;

            
            if (npcId == 0xffffffff)
                return false;

            var npc = NpcManagerEx.Instance.GetNpc(npcId) ?? (Npc?)NPCManager.Instance.GetNPC(npcId);
            if (npc == null)
                return false;

            var scriptObject = TryGetScriptObject(npc);
            if (scriptObject == null)
                return false;

            string normalized = (link ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                SendPage(player, npc, scriptObject, "@main");
                return true;
            }

            if (string.Equals(normalized, "@exit", StringComparison.OrdinalIgnoreCase))
            {
                ClosePage(player, npc);
                return true;
            }

            if (!normalized.StartsWith("@", StringComparison.Ordinal))
            {
                
                normalized = "@" + normalized;
            }

            
            if (TryHandleSpecialLink(player, npc, scriptObject, normalized))
                return true;

            SendPage(player, npc, scriptObject, normalized);
            return true;
        }

        internal static ScriptObject? TryGetScriptObject(Npc npc)
        {
            
            if (npc is NpcInstanceEx ex && ex.Definition.ScriptObject != null)
                return ex.Definition.ScriptObject;

            if (!string.IsNullOrEmpty(npc.ScriptFile))
            {
                string key = System.IO.Path.GetFileNameWithoutExtension(npc.ScriptFile);
                var obj = ScriptObjectMgr.Instance.GetScriptObject(key);
                if (obj != null)
                    return obj;

                
                var all = ScriptObjectMgr.Instance.GetAllScriptObjectNames();
                var found = all.FirstOrDefault(n => string.Equals(n, key, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(found))
                    return ScriptObjectMgr.Instance.GetScriptObject(found);
            }

            return null;
        }

        private static bool TryHandleSpecialLink(HumanPlayer player, Npc npc, ScriptObject scriptObject, string normalizedLink)
        {
            if (string.IsNullOrEmpty(normalizedLink))
                return false;

            
            if (string.Equals(normalizedLink, "@buy", StringComparison.OrdinalIgnoreCase))
            {
                SendGoodsList(player, npc, scriptObject);
                return true;
            }

            if (string.Equals(normalizedLink, "@sell", StringComparison.OrdinalIgnoreCase))
            {
                player.SendMsg(npc.ObjectId, SM_OPENSELL, 0, 0, 0, null);
                return true;
            }

            if (string.Equals(normalizedLink, "@repair", StringComparison.OrdinalIgnoreCase))
            {
                
                player.SendMsg(npc.ObjectId, SM_OPENREPAIR, 0, 0, 0, null);
                return true;
            }

            if (string.Equals(normalizedLink, "@s_repair", StringComparison.OrdinalIgnoreCase))
            {
                
                player.SendMsg(npc.ObjectId, SM_OPENREPAIR, 0, 0, 0, null);
                return true;
            }

            return false;
        }

        private static void SendGoodsList(HumanPlayer player, Npc npc, ScriptObject scriptObject)
        {
            try
            {
                float buyPercent = 1.0f;
                if (npc is NpcInstanceEx ex)
                    buyPercent = ex.Definition.BuyPercent;

                var goods = ScriptNpcShopHelper.ExtractGoods(scriptObject);
                var sb = new StringBuilder(1024);
                int count = 0;

                foreach (var g in goods)
                {
                    string name = g.TemplateName;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var def = ItemManager.Instance.GetDefinitionByName(name);
                    if (def == null)
                        continue;

                    uint templatePrice = ScriptNpcShopHelper.CalcBuyPrice(def, buyPercent);
                    uint limitedFlag = g.DefaultCount != 0 ? 1u : 0u;
                    uint curCount = g.DefaultCount > 0 ? (uint)g.DefaultCount : 0u;

                    sb.Append(name).Append('/')
                      .Append(limitedFlag).Append('/')
                      .Append(templatePrice).Append('/')
                      .Append(curCount).Append('/');

                    count++;
                }

                player.SendMsg(npc.ObjectId, SM_SENDGOODSLIST, (ushort)Math.Clamp(count, 0, ushort.MaxValue), 0, 0, sb.ToString());
            }
            catch (Exception ex)
            {
                LogManager.Default.Warning($"发送NPC商品列表失败: npc={npc?.Name}({npc?.ObjectId:X8}) err={ex.Message}");
                try { player.SendMsg(npc.ObjectId, SM_SENDGOODSLIST, 0, 0, 0, string.Empty); } catch { }
            }
        }

        private static void ClosePage(HumanPlayer player, Npc npc)
        {
            player.SendMsg(npc.ObjectId, SM_CLOSEPAGE, 0, 0, 0, null);
        }

        private static void SendPage(HumanPlayer player, Npc npc, ScriptObject scriptObject, string pageName)
        {
            string page = ExtractPage(scriptObject, pageName, player);
            if (string.IsNullOrEmpty(page))
            {
                
                ClosePage(player, npc);
                return;
            }

            
            string payload = $"{npc.Name}/" + page;
            player.SendMsg(npc.ObjectId, SM_NPCPAGE, 0, 0, 1, payload);
        }

        private static string ExtractPage(ScriptObject scriptObject, string pageName, HumanPlayer player)
        {
            if (scriptObject.Lines == null || scriptObject.Lines.Count == 0)
                return string.Empty;

            int start = -1;
            for (int i = 0; i < scriptObject.Lines.Count; i++)
            {
                if (TryParsePageHeader(scriptObject.Lines[i], out var header) &&
                    string.Equals(header, pageName, StringComparison.OrdinalIgnoreCase))
                {
                    start = i + 1;
                    break;
                }
            }

            if (start < 0 || start >= scriptObject.Lines.Count)
                return string.Empty;

            var output = new StringBuilder();

            bool inIf = false;
            bool condTrue = true;
            IfSection section = IfSection.None;

            for (int i = start; i < scriptObject.Lines.Count; i++)
            {
                string raw = scriptObject.Lines[i];
                string line = raw.Trim();

                if (TryParsePageHeader(line, out _))
                    break;

                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    if (line.Equals("#if", StringComparison.OrdinalIgnoreCase))
                    {
                        inIf = true;
                        condTrue = true;
                        section = IfSection.Conditions;
                        continue;
                    }
                    if (line.Equals("#act", StringComparison.OrdinalIgnoreCase))
                    {
                        if (inIf)
                            section = IfSection.Act;
                        continue;
                    }
                    if (line.Equals("#elsesay", StringComparison.OrdinalIgnoreCase))
                    {
                        if (inIf)
                            section = IfSection.ElseSay;
                        continue;
                    }
                    if (line.Equals("#elseact", StringComparison.OrdinalIgnoreCase))
                    {
                        if (inIf)
                            section = IfSection.ElseAct;
                        continue;
                    }

                    
                    continue;
                }

                if (!inIf)
                {
                    AppendOutputLine(output, raw, player);
                    continue;
                }

                switch (section)
                {
                    case IfSection.Conditions:
                        condTrue &= EvalCondition(player, line);
                        break;
                    case IfSection.ElseSay:
                        if (!condTrue)
                            AppendOutputLine(output, raw, player);
                        break;
                    default:
                        
                        break;
                }
            }

            return output.ToString();
        }

        private enum IfSection
        {
            None = 0,
            Conditions = 1,
            Act = 2,
            ElseSay = 3,
            ElseAct = 4,
        }

        private static void AppendOutputLine(StringBuilder sb, string rawLine, HumanPlayer player)
        {
            if (string.IsNullOrEmpty(rawLine))
                return;

            
            string substituted = SubstituteVars(rawLine, player);
            sb.Append(substituted);
        }

        private static string SubstituteVars(string text, HumanPlayer player)
        {
            
            return Regex.Replace(text, "<\\$(?<var>[^>]+)>", m =>
            {
                string key = m.Groups["var"].Value.Trim();
                return key.ToLowerInvariant() switch
                {
                    "username" => player.Name,
                    "serverdatetime" => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    _ => string.Empty
                };
            });
        }

        private static bool TryParsePageHeader(string line, out string header)
        {
            header = string.Empty;
            if (string.IsNullOrEmpty(line))
                return false;

            string trimmed = line.Trim();
            if (trimmed.Length < 3 || trimmed[0] != '[' || trimmed[^1] != ']')
                return false;

            header = trimmed.Substring(1, trimmed.Length - 2).Trim();
            return header.Length > 0;
        }

        private static bool EvalCondition(HumanPlayer player, string conditionLine)
        {
            try
            {
                var parts = SplitTokens(conditionLine);
                if (parts.Count == 0)
                    return false;

                string cmd = parts[0].ToLowerInvariant();
                switch (cmd)
                {
                    case "hour":
                        if (parts.Count >= 3 &&
                            int.TryParse(parts[1], out int h1) &&
                            int.TryParse(parts[2], out int h2))
                        {
                            int h = DateTime.Now.Hour;
                            return h >= h1 && h <= h2;
                        }
                        return false;

                    case "checkex":
                        if (parts.Count >= 4 && int.TryParse(parts[3], out int rhs))
                        {
                            int lhs = parts[1].ToLowerInvariant() switch
                            {
                                "level" => player.Level,
                                "gold" => (int)Math.Min(player.Gold, int.MaxValue),
                                "hp" => player.CurrentHP,
                                "mp" => player.CurrentMP,
                                "maxhp" => player.MaxHP,
                                "maxmp" => player.MaxMP,
                                _ => 0
                            };

                            char op = parts[2].Length > 0 ? parts[2][0] : '=';
                            return op switch
                            {
                                '<' => lhs < rhs,
                                '>' => lhs > rhs,
                                '=' => lhs == rhs,
                                '!' => lhs != rhs,
                                _ => false
                            };
                        }
                        return false;

                    case "checkbagitem":
                        if (parts.Count >= 3 && int.TryParse(parts[^1], out int needCount))
                        {
                            
                            string itemName = string.Join(" ", parts.Skip(1).Take(parts.Count - 2));
                            if (string.IsNullOrEmpty(itemName))
                                return false;

                            int count = 0;
                            foreach (var kv in player.Inventory.GetAllItems())
                            {
                                var it = kv.Value;
                                if (it?.Definition != null &&
                                    string.Equals(it.Definition.Name, itemName, StringComparison.OrdinalIgnoreCase))
                                {
                                    count += it.Count;
                                }
                            }
                            return count >= needCount;
                        }
                        return false;

                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static List<string> SplitTokens(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(line))
                return result;

            
            bool inQuotes = false;
            var current = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            return result;
        }
    }
}
