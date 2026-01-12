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
using static Mysqlx.Expect.Open.Types.Condition.Types;


using Player = GameServer.HumanPlayer;

namespace GameServer
{
    
    public enum ItemDataFlag
    {
        IDF_GROUND = 0,     
        IDF_BAG = 1,        
        IDF_EQUIPMENT = 2,  
        IDF_NPC = 3,        
        IDF_BANK = 4,       
        IDF_CACHE = 5,      
        IDF_PETBANK = 6,    
        IDF_UPGRADE = 7,    
    }

    
    
    
    public partial class GameClient
    {
        private readonly TcpClient _client;
        private readonly GameServerApp _server;
        private readonly GameWorld _world;
        private readonly NetworkStream _stream;
        private Player? _player;
        private readonly string _dbServerAddress;
        private readonly int _dbServerPort;

        
        private ClientState _state = ClientState.GSUM_NOTVERIFIED;
        private MirCommon.EnterGameServer _enterInfo = new MirCommon.EnterGameServer();
        private uint _clientKey = 0;
        private static int _nextClientKey = 0;

        
        private int _gmLevel = 0;
        private bool _scrollTextMode = false;
        private bool _noticeMode = false;
        private bool _competlyQuit = false;
        private readonly System.Diagnostics.Stopwatch _hlTimer = System.Diagnostics.Stopwatch.StartNew();

        
        private readonly List<byte> _verifyRawBuffer = new();

        
        private readonly List<byte> _codedRawBuffer = new();

        
        public const int EP_FIRSTLOGINPROCESS = 1;

        
        private bool _bagLoaded = false;
        private bool _equipmentLoaded = false;
        private bool _magicLoaded = false;
        private bool _taskInfoLoaded = false;
        private bool _upgradeItemLoaded = false;
        private bool _petBankLoaded = false;
        private bool _bankLoaded = false;

        
        private readonly List<ItemInstance> _bankCache = new();

        public GameClient(TcpClient client, GameServerApp server, GameWorld world, string dbServerAddress, int dbServerPort)
        {
            _client = client;
            _server = server;
            _world = world;
            _dbServerAddress = dbServerAddress;
            _dbServerPort = dbServerPort;
            _stream = client.GetStream();
            _clientKey = unchecked((uint)Interlocked.Increment(ref _nextClientKey));
        }

        
        
        
        public uint GetId()
        {
            return _clientKey;
        }

        
        
        
        
        public async Task ProcessAsync()
        {
            byte[] buffer = new byte[8192];
            var networkError = new NetworkError();

            while (_client.Connected)
            {
                try
                {
                    
                    if (_stream == null || !_stream.CanRead)
                    {
                        string remote = _client.Client?.RemoteEndPoint?.ToString() ?? "<unknown>";
                        string playerName = _player?.Name ?? "<unverified>";
                        LogManager.Default.Warning($"NetworkStream已被释放或不可读，退出处理循环: remote={remote}, player={playerName}, state={_state}");
                        break;
                    }

                    
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        
                        networkError.SetError(NetworkErrorCode.ME_SOCKETCLOSED, "连接正常关闭");
                        string remote = _client.Client?.RemoteEndPoint?.ToString() ?? "<unknown>";
                        string playerName = _player?.Name ?? "<unverified>";
                        LogManager.Default.Info($"客户端连接正常关闭: remote={remote}, player={playerName}, state={_state}, {networkError.GetFullErrorMessage()}");
                        break;
                    }

                    
                    var result = await ProcessMessageWithErrorHandling(buffer, bytesRead, networkError);
                    if (!result.IsSuccess)
                    {
                        LogManager.Default.Warning($"处理消息失败: {result.ErrorMessage}");
                        
                        if (result.ErrorCode == NetworkErrorCode.ME_SOCKETCLOSED ||
                            result.ErrorCode == NetworkErrorCode.ME_CONNECTIONRESET ||
                            result.ErrorCode == NetworkErrorCode.ME_CONNECTIONABORTED)
                        {
                            break;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    
                    string remote = _client.Client?.RemoteEndPoint?.ToString() ?? "<unknown>";
                    string playerName = _player?.Name ?? "<unverified>";
                    LogManager.Default.Info($"NetworkStream已被释放，退出处理循环: remote={remote}, player={playerName}, state={_state}");
                    break;
                }
                catch (SocketException ex)
                {
                    networkError.SetErrorFromSocketException(ex);
                    string remote = _client.Client?.RemoteEndPoint?.ToString() ?? "<unknown>";
                    string playerName = _player?.Name ?? "<unverified>";
                    LogManager.Default.Warning($"网络错误: remote={remote}, player={playerName}, state={_state}, {networkError.GetFullErrorMessage()}");

                    
                    if (ex.SocketErrorCode == SocketError.ConnectionReset ||
                        ex.SocketErrorCode == SocketError.ConnectionAborted ||
                        ex.SocketErrorCode == SocketError.Shutdown)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    networkError.SetErrorFromException(ex);
                    string remote = _client.Client?.RemoteEndPoint?.ToString() ?? "<unknown>";
                    string playerName = _player?.Name ?? "<unverified>";
                    LogManager.Default.Error($"处理客户端错误: remote={remote}, player={playerName}, state={_state}, {networkError.GetFullErrorMessage()}");
                    break;
                }
            }

            
            OnDisconnect();
        }

        
        
        
        
        
        
        
        private async Task<NetworkResult> ProcessMessageWithErrorHandling(byte[] data, int length, NetworkError networkError)
        {
            try
            {
                
                if (_state != ClientState.GSUM_VERIFIED)
                {
                    switch (_state)
                    {
                        case ClientState.GSUM_NOTVERIFIED:
                            {
                                
                                await HandleVerifyBytesAsync(data, length);
                                return NetworkResult.Success(length);
                            }
                        case ClientState.GSUM_WAITINGCONFIRM:
                            {
                                
                                bool decodeSuccess = GameMessageHandler.DecodeGameMessageOrign(data, length, out var msg, out var payload);
                                if (decodeSuccess)
                                {
                                    LogManager.Default.Info($"等待确认状态，收到消息: 0x{msg.wCmd:X4} (十进制: {msg.wCmd})");
                                    if (msg.wCmd == GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG)
                                    {
                                        LogManager.Default.Info($"收到CM_CONFIRMFIRSTDIALOG消息，开始处理确认第一个对话框");
                                        await HandleConfirmFirstDialog(msg, payload);
                                        return NetworkResult.Success(length);
                                    }
                                    else if (msg.wCmd == GameMessageHandler.ClientCommands.CM_PING)
                                    {
                                        
                                        await HandlePing(msg, payload);
                                        return NetworkResult.Success(length);
                                    }
                                    else
                                    {
                                        
                                        LogManager.Default.Info($"等待确认状态，忽略非确认消息: 0x{msg.wCmd:X4}");
                                        return NetworkResult.Success(length);
                                    }
                                }
                                else
                                {
                                    LogManager.Default.Warning("等待确认状态，解码消息失败，尝试检查是否为简单确认消息");

                                    
                                    
                                    if (length >= 2)
                                    {
                                        
                                        
                                        
                                        ushort possibleCmd = 0;
                                        if (length >= 2)
                                        {
                                            possibleCmd = BitConverter.ToUInt16(data, 0);
                                            
                                            if (possibleCmd != GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG && length >= 2)
                                            {
                                                
                                                possibleCmd = (ushort)((data[1] << 8) | data[0]);
                                            }
                                        }

                                        if (possibleCmd == GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG)
                                        {
                                            LogManager.Default.Info($"检测到简单确认消息: CM_CONFIRMFIRSTDIALOG (0x3fa)");
                                            
                                            var fakeMsg = new MirMsgOrign
                                            {
                                                dwFlag = 0,
                                                wCmd = GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG,
                                                wParam = new ushort[3] { 0, 0, 0 },
                                                
                                            };
                                            await HandleConfirmFirstDialog(fakeMsg, Array.Empty<byte>());
                                            return NetworkResult.Success(length);
                                        }
                                    }

                                    
                                    string messageStr = Encoding.GetEncoding("GBK").GetString(data, 0, length).TrimEnd('\0');
                                    LogManager.Default.Info($"原始消息内容: '{messageStr}' (长度: {messageStr.Length})");

                                    
                                    if (messageStr.Contains("confirm", StringComparison.OrdinalIgnoreCase) ||
                                        messageStr.Contains("ok", StringComparison.OrdinalIgnoreCase) ||
                                        messageStr.Contains("确认", StringComparison.OrdinalIgnoreCase))
                                    {
                                        LogManager.Default.Info($"检测到确认字符串消息，视为确认第一个对话框");
                                        var fakeMsg = new MirMsgOrign
                                        {
                                            dwFlag = 0,
                                            wCmd = GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG,
                                            wParam = new ushort[3] { 0, 0, 0 },
                                            
                                        };
                                        await HandleConfirmFirstDialog(fakeMsg, Array.Empty<byte>());
                                        return NetworkResult.Success(length);
                                    }

                                    
                                    LogManager.Default.Info($"尝试手动解码消息: 长度={length}");
                                    string hexData = BitConverter.ToString(data, 0, Math.Min(length, 32));
                                    LogManager.Default.Info($"消息十六进制: {hexData}");

                                    
                                    if (length >= 3 && data[0] == '#' && data[length - 1] == '!')
                                    {
                                        LogManager.Default.Info("检测到编码消息格式（#开头，!结尾），尝试手动解码");

                                        
                                        try
                                        {
                                            bool manualDecodeSuccess = GameMessageHandler.DecodeGameMessageOrign(data, length, out var manualMsg, out var manualPayload);
                                            if (manualDecodeSuccess)
                                            {
                                                LogManager.Default.Info($"手动解码成功: 命令=0x{manualMsg.wCmd:X4}");
                                                if (manualMsg.wCmd == GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG)
                                                {
                                                    LogManager.Default.Info($"收到CM_CONFIRMFIRSTDIALOG消息，开始处理确认第一个对话框");
                                                    await HandleConfirmFirstDialog(manualMsg, manualPayload);
                                                    return NetworkResult.Success(length);
                                                }
                                            }
                                            else
                                            {
                                                LogManager.Default.Warning("手动解码仍然失败");

                                                
                                                
                                                LogManager.Default.Info("解码失败，但消息格式正确，尝试直接处理为确认第一个对话框");
                                                var fakeMsg = new MirMsgOrign
                                                {
                                                    dwFlag = 0,
                                                    wCmd = GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG,
                                                    wParam = new ushort[3] { 0, 0, 0 },
                                                    
                                                };
                                                await HandleConfirmFirstDialog(fakeMsg, Array.Empty<byte>());
                                                return NetworkResult.Success(length);
                                            }
                                        }
                                        catch (Exception decodeEx)
                                        {
                                            LogManager.Default.Error($"手动解码异常: {decodeEx.Message}");

                                            
                                            LogManager.Default.Info("解码异常，但消息格式正确，尝试直接处理为确认第一个对话框");
                                            var fakeMsg = new MirMsgOrign
                                            {
                                                dwFlag = 0,
                                                wCmd = GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG,
                                                wParam = new ushort[3] { 0, 0, 0 },
                                                
                                            };
                                            await HandleConfirmFirstDialog(fakeMsg, Array.Empty<byte>());
                                            return NetworkResult.Success(length);
                                        }
                                    }

                                    LogManager.Default.Warning("等待确认状态，无法识别消息格式，忽略");
                                    return NetworkResult.Success(length);
                                }
                            }
                        case ClientState.GSUM_WAITINGDBINFO:
                            {
                                
                                bool decodeSuccess = GameMessageHandler.DecodeGameMessageOrign(data, length, out var msg, out var payload);
                                if (decodeSuccess && msg.wCmd == GameMessageHandler.ClientCommands.CM_PING)
                                {
                                    await HandlePing(msg, payload);
                                    return NetworkResult.Success(length);
                                }

                                
                                LogManager.Default.Debug("等待数据库信息状态，忽略消息");
                                return NetworkResult.Success(length);
                            }
                    }
                }

                
                
                await HandleVerifiedBytesAsync(data, length);
                return NetworkResult.Success(length);
            }
            catch (Exception ex)
            {
                networkError.SetErrorFromException(ex);
                return NetworkResult.FromException(ex);
            }
        }


        
        
        
        
        private async Task OnDBMsg(MirMsg pMsg, int datasize)
        {
            try
            {
                LogManager.Default.Info($"接收DB消息OnDBMsg: {(DbMsg)pMsg.wCmd}-{pMsg.wParam}-{pMsg.dwFlag}-{pMsg.data.Length}");

                
                if (pMsg.wCmd != (ushort)DbMsg.DM_GETCHARDBINFO && !IsMessageForMe(pMsg)) 
                {
                    LogManager.Default.Debug($"消息不属于本客户端，忽略: clientKey={_clientKey}, msgFlag={pMsg.dwFlag}");
                    return;
                }

                switch (pMsg.wCmd)
                {
                    case (ushort)DbMsg.DM_QUERYTASKINFO:
                        {
                            
                            uint key = (uint)((pMsg.wParam[1] << 16) | pMsg.wParam[0]);
                            ushort ret = pMsg.wParam[2];

                            if (key != _clientKey)
                            {
                                LogManager.Default.Warning($"DM_QUERYTASKINFO clientKey不匹配: 期望={_clientKey}, 收到={key}");
                                break;
                            }

                            if (_player == null)
                            {
                                LogManager.Default.Warning("DM_QUERYTASKINFO: 玩家对象为空，跳过解析");
                                _taskInfoLoaded = true;
                                CheckAllDataLoaded();
                                break;
                            }

                            if (ret == (ushort)SERVER_ERROR.SE_OK)
                            {
                                try
                                {
                                    var tasks = MirCommon.Database.DatabaseSerializer.DeserializeTaskInfos(pMsg.data);
                                    foreach (var t in tasks)
                                    {
                                        _player.OnTaskInfo(t);
                                    }
                                    LogManager.Default.Debug($"处理任务信息完成: 数量={tasks.Length}");
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Default.Error($"解析任务信息失败: {ex.Message}");
                                }
                            }
                            else
                            {
                                LogManager.Default.Warning($"DM_QUERYTASKINFO 返回码: {ret}");
                            }

                            _taskInfoLoaded = true;
                            CheckAllDataLoaded();
                        }
                        break;
                    case (ushort)DbMsg.DM_QUERYUPGRADEITEM:
                        {
                            
                            uint key = (uint)((pMsg.wParam[1] << 16) | pMsg.wParam[0]);
                            ushort count = pMsg.wParam[2];

                            if (key != _clientKey)
                            {
                                LogManager.Default.Warning($"DM_QUERYUPGRADEITEM clientKey不匹配: 期望={_clientKey}, 收到={key}");
                                break;
                            }

                            if (_player != null && count > 0 && pMsg.data != null && pMsg.data.Length > 0)
                            {
                                try
                                {
                                    var dbItem = BytesToStruct<MirCommon.Database.DBITEM>(pMsg.data);
                                    _player.SetUpgradeItem(dbItem.item);
                                    LogManager.Default.Debug($"设置升级物品: 物品ID={dbItem.item.dwMakeIndex}");
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Default.Error($"解析升级物品失败: {ex.Message}");
                                }
                            }

                            _upgradeItemLoaded = true;
                            LogManager.Default.Debug("升级物品数据加载完成");
                            CheckAllDataLoaded();
                        }
                        break;
                    case (ushort)DbMsg.DM_QUERYMAGIC:
                        {
                            
                            uint key = (uint)((pMsg.wParam[1] << 16) | pMsg.wParam[0]);
                            ushort countOrErr = pMsg.wParam[2];
                            bool magicOk = false;

                            if (key != _clientKey)
                            {
                                LogManager.Default.Warning($"DM_QUERYMAGIC clientKey不匹配: 期望={_clientKey}, 收到={key}");
                                break;
                            }

                            if ((countOrErr & 0x8000) != 0)
                            {
                                LogManager.Default.Warning($"DM_QUERYMAGIC 返回错误: 0x{countOrErr:X4}");
                                try { _player?.MarkMagicLoadedForSave(false); } catch { }
                                _magicLoaded = true;
                                CheckAllDataLoaded();
                                break;
                            }

                            if (_player != null)
                            {
                                try
                                {
                                    var magics = MirCommon.Database.DatabaseSerializer.DeserializeMagicDbs(pMsg.data);
                                    for (int i = 0; i < magics.Length; i++)
                                    {
                                        _player.SetMagic(magics[i], (byte)i);
                                    }
                                    _player.SendMagicList();
                                    LogManager.Default.Debug($"技能数量={magics.Length}");
                                    magicOk = true;
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Default.Error($"解析技能数据失败: {ex.Message}");
                                }
                            }

                            try { _player?.MarkMagicLoadedForSave(magicOk); } catch { }
                            _magicLoaded = true;
                            LogManager.Default.Debug("技能数据加载完成");
                            CheckAllDataLoaded();
                        }
                        break;
                    case (ushort)DbMsg.DM_QUERYITEMS:
                        {
                            
                            byte btFlag = (byte)pMsg.wParam[1];
                            int itemCount = pMsg.wParam[2];

                            void MarkContainerLoadedOnError()
                            {
                                
                                switch (btFlag)
                                {
                                    case (byte)ItemDataFlag.IDF_BAG:
                                        _bagLoaded = true;
                                        break;
                                    case (byte)ItemDataFlag.IDF_EQUIPMENT:
                                        _equipmentLoaded = true;
                                        break;
                                    case (byte)ItemDataFlag.IDF_BANK:
                                        _bankLoaded = true;
                                        break;
                                    case (byte)ItemDataFlag.IDF_PETBANK:
                                        _petBankLoaded = true;
                                        break;
                                }
                                CheckAllDataLoaded();
                            }

                            void FailCriticalContainer(string reason)
                            {
                                
                                if (btFlag == (byte)ItemDataFlag.IDF_BAG || btFlag == (byte)ItemDataFlag.IDF_EQUIPMENT)
                                {
                                    LogManager.Default.Error($"关键物品数据加载失败: btFlag={btFlag}, reason={reason}");
                                    SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "读取角色物品数据失败，请重新登录！");
                                    Disconnect(2000);
                                    return;
                                }

                                MarkContainerLoadedOnError();
                            }

                            if (pMsg.dwFlag != (uint)SERVER_ERROR.SE_OK)
                            {
                                LogManager.Default.Error($"DM_QUERYITEMS返回错误: {pMsg.dwFlag}");
                                FailCriticalContainer($"ret={pMsg.dwFlag}");
                                break;
                            }

                            if (pMsg.data == null || pMsg.data.Length < 4)
                            {
                                LogManager.Default.Error($"DM_QUERYITEMS数据长度不足: {pMsg.data?.Length ?? 0}字节");
                                FailCriticalContainer("data<4");
                                break;
                            }

                            uint keyInData = BitConverter.ToUInt32(pMsg.data, 0);
                            if (keyInData != _clientKey)
                            {
                                LogManager.Default.Warning($"DM_QUERYITEMS key不匹配: 期望={_clientKey}, 收到={keyInData}");
                                FailCriticalContainer($"keyMismatch expected={_clientKey} got={keyInData}");
                                break;
                            }

                            if (itemCount == 0)
                            {
                                LogManager.Default.Info($"DM_QUERYITEMS: 没有物品数据，btFlag={btFlag}");
                                var emptyItems = Array.Empty<MirCommon.Database.DBITEM>();
                                OnDBItem(emptyItems, 0, btFlag);
                                break;
                            }

                            int dbitemSize = System.Runtime.InteropServices.Marshal.SizeOf<MirCommon.Database.DBITEM>();
                            int expectedSize = 4 + itemCount * dbitemSize;

                            if (datasize < expectedSize)
                            {
                                LogManager.Default.Error($"DM_QUERYITEMS数据大小不足: 期望{expectedSize}字节, 实际{datasize}字节");
                                FailCriticalContainer($"sizeMismatch expected={expectedSize} got={datasize}");
                                break;
                            }

                            var dbItems = new MirCommon.Database.DBITEM[itemCount];
                            int baseOffset = 4;
                            for (int i = 0; i < itemCount; i++)
                            {
                                dbItems[i] = BytesToStruct<MirCommon.Database.DBITEM>(pMsg.data, baseOffset + i * dbitemSize, dbitemSize);
                            }

                            OnDBItem(dbItems, itemCount, btFlag);
                        }
                        break;
                    case (ushort)DbMsg.DM_GETCHARDBINFO:
                        {
                            
                            
                            
                            
                            if (pMsg.dwFlag != (ushort)SERVER_ERROR.SE_OK)
                            {
                                LogManager.Default.Error($"DM_GETCHARDBINFO返回失败: wParam[0]={pMsg.wParam[0]}, 数据长度={pMsg.data?.Length ?? 0}字节");
                                SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "读取数据库失败，请联系管理员解决！");
                                
                                break;
                            }

                            
                            if (pMsg.data == null || pMsg.data.Length < 136) 
                            {
                                LogManager.Default.Error($"DM_GETCHARDBINFO数据长度不足: 期望至少136字节, 实际={pMsg.data?.Length ?? 0}字节");
                                SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "角色数据格式错误！");
                                break;
                            }

                            
                            var charDbInfo = BytesToStruct<MirCommon.Database.CHARDBINFO>(pMsg.data);
                            if (charDbInfo.dwClientKey != _clientKey)
                            {
                                LogManager.Default.Warning($"DM_GETCHARDBINFO clientKey不匹配: 期望={_clientKey}, 收到={charDbInfo.dwClientKey}");
                                break;
                            }

                            
                            var createDesc = new CREATEHUMANDESC
                            {
                                dbinfo = charDbInfo,
                                pClientObj = IntPtr.Zero  
                            };

                            
                            string account = _enterInfo.GetAccount();
                            string playerName = _enterInfo.GetName();

                            if (string.IsNullOrEmpty(account))
                            {
                                account = "default_account";
                                LogManager.Default.Warning($"账号为空，使用默认账号: {account}");
                            }

                            if (string.IsNullOrEmpty(playerName))
                            {
                                playerName = "default_player";
                                LogManager.Default.Warning($"角色名为空，使用默认角色名: {playerName}");
                            }

                            
                            if (FindPlayerByName(playerName) != null)
                            {
                                LogManager.Default.Error($"角色已登录1: {playerName}");
                                SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "您登陆的角色已经登陆该服务器！");
                                
                                break;
                            }

                            
                            
                            
                            uint charId = charDbInfo.dwDBId;
                            
                            _player = HumanPlayerMgr.Instance.NewPlayer(account, playerName, charId, _client);
                            if (_player == null)
                            {
                                LogManager.Default.Error($"创建玩家对象失败: 账号={account}, 角色名={playerName}, 角色ID={charId}");
                                
                                break;
                            }

                            
                            _player.SetSendMessageDelegate((uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, byte[]? payload) =>
                            {
                                try
                                {
                                    
                                    SendMsg2(dwFlag, wCmd, w1, w2, w3, payload);
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Default.Error($"通过委托发送消息失败: {ex.Message}");
                                }
                            });

                            
                            if (!_player.Init(createDesc))
                            {
                                LogManager.Default.Error($"初始化玩家失败: {playerName}");
                                HumanPlayerMgr.Instance.DeletePlayer(_player);
                                _player = null;
                                SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "初始化失败！");
                                
                                break;
                            }

                            
                            _player.LoadVars();

                            
                            SendFirstDlg(GameWorld.Instance.GetNotice());

                            
                            _state = ClientState.GSUM_WAITINGCONFIRM;
                            LogManager.Default.Info($"已设置状态为GSUM_WAITINGCONFIRM，等待玩家确认第一个对话框: {playerName}");

                            
                            
                            
                            
                            

                            
                            
                            
                            
                            
                        }
                        break;
                    case (ushort)DbMsg.DM_CREATEITEM:
                        {
                            
                            var createItem = BytesToStruct<MirCommon.CREATEITEM>(pMsg.data);
                            if (createItem.dwClientKey != _clientKey)
                                break;

                            OnCreateItem(createItem.item, createItem.wPos, createItem.btFlag);
                        }
                        break;
                    case (ushort)DbMsg.DM_QUERYCOMMUNITY:
                        {
                            uint dwKey = (uint)(pMsg.wParam[0] | (pMsg.wParam[1] << 16));
                            if (dwKey == _clientKey && _player != null)
                            {
                                
                                try
                                {
                                    _player.OnCommunityInfo(pMsg.data ?? Array.Empty<byte>());
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Default.Error($"处理社区信息失败: {ex.Message}");
                                }
                            }
                        }
                        break;
                    default:
                        
                        LogManager.Default.Debug($"未知数据库消息: 0x{pMsg.wCmd:X4}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理数据库消息失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        
        
        
        
        
        
        private async Task HandleGameMessage(MirMsgOrign msg, byte[] payload)
        {
            try
            {
                
                LogManager.Default.Debug($"处理客户端消息: 0x{msg.wCmd:X4} (十进制: {msg.wCmd}), Flag: 0x{msg.dwFlag:X8}");

                
                if (_player != null && _state == ClientState.GSUM_VERIFIED && _player.IsDead)
                {
                    bool allowed = msg.wCmd == GameMessageHandler.ClientCommands.CM_RESTARTGAME
                                   || msg.wCmd == GameMessageHandler.ClientCommands.CM_SAY
                                   || msg.wCmd == GameMessageHandler.ClientCommands.CM_SETBAGITEMPOS
                                   || msg.wCmd == GameMessageHandler.ClientCommands.CM_COMPLETELYQUIT;

                    if (!allowed)
                    {
                        _player.SaySystem("你已经死亡");
                        return;
                    }
                }

                
                
                
                
                
                
                
                
                

                
                bool handled = false;

                
                switch (msg.wCmd)
                {
                    case GameMessageHandler.ClientCommands.CM_PUTITEMTOPETBAG: 
                        await HandlePutItemToPetBag(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_GETITEMFROMPETBAG: 
                        await HandleGetItemFromPetBag(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_DELETETASK: 
                        await HandleDeleteTask(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_GMCOMMAND: 
                        await HandleGMTestCommand(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_COMPLETELYQUIT: 
                        await HandleCompletelyQuit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_CUTBODY: 
                        await HandleCutBody(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_PUTITEM: 
                        await HandlePutItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SHOWPETINFO: 
                        await HandleShowPetInfo(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYTIME: 
                        await HandleQueryTime(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_MARKET: 
                        await HandleMarketMessage(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_MINE: 
                        await HandleMine(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_DELETEFRIEND: 
                        await HandleDeleteFriend(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_REPLYADDFRIEND: 
                        await HandleReplyAddFriendRequest(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_ADDFRIEND: 
                        await HandleAddFriend(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_CREATEGUILD: 
                        await HandleCreateGuildOrInputConfirm(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_RIDEHORSE: 
                        await HandleRideHorse(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_REPLYADDTOGUILD: 
                        await HandleReplyAddToGuildRequest(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_INVITETOGUILD: 
                        await HandleInviteToGuild(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_ZUOYI: 
                        await HandleZuoyi(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_TAKEBANKITEM: 
                        await HandleTakeBankItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_PUTBANKITEM: 
                        await HandlePutBankItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYCOMMUNITY: 
                        await HandleQueryCommunity(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_REMOVEGUILDMEMBER: 
                        await HandleDeleteGuildMember(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_EDITGUILDNOTICE: 
                        await HandleEditGuildNotice(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_EDITGUILDTITLE: 
                        await HandleEditGuildTitle(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYGUILDEXP: 
                        await HandleQueryGuildExp(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYGUILDINFO: 
                        await HandleQueryGuildInfo(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYGUILDMEMBERLIST: 
                        await HandleQueryGuildMemberList(msg, payload);
                        handled = true;
                        break;
                    
                    
                    
                    
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_POJISHIELD: 
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_HALFMOON: 
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_FIRE: 
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_ASSASSINATE: 
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_KILL: 
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYHISTORYADDR: 
                        await HandleQueryHistoryAddress(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SETMAGICKEY: 
                        await HandleSetMagicKey(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_USEITEM: 
                        await HandleUseItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYMINIMAP: 
                        await HandleQueryMinimap(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_BUYITEM: 
                        await HandleBuyItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SELLITEM: 
                        await HandleSellItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYITEMSELLPRICE: 
                        await HandleQueryItemSellPrice(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYITEMLIST: 
                        await HandleQueryItemList(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SETBAGITEMPOS: 
                        await HandleSetBagItemPos(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_REPAIRITEM: 
                        await HandleRepairItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYREPAIRPRICE: 
                        await HandleQueryRepairPrice(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SELECTLINK: 
                        await HandleSelectLink(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_NPCTALK: 
                        await HandleNPCTalkOrViewPrivateShop(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_RESTARTGAME: 
                        await HandleRestartGame(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_VIEWEQUIPMENT: 
                        await HandleViewEquipment(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_PING: 
                        await HandlePing(msg, payload);
                        handled = true;
                        break;
                    case 0x3d4: 
                        await HandlePingResponse(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_TRAINHORSE: 
                        await HandleTrainHorse(msg, payload);
                        handled = true;
                        break;
                    default:
                        
                        handled = false;
                        break;
                }

                if (!handled)
                {
                    
                    switch (msg.wCmd)
                    {
                        
                        
                        
                        case GameMessageHandler.ClientCommands.CM_WALK:
                            await HandleWalkMessage(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_RUN:
                            await HandleRunMessage(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_SAY:
                            await HandleSayMessage(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_TURN:
                            await HandleTurnMessage(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_ATTACK:
                            await HandleAttackMessage(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_GETMEAL:
                            await HandleGetMealMessage(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_STOP:
                            await HandleStopMessage(msg, payload);
                            break;
                        
                        
                        
                        case GameMessageHandler.ClientCommands.CM_SELECTLINK:
                            await HandleSelectLink(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_TAKEONITEM:
                            await HandleTakeOnItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_TAKEOFFITEM:
                            await HandleTakeOffItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_DROPITEM:
                            await HandleDropItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_PICKUPITEM:
                            await HandlePickupItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_SPELLSKILL:
                            await HandleSpellSkill(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_QUERYTRADE:
                            await HandleQueryTrade(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_PUTTRADEITEM:
                            await HandlePutTradeItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_PUTTRADEGOLD:
                            await HandlePutTradeGold(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_QUERYTRADEEND:
                            await HandleQueryTradeEnd(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_CANCELTRADE:
                            await HandleCancelTrade(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_CHANGEGROUPMODE:
                            await HandleChangeGroupMode(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_QUERYADDGROUPMEMBER:
                            await HandleQueryAddGroupMember(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_DELETEGROUPMEMBER:
                            await HandleDeleteGroupMember(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_QUERYSTARTPRIVATESHOP:
                            await HandleQueryStartPrivateShop(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_ZUOYI:
                            await HandleZuoyi(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_PING:
                            await HandlePing(msg, payload);
                            break;
                        case 0x3d4: 
                            await HandlePingResponse(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_QUERYTIME:
                            await HandleQueryTime(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_RIDEHORSE:
                            await HandleRideHorse(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_USEITEM:
                            await HandleUseItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_DROPGOLD:
                            await HandleDropGold(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_NPCTALK:
                            await HandleNPCTalk(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_BUYITEM:
                            await HandleBuyItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_SELLITEM:
                            await HandleSellItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_REPAIRITEM:
                            await HandleRepairItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_QUERYREPAIRPRICE:
                            await HandleQueryRepairPrice(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_QUERYMINIMAP:
                            await HandleQueryMinimap(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_VIEWEQUIPMENT:
                            await HandleViewEquipment(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_MINE:
                            await HandleMine(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_TRAINHORSE:
                            await HandleTrainHorse(msg, payload);
                            handled = true;
                            break;
                        case GameMessageHandler.ClientCommands.CM_SPECIALHIT_KILL:
                        case GameMessageHandler.ClientCommands.CM_SPECIALHIT_ASSASSINATE:
                        case GameMessageHandler.ClientCommands.CM_SPECIALHIT_HALFMOON:
                        case GameMessageHandler.ClientCommands.CM_SPECIALHIT_FIRE:
                        case GameMessageHandler.ClientCommands.CM_SPECIALHIT_POJISHIELD:
                            await HandleSpecialHit(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_LEAVESERVER:
                            await HandleLeaveServer(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.SM_UNKNOWN_COMMAND:
                            await HandleUnknown45(msg, payload);
                            break;
                        default:
                            Console.WriteLine($"未处理的消息命令: 0x{msg.wCmd:X4}");
                            
                            LogManager.Default.Info($"未知消息: 0x{msg.wCmd:X4} (十进制: {msg.wCmd})");
                            
                            SendUnknownCommandResponse(msg.wCmd);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理游戏消息失败: {ex.Message}");
                
                SendErrorMessage($"处理命令 0x{msg.wCmd:X4} 失败: {ex.Message}");
            }
        }

        private void SendEnterGameOk()
        {
            if (_player == null) return;

            
            
            var payload = new PacketBuilder();
            payload.WriteInt32(_player.MapId);
            payload.WriteInt32(_player.X);
            payload.WriteInt32(_player.Y);
            SendMsg2(_player.ObjectId, ProtocolCmd.SM_ENTERGAMEOK, 0, 0, 0, payload.Build());
        }

        
        
        
        private void TrySendEnterGameOk()
        {
            if (_player == null)
                return;

            if (_player.GetSystemFlag((int)MirCommon.SystemFlag.SF_ENTERGAMESENT))
                return;

            
            if (!_player.GetSystemFlag((int)MirCommon.SystemFlag.SF_BAGLOADED) ||
                !_player.GetSystemFlag((int)MirCommon.SystemFlag.SF_EQUIPMENTLOADED))
            {
                return;
            }

            SendEnterGameOk();
            _player.SetSystemFlag((int)MirCommon.SystemFlag.SF_ENTERGAMESENT, true);
        }

        
        private void SendActionResult(int x, int y, bool success)
        {
            if (_player == null) return;

            
            
            string message = success ? $"#+G/{x}/{y}!" : $"#+FL/{x}/{y}!";
            byte[] textBytes = Encoding.GetEncoding("GBK").GetBytes(message);
            
            lock (_stream)
            {
                _stream.Write(textBytes, 0, textBytes.Length);
                _stream.Flush();
            }

            
        }


        private void SendStopMessage()
        {
            if (_player == null) return;

            GameMessageHandler.SendSimpleMessage2(_stream, _player.ObjectId,
                GameMessageHandler.ServerCommands.SM_STOP, 0, 0, 0);
        }

        
        
        
        private void SendFirstDlg(string message)
        {
            try
            {
                
                
                
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(message);
                GameMessageHandler.SendSimpleMessage2(_stream, 0, GameMessageHandler.ServerCommands.SM_FIRSTDIALOG, 0, 0, 0, payload);
                LogManager.Default.Info($"已发送第一个对话框: {message}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送第一个对话框失败: {ex.Message}");
            }

        }

        private void SendEquipItemResult(bool success, int pos, uint dwMakeIndex)
        {
            if (_player == null) return;

            
            ushort cmd = success ? GameMessageHandler.ServerCommands.SM_TAKEON_OK :
                GameMessageHandler.ServerCommands.SM_TAKEON_FAIL;

            uint dwFlag = success ? _player.GetFeather() : 0xffffffffu;
            GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, cmd, 0, 0, 0);

            if (!success) return;

            _player.SendFeatureChanged();
            _player.UpdateProp();
            _player.UpdateSubProp();
            _player.SendStatusChanged();

            try
            {
                var equip = _player.Equipment.GetItem((EquipSlot)pos);
                if (equip != null)
                    _player.SendDuraChanged(pos, equip.Durability, equip.MaxDurability);
                else
                    _player.SendDuraChanged(pos, 0, 0);
            }
            catch { }
        }

        private void SendUnEquipItemResult(bool success, int pos, uint dwMakeIndex)
        {
            if (_player == null) return;

            
            ushort cmd = success ? GameMessageHandler.ServerCommands.SM_TAKEOFF_OK :
                GameMessageHandler.ServerCommands.SM_TAKEOFF_FAIL;

            uint dwFlag = success ? _player.GetFeather() : 0xffffffffu;
            GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, cmd, 0, 0, 0);

            if (!success) return;

            
            GameMessageHandler.SendSimpleMessage2(_stream, 0, 0x26c, 0, 0, 0);

            _player.SendFeatureChanged();
            _player.UpdateProp();
            _player.UpdateSubProp();
            _player.SendStatusChanged();

            try
            {
                _player.SendDuraChanged(pos, 0, 0);
            }
            catch { }
        }

        private void SendDropItemResult(bool success, uint itemId)
        {
            ushort cmd = success ? GameMessageHandler.ServerCommands.SM_DROPITEMOK : 
                GameMessageHandler.ServerCommands.SM_DROPITEMFAIL;
            GameMessageHandler.SendSimpleMessage2(_stream, itemId, cmd, 0, 0, 0);
        }

        private void SendPickupItemResult(bool success)
        {
            
            if (success)
            {
                _player?.SendWeightChanged();
            }
        }

        private void SendChatMessage(Player targetPlayer, string speaker, string message)
        {
            if (_player == null) return;

            
            
            try
            {
                
                string text = $"{speaker}: {message}";
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(text);
                if (payload.Length > 120)
                {
                    Array.Resize(ref payload, 120);
                }

                
                targetPlayer.SendMsg(_player.ObjectId,
                    MirCommon.ProtocolCmd.SM_CHAT, 0xff00, 0, 0, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送聊天消息失败: target={targetPlayer?.Name} - {ex.Message}");
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

        
        
        
        public void OnDisconnect()
        {
            try
            {
                if (_player != null)
                {
                    var player = _player;
                    
                    _player = null;
                    LogManager.Default.Info($"玩家断开连接: {player.Name}");

                    try
                    {
                        HumanPlayerMgr.Instance.DeletePlayer(player);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"OnDisconnect提前从HumanPlayerMgr移除失败: {player.Name} - {ex.Message}");
                    }

                    
                    if (!_competlyQuit && _state == ClientState.GSUM_VERIFIED)
                    {
                        try
                        {
                            var sc = _server.GetServerCenterClient();
                            if (sc != null)
                            {
                                byte[] enterBytes = StructToBytes(_enterInfo);
                                ushort targetIndex = (ushort)_enterInfo.dwSelectCharServerId;
                                byte sendType = (byte)MirCommon.ProtocolCmd.MST_SINGLE;

                                bool sent = sc.SendMsgAcrossServerAsync(
                                    clientId: 0,
                                    cmd: MirCommon.ProtocolCmd.MAS_RESTARTGAME,
                                    sendType: sendType,
                                    targetIndex: targetIndex,
                                    binaryData: enterBytes
                                ).GetAwaiter().GetResult();

                                if (sent)
                                {
                                    LogManager.Default.Info($"OnDisconnect已发送MAS_RESTARTGAME: targetIndex={targetIndex}");
                                }
                                else
                                {
                                    LogManager.Default.Warning($"OnDisconnect发送MAS_RESTARTGAME失败: targetIndex={targetIndex}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Default.Warning($"OnDisconnect发送MAS_RESTARTGAME异常: {ex.Message}");
                        }
                    }

                    
                    try
                    {
                        
                        
                        if (player is ScriptTarget scriptTarget)
                        {
                            SystemScript.Instance.Execute(scriptTarget, "Logout");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"执行Logout脚本失败: {player.Name} - {ex.Message}");
                    }

                    
                    try
                    {
                        
                        
                        player.Guild = null;
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"行会下线处理失败: {player.Name} - {ex.Message}");
                    }

                    
                    try
                    {
                        var trade = player.CurrentTrade ?? TradeManager.Instance.GetPlayerTrade(player);
                        trade?.End(player, TradeEndType.Cancel);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"交易取消处理失败: {player.Name} - {ex.Message}");
                    }

                    
                    try
                    {
                        var group = GroupObjectManager.Instance.GetPlayerGroup(player);
                        group?.DelMember(player);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"组队离队处理失败: {player.Name} - {ex.Message}");
                    }

                    
                    try
                    {
                        player.PetSystem?.CleanPets();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"清理宠物失败: {player.Name} - {ex.Message}");
                    }

                    
                    try
                    {
                        var dbClient = _server.GetDbServerClient();
                        player.UpdateToDB(dbClient);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"保存玩家数据失败: {player.Name} - {ex.Message}");
                    }

                    
                    try
                    {
                        _world.RemovePlayer(player.ObjectId);
                        var map = _world.GetMap(player.MapId);
                        map?.RemovePlayer(player.ObjectId);
                        player.CurrentMap?.RemoveObject(player);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"从地图移除失败: {player.Name} - {ex.Message}");
                    }
                }

                Disconnect();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"断开连接处理失败: {ex.Message}");
            }
        }

        
        
        
        private void SavePlayerDataToDB()
        {
            if (_player == null) return;

            try
            {
                
                
                LogManager.Default.Info($"保存玩家数据: {_player.Name}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存玩家数据失败: {ex.Message}");
            }
        }

        
        
        
        private void CleanupPlayerResources()
        {
            if (_player == null) return;

            try
            {
                
                
                LogManager.Default.Info($"清理玩家资源: {_player.Name}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"清理玩家资源失败: {ex.Message}");
            }
        }

        
        
        
        private void SendUnknownCommandResponse(ushort command)
        {
            try
            {
                
                GameMessageHandler.SendSimpleMessage2(_stream, 0,
                    GameMessageHandler.ServerCommands.SM_UNKNOWN_COMMAND, 0, 0, 0);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送未知命令响应失败: {ex.Message}");
            }
        }

        
        
        
        private void SendErrorMessage(string message)
        {
            try
            {
                
                byte[] payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(message);
                GameMessageHandler.SendSimpleMessage2(_stream, 0,
                    GameMessageHandler.ServerCommands.SM_ERROR, 0, 0, 0, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送错误消息失败: {ex.Message}");
            }
        }

        #region 处理客户端验证

        
        
        
        private async Task OnVerifyString(string verifyString)
        {
            try
            {
                LogManager.Default.Info($"处理验证字符串: {verifyString}");

                
                if (verifyString.StartsWith("#") && verifyString.EndsWith("!"))
                {
                    
                    string decodedString = DecodeVerifyString(verifyString);
                    if (string.IsNullOrEmpty(decodedString))
                    {
                        LogManager.Default.Warning($"解码验证字符串失败: {verifyString}");
                        
                        return;
                    }

                    LogManager.Default.Info($"解码后的验证字符串: {decodedString}");

                    
                    await ProcessDecodedString(decodedString);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理验证字符串失败: {ex.Message}");
                
            }
        }

        private sealed record VerifyInfo(uint LoginId, string CharName, uint SelCharId, string Version, bool IsNewVersion);

        private static bool TryParseVerifyInfo(string decodedString, out VerifyInfo info)
        {
            info = new VerifyInfo(0, string.Empty, 0, string.Empty, false);
            if (string.IsNullOrWhiteSpace(decodedString))
                return false;

            string p = decodedString.TrimEnd('\0').Trim();
            bool isNew = p.StartsWith("***", StringComparison.Ordinal);
            if (isNew)
                p = p.Substring(3);

            var parts = p.Split('/', StringSplitOptions.None);

            if (isNew)
            {
                
                if (parts.Length != 5)
                    return false;

                if (!uint.TryParse(parts[0], out uint loginId))
                    return false;

                string charName = parts[1];
                if (!uint.TryParse(parts[2], out uint selCharId))
                    return false;

                string version = parts[3];

                
                if (!int.TryParse(parts[4], out _))
                    return false;

                info = new VerifyInfo(loginId, charName, selCharId, version, true);
                return true;
            }

            
            if (parts.Length == 3 &&
                uint.TryParse(parts[0], out uint oldLoginId) &&
                uint.TryParse(parts[2], out uint oldSelCharId))
            {
                info = new VerifyInfo(oldLoginId, parts[1], oldSelCharId, string.Empty, false);
                return true;
            }

            return false;
        }

        private async Task HandleVerifyBytesAsync(byte[] data, int length)
        {
            if (length <= 0)
                return;

            
            if (_verifyRawBuffer.Count > 8192)
            {
                LogManager.Default.Warning($"验证缓冲区过大({_verifyRawBuffer.Count})，清空并重新同步");
                _verifyRawBuffer.Clear();
            }

            _verifyRawBuffer.AddRange(data.Take(length));

            var candidates = new List<(string VerifyString, string Decoded, VerifyInfo Info)>();

            while (true)
            {
                int endIndex = _verifyRawBuffer.IndexOf((byte)'!');
                if (endIndex < 0)
                    break;

                
                byte[] frame = _verifyRawBuffer.Take(endIndex + 1).ToArray();
                _verifyRawBuffer.RemoveRange(0, endIndex + 1);

                int startIndex = Array.IndexOf(frame, (byte)'#');
                if (startIndex < 0)
                    continue;

                string verifyString = Encoding.GetEncoding("GBK").GetString(frame, startIndex, frame.Length - startIndex).TrimEnd('\0').Trim();
                if (!verifyString.StartsWith("#", StringComparison.Ordinal) || !verifyString.EndsWith("!", StringComparison.Ordinal))
                    continue;

                string decoded = DecodeVerifyString(verifyString);
                if (string.IsNullOrEmpty(decoded))
                {
                    LogManager.Default.Warning($"解码验证字符串失败: {verifyString}");
                    continue;
                }

                LogManager.Default.Info($"解码后的验证字符串: {decoded}");

                if (TryParseVerifyInfo(decoded, out var info))
                {
                    candidates.Add((verifyString, decoded, info));
                }
            }

            if (candidates.Count == 0)
                return;

            
            var chosen = candidates.LastOrDefault(c => _server.GetEnterInfo(c.Info.LoginId) != null);
            if (chosen.Info != null && chosen.Info.LoginId != 0)
            {
                await ProcessDecodedString(chosen.Decoded);
                _verifyRawBuffer.Clear();
                return;
            }

            
            await ProcessDecodedString(candidates[^1].Decoded);
            _verifyRawBuffer.Clear();
        }

        private async Task HandleVerifiedBytesAsync(byte[] data, int length)
        {
            if (length <= 0)
                return;

            
            if (_codedRawBuffer.Count > 65536)
            {
                LogManager.Default.Warning($"消息缓冲区过大({_codedRawBuffer.Count})，清空并重新同步");
                _codedRawBuffer.Clear();
            }

            _codedRawBuffer.AddRange(data.Take(length));

            while (true)
            {
                int startIndex = _codedRawBuffer.IndexOf((byte)'#');
                if (startIndex < 0)
                {
                    
                    _codedRawBuffer.Clear();
                    break;
                }

                
                if (startIndex > 0)
                    _codedRawBuffer.RemoveRange(0, startIndex);

                int endIndex = _codedRawBuffer.IndexOf((byte)'!');
                if (endIndex < 0)
                    break; 

                byte[] frame = _codedRawBuffer.Take(endIndex + 1).ToArray();
                _codedRawBuffer.RemoveRange(0, endIndex + 1);

                if (!GameMessageHandler.DecodeGameMessageOrign(frame, frame.Length, out var msg, out var payload))
                {
                    LogManager.Default.Warning($"已验证状态解码失败，丢弃帧: len={frame.Length}");
                    continue;
                }

                await HandleGameMessage(msg, payload);
            }
        }

        
        
        
        private string DecodeVerifyString(string encodedString)
        {
            try
            {
                
                
                
                
                
                

                if (string.IsNullOrEmpty(encodedString) || encodedString.Length < 3)
                {
                    LogManager.Default.Warning($"编码字符串太短: {encodedString}");
                    return string.Empty;
                }

                
                string encodedPart = encodedString.Substring(1, encodedString.Length - 2);

                
                
                if (encodedPart.Length > 0 && encodedPart[0] >= '0' && encodedPart[0] <= '9')
                {
                    
                    encodedPart = encodedPart.Substring(1);
                    LogManager.Default.Debug($"跳过开头的数字字符，剩余编码部分: {encodedPart}");
                }

                
                byte[] encodedBytes = Encoding.GetEncoding("GBK").GetBytes(encodedPart);

                
                byte[] decodedBytes = new byte[encodedBytes.Length * 2]; 
                int decodedSize = MirCommon.GameCodecOrign.UnGameCodeOrign(encodedBytes, decodedBytes);

                if (decodedSize <= 0)
                {
                    LogManager.Default.Warning($"解码失败: 解码大小为{decodedSize}");
                    return string.Empty;
                }

                
                string decodedString = Encoding.GetEncoding("GBK").GetString(decodedBytes, 0, decodedSize).TrimEnd('\0');

                LogManager.Default.Debug($"解码成功: 原始='{encodedString}', 解码后='{decodedString}'");
                return decodedString;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解码验证字符串失败: {ex.Message}");
                return string.Empty;
            }
        }

        
        
        
        private async Task ProcessDecodedString(string decodedString)
        {
            try
            {
                LogManager.Default.Debug($"处理解码后的字符串: '{decodedString}'");

                string p = decodedString;
                bool isNewVersion = false;

                if (p.Length >= 3 && p[0] == '*' && p[1] == '*' && p[2] == '*')
                {
                    p = p.Substring(3);
                    isNewVersion = true;
                    LogManager.Default.Debug($"检测到以***开头");
                }

                
                string[] paramsArray = p.Split('/');

                uint loginId = 0;
                string charName = "";
                uint selCharId = 0;
                string version = "";

                if (isNewVersion && paramsArray.Length == 5)
                {
                    
                    loginId = uint.Parse(paramsArray[0]);
                    charName = paramsArray[1];
                    selCharId = uint.Parse(paramsArray[2]);
                    version = paramsArray[3];
                    

                    LogManager.Default.Info($"验证字符串解析成功: loginId={loginId}, charName={charName}, selCharId={selCharId}, version={version}");
                }
                else
                {
                    LogManager.Default.Warning($"验证字符串参数数量错误: {paramsArray.Length}, 期望: 新版本5个参数或老版本3个参数");
                    
                    return;
                }

                
                _state = ClientState.GSUM_WAITINGDBINFO;
                LogManager.Default.Info($"已设置状态为GSUM_WAITINGDBINFO，等待数据库返回角色信息");

                
                
                string account;
                string serverName = _server.GetServerName();

                
                LogManager.Default.Info($"开始从服务器获取进入信息，登录ID={loginId}");
                var enterInfo = _server.GetEnterInfo(loginId);
                if (enterInfo != null)
                {
                    
                    account = enterInfo.Value.GetAccount();
                    LogManager.Default.Info($"使用ServerCenter提供的账号: '{account}' (登录ID={loginId})");

                    
                    _enterInfo = enterInfo.Value;
                    LogManager.Default.Info($"已设置_enterInfo: 账号='{_enterInfo.GetAccount()}', 角色名='{_enterInfo.GetName()}', 登录ID={_enterInfo.nLoginId}, 选择角色ID={_enterInfo.nSelCharId}");

                    
                    if (isNewVersion && _enterInfo.GetName() != charName)
                    {
                        LogManager.Default.Error($"角色名不匹配: ServerCenter提供='{_enterInfo.GetName()}', 客户端发送='{charName}'");
                        SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "您登陆的角色已经登陆该服务器！");
                        Disconnect(1000);
                        return;
                    }

                    
                    _server.RemoveEnterInfo(loginId);
                    LogManager.Default.Info($"已从服务器字典移除进入信息，登录ID={loginId}");
                }
                else
                {
                    
                    if (isNewVersion)
                    {
                        
                        account = $"char_{loginId}";
                        LogManager.Default.Warning($"ServerCenter未提供账号，使用备选账号: {account}");
                    }
                    else
                    {
                        
                        account = $"old_{selCharId}";
                        LogManager.Default.Warning($"老版本客户端，ServerCenter未提供账号，使用selCharId作为账号: {account}");
                    }

                    
                    _enterInfo.nLoginId = loginId;
                    _enterInfo.nSelCharId = selCharId;
                    _enterInfo.SetName(charName);
                    _enterInfo.dwSelectCharServerId = 1; 
                    LogManager.Default.Info($"已设置进入信息: 登录ID={loginId}, 选择角色ID={selCharId}, 角色名={charName}");
                }

                
                LogManager.Default.Info($"开始查询数据库信息1: 角色={charName}, 账号={account}, 服务器={serverName}");

                
                var dbClient = _server.GetDbServerClient();
                if (dbClient == null)
                {
                    LogManager.Default.Error("无法获取DBServerClient");
                    SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "数据库错误！");
                    
                    return;
                }

                
                
                LogManager.Default.Info($"开始查询数据库信息: 角色={charName}, 账号={account}");

                byte[]? charData = await dbClient.GetCharDBInfoBytesAsync2(account, serverName, charName, _clientKey, 0);
                if (charData != null)
                {
                    
                    
                }
                else
                {
                    LogManager.Default.Error($"查询数据库信息失败: {charName}");
                    SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "查询数据库信息失败！");
                    
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理解码后的字符串失败: {ex.Message}");
                
            }

            await Task.CompletedTask;
        }

        #endregion

        
        
        
        private void SendMsg(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, string? message = null)
        {
            try
            {
                byte[]? payload = null;
                if (message != null)
                {
                    payload = Encoding.GetEncoding("GBK").GetBytes(message);
                }

                GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
            }
        }

        
        
        
        private void SendMsg2(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, string? message = null)
        {
            try
            {
                byte[]? payload = null;
                if (message != null)
                {
                    payload = Encoding.GetEncoding("GBK").GetBytes(message);
                }

                GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
            }
        }

        
        
        
        private void SendMsg2(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, byte[]? payload)
        {
            try
            {
                GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
            }
        }

        
        
        
        private void Disconnect(int delayMs = 0)
        {
            if (delayMs > 0)
            {
                Task.Delay(delayMs).ContinueWith(_ => Disconnect());
                return;
            }

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
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

        
        
        
        private Player? FindPlayerByName(string charName)
        {
            try
            {
                
                return HumanPlayerMgr.Instance.FindByName(charName);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"查找玩家失败: {ex.Message}");
                return null;
            }
        }

        private static string NormalizeItemName(string? rawName)
        {
            if (string.IsNullOrEmpty(rawName))
                return string.Empty;

            int nul = rawName.IndexOf('\0');
            if (nul >= 0)
                rawName = rawName.Substring(0, nul);

            return rawName.Trim();
        }

        private ItemInstance? CreateItemInstanceFromDbItem(in MirCommon.Database.DBITEM dbItem)
        {
            try
            {
                var item = dbItem.item;
                string name = NormalizeItemName(item.baseitem.szName);
                if (string.IsNullOrWhiteSpace(name))
                {
                    LogManager.Default.Warning($"DBITEM缺少物品名称: makeIndex={item.dwMakeIndex}");
                    return null;
                }

                var def = ItemManager.Instance.GetDefinitionByName(name);
                if (def == null)
                {
                    LogManager.Default.Warning($"未找到物品定义: '{name}' (makeIndex={item.dwMakeIndex})");
                    return null;
                }

                var instance = ItemManager.Instance.CreateItem(def.ItemId, 1);
                if (instance == null)
                    return null;

                instance.Durability = item.wCurDura;
                instance.MaxDurability = item.wMaxDura;
                if (item.dwMakeIndex != 0)
                    instance.InstanceId = item.dwMakeIndex;

                
                instance.EnhanceLevel = item.baseitem.btUpgradeTimes;
                instance.DressColor = (byte)(item.baseitem.btFlag & 0x0F);

                
                instance.UsingStartTime = (uint)(item.baseitem.Ac1 |
                    (item.baseitem.Ac2 << 8) |
                    (item.baseitem.Mac1 << 16) |
                    (item.baseitem.Mac2 << 24));

                return instance;
            }
            catch (Exception ex)
            {
                LogManager.Default.Warning($"DBITEM转换为ItemInstance失败: {ex.Message}");
                return null;
            }
        }

        
        
        
        private void OnDBItem(MirCommon.Database.DBITEM[] pItemArray, int nCount, byte btFlag)
        {
            try
            {
                LogManager.Default.Info($"处理数据库物品数据: 数量={nCount}, 标志={btFlag}");

                if (_player == null)
                {
                    LogManager.Default.Error("玩家对象为空，无法处理物品数据");
                    return;
                }

                
                if (nCount > 1 && pItemArray != null)
                {
                    Array.Sort(pItemArray, 0, Math.Min(nCount, pItemArray.Length), Comparer<MirCommon.Database.DBITEM>.Create((a, b) => a.wPos.CompareTo(b.wPos)));
                }

                
                switch (btFlag)
                {
                    case (byte)ItemDataFlag.IDF_BANK:
                        {
                            LogManager.Default.Info($"处理仓库物品: {nCount}个");

                            _bankCache.Clear();
                            for (int i = 0; i < nCount; i++)
                            {
                                var inst = CreateItemInstanceFromDbItem(pItemArray[i]);
                                if (inst != null)
                                    _bankCache.Add(inst);
                            }

                            
                            _bankLoaded = true;
                            LogManager.Default.Debug($"仓库数据加载完成(缓存条目={_bankCache.Count})");
                            CheckAllDataLoaded();
                        }
                        break;
                    case (byte)ItemDataFlag.IDF_BAG:
                        {
                            LogManager.Default.Info($"处理背包物品: {nCount}个");

                            _player.SetSystemFlag((int)MirCommon.SystemFlag.SF_BAGLOADED, true);

                            
                            _player.Inventory.Clear();

                            
                            for (int i = 0; i < nCount; i++)
                            {
                                var inst = CreateItemInstanceFromDbItem(pItemArray[i]);
                                if (inst == null)
                                    continue;

                                int pos = pItemArray[i].wPos;
                                bool placed = false;

                                if (pos >= 0 && pos < _player.Inventory.MaxSlots)
                                {
                                    placed = _player.Inventory.TrySetItem(pos, inst);
                                }

                                
                                if (!placed)
                                {
                                    placed = _player.Inventory.TryAddItemNoStack(inst, out int fallbackSlot);
                                    if (placed)
                                    {
                                        LogManager.Default.Warning($"背包装载pos冲突/越界，已落到空位: makeIndex={pItemArray[i].item.dwMakeIndex}, dbPos={pItemArray[i].wPos}, slot={fallbackSlot}");
                                    }
                                }

                                if (!placed)
                                {
                                    LogManager.Default.Warning($"背包装载失败(可能已满): makeIndex={pItemArray[i].item.dwMakeIndex}, pos={pItemArray[i].wPos}");
                                }
                            }

                            _bagLoaded = true;
                            LogManager.Default.Debug("背包数据加载完成");

                            
                            SendBagItems(pItemArray, nCount);
                            TrySendEnterGameOk();
                            CheckAllDataLoaded();
                        }
                        break;
                    case (byte)ItemDataFlag.IDF_EQUIPMENT:
                        {
                            LogManager.Default.Info($"处理装备物品: {nCount}个");

                            _player.SetSystemFlag((int)MirCommon.SystemFlag.SF_EQUIPMENTLOADED, true);

                            
                            try
                            {
                                for (int s = 0; s < (int)EquipSlot.Max; s++)
                                {
                                    try { _player.Equipment.Unequip((EquipSlot)s); } catch { }
                                }
                            }
                            catch { }

                            for (int i = 0; i < nCount; i++)
                            {
                                var dbItem = pItemArray[i];
                                var inst = CreateItemInstanceFromDbItem(dbItem);
                                if (inst == null)
                                    continue;

                                int posInt = dbItem.wPos;
                                if (posInt < 0) posInt = 0;
                                if (posInt >= (int)EquipSlot.Max) posInt = (int)EquipSlot.Max - 1;
                                var slot = (EquipSlot)posInt;

                                bool equipped = false;
                                try
                                {
                                    equipped = _player.Equipment.Equip(slot, inst);
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Default.Warning($"装备装载异常: pos={dbItem.wPos}, err={ex.Message}");
                                }

                                if (!equipped)
                                {
                                    _player.Inventory.AddItem(inst);
                                }
                            }

                            _equipmentLoaded = true;
                            LogManager.Default.Debug("装备数据加载完成");

                            SendEquipments();
                            CheckAllDataLoaded();
                        }
                        break;
                    case (byte)ItemDataFlag.IDF_PETBANK:
                        {
                            LogManager.Default.Info($"处理宠物仓库物品: {nCount}个");
                            var items = new MirCommon.Item[nCount];
                            for (int i = 0; i < nCount; i++)
                            {
                                items[i] = pItemArray[i].item;
                            }
                            _player.OnPetBank(items, nCount);
                            _petBankLoaded = true;
                            LogManager.Default.Debug("宠物仓库数据加载完成");
                            CheckAllDataLoaded();
                        }
                        break;
                    default:
                        LogManager.Default.Warning($"未知的物品标志: {btFlag}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理数据库物品数据失败: {ex.Message}");
            }
        }

        
        
        
        private void OnCreateItem(MirCommon.Item item, int pos, byte btFlag)
        {
            try
            {
                LogManager.Default.Info($"处理创建物品: 位置={pos}, 标志={btFlag}");

                if (_player == null)
                {
                    LogManager.Default.Error("玩家对象为空，无法创建物品");
                    return;
                }

                
                
                ItemInstance? instance = null;
                try
                {
                    string name = NormalizeItemName(item.baseitem.szName);
                    if (string.IsNullOrWhiteSpace(name))
                        throw new InvalidOperationException("物品名称为空");

                    var def = ItemManager.Instance.GetDefinitionByName(name);
                    if (def == null)
                        throw new InvalidOperationException($"未找到物品定义: {name}");

                    instance = ItemManager.Instance.CreateItem(def.ItemId, 1);
                    if (instance != null)
                    {
                        instance.Durability = item.wCurDura;
                        instance.MaxDurability = item.wMaxDura;

                        
                        
                        if (item.dwMakeIndex != 0)
                            instance.InstanceId = item.dwMakeIndex;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Warning($"创建ItemInstance失败，后续仅记录: {ex.Message}");
                }

                if (btFlag == (byte)ItemDataFlag.IDF_BAG)
                {
                    
                    if (instance == null)
                    {
                        LogManager.Default.Warning($"创建背包物品失败(实例为空): makeIndex={item.dwMakeIndex}");
                        return;
                    }

                    bool added = _player.AddItem(instance);
                    if (!added)
                    {
                        
                        LogManager.Default.Warning($"背包已满，尝试掉落物品: makeIndex={item.dwMakeIndex}");
                        if (_player.CurrentMap != null)
                        {
                            DownItemMgr.Instance.DropItem(_player.CurrentMap, instance, (ushort)_player.X, (ushort)_player.Y, _player.ObjectId);
                        }
                    }
                    else
                    {
                        
                        SendWeightChangedMessage();
                    }

                    LogManager.Default.Debug($"创建背包物品完成: 位置={pos}, makeIndex={item.dwMakeIndex}");
                }
                else
                {
                    
                    if (instance == null)
                    {
                        LogManager.Default.Warning($"创建掉落物品失败(实例为空): makeIndex={item.dwMakeIndex}");
                        return;
                    }

                    if (_player.CurrentMap == null)
                    {
                        LogManager.Default.Warning($"玩家不在地图上，无法掉落物品: {item.dwMakeIndex}");
                        return;
                    }

                    DownItemMgr.Instance.DropItem(_player.CurrentMap, instance, (ushort)_player.X, (ushort)_player.Y, _player.ObjectId);
                    LogManager.Default.Debug($"创建掉落物品完成: makeIndex={item.dwMakeIndex}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理创建物品失败: {ex.Message}");
            }
        }

        
        
        
        private void SendBagItems(MirCommon.Database.DBITEM[] pItems, int count)
        {
            try
            {
                if (_player == null) return;

                LogManager.Default.Info($"发送背包物品: {count}个");

                
                if (count > 100) count = 100;

                
                var items = new MirCommon.ItemClient[count];
                for (int i = 0; i < count; i++)
                {
                    
                    var dbItem = pItems[i];
                    var inst = CreateItemInstanceFromDbItem(in dbItem);

                    var baseItem = dbItem.item.baseitem;
                    uint makeIndex = dbItem.item.dwMakeIndex;
                    ushort curDura = dbItem.item.wCurDura;
                    ushort maxDura = dbItem.item.wMaxDura;
                    if (inst != null)
                    {
                        baseItem = ItemPacketBuilder.BuildBaseItem(inst);
                        makeIndex = unchecked((uint)inst.InstanceId);
                        curDura = (ushort)Math.Clamp(inst.Durability, 0, ushort.MaxValue);
                        maxDura = (ushort)Math.Clamp(inst.MaxDurability, 0, ushort.MaxValue);
                    }

                    items[i] = new MirCommon.ItemClient
                    {
                        baseitem = baseItem,
                        dwMakeIndex = makeIndex,
                        wCurDura = curDura,
                        wMaxDura = maxDura
                    };
                    LogManager.Default.Debug($"转换物品 {i + 1}/{count}: ID={dbItem.item.dwMakeIndex}, 名称={dbItem.item.baseitem.szName}");
                }

                
                
                byte[] itemsData = StructArrayToBytes(items);
                LogManager.Default.Info($"发送背包物品数据: ItemClient数组大小={itemsData.Length}字节, 物品数量={count}, 每个ItemClient大小={System.Runtime.InteropServices.Marshal.SizeOf<MirCommon.ItemClient>()}字节");
                SendMsg2(_player.ObjectId, GameMessageHandler.ServerCommands.SM_BAGINFO, 0, 0, (ushort)count, itemsData);

                
                SendWeightChangedMessage();

                
                var bagItemPos = new MirCommon.BAGITEMPOS[count];
                for (int i = 0; i < count; i++)
                {
                    bagItemPos[i] = new MirCommon.BAGITEMPOS
                    {
                        ItemId = pItems[i].item.dwMakeIndex,
                        wPos = pItems[i].wPos
                    };
                    
                    LogManager.Default.Debug($"设置物品位置 {i + 1}/{count}: ID={bagItemPos[i].ItemId}, 位置={bagItemPos[i].wPos}");
                }

                _player.SetBagItemPos(bagItemPos, count);
                
                byte[] itemposData = StructArrayToBytes(bagItemPos);
                SendMsg2(0, GameMessageHandler.ServerCommands.SM_SETITEMPOSITION, 0, 0, (ushort)count, itemposData);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送背包物品失败: {ex.Message}");
            }
        }

        
        
        
        private void SendEquipments()
        {
            try
            {
                if (_player == null) return;

                LogManager.Default.Info("发送装备信息");

                uint dwFeather = _player.GetFeather();
                var equipments = new MirCommon.EQUIPMENT[20];
                int count = _player.GetEquipments(equipments);

                
                if (count < 0) count = 0;
                if (count > equipments.Length) count = equipments.Length;

                var sendEquipments = new MirCommon.EQUIPMENT[count];
                if (count > 0)
                {
                    Array.Copy(equipments, sendEquipments, count);
                }

                byte[] equipData = StructArrayToBytes(sendEquipments);
                SendMsg2(0, GameMessageHandler.ServerCommands.SM_EQUIPMENTS, 0, 0, 0, equipData);

                
                _player.SendAllEquipmentDura();

                _player.SendFeatureChanged();
                _player.UpdateProp();
                _player.UpdateSubProp();
                _player.SendStatusChanged();

                
                TrySendEnterGameOk();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送装备信息失败: {ex.Message}");
            }
        }

        
        
        
        private void SendMsg2(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, byte[]? payload, object? data = null)
        {
            try
            {
                if (data != null)
                {
                    
                    int size = System.Runtime.InteropServices.Marshal.SizeOf(data);
                    byte[] dataBytes = new byte[size];
                    IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
                    try
                    {
                        System.Runtime.InteropServices.Marshal.StructureToPtr(data, ptr, false);
                        System.Runtime.InteropServices.Marshal.Copy(ptr, dataBytes, 0, size);
                    }
                    finally
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                    }

                    GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, dataBytes);
                }
                else if (payload != null)
                {
                    GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
                }
                else
                {
                    GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
            }
        }

        
        
        
        private T BytesToStruct<T>(byte[] bytes, int offset, int size) where T : struct
        {
            if (offset + size > bytes.Length)
                throw new ArgumentException($"字节数组长度不足: {bytes.Length} < {offset + size}");

            byte[] slice = new byte[size];
            Array.Copy(bytes, offset, slice, 0, size);
            return BytesToStruct<T>(slice);
        }

        
        
        
        private void SendWeightChangedMessage()
        {
            try
            {
                if (_player == null) return;

                _player.SendWeightChanged();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送重量变化消息失败: {ex.Message}");
            }
        }

        
        
        
        private byte[] StructArrayToBytes<T>(T[] array) where T : struct
        {
            if (array == null || array.Length == 0)
                return Array.Empty<byte>();

            int elementSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            byte[] result = new byte[elementSize * array.Length];
            
            for (int i = 0; i < array.Length; i++)
            {
                IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(elementSize);
                try
                {
                    System.Runtime.InteropServices.Marshal.StructureToPtr(array[i], ptr, false);
                    System.Runtime.InteropServices.Marshal.Copy(ptr, result, i * elementSize, elementSize);
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                }
            }
            
            return result;
        }

        private static byte[] StructToBytes<T>(T structure) where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            byte[] bytes = new byte[size];
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(structure, ptr, false);
                System.Runtime.InteropServices.Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }

            return bytes;
        }

        
        
        
        private void CheckAllDataLoaded()
        {
            try
            {
                if (_player == null) return;

                LogManager.Default.Debug($"检查数据加载状态: 背包={_bagLoaded}, 装备={_equipmentLoaded}, 技能={_magicLoaded}, 任务={_taskInfoLoaded}, 升级物品={_upgradeItemLoaded}, 宠物仓库={_petBankLoaded}, 仓库={_bankLoaded}");

                
                
                
                
                
                
                
                
                

                bool allDataLoaded = _bagLoaded && _equipmentLoaded && _magicLoaded &&
                                    _taskInfoLoaded && _upgradeItemLoaded &&
                                    _petBankLoaded && _bankLoaded;

                if (allDataLoaded)
                {
                    LogManager.Default.Info($"所有数据已加载完成: {_player.Name}");

                    
                    

                    
                    _player.SetSystemFlag((int)MirCommon.SystemFlag.SF_ALLDATALOADED, true);

                    
                    
                    TrySendEnterGameOk();

                    
                    _player.UpdateProp();
                    _player.UpdateSubProp();

                    
                    _player.SendStatusChanged();

                    
                    _player.SendFeatureChanged();

                    
                    SendWeightChangedMessage();

                    
                    if (_player.IsFirstLogin)
                    {
                        try
                        {
                            LogManager.Default.Info($"玩家第一次登录: {_player.Name}");

                            var first = GameWorld.Instance.GetFirstLoginInfo();
                            if (first != null)
                            {
                                
                                if (first.Level > 0 && _player.Level < first.Level)
                                {
                                    _player.Level = (byte)Math.Clamp(first.Level, 1, 255);
                                }

                                if (first.Gold > 0 && _player.Gold < first.Gold)
                                {
                                    _player.Gold = first.Gold;
                                    _player.SendMoneyChanged(MoneyType.Gold);
                                }

                                
                                foreach (var it in first.Items)
                                {
                                    if (it == null || string.IsNullOrWhiteSpace(it.ItemName))
                                        continue;

                                    int giveCount = Math.Max(1, it.Count);
                                    for (int i = 0; i < giveCount; i++)
                                    {
                                        if (!_player.CreateBagItem(it.ItemName, silence: false))
                                        {
                                            LogManager.Default.Warning($"首次登录发放物品失败: player={_player.Name}, item={it.ItemName}");
                                            break;
                                        }
                                    }
                                }
                            }

                            
                            _player.IsFirstLogin = false;
                            var dbClient = _server.GetDbServerClient();
                            _player.UpdateToDB(dbClient);
                        }
                        catch (Exception ex)
                        {
                            LogManager.Default.Warning($"首次登录处理失败: {_player.Name} - {ex.Message}");
                        }
                    }

                    LogManager.Default.Info($"玩家数据加载完成: {_player.Name}");
                }
                else
                {
                    LogManager.Default.Debug($"数据尚未完全加载，等待更多数据...");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"检查数据加载状态失败: {ex.Message}");
            }
        }

        
        
        
        public void HandleDbServerMessage(MirCommon.MirMsg msg)
        {
            try
            {
                LogManager.Default.Info($"GameClient收到转发的DBServer消息: Cmd=0x{msg.wCmd:X4}, Flag=0x{msg.dwFlag:X8}, w1={msg.wParam[0]}, w2={msg.wParam[1]}, w3={msg.wParam[2]}, 数据长度={msg.data?.Length ?? 0}字节");

                
                _ = OnDBMsg(msg, msg.data?.Length ?? 0);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"GameClient处理DBServer消息失败: {ex.Message}");
            }
        }

        
        
        
        public uint GetClientKey()
        {
            return _clientKey;
        }

        
        
        
        private bool IsMessageForMe(MirMsg pMsg)
        {
            try
            {
                
                
                
                
                

                if (pMsg.wCmd == (ushort)DbMsg.DM_QUERYITEMS)
                {
                    if (pMsg.data == null || pMsg.data.Length < 4) return false;
                    uint keyInData = BitConverter.ToUInt32(pMsg.data, 0);
                    return keyInData == _clientKey;
                }

                if (pMsg.wCmd == (ushort)DbMsg.DM_GETCHARDBINFO)
                {
                    if (pMsg.data == null || pMsg.data.Length < 4) return false;
                    try
                    {
                        var charDbInfo = BytesToStruct<MirCommon.Database.CHARDBINFO>(pMsg.data);
                        return charDbInfo.dwClientKey == _clientKey;
                    }
                    catch
                    {
                        
                        uint keyFallback = (uint)((pMsg.wParam[1] << 16) | pMsg.wParam[0]);
                        return keyFallback == _clientKey;
                    }
                }

                uint key = (uint)((pMsg.wParam[1] << 16) | pMsg.wParam[0]);
                return key == _clientKey;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"检查消息归属失败: {ex.Message}");
                return false;
            }
        }

        private async Task HandleZuoyi(MirMsgOrign msg, byte[] payload)
        {
            
            
            await Task.CompletedTask;
        }

        private async Task HandleChangeGroupMode(MirMsgOrign msg, byte[] payload)
        {
            
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryAddGroupMember(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null)
            {
                await Task.CompletedTask;
                return;
            }

            
            string targetName = payload != null && payload.Length > 0
                ? Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0')
                : string.Empty;

            if (string.IsNullOrWhiteSpace(targetName))
            {
                _player.SaySystem("玩家名为空");
                return;
            }

            var target = HumanPlayerMgr.Instance.FindByName(targetName);
            if (target == null)
            {
                _player.SaySystem("玩家不存在");
                return;
            }

            
            var group = GroupObjectManager.Instance.GetPlayerGroup(_player);
            if (group == null)
            {
                group = GroupObjectManager.Instance.CreateGroup(_player, target);
                if (group == null)
                    _player.SaySystem("无法创建小组");
            }
            else
            {
                if (!group.AddMember(target))
                    _player.SaySystem("无法加入小组");
            }

            await Task.CompletedTask;
        }

        private async Task HandleDeleteGroupMember(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null)
            {
                await Task.CompletedTask;
                return;
            }

            string targetName = payload != null && payload.Length > 0
                ? Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0')
                : string.Empty;

            var group = GroupObjectManager.Instance.GetPlayerGroup(_player);
            if (group == null)
            {
                _player.SaySystem("您没有加入小组");
                return;
            }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                
                group.LeaveMember(_player);
                return;
            }

            var target = HumanPlayerMgr.Instance.FindByName(targetName);
            if (target == null)
            {
                _player.SaySystem("玩家不存在");
                return;
            }

            
            if (!group.IsLeader(_player))
            {
                _player.SaySystem("只有队长才能踢人");
                return;
            }

            group.DelMember(target);
            await Task.CompletedTask;
        }

        private async Task HandleQueryStartPrivateShop(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null)
            {
                await Task.CompletedTask;
                return;
            }

            
            string shopName = payload != null && payload.Length > 0
                ? Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0')
                : string.Empty;

            if (string.IsNullOrWhiteSpace(shopName))
                shopName = $"{_player.Name}的摊位";

            var stall = StallManager.Instance.GetPlayerStall(_player.ObjectId);
            if (stall == null)
            {
                stall = StallManager.Instance.CreateStall(
                    _player.ObjectId,
                    _player.Name,
                    shopName,
                    (uint)_player.MapId,
                    (ushort)_player.X,
                    (ushort)_player.Y);
            }

            if (stall == null)
            {
                _player.SaySystem("无法开启个人商店");
                return;
            }

            StallManager.Instance.OpenStall(stall.StallId, _player.ObjectId);
            _player.SaySystem($"个人商店已开启: {stall.Name}");

            await Task.CompletedTask;
        }

        
        private async Task HandleQueryTime(MirMsgOrign msg, byte[] payload)
        {
            
            uint currentTime = (uint)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            GameMessageHandler.SendSimpleMessage2(_stream, currentTime,
                GameMessageHandler.ServerCommands.SM_TIMERESPONSE, 0, 0, 0);
            await Task.CompletedTask;
        }
    }
}
