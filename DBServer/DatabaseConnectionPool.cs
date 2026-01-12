using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using MirCommon.Utils;

namespace DBServer
{
    public class DatabaseConnectionPool : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _maxConnections;
        private readonly ConcurrentBag<SqlConnection> _connections;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer _healthCheckTimer;
        private readonly object _lock = new();
        private bool _disposed;
        private int _activeConnections;
        
        private long _totalConnectionsCreated;
        private long _totalConnectionsReused;
        private long _totalConnectionErrors;
        private DateTime _startTime;
        
        public DatabaseConnectionPool(string connectionString, int maxConnections = 100)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _maxConnections = maxConnections;
            _connections = new ConcurrentBag<SqlConnection>();
            _semaphore = new SemaphoreSlim(maxConnections, maxConnections);
            _activeConnections = 0;
            _totalConnectionsCreated = 0;
            _totalConnectionsReused = 0;
            _totalConnectionErrors = 0;
            _startTime = DateTime.Now;
            
            _healthCheckTimer = new Timer(HealthCheckCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            
            LogManager.Default.Info($"数据库连接池初始化完成，最大连接数: {maxConnections}");
        }
        
        public async Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            try
            {
                Interlocked.Increment(ref _activeConnections);
                
                if (_connections.TryTake(out var connection))
                {
                    Interlocked.Increment(ref _totalConnectionsReused);
                    
                    if (connection.State == ConnectionState.Open)
                    {
                        return connection;
                    }
                    else
                    {
                        connection.Dispose();
                        Interlocked.Decrement(ref _totalConnectionsReused);
                    }
                }
                
                Interlocked.Increment(ref _totalConnectionsCreated);
                connection = new SqlConnection(_connectionString);
                
                try
                {
                    await connection.OpenAsync(cancellationToken);
                    LogManager.Default.Debug($"创建新的数据库连接，当前活跃连接数: {_activeConnections}");
                    return connection;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _totalConnectionErrors);
                    LogManager.Default.Error($"创建数据库连接失败: {ex.Message}");
                    connection.Dispose();
                    throw;
                }
            }
            catch
            {
                Interlocked.Decrement(ref _activeConnections);
                _semaphore.Release();
                throw;
            }
        }
        
        public void ReleaseConnection(SqlConnection connection)
        {
            if (connection == null)
                return;
                
            try
            {
                if (connection.State == ConnectionState.Open)
                {
                    _connections.Add(connection);
                }
                else
                {
                    connection.Dispose();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeConnections);
                _semaphore.Release();
            }
        }
        
        public async Task<T> ExecuteWithConnectionAsync<T>(
            Func<SqlConnection, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            var connection = await GetConnectionAsync(cancellationToken);
            
            try
            {
                return await operation(connection);
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }
        
        public async Task ExecuteWithConnectionAsync(
            Func<SqlConnection, Task> operation,
            CancellationToken cancellationToken = default)
        {
            var connection = await GetConnectionAsync(cancellationToken);
            
            try
            {
                await operation(connection);
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }
        
        private void HealthCheckCallback(object state)
        {
            try
            {
                lock (_lock)
                {
                    var connectionsToRemove = new List<SqlConnection>();
                    
                    while (_connections.TryTake(out var connection))
                    {
                        try
                        {
                            using (var cmd = new SqlCommand("SELECT 1", connection))
                            {
                                if (connection.State != ConnectionState.Open)
                                {
                                    connectionsToRemove.Add(connection);
                                    continue;
                                }
                                
                                var result = cmd.ExecuteScalar();
                                if (result == null || (int)result != 1)
                                {
                                    connectionsToRemove.Add(connection);
                                    continue;
                                }
                            }
                            
                            _connections.Add(connection);
                        }
                        catch
                        {
                            connectionsToRemove.Add(connection);
                        }
                    }
                    
                    foreach (var connection in connectionsToRemove)
                    {
                        connection.Dispose();
                    }
                    
                    if (connectionsToRemove.Count > 0)
                    {
                        LogManager.Default.Warning($"数据库连接池健康检查移除了 {connectionsToRemove.Count} 个不健康的连接");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"数据库连接池健康检查失败: {ex.Message}");
            }
        }
        
        public ConnectionPoolStats GetStats()
        {
            return new ConnectionPoolStats
            {
                TotalConnectionsCreated = _totalConnectionsCreated,
                TotalConnectionsReused = _totalConnectionsReused,
                TotalConnectionErrors = _totalConnectionErrors,
                ActiveConnections = _activeConnections,
                AvailableConnections = _connections.Count,
                MaxConnections = _maxConnections,
                Uptime = DateTime.Now - _startTime
            };
        }
        
        public void Clear()
        {
            lock (_lock)
            {
                while (_connections.TryTake(out var connection))
                {
                    connection.Dispose();
                }
                
                LogManager.Default.Info("数据库连接池已清理");
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _healthCheckTimer?.Dispose();
                Clear();
                _semaphore?.Dispose();
                
                LogManager.Default.Info("数据库连接池已释放");
            }
        }
    }
    
    public class ConnectionPoolStats
    {
        public long TotalConnectionsCreated { get; set; }
        public long TotalConnectionsReused { get; set; }
        public long TotalConnectionErrors { get; set; }
        public int ActiveConnections { get; set; }
        public int AvailableConnections { get; set; }
        public int MaxConnections { get; set; }
        public TimeSpan Uptime { get; set; }
        
        public double ConnectionReuseRate => TotalConnectionsCreated > 0 
            ? (double)TotalConnectionsReused / TotalConnectionsCreated 
            : 0;
            
        public double ErrorRate => TotalConnectionsCreated > 0 
            ? (double)TotalConnectionErrors / TotalConnectionsCreated 
            : 0;
            
        public override string ToString()
        {
            return $"连接池统计: 创建={TotalConnectionsCreated}, 重用={TotalConnectionsReused}, " +
                   $"活跃={ActiveConnections}, 可用={AvailableConnections}, 最大={MaxConnections}, " +
                   $"运行时间={Uptime:hh\\:mm\\:ss}, 重用率={ConnectionReuseRate:P2}, 错误率={ErrorRate:P2}";
        }
    }
    
    public class QueryCacheManager
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly TimeSpan _defaultExpiration;
        private readonly Timer _cleanupTimer;
        private readonly object _lock = new();
        
        public QueryCacheManager(TimeSpan defaultExpiration = default)
        {
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _defaultExpiration = defaultExpiration == default ? TimeSpan.FromMinutes(5) : defaultExpiration;
            
            _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
        
        public bool TryGet<T>(string key, out T value)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpirationTime > DateTime.Now)
                {
                    value = (T)entry.Value;
                    entry.LastAccessTime = DateTime.Now;
                    return true;
                }
                else
                {
                    _cache.TryRemove(key, out _);
                }
            }
            
            value = default;
            return false;
        }
        
        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var entry = new CacheEntry
            {
                Key = key,
                Value = value,
                CreationTime = DateTime.Now,
                LastAccessTime = DateTime.Now,
                ExpirationTime = DateTime.Now + (expiration ?? _defaultExpiration)
            };
            
            _cache[key] = entry;
        }
        
        public bool Remove(string key)
        {
            return _cache.TryRemove(key, out _);
        }
        
        private void CleanupCallback(object state)
        {
            try
            {
                var now = DateTime.Now;
                var keysToRemove = new List<string>();
                
                foreach (var kvp in _cache)
                {
                    if (kvp.Value.ExpirationTime <= now)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    _cache.TryRemove(key, out _);
                }
                
                if (keysToRemove.Count > 0)
                {
                    LogManager.Default.Debug($"查询缓存清理了 {keysToRemove.Count} 个过期条目");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"查询缓存清理失败: {ex.Message}");
            }
        }
        
        public CacheStats GetStats()
        {
            return new CacheStats
            {
                TotalEntries = _cache.Count,
                MemoryUsage = CalculateMemoryUsage()
            };
        }
        
        private long CalculateMemoryUsage()
        {
            long total = 0;
            foreach (var kvp in _cache)
            {
                total += EstimateSize(kvp.Value.Value);
            }
            return total;
        }
        
        private long EstimateSize(object obj)
        {
            if (obj == null) return 0;
            
            if (obj is string str)
            {
                return str.Length * 2 + 20;
            }
            
            if (obj is Array array)
            {
                long size = 20; 
                if (array.Length > 0)
                {
                    var element = array.GetValue(0);
                    size += array.Length * EstimateSize(element);
                }
                return size;
            }
            
            return 50;
        }
        
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _cache.Clear();
        }
        
        private class CacheEntry
        {
            public string Key { get; set; }
            public object Value { get; set; }
            public DateTime CreationTime { get; set; }
            public DateTime LastAccessTime { get; set; }
            public DateTime ExpirationTime { get; set; }
        }
    }
    
    public class CacheStats
    {
        public int TotalEntries { get; set; }
        public long MemoryUsage { get; set; }
        
        public override string ToString()
        {
            return $"缓存统计: 条目数={TotalEntries}, 内存使用={MemoryUsage / 1024}KB";
        }
    }
    
    public class BatchOperationManager
    {
        private readonly DatabaseConnectionPool _connectionPool;
        private readonly List<BatchOperation> _operations;
        private readonly object _lock = new();
        private readonly Timer _flushTimer;
        private readonly int _batchSize;
        
        public BatchOperationManager(DatabaseConnectionPool connectionPool, int batchSize = 100)
        {
            _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
            _operations = new List<BatchOperation>();
            _batchSize = batchSize;
            
            _flushTimer = new Timer(FlushCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }
        
        public void AddOperation(string sql, params SqlParameter[] parameters)
        {
            lock (_lock)
            {
                _operations.Add(new BatchOperation
                {
                    Sql = sql,
                    Parameters = parameters?.ToList() ?? new List<SqlParameter>(),
                    Timestamp = DateTime.Now
                });
                
                if (_operations.Count >= _batchSize)
                {
                    Task.Run(() => ExecuteBatchAsync());
                }
            }
        }
        
        private async Task ExecuteBatchAsync()
        {
            List<BatchOperation> operationsToExecute;
            
            lock (_lock)
            {
                if (_operations.Count == 0)
                    return;
                    
                operationsToExecute = new List<BatchOperation>(_operations);
                _operations.Clear();
            }
            
            try
            {
                await _connectionPool.ExecuteWithConnectionAsync(async connection =>
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var operation in operationsToExecute)
                            {
                                using (var cmd = new SqlCommand(operation.Sql, connection, transaction))
                                {
                                    cmd.Parameters.AddRange(operation.Parameters.ToArray());
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                            
                            await transaction.CommitAsync();
                            LogManager.Default.Debug($"批量操作执行成功，处理了 {operationsToExecute.Count} 个操作");
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            LogManager.Default.Error($"批量操作执行失败: {ex.Message}");
                            throw;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"批量操作执行异常: {ex.Message}");
                
                lock (_lock)
                {
                    _operations.InsertRange(0, operationsToExecute);
                }
            }
        }
        
        private void FlushCallback(object state)
        {
            Task.Run(() => ExecuteBatchAsync());
        }
        
        public async Task FlushAsync()
        {
            await ExecuteBatchAsync();
        }
        
        public BatchStats GetStats()
        {
            lock (_lock)
            {
                return new BatchStats
                {
                    PendingOperations = _operations.Count,
                    BatchSize = _batchSize,
                    LastFlushTime = _operations.Count > 0 ? _operations[0].Timestamp : DateTime.MinValue
                };
            }
        }
        
        public void Dispose()
        {
            _flushTimer?.Dispose();
            Task.Run(async () => await FlushAsync()).Wait();
        }
        
        private class BatchOperation
        {
            public string Sql { get; set; }
            public List<SqlParameter> Parameters { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
    
    public class BatchStats
    {
        public int PendingOperations { get; set; }
        public int BatchSize { get; set; }
        public DateTime LastFlushTime { get; set; }
        
        public override string ToString()
        {
            return $"批量操作统计: 待处理={PendingOperations}, 批量大小={BatchSize}, 最后刷新时间={LastFlushTime:yyyy-MM-dd HH:mm:ss}";
        }
    }
}
