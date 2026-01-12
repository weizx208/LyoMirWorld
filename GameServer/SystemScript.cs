using System;
using System.Collections.Generic;

namespace GameServer
{
    
    
    
    
    public class SystemScript : ScriptShell
    {
        private static SystemScript? _instance;
        public static SystemScript Instance => _instance ??= new SystemScript();

        private ScriptObject? _scriptObject;

        
        
        
        private SystemScript()
        {
            _scriptObject = null;
        }

        
        
        
        
        public bool Init(ScriptObject? scriptObject)
        {
            _scriptObject = scriptObject;
            
            if (_scriptObject != null)
            {
                Console.WriteLine($"系统脚本初始化成功: {_scriptObject.Name}");
                return true;
            }
            else
            {
                Console.WriteLine("系统脚本初始化: 使用空脚本对象");
                return true;
            }
        }

        
        
        
        public ScriptObject? GetScriptObject()
        {
            return _scriptObject;
        }

        
        
        
        public void ExecuteSystemScript(string scriptName, ScriptTarget? target = null)
        {
            if (_scriptObject == null)
            {
                
                var scriptObj = ScriptObjectMgr.Instance.GetScriptObject(scriptName);
                if (scriptObj != null)
                {
                    _scriptObject = scriptObj;
                }
                else
                {
                    Console.WriteLine($"系统脚本 {scriptName} 不存在");
                    return;
                }
            }

            Console.WriteLine($"执行系统脚本: {scriptName}");
            
            
            if (target != null)
            {
                Execute(target, _scriptObject);
            }
            else
            {
                
                _scriptObject.Execute();
            }
        }

        
        
        
        
        public void ExecuteLoginScript(ScriptTarget target)
        {
            ExecuteSystemScript("system.login", target);
        }

        
        
        
        
        public void ExecuteLevelUpScript(ScriptTarget target)
        {
            ExecuteSystemScript("system.levelup", target);
        }

        
        
        
        
        public void ExecuteLogoutScript(ScriptTarget target)
        {
            ExecuteSystemScript("system.logout", target);
        }

        
        
        
        public void Reload()
        {
            if (_scriptObject != null)
            {
                _scriptObject.Reload();
                Console.WriteLine($"系统脚本已重新加载: {_scriptObject.Name}");
            }
        }
    }

    
    
    
    public class ScriptShell
    {
        protected readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);
        protected ScriptObject? _currentScriptObject;
        protected ExecuteResult _executeResult = ExecuteResult.Ok;
        protected string _resultValue = string.Empty;

        
        
        
        
        public enum ExecuteResult
        {
            Ok,
            Close,
            Return,
            Break
        }

        
        
        
        public ScriptShell()
        {
            _currentScriptObject = null;
            InitializeDefaultVariables();
        }

        
        
        
        private void InitializeDefaultVariables()
        {
            
            _variables["_return"] = "0";
            _variables["_p1"] = "0";
            _variables["_p2"] = "0";
            _variables["_p3"] = "0";
            _variables["_p4"] = "0";
            _variables["returnvalue"] = "0";
        }

        
        
        
        
        public bool Execute(ScriptTarget target, ScriptObject scriptObject)
        {
            if (scriptObject == null)
                return false;

            _currentScriptObject = scriptObject;
            _executeResult = ExecuteResult.Ok;
            
            Console.WriteLine($"脚本执行开始: {scriptObject.Name}, 目标: {target.GetTargetName()}");

            try
            {
                
                
                scriptObject.Execute();
                
                
                SetExecuteResult(ExecuteResult.Ok, "0");
                
                Console.WriteLine($"脚本执行完成: {scriptObject.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"脚本执行异常 {scriptObject.Name}: {ex.Message}");
                SetExecuteResult(ExecuteResult.Close, "0");
                return false;
            }
        }

        
        
        
        public bool Execute(ScriptTarget target, string pageName)
        {
            if (_currentScriptObject == null)
                return false;

            Console.WriteLine($"执行脚本页面: {pageName}, 目标: {target.GetTargetName()}");
            
            
            var lines = _currentScriptObject.Lines;
            bool inTargetPage = false;
            
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string page = line.Substring(1, line.Length - 2).Trim();
                    inTargetPage = page.Equals(pageName, StringComparison.OrdinalIgnoreCase);
                }
                else if (inTargetPage && !string.IsNullOrEmpty(line))
                {
                    
                    ExecuteCommand(line, target);
                }
            }
            
            return true;
        }

        
        
        
        
        private void ExecuteCommand(string command, ScriptTarget target)
        {
            if (string.IsNullOrEmpty(command))
                return;

            
            var parsedCommand = ParseCommand(command);
            if (parsedCommand == null)
                return;

            
            var proc = CommandManager.Instance.GetCommandProc(parsedCommand.CommandName);
            if (proc == null)
            {
                Console.WriteLine($"未知命令: {parsedCommand.CommandName}");
                return;
            }

            
            var scriptParams = new ScriptParam[parsedCommand.Parameters.Count];
            for (int i = 0; i < parsedCommand.Parameters.Count; i++)
            {
                scriptParams[i] = new ScriptParam
                {
                    StringValue = parsedCommand.Parameters[i],
                    IntValue = TryParseInt(parsedCommand.Parameters[i])
                };
            }

            
            bool continueExecution = true;
            try
            {
                uint result = proc(this, target, null, scriptParams, (uint)parsedCommand.Parameters.Count, ref continueExecution);
                Console.WriteLine($"命令执行结果: {parsedCommand.CommandName} = {result}");
                
                
                SetVariable("_return", result.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"命令执行异常 {parsedCommand.CommandName}: {ex.Message}");
            }
        }

        
        
        
        
        private ParsedCommand? ParseCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return null;

            command = command.Trim();
            if (string.IsNullOrEmpty(command))
                return null;

            
            var parts = new List<string>();
            bool inQuotes = false;
            string currentPart = string.Empty;

            for (int i = 0; i < command.Length; i++)
            {
                char c = command[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    if (!inQuotes)
                    {
                        parts.Add(currentPart);
                        currentPart = string.Empty;
                    }
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (!string.IsNullOrEmpty(currentPart))
                    {
                        parts.Add(currentPart);
                        currentPart = string.Empty;
                    }
                }
                else
                {
                    currentPart += c;
                }
            }

            if (!string.IsNullOrEmpty(currentPart))
            {
                parts.Add(currentPart);
            }

            if (parts.Count == 0)
                return null;

            var parsedCommand = new ParsedCommand
            {
                CommandName = parts[0].ToUpper(),
                Parameters = new List<string>()
            };

            for (int i = 1; i < parts.Count; i++)
            {
                parsedCommand.Parameters.Add(parts[i]);
            }

            return parsedCommand;
        }

        
        
        
        private int TryParseInt(string value)
        {
            if (int.TryParse(value, out int result))
                return result;
            return 0;
        }

        
        
        
        private class ParsedCommand
        {
            public string CommandName { get; set; } = string.Empty;
            public List<string> Parameters { get; set; } = new List<string>();
        }

        
        
        
        
        public string? GetVariableValue(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return null;

            return _variables.TryGetValue(variableName, out var value) ? value : null;
        }

        
        
        
        public void SetVariable(string variableName, string value)
        {
            if (string.IsNullOrEmpty(variableName))
                return;

            _variables[variableName] = value;
        }

        
        
        
        
        public void SetExecuteResult(ExecuteResult result, string value = "0")
        {
            _executeResult = result;
            _resultValue = value;
            SetVariable("returnvalue", value);
        }

        
        
        
        public ExecuteResult GetExecuteResult()
        {
            return _executeResult;
        }

        
        
        
        public string GetExecuteResultValue()
        {
            return _resultValue;
        }

        
        
        
        public ScriptObject? GetCurrentScriptObject()
        {
            return _currentScriptObject;
        }

        
        
        
        public Dictionary<string, string> GetAllVariables()
        {
            return new Dictionary<string, string>(_variables);
        }

        
        
        
        public void ClearVariables()
        {
            _variables.Clear();
            InitializeDefaultVariables();
        }
    }

    
    
    
    public interface ScriptTarget
    {
        
        
        
        string GetTargetName();
        
        
        
        
        uint GetTargetId();
        
        
        
        
        void ExecuteScriptAction(string action, params string[] parameters);
    }

    
    
    
    public class BaseScriptTarget : ScriptTarget
    {
        private readonly string _name;
        private readonly uint _id;

        public BaseScriptTarget(string name, uint id)
        {
            _name = name;
            _id = id;
        }

        public string GetTargetName()
        {
            return _name;
        }

        public uint GetTargetId()
        {
            return _id;
        }

        public void ExecuteScriptAction(string action, params string[] parameters)
        {
            Console.WriteLine($"脚本动作: {action}, 参数: {string.Join(", ", parameters)}");
            
        }
    }
}
