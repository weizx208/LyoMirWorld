using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MirCommon.Network;

namespace MirCommon.Database
{
    
    
    
    public class DBServerClient : IDisposable
    {
        private readonly string _serverAddress;
        private readonly int _serverPort;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _connected = false;
        private CancellationTokenSource? _listeningCts;
        private Task? _listeningTask;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        
        private readonly List<byte> _recvBuffer = new List<byte>(8192);

        
        public event Action<MirMsg>? OnDbMessageReceived;
        public event Action<string>? OnLogMessage;

        public DBServerClient(string address = "127.0.0.1", int port = 8000)
        {
            _serverAddress = address;
            _serverPort = port;
        }

        
        
        
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_serverAddress, _serverPort);
                _stream = _client.GetStream();
                _connected = true;
                
                Log($"已连接到DBServer: {_serverAddress}:{_serverPort}");
                return true;
            }
            catch (Exception ex)
            {
                _connected = false;
                Log($"连接到DBServer失败: {ex.Message}");
                return false;
            }
        }

        
        
        
        public void StartListening()
        {
            if (!_connected || _stream == null)
            {
                Log("无法启动监听：未连接到DBServer");
                return;
            }

            if (_listeningTask != null && !_listeningTask.IsCompleted)
            {
                Log("监听任务已在运行");
                return;
            }

            _listeningCts = new CancellationTokenSource();
            _listeningTask = Task.Run(async () => await ListenToDbServerAsync(_listeningCts.Token));
            Log("已启动DBServer消息监听");
        }

        
        
        
        public void StopListening()
        {
            _listeningCts?.Cancel();
            _listeningTask = null;
            Log("已停止DBServer消息监听");
        }

        
        
        
        private async Task ListenToDbServerAsync(CancellationToken cancellationToken)
        {
            if (_stream == null) return;

            byte[] buffer = new byte[8192];
            int reconnectAttempts = 0;
            const int maxReconnectAttempts = 3;

            while (!cancellationToken.IsCancellationRequested && _connected)
            {
                try
                {
                    if (!_client?.Connected ?? true)
                    {
                        Log("DBServer连接已断开，尝试重新连接...");
                        reconnectAttempts++;
                        if (reconnectAttempts > maxReconnectAttempts)
                        {
                            Log($"达到最大重连次数({maxReconnectAttempts})，停止监听");
                            break;
                        }

                        if (await ConnectAsync())
                        {
                            reconnectAttempts = 0;
                            continue;
                        }
                        else
                        {
                            await Task.Delay(5000, cancellationToken);
                            continue;
                        }
                    }

                    
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        Log($"收到DBServer消息: {bytesRead}字节");
                        await ProcessReceivedData(buffer, bytesRead);
                    }
                    else if (bytesRead == 0)
                    {
                        
                        Log("DBServer连接已关闭");
                        _connected = false;
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    
                    Log("DBServer监听任务被取消");
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Log($"读取DBServer消息失败: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }

            Log("DBServer监听任务已停止");
        }

        
        
        
        private async Task ProcessReceivedData(byte[] data, int length)
        {
            try
            {
                if (data == null || length <= 0)
                    return;

                for (int i = 0; i < length; i++)
                {
                    _recvBuffer.Add(data[i]);
                }

                const int maxBufferedBytes = 1024 * 1024; 
                if (_recvBuffer.Count > maxBufferedBytes)
                {
                    Log($"DBServer接收缓存过大({_recvBuffer.Count}字节)，已清空以防止内存膨胀");
                    _recvBuffer.Clear();
                    return;
                }

                
                while (true)
                {
                    int start = _recvBuffer.IndexOf((byte)'#');
                    if (start < 0)
                    {
                        
                        _recvBuffer.Clear();
                        break;
                    }

                    if (start > 0)
                    {
                        
                        _recvBuffer.RemoveRange(0, start);
                    }

                    int end = _recvBuffer.IndexOf((byte)'!', 1);
                    if (end < 0)
                    {
                        
                        break;
                    }

                    int encodedLength = end - 1; 
                    if (encodedLength <= 0)
                    {
                        
                        _recvBuffer.RemoveRange(0, end + 1);
                        continue;
                    }

                    byte[] encodedData = _recvBuffer.GetRange(1, encodedLength).ToArray();
                    _recvBuffer.RemoveRange(0, end + 1);

                    try
                    {
                        
                        byte[] decoded = new byte[encodedLength * 3 / 4 + 4];
                        int decodedSize = GameCodec.UnGameCode(encodedData, decoded);

                        if (decodedSize < 12)
                        {
                            Log($"解码后的DBServer消息太小: {decodedSize}字节");
                            continue;
                        }

                        
                        var reader = new PacketReader(decoded);
                        uint dwFlag = reader.ReadUInt32();
                        ushort wCmd = reader.ReadUInt16();
                        ushort w1 = reader.ReadUInt16();
                        ushort w2 = reader.ReadUInt16();
                        ushort w3 = reader.ReadUInt16();
                        byte[] msgData = reader.ReadBytes(decodedSize - 12);

                        var msg = new MirMsg
                        {
                            dwFlag = dwFlag,
                            wCmd = wCmd,
                            wParam = new ushort[3] { w1, w2, w3 },
                            data = msgData
                        };

                        Log($"解析DBServer消息: Cmd=0x{wCmd:X4}({(DbMsg)wCmd}), Flag=0x{dwFlag:X8}, w1={w1}, w2={w2}, w3={w3}, 数据长度={msgData.Length}字节");

                        OnDbMessageReceived?.Invoke(msg);
                    }
                    catch (Exception ex)
                    {
                        Log($"解码/解析DBServer消息失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"处理DBServer消息失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        
        
        
        private async Task<DbResponse?> SendDbMessageAsync(DbMsg msgType, byte[] payload, ushort wParam1 = 0, ushort wParam2 = 0, ushort wParam3 = 0, uint dwFlag = 0)
        {
            if (_stream == null) return null;

            try
            {
                
                
                byte[] encoded = new byte[8192];
                int encodedSize = GameCodec.EncodeMsg(encoded, dwFlag, (ushort)msgType, wParam1, wParam2, wParam3, payload, payload.Length);

                
                await _stream.WriteAsync(encoded, 0, encodedSize);
                await _stream.FlushAsync();

                
                byte[] buffer = new byte[8192];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 3) return null; 

                
                int startIndex = -1;
                int endIndex = -1;
                
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == '#')
                    {
                        startIndex = i + 1; 
                    }
                    else if (buffer[i] == '!')
                    {
                        endIndex = i; 
                        break;
                    }
                }
                
                if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
                {
                    return null;
                }
                
                
                int encodedLength = endIndex - startIndex;
                byte[] encodedData = new byte[encodedLength];
                Array.Copy(buffer, startIndex, encodedData, 0, encodedLength);
                
                
                byte[] decoded = new byte[encodedLength * 3 / 4 + 4]; 
                int decodedSize = GameCodec.UnGameCode(encodedData, decoded);
                
                if (decodedSize < 12) return null;

                
                var reader = new PacketReader(decoded);
                uint responseDwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                ushort responseW1 = reader.ReadUInt16();
                ushort responseW2 = reader.ReadUInt16();
                ushort responseW3 = reader.ReadUInt16();
                byte[] data = reader.ReadBytes(decodedSize - 12);

                Log($"收到DBServer响应: Cmd=0x{wCmd:X4}({(DbMsg)wCmd}), Flag=0x{responseDwFlag:X8}, w1={responseW1}, w2={responseW2}, w3={responseW3}, 数据长度={data.Length}字节");

                return new DbResponse
                {
                    dwFlag = responseDwFlag,
                    wCmd = wCmd,
                    data = data
                };
            }
            catch (Exception ex)
            {
                Log($"SendDbMessageAsync失败: {ex.Message}");
                return null;
            }
        }

        
        
        
        
        private async Task<bool> SendDbMessageNoWaitAsync(DbMsg msgType, byte[] payload, ushort wParam1, ushort wParam2, ushort wParam3, uint dwFlag)
        {
            if (!_connected || _stream == null)
            {
                Log($"SendDbMessageNoWaitAsync失败：未连接到DBServer msgType={msgType}");
                return false;
            }

            try
            {
                byte[] encoded = new byte[8192];
                int encodedSize = GameCodec.EncodeMsg(encoded, dwFlag, (ushort)msgType, wParam1, wParam2, wParam3, payload, payload.Length);

                await _sendLock.WaitAsync();
                try
                {
                    await _stream.WriteAsync(encoded, 0, encodedSize);
                    await _stream.FlushAsync();
                }
                finally
                {
                    _sendLock.Release();
                }

                return true;
            }
            catch (Exception ex)
            {
                Log($"SendDbMessageNoWaitAsync失败: {ex.Message}");
                return false;
            }
        }

        #region GameServer调用方法

        
        
        
        public async Task SendQueryMagic(uint serverId, uint clientKey, uint charId)
        {
            if (!_connected)
            {
                Log("SendQueryMagic跳过：未连接到DBServer");
                return;
            }

            try
            {
                
                
                byte[] payload = BitConverter.GetBytes(charId);
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = 0;

                bool ok = await SendDbMessageNoWaitAsync(DbMsg.DM_QUERYMAGIC, payload, wParam1, wParam2, wParam3, serverId);
                Log(ok
                    ? $"已发送 DM_QUERYMAGIC: serverId={serverId}, clientKey={clientKey}, ownerId={charId}"
                    : $"发送 DM_QUERYMAGIC 失败: serverId={serverId}, clientKey={clientKey}, ownerId={charId}");
            }
            catch (Exception ex)
            {
                Log($"发送 DM_QUERYMAGIC 异常: {ex.Message}");
            }
        }

        
        
        
        public async Task SendQueryItem(uint serverId, uint clientKey, uint charId, byte flag, int count)
        {
            if (!_connected)
            {
                Log("SendQueryItem跳过：未连接到DBServer");
                return;
            }

            try
            {
                
                
                
                byte[] payload = new byte[8];
                BitConverter.GetBytes(clientKey).CopyTo(payload, 0);
                BitConverter.GetBytes(charId).CopyTo(payload, 4);

                ushort wParam1 = 0;
                ushort wParam2 = flag;
                int maxCount = count;
                if (maxCount < 0) maxCount = 0;
                if (maxCount > ushort.MaxValue) maxCount = ushort.MaxValue;
                ushort wParam3 = (ushort)maxCount;

                bool ok = await SendDbMessageNoWaitAsync(DbMsg.DM_QUERYITEMS, payload, wParam1, wParam2, wParam3, serverId);
                Log(ok
                    ? $"已发送 DM_QUERYITEMS: serverId={serverId}, clientKey={clientKey}, ownerId={charId}, flag={flag}, maxCount={maxCount}"
                    : $"发送 DM_QUERYITEMS 失败: serverId={serverId}, clientKey={clientKey}, ownerId={charId}, flag={flag}, maxCount={maxCount}");
            }
            catch (Exception ex)
            {
                Log($"发送 DM_QUERYITEMS 异常: {ex.Message}");
            }
        }

        
        
        
        
        public async Task SendUpdateItemPos(uint itemId, byte flag, ushort pos)
        {
            if (!_connected)
            {
                Log("SendUpdateItemPos跳过：未连接到DBServer");
                return;
            }

            
            if ((itemId & 0x80000000u) != 0)
            {
                Log($"SendUpdateItemPos跳过：临时物品 itemId={itemId}");
                return;
            }

            try
            {
                byte[] payload = new byte[7];
                BitConverter.GetBytes(itemId).CopyTo(payload, 0);
                payload[4] = flag;
                BitConverter.GetBytes(pos).CopyTo(payload, 5);

                bool ok = await SendDbMessageNoWaitAsync(DbMsg.DM_UPDATEITEMPOS, payload, 0, 0, 0, 0);
                Log(ok
                    ? $"已发送 DM_UPDATEITEMPOS: itemId={itemId}, flag={flag}, pos={pos}"
                    : $"发送 DM_UPDATEITEMPOS 失败: itemId={itemId}, flag={flag}, pos={pos}");
            }
            catch (Exception ex)
            {
                Log($"发送 DM_UPDATEITEMPOS 异常: {ex.Message}");
            }
        }

        
        
        
        public async Task SendPutCharDBInfo(CHARDBINFO info)
        {
            if (!_connected)
            {
                Log("SendPutCharDBInfo跳过：未连接到DBServer");
                return;
            }

            try
            {
                byte[] payload = info.ToBytes();
                bool ok = await SendDbMessageNoWaitAsync(DbMsg.DM_PUTCHARDBINFO, payload, 0, 0, 0, 0);
                Log(ok
                    ? $"已发送 DM_PUTCHARDBINFO: dbId={info.dwDBId}, name={info.szName}"
                    : $"发送 DM_PUTCHARDBINFO 失败: dbId={info.dwDBId}, name={info.szName}");
            }
            catch (Exception ex)
            {
                Log($"发送 DM_PUTCHARDBINFO 异常: {ex.Message}");
            }
        }

        
        
        
        public async Task SendUpdateItems(uint ownerId, byte flag, DBITEM[] items)
        {
            if (!_connected)
            {
                Log("SendUpdateItems跳过：未连接到DBServer");
                return;
            }

            try
            {
                if (items == null || items.Length == 0)
                {
                    
                    bool okEmpty = await SendDbMessageNoWaitAsync(DbMsg.DM_UPDATEITEMS, Array.Empty<byte>(), 0, flag, 0, ownerId);
                    Log(okEmpty
                        ? $"已发送 DM_UPDATEITEMS(空): ownerId={ownerId}, flag={flag}"
                        : $"发送 DM_UPDATEITEMS(空) 失败: ownerId={ownerId}, flag={flag}");
                    return;
                }

                byte[] payload = DatabaseSerializer.SerializeDbItems(items);
                ushort count = (ushort)Math.Min(items.Length, ushort.MaxValue);
                bool ok = await SendDbMessageNoWaitAsync(DbMsg.DM_UPDATEITEMS, payload, count, flag, 0, ownerId);
                Log(ok
                    ? $"已发送 DM_UPDATEITEMS: ownerId={ownerId}, flag={flag}, count={count}, bytes={payload.Length}"
                    : $"发送 DM_UPDATEITEMS 失败: ownerId={ownerId}, flag={flag}, count={count}");
            }
            catch (Exception ex)
            {
                Log($"发送 DM_UPDATEITEMS 异常: {ex.Message}");
            }
        }

        
        
        
        public async Task SendUpdateMagic(uint ownerId, MAGICDB[] magics)
        {
            if (!_connected)
            {
                Log("SendUpdateMagic跳过：未连接到DBServer");
                return;
            }

            try
            {
                if (magics == null || magics.Length == 0)
                {
                    bool okEmpty = await SendDbMessageNoWaitAsync(DbMsg.DM_UPDATEMAGIC, Array.Empty<byte>(), 0, 0, 0, ownerId);
                    Log(okEmpty
                        ? $"已发送 DM_UPDATEMAGIC(空): ownerId={ownerId}"
                        : $"发送 DM_UPDATEMAGIC(空) 失败: ownerId={ownerId}");
                    return;
                }

                byte[] payload = DatabaseSerializer.SerializeMagicDbs(magics);
                ushort count = (ushort)Math.Min(magics.Length, ushort.MaxValue);
                bool ok = await SendDbMessageNoWaitAsync(DbMsg.DM_UPDATEMAGIC, payload, 0, 0, count, ownerId);
                Log(ok
                    ? $"已发送 DM_UPDATEMAGIC: ownerId={ownerId}, count={count}, bytes={payload.Length}"
                    : $"发送 DM_UPDATEMAGIC 失败: ownerId={ownerId}, count={count}");
            }
            catch (Exception ex)
            {
                Log($"发送 DM_UPDATEMAGIC 异常: {ex.Message}");
            }
        }

        
        
        
        public async Task SendQueryUpgradeItem(uint serverId, uint clientKey, uint charId)
        {
            if (!_connected)
            {
                Log("SendQueryUpgradeItem跳过：未连接到DBServer");
                return;
            }

            try
            {
                
                byte[] payload = BitConverter.GetBytes(charId);
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = 0;

                bool ok = await SendDbMessageNoWaitAsync(DbMsg.DM_QUERYUPGRADEITEM, payload, wParam1, wParam2, wParam3, serverId);
                Log(ok
                    ? $"已发送 DM_QUERYUPGRADEITEM: serverId={serverId}, clientKey={clientKey}, ownerId={charId}"
                    : $"发送 DM_QUERYUPGRADEITEM 失败: serverId={serverId}, clientKey={clientKey}, ownerId={charId}");
            }
            catch (Exception ex)
            {
                Log($"发送 DM_QUERYUPGRADEITEM 异常: {ex.Message}");
            }
        }

        
        
        
        public async Task QueryTaskInfo(uint serverId, uint clientKey, uint charId)
        {
            if (!_connected)
            {
                Log("QueryTaskInfo跳过：未连接到DBServer");
                return;
            }

            try
            {
                
                byte[] payload = BitConverter.GetBytes(charId);
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = 0;

                bool ok = await SendDbMessageNoWaitAsync(DbMsg.DM_QUERYTASKINFO, payload, wParam1, wParam2, wParam3, serverId);
                Log(ok
                    ? $"已发送 DM_QUERYTASKINFO: serverId={serverId}, clientKey={clientKey}, ownerId={charId}"
                    : $"发送 DM_QUERYTASKINFO 失败: serverId={serverId}, clientKey={clientKey}, ownerId={charId}");
            }
            catch (Exception ex)
            {
                Log($"发送 DM_QUERYTASKINFO 异常: {ex.Message}");
            }
        }

        
        
        
        public async Task<byte[]?> GetCharDBInfoBytesAsync(string account, string serverName, string charName, uint clientKey = 0, uint charId = 0)
        {
            
            
            return await GetCharDBInfoBytesAsync2(account, serverName, charName, clientKey, charId);
        }

        
        
        
        public async Task<byte[]?> GetCharDBInfoBytesAsync2(string account, string serverName, string charName, uint clientKey = 0, uint charId = 0)
        {
            if (!_connected) return null;

            try
            {
                
                string data = $"{account}/{serverName}/{charName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);

                
                var tcs = new TaskCompletionSource<DbResponse?>();
                Action<MirMsg>? handler = null;

                handler = (msg) =>
                {
                    if (msg.wCmd == (ushort)DbMsg.DM_GETCHARDBINFO)
                    {
                        
                        
                        
                        Log($"立即收到 DM_GETCHARDBINFO 响应: 数据长度={msg.data?.Length ?? 0}字节");
                        if (msg.data != null && msg.data.Length >= 136) 
                        {
                            try
                            {
                                
                                var charDbInfo = BytesToStruct<CHARDBINFO>(msg.data);
                                uint receivedClientKey = charDbInfo.dwClientKey;

                                if (receivedClientKey == clientKey)
                                {
                                    
                                    OnDbMessageReceived -= handler;

                                    
                                    var response = new DbResponse
                                    {
                                        dwFlag = msg.dwFlag,
                                        wCmd = msg.wCmd,
                                        data = msg.data
                                    };
                                    tcs.SetResult(response);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"解析CHARDBINFO结构体失败2: {ex.Message}");
                            }
                        }
                    }
                };

                
                OnDbMessageReceived += handler;

                
                
                
                
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = (ushort)(charId & 0xFFFF);

                
                bool ok = await SendDbMessageNoWaitAsync(DbMsg.DM_GETCHARDBINFO, payload, wParam1, wParam2, wParam3, 0);
                if (!ok)
                {
                    Log($"发送 DM_GETCHARDBINFO 失败: 账号={account}, 服务器={serverName}, 角色名={charName}, clientKey={clientKey}, charId={charId}");
                    OnDbMessageReceived -= handler;
                    return null;
                }
                Log($"已发送 DM_GETCHARDBINFO 消息: 账号={account}, 服务器={serverName}, 角色名={charName}, clientKey={clientKey}, charId={charId}");

                
                var timeoutTask = Task.Delay(10000); 
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == tcs.Task)
                {
                    var response = await tcs.Task;
                    if (response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK && response.data != null)
                    {
                        Log($"收到 DM_GETCHARDBINFO 响应2: 数据长度={response.data.Length}字节");
                        return response.data;
                    }
                    else
                    {
                        Log($"DM_GETCHARDBINFO 响应失败2: dwFlag={response?.dwFlag ?? 0}");
                        return null;
                    }
                }
                else
                {
                    Log("等待 DM_GETCHARDBINFO 响应超时2");
                    
                    OnDbMessageReceived -= handler;
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"GetCharDBInfoBytesAsync失败2: {ex.Message}");
                return null;
            }
        }


        #endregion

        #region LoginServer调用方法

        
        
        
        public async Task<bool> CheckAccountAsync(string account, string password)
        {
            if (!_connected) return false;

            try
            {
                
                string data = $"{account}/{password}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_CHECKACCOUNT, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        public async Task<bool> CheckAccountExistAsync(string account)
        {
            if (!_connected) return false;

            try
            {
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(account);
                var response = await SendDbMessageAsync(DbMsg.DM_CHECKACCOUNTEXIST, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        public async Task<bool> CreateAccountAsync(string account, string password, string name, string birthday,
                                                  string q1, string a1, string q2, string a2, string email,
                                                  string phoneNumber, string mobilePhoneNumber, string idCard)
        {
            if (!_connected) return false;

            try
            {
                byte[] payload = BuildRegisterAccountStruct(account, password, name, birthday, q1, a1, q2, a2, email,
                                                           phoneNumber, mobilePhoneNumber, idCard);
                var response = await SendDbMessageAsync(DbMsg.DM_CREATEACCOUNT, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        public async Task<bool> ChangePasswordAsync(string account, string oldPassword, string newPassword)
        {
            if (!_connected) return false;

            try
            {
                
                string data = $"{account}/{oldPassword}/{newPassword}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_CHANGEPASSWORD, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        public async Task<bool> CheckCharacterNameExistsAsync(string serverName, string charName)
        {
            if (!_connected) return false;

            try
            {
                
                string data = $"{serverName}/{charName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_CHECKCHARACTERNAMEEXISTS, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        private byte[] BuildRegisterAccountStruct(string account, string password, string name, string birthday,
                                                  string q1, string a1, string q2, string a2, string email,
                                                  string phoneNumber, string mobilePhoneNumber, string idCard)
        {
            byte[] buffer = new byte[331]; 

            int offset = 0;

            
            buffer[offset++] = (byte)Math.Min(account.Length, 10);
            Encoding.GetEncoding("GBK").GetBytes(account, 0, Math.Min(account.Length, 10), buffer, offset);
            offset += 10;

            
            buffer[offset++] = (byte)Math.Min(password.Length, 10);
            Encoding.GetEncoding("GBK").GetBytes(password, 0, Math.Min(password.Length, 10), buffer, offset);
            offset += 10;

            
            buffer[offset++] = (byte)Math.Min(name.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(name, 0, Math.Min(name.Length, 20), buffer, offset);
            offset += 20;

            
            buffer[offset++] = (byte)Math.Min(idCard.Length, 19);
            Encoding.GetEncoding("GBK").GetBytes(idCard, 0, Math.Min(idCard.Length, 19), buffer, offset);
            offset += 19;

            
            buffer[offset++] = (byte)Math.Min(phoneNumber.Length, 14);
            Encoding.GetEncoding("GBK").GetBytes(phoneNumber, 0, Math.Min(phoneNumber.Length, 14), buffer, offset);
            offset += 14;

            
            buffer[offset++] = (byte)Math.Min(q1.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(q1, 0, Math.Min(q1.Length, 20), buffer, offset);
            offset += 20;

            
            buffer[offset++] = (byte)Math.Min(a1.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(a1, 0, Math.Min(a1.Length, 20), buffer, offset);
            offset += 20;

            
            buffer[offset++] = (byte)Math.Min(email.Length, 40);
            Encoding.GetEncoding("GBK").GetBytes(email, 0, Math.Min(email.Length, 40), buffer, offset);
            offset += 40;

            
            buffer[offset++] = (byte)Math.Min(q2.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(q2, 0, Math.Min(q2.Length, 20), buffer, offset);
            offset += 20;

            
            buffer[offset++] = (byte)Math.Min(a2.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(a2, 0, Math.Min(a2.Length, 20), buffer, offset);
            offset += 20;

            
            buffer[offset++] = (byte)Math.Min(birthday.Length, 10);
            Encoding.GetEncoding("GBK").GetBytes(birthday, 0, Math.Min(birthday.Length, 10), buffer, offset);
            offset += 10;

            
            buffer[offset++] = (byte)Math.Min(mobilePhoneNumber.Length, 11);
            Encoding.GetEncoding("GBK").GetBytes(mobilePhoneNumber, 0, Math.Min(mobilePhoneNumber.Length, 11), buffer, offset);
            offset += 11;

            
            for (int i = 0; i < 85; i++)
            {
                buffer[offset++] = 0;
            }

            return buffer;
        }

        #endregion

        #region SelectCharServer调用方法

        
        
        
        public async Task<MapPositionResult?> QueryMapPositionAsync(string account, string serverName, string charName)
        {
            if (!_connected) return null;

            try
            {
                
                string data = $"{account}/{serverName}/{charName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_GETCHARPOSITIONFORSELCHAR, payload);
                if (response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK && response.data != null)
                {
                    
                    
                    string positionData = Encoding.GetEncoding("GBK").GetString(response.data).TrimEnd('\0');
                    string[] parts = positionData.Split('/');
                    if (parts.Length >= 3)
                    {
                        return new MapPositionResult
                        {
                            MapName = parts[0],
                            X = short.Parse(parts[1]),
                            Y = short.Parse(parts[2])
                        };
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        
        
        
        public async Task<string?> QueryCharListAsync(string account, string serverName)
        {
            if (!_connected) return null;

            try
            {
                
                string data = $"{account}/{serverName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_QUERYCHARLIST, payload);
                if (response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK && response.data != null)
                {
                    return Encoding.GetEncoding("GBK").GetString(response.data).TrimEnd('\0');
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        
        
        
        public async Task<SERVER_ERROR> CreateCharacterAsync(string account, string serverName, string charName, byte prof, byte hair, byte sex)
        {
            if (!_connected) return SERVER_ERROR.SE_FAIL;

            try
            {
                
                string data = $"{account}/{serverName}/{charName}/{prof}/{hair}/{sex}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_CREATECHARACTER, payload);
                if (response != null)
                {
                    return (SERVER_ERROR)response.dwFlag;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }

        
        
        
        public async Task<bool> DeleteCharacterAsync(string account, string serverName, string charName)
        {
            if (!_connected) return false;

            try
            {
                
                string data = $"{account}/{serverName}/{charName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_DELETECHARACTER, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        public async Task<bool> RestoreCharacterAsync(string account, string serverName, string charName)
        {
            if (!_connected) return false;

            try
            {
                
                string data = $"{account}/{serverName}/{charName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_RESTORECHARACTER, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        
        
        
        private class DbResponse
        {
            public uint dwFlag { get; set; }
            public ushort wCmd { get; set; }
            public byte[]? data { get; set; }
        }

        
        
        
        public class MapPositionResult
        {
            public string MapName { get; set; } = string.Empty;
            public short X { get; set; }
            public short Y { get; set; }
        }

        
        
        
        public void Disconnect()
        {
            try
            {
                StopListening();
                _stream?.Close();
                _client?.Close();
                _connected = false;
                Log("已断开与DBServer的连接");
            }
            catch (Exception ex)
            {
                Log($"断开连接失败: {ex.Message}");
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

        
        
        
        private void Log(string message)
        {
            OnLogMessage?.Invoke($"[DBServerClient] {DateTime.Now:HH:mm:ss:fff} {message}");
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
