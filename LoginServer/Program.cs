using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MirCommon.Utils.GameEncoding;

namespace LoginServer
{
    
    
    
    class Program
    {
        private static LoginServer? _server;
        private static bool _isRunning = true;

        static async Task Main(string[] args)
        {
            Console.Title = "MirWorld Login Server - C# 版本";
            Console.WriteLine("===========================================");
            Console.WriteLine("   传世登录服务器 - C# 版本");
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

                
                _server = new LoginServer(iniReader);

                if (await _server.Initialize())
                {
                    LogManager.Default.Info("登录服务器初始化成功");
                    
                    await _server.Start();
                    
                    
                    _ = Task.Run(() => CommandLoop());
                    
                    
                    while (_isRunning)
                    {
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    LogManager.Default.Error("登录服务器初始化失败，请检查后重启本程序！");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Fatal("严重错误", exception: ex);
            }
            finally
            {
                _server?.Stop();
                LogManager.Default.Info("登录服务器已停止");
                LogManager.Shutdown();
            }
        }

        private static void CommandLoop()
        {
            Console.WriteLine("输入命令 (help - 显示帮助, exit - 退出):");
            while (_isRunning)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                string[] parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                string command = parts[0].ToLower();

                switch (command)
                {
                    case "help":
                        ShowHelp();
                        break;

                    case "exit":
                    case "quit":
                        Console.WriteLine("正在关闭服务器...");
                        _isRunning = false;
                        break;

                    case "status":
                        ShowStatus();
                        break;

                    case "connections":
                        ShowConnections();
                        break;

                    case "clear":
                        Console.Clear();
                        break;

                    default:
                        Console.WriteLine($"未知命令: {command}");
                        break;
                }
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("可用命令:");
            Console.WriteLine("  help        - 显示此帮助信息");
            Console.WriteLine("  status      - 显示服务器状态");
            Console.WriteLine("  connections - 显示当前连接数");
            Console.WriteLine("  clear       - 清屏");
            Console.WriteLine("  exit/quit   - 退出服务器");
        }

        private static void ShowStatus()
        {
            if (_server != null)
            {
                Console.WriteLine("服务器状态:");
                Console.WriteLine($"  运行时间: {_server.GetUptime()}");
                Console.WriteLine($"  当前连接: {_server.GetConnectionCount()}");
                Console.WriteLine($"  登录成功: {_server.GetLoginSuccessCount()}");
                Console.WriteLine($"  注册账号: {_server.GetRegisterCount()}");
            }
        }

        private static void ShowConnections()
        {
            if (_server != null)
            {
                Console.WriteLine($"当前连接数: {_server.GetConnectionCount()}");
            }
        }
    }

    
    
    
    
    
    
    
    
    
    
    
    
    
    

    
    
    
    public class LoginServer
    {
        private readonly IniFileReader _config;
        
        private TcpListener? _listener;
        private readonly List<LoginClient> _clients = new();
        private readonly object _clientLock = new();
        private DateTime _startTime;
        private long _loginSuccessCount = 0;
        private long _registerCount = 0;
        private bool _isRunning = false;
        
        

        private string _addr = "127.0.0.1";
        private int _port = 7000;
        private string _name = "淡抹夕阳";
        private int _maxconnection = 1024;
        private int _disableregister = 1;
        private string _dbServerAddress = "127.0.0.1";
        private int _dbServerPort = 8000;
        
        
        private string _serverCenterAddress = "127.0.0.1";
        private int _serverCenterPort = 6000;
        private MirCommon.Network.ServerCenterClient? _serverCenterClient;
        private byte _serverCenterIndex = 0; 

        private string ServerTips = "欢迎来到传世服务器";
        private string LoginOkTips = "登录成功！";
        private string RegisterTips = "注册成功！";

        public LoginServer(IniFileReader config)
        {
            _config = config;
        }

        public async Task<bool> Initialize()
        {
            try
            {
                
                string sectionName = "登陆服务器";
                _addr = _config.GetString(sectionName, "addr", "127.0.0.1");
                _port = _config.GetInteger(sectionName, "port", 7000);
                _name = _config.GetString(sectionName, "name", " 淡抹夕阳");
                _maxconnection = _config.GetInteger(sectionName, "maxconnection", 1024);
                _disableregister = _config.GetInteger(sectionName, "disableregister", 1);
                
                
                string dbSectionName = "数据库服务器";
                _dbServerAddress = _config.GetString(dbSectionName, "addr", "127.0.0.1");
                _dbServerPort = _config.GetInteger(dbSectionName, "port", 8000);
                
                
                string scSectionName = "服务器中心";
                string serverCenterAddress = _config.GetString(scSectionName, "addr", "127.0.0.1");
                int serverCenterPort = _config.GetInteger(scSectionName, "port", 6000);

                
                LogManager.Default.Info("正在初始化DBServer连接...");
                LogManager.Default.Info($"DBServer地址: {_dbServerAddress}:{_dbServerPort}");
                
                
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("DBServer连接测试失败");
                    return false;
                }
                
                LogManager.Default.Info("DBServer连接测试成功");
                
                
                LogManager.Default.Info("正在向ServerCenter注册...");
                _serverCenterAddress = serverCenterAddress;
                _serverCenterPort = serverCenterPort;
                
                _serverCenterClient = new MirCommon.Network.ServerCenterClient(serverCenterAddress, serverCenterPort);
                if (await _serverCenterClient.ConnectAsync())
                {
                    bool registered = await _serverCenterClient.RegisterServerAsync("LoginServer", _name, _addr, _port, _maxconnection);
                    if (registered)
                    {
                        LogManager.Default.Info("ServerCenter注册成功");
                        
                        
                        
                    }
                    else
                    {
                        LogManager.Default.Warning("ServerCenter注册失败");
                        _serverCenterClient.Dispose();
                        _serverCenterClient = null;
                    }
                }
                else
                {
                    LogManager.Default.Warning("无法连接到ServerCenter");
                    _serverCenterClient = null;
                }

                LogManager.Default.Info($"监听端口: {_port}");
                LogManager.Default.Info($"最大连接: {_maxconnection}");
                LogManager.Default.Info($"允许注册: {(_disableregister == 1 ? "是" : "否")}");

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

            LogManager.Default.Info($"登录服务器已启动，等待连接...");

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
                        {
                            LogManager.Default.Error($"接受连接错误: {ex.Message}");
                        }
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
                foreach (var client in _clients.ToList())
                {
                    client.Disconnect();
                }
                _clients.Clear();
            }
        }

        private async Task HandleClient(TcpClient tcpClient)
        {
            var client = new LoginClient(tcpClient, this, LoginOkTips, _disableregister == 1, RegisterTips, _dbServerAddress, _dbServerPort);

            lock (_clientLock)
            {
                if (_clients.Count >= _maxconnection)
                {
                    LogManager.Default.Warning($"连接已满，拒绝新连接");
                    tcpClient.Close();
                    return;
                }
                _clients.Add(client);
            }

            string? remoteEp = tcpClient.Client.RemoteEndPoint?.ToString();
            LogManager.Default.Info($"新连接: {remoteEp}");

            
            client.SendTipMessage(ServerTips);

            try
            {
                await client.ProcessAsync();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理客户端错误: {ex.Message}");
            }
            finally
            {
                lock (_clientLock)
                {
                    _clients.Remove(client);
                }
                LogManager.Default.Info($"连接断开: {remoteEp}");
            }
        }

        public void IncrementLoginSuccess()
        {
            Interlocked.Increment(ref _loginSuccessCount);
        }

        public void IncrementRegister()
        {
            Interlocked.Increment(ref _registerCount);
        }

        public TimeSpan GetUptime() => DateTime.Now - _startTime;
        public int GetConnectionCount()
        {
            lock (_clientLock)
            {
                return _clients.Count;
            }
        }
        public long GetLoginSuccessCount() => Interlocked.Read(ref _loginSuccessCount);
        public long GetRegisterCount() => Interlocked.Read(ref _registerCount);
        
        
        
        
        public MirCommon.Network.ServerCenterClient? GetServerCenterClient()
        {
            return _serverCenterClient;
        }
    }

    
    
    
    public class LoginClient
    {
        private readonly TcpClient _client;
        private readonly LoginServer _server;
        private readonly NetworkStream _stream;
        private string _account = string.Empty;
        private uint _loginId = 0;
        private int _failCount = 0;
        private DateTime _lastActivity;
        private string LoginOkTips= string.Empty;
        private bool AllowRegister = false;
        private string RegisterTips = string.Empty;
        private readonly string _dbServerAddress;
        private readonly int _dbServerPort;

        public LoginClient(TcpClient client, LoginServer server, string _loginOkTips, bool _allowRegister, string _registerTips, string dbServerAddress, int dbServerPort)
        {
            _client = client;
            _server = server;
            LoginOkTips = _loginOkTips;
            AllowRegister = _allowRegister;
            RegisterTips = _registerTips;
            _dbServerAddress = dbServerAddress;
            _dbServerPort = dbServerPort;
            _stream = client.GetStream();
            _lastActivity = DateTime.Now;
        }

        public async Task ProcessAsync()
        {
            byte[] buffer = new byte[8192];

            while (_client.Connected)
            {
                try
                {
                    
                    if ((DateTime.Now - _lastActivity).TotalMinutes > 5)
                    {
                        LogManager.Default.Info("连接超时");
                        break;
                    }

                    
                    if (_failCount >= 16)
                    {
                        LogManager.Default.Warning("失败次数过多，断开连接");
                        break;
                    }

                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    _lastActivity = DateTime.Now;

                    
                    await ProcessMessage(buffer, bytesRead);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"处理消息错误: {ex.Message}");
                    break;
                }
            }
        }

        private async Task ProcessMessage(byte[] data, int length)
        {
            try
            {
                
                int parsedSize = 0;
                int msgPtr = 0;
                
                do
                {
                    parsedSize = ParseSingleMessage(data, msgPtr, length - msgPtr);
                    if (parsedSize > 0)
                    {
                        msgPtr += parsedSize;
                    }
                } while (parsedSize > 0 && msgPtr < length);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析消息失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        
        
        
        private int ParseSingleMessage(byte[] data, int startIndex, int length)
        {
            char startChar = '#';
            char endChar = '!';
            int parsedSize = 0;
            int messageStart = -1;
            
            for (int i = 0; i < length; i++)
            {
                int currentIndex = startIndex + i;
                char currentChar = (char)data[currentIndex];
                
                if (currentChar == '*')
                {
                    
                    parsedSize = i + 1;
                }
                else if (currentChar == startChar)
                {
                    messageStart = currentIndex + 1; 
                }
                else if (currentChar == endChar)
                {
                    if (messageStart != -1)
                    {
                        
                        int encodedLength = currentIndex - messageStart;
                        byte[] encodedData = new byte[encodedLength];
                        Array.Copy(data, messageStart, encodedData, 0, encodedLength);
                        
                        
                        string encodedHex = BitConverter.ToString(encodedData).Replace("-", " ");
                        LogManager.Default.Debug($"原始编码数据({encodedLength}字节): {encodedHex}");
                        
                        
                        int decodeStart = 0;
                        if (encodedLength > 0 && encodedData[0] >= '0' && encodedData[0] <= '9')
                        {
                            decodeStart = 1;
                            LogManager.Default.Debug($"跳过数字字符: {(char)encodedData[0]}");
                        }
                        
                        
                        byte[] decoded = new byte[(encodedLength - decodeStart) * 3 / 4 + 4];
                        byte[] dataToDecode = new byte[encodedLength - decodeStart];
                        Array.Copy(encodedData, decodeStart, dataToDecode, 0, dataToDecode.Length);
                        int decodedSize = GameCodec.UnGameCode(dataToDecode, decoded);
                        
                        
                        string decodedHex = BitConverter.ToString(decoded, 0, Math.Min(decodedSize, 64)).Replace("-", " ");
                        LogManager.Default.Debug($"解码后数据({decodedSize}字节): {decodedHex}");
                        
                        if (decodedSize >= 12) 
                        {
                            var reader = new PacketReader(decoded);
                            uint dwFlag = reader.ReadUInt32();
                            ushort wCmd = reader.ReadUInt16();
                            ushort w1 = reader.ReadUInt16();
                            ushort w2 = reader.ReadUInt16();
                            ushort w3 = reader.ReadUInt16();
                            
                            byte[] payload = reader.ReadBytes(decodedSize - 12);
                            string dataStr = GetString(payload);  

                            LogManager.Default.Debug($"收到消息: Cmd={wCmd:X4}, Data={dataStr}");

                            
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await ProcessDecodedMessage(wCmd, dataStr, payload);
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Default.Error($"处理消息失败: {ex.Message}");
                                }
                            });
                        }
                        else
                        {
                            LogManager.Default.Warning($"解码后数据太小: {decodedSize}字节");
                        }
                        
                        messageStart = -1;
                    }
                    parsedSize = i + 1;
                }
            }
            
            return parsedSize;
        }

        
        
        
        private async Task ProcessDecodedMessage(ushort wCmd, string dataStr, byte[] payload)
        {
            switch (wCmd)
            {
                case ProtocolCmd.CM_LOGIN:
                case ProtocolCmd.CM_PTLOGIN:
                    await HandleLogin(dataStr);
                    break;

                case ProtocolCmd.CM_REGISTERACCOUNT:
                    await HandleRegister(payload);
                    break;

                case ProtocolCmd.CM_CHECKACCOUNTEXIST:
                    await HandleCheckAccount(dataStr);
                    break;

                case ProtocolCmd.CM_CHANGEPASSWORD:
                    await HandleChangePassword(dataStr);
                    break;

                case ProtocolCmd.CM_SELECTSERVER:
                    await HandleSelectServer(dataStr);
                    break;

                case 0x1D8E: 
                    await HandleCheckCharName(dataStr);
                    break;

                default:
                    _failCount++;
                    LogManager.Default.Warning($"未知消息: {wCmd:X4}");
                    break;
            }
        }

        private async Task HandleLogin(string data)
        {
            try
            {
                
                string[] parts = data.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    SendLoginFail(-2);
                    return;
                }

                _account = parts[0];
                string password = parts[1];

                LogManager.Default.Info($"登录请求: 账号={_account}");

                
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendLoginFail(-2);
                    return;
                }

                bool isValid = await dbClient.CheckAccountAsync(_account, password);
                if (!isValid)
                {
                    LogManager.Default.Warning($"账号密码验证失败: {_account}");
                    SendLoginFail(-2);
                    return;
                }

                
                _loginId = (uint)Random.Shared.Next(1000000, 9999999);
                
                SendLoginSuccess(_loginId);
                SendTipMessage(LoginOkTips);
                
                _server.IncrementLoginSuccess();
                LogManager.Default.Info($"登录成功: {_account}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理登录失败: {ex.Message}");
                SendLoginFail(-2);
            }

            await Task.CompletedTask;
        }

        private async Task HandleRegister(byte[] data)
        {
            try
            {
                if (!AllowRegister)
                {
                    SendMessage(0, ProtocolCmd.SM_REGISTERACCOUNTFAIL, 0, 0, 0, null);
                    SendTipMessage("账号注册被禁止");
                    return;
                }

                LogManager.Default.Info("注册请求");

                
                if (data.Length < 311)
                {
                    SendMessage(0, ProtocolCmd.SM_REGISTERACCOUNTFAIL, 0, 0, 0, null);
                    SendTipMessage("注册信息不完整");
                    return;
                }

                
                int offset = 0;
                
                
                byte accountLength = data[offset++];
                string account = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)accountLength, 10)).TrimEnd('\0');
                offset += 10;
                
                
                byte passwordLength = data[offset++];
                string password = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)passwordLength, 10)).TrimEnd('\0');
                offset += 10;
                
                
                byte nameLength = data[offset++];
                string name = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)nameLength, 20)).TrimEnd('\0');
                offset += 20;
                
                
                byte idCardLength = data[offset++];
                string idCard = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)idCardLength, 19)).TrimEnd('\0');
                offset += 19;
                
                
                byte phoneNumberLength = data[offset++];
                string phoneNumber = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)phoneNumberLength, 14)).TrimEnd('\0');
                offset += 14;
                
                
                byte q1Length = data[offset++];
                string q1 = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)q1Length, 20)).TrimEnd('\0');
                offset += 20;
                
                
                byte a1Length = data[offset++];
                string a1 = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)a1Length, 20)).TrimEnd('\0');
                offset += 20;
                
                
                byte emailLength = data[offset++];
                string email = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)emailLength, 40)).TrimEnd('\0');
                offset += 40;
                
                
                byte q2Length = data[offset++];
                string q2 = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)q2Length, 20)).TrimEnd('\0');
                offset += 20;
                
                
                byte a2Length = data[offset++];
                string a2 = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)a2Length, 20)).TrimEnd('\0');
                offset += 20;
                
                
                byte birthdayLength = data[offset++];
                string birthday = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)birthdayLength, 10)).TrimEnd('\0');
                offset += 10;
                
                
                byte mobileNumberLength = data[offset++];
                string mobilePhoneNumber = System.Text.Encoding.GetEncoding("GBK").GetString(data, offset, Math.Min((int)mobileNumberLength, 11)).TrimEnd('\0');
                offset += 11;
                
                
                offset += 85;

                LogManager.Default.Debug($"注册信息: 账号={account}, 姓名={name}, 邮箱={email}");

                
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendMessage(0, ProtocolCmd.SM_REGISTERACCOUNTFAIL, 0, 0, 0, null);
                    SendTipMessage("服务器内部错误");
                    return;
                }

                
                bool exists = await dbClient.CheckAccountExistAsync(account);
                if (exists)
                {
                    SendMessage(0, ProtocolCmd.SM_REGISTERACCOUNTFAIL, 0, 0, 0, null);
                    SendTipMessage("账号已存在");
                    return;
                }

                
                bool success = await dbClient.CreateAccountAsync(account, password, name, birthday, q1, a1, q2, a2, email, phoneNumber, mobilePhoneNumber, idCard);
                if (!success)
                {
                    SendMessage(0, ProtocolCmd.SM_REGISTERACCOUNTFAIL, 0, 0, 0, null);
                    SendTipMessage("注册失败");
                    return;
                }

                SendMessage(0, ProtocolCmd.SM_REGISTERACCOUNTOK, 0, 0, 0, null);
                SendTipMessage(RegisterTips);
                
                _server.IncrementRegister();
                LogManager.Default.Info($"注册成功: {account}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理注册失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SM_REGISTERACCOUNTFAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleCheckAccount(string data)
        {
            try
            {
                string account = data.Trim();
                LogManager.Default.Debug($"检查账号: {account}");
                
                
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendMessage(0, ProtocolCmd.SM_CHECKACCOUNTEXISTRET, 0, 0, 0, null);
                    return;
                }

                bool exists = await dbClient.CheckAccountExistAsync(account);
                SendMessage(exists ? 1u : 0u, ProtocolCmd.SM_CHECKACCOUNTEXISTRET, 0, 0, 0, null);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"检查账号失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SM_CHECKACCOUNTEXISTRET, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleChangePassword(string data)
        {
            try
            {
                
                string[] parts = data.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    SendMessage(0, ProtocolCmd.SM_CHANGEPASSWORDFAIL, 0, 0, 0, null);
                    return;
                }

                string account = parts[0];
                string oldPassword = parts[1];
                string newPassword = parts[2];

                LogManager.Default.Info($"修改密码请求: {account}");

                
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendMessage(0, ProtocolCmd.SM_CHANGEPASSWORDFAIL, 0, 0, 0, null);
                    return;
                }

                bool success = await dbClient.ChangePasswordAsync(account, oldPassword, newPassword);
                if (!success)
                {
                    SendMessage(0, ProtocolCmd.SM_CHANGEPASSWORDFAIL, 0, 0, 0, null);
                    SendTipMessage("修改密码失败，请检查旧密码是否正确");
                    return;
                }

                SendMessage(0, ProtocolCmd.SM_CHANGEPASSWORDOK, 0, 0, 0, null);
                SendTipMessage("密码修改成功");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"修改密码失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SM_CHANGEPASSWORDFAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleCheckCharName(string data)
        {
            try
            {
                string charName = data.Trim();
                LogManager.Default.Debug($"检查角色名: {charName}");
                
                
                
                if (string.IsNullOrEmpty(charName) || charName.Length < 2 || charName.Length > 14)
                {
                    SendCheckCharNameResult(false, "角色名长度必须在2-14个字符之间");
                    return;
                }
                
                
                foreach (char c in charName)
                {
                    if (c < 32 || c > 126)
                    {
                        SendCheckCharNameResult(false, "角色名包含非法字符");
                        return;
                    }
                }
                
                
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendCheckCharNameResult(false, "服务器内部错误");
                    return;
                }
                
                
                string serverName = "淡抹夕阳"; 
                bool exists = await dbClient.CheckCharacterNameExistsAsync(serverName, charName);
                
                if (exists)
                {
                    SendCheckCharNameResult(false, "角色名已存在");
                    return;
                }
                
                SendCheckCharNameResult(true, "角色名可用");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"检查角色名失败: {ex.Message}");
                SendCheckCharNameResult(false, "检查角色名失败");
            }

            await Task.CompletedTask;
        }

        private async Task HandleSelectServer(string data)
        {
            try
            {
                if (_loginId == 0)
                {
                    SendTipMessage("您还没有登录！");
                    return;
                }

                string serverName = data.Trim();
                LogManager.Default.Info($"选择服务器: {serverName}, 账号: {_account}, LoginId: {_loginId}");

                
                var iniReader = new IniFileReader("config.ini");
                if (!iniReader.Open())
                {
                    SendTipMessage("配置文件读取失败");
                    return;
                }
                
                string selectCharServerAddr = "";
                int selectCharServerPort = 0;
                
                
                string scSectionName = "服务器中心";
                string serverCenterAddress = iniReader.GetString(scSectionName, "addr", "127.0.0.1");
                int serverCenterPort = iniReader.GetInteger(scSectionName, "port", 6000);
                
                bool foundFromServerCenter = false;
                byte selectCharServerIndex = 0; 
                try
                {
                    LogManager.Default.Debug($"开始连接ServerCenter: {serverCenterAddress}:{serverCenterPort}");
                    
                    
                    var serverCenterClient = _server.GetServerCenterClient();
                    if (serverCenterClient != null && serverCenterClient.IsConnected())
                    {
                        LogManager.Default.Debug($"已连接到ServerCenter（使用现有连接）");
                        
                        LogManager.Default.Debug($"开始查找SelectCharServer - 服务器类型: SelectCharServer, 服务器名: {serverName}");
                        FindServerResult? selectCharServerInfo = await serverCenterClient.FindServerAsync("SelectCharServer", serverName);
                        if (selectCharServerInfo!=null)
                        {
                            
                            byte[] addrBytes = selectCharServerInfo.Value.addr.addr;
                            
                            int nullIndex = Array.IndexOf(addrBytes, (byte)0);
                            if (nullIndex < 0) nullIndex = addrBytes.Length;
                            
                            selectCharServerAddr = Encoding.GetEncoding("GBK").GetString(addrBytes, 0, nullIndex).Trim();
                            selectCharServerPort = (int)selectCharServerInfo.Value.addr.nPort;
                            selectCharServerIndex = selectCharServerInfo.Value.Id.bIndex; 
                            foundFromServerCenter = true;
                            LogManager.Default.Info($"从ServerCenter找到SelectCharServer: {selectCharServerAddr}:{selectCharServerPort}, 索引: {selectCharServerIndex}");
                            LogManager.Default.Debug($"FindServer成功 - 地址: {selectCharServerAddr}:{selectCharServerPort}, 目标索引: {selectCharServerIndex}");
                        }
                        else
                        {
                            LogManager.Default.Warning($"ServerCenter未找到SelectCharServer: {serverName}");
                        }
                    }
                    else
                    {
                        LogManager.Default.Warning($"ServerCenter连接不可用，尝试创建新连接");
                        
                        using var scClient = new MirCommon.Network.ServerCenterClient(serverCenterAddress, serverCenterPort);
                        if (await scClient.ConnectAsync())
                        {
                            LogManager.Default.Debug($"已连接到ServerCenter（新连接）");
                            FindServerResult? selectCharServerInfo = await scClient.FindServerAsync("SelectCharServer", serverName);
                            if (selectCharServerInfo!=null)
                            {
                                byte[] addrBytes = selectCharServerInfo.Value.addr.addr;
                                int nullIndex = Array.IndexOf(addrBytes, (byte)0);
                                if (nullIndex < 0) nullIndex = addrBytes.Length;
                                selectCharServerAddr = Encoding.GetEncoding("GBK").GetString(addrBytes, 0, nullIndex).Trim();
                                selectCharServerPort = (int)selectCharServerInfo.Value.addr.nPort;
                                selectCharServerIndex = selectCharServerInfo.Value.Id.bIndex;
                                foundFromServerCenter = true;
                                LogManager.Default.Info($"从ServerCenter找到SelectCharServer: {selectCharServerAddr}:{selectCharServerPort}, 索引: {selectCharServerIndex}");
                            }
                        }
                        else
                        {
                            LogManager.Default.Warning($"无法连接到ServerCenter: {serverCenterAddress}:{serverCenterPort}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Warning($"ServerCenter查询失败: {ex.Message}，将使用配置文件回退方案");
                }
                
                
                if (!foundFromServerCenter)
                {
                    LogManager.Default.Info("从config.ini读取SelectCharServer配置");
                    string selCharSectionName = "选人服务器";
                    string configServerName = iniReader.GetString(selCharSectionName, "name", "");
                    
                    
                    if (configServerName != serverName)
                    {
                        LogManager.Default.Warning($"配置的服务器名称({configServerName})与请求的服务器名称({serverName})不匹配");
                        SendTipMessage("您选择的服务器不存在！");
                        return;
                    }
                    
                    selectCharServerAddr = iniReader.GetString(selCharSectionName, "addr", "127.0.0.1");
                    selectCharServerPort = iniReader.GetInteger(selCharSectionName, "port", 7100);
                    LogManager.Default.Info($"从配置文件读取SelectCharServer: {selectCharServerAddr}:{selectCharServerPort}");
                }
                
                
                if (string.IsNullOrEmpty(selectCharServerAddr) || selectCharServerPort == 0)
                {
                    LogManager.Default.Error("无法获取SelectCharServer地址");
                    SendTipMessage("您选择的服务器不存在！");
                    return;
                }
                
                
                uint selectId = (uint)Random.Shared.Next(10000000, 99999999);
                
                
                if (foundFromServerCenter)
                {
                    try
                    {
                        
                        
                        
                        
                        
                        
                        
                        
                        var enterSelCharServer = new MirCommon.EnterSelCharServer
                        {
                            nClientId = 0, 
                            nLoginId = _loginId
                        };
                        enterSelCharServer.SetAccount(_account);
                        
                        
                        byte[] selectIdBytes = BitConverter.GetBytes(selectId);
                        Array.Copy(selectIdBytes, 0, enterSelCharServer.reserved, 0, 4);
                        
                        
                        int structSize = Marshal.SizeOf<MirCommon.EnterSelCharServer>();
                        byte[] structData = new byte[structSize];
                        IntPtr ptr = Marshal.AllocHGlobal(structSize);
                        try
                        {
                            Marshal.StructureToPtr(enterSelCharServer, ptr, false);
                            Marshal.Copy(ptr, structData, 0, structSize);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                        
                        LogManager.Default.Debug($"开始发送跨服务器消息到SelectCharServer:");
                        LogManager.Default.Debug($"  - 命令: MAS_ENTERSELCHARSERVER (0x{ProtocolCmd.MAS_ENTERSELCHARSERVER:X4})");
                        LogManager.Default.Debug($"  - 发送类型: MST_SINGLE (0x{ProtocolCmd.MST_SINGLE:X2})");
                        LogManager.Default.Debug($"  - 目标索引: {selectCharServerIndex} (SelectCharServer的实际索引)");
                        LogManager.Default.Debug($"  - 数据: EnterSelCharServer结构体 (大小: {structSize}字节)");
                        LogManager.Default.Debug($"  - 账号: {_account}, LoginId: {_loginId}");
                        
                        
                        var serverCenterClient = _server.GetServerCenterClient();
                        if (serverCenterClient != null && serverCenterClient.IsConnected())
                        {
                            LogManager.Default.Debug($"使用现有ServerCenter连接发送跨服务器消息");
                            
                            
                            
                            
                            
                            byte senderServerType = 2; 
                            byte senderServerIndex = 1; 
                            
                            
                            
                            
                            bool sent = await serverCenterClient.SendMsgAcrossServerAsync(
                                0, 
                                ProtocolCmd.MAS_ENTERSELCHARSERVER, 
                                0, 
                                selectCharServerIndex, 
                                structData
                            );
                            
                            if (sent)
                            {
                                LogManager.Default.Info($"已通过ServerCenter向SelectCharServer注册登录信息: 账号={_account}, LoginId={_loginId}, 目标索引={selectCharServerIndex}");
                                LogManager.Default.Debug($"发送跨服务器消息成功");
                                LogManager.Default.Debug($"发送者信息: 服务器类型={senderServerType} (ST_LOGINSERVER), 服务器索引={senderServerIndex}");
                            }
                            else
                            {
                                LogManager.Default.Warning($"发送跨服务器消息失败");
                            }
                        }
                        else
                        {
                            LogManager.Default.Warning($"ServerCenter连接不可用，无法发送登录信息");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"向SelectCharServer发送登录信息失败: {ex.Message}");
                    }
                }
                else
                {
                    
                    string loginInfo = $"{_loginId}/{selectId}/{_account}";
                    LogManager.Default.Info($"使用配置文件回退方案，SelectId={selectId}, Account={_account}");
                }
                
                
                
                string serverInfo = $"{selectCharServerAddr}/{selectCharServerPort}/{selectId}";
                byte[] serverData = GetBytes(serverInfo);  
                SendMessage(0, ProtocolCmd.SM_SELECTSERVEROK, 0, 0, 0, serverData);
                
                LogManager.Default.Info($"选择服务器成功: 账号={_account}, 服务器={serverName}, SelectId={selectId}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"选择服务器失败: {ex.Message}", exception: ex);
                SendTipMessage("服务器不存在");
            }

            await Task.CompletedTask;
        }

        private void SendLoginFail(int error)
        {
            SendMessage((uint)error, ProtocolCmd.SM_LOGINFAIL, 0, 0, 0, null);
        }

        private void SendLoginSuccess(uint loginId)
        {
            string data = $"*{loginId}";
            byte[] buffer = GetBytes(data);  
            SendMessage(0, ProtocolCmd.SM_LOGINOK, 0, 0, 0, buffer);
        }

        public void SendTipMessage(string message)
        {
            byte[] buffer = GetBytes(message);  
            SendMessage(0, ProtocolCmd.SM_TIPWINDOW, 0, 0, 0, buffer);
        }

        private void SendCheckCharNameResult(bool available, string message)
        {
            
            uint result = available ? 1u : 0u;
            byte[] buffer = GetBytes(message);  
            SendMessage(result, 0x1D8E, 0, 0, 0, buffer);
        }

        private void SendMessage(uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, byte[]? data)
        {
            try
            {
                
                byte[] encoded = new byte[8192];
                int encodedSize = GameCodec.EncodeMsg(encoded, dwFlag, wCmd, w1, w2, w3, data, data?.Length ?? 0);
                
                _stream.Write(encoded, 0, encodedSize);
                _stream.Flush();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
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
    }
}
