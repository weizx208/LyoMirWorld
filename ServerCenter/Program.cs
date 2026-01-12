using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;

namespace ServerCenter
{
    class Program
    {
        private static ServerCenterApp? _server;
        private static bool _isRunning = true;

        static async Task Main(string[] args)
        {
            Console.Title = "MirWorld Server Center - C# 版本";
            Console.WriteLine("===========================================");
            Console.WriteLine("   传世服务器中心 - C# 版本");
            Console.WriteLine("===========================================");
            Console.WriteLine();

            try
            {
                
                var iniReader = new IniFileReader("config.ini");
                if (!iniReader.Open())
                {
                    Console.WriteLine("无法打开配置文件 config.ini");
                    return;
                }

                
                var logger = new Logger("logs", true, true);
                LogManager.SetDefaultLogger(logger);

                _server = new ServerCenterApp(iniReader);

                if (await _server.Initialize())
                {
                    LogManager.Default.Info("服务器中心初始化成功");
                    await _server.Start();
                    _ = Task.Run(() => CommandLoop());
                    
                    while (_isRunning)
                    {
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Fatal("严重错误", exception: ex);
            }
            finally
            {
                _server?.Stop();
                LogManager.Shutdown();
            }
        }

        private static void CommandLoop()
        {
            Console.WriteLine("输入命令 (help/list/exit):");
            while (_isRunning)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                switch (input.Trim().ToLower())
                {
                    case "help":
                        Console.WriteLine("命令列表:");
                        Console.WriteLine("  help   - 显示帮助");
                        Console.WriteLine("  list   - 显示所有注册的服务器");
                        Console.WriteLine("  status - 显示服务器状态");
                        Console.WriteLine("  clear  - 清屏");
                        Console.WriteLine("  exit   - 退出");
                        break;
                    case "list":
                        _server?.ListServers();
                        break;
                    case "status":
                        _server?.ShowStatus();
                        break;
                    case "clear":
                        Console.Clear();
                        break;
                    case "exit":
                    case "quit":
                        _isRunning = false;
                        break;
                }
            }
        }
    }

    
    
    
    public class RegisteredServer
    {
        public MirCommon.ServerId Id { get; set; } = new MirCommon.ServerId();
        public MirCommon.ServerAddr Addr { get; set; } = new MirCommon.ServerAddr();
        public string Name { get; set; } = string.Empty;
        public uint Connections { get; set; }
        public int SendDbCount { get; set; }
        public DateTime RegisterTime { get; set; }
    }

    
    
    
    public class ServerCenterApp
    {
        private readonly IniFileReader _config;
        private TcpListener? _listener;
        private readonly List<ServerCenterClient> _clients = new();
        private readonly object _clientLock = new();
        
        
        private readonly Dictionary<ServerType, List<uint>> _serverArrays = new();
        private readonly Dictionary<ServerType, int> _pickPointers = new();
        
        private DateTime _startTime;
        private bool _isRunning = false;
        private int _port = 6000;
        private int _maxConnections = 1024;
        private string _address = "127.0.0.1";

        public ServerCenterApp(IniFileReader config)
        {
            _config = config;
            
            
            foreach (ServerType type in Enum.GetValues(typeof(ServerType)))
            {
                if (type != ServerType.ST_UNKNOWN)
                {
                    _serverArrays[type] = new List<uint>();
                    _pickPointers[type] = 0;
                }
            }
        }

        public async Task<bool> Initialize()
        {
            try
            {
                
                string sectionName = "服务器中心";
                _address = _config.GetString(sectionName, "addr", "127.0.0.1");
                _port = _config.GetInteger(sectionName, "port", 6000);
                _maxConnections = _config.GetInteger(sectionName, "maxconnection", 1024);
                
                LogManager.Default.Info($"服务器地址: {_address}");
                LogManager.Default.Info($"监听端口: {_port}");
                LogManager.Default.Info($"最大连接: {_maxConnections}");
                
                _startTime = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("初始化失败", exception: ex);
                return false;
            }
        }

        public async Task Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;

            LogManager.Default.Info("服务器中心已启动");

            _ = Task.Run(async () =>
            {
                while (_isRunning)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleClient(client));
                    }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                            LogManager.Default.Error($"接受连接错误: {ex.Message}");
                    }
                }
            });
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            lock (_clientLock)
            {
                _clients.ForEach(c => c.Disconnect());
                _clients.Clear();
            }
        }

        private async Task HandleClient(TcpClient tcpClient)
        {
            var client = new ServerCenterClient(tcpClient, this);

            lock (_clientLock)
            {
                if (_clients.Count >= _maxConnections)
                {
                    tcpClient.Close();
                    return;
                }
                _clients.Add(client);
            }

            LogManager.Default.Info($"新服务器连接: {tcpClient.Client.RemoteEndPoint}");

            try
            {
                await client.ProcessAsync();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理服务器错误: {ex.Message}");
            }
            finally
            {
                UnregisterServer(client);
                lock (_clientLock)
                {
                    _clients.Remove(client);
                }
            }
        }

        public bool RegisterServer(ServerCenterClient client, MirCommon.RegisterServerInfo info, out MirCommon.RegisterServerResult result)
        {
            result = new MirCommon.RegisterServerResult();

            ServerType type = (ServerType)info.Id.bType;
            if (!IsValidServerType(type))
            {
                LogManager.Default.Warning($"无效的服务器类型: {type}");
                return false;
            }

            uint clientId = client.GetId();

            lock (_serverArrays)
            {
                if (!_serverArrays[type].Contains(clientId))
                {
                    _serverArrays[type].Add(clientId);
                }
            }

            var regInfo = client.GetRegInfo();
            var serverId = info.Id;
            serverId.bIndex = (byte)clientId;
            regInfo.Id = serverId;
            
            
            
            var cleanAddr = new ServerAddr();
            cleanAddr.nPort = info.addr.nPort;
            
            
            string addressStr = info.addr.GetAddress();
            if (string.IsNullOrEmpty(addressStr))
            {
                
                byte[] addrBytes = info.addr.addr;
                int nullIndex = Array.IndexOf(addrBytes, (byte)0);
                if (nullIndex < 0) nullIndex = addrBytes.Length;
                if (nullIndex > 0)
                {
                    addressStr = System.Text.Encoding.GetEncoding("GBK").GetString(addrBytes, 0, Math.Min(nullIndex, 16));
                }
                else
                {
                    addressStr = "127.0.0.1"; 
                }
            }
            
            
            cleanAddr.SetAddress(addressStr);
            
            regInfo.Addr = cleanAddr;
            regInfo.Name = System.Text.Encoding.GetEncoding("GBK").GetString(info.szName).TrimEnd('\0');
            regInfo.Connections = 0;
            regInfo.RegisterTime = DateTime.Now;

            LogManager.Default.Info($"[注册] {GetServerTypeName(type)} - {regInfo.Name} ({info.addr.addr}:{info.addr.nPort})");
            LogManager.Default.Info($"[注册调试] 端口值: nPort={info.addr.nPort}, 地址: {addressStr}");
            LogManager.Default.Info($"[注册调试] 设置服务器索引: clientId={clientId}, bIndex={(byte)clientId}, Id.bType={regInfo.Id.bType}, Id.bGroup={regInfo.Id.bGroup}, Id.bIndex={regInfo.Id.bIndex}");

            result.Id = regInfo.Id;

            
            if (type != ServerType.ST_DATABASESERVER)
            {
                int dbCount = type == ServerType.ST_GAMESERVER ? 2 : 1;
                var dbServers = PrepareServers(ServerType.ST_DATABASESERVER, dbCount);
                result.nDbCount = dbServers.Length;
                result.DbAddr = dbServers;
                regInfo.SendDbCount = result.nDbCount;
            }
            else
            {
                
                var dbServers = PrepareServers(ServerType.ST_DATABASESERVER, 2);
                if (dbServers.Length == 2)
                {
                    SendDbServerToAll(dbServers);
                }
            }

            return true;
        }

        public void UnregisterServer(ServerCenterClient client)
        {
            var regInfo = client.GetRegInfo();
            if (regInfo.Id.dwId == 0) return;

            ServerType type = (ServerType)regInfo.Id.bType;
            uint clientId = client.GetId();

            lock (_serverArrays)
            {
                if (_serverArrays.ContainsKey(type))
                {
                    _serverArrays[type].Remove(clientId);
                }
            }

            LogManager.Default.Info($"[注销] {GetServerTypeName(type)} - {regInfo.Name}");
        }

        public bool FindServer(ServerType type, string name, out MirCommon.FindServerResult result)
        {
            result = new MirCommon.FindServerResult();

            if (!IsValidServerType(type))
                return false;

            lock (_clientLock)
            {
                lock (_serverArrays)
                {
                    if (!_serverArrays.ContainsKey(type))
                        return false;

                    foreach (uint id in _serverArrays[type])
                    {
                        var client = _clients.FirstOrDefault(c => c.GetId() == id);
                        if (client != null)
                        {
                            var info = client.GetRegInfo();
                            if (info.Name == name)
                            {
                                result.addr = info.Addr;
                                result.Id = info.Id;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private MirCommon.ServerAddr[] PrepareServers(ServerType type, int count)
        {
            var result = new List<MirCommon.ServerAddr>();

            lock (_serverArrays)
            {
                if (!_serverArrays.ContainsKey(type) || _serverArrays[type].Count == 0)
                    return result.ToArray();

                var servers = _serverArrays[type];
                int pickPtr = _pickPointers[type];

                for (int i = 0; i < count && i < servers.Count; i++)
                {
                    if (pickPtr < 0 || pickPtr >= servers.Count)
                        pickPtr = 0;

                    uint id = servers[pickPtr];
                    var client = _clients.FirstOrDefault(c => c.GetId() == id);
                    if (client != null)
                    {
                        result.Add(client.GetRegInfo().Addr);
                    }

                    pickPtr++;
                }

                _pickPointers[type] = pickPtr;
            }

            return result.ToArray();
        }

        private void SendDbServerToAll(MirCommon.ServerAddr[] dbServers)
        {
            if (dbServers.Length == 0) return;

            var result = new MirCommon.RegisterServerResult
            {
                DbAddr = dbServers,
                nDbCount = dbServers.Length
            };

            lock (_clientLock)
            {
                foreach (var client in _clients.ToList())
                {
                    var info = client.GetRegInfo();
                    ServerType type = (ServerType)info.Id.bType;
                    
                    if (type != ServerType.ST_UNKNOWN && 
                        type != ServerType.ST_DATABASESERVER && 
                        info.SendDbCount == 0)
                    {
                        result.Id = info.Id;
                        result.nDbCount = type == ServerType.ST_GAMESERVER ? 
                            Math.Min(2, dbServers.Length) : 1;
                        
                        info.SendDbCount = result.nDbCount;
                        client.SendRegisterResult(result);
                    }
                }
            }
        }

        private bool IsValidServerType(ServerType type)
        {
            return type > ServerType.ST_UNKNOWN && type <= ServerType.ST_GAMESERVER;
        }

        private string GetServerTypeName(ServerType type)
        {
            return type switch
            {
                ServerType.ST_DATABASESERVER => "数据库服务器",
                ServerType.ST_LOGINSERVER => "登录服务器",
                ServerType.ST_SELCHARSERVER => "选人服务器",
                ServerType.ST_GAMESERVER => "游戏服务器",
                _ => "未知服务器"
            };
        }

        public void ListServers()
        {
            Console.WriteLine("\n========== 已注册服务器 ==========");
            
            lock (_clientLock)
            {
                lock (_serverArrays)
                {
                    int totalCount = 0;
                    foreach (var kvp in _serverArrays.OrderBy(x => x.Key))
                    {
                        if (kvp.Value.Count > 0)
                        {
                            Console.WriteLine($"\n{GetServerTypeName(kvp.Key)} ({kvp.Value.Count}):");
                            foreach (uint id in kvp.Value)
                            {
                                var client = _clients.FirstOrDefault(c => c.GetId() == id);
                                if (client != null)
                                {
                                    var info = client.GetRegInfo();
                                    Console.WriteLine($"  [{info.Id.bIndex}] {info.Name} - {info.Addr.addr}:{info.Addr.nPort} (运行: {DateTime.Now - info.RegisterTime:hh\\:mm\\:ss})");
                                    totalCount++;
                                }
                            }
                        }
                    }
                    Console.WriteLine($"\n总计: {totalCount} 个服务器");
                }
            }
            Console.WriteLine("================================\n");
        }

        public void ShowStatus()
        {
            Console.WriteLine($"运行时间: {DateTime.Now - _startTime}");
            Console.WriteLine($"连接数: {_clients.Count}");
            
            lock (_serverArrays)
            {
                foreach (var kvp in _serverArrays)
                {
                    if (kvp.Value.Count > 0)
                    {
                        Console.WriteLine($"{GetServerTypeName(kvp.Key)}: {kvp.Value.Count}");
                    }
                }
            }
        }

        
        
        
        public ServerCenterClient? GetClientById(uint id)
        {
            lock (_clientLock)
            {
                return _clients.FirstOrDefault(c => c.GetId() == id);
            }
        }

        
        
        
        public ServerCenterClient? GetClientByIndex(byte index)
        {
            lock (_clientLock)
            {
                return _clients.FirstOrDefault(c => c.GetRegInfo().Id.bIndex == index);
            }
        }

        
        
        
        public List<ServerCenterClient> GetClientsByGroup(ushort groupId)
        {
            var result = new List<ServerCenterClient>();
            
            lock (_clientLock)
            {
                foreach (var client in _clients)
                {
                    var info = client.GetRegInfo();
                    if (info.Id.bGroup == groupId)
                    {
                        result.Add(client);
                    }
                }
            }
            
            return result;
        }

        
        
        
        public List<ServerCenterClient> GetClientsByType(ServerType serverType)
        {
            var result = new List<ServerCenterClient>();
            
            lock (_clientLock)
            {
                lock (_serverArrays)
                {
                    if (_serverArrays.ContainsKey(serverType))
                    {
                        foreach (uint id in _serverArrays[serverType])
                        {
                            var client = _clients.FirstOrDefault(c => c.GetId() == id);
                            if (client != null)
                            {
                                result.Add(client);
                            }
                        }
                    }
                }
            }
            
            return result;
        }
    }

    
    
    
    public class ServerCenterClient
    {
        private readonly TcpClient _client;
        private readonly ServerCenterApp _server;
        private readonly NetworkStream _stream;
        private readonly RegisteredServer _regInfo = new();
        private uint _id;
        private static uint _nextId = 1;

        public ServerCenterClient(TcpClient client, ServerCenterApp server)
        {
            _client = client;
            _server = server;
            _stream = client.GetStream();
            _id = Interlocked.Increment(ref _nextId);
        }

        public uint GetId() => _id;
        public RegisteredServer GetRegInfo() => _regInfo;

        public async Task ProcessAsync()
        {
            byte[] buffer = new byte[8192];

            while (_client.Connected)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    await ProcessMessage(buffer, bytesRead);
                }
                catch { break; }
            }
        }

        private async Task ProcessMessage(byte[] data, int length)
        {
            if (length < 12) return;

            var reader = new PacketReader(data);
            uint dwFlag = reader.ReadUInt32();
            ushort wCmd = reader.ReadUInt16();
            ushort w1 = reader.ReadUInt16(); 
            ushort w2 = reader.ReadUInt16(); 
            ushort w3 = reader.ReadUInt16(); 
            byte[] payload = reader.ReadBytes(length - 12);

            
            LogManager.Default.Info($"[ServerCenter接收] 消息头: dwFlag={dwFlag}, wCmd={wCmd}, w1={w1}, w2={w2}, w3={w3}");
            LogManager.Default.Info($"[ServerCenter接收] 数据长度: {length}字节, 负载长度: {payload.Length}字节");

            switch (wCmd)
            {
                case ProtocolCmd.SCM_REGISTERSERVER:
                    await HandleRegister(payload);
                    break;
                case ProtocolCmd.SCM_FINDSERVER:
                    await HandleFindServer(payload);
                    break;
                case ProtocolCmd.SCM_GETGAMESERVERADDR:
                    await HandleGetGameServerAddr(dwFlag, w1, w2, w3, payload);
                    break;
                case ProtocolCmd.SCM_MSGACROSSSERVER:
                    await HandleMsgAcrossServer(dwFlag, w1, w2, w3, payload);
                    break;
            }
        }

        private async Task HandleRegister(byte[] data)
        {
            try
            {
                
                if (data.Length < Marshal.SizeOf<MirCommon.RegisterServerInfo>())
                {
                    LogManager.Default.Error($"注册数据长度不足: {data.Length}字节");
                    SendMessage(0, ProtocolCmd.SCM_REGISTERSERVER, 1, 0, 0, null);
                    return;
                }
                
                
                var info = BytesToStruct<MirCommon.RegisterServerInfo>(data);
                
                
                ServerType type = (ServerType)info.Id.bType;
                
                if (type == ServerType.ST_UNKNOWN)
                {
                    LogManager.Default.Error($"未知服务器类型: {info.Id.bType}");
                    SendMessage(0, ProtocolCmd.SCM_REGISTERSERVER, 1, 0, 0, null);
                    return;
                }

                if (_server.RegisterServer(this, info, out var result))
                {
                    
                    SendMessage(1, ProtocolCmd.SM_REGISTERSERVEROK, 0, 0, 0, result);
                }
                else
                {
                    
                    SendMessage(0, ProtocolCmd.SM_REGISTERSERVEROK, 0, 0, 0, null);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"注册失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SCM_REGISTERSERVER, 1, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleFindServer(byte[] data)
        {
            try
            {
                
                string requestData = System.Text.Encoding.GetEncoding("GBK").GetString(data).TrimEnd('\0');
                string[] parts = requestData.Split('/');
                
                if (parts.Length < 2)
                {
                    LogManager.Default.Error($"查找服务器请求格式错误: {requestData}");
                    SendMessage(0, ProtocolCmd.SCM_FINDSERVER, 1, 0, 0, null);
                    return;
                }
                
                string serverTypeStr = parts[0];
                string serverName = parts[1];
                
                
                ServerType type = serverTypeStr switch
                {
                    "DBServer" => ServerType.ST_DATABASESERVER,
                    "LoginServer" => ServerType.ST_LOGINSERVER,
                    "SelectCharServer" => ServerType.ST_SELCHARSERVER,
                    "GameServer" => ServerType.ST_GAMESERVER,
                    _ => ServerType.ST_UNKNOWN
                };
                
                if (type == ServerType.ST_UNKNOWN)
                {
                    LogManager.Default.Error($"未知服务器类型: {serverTypeStr}");
                    SendMessage(0, ProtocolCmd.SCM_FINDSERVER, 1, 0, 0, null);
                    return;
                }

                LogManager.Default.Info($"查找服务器: 类型={serverTypeStr}, 名称={serverName}");
                
                if (_server.FindServer(type, serverName, out var result))
                {
                    LogManager.Default.Info($"找到服务器: {serverName}, 索引={result.Id.bIndex}, bType={result.Id.bType}, bGroup={result.Id.bGroup}, dwId={result.Id.dwId}");
                    SendMessage(0, ProtocolCmd.SCM_FINDSERVER, 0, 0, 0, result);
                }
                else
                {
                    LogManager.Default.Warning($"未找到服务器: {serverTypeStr}/{serverName}");
                    SendMessage(0, ProtocolCmd.SCM_FINDSERVER, 1, 0, 0, null);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"查找服务器失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SCM_FINDSERVER, 1, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        
        
        
        private async Task HandleGetGameServerAddr(uint clientId, ushort w1, ushort w2, ushort w3, byte[] data)
        {
            try
            {
                
                
                LogManager.Default.Info($"收到获取游戏服务器地址请求: 客户端ID={clientId}, w1={w1}, w2={w2}, w3={w3}, 数据长度={data.Length}");
                
                string account;
                string charName;
                string mapName = "0"; 
                
                
                if (data.Length >= 64) 
                {
                    try
                    {
                        
                        var enterInfo = BytesToStruct<MirCommon.EnterGameServer>(data);
                        account = enterInfo.GetAccount();
                        charName = enterInfo.GetName();
                        
                        LogManager.Default.Info($"解析EnterGameServer结构体成功: 账号={account}, 角色={charName}");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"解析EnterGameServer结构体失败: {ex.Message}");
                        
                        string requestData = System.Text.Encoding.GetEncoding("GBK").GetString(data).TrimEnd('\0');
                        string[] parts = requestData.Split('/');
                        
                        if (parts.Length < 3)
                        {
                            LogManager.Default.Error($"获取游戏服务器地址请求格式错误: {requestData}");
                            SendMessage(0, ProtocolCmd.SCM_GETGAMESERVERADDR, (ushort)SERVER_ERROR.SE_FAIL, 0, 0, null);
                            return;
                        }
                        
                        account = parts[0];
                        charName = parts[1];
                        mapName = parts[2];
                    }
                }
                else
                {
                    
                    string requestData = System.Text.Encoding.GetEncoding("GBK").GetString(data).TrimEnd('\0');
                    if(requestData.Equals("heartbeat"))
                    {
                        LogManager.Default.Info($"GameServer心跳：{requestData}");
                        return;
                    }
                    string[] parts = requestData.Split('/');

                    if (parts.Length < 3)
                    {
                        LogManager.Default.Error($"获取游戏服务器地址请求格式错误: {requestData}");
                        SendMessage(0, ProtocolCmd.SCM_GETGAMESERVERADDR, (ushort)SERVER_ERROR.SE_FAIL, 0, 0, null);
                        return;
                    }

                    account = parts[0];
                    charName = parts[1];
                    mapName = parts[2];
                }
                
                LogManager.Default.Info($"获取游戏服务器地址: 账号={account}, 角色={charName}, 地图={mapName}");
                
                
                
                var gameServers = _server.GetClientsByType(ServerType.ST_GAMESERVER);
                if (gameServers.Count > 0)
                {
                    
                    var gameServer = gameServers[0];
                    var gameServerInfo = gameServer.GetRegInfo();
                    
                    
                    var findResult = new MirCommon.FindServerResult
                    {
                        Id = gameServerInfo.Id,
                        addr = gameServerInfo.Addr
                    };
                    
                    LogManager.Default.Info($"找到游戏服务器: {gameServerInfo.Name}, 地址={gameServerInfo.Addr.GetAddress()}:{gameServerInfo.Addr.nPort}, 索引={gameServerInfo.Id.bIndex}");
                    
                    
                    SendMessage(0, ProtocolCmd.SCM_GETGAMESERVERADDR, (ushort)SERVER_ERROR.SE_OK, 0, 0, findResult);
                }
                else
                {
                    LogManager.Default.Warning($"未找到可用的游戏服务器");
                    SendMessage(0, ProtocolCmd.SCM_GETGAMESERVERADDR, (ushort)SERVER_ERROR.SE_FAIL, 0, 0, null);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理获取游戏服务器地址请求失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SCM_GETGAMESERVERADDR, (ushort)SERVER_ERROR.SE_FAIL, 0, 0, null);
            }
            
            await Task.CompletedTask;
        }

        
        
        
        private async Task HandleMsgAcrossServer(uint dwFlag, ushort cmd, ushort w2, ushort targetIndex, byte[] data)
        {
            try
            {
                LogManager.Default.Info($"收到跨服务器消息: dwFlag={dwFlag}, 命令={cmd:X4}, w2={w2}, 目标索引={targetIndex}, 数据长度={data.Length}");
                
                
                
                byte senderIndex = (byte)dwFlag;
                
                
                var senderInfo = _regInfo;
                
                
                if (senderInfo.Id.bIndex != senderIndex)
                {
                    
                    
                    if (senderIndex == 0)
                    {
                        LogManager.Default.Info($"发送者索引为0，使用当前连接的注册信息: bType={senderInfo.Id.bType}, bIndex={senderInfo.Id.bIndex}, Name={senderInfo.Name}");
                    }
                    else
                    {
                        
                        var correctClient = _server.GetClientByIndex(senderIndex);
                        if (correctClient != null)
                        {
                            senderInfo = correctClient.GetRegInfo();
                            LogManager.Default.Info($"当前连接的bIndex({_regInfo.Id.bIndex})不匹配发送者索引({senderIndex})，已找到正确的发送者: bType={senderInfo.Id.bType}, bIndex={senderInfo.Id.bIndex}, Name={senderInfo.Name}");
                        }
                        else
                        {
                            LogManager.Default.Warning($"当前连接的bIndex({_regInfo.Id.bIndex})不匹配发送者索引({senderIndex})，且找不到bIndex={senderIndex}的发送者");
                            
                            
                            if (senderInfo.Id.bType == 0 && string.IsNullOrEmpty(senderInfo.Name))
                            {
                                LogManager.Default.Warning($"当前连接未注册，无法作为发送者");
                                return;
                            }
                            
                            
                            
                            var newId = senderInfo.Id;
                            newId.bIndex = senderIndex;
                            senderInfo.Id = newId;
                            LogManager.Default.Info($"使用当前连接作为发送者，更新bIndex为{senderIndex}");
                        }
                    }
                }
                
                
                LogManager.Default.Info($"发送者注册信息检查: dwId={senderInfo.Id.dwId}, bType={senderInfo.Id.bType}, bIndex={senderInfo.Id.bIndex}, Name={senderInfo.Name}");
                
                
                
                
                
                if (senderInfo.Id.bType == 0 && string.IsNullOrEmpty(senderInfo.Name))
                {
                    LogManager.Default.Warning($"当前连接未注册，无法作为发送者");
                    LogManager.Default.Warning($"注册信息: dwId={senderInfo.Id.dwId}, bType={senderInfo.Id.bType}, bIndex={senderInfo.Id.bIndex}, Name={senderInfo.Name}");
                    
                    
                    uint clientId = GetId();
                    LogManager.Default.Warning($"连接ID: {clientId}");
                    
                    
                    var serverApp = _server;
                    var clientFromApp = serverApp?.GetClientById(clientId);
                    if (clientFromApp != null)
                    {
                        var regInfoFromApp = clientFromApp.GetRegInfo();
                        LogManager.Default.Warning($"从ServerCenterApp获取的注册信息: dwId={regInfoFromApp.Id.dwId}, bType={regInfoFromApp.Id.bType}, bIndex={regInfoFromApp.Id.bIndex}, Name={regInfoFromApp.Name}");
                        
                        
                        if (regInfoFromApp.Id.bType != 0 || !string.IsNullOrEmpty(regInfoFromApp.Name))
                        {
                            LogManager.Default.Info($"使用从ServerCenterApp获取的注册信息作为发送者");
                            senderInfo = regInfoFromApp;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                
                
                LogManager.Default.Info($"发送者信息最终: dwId={senderInfo.Id.dwId}, bType={senderInfo.Id.bType}, bIndex={senderInfo.Id.bIndex}, Name={senderInfo.Name}");
                
                LogManager.Default.Info($"发送者信息: 类型={senderInfo.Id.bType}, 索引={senderInfo.Id.bIndex}, 名称={senderInfo.Name}");
                
                
                
                
                ushort sendType = ProtocolCmd.MST_SINGLE;
                byte wType = 0;
                
                
                
                byte decodedType = (byte)((w2 >> 4) & 0x0F);
                byte decodedIndex = (byte)(w2 & 0x0F);
                
                if (decodedType > 0 && decodedType <= 6 && decodedIndex < 10)
                {
                    
                    wType = (byte)w2;
                    sendType = ProtocolCmd.MST_SINGLE; 
                    LogManager.Default.Info($"w2参数解析为wType: 发送者类型={decodedType}, 发送者索引={decodedIndex}, wType={wType:X2}");
                }
                else if (w2 <= 2)
                {
                    
                    sendType = w2;
                    LogManager.Default.Info($"w2参数解析为发送类型: {sendType}");
                }
                else
                {
                    
                    LogManager.Default.Warning($"未知的w2参数值: {w2}, 使用默认的单播发送类型");
                    sendType = ProtocolCmd.MST_SINGLE;
                }
                
                
                switch (sendType)
                {
                    case ProtocolCmd.MST_SINGLE: 
                        await ForwardToSingleServer(senderInfo, cmd, targetIndex, data, wType);
                        break;
                    case ProtocolCmd.MST_GROUP: 
                        await ForwardToServerGroup(senderInfo, cmd, targetIndex, data, wType);
                        break;
                    case ProtocolCmd.MST_TYPE: 
                        await ForwardToServerType(senderInfo, cmd, targetIndex, data, wType);
                        break;
                    default:
                        LogManager.Default.Warning($"未知的发送类型: {sendType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理跨服务器消息失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        
        
        
        private async Task ForwardToSingleServer(RegisteredServer senderInfo, ushort cmd, ushort targetIndex, byte[] data, byte wType = 0)
        {
            try
            {
                
                var targetClient = _server.GetClientByIndex((byte)targetIndex);
                if (targetClient != null)
                {
                    
                    if (wType == 0)
                    {
                        
                        
                        
                        wType = (byte)(((senderInfo.Id.bType & 0x0F) << 4) | (senderInfo.Id.bIndex & 0x0F));
                    }
                    
                    
                    var builder = new PacketBuilder();
                    builder.WriteUInt32(0); 
                    builder.WriteUInt16(ProtocolCmd.SCM_MSGACROSSSERVER); 
                    builder.WriteUInt16(cmd); 
                    builder.WriteUInt16(wType); 
                    builder.WriteUInt16(targetIndex); 
                    builder.WriteBytes(data);

                    byte[] packet = builder.Build();
                    await targetClient.SendRawMessageAsync(packet);
                    
                    LogManager.Default.Info($"已转发消息到服务器索引: {targetIndex}, 原始命令={cmd:X4}, 转发命令={ProtocolCmd.SCM_MSGACROSSSERVER:X4}, 发送者类型={senderInfo.Id.bType}, 发送者索引={senderInfo.Id.bIndex}, wType={wType:X2}, 数据长度={data.Length}");
                    
                    
                    LogManager.Default.Debug($"转发消息包结构:");
                    LogManager.Default.Debug($"  - dwFlag: 0");
                    LogManager.Default.Debug($"  - wCmd: {ProtocolCmd.SCM_MSGACROSSSERVER:X4} (SCM_MSGACROSSSERVER)");
                    LogManager.Default.Debug($"  - w1: {cmd:X4} (原始命令: MAS_ENTERGAMESERVER)");
                    LogManager.Default.Debug($"  - w2: {wType:X2} (发送者类型={senderInfo.Id.bType}, 发送者索引={senderInfo.Id.bIndex})");
                    LogManager.Default.Debug($"  - w3: {targetIndex} (目标索引)");
                    LogManager.Default.Debug($"  - 数据长度: {data.Length}字节");
                }
                else
                {
                    LogManager.Default.Warning($"收到无效待转发消息，找不到目标服务器: 索引={targetIndex}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"转发到单个服务器失败: {ex.Message}");
            }
        }

        
        
        
        private async Task ForwardToServerGroup(RegisteredServer senderInfo, ushort cmd, ushort groupId, byte[] data, byte wType = 0)
        {
            try
            {
                
                var groupClients = _server.GetClientsByGroup(groupId);
                if (groupClients.Count > 0)
                {
                    
                    if (wType == 0)
                    {
                        
                        
                        wType = (byte)(((senderInfo.Id.bType & 0x0F) << 4) | (senderInfo.Id.bIndex & 0x0F));
                    }
                    
                    var builder = new PacketBuilder();
                    builder.WriteUInt32(0); 
                    builder.WriteUInt16(cmd);
                    builder.WriteUInt16(0); 
                    builder.WriteUInt16(wType); 
                    builder.WriteUInt16(0); 
                    builder.WriteBytes(data);

                    byte[] packet = builder.Build();
                    
                    
                    foreach (var client in groupClients)
                    {
                        await client.SendRawMessageAsync(packet);
                    }
                    
                    LogManager.Default.Info($"已转发消息到服务器组: {groupId}, 发送者类型={senderInfo.Id.bType}, 发送者索引={senderInfo.Id.bIndex}, wType={wType:X2}, 服务器数量={groupClients.Count}");
                }
                else
                {
                    LogManager.Default.Warning($"找不到服务器组: 组ID={groupId}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"转发到服务器组失败: {ex.Message}");
            }
        }

        
        
        
        private async Task ForwardToServerType(RegisteredServer senderInfo, ushort cmd, ushort serverType, byte[] data, byte wType = 0)
        {
            try
            {
                
                var typeClients = _server.GetClientsByType((ServerType)serverType);
                if (typeClients.Count > 0)
                {
                    
                    if (wType == 0)
                    {
                        
                        
                        wType = (byte)(((senderInfo.Id.bType & 0x0F) << 4) | (senderInfo.Id.bIndex & 0x0F));
                    }
                    
                    var builder = new PacketBuilder();
                    builder.WriteUInt32(0); 
                    builder.WriteUInt16(cmd);
                    builder.WriteUInt16(0); 
                    builder.WriteUInt16(wType); 
                    builder.WriteUInt16(0); 
                    builder.WriteBytes(data);

                    byte[] packet = builder.Build();
                    
                    
                    foreach (var client in typeClients)
                    {
                        await client.SendRawMessageAsync(packet);
                    }
                    
                    LogManager.Default.Info($"已转发消息到服务器类型: {(ServerType)serverType}, 发送者类型={senderInfo.Id.bType}, 发送者索引={senderInfo.Id.bIndex}, wType={wType:X2}, 服务器数量={typeClients.Count}");
                }
                else
                {
                    LogManager.Default.Warning($"找不到服务器类型: {(ServerType)serverType}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"转发到服务器类型失败: {ex.Message}");
            }
        }

        
        
        
        public async Task SendRawMessageAsync(byte[] data)
        {
            try
            {
                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送原始消息失败: {ex.Message}");
            }
        }

        public void SendRegisterResult(MirCommon.RegisterServerResult result)
        {
            SendMessage(0, ProtocolCmd.SCM_REGISTERSERVER, 0, 0, 0, result);
        }

        private void SendMessage(uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, object? data)
        {
            try
            {
                
                LogManager.Default.Info($"[ServerCenter发送] 消息头: dwFlag={dwFlag}, wCmd={wCmd}, w1={w1}, w2={w2}, w3={w3}");
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(dwFlag);
                builder.WriteUInt16(wCmd);
                builder.WriteUInt16(w1);
                builder.WriteUInt16(w2);
                builder.WriteUInt16(w3);
                
                
                if (data != null)
                {
                    if (data is MirCommon.RegisterServerResult regResult)
                    {
                        
                        LogManager.Default.Info($"[ServerCenter发送] 注册结果: 服务器类型={regResult.Id.bType}, 组={regResult.Id.bGroup}, 索引={regResult.Id.bIndex}, 数据库服务器数量={regResult.nDbCount}");
                        
                        
                        builder.WriteByte(regResult.Id.bType);
                        builder.WriteByte(regResult.Id.bGroup);
                        builder.WriteByte(regResult.Id.bIndex);
                        builder.WriteByte(0); 
                        builder.WriteInt32(regResult.nDbCount);
                        
                        
                        
                        for (int i = 0; i < regResult.nDbCount && i < regResult.DbAddr.Length; i++)
                        {
                            var addr = regResult.DbAddr[i];
                            
                            byte[] addrBytes = addr.addr;
                            if (addrBytes.Length < 16)
                            {
                                Array.Resize(ref addrBytes, 16);
                            }
                            else if (addrBytes.Length > 16)
                            {
                                byte[] temp = new byte[16];
                                Array.Copy(addrBytes, 0, temp, 0, 16);
                                addrBytes = temp;
                            }
                            builder.WriteBytes(addrBytes);
                            
                            builder.WriteUInt32(addr.nPort);
                        }
                    }
                    else if (data is MirCommon.FindServerResult findResult)
                    {
                        
                        LogManager.Default.Info($"[ServerCenter发送] 查找结果: 服务器类型={findResult.Id.bType}, 组={findResult.Id.bGroup}, 索引={findResult.Id.bIndex}");
                        
                        
                        builder.WriteByte(findResult.Id.bType);
                        builder.WriteByte(findResult.Id.bGroup);
                        builder.WriteByte(findResult.Id.bIndex);
                        builder.WriteByte(0); 
                        
                        
                        byte[] addrBytes = findResult.addr.addr;
                        if (addrBytes.Length < 16)
                        {
                            Array.Resize(ref addrBytes, 16);
                        }
                        else if (addrBytes.Length > 16)
                        {
                            byte[] temp = new byte[16];
                            Array.Copy(addrBytes, 0, temp, 0, 16);
                            addrBytes = temp;
                        }
                        builder.WriteBytes(addrBytes);
                        
                        builder.WriteUInt32(findResult.addr.nPort);
                    }
                }
                
                byte[] packet = builder.Build();
                LogManager.Default.Info($"[ServerCenter发送] 数据包长度: {packet.Length}字节");
                
                _stream.Write(packet, 0, packet.Length);
                _stream.Flush();
                
                
                LogManager.Default.Info($"[ServerCenter发送] 发送成功: wCmd={wCmd}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[ServerCenter发送] 发送失败: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
        }

        
        
        
        private T BytesToStruct<T>(byte[] bytes) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            if (bytes.Length < size)
                throw new ArgumentException($"字节数组长度不足，需要{size}字节，实际{bytes.Length}字节");

            IntPtr ptr = Marshal.AllocHGlobal(size);
            
            try
            {
                Marshal.Copy(bytes, 0, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

}
