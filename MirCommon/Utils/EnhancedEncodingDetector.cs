using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ude;

namespace MirCommon.Utils
{
    public class SmartReader
    {
        private static bool _encodingProviderRegistered = false;

        
        
        
        private static void EnsureEncodingProvider()
        {
            if (!_encodingProviderRegistered)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _encodingProviderRegistered = true;
            }
        }

        
        
        
        public static Encoding DetectEncoding(string filePath, int sampleSize = 4096)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);

            EnsureEncodingProvider();

            
            var bomEncoding = DetectBomEncodingQuick(filePath);
            if (bomEncoding != null)
                return bomEncoding;

            
            byte[] buffer = new byte[sampleSize];

            using (var fs = File.OpenRead(filePath))
            {
                int bytesRead = fs.Read(buffer, 0, sampleSize);

                
                ICharsetDetector detector = new CharsetDetector();
                detector.Feed(buffer, 0, bytesRead);
                detector.DataEnd();

                if (detector.Charset != null && detector.Confidence > 0.5)
                {
                    string charset = detector.Charset.ToUpperInvariant();

                    
                    var encoding = MapCharsetToEncoding(charset);
                    if (encoding != null)
                    {
                        
                        
                        return PreferChineseEncodingIfSuspicious(filePath, buffer, bytesRead, encoding);
                    }
                }
            }

            
            return FallbackDetection(filePath);
        }

        
        
        
        private static Encoding DetectBomEncodingQuick(string filePath)
        {
            byte[] bom = new byte[4];

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead = fs.Read(bom, 0, 4);

                if (bytesRead >= 4)
                {
                    
                    if (bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
                        return Encoding.GetEncoding("UTF-32BE");

                    
                    if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
                        return Encoding.UTF32;
                }

                if (bytesRead >= 3)
                {
                    
                    if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                        return Encoding.UTF8;
                }

                if (bytesRead >= 2)
                {
                    
                    if (bom[0] == 0xFF && bom[1] == 0xFE)
                        return Encoding.Unicode;

                    
                    if (bom[0] == 0xFE && bom[1] == 0xFF)
                        return Encoding.BigEndianUnicode;
                }
            }

            return null;
        }

        
        
        
        private static Encoding MapCharsetToEncoding(string charset)
        {
            try
            {
                return charset.ToUpperInvariant() switch
                {
                    
                    "UTF-8" => Encoding.UTF8,
                    "UTF-8-BOM" => Encoding.UTF8,
                    "UTF-16LE" or "UTF-16" => Encoding.Unicode,
                    "UTF-16BE" => Encoding.BigEndianUnicode,
                    "UTF-32LE" or "UTF-32" => Encoding.UTF32,
                    "UTF-32BE" => Encoding.GetEncoding("UTF-32BE"),

                    
                    "GB2312" or "GBK" or "GB18030" => Encoding.GetEncoding("GB18030"), 
                    "BIG5" or "BIG5-HKSCS" => Encoding.GetEncoding("Big5"),

                    
                    "SHIFT_JIS" => Encoding.GetEncoding("Shift_JIS"),
                    "EUC-JP" => Encoding.GetEncoding("EUC-JP"),
                    "ISO-2022-JP" => Encoding.GetEncoding("ISO-2022-JP"),

                    
                    "EUC-KR" => Encoding.GetEncoding("EUC-KR"),
                    "ISO-2022-KR" => Encoding.GetEncoding("ISO-2022-KR"),

                    
                    "WINDOWS-1251" => Encoding.GetEncoding(1251),
                    "KOI8-R" => Encoding.GetEncoding("KOI8-R"),
                    "KOI8-U" => Encoding.GetEncoding("KOI8-U"),
                    "ISO-8859-5" => Encoding.GetEncoding("ISO-8859-5"),

                    
                    "WINDOWS-1252" => Encoding.GetEncoding(1252),
                    "ISO-8859-1" => Encoding.GetEncoding("ISO-8859-1"),
                    "ISO-8859-15" => Encoding.GetEncoding("ISO-8859-15"),

                    
                    "WINDOWS-1256" => Encoding.GetEncoding(1256), 
                    "WINDOWS-1255" => Encoding.GetEncoding(1255), 

                    
                    _ => Encoding.GetEncoding(charset)
                };
            }
            catch (ArgumentException)
            {
                
                return null;
            }
        }

        private static Encoding PreferChineseEncodingIfSuspicious(string filePath, byte[] buffer, int bytesRead, Encoding detected)
        {
            try
            {
                
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext is not (".ini" or ".txt" or ".csv"))
                    return detected;

                
                if (detected == Encoding.UTF8 || detected == Encoding.Unicode || detected == Encoding.BigEndianUnicode || detected == Encoding.UTF32)
                    return detected;

                
                
                
                bool isSingleByteSuspicious = detected.CodePage is 1251 or 1252 or 28591 or 28595;
                if (!isSingleByteSuspicious)
                    return detected;

                var gb = Encoding.GetEncoding("GB18030");

                
                int sampleLen = Math.Min(bytesRead, 4096);
                string sDetected = detected.GetString(buffer, 0, sampleLen);
                string sGb = gb.GetString(buffer, 0, sampleLen);

                int scoreDetected = ScoreDecodedText(sDetected);
                int scoreGb = ScoreDecodedText(sGb);

                if (scoreGb > scoreDetected + 20)
                    return gb;

                
                byte[] large = ReadSampleBytes(filePath, 64 * 1024);
                if (large.Length > sampleLen)
                {
                    string sDetected2 = detected.GetString(large, 0, large.Length);
                    string sGb2 = gb.GetString(large, 0, large.Length);

                    int scoreDetected2 = ScoreDecodedText(sDetected2);
                    int scoreGb2 = ScoreDecodedText(sGb2);

                    if (scoreGb2 > scoreDetected2 + 10)
                        return gb;
                }

                
                if (detected.CodePage is 1251 or 28595)
                {
                    try
                    {
                        var iso88595 = Encoding.GetEncoding("ISO-8859-5");

                        
                        string sIso = iso88595.GetString(buffer, 0, sampleLen);
                        int scoreIso = ScoreDecodedText(sIso);

                        if (scoreIso > scoreDetected)
                            return iso88595;
                    }
                    catch
                    {
                        
                    }
                }

                return detected;
            }
            catch
            {
                return detected;
            }
        }

        private static byte[] ReadSampleBytes(string filePath, int maxBytes)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                int len = (int)Math.Min(fs.Length, maxBytes);
                if (len <= 0)
                    return Array.Empty<byte>();

                byte[] buf = new byte[len];
                int read = fs.Read(buf, 0, len);
                if (read == len)
                    return buf;

                
                return buf.Take(read).ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private static int ScoreDecodedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return int.MinValue;

            int score = 0;
            int limit = Math.Min(text.Length, 300);

            for (int i = 0; i < limit; i++)
            {
                char c = text[i];

                if (c == '\uFFFD')
                {
                    score -= 20;
                    continue;
                }

                if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    
                    score += 5;
                    continue;
                }

                if (c >= 32 && c <= 126)
                {
                    
                    score += 1;
                    continue;
                }

                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                {
                    score -= 10;
                    continue;
                }

                
                if (c >= 0x0400 && c <= 0x04FF)
                {
                    score -= 1;
                }
            }

            return score;
        }

        
        
        
        private static Encoding FallbackDetection(string filePath)
        {
            byte[] buffer = new byte[8192]; 

            using (var fs = File.OpenRead(filePath))
            {
                int bytesRead = fs.Read(buffer, 0, buffer.Length);

                
                if (IsValidUtf8(buffer, bytesRead))
                    return Encoding.UTF8;

                
                var chineseEncodings = new[]
                {
                "GB18030", 
                "GBK",
                "GB2312"
            };

                foreach (var encodingName in chineseEncodings)
                {
                    try
                    {
                        var encoding = Encoding.GetEncoding(encodingName);
                        string test = encoding.GetString(buffer, 0, Math.Min(bytesRead, 1024));

                        
                        if (IsLikelyValidText(test, encodingName))
                            return encoding;
                    }
                    catch
                    {
                        
                    }
                }

                
                var commonEncodings = new[]
                {
                Encoding.Default,
                Encoding.GetEncoding(1252), 
                Encoding.GetEncoding("ISO-8859-1"),
                Encoding.GetEncoding("Windows-1251") 
            };

                foreach (var encoding in commonEncodings)
                {
                    try
                    {
                        string test = encoding.GetString(buffer, 0, Math.Min(bytesRead, 1024));

                        
                        if (!ContainsExcessiveControlChars(test))
                            return encoding;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            
            return Encoding.Default;
        }

        
        
        
        private static bool IsValidUtf8(byte[] buffer, int length)
        {
            try
            {
                string test = Encoding.UTF8.GetString(buffer, 0, length);

                
                if (test.Contains('\uFFFD'))
                    return false;

                
                foreach (char c in test)
                {
                    if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                    {
                        
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        private static bool IsLikelyValidText(string text, string encodingName)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            int validChars = 0;
            int totalChars = Math.Min(text.Length, 100); 

            for (int i = 0; i < totalChars; i++)
            {
                char c = text[i];

                
                if (c >= 32 && c <= 126 || c == '\r' || c == '\n' || c == '\t')
                {
                    validChars++;
                }
                
                else if (encodingName.StartsWith("GB") || encodingName == "Big5")
                {
                    if ((c >= 0x4E00 && c <= 0x9FFF) || 
                        (c >= 0x3400 && c <= 0x4DBF) || 
                        (c >= 0x20000 && c <= 0x2A6DF) || 
                        (c >= 0x2A700 && c <= 0x2B73F) || 
                        (c >= 0x2B740 && c <= 0x2B81F) || 
                        (c >= 0x2B820 && c <= 0x2CEAF))   
                    {
                        validChars++;
                    }
                }
            }

            
            return (double)validChars / totalChars > 0.7;
        }

        
        
        
        private static bool ContainsExcessiveControlChars(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            int controlChars = 0;
            int totalChars = Math.Min(text.Length, 100);

            for (int i = 0; i < totalChars; i++)
            {
                char c = text[i];

                
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                {
                    controlChars++;
                }
            }

            
            return (double)controlChars / totalChars > 0.1;
        }

        
        
        
        public static string ReadTextFile(string filePath)
        {
            var encoding = DetectEncoding(filePath);
            
            string content = File.ReadAllText(filePath, encoding);
            if (ContainsReplacementChars(content) && encoding.CodePage != Encoding.GetEncoding("ISO-8859-5").CodePage)
            {
                try
                {
                    var iso = Encoding.GetEncoding("ISO-8859-5");
                    string isoContent = File.ReadAllText(filePath, iso);
                    if (!ContainsReplacementChars(isoContent))
                        return isoContent;
                }
                catch { }
            }
            return content;
        }

        
        
        
        public static string[] ReadAllLines(string filePath)
        {
            var encoding = DetectEncoding(filePath);
            var lines = File.ReadAllLines(filePath, encoding);

            
            if (lines != null && lines.Length > 0 && lines.Any(l => ContainsReplacementChars(l)))
            {
                try
                {
                    var iso = Encoding.GetEncoding("ISO-8859-5");
                    var isoLines = File.ReadAllLines(filePath, iso);
                    if (isoLines != null && isoLines.Length > 0 && !isoLines.Any(l => ContainsReplacementChars(l)))
                        return isoLines;
                }
                catch { }
            }

            return lines;
        }

        
        
        
        public static IEnumerable<string> ReadLines(string filePath)
        {
            var encoding = DetectEncoding(filePath);

            using (var reader = new StreamReader(filePath, encoding))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        private static bool ContainsReplacementChars(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return s.Contains('\uFFFD');
        }
    }
}
