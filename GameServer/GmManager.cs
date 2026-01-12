using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    public sealed class GmManager
    {
        private static GmManager? _instance;
        public static GmManager Instance => _instance ??= new GmManager();

        private readonly object _lock = new();
        private readonly Dictionary<string, int> _gmLevelsByAccount = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameCommandDef> _commandDefs = new(StringComparer.OrdinalIgnoreCase);

        private string _gmListPath = string.Empty;
        private string _cmdListPath = string.Empty;

        private sealed class GameCommandDef
        {
            public bool IsGmCmd { get; init; }
            public int LimitLevel { get; init; }
            public string BuiltinCommand { get; init; } = string.Empty;
        }

        private GmManager() { }

        public int LoadGmList(string filePath)
        {
            lock (_lock)
            {
                _gmListPath = filePath ?? string.Empty;
                _gmLevelsByAccount.Clear();

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return 0;

                int count = 0;
                foreach (var raw in SmartReader.ReadAllLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;

                    
                    var parts = line.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                        continue;

                    string account = parts[0].Trim();
                    if (account.Length == 0)
                        continue;

                    if (!int.TryParse(parts[1].Trim(), out int level))
                        continue;

                    _gmLevelsByAccount[account] = level;
                    count++;
                }

                return count;
            }
        }

        public int LoadCommandDef(string filePath)
        {
            lock (_lock)
            {
                _cmdListPath = filePath ?? string.Empty;
                _commandDefs.Clear();

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return 0;

                int count = 0;
                foreach (var raw in SmartReader.ReadAllLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;

                    if (!TryParseCmdListLine(line, out int level, out string alias, out string builtin))
                        continue;

                    var def = new GameCommandDef
                    {
                        IsGmCmd = level > 0,
                        LimitLevel = Math.Abs(level),
                        BuiltinCommand = builtin,
                    };

                    if (!_commandDefs.ContainsKey(alias))
                    {
                        _commandDefs[alias] = def;
                        count++;
                    }
                }

                return count;
            }
        }

        public int GetGmLevel(string? account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return 0;

            lock (_lock)
            {
                return _gmLevelsByAccount.TryGetValue(account.Trim(), out int level) ? level : 0;
            }
        }

        public int ReloadCommandDef()
        {
            string path;
            lock (_lock)
            {
                path = _cmdListPath;
            }

            return LoadCommandDef(path);
        }

        public bool ExecGameCmd(string commandLine, HumanPlayer player)
        {
            if (player == null)
                return false;

            var tokens = SplitCommandLine(commandLine);
            if (tokens.Count == 0)
                return false;

            string alias = tokens[0];
            GameCommandDef? def;
            lock (_lock)
            {
                _commandDefs.TryGetValue(alias, out def);
            }

            if (def == null)
            {
                player.SaySystem($"<{alias} 命令不存在>");
                return false;
            }

            bool isGmMode = player.GetSystemFlag((int)MirCommon.SystemFlag.SF_GM);
            int gmLevel = player.GmLevel;

            if (def.IsGmCmd)
            {
                if (!isGmMode)
                {
                    player.SaySystem($"<{alias} 命令不存在>");
                    return false;
                }

                if (gmLevel < def.LimitLevel)
                {
                    player.SaySystem("GM等级不足，无法执行该命令");
                    return false;
                }
            }
            else
            {
                if (!isGmMode && player.Level < def.LimitLevel)
                {
                    player.SaySystem("等级不足，无法执行该命令");
                    return false;
                }
            }

            string builtin = def.BuiltinCommand;
            var args = tokens.Skip(1).ToArray();
            uint result = ExecuteBuiltin(builtin, player, args);

            if (def.IsGmCmd)
            {
                player.SaySystem($"命令返回值: {result}");
            }

            return true;
        }

        private uint ExecuteBuiltin(string builtinCommand, HumanPlayer player, string[] args)
        {
            if (string.IsNullOrWhiteSpace(builtinCommand))
                return 0;

            switch (builtinCommand.Trim().ToUpperInvariant())
            {
                case "GAMEMASTER":
                    return CmdGamemaster(player);
                case "GIVEEXP":
                    return CmdGiveExp(player, args);
                case "GIVEGOLD":
                    return CmdGiveGold(player, args);
                case "GIVEYUANBAO":
                    return CmdGiveYuanbao(player, args);
                case "GIVE":
                    return CmdGive(player, args);
                case "TAKEGOLD":
                    return CmdTakeGold(player, args);
                case "TAKEYUANBAO":
                    return CmdTakeYuanbao(player, args);
                case "RELOADCMDLIST":
                    return CmdReloadCmdList(player);
                case "RELOADMARKET":
                    return CmdReloadMarket(player);
                default:
                    player.SaySystem($"未实现命令: {builtinCommand}");
                    return 0;
            }
        }

        private uint CmdGive(HumanPlayer player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SaySystem("用法: @GIVE <物品名> [数量]");
                return 0;
            }

            string itemName = args[0].Trim();
            uint count = 1;
            if (args.Length >= 2 && (!uint.TryParse(args[1], out count) || count == 0))
            {
                player.SaySystem("参数错误");
                return 0;
            }

            
            uint success = 0;
            for (uint i = 0; i < count; i++)
            {
                if (!player.CreateBagItem(itemName))
                    break;
                success++;
            }

            if (success == 0)
            {
                player.SaySystem("发放失败：物品不存在或背包已满");
            }

            return success;
        }

        private uint CmdGamemaster(HumanPlayer player)
        {
            bool isGmMode = player.GetSystemFlag((int)MirCommon.SystemFlag.SF_GM);
            if (isGmMode && player.GmLevel > 0)
            {
                player.SetSystemFlag((int)MirCommon.SystemFlag.SF_GM, false);
                player.GmLevel = 0;
                player.SaySystem("退出GM模式!");
                return 1;
            }

            int gmLevel = GetGmLevel(player.Account);
            if (gmLevel <= 0)
            {
                player.SaySystem("你没有权限进入GM模式");
                return 0;
            }

            player.SetSystemFlag((int)MirCommon.SystemFlag.SF_GM, true);
            player.GmLevel = gmLevel;
            player.SaySystem("进入GM模式!");
            return 1;
        }

        private uint CmdGiveExp(HumanPlayer player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SaySystem("用法: @GIVEEXP <exp>");
                return 0;
            }

            if (!uint.TryParse(args[0], out uint exp) || exp == 0)
            {
                player.SaySystem("参数错误");
                return 0;
            }

            player.AddExp(exp, noBonus: true);
            return exp;
        }

        private uint CmdGiveGold(HumanPlayer player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SaySystem("用法: @GIVEGOLD <gold>");
                return 0;
            }

            if (!uint.TryParse(args[0], out uint gold) || gold == 0)
            {
                player.SaySystem("参数错误");
                return 0;
            }

            player.Gold += gold;
            player.SendMoneyChanged(MoneyType.Gold);
            return gold;
        }

        private uint CmdTakeGold(HumanPlayer player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SaySystem("用法: @TAKEGOLD <gold>");
                return 0;
            }

            if (!uint.TryParse(args[0], out uint gold) || gold == 0)
            {
                player.SaySystem("参数错误");
                return 0;
            }

            if (player.Gold < gold)
                gold = player.Gold;
            player.Gold -= gold;
            player.SendMoneyChanged(MoneyType.Gold);
            return gold;
        }

        private uint CmdGiveYuanbao(HumanPlayer player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SaySystem("用法: @GIVEYUANBAO <yuanbao>");
                return 0;
            }

            if (!uint.TryParse(args[0], out uint yuanbao) || yuanbao == 0)
            {
                player.SaySystem("参数错误");
                return 0;
            }

            player.Yuanbao += yuanbao;
            player.SendMoneyChanged(MoneyType.Yuanbao);
            return yuanbao;
        }

        private uint CmdTakeYuanbao(HumanPlayer player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SaySystem("用法: @TAKEYUANBAO <yuanbao>");
                return 0;
            }

            if (!uint.TryParse(args[0], out uint yuanbao) || yuanbao == 0)
            {
                player.SaySystem("参数错误");
                return 0;
            }

            if (player.Yuanbao < yuanbao)
                yuanbao = player.Yuanbao;
            player.Yuanbao -= yuanbao;
            player.SendMoneyChanged(MoneyType.Yuanbao);
            return yuanbao;
        }

        private uint CmdReloadCmdList(HumanPlayer player)
        {
            int count = ReloadCommandDef();
            player.SaySystem($"已重载GM命令定义: {count} 条");
            return (uint)count;
        }

        private uint CmdReloadMarket(HumanPlayer player)
        {
            try
            {
                string dataDir = Path.Combine(Environment.CurrentDirectory, "data");
                string marketDir = Path.Combine(dataDir, "Market");
                string scrollText = Path.Combine(marketDir, "scrolltext.txt");
                string mainDir = Path.Combine(marketDir, "MainDir.txt");

                if (File.Exists(scrollText))
                    MarketManager.Instance.LoadScrollText(scrollText);
                if (File.Exists(mainDir))
                    MarketManager.Instance.LoadMainDirectory(mainDir);

                player.SaySystem("商城已重载");
                return 1;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"重载商城失败: {ex.Message}", exception: ex);
                player.SaySystem("商城重载失败");
                return 0;
            }
        }

        private static bool TryParseCmdListLine(string line, out int level, out string alias, out string builtin)
        {
            level = 0;
            alias = string.Empty;
            builtin = string.Empty;

            
            int l = line.IndexOf('(');
            int r = line.IndexOf(')');
            int eq = line.IndexOf('=');
            if (l < 0 || r <= l || eq <= r)
                return false;

            string levelStr = line.Substring(l + 1, r - l - 1).Trim();
            if (!int.TryParse(levelStr, out level))
                return false;

            alias = line.Substring(r + 1, eq - (r + 1)).Trim();
            builtin = line.Substring(eq + 1).Trim();

            return alias.Length > 0 && builtin.Length > 0;
        }

        private static List<string> SplitCommandLine(string commandLine)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(commandLine))
                return result;

            bool inQuotes = false;
            var sb = new StringBuilder();

            foreach (char c in commandLine)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && (char.IsWhiteSpace(c) || c == ','))
                {
                    if (sb.Length > 0)
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    continue;
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
                result.Add(sb.ToString());

            return result;
        }
    }
}
