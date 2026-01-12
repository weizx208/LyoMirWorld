using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MirCommon.Network
{
    
    
    
    public class ServerCenterClient : IDisposable
    {
        private readonly string _serverCenterAddress;
        private readonly int _serverCenterPort;
        private TcpClient? _client;
        private NetworkStream? _stream;
        public bool _connected = false;
        private byte _serverIndex = 0; 
        private ServerId _serverId = new ServerId(); 
        private string _serverName = string.Empty; 
        private ServerAddr _serverAddr = new ServerAddr(); 

        public ServerCenterClient(string address = "127.0.0.1", int port = 6000)
        {
            _serverCenterAddress = address;
            _serverCenterPort = port;
        }

        
        
        
        public bool IsConnected()
        {
            return _connected;
        }

        
        
        
        public byte GetServerIndex()
        {
            return _serverIndex;
        }

        
        
        
        public ServerId GetServerId()
        {
            return _serverId;
        }

        
        
        
        public string GetServerName()
        {
            return _serverName;
        }

        
        
        
        public ServerAddr GetServerAddr()
        {
            return _serverAddr;
        }

        
        
        
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_serverCenterAddress, _serverCenterPort);
                _stream = _client.GetStream();
                _connected = true;
                return true;
            }
            catch (Exception)
            {
                _connected = false;
                return false;
            }
        }

        
        
        
        public void Disconnect()
        {
            _connected = false;
            _stream?.Close();
            _client?.Close();
            _stream = null;
            _client = null;
        }

        
        
        
        public async Task<bool> RegisterServerAsync(string serverType, string serverName, string address, int port, int maxConnections)
        {
            if (!_connected) return false;

            try
            {
                
                var registerInfo = new RegisterServerInfo
                {
                    szName = MirCommon.Utils.Helper.ConvertToFixedBytes(serverName,64),
                    Id = new ServerId
                    {
                        bType = GetServerTypeByte(serverType),
                        bGroup = 0, 
                        bId = 0,    
                        bIndex = 0  
                    },
                    addr = new ServerAddr()
                };
                
                
                registerInfo.addr.SetAddress(address);
                registerInfo.addr.nPort = (uint)port;

                
                byte[] payload = StructToBytes(registerInfo);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); 
                builder.WriteUInt16(ProtocolCmd.CM_REGISTERSERVER);
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return false;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                
                if (wCmd == ProtocolCmd.SM_REGISTERSERVEROK && dwFlag == 1)  
                {
                    
                    
                    
                    if (bytesRead >= 12 + 8) 
                    {
                        reader.ReadUInt16(); 
                        reader.ReadUInt16(); 
                        reader.ReadUInt16(); 
                        
                        
                        
                        byte bType = reader.ReadByte();
                        byte bGroup = reader.ReadByte();
                        byte bIndex = reader.ReadByte();
                        reader.ReadByte(); 
                        
                        
                        _serverIndex = bIndex;
                        _serverId.bType = bType;
                        _serverId.bGroup = bGroup;
                        _serverId.bIndex = bIndex;
                        _serverName = serverName;
                        _serverAddr.SetAddress(address);
                        _serverAddr.nPort = (uint)port;
                        
                        Console.WriteLine($"服务器注册成功，分配的索引: {_serverIndex}, 类型: {bType}, 组: {bGroup}");
                    }
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        
        
        
        public async Task<FindServerResult?> FindServerAsync(string serverType, string serverName)
        {
            if (!_connected) return null;

            try
            {
                
                string queryData = $"{serverType}/{serverName}";
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(queryData);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); 
                builder.WriteUInt16(ProtocolCmd.SCM_FINDSERVER);
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return null;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32(); 
                ushort wCmd = reader.ReadUInt16();
                reader.ReadUInt16(); 
                reader.ReadUInt16(); 
                reader.ReadUInt16(); 
                
                if (wCmd == ProtocolCmd.SCM_FINDSERVER && dwFlag == 0) 
                {
                    
                    
                    if (bytesRead - 12 >= 24)
                    {
                        
                        byte bType = reader.ReadByte();
                        byte bGroup = reader.ReadByte();
                        byte bIndex = reader.ReadByte();
                        reader.ReadByte(); 
                        
                        
                        byte[] addrBytes = reader.ReadBytes(16);
                        
                        
                        uint nPort = reader.ReadUInt32();
                        
                        
                        var result = new FindServerResult
                        {
                            Id = new ServerId
                            {
                                bType = bType,
                                bGroup = bGroup,
                                bIndex = bIndex
                            },
                            addr = new ServerAddr
                            {
                                addr = addrBytes,
                                nPort = nPort
                            }
                        };
                        
                        return result;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        
        
        
        
        
        
        
        
        public async Task<bool> SendMsgAcrossServerAsync(uint clientId, ushort cmd, byte sendType, ushort targetIndex, string data)
        {
            if (!_connected) return false;

            try
            {
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(clientId);
                builder.WriteUInt16(ProtocolCmd.SCM_MSGACROSSSERVER);
                builder.WriteUInt16(cmd); 
                builder.WriteUInt16(sendType); 
                builder.WriteUInt16(targetIndex); 
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        
        
        
        
        
        public async Task<bool> SendMsgAcrossServerAsync(uint clientId, ushort cmd, byte sendType, ushort targetIndex, byte[] binaryData)
        {
            if (!_connected) return false;

            try
            {
                var builder = new PacketBuilder();
                builder.WriteUInt32(clientId);
                builder.WriteUInt16(ProtocolCmd.SCM_MSGACROSSSERVER);
                builder.WriteUInt16(cmd); 
                builder.WriteUInt16(sendType); 
                builder.WriteUInt16(targetIndex); 
                builder.WriteBytes(binaryData);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        private ushort GetServerTypeId(string serverType)
        {
            return serverType.ToLower() switch
            {
                "servercenter" => 1,
                "databaseserver" => 1,  
                "dbserver" => 1,        
                "loginserver" => 2,     
                "selectcharserver" => 4, 
                "gameserver" => 6,      
                _ => 0
            };
        }

        
        
        
        public async Task<string?> QueryServerAsync(string serverType, string serverName)
        {
            if (!_connected) return null;

            try
            {
                string data = $"{serverType}/{serverName}";
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); 
                builder.WriteUInt16(ProtocolCmd.CM_QUERYSERVER);
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return null;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                reader.ReadUInt16(); 
                reader.ReadUInt16(); 
                reader.ReadUInt16(); 
                byte[] responseData = reader.ReadBytes(bytesRead - 12);

                if (wCmd == ProtocolCmd.SM_QUERYSERVEROK && dwFlag == 1) 
                {
                    return Encoding.GetEncoding("GBK").GetString(responseData).TrimEnd('\0');
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        
        
        
        public async Task<bool> UnregisterServerAsync(string serverType, string serverName)
        {
            if (!_connected) return false;

            try
            {
                string data = $"{serverType}/{serverName}";
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); 
                builder.WriteUInt16(ProtocolCmd.CM_UNREGISTERSERVER);
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return false;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                
                return wCmd == ProtocolCmd.SM_UNREGISTERSERVEROK && dwFlag == 1;
            }
            catch
            {
                return false;
            }
        }


        
        
        
        private byte[] StructToBytes<T>(T structure) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            
            try
            {
                Marshal.StructureToPtr(structure, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return bytes;
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

        
        
        
        private byte GetServerTypeByte(string serverType)
        {
            return serverType.ToLower() switch
            {
                "servercenter" => 1,
                "databaseserver" => 1,  
                "dbserver" => 1,        
                "loginserver" => 2,     
                "selectcharserver" => 4, 
                "gameserver" => 6,      
                _ => 0
            };
        }

        
        
        
        
        
        
        
        public async Task<ServerAddr?> GetGameServerAddrAsync(string account, string charName, string mapName)
        {
            if (!_connected) return null;

            try
            {
                
                var enterGameServer = new EnterGameServer();
                enterGameServer.SetAccount(account);
                enterGameServer.SetName(charName);
                enterGameServer.nLoginId = 0;
                enterGameServer.nSelCharId = 0;
                enterGameServer.nClientId = 0;
                enterGameServer.dwEnterTime = (uint)Environment.TickCount;
                enterGameServer.dwSelectCharServerId = 0;

                byte[] payload = StructToBytes(enterGameServer);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); 
                builder.WriteUInt16(ProtocolCmd.SCM_GETGAMESERVERADDR);
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return null;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                ushort w1 = reader.ReadUInt16(); 
                reader.ReadUInt16(); 
                reader.ReadUInt16(); 
                
                
                if (wCmd == ProtocolCmd.SCM_GETGAMESERVERADDR && w1 == (ushort)SERVER_ERROR.SE_OK)
                {
                    
                    if (bytesRead - 12 >= 24)
                    {
                        
                        reader.ReadByte(); 
                        reader.ReadByte(); 
                        reader.ReadByte(); 
                        reader.ReadByte(); 
                        
                        
                        byte[] addrBytes = reader.ReadBytes(16);
                        
                        
                        uint nPort = reader.ReadUInt32();
                        
                        
                        return new ServerAddr
                        {
                            addr = addrBytes,
                            nPort = nPort
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

        
        
        
        
        
        public async Task<bool> SendHeartbeatAsync()
        {
            if (!_connected) return false;

            try
            {
                
                
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); 
                builder.WriteUInt16(ProtocolCmd.CM_QUERYSERVER); 
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                builder.WriteUInt16(0); 
                
                builder.WriteBytes(Encoding.GetEncoding("GBK").GetBytes("heartbeat"));

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
