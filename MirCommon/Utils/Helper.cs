using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MirCommon.Utils
{
    public class Helper
    {
        
        
        
        
        public static byte[] ConvertToFixedBytes(string text, int bytelength, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.GetEncoding("GBK");
            byte[] result = new byte[bytelength];

            if (string.IsNullOrEmpty(text))
                return result; 

            byte[] tempBytes = encoding.GetBytes(text);

            
            int copyLength = Math.Min(tempBytes.Length, bytelength);
            Buffer.BlockCopy(tempBytes, 0, result, 0, copyLength);

            return result;
        }

        
        
        
        public static bool TryHexToInt(string hexString, out int result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(hexString))
                return false;

            try
            {
                hexString = hexString.Trim();
                hexString = NormalizeHexString(hexString);

                result = Convert.ToInt32(hexString, 16);
                return true;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        private static string NormalizeHexString(string hexString)
        {
            
            hexString = hexString.ToUpperInvariant();

            
            if (hexString.StartsWith("0X"))
                return hexString.Substring(2);
            if (hexString.StartsWith("X"))
                return hexString.Substring(1);
            if (hexString.StartsWith("#"))
                return hexString.Substring(1);
            if (hexString.StartsWith("&H"))
                return hexString.Substring(2);

            return hexString;
        }

        public static bool BoolParser(string val)
        {
            val = val.Trim();
            if (!string.IsNullOrEmpty(val))
            {
                if (val.Equals("1")) return true;
                else if (val.Equals("0")) return false;
                else if (bool.TryParse(val, out bool res))
                {
                    return res;
                }
                return false;
            }
            return false;
        }
    }
}
