using System;
using System.Text;

namespace MirCommon.Utils
{
    
    
    
    
    public static class GameEncoding
    {
        
        
        
        public static readonly Encoding GBK;

        static GameEncoding()
        {
            
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            
            GBK = Encoding.GetEncoding("GBK");
        }
        
        
        
        
        
        
        public static byte[] GetBytes(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<byte>();
            return GBK.GetBytes(text);
        }
        
        
        
        
        
        
        public static string GetString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;
            return GBK.GetString(bytes).TrimEnd('\0');
        }
        
        
        
        
        
        
        
        
        public static string GetString(byte[] bytes, int index, int count)
        {
            if (bytes == null || bytes.Length == 0 || count == 0)
                return string.Empty;
            return GBK.GetString(bytes, index, count).TrimEnd('\0');
        }
    }
}
