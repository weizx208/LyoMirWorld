using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Player = GameServer.HumanPlayer;

namespace GameServer
{
    
    

    
    
    
    public class GameMap
    {
        public int MapId { get; set; }
        public string Name { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }

        public void AddPlayer(Player player) { }
        public void RemovePlayer(uint playerId) { }
        public int GetPlayerCount() => 0;
        public Player[] GetPlayersInRange(int x, int y, int range) => Array.Empty<Player>();
    }

    
    
    
    public class GameServerApp
    {
        private readonly IniFileReader _config;
        
        private TcpListener? _listener;
        private readonly List<GameClient> _clients = new();
        private readonly object _clientLock = new();
        private readonly GameWorld _world = GameWorld.Instance;
        private bool _isRunning = false;
        
        private static uint _nextPlayerId = 1;
        private MirCommon.Database.DatabaseManager? _databaseManager;

        private string _addr = "127.0.0.1";
        private int _port = 7200;
        private string _name = "淡抹夕阳";
        private int _maxconnection = 4000;
        private string _baniplist = "banip.txt";
        private string _trustiplist = "trustip.txt";
        private string _dbServerAddress = "127.0.0.1";
        private int _dbServerPort = 8000;
        private string _serverCenterAddress = "127.0.0.1";
        private int _serverCenterPort = 6000;

        
        private MirCommon.Network.ServerCenterClient? _serverCenterClient;
        private Task? _serverCenterTask;

        
        private MirCommon.Database.DBServerClient? _dbServerClient;
        

        
        private readonly Dictionary<uint, MirCommon.EnterGameServer> _enterInfoDict = new();
        private readonly object _enterInfoLock = new();

        
        private readonly ConcurrentDictionary<uint, GameClient> _gameClients = new();
        private readonly object _gameClientsLock = new();

        public GameServerApp(IniFileReader config)
        {
            _config = config;
        }

        public async Task<bool> Initialize()
        {
            try
            {
                
                string sectionName = "游戏世界服务器";
                _addr = _config.GetString(sectionName, "addr", "127.0.0.1");
                _port = _config.GetInteger(sectionName, "port", 7200);
                _name = _config.GetString(sectionName, "name", " 淡抹夕阳");
                _maxconnection = _config.GetInteger(sectionName, "maxconnection", 4000);
                _baniplist = _config.GetString(sectionName, "baniplist", "banip.txt");
                _trustiplist = _config.GetString(sectionName, "trustiplist", "trustip.txt");

                
                string dbSectionName = "数据库服务器";
                _dbServerAddress = _config.GetString(dbSectionName, "addr", "127.0.0.1");
                _dbServerPort = _config.GetInteger(dbSectionName, "port", 8000);

                
                string scSectionName = "服务器中心";
                _serverCenterAddress = _config.GetString(scSectionName, "addr", "127.0.0.1");
                _serverCenterPort = _config.GetInteger(scSectionName, "port", 6000);

                
                LogManager.Default.Info("正在加载游戏配置文件...");
                if (!await ConfigLoader.Instance.LoadAllConfigsAsync())
                {
                    LogManager.Default.Error("配置文件加载失败");
                    return false;
                }

                #region DBServer长连接

                
                LogManager.Default.Info("正在初始化DBServer连接...");
                LogManager.Default.Info($"DBServer地址: {_dbServerAddress}:{_dbServerPort}");
                
                _dbServerClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await _dbServerClient.ConnectAsync())
                {
                    LogManager.Default.Error("DBServer连接失败");
                    return false;
                }

                
                _dbServerClient.OnDbMessageReceived += HandleDbServerMessage;
                _dbServerClient.OnLogMessage += (msg) => LogManager.Default.Info(msg);

                LogManager.Default.Info("DBServer连接成功");

                
                LogManager.Default.Info("正在启动DBServer消息监听...");
                _dbServerClient.StartListening();
                
                LogManager.Default.Info("DBServer消息监听已启动");

                #endregion

                #region ServerCenter长连接

                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                

                #endregion

                if (!_world.Initialize())
                {
                    LogManager.Default.Error("游戏世界初始化失败");
                    return false;
                }

                
                _world.StartMonsterUpdateThread();
                _world.StartDBUpdateTimer();

                
                
                

                LogManager.Default.Info($"监听端口: {_port}");
                LogManager.Default.Info($"最大玩家: {_maxconnection}");

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("初始化失败", exception: ex);
                return false;
            }
        }

        
        
        
        
        public async Task StartServerCenterClient()
        {
            
            LogManager.Default.Info("正在向ServerCenter注册...");
            _serverCenterClient = new MirCommon.Network.ServerCenterClient(_serverCenterAddress, _serverCenterPort);
            if (await _serverCenterClient.ConnectAsync())
            {
                bool registered = await _serverCenterClient.RegisterServerAsync("GameServer", _name, _addr, _port, _maxconnection);
                if (registered)
                {
                    LogManager.Default.Info("ServerCenter注册成功");
                    
                    _serverCenterTask = Task.Run(async () => await ProcessServerCenterMessagesAsync());
                    
                    _ = Task.Run(async () => await SendHeartbeatAsync());
                }
                else
                {
                    LogManager.Default.Warning("ServerCenter注册失败");
                }
            }
            else
            {
                LogManager.Default.Warning("无法连接到ServerCenter");
            }
        }

        
        
        
        
        public async Task Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;

            LogManager.Default.Info("游戏服务器已启动");

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

            
            try
            {
                if (_dbServerClient != null)
                {
                    _dbServerClient.StopListening();
                    _dbServerClient.Disconnect();
                    _dbServerClient = null;
                }
            }
            catch { }

            
            try
            {
                if (_serverCenterClient != null)
                {
                    _serverCenterClient.UnregisterServerAsync("GameServer", _name).GetAwaiter().GetResult();
                    _serverCenterClient.Disconnect();
                    _serverCenterClient = null;
                }
            }
            catch { }
        }

        public void Update()
        {
            _world.Update();
        }

        
        
        
        
        
        private async Task HandleClient(TcpClient tcpClient)
        {
            var client = new GameClient(tcpClient, this, _world, _dbServerAddress, _dbServerPort);

            lock (_clientLock)
            {
                if (_clients.Count >= _maxconnection)
                {
                    LogManager.Default.Warning("服务器已满");
                    tcpClient.Close();
                    return;
                }
                _clients.Add(client);
            }

            
            RegisterGameClient(client);

            LogManager.Default.Info($"新玩家连接: {tcpClient.Client.RemoteEndPoint}");

            try
            {
                
                await client.ProcessAsync();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理玩家错误: {ex.Message}");
            }
            finally
            {
                
                UnregisterGameClient(client);
                
                lock (_clientLock)
                {
                    _clients.Remove(client);
                }
                LogManager.Default.Info($"玩家断开");
            }
        }

        public uint GeneratePlayerId() => Interlocked.Increment(ref _nextPlayerId);

        public void ListPlayers()
        {
            var players = _world.GetAllPlayers();
            Console.WriteLine($"\n========== 在线玩家 ({players.Length}) ==========");
            foreach (var player in players.Take(20))
            {
                Console.WriteLine($"  [{player.ObjectId}] {player.Name} - 地图:{player.MapId} 位置:({player.X},{player.Y}) 等级:{player.Level}");
            }
            if (players.Length > 20)
                Console.WriteLine($"  ... 还有 {players.Length - 20} 个玩家");
            Console.WriteLine("================================\n");
        }

        public void ListMaps()
        {
            var maps = _world.GetAllMaps();
            Console.WriteLine($"\n========== 地图列表 ({maps.Length}) ==========");
            foreach (var map in maps)
            {
                Console.WriteLine($"  [{map.MapId}] {map.Name} - 大小:{map.Width}x{map.Height} 玩家:{map.GetPlayerCount()}");
            }
            Console.WriteLine("================================\n");
        }

        public void ListMonsters()
        {
            var monsters = MonsterManagerEx.Instance.GetAllMonsters();
            Console.WriteLine($"\n========== 怪物列表 ({monsters.Count}) ==========");

            
            var monstersByMap = monsters.GroupBy(m => m.MapId);

            foreach (var mapGroup in monstersByMap)
            {
                var map = _world.GetMap(mapGroup.Key);
                string mapName = map != null ? map.Name : $"地图{mapGroup.Key}";
                Console.WriteLine($"\n  [{mapGroup.Key}] {mapName} - 怪物数量: {mapGroup.Count()}");

                
                var monstersByType = mapGroup.GroupBy(m => m.GetDesc()?.Base.MonsterId ?? 0);
                foreach (var typeGroup in monstersByType.Take(10)) 
                {
                    var monsterClass = MonsterManagerEx.Instance.GetMonsterClass(typeGroup.Key);
                    string monsterName = monsterClass != null ? monsterClass.Base.ClassName : $"怪物{typeGroup.Key}";
                    Console.WriteLine($"    {monsterName} (ID:{typeGroup.Key}) x{typeGroup.Count()}");
                }

                if (monstersByType.Count() > 10)
                {
                    Console.WriteLine($"    ... 还有 {monstersByType.Count() - 10} 种怪物类型");
                }
            }

            if (monstersByMap.Count() == 0)
            {
                Console.WriteLine("  当前没有怪物");
            }

            Console.WriteLine("================================\n");
        }

        public void SpawnMonster(int mapId, int monsterId)
        {
            try
            {
                var map = _world.GetMap(mapId);
                if (map == null)
                {
                    Console.WriteLine($"错误: 地图 {mapId} 不存在\n");
                    return;
                }

                var monsterClass = MonsterManagerEx.Instance.GetMonsterClass(monsterId);
                if (monsterClass == null)
                {
                    Console.WriteLine($"错误: 怪物ID {monsterId} 不存在\n");
                    return;
                }

                
                int centerX = map.Width / 2;
                int centerY = map.Height / 2;

                
                Random rand = new Random();
                int x = Math.Clamp(centerX + rand.Next(-10, 11), 0, map.Width - 1);
                int y = Math.Clamp(centerY + rand.Next(-10, 11), 0, map.Height - 1);

                
                var monster = new MonsterEx();
                if (monster.Init(monsterClass, mapId, x, y))
                {
                    
                    
                    

                    
                    
                    

                    Console.WriteLine($"成功生成怪物: {monsterClass.Base.ClassName} (ID:{monsterId}) 在地图 {mapId} 位置 ({x},{y})\n");
                }
                else
                {
                    Console.WriteLine($"错误: 怪物初始化失败\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷怪错误: {ex.Message}\n");
            }
        }

        public void ShowStatus()
        {
            Console.WriteLine($"运行时间: {_world.GetUptime()}");
            Console.WriteLine($"在线玩家: {_world.GetPlayerCount()}");
            Console.WriteLine($"地图数量: {_world.GetMapCount()}");
            Console.WriteLine($"更新次数: {_world.GetUpdateCount()}");
        }

        
        
        
        private async Task ProcessServerCenterMessagesAsync()
        {
            if (_serverCenterClient == null)
            {
                LogManager.Default.Error("ServerCenterClient为null，无法启动消息处理任务");
                return;
            }

            try
            {
                LogManager.Default.Info("ServerCenter消息处理任务已启动");

                
                
                
                var clientType = _serverCenterClient.GetType();
                var clientField = clientType.GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var streamField = clientType.GetField("_stream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (clientField == null || streamField == null)
                {
                    LogManager.Default.Error("无法访问ServerCenterClient的内部字段");
                    return;
                }

                var tcpClient = clientField.GetValue(_serverCenterClient) as TcpClient;
                var networkStream = streamField.GetValue(_serverCenterClient) as NetworkStream;

                if (tcpClient == null || networkStream == null)
                {
                    LogManager.Default.Error("无法获取ServerCenterClient的TcpClient或NetworkStream");
                    return;
                }

                LogManager.Default.Info($"ServerCenter连接状态: Connected={tcpClient.Connected}, Available={tcpClient.Available}");

                byte[] buffer = new byte[8192];
                int reconnectAttempts = 0;
                const int maxReconnectAttempts = 3;

                while (_isRunning)
                {
                    try
                    {
                        if (!_serverCenterClient._connected)
                        {
                            LogManager.Default.Warning("ServerCenter连接已断开，尝试重新连接...");
                            reconnectAttempts++;
                            if (reconnectAttempts > maxReconnectAttempts)
                            {
                                LogManager.Default.Error($"达到最大重连次数({maxReconnectAttempts})，停止ServerCenter消息处理");
                                break;
                            }

                            
                            try
                            {
                                if (await _serverCenterClient.ConnectAsync())
                                {
                                    bool registered = await _serverCenterClient.RegisterServerAsync("GameServer", _name, _addr, _port, _maxconnection);
                                    if (registered)
                                    {
                                        LogManager.Default.Info("ServerCenter重新注册成功");
                                        reconnectAttempts = 0;

                                        
                                        tcpClient = clientField.GetValue(_serverCenterClient) as TcpClient;
                                        networkStream = streamField.GetValue(_serverCenterClient) as NetworkStream;
                                        if (tcpClient == null || networkStream == null)
                                        {
                                            LogManager.Default.Error("重新连接后无法获取TcpClient或NetworkStream");
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        LogManager.Default.Warning("ServerCenter重新注册失败");
                                        await Task.Delay(5000); 
                                        continue;
                                    }
                                }
                                else
                                {
                                    LogManager.Default.Warning("无法重新连接到ServerCenter");
                                    await Task.Delay(5000); 
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                LogManager.Default.Error($"重新连接ServerCenter失败: {ex.Message}");
                                await Task.Delay(5000); 
                                continue;
                            }
                        }

                        LogManager.Default.Debug("等待ServerCenter消息...");

                        
                        
                        
                        
                        
                        
                        

                        
                        var readTask = networkStream.ReadAsync(buffer, 0, buffer.Length);

                        
                        
                        
                        int bytesRead = await readTask;
                        LogManager.Default.Debug($"收到ServerCenter消息: {bytesRead}字节");
                        if (bytesRead > 0)
                        {
                            await ProcessServerCenterMessage(buffer, bytesRead);
                        }
                        else
                        {
                            
                            LogManager.Default.Info("ServerCenter连接已关闭");
                            
                            await Task.Delay(1000);
                        }
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                    }
                    catch (OperationCanceledException)
                    {
                        
                        LogManager.Default.Info("ServerCenter消息读取任务被取消");
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                        {
                            LogManager.Default.Error($"读取ServerCenter消息失败: {ex.Message}");
                            LogManager.Default.Error($"堆栈跟踪: {ex.StackTrace}");
                            
                            await Task.Delay(1000);
                        }
                    }
                }

                LogManager.Default.Info("ServerCenter消息处理任务已停止");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理ServerCenter消息失败: {ex.Message}");
                LogManager.Default.Error($"堆栈跟踪: {ex.StackTrace}");
            }
        }

        
        
        
        private async Task ProcessServerCenterMessage(byte[] data, int length)
        {
            if (length < 12) return;

            try
            {
                
                var reader = new PacketReader(data);
                uint clientId = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                ushort w1 = reader.ReadUInt16();
                ushort w2 = reader.ReadUInt16();
                ushort w3 = reader.ReadUInt16();
                byte[] payload = reader.ReadBytes(length - 12);

                
                LogManager.Default.Info($"收到ServerCenter消息: ClientId={clientId}, Cmd={wCmd:X4}, w1={w1}, w2={w2}, w3={w3}, 数据长度={payload.Length}字节");

                
                if (payload.Length > 0)
                {
                    string hexPayload = BitConverter.ToString(payload).Replace("-", " ");
                    LogManager.Default.Debug($"Payload十六进制: {hexPayload.Substring(0, Math.Min(100, hexPayload.Length))}...");
                }

                
                if (wCmd == ProtocolCmd.SCM_MSGACROSSSERVER)
                {
                    
                    LogManager.Default.Info($"处理跨服务器消息: 真实命令={w1:X4}, 发送类型={w2}, 目标索引={w3}");
                    await OnMASMsg((ushort)w1, (byte)w2, w3, payload);
                }
                else if (wCmd == ProtocolCmd.SCM_GETGAMESERVERADDR)
                {
                    
                    LogManager.Default.Info($"处理获取游戏服务器地址消息: w1={w1}, w2={w2}, w3={w3}, 数据长度={payload.Length}");
                    
                    
                }
                else
                {
                    
                    LogManager.Default.Info($"处理其他ServerCenter消息: Cmd={wCmd:X4}, w1={w1}, w2={w2}, w3={w3}");
                    await OnMASMsg(wCmd, (byte)w2, w3, payload);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析ServerCenter消息失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        
        
        
        
        
        
        
        public async Task OnMASMsg(ushort wCmd, byte wType, ushort wIndex, byte[] data)
        {
            try
            {
                LogManager.Default.Info($"收到ServerCenter消息: Cmd={wCmd:X4}, Type={wType}, Index={wIndex}, 数据长度={data.Length}字节");

                
                
                
                byte senderServerType = (byte)((wType >> 4) & 0x0F);
                byte senderServerIndex = (byte)(wType & 0x0F);

                LogManager.Default.Info($"发送者信息: 服务器类型={senderServerType}, 服务器索引={senderServerIndex}");

                
                
                
                

                switch (wCmd)
                {
                    case ProtocolCmd.MAS_ENTERGAMESERVER:
                        LogManager.Default.Info($"处理MAS_ENTERGAMESERVER消息，数据长度={data.Length}字节");
                        await HandleEnterGameServerMessage(data, wIndex, senderServerType, senderServerIndex);
                        break;

                    default:
                        LogManager.Default.Warning($"未知的ServerCenter消息: {wCmd:X4}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理ServerCenter消息失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        
        
        
        private async Task HandleEnterGameServerMessage(byte[] data, ushort wIndex, byte senderServerType, byte senderServerIndex)
        {
            try
            {
                LogManager.Default.Info($"开始处理进入游戏服务器消息: 数据长度={data.Length}字节, wIndex={wIndex}, 发送者类型={senderServerType}, 发送者索引={senderServerIndex}");

                
                if (data.Length >= 64) 
                {
                    
                    var enterInfo = BytesToStruct<MirCommon.EnterGameServer>(data);

                    
                    string account = enterInfo.GetAccount();
                    string name = enterInfo.GetName();
                    uint loginId = enterInfo.nLoginId;

                    
                    
                    enterInfo.dwSelectCharServerId = senderServerIndex;

                    LogManager.Default.Info($"收到ServerCenter发来的玩家进入游戏服务器通知: 账号='{account}', 角色名='{name}', 登录ID={loginId}");
                    LogManager.Default.Info($"详细结构体信息:");
                    LogManager.Default.Info($"  - 账号字节数组: {BitConverter.ToString(enterInfo.szAccount).Replace("-", " ")}");
                    LogManager.Default.Info($"  - 账号字符串: '{account}' (长度: {account?.Length ?? 0})");
                    LogManager.Default.Info($"  - 角色名字节数组: {BitConverter.ToString(enterInfo.szName).Replace("-", " ")}");
                    LogManager.Default.Info($"  - 角色名字符串: '{name}' (长度: {name?.Length ?? 0})");
                    LogManager.Default.Info($"  - 登录ID: {enterInfo.nLoginId}");
                    LogManager.Default.Info($"  - 选择角色ID: {enterInfo.nSelCharId}");
                    LogManager.Default.Info($"  - 客户端ID: {enterInfo.nClientId}");
                    LogManager.Default.Info($"  - 进入时间: {enterInfo.dwEnterTime}");
                    LogManager.Default.Info($"  - 选择角色服务器ID: {enterInfo.dwSelectCharServerId}");

                    
                    lock (_enterInfoLock)
                    {
                        _enterInfoDict[loginId] = enterInfo;
                        LogManager.Default.Info($"已保存进入信息到字典，登录ID={loginId}，当前字典大小={_enterInfoDict.Count}");

                        
                        LogManager.Default.Info($"当前字典中的登录ID: {string.Join(", ", _enterInfoDict.Keys)}");
                    }

                    
                    await SendEnterGameServerAck(enterInfo, senderServerType, senderServerIndex, wIndex, data);
                }
                else
                {
                    LogManager.Default.Error($"进入游戏服务器消息数据长度不足: {data.Length}字节");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理进入游戏服务器消息失败: {ex.Message}");
                LogManager.Default.Error($"堆栈跟踪: {ex.StackTrace}");
            }

            await Task.CompletedTask;
        }

        
        
        
        private async Task SendEnterGameServerAck(
            MirCommon.EnterGameServer enterInfo,
            byte senderServerType,
            byte senderServerIndex,
            ushort serverCenterTargetIndex,
            byte[] originalEnterInfoBytes)
        {
            try
            {
                if (_serverCenterClient == null)
                    return;

                
                
                
                
                const int ENTERGAMESERVER_SIZE = 64;
                if (originalEnterInfoBytes == null || originalEnterInfoBytes.Length < ENTERGAMESERVER_SIZE)
                {
                    LogManager.Default.Warning($"发送进入游戏服务器确认响应失败：原始EnterGameServer字节数组不足 {ENTERGAMESERVER_SIZE} 字节");
                    return;
                }

                
                uint replyClientId = enterInfo.nClientId;

                
                ushort targetIndex = senderServerIndex;

                
                var ackData = new byte[ENTERGAMESERVER_SIZE];
                Array.Copy(originalEnterInfoBytes, 0, ackData, 0, ENTERGAMESERVER_SIZE);

                
                BitConverter.GetBytes((uint)MirCommon.SERVER_ERROR.SE_OK).CopyTo(ackData, 20);

                
                BitConverter.GetBytes((uint)senderServerIndex).CopyTo(ackData, 60);

                
                byte sendType = (byte)MirCommon.ProtocolCmd.MST_SINGLE;

                LogManager.Default.Info(
                    $"回发MAS_ENTERGAMESERVER确认: replyClientId={replyClientId}, " +
                    $"senderType={senderServerType}, senderIndex={senderServerIndex}, targetIndex={targetIndex}"
                );

                bool sent = await _serverCenterClient.SendMsgAcrossServerAsync(
                    clientId: replyClientId,
                    cmd: ProtocolCmd.MAS_ENTERGAMESERVER,
                    sendType: sendType,
                    targetIndex: targetIndex,
                    binaryData: ackData
                );

                if (sent)
                {
                    LogManager.Default.Info($"已发送进入游戏服务器确认响应: targetIndex={targetIndex}, sendType={sendType}");
                }
                else
                {
                    LogManager.Default.Warning("发送进入游戏服务器确认响应失败");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送进入游戏服务器确认响应失败: {ex.Message}");
            }
        }

        
        
        
        private T BytesToStruct<T>(byte[] bytes) where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            if (bytes.Length < size)
                throw new ArgumentException($"字节数组长度不足: {bytes.Length} < {size}");

            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, size);
                return System.Runtime.InteropServices.Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }
        }

        
        
        
        
        
        
        
        

        
        
        

        
        
        

        
        
        
        
        
        
        
        
        
        
        
        

        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
                
        
        
        
        
        
        
        
        
        
        
        
        
        
        
                
        
        
        
        
        
        
        
        
        
        
        
        

        
        
        
        
        
        
        
        
        
        
        

        
        
        
        private async Task SendHeartbeatAsync()
        {
            while (_isRunning && _serverCenterClient != null)
            {
                try
                {
                    await Task.Delay(30000); 

                    if (_serverCenterClient == null || !_isRunning)
                        break;

                    
                    bool sent = await _serverCenterClient.SendHeartbeatAsync();
                    if (sent)
                    {
                        
                    }
                    else
                    {
                        LogManager.Default.Warning("ServerCenter心跳发送失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"发送ServerCenter心跳失败: {ex.Message}");
                }
            }
        }

        
        
        
        private bool IsServerCenterConnected()
        {
            
            
            return _serverCenterClient != null;
        }

        
        
        
        public string GetServerName()
        {
            return _name;
        }

        #region 迁移备份

        
        
        
        private async Task HandleEnterGameServerMessage(MirMsg msg, byte[] payload)
        {
            try
            {
                LogManager.Default.Debug($"收到跨服务器消息: MAS_ENTERGAMESERVER (0x{ProtocolCmd.MAS_ENTERGAMESERVER:X4})");
                LogManager.Default.Debug($"消息标志: 0x{msg.dwFlag:X8}");
                LogManager.Default.Debug($"消息参数: w1=0x{msg.wParam[0]:X4}, w2=0x{msg.wParam[1]:X4}, w3=0x{msg.wParam[2]:X4}");
                LogManager.Default.Debug($"消息数据长度: {payload.Length}字节");

                
                if (payload.Length > 0)
                {
                    string hexPayload = BitConverter.ToString(payload).Replace("-", " ");
                    LogManager.Default.Debug($"Payload十六进制: {hexPayload.Substring(0, Math.Min(100, hexPayload.Length))}...");
                }

                
                if (payload.Length < System.Runtime.InteropServices.Marshal.SizeOf<MirCommon.EnterGameServer>())
                {
                    LogManager.Default.Error($"EnterGameServer消息数据长度不足: {payload.Length}字节, 需要至少{System.Runtime.InteropServices.Marshal.SizeOf<MirCommon.EnterGameServer>()}字节");
                    return;
                }

                
                var enterGameServer = BytesToStruct<MirCommon.EnterGameServer>(payload);

                
                string account = enterGameServer.GetAccount();
                string name = enterGameServer.GetName();
                uint loginId = enterGameServer.nLoginId;

                LogManager.Default.Info($"收到玩家进入游戏服务器通知: 账号='{account}', 角色名='{name}'");
                LogManager.Default.Debug($"详细结构体信息:");
                LogManager.Default.Debug($"  - 账号字节数组: {BitConverter.ToString(enterGameServer.szAccount).Replace("-", " ")}");
                LogManager.Default.Debug($"  - 账号字符串: '{account}' (长度: {account?.Length ?? 0})");
                LogManager.Default.Debug($"  - 角色名字节数组: {BitConverter.ToString(enterGameServer.szName).Replace("-", " ")}");
                LogManager.Default.Debug($"  - 角色名字符串: '{name}' (长度: {name?.Length ?? 0})");
                LogManager.Default.Debug($"  - 登录ID: {enterGameServer.nLoginId}");
                LogManager.Default.Debug($"  - 选择角色ID: {enterGameServer.nSelCharId}");
                LogManager.Default.Debug($"  - 客户端ID: {enterGameServer.nClientId}");
                LogManager.Default.Debug($"  - 进入时间: {enterGameServer.dwEnterTime}");
                LogManager.Default.Debug($"  - 选择角色服务器ID: {enterGameServer.dwSelectCharServerId}");

                

                _enterInfoDict[loginId] = enterGameServer;
                LogManager.Default.Debug($"已保存进入信息到_enterInfo");
                LogManager.Default.Debug($"_enterInfo.GetAccount() = '{enterGameServer.GetAccount()}'");
                LogManager.Default.Debug($"_enterInfo.GetName() = '{enterGameServer.GetName()}'");
                LogManager.Default.Debug($"_enterInfo.nLoginId = {enterGameServer.nLoginId}");
                LogManager.Default.Debug($"_enterInfo.nSelCharId = {enterGameServer.nSelCharId}");

                
                SendEnterGameServerAck1();
                LogManager.Default.Debug($"已发送进入游戏服务器确认响应");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理进入游戏服务器消息失败: {ex.Message}");
                LogManager.Default.Error($"堆栈跟踪: {ex.StackTrace}");
            }

            await Task.CompletedTask;
        }

        
        
        
        private void SendEnterGameServerAck1()
        {
            
            
            
            
            
            
            
            
            
            

            
            
            
            
            
            
            
            
        }

        #endregion


        
        
        
        public MirCommon.EnterGameServer? GetEnterInfo(uint loginId)
        {
            lock (_enterInfoLock)
            {
                if (_enterInfoDict.TryGetValue(loginId, out var enterInfo))
                {
                    LogManager.Default.Debug($"从字典获取进入信息成功，登录ID={loginId}");
                    return enterInfo;
                }
                else
                {
                    LogManager.Default.Debug($"字典中未找到进入信息，登录ID={loginId}，字典大小={_enterInfoDict.Count}");
                    return null;
                }
            }
        }

        
        
        
        public void RemoveEnterInfo(uint loginId)
        {
            lock (_enterInfoLock)
            {
                if (_enterInfoDict.Remove(loginId))
                {
                    LogManager.Default.Debug($"已从字典移除进入信息，登录ID={loginId}");
                }
            }
        }


        
        
        
        private void HandleDbServerMessage(MirCommon.MirMsg msg)
        {
            try
            {
                LogManager.Default.Info($"收到DBServer消息: Cmd=0x{msg.wCmd:X4}({(DbMsg)msg.wCmd}), Flag=0x{msg.dwFlag:X8}, w1={msg.wParam[0]}, w2={msg.wParam[1]}, w3={msg.wParam[2]}, 数据长度={msg.data?.Length ?? 0}字节");

                
                
                
                var clients = new List<GameClient>();
                lock (_gameClientsLock)
                {
                    foreach (var kv in _gameClients)
                    {
                        if (kv.Value != null)
                            clients.Add(kv.Value);
                    }
                }

                foreach (var c in clients)
                {
                    try
                    {
                        c.HandleDbServerMessage(msg);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"转发DB消息到GameClient失败: {ex.Message}");
                    }
                }

                
                if (msg.wCmd == (ushort)DbMsg.DM_CREATEITEM)
                {
                    HandleServerLevelDbMessage(msg);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理DBServer消息失败: {ex.Message}");
            }
        }

        
        
        
        private void HandleServerLevelDbMessage(MirCommon.MirMsg msg)
        {
            try
            {
                switch (msg.wCmd)
                {
                    case (ushort)DbMsg.DM_CREATEITEM:
                        HandleDBCreateItem(msg);
                        break;
                    
                    default:
                        LogManager.Default.Warning($"未处理的服务器级别DBServer消息: 0x{msg.wCmd:X4}({(DbMsg)msg.wCmd})");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理服务器级别DBServer消息失败: {ex.Message}");
            }
        }

        
        
        
        private void HandleDBCreateItem(MirCommon.MirMsg msg)
        {
            try
            {
                LogManager.Default.Info($"处理DM_CREATEITEM消息");
                
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理DM_CREATEITEM消息失败: {ex.Message}");
            }
        }

        
        
        
        private void RegisterGameClient(GameClient client)
        {
            try
            {
                
                uint clientId = client.GetId();
                if (clientId > 0)
                {
                    lock (_gameClientsLock)
                    {
                        _gameClients[clientId] = client;
                        LogManager.Default.Info($"已注册GameClient: clientId={clientId}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"注册GameClient失败: {ex.Message}");
            }
        }

        
        
        
        private void UnregisterGameClient(GameClient client)
        {
            try
            {
                
                uint clientId = client.GetId();
                if (clientId > 0)
                {
                    lock (_gameClientsLock)
                    {
                        if (_gameClients.TryRemove(clientId, out _))
                        {
                            LogManager.Default.Info($"已移除GameClient: clientId={clientId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"移除GameClient失败: {ex.Message}");
            }
        }

        
        
        
        private GameClient? FindGameClientById(uint clientId)
        {
            lock (_gameClientsLock)
            {
                if (_gameClients.TryGetValue(clientId, out var client))
                {
                    return client;
                }
                return null;
            }
        }

        
        
        
        public MirCommon.Database.DBServerClient? GetDbServerClient()
        {
            return _dbServerClient;
        }

        
        
        
        public MirCommon.Network.ServerCenterClient? GetServerCenterClient()
        {
            return _serverCenterClient;
        }
    }


    
    
    
    public enum ClientState
    {
        GSUM_NOTVERIFIED = 0,      
        GSUM_WAITINGDBINFO = 1,    
        GSUM_WAITINGCONFIRM = 2,   
        GSUM_VERIFIED = 3          
    }

    
    
    
    public class CharacterInfo
    {
        public int Id { get; set; }
        public byte Job { get; set; }
        public byte Sex { get; set; }
        public short Level { get; set; }
        public string MapName { get; set; } = string.Empty;
        public short X { get; set; }
        public short Y { get; set; }
        public byte Hair { get; set; }
        public uint Exp { get; set; }
        public ushort CurrentHP { get; set; }
        public ushort CurrentMP { get; set; }
        public ushort MaxHP { get; set; }
        public ushort MaxMP { get; set; }
        public ushort MinDC { get; set; }
        public ushort MaxDC { get; set; }
        public ushort MinMC { get; set; }
        public ushort MaxMC { get; set; }
        public ushort MinSC { get; set; }
        public ushort MaxSC { get; set; }
        public ushort MinAC { get; set; }
        public ushort MaxAC { get; set; }
        public ushort MinMAC { get; set; }
        public ushort MaxMAC { get; set; }
        public ushort Weight { get; set; }
        public ushort HandWeight { get; set; }
        public ushort BodyWeight { get; set; }
        public uint Gold { get; set; }
        public int MapId { get; set; }
        public uint Yuanbao { get; set; }
        public uint Flag1 { get; set; }
        public uint Flag2 { get; set; }
        public uint Flag3 { get; set; }
        public uint Flag4 { get; set; }
        public string GuildName { get; set; } = string.Empty;
        public uint ForgePoint { get; set; }
        public uint Prop1 { get; set; }
        public uint Prop2 { get; set; }
        public uint Prop3 { get; set; }
        public uint Prop4 { get; set; }
        public uint Prop5 { get; set; }
        public uint Prop6 { get; set; }
        public uint Prop7 { get; set; }
        public uint Prop8 { get; set; }
        public ushort Accuracy { get; set; }
        public ushort Agility { get; set; }
        public ushort Lucky { get; set; }
    }
}
