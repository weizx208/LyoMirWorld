using System;
using System.Data;
using System.Text;
using MirCommon.Utils;

namespace MirCommon.Database
{
    
    
    
    public abstract class BaseDatabase : IDatabase
    {
        protected IDbConnection? _connection;
        protected readonly string _connectionString;
        
        protected BaseDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        
        
        
        protected abstract IDbConnection CreateConnection();
        
        
        
        
        protected abstract IDbDataParameter CreateParameter(string name, object value);
        
        public virtual SERVER_ERROR OpenDataBase()
        {
            try
            {
                _connection = CreateConnection();
                _connection.Open();
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception)
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CheckAccount(string account, string password)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM TBL_ACCOUNT WHERE ACCOUNT = @account AND PASSWORD = @password";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@password", password));
                
                var result = cmd.ExecuteScalar();
                return (result != null && Convert.ToInt32(result) > 0) ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CheckAccountExist(string account)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM TBL_ACCOUNT WHERE ACCOUNT = @account";
                cmd.Parameters.Add(CreateParameter("@account", account));
                
                var result = cmd.ExecuteScalar();
                return (result != null && Convert.ToInt32(result) > 0) ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateAccount(string account, string password, string name, string birthday,
                                                 string q1, string a1, string q2, string a2, string email,
                                                 string phoneNumber, string mobilePhoneNumber, string idCard)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"INSERT INTO TBL_ACCOUNT (ACCOUNT, PASSWORD, NAME, BIRTHDAY, 
                                  Q1, A1, Q2, A2, EMAIL, PHONENUMBER, MOBILEPHONENUMBER, IDCARD)
                                  VALUES (@account, @password, @name, @birthday, @q1, @a1, @q2, @a2, @email, @phone, @mobile, @idcard)";
                
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@password", password));
                cmd.Parameters.Add(CreateParameter("@name", name));
                cmd.Parameters.Add(CreateParameter("@birthday", birthday));
                cmd.Parameters.Add(CreateParameter("@q1", q1));
                cmd.Parameters.Add(CreateParameter("@a1", a1));
                cmd.Parameters.Add(CreateParameter("@q2", q2));
                cmd.Parameters.Add(CreateParameter("@a2", a2));
                cmd.Parameters.Add(CreateParameter("@email", email));
                cmd.Parameters.Add(CreateParameter("@phone", phoneNumber));
                cmd.Parameters.Add(CreateParameter("@mobile", mobilePhoneNumber));
                cmd.Parameters.Add(CreateParameter("@idcard", idCard));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR ChangePassword(string account, string oldPassword, string newPassword)
        {
            try
            {
                
                if (CheckAccount(account, oldPassword) != SERVER_ERROR.SE_OK)
                    return SERVER_ERROR.SE_FAIL;
                
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "UPDATE TBL_ACCOUNT SET PASSWORD = @newPassword WHERE ACCOUNT = @account";
                cmd.Parameters.Add(CreateParameter("@newPassword", newPassword));
                cmd.Parameters.Add(CreateParameter("@account", account));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR GetCharList(string account, string serverName, out string charListData)
        {
            charListData = "";
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT NAME, CLASS, HAIR, VLEVEL, SEX, ODATE 
                                  FROM TBL_CHARACTER_INFO 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND DELFLAG = 0
                                  ORDER BY ODATE DESC";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                
                using var reader = cmd.ExecuteReader();
                var result = new StringBuilder();
                
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    byte job = reader.GetByte(1);
                    byte hair = reader.GetByte(2);
                    ushort level = (ushort)reader.GetInt16(3);
                    byte sex = reader.GetByte(4);
                    DateTime odate = reader.GetDateTime(5);
                    
                    
                    
                    
                    
                    if (result.Length == 0)
                    {
                        
                        result.Append($"*{name}/{job}/{hair}/{level}/{sex}/");
                    }
                    else
                    {
                        result.Append($"{name}/{job}/{hair}/{level}/{sex}/");
                    }
                }
                
                charListData = result.ToString();
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"GetCharList失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR GetDelCharList(string account, string serverName, out string delCharListData)
        {
            delCharListData = "";
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT NAME, CLASS, SEX, VLEVEL, HAIR, DELDATE 
                                  FROM TBL_CHARACTER_INFO 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND DELFLAG = 1";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                
                using var reader = cmd.ExecuteReader();
                var result = new StringBuilder();
                
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    byte job = reader.GetByte(1);
                    byte sex = reader.GetByte(2);
                    ushort level = (ushort)reader.GetInt16(3);
                    byte hair = reader.GetByte(4);
                    DateTime deldate = reader.GetDateTime(5);
                    
                    result.Append($"{name}/{job}/{sex}/{level}/{hair}/{deldate:yyyy-MM-dd HH:mm:ss}/");
                }
                
                delCharListData = result.ToString();
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex)
        {
            
            return CreateCharacter(account, serverName, name, job, hair, sex, 0);
        }
        
        public virtual SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex, byte level)
        {
            try
            {
                
                using var checkCmd = _connection!.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM TBL_CHARACTER_INFO WHERE SERVER = @server AND NAME = @name";
                checkCmd.Parameters.Add(CreateParameter("@server", serverName));
                checkCmd.Parameters.Add(CreateParameter("@name", name));
                
                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    return SERVER_ERROR.SE_SELCHAR_CHAREXIST; 
                
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"INSERT INTO TBL_CHARACTER_INFO (ACCOUNT, SERVER, NAME, CLASS, SEX, VLEVEL, HAIR, ODATE, DELFLAG) 
                                  VALUES (@account, @server, @name, @job, @sex, @level, @hair, @odate, @delflag)";
                
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                cmd.Parameters.Add(CreateParameter("@job", job));
                cmd.Parameters.Add(CreateParameter("@sex", sex));
                cmd.Parameters.Add(CreateParameter("@level", level));
                cmd.Parameters.Add(CreateParameter("@hair", hair));
                cmd.Parameters.Add(CreateParameter("@odate", DateTime.Now));
                cmd.Parameters.Add(CreateParameter("@delflag", 0)); 
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateCharacter(CREATECHARDESC desc)
        {
            return CreateCharacter(desc.szAccount, desc.szServer, desc.szName, desc.btClass, desc.btHair, desc.btSex, desc.btLevel);
        }
        
        public virtual SERVER_ERROR DelCharacter(string account, string serverName, string name)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_INFO SET DELFLAG = 1, DELDATE = CURRENT_TIMESTAMP 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND NAME = @name";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR RestoreCharacter(string account, string serverName, string name)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_INFO SET DELFLAG = 0 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND NAME = @name";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR GetCharDBInfo(string account, string serverName, string name, out byte[] charData)
        {
            charData = Array.Empty<byte>();
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT 
                    ID, CLASS, SEX, VLEVEL, MAPNAME, POSX, POSY, HAIR,
                    CUREXP, HP, MP, MAXHP, MAXMP, MINDC, MAXDC,
                    MINMC, MAXMC, MINSC, MAXSC, MINAC, MAXAC,
                    MINMAC, MAXMAC, WEIGHT, HANDWEIGHT, BODYWEIGHT,
                    GOLD, MAPID, YUANBAO, FLAG1, FLAG2, FLAG3, FLAG4, GUILDNAME, FORGEPOINT, 
                    PROP1, PROP2, PROP3, PROP4, PROP5, PROP6, PROP7, PROP8
                    FROM TBL_CHARACTER_INFO 
                    WHERE ACCOUNT = @account AND SERVER = @server AND NAME = @name";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    
                    
                    
                    
                    var charDbInfo = new CHARDBINFO
                    {
                        
                        dwClientKey = 0, 
                        szName = name,
                        dwDBId = (uint)reader.GetInt32(0),
                        mapid = (uint)reader.GetInt32(27), 
                        x = (ushort)reader.GetInt16(5), 
                        y = (ushort)reader.GetInt16(6), 
                        dwGold = (uint)reader.GetInt32(26), 
                        dwYuanbao = (uint)reader.GetInt32(28), 
                        dwCurExp = (uint)reader.GetInt32(8), 
                        wLevel = (ushort)reader.GetInt16(3), 
                        btClass = reader.GetByte(1), 
                        btHair = reader.GetByte(7), 
                        btSex = reader.GetByte(2), 
                        flag = 0, 
                        hp = (ushort)reader.GetInt16(9), 
                        mp = (ushort)reader.GetInt16(10), 
                        maxhp = (ushort)reader.GetInt16(11), 
                        maxmp = (ushort)reader.GetInt16(12), 
                        mindc = reader.GetByte(13), 
                        maxdc = reader.GetByte(14), 
                        minmc = reader.GetByte(15), 
                        maxmc = reader.GetByte(16), 
                        minsc = reader.GetByte(17), 
                        maxsc = reader.GetByte(18), 
                        minac = reader.GetByte(19), 
                        maxac = reader.GetByte(20), 
                        minmac = reader.GetByte(21), 
                        maxmac = reader.GetByte(22), 
                        weight = (ushort)reader.GetInt16(23), 
                        handweight = (byte)reader.GetInt16(24), 
                        bodyweight = (byte)reader.GetInt16(25), 
                        dwForgePoint = (uint)reader.GetInt32(34), 
                        dwProp = new uint[8] {
                            (uint)reader.GetInt32(35), 
                            (uint)reader.GetInt32(36), 
                            (uint)reader.GetInt32(37), 
                            (uint)reader.GetInt32(38), 
                            (uint)reader.GetInt32(39), 
                            (uint)reader.GetInt32(40), 
                            (uint)reader.GetInt32(41), 
                            (uint)reader.GetInt32(42)  
                        },
                        dwFlag = new uint[4] {
                            (uint)reader.GetInt32(29), 
                            (uint)reader.GetInt32(30), 
                            (uint)reader.GetInt32(31), 
                            (uint)reader.GetInt32(32)  
                        },
                        szStartPoint = reader.GetString(4), 
                        szGuildName = reader.IsDBNull(33) ? "" : reader.GetString(33) 
                    };
                    
                    charData = charDbInfo.ToBytes();
                    return SERVER_ERROR.SE_OK;
                }
                
                return SERVER_ERROR.SE_FAIL;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"GetCharDBInfo失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR PutCharDBInfo(string account, string serverName, string name, byte[] charData)
        {
            try
            {
                if (_connection == null)
                    return SERVER_ERROR.SE_FAIL;

                if (charData == null || charData.Length < CHARDBINFO.Size)
                    return SERVER_ERROR.SE_FAIL;

                var info = CHARDBINFO.FromBytes(charData);
                if (info.dwDBId == 0)
                    return SERVER_ERROR.SE_FAIL;

                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
UPDATE TBL_CHARACTER_INFO SET
    NAME=@name,
    CLASS=@class,
    SEX=@sex,
    VLEVEL=@level,
    MAPNAME=@mapname,
    POSX=@x,
    POSY=@y,
    HAIR=@hair,
    CUREXP=@exp,
    HP=@hp,
    MP=@mp,
    MAXHP=@maxhp,
    MAXMP=@maxmp,
    MINDC=@mindc,
    MAXDC=@maxdc,
    MINMC=@minmc,
    MAXMC=@maxmc,
    MINSC=@minsc,
    MAXSC=@maxsc,
    MINAC=@minac,
    MAXAC=@maxac,
    MINMAC=@minmac,
    MAXMAC=@maxmac,
    WEIGHT=@weight,
    HANDWEIGHT=@handweight,
    BODYWEIGHT=@bodyweight,
    GOLD=@gold,
    MAPID=@mapid,
    YUANBAO=@yuanbao,
    FLAG1=@flag1,
    FLAG2=@flag2,
    FLAG3=@flag3,
    FLAG4=@flag4,
    GUILDNAME=@guildname,
    FORGEPOINT=@forgepoint,
    PROP1=@prop1,
    PROP2=@prop2,
    PROP3=@prop3,
    PROP4=@prop4,
    PROP5=@prop5,
    PROP6=@prop6,
    PROP7=@prop7,
    PROP8=@prop8
WHERE ID=@id";

                cmd.Parameters.Add(CreateParameter("@id", (int)info.dwDBId));
                cmd.Parameters.Add(CreateParameter("@name", info.szName ?? string.Empty));
                cmd.Parameters.Add(CreateParameter("@class", (int)info.btClass));
                cmd.Parameters.Add(CreateParameter("@sex", (int)info.btSex));
                cmd.Parameters.Add(CreateParameter("@level", (int)info.wLevel));
                cmd.Parameters.Add(CreateParameter("@mapname", info.szStartPoint ?? "0"));
                cmd.Parameters.Add(CreateParameter("@x", (int)info.x));
                cmd.Parameters.Add(CreateParameter("@y", (int)info.y));
                cmd.Parameters.Add(CreateParameter("@hair", (int)info.btHair));
                cmd.Parameters.Add(CreateParameter("@exp", (long)info.dwCurExp));
                cmd.Parameters.Add(CreateParameter("@hp", (int)info.hp));
                cmd.Parameters.Add(CreateParameter("@mp", (int)info.mp));
                cmd.Parameters.Add(CreateParameter("@maxhp", (int)info.maxhp));
                cmd.Parameters.Add(CreateParameter("@maxmp", (int)info.maxmp));

                cmd.Parameters.Add(CreateParameter("@mindc", (int)info.mindc));
                cmd.Parameters.Add(CreateParameter("@maxdc", (int)info.maxdc));
                cmd.Parameters.Add(CreateParameter("@minmc", (int)info.minmc));
                cmd.Parameters.Add(CreateParameter("@maxmc", (int)info.maxmc));
                cmd.Parameters.Add(CreateParameter("@minsc", (int)info.minsc));
                cmd.Parameters.Add(CreateParameter("@maxsc", (int)info.maxsc));
                cmd.Parameters.Add(CreateParameter("@minac", (int)info.minac));
                cmd.Parameters.Add(CreateParameter("@maxac", (int)info.maxac));
                cmd.Parameters.Add(CreateParameter("@minmac", (int)info.minmac));
                cmd.Parameters.Add(CreateParameter("@maxmac", (int)info.maxmac));

                cmd.Parameters.Add(CreateParameter("@weight", (int)info.weight));
                cmd.Parameters.Add(CreateParameter("@handweight", (int)info.handweight));
                cmd.Parameters.Add(CreateParameter("@bodyweight", (int)info.bodyweight));
                cmd.Parameters.Add(CreateParameter("@gold", (long)info.dwGold));
                cmd.Parameters.Add(CreateParameter("@mapid", (int)info.mapid));
                cmd.Parameters.Add(CreateParameter("@yuanbao", (long)info.dwYuanbao));

                uint f0 = (info.dwFlag != null && info.dwFlag.Length > 0) ? info.dwFlag[0] : 0;
                uint f1 = (info.dwFlag != null && info.dwFlag.Length > 1) ? info.dwFlag[1] : 0;
                uint f2 = (info.dwFlag != null && info.dwFlag.Length > 2) ? info.dwFlag[2] : 0;
                uint f3 = (info.dwFlag != null && info.dwFlag.Length > 3) ? info.dwFlag[3] : 0;

                cmd.Parameters.Add(CreateParameter("@flag1", (long)f0));
                cmd.Parameters.Add(CreateParameter("@flag2", (long)f1));
                cmd.Parameters.Add(CreateParameter("@flag3", (long)f2));
                cmd.Parameters.Add(CreateParameter("@flag4", (long)f3));

                cmd.Parameters.Add(CreateParameter("@guildname", info.szGuildName ?? string.Empty));
                cmd.Parameters.Add(CreateParameter("@forgepoint", (long)info.dwForgePoint));

                uint p1 = (info.dwProp != null && info.dwProp.Length > 0) ? info.dwProp[0] : 0;
                uint p2 = (info.dwProp != null && info.dwProp.Length > 1) ? info.dwProp[1] : 0;
                uint p3 = (info.dwProp != null && info.dwProp.Length > 2) ? info.dwProp[2] : 0;
                uint p4 = (info.dwProp != null && info.dwProp.Length > 3) ? info.dwProp[3] : 0;
                uint p5 = (info.dwProp != null && info.dwProp.Length > 4) ? info.dwProp[4] : 0;
                uint p6 = (info.dwProp != null && info.dwProp.Length > 5) ? info.dwProp[5] : 0;
                uint p7 = (info.dwProp != null && info.dwProp.Length > 6) ? info.dwProp[6] : 0;
                uint p8 = (info.dwProp != null && info.dwProp.Length > 7) ? info.dwProp[7] : 0;

                cmd.Parameters.Add(CreateParameter("@prop1", (long)p1));
                cmd.Parameters.Add(CreateParameter("@prop2", (long)p2));
                cmd.Parameters.Add(CreateParameter("@prop3", (long)p3));
                cmd.Parameters.Add(CreateParameter("@prop4", (long)p4));
                cmd.Parameters.Add(CreateParameter("@prop5", (long)p5));
                cmd.Parameters.Add(CreateParameter("@prop6", (long)p6));
                cmd.Parameters.Add(CreateParameter("@prop7", (long)p7));
                cmd.Parameters.Add(CreateParameter("@prop8", (long)p8));

                int affected = cmd.ExecuteNonQuery();
                return affected > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"PutCharDBInfo失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryItems(uint ownerId, byte flag, out byte[] itemsData)
        {
            itemsData = Array.Empty<byte>();
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT 
                    ID, NAME, MINDC, MAXDC, MINMC, MAXMC, MINSC, MAXSC, MINAC, MAXAC,
                    MINMAC, MAXMAC, DURA, CURDURA, MAXDURA, NEEDTYPE, NEEDLEVEL, SPECIALPOWER, NEEDIDENTIFY,
                    WEIGHT, STDMODE, SHAPE, PRICE, UNKNOWN_1, UNKNOWN_2, POS, FINDKEY, IMAGEINDEX
                    FROM TBL_CHARACTER_ITEM 
                    WHERE OWNERID = @owner AND FLAG = @flag AND DELFLAG = 0";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                cmd.Parameters.Add(CreateParameter("@flag", flag));
                
                using var reader = cmd.ExecuteReader();
                var dbItems = new System.Collections.Generic.List<DBITEM>();
                var findKeyIdsToClear = new System.Collections.Generic.List<long>();
                
                while (reader.Read())
                {
                    try
                    {
                        
                        Console.WriteLine($"读取物品数据，字段数: {reader.FieldCount}");
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        string rawName = reader.GetString(1);
                        int nul = rawName.IndexOf('\0');
                        string cleanName = (nul >= 0 ? rawName.Substring(0, nul) : rawName).Trim();
                        int nameByteLen = 0;
                        try
                        {
                            
                            nameByteLen = MirCommon.StringEncoding.GetGBKBytes(cleanName).Length;
                        }
                        catch
                        {
                            nameByteLen = cleanName.Length;
                        }
                        nameByteLen = Math.Min(nameByteLen, 13);

                        
                        
                        
                        int unknown1 = 0;
                        short unknown2 = 0;
                        try
                        {
                            if (!reader.IsDBNull(23))
                                unknown1 = Convert.ToInt32(reader.GetValue(23));
                        }
                        catch { unknown1 = 0; }

                        try
                        {
                            if (!reader.IsDBNull(24))
                                unknown2 = reader.GetInt16(24);
                        }
                        catch
                        {
                            try { unknown2 = unchecked((short)Convert.ToInt32(reader.GetValue(24))); } catch { unknown2 = 0; }
                        }

                        ushort wUnknown = unchecked((ushort)unknown2);

                        int price = 0;
                        try
                        {
                            if (!reader.IsDBNull(22))
                            {
                                long p = Convert.ToInt64(reader.GetValue(22));
                                if (p < 0) p = 0;
                                if (p > int.MaxValue) p = int.MaxValue;
                                price = (int)p;
                            }
                        }
                        catch { price = 0; }

                        ushort imageIndex = 0;
                        try
                        {
                            if (reader.FieldCount > 27 && !reader.IsDBNull(27))
                            {
                                long v = Convert.ToInt64(reader.GetValue(27));
                                if (v < 0) v = 0;
                                if (v > ushort.MaxValue) v = ushort.MaxValue;
                                imageIndex = (ushort)v;
                            }
                        }
                        catch { imageIndex = 0; }

                        ushort ReadUShort(int ordinal)
                        {
                            try
                            {
                                if (reader.IsDBNull(ordinal))
                                    return 0;
                                long v = Convert.ToInt64(reader.GetValue(ordinal));
                                if (v < 0) v = 0;
                                if (v > ushort.MaxValue) v = ushort.MaxValue;
                                return (ushort)v;
                            }
                            catch
                            {
                                return 0;
                            }
                        }

                        byte ReadByte(int ordinal)
                        {
                            try
                            {
                                if (reader.IsDBNull(ordinal))
                                    return 0;
                                long v = Convert.ToInt64(reader.GetValue(ordinal));
                                if (v < byte.MinValue) v = byte.MinValue;
                                if (v > byte.MaxValue) v = byte.MaxValue;
                                return (byte)v;
                            }
                            catch
                            {
                                return 0;
                            }
                        }

                        ushort dura = ReadUShort(12);    
                        ushort curDura = ReadUShort(13); 
                        ushort maxDura = ReadUShort(14); 

                        
                        var baseItem = new MirCommon.BaseItem
                        {
                            btNameLength = (byte)nameByteLen, 
                            szName = cleanName, 
                            btStdMode = ReadByte(20), 
                            btShape = ReadByte(21),   
                            btWeight = ReadByte(19),  
                            btAniCount = 0, 
                            btSpecialpower = ReadByte(17), 
                            bNeedIdentify = ReadByte(18), 
                            btPriceType = (byte)Math.Clamp(unknown1, 0, 255),
                            
                            wImageIndex = imageIndex, 
                            wMaxDura = dura, 
                            Ac1 = ReadByte(8),  
                            Ac2 = ReadByte(9),  
                            Mac1 = ReadByte(10), 
                            Mac2 = ReadByte(11), 
                            Dc1 = ReadByte(2),  
                            Dc2 = ReadByte(3),  
                            Mc1 = ReadByte(4),  
                            Mc2 = ReadByte(5),  
                            Sc1 = ReadByte(6),  
                            Sc2 = ReadByte(7),  
                            needtype = ReadByte(15), 
                            needvalue = ReadByte(16), 
                            btFlag = (byte)(wUnknown & 0xFF),
                            btUpgradeTimes = (byte)((wUnknown >> 8) & 0xFF),
                            nPrice = price 
                        };
                        
                        
                        long itemId64 = reader.GetInt64(0);
                        if (itemId64 < 0) itemId64 = 0;
                        uint makeIndex = itemId64 > uint.MaxValue ? uint.MaxValue : (uint)itemId64;

                        int posValue = 0;
                        try { posValue = Convert.ToInt32(reader.GetValue(25)); } catch { posValue = 0; }
                        if (posValue < 0) posValue = 0;
                        if (posValue > ushort.MaxValue) posValue = ushort.MaxValue;

                        long findKey = reader.GetInt64(26); 
                        if (findKey != 0)
                        {
                            
                            findKeyIdsToClear.Add(itemId64);
                        }

                        var item = new MirCommon.Item
                        {
                            baseitem = baseItem,
                            dwMakeIndex = makeIndex, 
                            wCurDura = curDura, 
                            wMaxDura = maxDura, 
                            dwParam = new uint[4] { 0, 0, 0, 0 } 
                        };
                        
                        
                        var dbItem = new DBITEM
                        {
                            item = item,
                            wPos = (ushort)posValue,
                            btFlag = flag
                        };
                        
                        dbItems.Add(dbItem);
                        Console.WriteLine($"成功创建DBITEM: ID={item.dwMakeIndex}, Name={baseItem.szName}, Pos={dbItem.wPos}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"创建DBITEM时出错: {ex.Message}");
                        Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                        
                    }
                }
                
                Console.WriteLine($"总共创建了 {dbItems.Count} 个DBITEM");

                
                if (findKeyIdsToClear.Count > 0)
                {
                    try
                    {
                        try { reader.Close(); } catch { }
                        foreach (long id in findKeyIdsToClear)
                        {
                            using var clearCmd = _connection!.CreateCommand();
                            clearCmd.CommandText = "UPDATE TBL_CHARACTER_ITEM SET FINDKEY = 0 WHERE ID = @id";
                            clearCmd.Parameters.Add(CreateParameter("@id", id));
                            clearCmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"清理物品FINDKEY失败: {ex.Message}");
                    }
                }
                
                
                if (dbItems.Count > 0)
                {
                    itemsData = DatabaseSerializer.SerializeDbItems(dbItems.ToArray());
                    Console.WriteLine($"序列化成功，数据长度: {itemsData.Length} 字节");
                }
                else
                {
                    Console.WriteLine("没有物品需要序列化");
                }
                
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"QueryItems失败: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                LogManager.Default.Error($"QueryItems失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItems(uint ownerId, byte flag, byte[] itemsData)
        {
            try
            {
                if (_connection == null)
                    return SERVER_ERROR.SE_FAIL;

                var items = DatabaseSerializer.DeserializeDbItems(itemsData);

                using var tx = _connection.BeginTransaction();

                
                using (var markCmd = _connection.CreateCommand())
                {
                    markCmd.Transaction = tx;
                    markCmd.CommandText = "UPDATE TBL_CHARACTER_ITEM SET DELFLAG = 1 WHERE OWNERID = @owner AND FLAG = @flag";
                    markCmd.Parameters.Add(CreateParameter("@owner", (long)ownerId));
                    markCmd.Parameters.Add(CreateParameter("@flag", (int)flag));
                    markCmd.ExecuteNonQuery();
                }

                foreach (var dbItem in items)
                {
                    var item = dbItem.item;

                    uint rawMakeIndex = item.dwMakeIndex;
                    bool isTemp = (rawMakeIndex & 0x80000000u) != 0;

                    
                    
                    
                    
                    uint makeIndex = isTemp ? 0u : rawMakeIndex;
                    long findKey = isTemp ? (long)rawMakeIndex : 0L;

                    string name = item.baseitem.szName ?? string.Empty;
                    int nul = name.IndexOf('\0');
                    if (nul >= 0) name = name.Substring(0, nul);
                    name = name.Trim();

                    int mindc = item.baseitem.Dc1;
                    int maxdc = item.baseitem.Dc2;
                    int minmc = item.baseitem.Mc1;
                    int maxmc = item.baseitem.Mc2;
                    int minsc = item.baseitem.Sc1;
                    int maxsc = item.baseitem.Sc2;
                    int minac = item.baseitem.Ac1;
                    int maxac = item.baseitem.Ac2;
                    int minmac = item.baseitem.Mac1;
                    int maxmac = item.baseitem.Mac2;

                    int dura = item.baseitem.wMaxDura;
                    int curdura = item.wCurDura;
                    int maxdura = item.wMaxDura;

                    int needtype = item.baseitem.needtype;
                    int needlevel = item.baseitem.needvalue;
                    int specialpower = item.baseitem.btSpecialpower;
                    int needidentify = item.baseitem.bNeedIdentify;

                    int weight = item.baseitem.btWeight;
                    int stdmode = item.baseitem.btStdMode;
                    int shape = item.baseitem.btShape;
                    int price = item.baseitem.nPrice;

                    int pos = dbItem.wPos;
                    int imageIndex = item.baseitem.wImageIndex;

                    
                    int unknown1 = item.baseitem.btPriceType;
                    ushort wUnknown = (ushort)(item.baseitem.btFlag | (item.baseitem.btUpgradeTimes << 8));
                    short unknown2 = unchecked((short)wUnknown);

                    using var updateCmd = _connection.CreateCommand();
                    updateCmd.Transaction = tx;
                    updateCmd.CommandText = @"
UPDATE TBL_CHARACTER_ITEM SET
    OWNERID=@owner,
    FLAG=@flag,
    NAME=@name,
    MINDC=@mindc,
    MAXDC=@maxdc,
    MINMC=@minmc,
    MAXMC=@maxmc,
    MINSC=@minsc,
    MAXSC=@maxsc,
    MINAC=@minac,
    MAXAC=@maxac,
    MINMAC=@minmac,
    MAXMAC=@maxmac,
    DURA=@dura,
    CURDURA=@curdura,
    MAXDURA=@maxdura,
    NEEDTYPE=@needtype,
    NEEDLEVEL=@needlevel,
    SPECIALPOWER=@specialpower,
    NEEDIDENTIFY=@needidentify,
    WEIGHT=@weight,
    STDMODE=@stdmode,
    SHAPE=@shape,
    PRICE=@price,
    UNKNOWN_1=@unknown1,
    UNKNOWN_2=@unknown2,
    POS=@pos,
    FINDKEY=@findkey,
    IMAGEINDEX=@imageindex,
    DELFLAG=0
WHERE " + (isTemp ? "FINDKEY=@findkey_match" : "ID=@id") + ";";

                    updateCmd.Parameters.Add(CreateParameter("@owner", (long)ownerId));
                    updateCmd.Parameters.Add(CreateParameter("@flag", (int)flag));
                    updateCmd.Parameters.Add(CreateParameter("@name", name));
                    updateCmd.Parameters.Add(CreateParameter("@mindc", mindc));
                    updateCmd.Parameters.Add(CreateParameter("@maxdc", maxdc));
                    updateCmd.Parameters.Add(CreateParameter("@minmc", minmc));
                    updateCmd.Parameters.Add(CreateParameter("@maxmc", maxmc));
                    updateCmd.Parameters.Add(CreateParameter("@minsc", minsc));
                    updateCmd.Parameters.Add(CreateParameter("@maxsc", maxsc));
                    updateCmd.Parameters.Add(CreateParameter("@minac", minac));
                    updateCmd.Parameters.Add(CreateParameter("@maxac", maxac));
                    updateCmd.Parameters.Add(CreateParameter("@minmac", minmac));
                    updateCmd.Parameters.Add(CreateParameter("@maxmac", maxmac));
                    updateCmd.Parameters.Add(CreateParameter("@dura", dura));
                    updateCmd.Parameters.Add(CreateParameter("@curdura", curdura));
                    updateCmd.Parameters.Add(CreateParameter("@maxdura", maxdura));
                    updateCmd.Parameters.Add(CreateParameter("@needtype", needtype));
                    updateCmd.Parameters.Add(CreateParameter("@needlevel", needlevel));
                    updateCmd.Parameters.Add(CreateParameter("@specialpower", specialpower));
                    updateCmd.Parameters.Add(CreateParameter("@needidentify", needidentify));
                    updateCmd.Parameters.Add(CreateParameter("@weight", weight));
                    updateCmd.Parameters.Add(CreateParameter("@stdmode", stdmode));
                    updateCmd.Parameters.Add(CreateParameter("@shape", shape));
                    updateCmd.Parameters.Add(CreateParameter("@price", price));
                    updateCmd.Parameters.Add(CreateParameter("@unknown1", unknown1));
                    updateCmd.Parameters.Add(CreateParameter("@unknown2", unknown2));
                    updateCmd.Parameters.Add(CreateParameter("@pos", pos));
                    updateCmd.Parameters.Add(CreateParameter("@findkey", (long)findKey));
                    updateCmd.Parameters.Add(CreateParameter("@imageindex", imageIndex));
                    updateCmd.Parameters.Add(CreateParameter("@id", (long)makeIndex));
                    updateCmd.Parameters.Add(CreateParameter("@findkey_match", (long)findKey));

                    int updated = updateCmd.ExecuteNonQuery();
                    if (updated > 0)
                        continue;

                    using var insertCmd = _connection.CreateCommand();
                    insertCmd.Transaction = tx;

                    
                    
                    insertCmd.CommandText = isTemp
                        ? @"
INSERT INTO TBL_CHARACTER_ITEM (
    OWNERID, FLAG, NAME,
    MINDC, MAXDC, MINMC, MAXMC, MINSC, MAXSC,
    MINAC, MAXAC, MINMAC, MAXMAC,
    DURA, CURDURA, MAXDURA,
    NEEDTYPE, NEEDLEVEL, SPECIALPOWER, NEEDIDENTIFY,
    WEIGHT, STDMODE, SHAPE, PRICE,
    UNKNOWN_1, UNKNOWN_2, POS, FINDKEY, IMAGEINDEX, DELFLAG
) VALUES (
    @owner, @flag, @name,
    @mindc, @maxdc, @minmc, @maxmc, @minsc, @maxsc,
    @minac, @maxac, @minmac, @maxmac,
    @dura, @curdura, @maxdura,
    @needtype, @needlevel, @specialpower, @needidentify,
    @weight, @stdmode, @shape, @price,
    @unknown1, @unknown2, @pos, @findkey, @imageindex, 0
)"
                        : @"
INSERT INTO TBL_CHARACTER_ITEM (
    ID, OWNERID, FLAG, NAME,
    MINDC, MAXDC, MINMC, MAXMC, MINSC, MAXSC,
    MINAC, MAXAC, MINMAC, MAXMAC,
    DURA, CURDURA, MAXDURA,
    NEEDTYPE, NEEDLEVEL, SPECIALPOWER, NEEDIDENTIFY,
    WEIGHT, STDMODE, SHAPE, PRICE,
    UNKNOWN_1, UNKNOWN_2, POS, FINDKEY, IMAGEINDEX, DELFLAG
) VALUES (
    @id, @owner, @flag, @name,
    @mindc, @maxdc, @minmc, @maxmc, @minsc, @maxsc,
    @minac, @maxac, @minmac, @maxmac,
    @dura, @curdura, @maxdura,
    @needtype, @needlevel, @specialpower, @needidentify,
    @weight, @stdmode, @shape, @price,
    @unknown1, @unknown2, @pos, @findkey, @imageindex, 0
)";

                    if (!isTemp)
                        insertCmd.Parameters.Add(CreateParameter("@id", (long)makeIndex));
                    insertCmd.Parameters.Add(CreateParameter("@owner", (long)ownerId));
                    insertCmd.Parameters.Add(CreateParameter("@flag", (int)flag));
                    insertCmd.Parameters.Add(CreateParameter("@name", name));
                    insertCmd.Parameters.Add(CreateParameter("@mindc", mindc));
                    insertCmd.Parameters.Add(CreateParameter("@maxdc", maxdc));
                    insertCmd.Parameters.Add(CreateParameter("@minmc", minmc));
                    insertCmd.Parameters.Add(CreateParameter("@maxmc", maxmc));
                    insertCmd.Parameters.Add(CreateParameter("@minsc", minsc));
                    insertCmd.Parameters.Add(CreateParameter("@maxsc", maxsc));
                    insertCmd.Parameters.Add(CreateParameter("@minac", minac));
                    insertCmd.Parameters.Add(CreateParameter("@maxac", maxac));
                    insertCmd.Parameters.Add(CreateParameter("@minmac", minmac));
                    insertCmd.Parameters.Add(CreateParameter("@maxmac", maxmac));
                    insertCmd.Parameters.Add(CreateParameter("@dura", dura));
                    insertCmd.Parameters.Add(CreateParameter("@curdura", curdura));
                    insertCmd.Parameters.Add(CreateParameter("@maxdura", maxdura));
                    insertCmd.Parameters.Add(CreateParameter("@needtype", needtype));
                    insertCmd.Parameters.Add(CreateParameter("@needlevel", needlevel));
                    insertCmd.Parameters.Add(CreateParameter("@specialpower", specialpower));
                    insertCmd.Parameters.Add(CreateParameter("@needidentify", needidentify));
                    insertCmd.Parameters.Add(CreateParameter("@weight", weight));
                    insertCmd.Parameters.Add(CreateParameter("@stdmode", stdmode));
                    insertCmd.Parameters.Add(CreateParameter("@shape", shape));
                    insertCmd.Parameters.Add(CreateParameter("@price", price));
                    insertCmd.Parameters.Add(CreateParameter("@unknown1", unknown1));
                    insertCmd.Parameters.Add(CreateParameter("@unknown2", unknown2));
                    insertCmd.Parameters.Add(CreateParameter("@pos", pos));
                    insertCmd.Parameters.Add(CreateParameter("@findkey", (long)findKey));
                    insertCmd.Parameters.Add(CreateParameter("@imageindex", imageIndex));

                    insertCmd.ExecuteNonQuery();
                }

                tx.Commit();
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"UpdateItems失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryMagic(uint ownerId, out byte[] magicData)
        {
            magicData = Array.Empty<byte>();
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT USERKEY, CURLEVEL, MAGICID, CURTRAIN
                    FROM TBL_CHARACTER_MAGIC 
                    WHERE CHARID = @owner";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                
                using var reader = cmd.ExecuteReader();
                var magicList = new System.Collections.Generic.List<MAGICDB>();
                
                while (reader.Read())
                {
                    var magicDb = new MAGICDB
                    {
                        btUserKey = reader.GetByte(0), 
                        btCurLevel = reader.GetByte(1), 
                        wMagicId = (ushort)reader.GetInt16(2), 
                        dwCurTrain = (uint)reader.GetInt32(3) 
                    };
                    
                    magicList.Add(magicDb);
                }
                
                
                magicData = DatabaseSerializer.SerializeMagicDbs(magicList.ToArray());
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"QueryMagic失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateMagic(uint ownerId, byte[] magicData)
        {
            try
            {
                if (_connection == null)
                    return SERVER_ERROR.SE_FAIL;

                var magics = DatabaseSerializer.DeserializeMagicDbs(magicData);

                using var tx = _connection.BeginTransaction();

                using (var delCmd = _connection.CreateCommand())
                {
                    delCmd.Transaction = tx;
                    delCmd.CommandText = "DELETE FROM TBL_CHARACTER_MAGIC WHERE CHARID = @owner";
                    delCmd.Parameters.Add(CreateParameter("@owner", (long)ownerId));
                    delCmd.ExecuteNonQuery();
                }

                foreach (var m in magics)
                {
                    using var insCmd = _connection.CreateCommand();
                    insCmd.Transaction = tx;
                    insCmd.CommandText = @"
INSERT INTO TBL_CHARACTER_MAGIC (CHARID, USERKEY, CURLEVEL, MAGICID, CURTRAIN)
VALUES (@owner, @userkey, @curlevel, @magicid, @curtrain)";
                    insCmd.Parameters.Add(CreateParameter("@owner", (long)ownerId));
                    insCmd.Parameters.Add(CreateParameter("@userkey", (int)m.btUserKey));
                    insCmd.Parameters.Add(CreateParameter("@curlevel", (int)m.btCurLevel));
                    insCmd.Parameters.Add(CreateParameter("@magicid", (int)m.wMagicId));
                    insCmd.Parameters.Add(CreateParameter("@curtrain", (long)m.dwCurTrain));
                    insCmd.ExecuteNonQuery();
                }

                tx.Commit();
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"UpdateMagic失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR ExecSqlCommand(string sql, out DataTable result)
        {
            result = new DataTable();
            try
            {
                
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = sql;
                using var reader = cmd.ExecuteReader();
                
                
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    result.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
                }
                
                
                while (reader.Read())
                {
                    var row = result.NewRow();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                    }
                    result.Rows.Add(row);
                }
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual void Close()
        {
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }
        
        
        
        public virtual SERVER_ERROR GetMapPosition(string account, string serverName, string name, out string mapName, out short x, out short y)
        {
            mapName = "";
            x = 0;
            y = 0;
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT MAPNAME, POSX, POSY FROM TBL_CHARACTER_INFO 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND NAME = @name";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    mapName = reader.GetString(0);
                    x = reader.GetInt16(1);
                    y = reader.GetInt16(2);
                    return SERVER_ERROR.SE_OK;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR GetFreeItemId(out uint itemId)
        {
            itemId = 0;
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT TOP 1 ID FROM TBL_CHARACTER_ITEM WHERE DELFLAG = 1";
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    long id64 = reader.GetInt64(0);
                    if (id64 < 0) id64 = 0;
                    if (id64 > uint.MaxValue) id64 = uint.MaxValue;
                    itemId = (uint)id64;
                    return SERVER_ERROR.SE_OK;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR FindItemId(uint ownerId, byte flag, ushort pos, uint findKey, out uint itemId)
        {
            itemId = 0;
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT TOP 1 ID FROM TBL_CHARACTER_ITEM 
                                  WHERE OWNERID = @owner AND FLAG = @flag AND POS = @pos AND FINDKEY = @findKey";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                cmd.Parameters.Add(CreateParameter("@flag", flag));
                cmd.Parameters.Add(CreateParameter("@pos", pos));
                cmd.Parameters.Add(CreateParameter("@findKey", findKey));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    long id64 = reader.GetInt64(0);
                    if (id64 < 0) id64 = 0;
                    if (id64 > uint.MaxValue) id64 = uint.MaxValue;
                    itemId = (uint)id64;
                    
                    
                    using var updateCmd = _connection!.CreateCommand();
                    updateCmd.CommandText = "UPDATE TBL_CHARACTER_ITEM SET FINDKEY = 0 WHERE ID = @id";
                    updateCmd.Parameters.Add(CreateParameter("@id", (long)itemId));
                    updateCmd.ExecuteNonQuery();
                    
                    return SERVER_ERROR.SE_OK;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpgradeItem(uint makeIndex, uint upgrade)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_ITEM 
                                  SET NEEDIDENTIFY = 1, FLAG = @flag, FINDKEY = @upgrade 
                                  WHERE ID = @id";
                cmd.Parameters.Add(CreateParameter("@flag", (byte)1)); 
                cmd.Parameters.Add(CreateParameter("@upgrade", upgrade));
                cmd.Parameters.Add(CreateParameter("@id", makeIndex));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateItem(uint ownerId, byte flag, ushort pos, byte[] itemData)
        {
            try
            {
                
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateItemEx(uint ownerId, byte flag, ushort pos, byte[] itemData)
        {
            try
            {
                
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItem(uint ownerId, byte flag, ushort pos, byte[] itemData)
        {
            try
            {
                
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteItem(uint itemId)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "DELETE FROM TBL_CHARACTER_ITEM WHERE ID = @id";
                cmd.Parameters.Add(CreateParameter("@id", itemId));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItemPos(uint itemId, byte flag, ushort pos)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "UPDATE TBL_CHARACTER_ITEM SET FLAG = @flag, POS = @pos WHERE ID = @id";
                cmd.Parameters.Add(CreateParameter("@flag", flag));
                cmd.Parameters.Add(CreateParameter("@pos", pos));
                cmd.Parameters.Add(CreateParameter("@id", itemId));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItemPosEx(byte flag, ushort count, byte[] itemPosData)
        {
            try
            {
                
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItemOwner(uint itemId, uint ownerId, byte flag, ushort pos)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_ITEM 
                                  SET OWNERID = @owner, FLAG = @flag, POS = @pos 
                                  WHERE ID = @id";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                cmd.Parameters.Add(CreateParameter("@flag", flag));
                cmd.Parameters.Add(CreateParameter("@pos", pos));
                cmd.Parameters.Add(CreateParameter("@id", itemId));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteMagic(uint ownerId, ushort magicId)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "DELETE FROM TBL_CHARACTER_MAGIC WHERE CHARID = @owner AND MAGICID = @magicId";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                cmd.Parameters.Add(CreateParameter("@magicId", magicId));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateCommunity(uint ownerId, string communityData)
        {
            try
            {
                
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryCommunity(uint ownerId, out string communityData)
        {
            communityData = "";
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT MARRIAGE, MASTER, STUDENT1, STUDENT2, STUDENT3,
                    FRIEND1, FRIEND2, FRIEND3, FRIEND4, FRIEND5, FRIEND6, FRIEND7, FRIEND8, FRIEND9, FRIEND10
                    FROM TBL_CHARACTER_COMMUNITY 
                    WHERE OWNERID = @owner";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var result = new StringBuilder();
                    
                    
                    string wife = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    if (!string.IsNullOrEmpty(wife))
                        result.Append($"{wife}/");
                    else
                        result.Append("/");
                    
                    
                    string master = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    if (!string.IsNullOrEmpty(master))
                        result.Append($"{master}/");
                    else
                        result.Append("/");
                    
                    
                    for (int i = 2; i <= 4; i++)
                    {
                        string student = reader.IsDBNull(i) ? "" : reader.GetString(i);
                        if (!string.IsNullOrEmpty(student))
                            result.Append($"{student}/");
                        else
                            result.Append("/");
                    }
                    
                    
                    for (int i = 5; i <= 14; i++)
                    {
                        string friend = reader.IsDBNull(i) ? "" : reader.GetString(i);
                        if (!string.IsNullOrEmpty(friend))
                            result.Append($"{friend}/");
                        else
                            result.Append("/");
                    }
                    
                    communityData = result.ToString();
                    return SERVER_ERROR.SE_OK;
                }
                else
                {
                    
                    
                    communityData = "///////////////";
                    return SERVER_ERROR.SE_OK;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"QueryCommunity失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteMarriage(string name, string marriage)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_COMMUNITY 
                                  SET MARRIAGE = '' 
                                  WHERE NAME = @name AND MARRIAGE = @marriage";
                cmd.Parameters.Add(CreateParameter("@name", name));
                cmd.Parameters.Add(CreateParameter("@marriage", marriage));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteTeacher(string name, string teacher)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_COMMUNITY 
                                  SET MASTER = '' 
                                  WHERE NAME = @name AND MASTER = @teacher";
                cmd.Parameters.Add(CreateParameter("@name", name));
                cmd.Parameters.Add(CreateParameter("@teacher", teacher));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteStudent(string teacher, string student)
        {
            try
            {
                
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR BreakFriend(string friend1, string friend2)
        {
            try
            {
                
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR RestoreGuild(string name, string guildName)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_INFO 
                                  SET GUILDNAME = @guildName 
                                  WHERE NAME = @name";
                cmd.Parameters.Add(CreateParameter("@guildName", guildName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR AddCredit(string name, uint count)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT FLAG1 FROM TBL_CHARACTER_INFO WHERE NAME = @name";
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    uint credit = (uint)reader.GetInt32(0);
                    uint newCredit = credit + count;
                    if (newCredit > 0xFFFF) newCredit = 0xFFFF;
                    
                    reader.Close();
                    
                    using var updateCmd = _connection!.CreateCommand();
                    updateCmd.CommandText = "UPDATE TBL_CHARACTER_INFO SET FLAG1 = @credit WHERE NAME = @name";
                    updateCmd.Parameters.Add(CreateParameter("@credit", newCredit));
                    updateCmd.Parameters.Add(CreateParameter("@name", name));
                    
                    return updateCmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryTaskInfo(uint ownerId, out byte[] taskInfoData)
        {
            taskInfoData = Array.Empty<byte>();
            try
            {
                
                bool isSQLite = _connectionString.Contains("Data Source=") || _connectionString.Contains(".sqlite");
                
                if (isSQLite)
                {
                    
                    using var cmd = _connection!.CreateCommand();
                    cmd.CommandText = @"SELECT ACHIEVEMENT, TASKID1, TASKSTEP1, TASKID2, TASKSTEP2, 
                        TASKID3, TASKSTEP3, TASKID4, TASKSTEP4, TASKID5, TASKSTEP5,
                        TASKID6, TASKSTEP6, TASKID7, TASKSTEP7, TASKID8, TASKSTEP8,
                        TASKID9, TASKSTEP9, TASKID10, TASKSTEP10, FLAGS
                        FROM TBL_CHARACTER_TASK 
                        WHERE CHARID = @owner";
                    cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                    
                    using var reader = cmd.ExecuteReader();
                    var taskList = new System.Collections.Generic.List<TaskInfo>();
                    
                    if (reader.Read())
                    {
                        
                        uint achievement = (uint)reader.GetInt32(0);
                        
                        
                        for (int i = 0; i < 10; i++)
                        {
                            int taskIdIndex = 1 + i * 2;
                            int taskStepIndex = 2 + i * 2;
                            
                            uint taskId = (uint)reader.GetInt32(taskIdIndex);
                            uint taskStep = (uint)reader.GetInt32(taskStepIndex);
                            
                            if (taskId > 0)
                            {
                                var taskInfo = new TaskInfo
                                {
                                    dwOwner = ownerId,
                                    dwTaskId = taskId,
                                    dwState = taskStep, 
                                    dwParam1 = 0, 
                                    dwParam2 = 0, 
                                    dwParam3 = 0, 
                                    dwParam4 = 0 
                                };
                                
                                taskList.Add(taskInfo);
                            }
                        }
                        
                        
                        
                    }
                    
                    
                    taskInfoData = DatabaseSerializer.SerializeTaskInfos(taskList.ToArray());
                    return SERVER_ERROR.SE_OK;
                }
                else
                {
                    
                    using var cmd = _connection!.CreateCommand();
                    cmd.CommandText = @"SELECT TASKID, TASKSTATUS, TASKPROGRESS, TASKFLAG, TASKTIME
                        FROM TBL_CHARACTER_TASK 
                        WHERE CHARID = @owner";
                    cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                    
                    using var reader = cmd.ExecuteReader();
                    var taskList = new System.Collections.Generic.List<TaskInfo>();
                    
                    while (reader.Read())
                    {
                        var taskInfo = new TaskInfo
                        {
                            dwOwner = ownerId,
                            dwTaskId = (uint)reader.GetInt32(0), 
                            dwState = (uint)reader.GetInt32(1), 
                            dwParam1 = (uint)reader.GetInt32(2), 
                            dwParam2 = (uint)reader.GetInt32(3), 
                            dwParam3 = (uint)reader.GetInt32(4), 
                            dwParam4 = 0 
                        };
                        
                        taskList.Add(taskInfo);
                    }
                    
                    
                    taskInfoData = DatabaseSerializer.SerializeTaskInfos(taskList.ToArray());
                    return SERVER_ERROR.SE_OK;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"QueryTaskInfo失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateTaskInfo(uint ownerId, byte[] taskInfoData)
        {
            try
            {
                
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryUpgradeItem(uint ownerId, out byte[] upgradeItemData)
        {
            upgradeItemData = Array.Empty<byte>();
            try
            {
                
                
                byte flag = 1; 
                
                
                return QueryItems(ownerId, flag, out upgradeItemData);
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
    }
}
