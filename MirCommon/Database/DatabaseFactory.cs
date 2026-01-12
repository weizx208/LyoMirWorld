using System;

namespace MirCommon.Database
{
    
    
    
    public enum DatabaseType
    {
        
        
        
        SQLite,
        
        
        
        
        MySQL,
        
        
        
        
        SqlServer
    }
    
    
    
    
    public static class DatabaseFactory
    {
        
        
        
        
        
        
        public static IDatabase CreateDatabase(DatabaseType type, string connectionString)
        {
            return type switch
            {
                DatabaseType.SQLite => new SQLiteDatabase(connectionString),
                DatabaseType.MySQL => new MySQLDatabase(connectionString),
                DatabaseType.SqlServer => new SqlServerDatabase(connectionString),
                _ => throw new ArgumentException($"不支持的数据库类型: {type}")
            };
        }
        
        
        
        
        
        
        public static IDatabase CreateDatabaseFromConfig(DatabaseConfig config)
        {
            return config.Type switch
            {
                DatabaseType.SQLite => new SQLiteDatabase(config.ConnectionString),
                DatabaseType.MySQL => new MySQLDatabase(config.ConnectionString),
                DatabaseType.SqlServer => new SqlServerDatabase(config.ConnectionString),
                _ => throw new ArgumentException($"不支持的数据库类型: {config.Type}")
            };
        }
    }
    
}
