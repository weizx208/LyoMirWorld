using Microsoft.Data.Sqlite;
using MirCommon;
using MirCommon.Database;
using MirCommon.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace DBServer
{
    public class ErrorHandler
    {
        private readonly string _connectionString;
        private readonly DatabaseType _databaseType;
        private readonly int _maxRetryCount;
        private readonly TimeSpan _retryDelay;
        private readonly TimeSpan _connectionTimeout;
        private int _currentRetryCount;
        private DateTime _lastErrorTime;
        private readonly object _lock = new object();

        public ErrorHandler(string connectionString, int maxRetryCount = 3, TimeSpan? retryDelay = null, TimeSpan? connectionTimeout = null)
            : this(connectionString, DatabaseType.SqlServer, maxRetryCount, retryDelay, connectionTimeout)
        {
        }

        public ErrorHandler(string connectionString, DatabaseType databaseType, int maxRetryCount = 3, TimeSpan? retryDelay = null, TimeSpan? connectionTimeout = null)
        {
            _connectionString = connectionString;
            _databaseType = databaseType;
            _maxRetryCount = maxRetryCount;
            _retryDelay = retryDelay ?? TimeSpan.FromSeconds(5);
            _connectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(30);
            _currentRetryCount = 0;
            _lastErrorTime = DateTime.MinValue;
        }

        public async Task<MirCommon.SERVER_ERROR> ExecuteWithRetryAsync(Func<Task<MirCommon.SERVER_ERROR>> operation, string operationName = "")
        {
            MirCommon.SERVER_ERROR lastError = MirCommon.SERVER_ERROR.SE_FAIL;
            
            for (int attempt = 1; attempt <= _maxRetryCount; attempt++)
            {
                try
                {
                    var result = await operation();
                    
                    if (result == MirCommon.SERVER_ERROR.SE_OK)
                    {
                        lock (_lock)
                        {
                            _currentRetryCount = 0;
                        }
                        return result;
                    }
                    
                    if (IsBusinessError(result))
                    {
                        return result;
                    }
                    
                    lastError = result;
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} 操作失败 (尝试 {attempt}/{_maxRetryCount}): {GetErrorDescription(result)}");
                }
                catch (SqlException sqlEx)
                {
                    lastError = MapSqlExceptionToServerError(sqlEx);
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} SQL异常 (尝试 {attempt}/{_maxRetryCount}): {sqlEx.Message}");
                    
                    if (IsConnectionError(sqlEx))
                    {
                        LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 检测到连接错误，将在 {_retryDelay.TotalSeconds} 秒后重试...");
                    }
                }
                catch (MySqlException mySqlEx)
                {
                    lastError = MapMySqlExceptionToServerError(mySqlEx);
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} MySQL异常 (尝试 {attempt}/{_maxRetryCount}): {mySqlEx.Message}");
                    
                    if (IsMySqlConnectionError(mySqlEx))
                    {
                        LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 检测到连接错误，将在 {_retryDelay.TotalSeconds} 秒后重试...");
                    }
                }
                catch (SqliteException sqliteEx)
                {
                    lastError = MapSqliteExceptionToServerError(sqliteEx);
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} SQLite异常 (尝试 {attempt}/{_maxRetryCount}): {sqliteEx.Message}");
                    
                    if (IsSqliteConnectionError(sqliteEx))
                    {
                        LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 检测到连接错误，将在 {_retryDelay.TotalSeconds} 秒后重试...");
                    }
                }
                catch (Exception ex)
                {
                    lastError = MirCommon.SERVER_ERROR.SE_FAIL;
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} 异常 (尝试 {attempt}/{_maxRetryCount}): {ex.Message}");
                }

                if (attempt < _maxRetryCount)
                {
                    await Task.Delay(_retryDelay);
                }
            }

            lock (_lock)
            {
                _currentRetryCount++;
                _lastErrorTime = DateTime.Now;
            }
            
            LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] {operationName} 操作失败，已达到最大重试次数 ({_maxRetryCount})");
            return lastError;
        }

        public MirCommon.SERVER_ERROR ExecuteWithRetry(Func<MirCommon.SERVER_ERROR> operation, string operationName = "")
        {
            return ExecuteWithRetryAsync(() => Task.FromResult(operation()), operationName).GetAwaiter().GetResult();
        }

        public async Task<bool> CheckConnectionHealthAsync()
        {
            try
            {
                switch (_databaseType)
                {
                    case DatabaseType.SQLite:
                        using (var connection = new SqliteConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            using var command = new SqliteCommand("SELECT 1", connection);
                            command.CommandTimeout = (int)_connectionTimeout.TotalSeconds;
                            var result = await command.ExecuteScalarAsync();
                            return result != null && Convert.ToInt32(result) == 1;
                        }
                        
                    case DatabaseType.MySQL:
                        using (var connection = new MySqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            using var command = new MySqlCommand("SELECT 1", connection);
                            command.CommandTimeout = (int)_connectionTimeout.TotalSeconds;
                            var result = await command.ExecuteScalarAsync();
                            return result != null && Convert.ToInt32(result) == 1;
                        }
                        
                    case DatabaseType.SqlServer:
                    default:
                        using (var connection = new SqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            using var command = new SqlCommand("SELECT 1", connection);
                            command.CommandTimeout = (int)_connectionTimeout.TotalSeconds;
                            var result = await command.ExecuteScalarAsync();
                            return result != null && Convert.ToInt32(result) == 1;
                        }
                }
            }
            catch
            {
                return false;
            }
        }

        public ErrorStatistics GetErrorStatistics()
        {
            lock (_lock)
            {
                return new ErrorStatistics
                {
                    CurrentRetryCount = _currentRetryCount,
                    LastErrorTime = _lastErrorTime,
                    MaxRetryCount = _maxRetryCount,
                    IsHealthy = _currentRetryCount == 0
                };
            }
        }

        public void ResetErrorStatistics()
        {
            lock (_lock)
            {
                _currentRetryCount = 0;
                _lastErrorTime = DateTime.MinValue;
            }
        }

        private bool IsBusinessError(MirCommon.SERVER_ERROR error)
        {
            switch (error)
            {
                case MirCommon.SERVER_ERROR.SE_LOGIN_ACCOUNTEXIST:
                case MirCommon.SERVER_ERROR.SE_LOGIN_ACCOUNTNOTEXIST:
                case MirCommon.SERVER_ERROR.SE_LOGIN_PASSWORDERROR:
                case MirCommon.SERVER_ERROR.SE_SELCHAR_CHAREXIST:
                case MirCommon.SERVER_ERROR.SE_SELCHAR_NOTEXIST:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDACCOUNT:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDPASSWORD:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDNAME:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDBIRTHDAY:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDPHONENUMBER:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDMOBILEPHONE:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDQUESTION:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDANSWER:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDIDCARD:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDEMAIL:
                case MirCommon.SERVER_ERROR.SE_CREATECHARACTER_INVALID_CHARNAME:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsConnectionError(SqlException ex)
        {
            int[] connectionErrorCodes = { -2, 20, 53, 121, 233, 4060, 18456 };
            
            return Array.Exists(connectionErrorCodes, code => code == ex.Number);
        }

        private MirCommon.SERVER_ERROR MapSqlExceptionToServerError(SqlException ex)
        {
            switch (ex.Number)
            {
                case 18456: 
                    return MirCommon.SERVER_ERROR.SE_LOGIN_PASSWORDERROR;
                case 4060: 
                    return MirCommon.SERVER_ERROR.SE_DB_NOTINITED;
                case -2: 
                case 121: 
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLEXECDIRECTFAIL;
                case 53: 
                case 233: 
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLCONNECTFAIL;
                default:
                    return MirCommon.SERVER_ERROR.SE_FAIL;
            }
        }

        private bool IsMySqlConnectionError(MySqlException ex)
        {
            
            
            
            
            
            
            
            
            uint[] connectionErrorCodes = { 1042, 1043, 1044, 1045, 2002, 2003, 2006, 2013 };
            
            return Array.Exists(connectionErrorCodes, code => code == ex.Number);
        }

        private MirCommon.SERVER_ERROR MapMySqlExceptionToServerError(MySqlException ex)
        {
            switch (ex.Number)
            {
                case 1045: 
                    return MirCommon.SERVER_ERROR.SE_LOGIN_PASSWORDERROR;
                case 1044: 
                case 1049: 
                    return MirCommon.SERVER_ERROR.SE_DB_NOTINITED;
                case 2002: 
                case 2003: 
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLCONNECTFAIL;
                case 2006: 
                case 2013: 
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLEXECDIRECTFAIL;
                default:
                    return MirCommon.SERVER_ERROR.SE_FAIL;
            }
        }

        private bool IsSqliteConnectionError(SqliteException ex)
        {
            
            
            
            
            int[] connectionErrorCodes = { 1, 14, 21, 26 };
            
            return Array.Exists(connectionErrorCodes, code => code == ex.SqliteErrorCode);
        }

        private MirCommon.SERVER_ERROR MapSqliteExceptionToServerError(SqliteException ex)
        {
            switch (ex.SqliteErrorCode)
            {
                case 1: 
                case 14: 
                    return MirCommon.SERVER_ERROR.SE_DB_NOTINITED;
                case 26: 
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLEXECDIRECTFAIL;
                default:
                    return MirCommon.SERVER_ERROR.SE_FAIL;
            }
        }

        private string GetErrorDescription(MirCommon.SERVER_ERROR error)
        {
            return error switch
            {
                MirCommon.SERVER_ERROR.SE_OK => "操作成功",
                MirCommon.SERVER_ERROR.SE_FAIL => "操作失败",
                MirCommon.SERVER_ERROR.SE_ALLOCMEMORYFAIL => "内存分配失败",
                MirCommon.SERVER_ERROR.SE_DB_NOMOREDATA => "没有更多数据",
                MirCommon.SERVER_ERROR.SE_DB_NOTINITED => "数据库未初始化",
                MirCommon.SERVER_ERROR.SE_LOGIN_ACCOUNTEXIST => "账号已存在",
                MirCommon.SERVER_ERROR.SE_LOGIN_ACCOUNTNOTEXIST => "账号不存在",
                MirCommon.SERVER_ERROR.SE_LOGIN_PASSWORDERROR => "密码错误",
                MirCommon.SERVER_ERROR.SE_SELCHAR_CHAREXIST => "角色已存在",
                MirCommon.SERVER_ERROR.SE_SELCHAR_NOTEXIST => "角色不存在",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDACCOUNT => "无效的账号",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDPASSWORD => "无效的密码",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDNAME => "无效的名字",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDBIRTHDAY => "无效的生日",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDPHONENUMBER => "无效的电话号码",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDMOBILEPHONE => "无效的手机号码",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDQUESTION => "无效的问题",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDANSWER => "无效的答案",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDIDCARD => "无效的身份证",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDEMAIL => "无效的邮箱",
                MirCommon.SERVER_ERROR.SE_CREATECHARACTER_INVALID_CHARNAME => "无效的角色名",
                MirCommon.SERVER_ERROR.SE_ODBC_SQLCONNECTFAIL => "数据库连接失败",
                MirCommon.SERVER_ERROR.SE_ODBC_SQLEXECDIRECTFAIL => "SQL执行失败",
                _ => $"未知错误: {error}"
            };
        }
    }

    public class ErrorStatistics
    {
        public int CurrentRetryCount { get; set; }
        public DateTime LastErrorTime { get; set; }
        public int MaxRetryCount { get; set; }
        public bool IsHealthy { get; set; }
        
        public int TotalErrors { get; set; }
        public int ConnectionErrors { get; set; }
        public int QueryErrors { get; set; }
        public int TimeoutErrors { get; set; }
        public int RetrySuccesses { get; set; }
        public int RetryFailures { get; set; }
        public string LastErrorMessage { get; set; } = string.Empty;
        
        public TimeSpan TimeSinceLastError => DateTime.Now - LastErrorTime;
        
        public override string ToString()
        {
            return $"错误统计: 当前重试次数={CurrentRetryCount}, 最大重试次数={MaxRetryCount}, " +
                   $"最后错误时间={LastErrorTime:yyyy-MM-dd HH:mm:ss}, 距离最后错误={TimeSinceLastError:hh\\:mm\\:ss}, " +
                   $"健康状态={(IsHealthy ? "健康" : "异常")}";
        }
    }

    public class ConnectionHealthChecker
    {
        private readonly ErrorHandler _errorHandler;
        private readonly TimeSpan _checkInterval;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _healthCheckTask;
        private bool _isRunning;

        public event EventHandler<ConnectionHealthChangedEventArgs> ConnectionHealthChanged;

        public ConnectionHealthChecker(ErrorHandler errorHandler, TimeSpan checkInterval)
        {
            _errorHandler = errorHandler;
            _checkInterval = checkInterval;
            _isRunning = false;
        }

        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _healthCheckTask = Task.Run(() => HealthCheckLoop(_cancellationTokenSource.Token));
            
            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 连接健康检查已启动，检查间隔: {_checkInterval.TotalSeconds}秒");
        }

        public void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            _healthCheckTask?.Wait(TimeSpan.FromSeconds(5));
            
            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 连接健康检查已停止");
        }

        private async Task HealthCheckLoop(CancellationToken cancellationToken)
        {
            bool lastHealthStatus = true;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    bool isHealthy = await _errorHandler.CheckConnectionHealthAsync();
                    
                    if (isHealthy != lastHealthStatus)
                    {
                        lastHealthStatus = isHealthy;
                        OnConnectionHealthChanged(new ConnectionHealthChangedEventArgs(isHealthy));
                        
                        if (isHealthy)
                        {
                            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 连接健康检查: 连接已恢复");
                            _errorHandler.ResetErrorStatistics();
                        }
                        else
                        {
                            LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 连接健康检查: 连接异常");
                        }
                    }
                    
                    var stats = _errorHandler.GetErrorStatistics();
                    if (!stats.IsHealthy)
                    {
                        LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 连接健康检查: {stats}");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 连接健康检查异常: {ex.Message}");
                }

                try
                {
                    await Task.Delay(_checkInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        protected virtual void OnConnectionHealthChanged(ConnectionHealthChangedEventArgs e)
        {
            ConnectionHealthChanged?.Invoke(this, e);
        }
    }

    public class ConnectionHealthChangedEventArgs : EventArgs
    {
        public bool IsHealthy { get; }

        public ConnectionHealthChangedEventArgs(bool isHealthy)
        {
            IsHealthy = isHealthy;
        }
    }
}
