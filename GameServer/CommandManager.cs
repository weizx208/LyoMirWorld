using System;
using System.Collections.Generic;
using System.IO;

namespace GameServer
{
    public class ScriptParam
    {
        public string StringValue { get; set; } = string.Empty;
        public int IntValue { get; set; }
    }

    public delegate uint CommandProc(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution);

    public class CommandManager
    {
        private static CommandManager? _instance;
        public static CommandManager Instance => _instance ??= new CommandManager();

        private readonly Dictionary<string, CommandProc> _commandProcs = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        private CommandManager()
        {
            InitializeDefaultCommands();
        }

        private void InitializeDefaultCommands()
        {
            AddCommand("RANDOM", ProcRandom);
            AddCommand("GIVE", ProcGive);
            AddCommand("SET", ProcSet);
            AddCommand("CHECK", ProcCheck);
            AddCommand("CHECKEX", ProcCheckEx);
            AddCommand("TAKEBAGITEM", ProcTakeBagItem);
            AddCommand("GIVEGOLD", ProcGiveGold);
            AddCommand("TAKEGOLD", ProcTakeGold);
            AddCommand("GIVEYUANBAO", ProcGiveYuanbao);
            AddCommand("TAKEYUANBAO", ProcTakeYuanbao);
            AddCommand("GIVEEXP", ProcGiveExp);
            AddCommand("MOVE", ProcMove);
            AddCommand("MAPMOVE", ProcMapMove);
            AddCommand("CLOSE", ProcClose);
            AddCommand("GOTO", ProcGoto);
            AddCommand("DELAY", ProcDelay);
            AddCommand("RETURN", ProcReturn);
            AddCommand("CALL", ProcCall);
            AddCommand("INC", ProcInc);
            AddCommand("DEC", ProcDec);
            AddCommand("VAR", ProcVar);
            AddCommand("CLRVAR", ProcClrVar);
            AddCommand("MOVR", ProcMovr);
            AddCommand("SYSTEMMSG", ProcSystemMsg);
            AddCommand("SCROLLMSG", ProcScrollMsg);

            AddCommand("HOUR", ProcHour);
            AddCommand("MINUTE", ProcMinute);
            AddCommand("DAYOFWEEK", ProcDayOfWeek);
            AddCommand("BEFORE", ProcBefore);
            AddCommand("AFTER", ProcAfter);

            AddCommand("US_CHECKANDUPTODATE", ProcUsCheckAndUpToDate);
            AddCommand("US_DAMAGEDURA", ProcUsDamageDura);
            AddCommand("US_DELETE", ProcUsDelete);
            AddCommand("SETEXPFACTOR", ProcSetExpFactor);

            AddCommand("ADDDYNAMICNPC", ProcAddDynamicNpc);
            AddCommand("REMOVEDYNAMICNPC", ProcRemoveDynamicNpc);
        }

        public bool AddCommand(string command, CommandProc proc)
        {
            if (string.IsNullOrEmpty(command) || proc == null)
                return false;

            lock (_lock)
            {
                if (_commandProcs.ContainsKey(command))
                {
                    Console.WriteLine($"命令 {command} 已经注册过");
                    return false;
                }

                _commandProcs[command] = proc;
                Console.WriteLine($"注册命令: {command}");
                return true;
            }
        }

        public CommandProc? GetCommandProc(string command)
        {
            if (string.IsNullOrEmpty(command))
                return null;

            lock (_lock)
            {
                return _commandProcs.TryGetValue(command, out var proc) ? proc : null;
            }
        }

        public bool ChangeCommandName(string oldCommand, string newCommand)
        {
            if (string.IsNullOrEmpty(oldCommand) || string.IsNullOrEmpty(newCommand))
                return false;

            lock (_lock)
            {
                if (!_commandProcs.TryGetValue(oldCommand, out var proc))
                    return false;

                _commandProcs.Remove(oldCommand);
                _commandProcs[newCommand] = proc;
                return true;
            }
        }

        public List<string> GetAllCommandNames()
        {
            lock (_lock)
            {
                return new List<string>(_commandProcs.Keys);
            }
        }

        public int GetCommandCount()
        {
            lock (_lock)
            {
                return _commandProcs.Count;
            }
        }

        #region 命令处理器实现

        private static uint ProcRandom(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            var random = new Random();
            if (paramCount == 0)
                return (uint)random.Next();
            else if (paramCount == 1)
                return (uint)random.Next(parameters[0].IntValue);
            else
                return (uint)random.Next(parameters[0].IntValue, parameters[1].IntValue);
        }

        private static uint ProcGive(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcSet(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 1)
                return 0;

            return 1;
        }

        private static uint ProcCheck(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 1)
                return 0;

            int value = 0;
            if (paramCount > 1)
            {
                int compValue = parameters[1].IntValue;
                return (uint)(value == compValue ? 1 : 0);
            }
            else
            {
                return (uint)(value > 0 ? 1 : 0);
            }
        }

        private static uint ProcCheckEx(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount != 3)
                return 0;

            int value = ResolveInt(shell, target, parameters[0]);
            int compValue = ResolveInt(shell, target, parameters[2]);
            char sign = parameters[1].StringValue.Length > 0 ? parameters[1].StringValue[0] : '=';

            switch (sign)
            {
                case '<': return (uint)(value < compValue ? 1 : 0);
                case '>': return (uint)(value > compValue ? 1 : 0);
                case '=': return (uint)(value == compValue ? 1 : 0);
                case '!': return (uint)(value != compValue ? 1 : 0);
                default: return 0;
            }
        }

        private static int ResolveInt(ScriptShell shell, ScriptTarget target, ScriptParam param)
        {
            if (shell == null)
                return param.IntValue;

            string token = (param.StringValue ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(token))
                return 0;

            if (token.StartsWith("$", StringComparison.Ordinal))
            {
                string varName = token.Substring(1);
                string? shellVal = shell.GetVariableValue(varName);
                if (!string.IsNullOrEmpty(shellVal) && int.TryParse(shellVal, out int vShell))
                    return vShell;

                return ResolveTargetVarInt(target, varName);
            }

            if (int.TryParse(token, out int v))
                return v;

            return ResolveTargetVarInt(target, token);
        }

        private static int ResolveTargetVarInt(ScriptTarget target, string name)
        {
            if (target is not HumanPlayer p || string.IsNullOrWhiteSpace(name))
                return 0;

            string key = name.Trim().TrimStart('$').ToLowerInvariant();
            return key switch
            {
                "level" => p.Level,
                "gold" => (int)Math.Min(p.Gold, int.MaxValue),
                "hp" => p.CurrentHP,
                "mp" => p.CurrentMP,
                "maxhp" => p.MaxHP,
                "maxmp" => p.MaxMP,
                "expfactor" => p.GetExpFactor100(),
                _ => 0
            };
        }

        private static uint ProcSetExpFactor(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            if (target is not HumanPlayer p)
                return 0;

            int before = p.GetExpFactor100();
            int factor100 = ResolveInt(shell, target, parameters[0]);
            float factor = factor100 / 100.0f;
            p.SetExpFactor(factor);
            int after = p.GetExpFactor100();
            if (after != before)
            {
                p.SaySystem($"经验倍率已调整为 {after}%");
            }
            return 1;
        }

        private static uint ProcUsDelete(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (target is not HumanPlayer p)
                return 0;

            return p.MarkUsingItemDeleted() ? 1u : 0u;
        }

        private static uint ProcUsDamageDura(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            if (target is not HumanPlayer p)
                return 0;

            int damage = ResolveInt(shell, target, parameters[0]);
            return p.DamageUsingItemDura(damage) ? 1u : 0u;
        }

        private static uint ProcUsCheckAndUpToDate(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (target is not HumanPlayer p)
                return 0;

            return p.CheckAndUpdateUsingItemTime();
        }

        private static uint ProcTakeBagItem(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcGiveGold(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcTakeGold(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcGiveYuanbao(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcTakeYuanbao(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcGiveExp(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcMove(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;
            
            return 1;
        }

        private static uint ProcMapMove(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;
            
            return 1;
        }

        private static uint ProcClose(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            shell.SetExecuteResult(ScriptShell.ExecuteResult.Close);
            continueExecution = false;
            return 0;
        }

        private static uint ProcGoto(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDelay(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            return 1;
        }

        private static uint ProcReturn(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            string value = paramCount > 0 ? parameters[0].StringValue : "0";
            shell.SetExecuteResult(ScriptShell.ExecuteResult.Return, value);
            continueExecution = false;
            return 0;
        }

        private static uint ProcCall(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcInc(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDec(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcVar(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcClrVar(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcMovr(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcSystemMsg(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcScrollMsg(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCheckBagItem(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCheckEquipment(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcLevelUp(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcAddNameList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcAddAccountList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcAddIpList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDelNameList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDelAccountList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDelIpList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCheckAccountList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCheckCharNameList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCheckIpList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcHour(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            int hour = DateTime.Now.Hour;
            int start = parameters[0].IntValue;
            int end = parameters[1].IntValue;
            return (uint)(hour >= start && hour <= end ? 1 : 0);
        }

        private static uint ProcMinute(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            int minute = DateTime.Now.Minute;
            int start = parameters[0].IntValue;
            int end = parameters[1].IntValue;
            return (uint)(minute >= start && minute <= end ? 1 : 0);
        }

        private static uint ProcDayOfWeek(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            int dayOfWeek = (int)DateTime.Now.DayOfWeek;
            int targetDay = parameters[0].IntValue;
            return (uint)(dayOfWeek == targetDay ? 1 : 0);
        }

        private static uint ProcBefore(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcAfter(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcSetFlag(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcClrFlag(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCheckFlag(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcIsSabukOwner(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcIsSabukMember(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcIsGuildMaster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcHasGuild(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcIsAttackSabukGuild(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcRequestAttackSabuk(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcChangeNameColor(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            return 1;
        }

        private static uint ProcChangeDressColor(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcRepairMainDoor(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcRepairWall(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcIsSabukWarStarted(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcGiveCredit(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcTakeCredit(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcIncPkPoint(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDecPkPoint(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcClrPkPoint(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcDoUpgradeWeapon(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcTakeUpgradeWeapon(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcHasUpgradeWeapon(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcIsFirstLogin(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcClearFirstLogin(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcIsGroupMember(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcIsGroupLeader(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcEnterTopList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcSetSabukMaster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcCheckMapHum(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 3)
                return 0;

            return 0;
        }

        private static uint ProcCheckMapMon(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 3)
                return 0;

            return 0;
        }

        private static uint ProcMonGen(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 6)
                return 0;

            return 1;
        }

        private static uint ProcTargetMonGen(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 8)
                return 0;

            return 1;
        }

        private static uint ProcHumOnline(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 0;
        }

        private static uint ProcInputText(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 3)
                return 0;

            return 1;
        }

        private static uint ProcHasTeacher(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcHasMaster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcAddStudent(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDeleteStudent(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCanTakeStudent(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcIsMarried(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcRemoteCall(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            return 1;
        }

        private static uint ProcTakeEquipment(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcTakeEquipmentEx(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcAddTeacherCredit(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcValueOf(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDebugMode(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcHasStudent(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcSystemMsgEx(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcOpenPage(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcUpdateEquipment(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcShowMonster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcTransform(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcStrEqu(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            bool result = parameters[0].StringValue.Equals(parameters[1].StringValue, StringComparison.Ordinal);
            return (uint)(result ? 1 : 0);
        }

        private static uint ProcStrEquNoCase(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            bool result = parameters[0].StringValue.Equals(parameters[1].StringValue, StringComparison.OrdinalIgnoreCase);
            return (uint)(result ? 1 : 0);
        }

        private static uint ProcStrEquLength(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            bool result = parameters[0].StringValue.Length == parameters[1].IntValue;
            return (uint)(result ? 1 : 0);
        }

        private static uint ProcStrEquLengthNoCase(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            bool result = parameters[0].StringValue.Length == parameters[1].IntValue;
            return (uint)(result ? 1 : 0);
        }

        private static uint ProcSaveVarToDb(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcMultiCheckMapHum(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 3)
                return 0;

            return 0;
        }

        private static uint ProcChangeFontColor(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcGroupMove(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcMultiMapHumTeleport(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcMsgBox(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcTimerStart(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcTimerStop(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcTimerTimeout(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcAddDynamicNpc(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 7)
                return 0;

            if (parameters[0].IntValue < 0 || parameters[2].IntValue < 0 || parameters[3].IntValue < 0)
                return 0;

            uint ident = (uint)parameters[0].IntValue;
            string name = parameters[1].StringValue ?? string.Empty;
            uint viewId = (uint)parameters[2].IntValue;
            uint mapId = (uint)parameters[3].IntValue;
            uint x = (uint)Math.Max(0, parameters[4].IntValue);
            uint y = (uint)Math.Max(0, parameters[5].IntValue);

            string scriptKey = parameters[6].StringValue ?? string.Empty;
            scriptKey = scriptKey.Trim();
            if (scriptKey.StartsWith("@", StringComparison.Ordinal))
                scriptKey = scriptKey.Substring(1);
            int dot = scriptKey.IndexOf('.');
            if (dot > 0)
                scriptKey = scriptKey.Substring(0, dot);
            scriptKey = Path.GetFileNameWithoutExtension(scriptKey);

            bool ok = NpcManagerEx.Instance.AddDynamicNpc(ident, name, viewId, mapId, x, y, scriptKey);
            return ok ? 1u : 0u;
        }

        private static uint ProcRemoveDynamicNpc(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            if (parameters[0].IntValue < 0)
                return 0;

            uint ident = (uint)parameters[0].IntValue;
            bool ok = NpcManagerEx.Instance.RemoveDynamicNpc(ident);
            return ok ? 1u : 0u;
        }

        private static uint ProcSystemDelay(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcShowStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcGetStringListLines(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 0;
        }

        private static uint ProcHasTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 0;
        }

        private static uint ProcAddTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcRemoveTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcReloadTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcModifyTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCompleteTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCheckTaskStep(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 0;
        }

        private static uint ProcAddHp(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcReloadItemLimit(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcReloadItemScript(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcIsFirstGuildMaster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcFormatString(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCheckDateTime(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 0;
        }

        private static uint ProcAddStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDelStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcCheckStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 0;
        }

        private static uint ProcClearStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcBuildItem(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcDoMapScript(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcHasTracedItem(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 0;
        }

        private static uint ProcSendGuildSos(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 1;
        }

        private static uint ProcInSafeArea(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcInCityArea(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcInWarArea(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            return 0;
        }

        private static uint ProcTakeForgeRate(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        private static uint ProcAddForgeRate(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            return 1;
        }

        #endregion
    }
}
