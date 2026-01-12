using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MirCommon.Network
{
    
    
    
    public delegate Task<bool> MessageHandlerDelegate(uint clientId, byte[] data, int length);

    
    
    
    public class MessageHandlerManager
    {
        private readonly Dictionary<DbMsg, MessageHandlerDelegate> _handlers = new();
        private readonly object _lock = new();

        
        
        
        public void RegisterHandler(DbMsg msgType, MessageHandlerDelegate handler)
        {
            lock (_lock)
            {
                _handlers[msgType] = handler;
            }
        }

        
        
        
        public void UnregisterHandler(DbMsg msgType)
        {
            lock (_lock)
            {
                _handlers.Remove(msgType);
            }
        }

        
        
        
        public async Task<bool> HandleMessageAsync(DbMsg msgType, uint clientId, byte[] data, int length)
        {
            MessageHandlerDelegate? handler;
            
            lock (_lock)
            {
                if (!_handlers.TryGetValue(msgType, out handler))
                {
                    return false;
                }
            }

            try
            {
                return await handler(clientId, data, length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 处理消息 {msgType} 时发生错误: {ex.Message}");
                return false;
            }
        }

        
        
        
        public int GetHandlerCount()
        {
            lock (_lock)
            {
                return _handlers.Count;
            }
        }
    }

    
    
    
    public static class MessageParser
    {
        
        
        
        public static bool ParseDbMessage(byte[] data, int length, out DbMsg msgType, out byte[] payload)
        {
            msgType = DbMsg.DM_START;
            payload = Array.Empty<byte>();

            if (length < 2) 
                return false;

            try
            {
                
                ushort msgValue = BitConverter.ToUInt16(data, 0);
                msgType = (DbMsg)msgValue;

                
                if (length > 2)
                {
                    payload = new byte[length - 2];
                    Array.Copy(data, 2, payload, 0, payload.Length);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        
        
        
        public static byte[] BuildDbMessage(DbMsg msgType, byte[]? payload = null)
        {
            int payloadLength = payload?.Length ?? 0;
            byte[] result = new byte[2 + payloadLength];

            
            BitConverter.GetBytes((ushort)msgType).CopyTo(result, 0);

            
            if (payload != null && payloadLength > 0)
            {
                Array.Copy(payload, 0, result, 2, payloadLength);
            }

            return result;
        }

        
        
        
        public static string[] ParseStringParameters(string data, char separator = '/')
        {
            if (string.IsNullOrEmpty(data))
                return Array.Empty<string>();

            return data.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        }

        
        
        
        public static string BuildStringParameters(params string[] parameters)
        {
            return string.Join("/", parameters);
        }
    }

    
    
    
    public class PacketBuilder
    {
        private readonly List<byte> _buffer = new();

        public PacketBuilder()
        {
        }

        public PacketBuilder(DbMsg msgType)
        {
            WriteUInt16((ushort)msgType);
        }

        public PacketBuilder WriteByte(byte value)
        {
            _buffer.Add(value);
            return this;
        }

        public PacketBuilder WriteUInt16(ushort value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        public PacketBuilder WriteUInt32(uint value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        public PacketBuilder WriteInt32(int value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        public PacketBuilder WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteUInt16(0);
                return this;
            }

            byte[] strBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(value);
            WriteUInt16((ushort)strBytes.Length);
            _buffer.AddRange(strBytes);
            return this;
        }

        public PacketBuilder WriteBytes(byte[] data)
        {
            _buffer.AddRange(data);
            return this;
        }

        public byte[] Build()
        {
            return _buffer.ToArray();
        }

        public int Length => _buffer.Count;
    }

    
    
    
    public class PacketReader
    {
        private readonly byte[] _data;
        private int _position;

        public PacketReader(byte[] data)
        {
            _data = data;
            _position = 0;
        }

        public bool CanRead(int bytes)
        {
            return _position + bytes <= _data.Length;
        }

        public byte ReadByte()
        {
            if (!CanRead(1))
                throw new InvalidOperationException("Not enough data to read");

            return _data[_position++];
        }

        public ushort ReadUInt16()
        {
            if (!CanRead(2))
                throw new InvalidOperationException("Not enough data to read");

            ushort value = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            if (!CanRead(4))
                throw new InvalidOperationException("Not enough data to read");

            uint value = BitConverter.ToUInt32(_data, _position);
            _position += 4;
            return value;
        }

        public int ReadInt32()
        {
            if (!CanRead(4))
                throw new InvalidOperationException("Not enough data to read");

            int value = BitConverter.ToInt32(_data, _position);
            _position += 4;
            return value;
        }

        public string ReadString()
        {
            ushort length = ReadUInt16();
            if (length == 0)
                return string.Empty;

            if (!CanRead(length))
                throw new InvalidOperationException("Not enough data to read");

            string value = System.Text.Encoding.GetEncoding("GBK").GetString(_data, _position, length);
            _position += length;
            return value;
        }

        public byte[] ReadBytes(int count)
        {
            if (!CanRead(count))
                throw new InvalidOperationException("Not enough data to read");

            byte[] result = new byte[count];
            Array.Copy(_data, _position, result, 0, count);
            _position += count;
            return result;
        }

        public int Position => _position;
        public int Remaining => _data.Length - _position;
    }
}
