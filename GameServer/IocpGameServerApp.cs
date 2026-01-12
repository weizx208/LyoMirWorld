using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer
{
    
    
    
    public class IocpGameServerApp : IDisposable
    {
        #region 字段
        private readonly IocpNetworkEngine _networkEngine;
        private readonly ConcurrentDictionary<uint, IocpGameClient> _clients = new ConcurrentDictionary<uint, IocpGameClient>();
        private readonly ConcurrentDictionary<uint, MirCommon.EnterGameServer> _enterInfoDict = new ConcurrentDictionary<uint, MirCommon.EnterGameServer>();
        private readonly GameWorld _gameWorld;
        private readonly string _serverName;
        private readonly string _dbServerAddress;
        private readonly int _dbServerPort;
        private readonly Timer _updateTimer;
        private volatile bool _isRunning = false;
        private uint _nextClientId = 1;
        #endregion

        #region 属性
        
        
        
        public string GetServerName() => _serverName;

        
        
        
        public GameWorld GetGameWorld() => _gameWorld;
        #endregion

        #region 构造函数
        
        
        
        public IocpGameServerApp(string serverName, string dbServerAddress, int dbServerPort)
        {
            _serverName = serverName;
            _dbServerAddress = dbServerAddress;
            _dbServerPort = dbServerPort;
            
            _networkEngine = new IocpNetworkEngine();
            _gameWorld = GameWorld.Instance;
            
            
            _updateTimer = new Timer(UpdateCallback, null, Timeout.Infinite, Timeout.Infinite);
            
            
            _networkEngine.OnConnection += OnNewConnection;
            _networkEngine.OnDisconnection += OnDisconnection;
            _networkEngine.OnDataReceived += OnDataReceived;
            _networkEngine.OnError += OnNetworkError;
        }
        #endregion

        #region 公共方法
        
        
        
        public bool Start(string ipAddress, int port)
        {
            try
            {
                LogManager.Default.Info($"启动IOCP游戏服务器: {_serverName} ({ipAddress}:{port})");
                
                
                if (!_networkEngine.Start())
                {
                    LogManager.Default.Error("启动网络引擎失败");
                    return false;
                }
                
                
                if (!_networkEngine.StartListen(ipAddress, port, 64, 0))
                {
                    LogManager.Default.Error($"开始监听端口失败: {port}");
                    _networkEngine.Stop();
                    return false;
                }
                
                
                if (!_gameWorld.Initialize())
                {
                    LogManager.Default.Error("初始化游戏世界失败");
                    _networkEngine.Stop();
                    return false;
                }
                
                
                _updateTimer.Change(0, 16); 
                _isRunning = true;
                
                LogManager.Default.Info($"IOCP游戏服务器已启动: {_serverName}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"启动服务器失败: {ex.Message}");
                return false;
            }
        }

        
        
        
        public void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            
            
            _updateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            
            foreach (var client in _clients.Values)
            {
                try
                {
                    client.Disconnect();
                }
                catch { }
            }
            _clients.Clear();
            
            
            _networkEngine.Stop();
            
            
            
            
            LogManager.Default.Info($"IOCP游戏服务器已停止: {_serverName}");
        }

        
        
        
        public MirCommon.EnterGameServer? GetEnterInfo(uint loginId)
        {
            if (_enterInfoDict.TryGetValue(loginId, out var enterInfo))
            {
                return enterInfo;
            }
            return null;
        }

        
        
        
        public void RemoveEnterInfo(uint loginId)
        {
            _enterInfoDict.TryRemove(loginId, out _);
        }

        
        
        
        public void AddEnterInfo(uint loginId, MirCommon.EnterGameServer enterInfo)
        {
            _enterInfoDict[loginId] = enterInfo;
            LogManager.Default.Info($"添加进入信息: 登录ID={loginId}, 账号={enterInfo.GetAccount()}, 角色名={enterInfo.GetName()}");
        }

        
        
        
        public object GetDbServerClient()
        {
            
            return new object();
        }

        
        
        
        public (int clientCount, long recvBytes, long sendBytes) GetStatistics()
        {
            var stats = _networkEngine.GetStatistics();
            return (_clients.Count, stats.recvBytes, stats.sendBytes);
        }
        #endregion

        #region 私有方法
        
        
        
        private void OnNewConnection(Socket socket, uint listenerId)
        {
            try
            {
                var clientId = _nextClientId++;
                var client = new IocpGameClient(socket, this, _gameWorld, _dbServerAddress, _dbServerPort);
                
                if (_clients.TryAdd(clientId, client))
                {
                    LogManager.Default.Info($"新客户端连接: ID={clientId}, 远程地址={socket.RemoteEndPoint}");
                    
                    
                    Task.Run(async () =>
                    {
                        try
                        {
                            await client.ProcessAsync();
                        }
                        catch (Exception ex)
                        {
                            LogManager.Default.Error($"客户端处理异常 (ID={clientId}): {ex.Message}");
                        }
                        finally
                        {
                            
                            _clients.TryRemove(clientId, out _);
                            LogManager.Default.Info($"客户端断开连接: ID={clientId}");
                        }
                    });
                }
                else
                {
                    LogManager.Default.Error($"添加客户端到字典失败: ID={clientId}");
                    socket.Close();
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理新连接失败: {ex.Message}");
                try { socket.Close(); } catch { }
            }
        }

        
        
        
        private void OnDisconnection(Socket socket)
        {
            
            LogManager.Default.Debug($"Socket断开连接: {socket.RemoteEndPoint}");
        }

        
        
        
        private void OnDataReceived(Socket socket, byte[] data, int length)
        {
            
            LogManager.Default.Debug($"接收到数据: {socket.RemoteEndPoint}, 长度={length}");
        }

        
        
        
        private void OnNetworkError(Socket socket, Exception exception)
        {
            LogManager.Default.Error($"网络错误: {socket.RemoteEndPoint}, {exception.Message}");
        }

        
        
        
        private void UpdateCallback(object state)
        {
            try
            {
                if (!_isRunning) return;
                
                
                _networkEngine.Update();
                
                
                _gameWorld.Update();
                
                
                foreach (var client in _clients.Values)
                {
                    try
                    {
                        
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"更新客户端失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"更新回调异常: {ex.Message}");
            }
        }
        #endregion

        #region IDisposable实现
        public void Dispose()
        {
            Stop();
            _updateTimer?.Dispose();
            _networkEngine?.Dispose();
            
        }
        #endregion
    }
}
