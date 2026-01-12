using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    
    
    public static class CxxScriptExecutor
    {
        public static bool Execute(HumanPlayer player, string pageRef, bool silent = false)
        {
            if (player == null || string.IsNullOrWhiteSpace(pageRef))
                return false;

            try
            {
                if (!TryResolveScriptPage(pageRef, out string scriptName, out string pageName, out var callParams))
                {
                    if (!silent)
                        LogManager.Default.Warning($"脚本页解析失败: '{pageRef}'");
                    return false;
                }

                var scriptObject = ScriptObjectMgr.Instance.GetScriptObject(scriptName);
                if (scriptObject == null)
                {
                    if (!silent)
                        LogManager.Default.Warning($"脚本对象不存在: {scriptName}, pageRef={pageRef}");
                    return false;
                }

                int startIndex = FindPageStart(scriptObject, pageName);
                if (startIndex < 0)
                {
                    if (!silent)
                        LogManager.Default.Warning($"脚本页面不存在: {scriptName}[{pageName}], pageRef={pageRef}");
                    return false;
                }

                var shell = new ScriptShell();
                ApplyCallParams(shell, player, callParams);

                ExecutePage(scriptObject, startIndex, shell, player);
                return true;
            }
            catch (Exception ex)
            {
                if (!silent)
                    LogManager.Default.Error($"执行脚本页异常: pageRef={pageRef}", exception: ex);
                return false;
            }
        }

        private static bool TryResolveScriptPage(string pageRef, out string scriptName, out string pageName, out List<string> callParams)
        {
            scriptName = string.Empty;
            pageName = string.Empty;
            callParams = new List<string>();

            string raw = pageRef.Trim();
            if (raw.Length == 0)
                return false;

            
            var segs = raw.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segs.Length == 0)
                return false;

            string pageSeg = segs[0].Trim();
            if (pageSeg.Length == 0)
                return false;

            
            if (pageSeg.StartsWith("@", StringComparison.Ordinal))
                pageSeg = pageSeg.Substring(1);

            int dot = pageSeg.IndexOf('.');
            if (dot <= 0 || dot == pageSeg.Length - 1)
                return false;

            scriptName = pageSeg.Substring(0, dot).Trim();
            string pagePart = pageSeg.Substring(dot + 1).Trim();
            if (string.IsNullOrWhiteSpace(scriptName) || string.IsNullOrWhiteSpace(pagePart))
                return false;

            
            pageName = pagePart.StartsWith("@", StringComparison.Ordinal) ? pagePart : "@" + pagePart;

            
            for (int i = 1; i < segs.Length; i++)
            {
                string p = segs[i].Trim();
                if (p.Length == 0)
                    continue;
                callParams.Add(p);
            }

            return true;
        }

        private static void ApplyCallParams(ScriptShell shell, HumanPlayer player, List<string> callParams)
        {
            if (callParams.Count == 0)
                return;

            for (int i = 0; i < callParams.Count; i++)
            {
                string v = callParams[i];
                
                if (v.StartsWith("$", StringComparison.Ordinal))
                {
                    string varName = v.Substring(1);
                    v = ResolveTargetVarString(player, varName);
                }

                shell.SetVariable($"_param{i}", v);
                shell.SetVariable($"param{i}", v);
            }
        }

        private static int FindPageStart(ScriptObject scriptObject, string pageName)
        {
            if (scriptObject.Lines == null || scriptObject.Lines.Count == 0)
                return -1;

            for (int i = 0; i < scriptObject.Lines.Count; i++)
            {
                if (TryParsePageHeader(scriptObject.Lines[i], out var header) &&
                    string.Equals(header, pageName, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }

            return -1;
        }

        private static void ExecutePage(ScriptObject scriptObject, int startIndex, ScriptShell shell, HumanPlayer target)
        {
            for (int i = startIndex; i < scriptObject.Lines.Count; i++)
            {
                string raw = scriptObject.Lines[i];
                string line = raw.Trim();

                if (TryParsePageHeader(line, out _))
                    break;

                if (string.IsNullOrEmpty(line))
                    continue;

                
                if (line.StartsWith(";") || line.StartsWith("//"))
                    continue;

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    if (line.Equals("#if", StringComparison.OrdinalIgnoreCase))
                    {
                        i = ExecuteIfBlock(scriptObject, i, shell, target);
                        continue;
                    }

                    if (line.Equals("#act", StringComparison.OrdinalIgnoreCase))
                    {
                        int next = ExecuteActBlock(scriptObject, i + 1, shell, target);
                        i = next - 1;
                        if (shell.GetExecuteResult() != ScriptShell.ExecuteResult.Ok)
                            break;
                        continue;
                    }

                    
                    continue;
                }

                ExecuteCommandLine(line, shell, target);
                if (shell.GetExecuteResult() != ScriptShell.ExecuteResult.Ok)
                    break;
            }
        }

        
        
        
        
        private static int ExecuteActBlock(ScriptObject scriptObject, int startIndex, ScriptShell shell, HumanPlayer target)
        {
            for (int i = startIndex; i < scriptObject.Lines.Count; i++)
            {
                string line = scriptObject.Lines[i].Trim();
                if (TryParsePageHeader(line, out _))
                    return i;
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("//"))
                    continue;
                if (line.StartsWith("#", StringComparison.Ordinal))
                    return i;

                ExecuteCommandLine(line, shell, target);
                if (shell.GetExecuteResult() != ScriptShell.ExecuteResult.Ok)
                    return scriptObject.Lines.Count;
            }

            return scriptObject.Lines.Count;
        }

        
        
        
        private static int ExecuteIfBlock(ScriptObject scriptObject, int ifLineIndex, ScriptShell shell, HumanPlayer target)
        {
            int i = ifLineIndex + 1;

            var branches = new List<IfBranch>();
            var cur = new IfBranch(IfBranchKind.If);
            bool inAct = false;

            for (; i < scriptObject.Lines.Count; i++)
            {
                string line = scriptObject.Lines[i].Trim();

                if (TryParsePageHeader(line, out _))
                    break;

                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("//"))
                    continue;

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    if (line.Equals("#act", StringComparison.OrdinalIgnoreCase))
                    {
                        inAct = true;
                        continue;
                    }

                    if (line.Equals("#elseif", StringComparison.OrdinalIgnoreCase))
                    {
                        branches.Add(cur);
                        cur = new IfBranch(IfBranchKind.ElseIf);
                        inAct = false;
                        continue;
                    }

                    if (line.Equals("#elseact", StringComparison.OrdinalIgnoreCase) || line.Equals("#else", StringComparison.OrdinalIgnoreCase))
                    {
                        branches.Add(cur);
                        cur = new IfBranch(IfBranchKind.Else);
                        inAct = true;
                        continue;
                    }

                    if (line.Equals("#end", StringComparison.OrdinalIgnoreCase))
                    {
                        branches.Add(cur);
                        break;
                    }

                    
                    continue;
                }

                if (!inAct)
                    cur.Conditions.Add(line);
                else
                    cur.Actions.Add(line);
            }

            
            foreach (var b in branches)
            {
                bool take = b.Kind == IfBranchKind.Else;
                if (!take)
                {
                    take = true;
                    foreach (var cond in b.Conditions)
                    {
                        uint r = ExecuteCommandLine(cond, shell, target);
                        if (r == 0)
                        {
                            take = false;
                            break;
                        }
                        if (shell.GetExecuteResult() != ScriptShell.ExecuteResult.Ok)
                            break;
                    }
                }

                if (!take)
                    continue;

                foreach (var act in b.Actions)
                {
                    ExecuteCommandLine(act, shell, target);
                    if (shell.GetExecuteResult() != ScriptShell.ExecuteResult.Ok)
                        return i;
                }

                
                return i;
            }

            return i;
        }

        private static uint ExecuteCommandLine(string line, ScriptShell shell, HumanPlayer target)
        {
            try
            {
                var tokens = SplitTokens(line);
                if (tokens.Count == 0)
                    return 0;

                string cmd = tokens[0];
                var proc = CommandManager.Instance.GetCommandProc(cmd);
                if (proc == null)
                {
                    LogManager.Default.Debug($"脚本未知命令: {cmd}");
                    return 0;
                }

                var ps = new ScriptParam[Math.Max(0, tokens.Count - 1)];
                for (int i = 1; i < tokens.Count; i++)
                {
                    string resolved = ResolveToken(shell, target, tokens[i]);
                    ps[i - 1] = new ScriptParam
                    {
                        StringValue = resolved,
                        IntValue = TryParseInt(resolved)
                    };
                }

                bool cont = true;
                uint result = proc(shell, target, null, ps, (uint)ps.Length, ref cont);
                shell.SetVariable("_return", result.ToString(CultureInfo.InvariantCulture));
                return result;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"执行脚本命令异常: {line}", exception: ex);
                return 0;
            }
        }

        private static string ResolveToken(ScriptShell shell, HumanPlayer target, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "0";

            string t = token.Trim();
            if (!t.StartsWith("$", StringComparison.Ordinal))
                return t;

            string varName = t.Substring(1);
            var shellVal = shell.GetVariableValue(varName);
            if (!string.IsNullOrEmpty(shellVal))
                return shellVal;

            return ResolveTargetVarString(target, varName);
        }

        private static string ResolveTargetVarString(HumanPlayer target, string varName)
        {
            if (target == null || string.IsNullOrWhiteSpace(varName))
                return "0";

            string key = varName.Trim().TrimStart('$').ToLowerInvariant();
            return key switch
            {
                "username" => target.Name ?? string.Empty,
                "level" => target.Level.ToString(CultureInfo.InvariantCulture),
                "gold" => target.Gold.ToString(CultureInfo.InvariantCulture),
                "hp" => target.CurrentHP.ToString(CultureInfo.InvariantCulture),
                "mp" => target.CurrentMP.ToString(CultureInfo.InvariantCulture),
                "maxhp" => target.MaxHP.ToString(CultureInfo.InvariantCulture),
                "maxmp" => target.MaxMP.ToString(CultureInfo.InvariantCulture),
                "expfactor" => target.GetExpFactor100().ToString(CultureInfo.InvariantCulture),
                _ => "0"
            };
        }

        private static int TryParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            string s = value.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
                    return hex;
            }

            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        }

        private static bool TryParsePageHeader(string line, out string header)
        {
            header = string.Empty;
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string trimmed = line.Trim();
            if (trimmed.Length < 3 || trimmed[0] != '[' || trimmed[^1] != ']')
                return false;

            header = trimmed.Substring(1, trimmed.Length - 2).Trim();
            return header.Length > 0;
        }

        private static List<string> SplitTokens(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(line))
                return result;

            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

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

        private enum IfBranchKind
        {
            If = 0,
            ElseIf = 1,
            Else = 2
        }

        private sealed class IfBranch
        {
            public IfBranchKind Kind { get; }
            public List<string> Conditions { get; } = new();
            public List<string> Actions { get; } = new();

            public IfBranch(IfBranchKind kind)
            {
                Kind = kind;
            }
        }
    }
}

