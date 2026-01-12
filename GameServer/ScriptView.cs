using System;

namespace GameServer
{
    
    
    
    
    public class ScriptView
    {
        protected ScriptShell? _shell;
        protected byte[] _scriptPacket = Array.Empty<byte>();
        protected uint _param;
        protected uint _pageSize;

        
        
        
        public ScriptView(ScriptShell? shell = null)
        {
            _shell = shell;
            _param = 0;
            _pageSize = 0;
        }

        
        
        
        
        public virtual bool AppendWords(string words)
        {
            Console.WriteLine($"脚本视图添加文本: {words}");
            return true;
        }

        
        
        
        
        public bool AppendWordsEx(string format, params object[] args)
        {
            string words = string.Format(format, args);
            return AppendWords(words);
        }

        
        
        
        
        public virtual void SendPageToTarget(ScriptTarget target, uint param = 0)
        {
            Console.WriteLine($"发送页面到目标: {target.GetTargetName()}, 参数: {param}");
        }

        
        
        
        
        public virtual void SendClosePageToTarget(ScriptTarget target)
        {
            Console.WriteLine($"发送关闭页面到目标: {target.GetTargetName()}");
        }

        
        
        
        
        public virtual void ChangeShell(ScriptShell shell)
        {
            _shell = shell;
        }

        
        
        
        
        public void Clear()
        {
            _scriptPacket = Array.Empty<byte>();
            _param = 0;
            _pageSize = 0;
        }

        
        
        
        
        public byte[] GetPacket()
        {
            return _scriptPacket;
        }

        
        
        
        
        public uint GetParam()
        {
            return _param;
        }

        
        
        
        
        public uint GetSize()
        {
            return _pageSize;
        }

        
        
        
        public void SetPacket(byte[] packet)
        {
            _scriptPacket = packet;
        }

        
        
        
        public void SetParam(uint param)
        {
            _param = param;
        }

        
        
        
        public void SetPageSize(uint pageSize)
        {
            _pageSize = pageSize;
        }
    }

    
    
    
    
    public class ScriptPageView : ScriptView
    {
        
        
        
        public ScriptPageView(ScriptShell? shell = null) : base(shell)
        {
        }

        
        
        
        
        public override bool AppendWords(string words)
        {
            Console.WriteLine($"脚本页面视图添加文本: {words}");
            
            return true;
        }

        
        
        
        
        public override void SendPageToTarget(ScriptTarget target, uint param = 0)
        {
            Console.WriteLine($"脚本页面视图发送页面到目标: {target.GetTargetName()}, 参数: {param}");
            
        }

        
        
        
        
        public override void SendClosePageToTarget(ScriptTarget target)
        {
            Console.WriteLine($"脚本页面视图发送关闭页面到目标: {target.GetTargetName()}");
            
        }

        
        
        
        
        public override void ChangeShell(ScriptShell shell)
        {
            base.ChangeShell(shell);
            Console.WriteLine($"脚本页面视图更改shell");
        }
    }
}
