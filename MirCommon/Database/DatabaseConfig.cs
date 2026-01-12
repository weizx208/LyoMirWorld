using System;
using System.Data;
using MirCommon.Utils;
using MySql.Data.MySqlClient;
using Microsoft.Data.Sqlite;

namespace MirCommon.Database
{
    
    
    
    public class DatabaseConfig
    {
        
        
        
        public DatabaseType Type { get; set; } = DatabaseType.SQLite;
        
        
        
        
        public string ConnectionString { get; set; } = string.Empty;
        
        
        
        
        public string Server { get; set; } = "(local)";
        
        
        
        
        public string Database { get; set; } = "MirWorldDB";
        
        
        
        
        public string UserId { get; set; } = "sa";
        
        
        
        
        public string Password { get; set; } = "123456";
        
        
        
        
        public string SqliteFilePath { get; set; } = "MirWorldDB.sqlite";
        
        public int MaxConnections { get; set; } = 1024;
        public int ConnectionTimeout { get; set; } = 30;
        public int CommandTimeout { get; set; } = 30;

        
        
        
        public static DatabaseConfig FromConfigString(string type, string server, string database, string userId, string password, string sqlitePath = "")
        {
            var config = new DatabaseConfig();
            
            
            config.Type = type.ToLower() switch
            {
                "sqlite" => DatabaseType.SQLite,
                "mysql" => DatabaseType.MySQL,
                "sqlserver" or "mssql" => DatabaseType.SqlServer,
                _ => DatabaseType.SQLite 
            };
            
            config.Server = server;
            config.Database = database;
            config.UserId = userId;
            config.Password = password;
            config.SqliteFilePath = sqlitePath;
            
            
            config.ConnectionString = config.Type switch
            {
                DatabaseType.SQLite => $"Data Source={sqlitePath};",  
                DatabaseType.MySQL => $"Server={server};Database={database};User Id={userId};Password={password};",
                DatabaseType.SqlServer => $"Server={server};Database={database};User Id={userId};Password={password};",
                _ => $"Data Source={sqlitePath};Version=3;"
            };
            
            return config;
        }

        
        
        
        public static DatabaseConfig LoadFromIni(string iniFilePath, string sectionName = "数据库服务器")
        {
            var config = new DatabaseConfig();
            
            try
            {
                var iniReader = new IniFileReader(iniFilePath);
                if (iniReader.Open())
                {
                    
                    string dbType = iniReader.GetString(sectionName, "dbtype", "sqlite");
                    config.Type = dbType.ToLower() switch
                    {
                        "sqlite" => DatabaseType.SQLite,
                        "mysql" => DatabaseType.MySQL,
                        "sqlserver" or "mssql" => DatabaseType.SqlServer,
                        _ => DatabaseType.SQLite
                    };
                    
                    config.Server = iniReader.GetString(sectionName, "server", "(local)");
                    config.Database = iniReader.GetString(sectionName, "database", "MirWorldDB");
                    config.UserId = iniReader.GetString(sectionName, "account", "sa");
                    config.Password = iniReader.GetString(sectionName, "password", "123456");
                    config.SqliteFilePath = iniReader.GetString(sectionName, "sqlitepath", "MirWorldDB.sqlite");
                    config.MaxConnections = iniReader.GetInteger(sectionName, "maxconnection", 1024);
                    
                    
                    config.ConnectionString = config.Type switch
                    {
                        DatabaseType.SQLite => $"Data Source={config.SqliteFilePath};Version=3;",
                        DatabaseType.MySQL => $"Server={config.Server};Database={config.Database};User Id={config.UserId};Password={config.Password};",
                        DatabaseType.SqlServer => $"Server={config.Server};Database={config.Database};User Id={config.UserId};Password={config.Password};",
                        _ => $"Data Source={config.SqliteFilePath};Version=3;"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载数据库配置失败: {ex.Message}");
            }

            return config;
        }

        
        
        
        public string GetConnectionString()
        {
            if (!string.IsNullOrEmpty(ConnectionString))
                return ConnectionString;
                
            return Type switch
            {
                DatabaseType.SQLite => $"Data Source={SqliteFilePath};Version=3;",
                DatabaseType.MySQL => $"Server={Server};Database={Database};User Id={UserId};Password={Password};",
                DatabaseType.SqlServer => $"Server={Server};Database={Database};User Id={UserId};Password={Password};TrustServerCertificate=True;Connection Timeout={ConnectionTimeout};Max Pool Size={MaxConnections};",
                _ => $"Data Source={SqliteFilePath};Version=3;"
            };
        }

        
        
        
        public bool TestConnection()
        {
            try
            {
                switch (Type)
                {
                    case DatabaseType.SQLite:
                        using (var connection = new SqliteConnection(GetConnectionString()))
                        {
                            connection.Open();
                            return connection.State == ConnectionState.Open;
                        }
                        
                    case DatabaseType.MySQL:
                        using (var connection = new MySqlConnection(GetConnectionString()))
                        {
                            connection.Open();
                            return connection.State == ConnectionState.Open;
                        }
                        
                    case DatabaseType.SqlServer:
                    default:
                        using (var connection = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
                        {
                            connection.Open();
                            return connection.State == ConnectionState.Open;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"数据库连接测试失败: {ex.Message}");
                return false;
            }
        }
    }
}
