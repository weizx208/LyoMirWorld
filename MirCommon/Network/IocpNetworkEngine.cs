using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MirCommon.Utils;

namespace MirCommon.Network
{
    
    
    
    public class IocpNetworkEngine : IDisposable
    {
        #region Windows IOCP API
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateIoCompletionPort(IntPtr fileHandle, IntPtr existingCompletionPort, UIntPtr completionKey, uint numberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetQueuedCompletionStatus(IntPtr completionPort, out uint lpNumberOfBytesTransferred, out UIntPtr lpCompletionKey, out IntPtr lpOverlapped, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PostQueuedCompletionStatus(IntPtr completionPort, uint dwNumberOfBytesTransferred, UIntPtr dwCompletionKey, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        #endregion

        #region 常量定义
        private const int DEFAULT_WORKER_THREADS = 4;
        private const int DEFAULT_POST_ACCEPT_COUNT = 64;
        private const int DEFAULT_BUFFER_SIZE = 8192;
        private const int MAX_PENDING_CONNECTIONS = 1000;
        #endregion

        #region 内部类
        
        
        
        private enum IocpOperation
        {
            Accept,
            Receive,
            Send,
            Disconnect
        }

        
        
        
        private class IocpContext : IDisposable
        {
            public Socket Socket { get; set; }
            public IocpOperation Operation { get; set; }
            public byte[] Buffer { get; set; }
            public int Offset { get; set; }
            public int BytesTransferred { get; set; }
            public SocketAsyncEventArgs EventArgs { get; set; }
            public object UserToken { get; set; }
            public DateTime CreateTime { get; set; }

            public IocpContext()
            {
                CreateTime = DateTime.UtcNow;
            }

            public void Dispose()
            {
                EventArgs?.Dispose();
                Buffer = null;
                UserToken = null;
            }
        }

        
        
        
        private class Listener
        {
            public Socket Socket { get; set; }
            public IPEndPoint EndPoint { get; set; }
            public uint Id { get; set; }
            public int PostAcceptCount { get; set; }
            public DateTime CreateTime { get; set; }

            public Listener()
            {
                CreateTime = DateTime.UtcNow;
            }
        }

        
        
        
        private class TempClient
        {
            public Socket Socket { get; set; }
            public uint ListenerId { get; set; }
            public DateTime AcceptTime { get; set; }
            public bool PreDeleted { get; set; }
            public DateTime DeleteTime { get; set; }

            public TempClient()
            {
                AcceptTime = DateTime.UtcNow;
                PreDeleted = false;
            }

            public bool IsDeleteTimeout(int timeoutMs = 10000)
            {
                if (!PreDeleted) return false;
                return (DateTime.UtcNow - DeleteTime).TotalMilliseconds >= timeoutMs;
            }

            public void PreDelete(int timeoutMs = 10000)
            {
                PreDeleted = true;
                DeleteTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            }
        }
        #endregion

        #region 字段
        private IntPtr _completionPort = IntPtr.Zero;
        private readonly List<Thread> _workerThreads = new List<Thread>();
        private readonly ConcurrentDictionary<uint, Listener> _listeners = new ConcurrentDictionary<uint, Listener>();
        private readonly ConcurrentDictionary<IntPtr, IocpContext> _contexts = new ConcurrentDictionary<IntPtr, IocpContext>();
        private readonly ConcurrentQueue<TempClient> _acceptQueue = new ConcurrentQueue<TempClient>();
        private readonly ConcurrentQueue<Socket> _disconnectQueue = new ConcurrentQueue<Socket>();
        private readonly ObjectPool<SocketAsyncEventArgs> _eventArgsPool;
        private readonly ZeroCopyBuffer _zeroCopyBuffer;

        private volatile bool _isRunning = false;
        private volatile bool _isDisposed = false;
        private readonly object _syncLock = new object();

        
        private long _totalRecvBytes = 0;
        private long _totalSendBytes = 0;
        private long _totalRecvPackets = 0;
        private long _totalSendPackets = 0;
        private long _totalConnections = 0;
        private long _totalDisconnections = 0;
        #endregion

        #region 事件
        
        
        
        public event Action<Socket, uint> OnConnection;

        
        
        
        public event Action<Socket> OnDisconnection;

        
        
        
        public event Action<Socket, byte[], int> OnDataReceived;

        
        
        
        public event Action<Socket, Exception> OnError;
        #endregion

        #region 构造函数
        
        
        
        public IocpNetworkEngine(bool useUnmanagedMemory = false)
        {
            _eventArgsPool = new ObjectPool<SocketAsyncEventArgs>(() =>
            {
                var args = new SocketAsyncEventArgs();
                args.Completed += OnIoCompleted;
                return args;
            }, 1000);

            _zeroCopyBuffer = new ZeroCopyBuffer(useUnmanagedMemory);
        }
        #endregion

        #region 公共方法
        
        
        
        
        
        public bool Start(int workerThreads = DEFAULT_WORKER_THREADS)
        {
            lock (_syncLock)
            {
                if (_isRunning) return true;
                if (_isDisposed) throw new ObjectDisposedException(nameof(IocpNetworkEngine));

                try
                {
                    
                    _completionPort = CreateIoCompletionPort(new IntPtr(-1), IntPtr.Zero, UIntPtr.Zero, 0);
                    if (_completionPort == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        LogManager.Default.Error($"创建完成端口失败，错误代码: {error}");
                        return false;
                    }

                    
                    for (int i = 0; i < workerThreads; i++)
                    {
                        var thread = new Thread(WorkerThreadProc)
                        {
                            Name = $"IOCP Worker {i + 1}",
                            IsBackground = true
                        };
                        thread.Start();
                        _workerThreads.Add(thread);
                    }

                    _isRunning = true;
                    LogManager.Default.Info($"IOCP网络引擎已启动，工作线程数: {workerThreads}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"启动IOCP引擎失败: {ex.Message}");
                    Stop();
                    return false;
                }
            }
        }

        
        
        
        public void Stop()
        {
            lock (_syncLock)
            {
                if (!_isRunning) return;

                _isRunning = false;

                
                for (int i = 0; i < _workerThreads.Count; i++)
                {
                    PostQueuedCompletionStatus(_completionPort, 0, UIntPtr.Zero, IntPtr.Zero);
                }

                
                foreach (var thread in _workerThreads)
                {
                    try
                    {
                        if (thread.IsAlive)
                            thread.Join(5000);
                    }
                    catch { }
                }
                _workerThreads.Clear();

                
                foreach (var listener in _listeners.Values)
                {
                    try
                    {
                        listener.Socket?.Close();
                    }
                    catch { }
                }
                _listeners.Clear();

                
                if (_completionPort != IntPtr.Zero)
                {
                    CloseHandle(_completionPort);
                    _completionPort = IntPtr.Zero;
                }

                
                _eventArgsPool.Clear();
                _zeroCopyBuffer?.Dispose();

                LogManager.Default.Info("IOCP网络引擎已停止");
            }
        }

        
        
        
        
        
        
        
        
        public bool StartListen(string ipAddress, int port, int postAcceptCount = DEFAULT_POST_ACCEPT_COUNT, uint listenerId = 0)
        {
            if (!_isRunning) return false;

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
                var listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                    ReceiveBufferSize = DEFAULT_BUFFER_SIZE,
                    SendBufferSize = DEFAULT_BUFFER_SIZE
                };

                listenerSocket.Bind(endPoint);
                listenerSocket.Listen(MAX_PENDING_CONNECTIONS);

                
                if (!BindSocketToCompletionPort(listenerSocket, listenerId))
                {
                    listenerSocket.Close();
                    return false;
                }

                var listener = new Listener
                {
                    Socket = listenerSocket,
                    EndPoint = endPoint,
                    Id = listenerId,
                    PostAcceptCount = postAcceptCount
                };

                if (!_listeners.TryAdd(listenerId, listener))
                {
                    listenerSocket.Close();
                    return false;
                }

                
                for (int i = 0; i < postAcceptCount; i++)
                {
                    PostAccept(listenerId);
                }

                LogManager.Default.Info($"开始监听: {endPoint}, 监听器ID: {listenerId}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"开始监听失败 {ipAddress}:{port}: {ex.Message}");
                return false;
            }
        }

        
        
        
        
        
        
        
        
        public bool Send(Socket socket, byte[] data, int offset, int count)
        {
            if (!_isRunning || socket == null || !socket.Connected)
                return false;

            try
            {
                var context = new IocpContext
                {
                    Socket = socket,
                    Operation = IocpOperation.Send,
                    Buffer = data,
                    Offset = offset,
                    BytesTransferred = 0
                };

                var args = _eventArgsPool.Get();
                args.UserToken = context;
                args.SetBuffer(data, offset, count);

                
                var handle = GCHandle.Alloc(context);
                _contexts.TryAdd(GCHandle.ToIntPtr(handle), context);

                if (!socket.SendAsync(args))
                {
                    
                    ProcessSend(args);
                }

                Interlocked.Add(ref _totalSendBytes, count);
                Interlocked.Increment(ref _totalSendPackets);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送数据失败: {ex.Message}");
                OnError?.Invoke(socket, ex);
                return false;
            }
        }

        
        
        
        
        public void Disconnect(Socket socket)
        {
            if (socket == null || !socket.Connected)
                return;

            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch { }
        }

        
        
        
        public void Update()
        {
            
            while (_disconnectQueue.TryDequeue(out var socket))
            {
                try
                {
                    OnDisconnection?.Invoke(socket);
                    Interlocked.Increment(ref _totalDisconnections);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"处理断开连接事件失败: {ex.Message}");
                }
            }

            
            while (_acceptQueue.TryDequeue(out var tempClient))
            {
                try
                {
                    if (tempClient.IsDeleteTimeout())
                    {
                        
                        tempClient.Socket?.Close();
                        continue;
                    }

                    if (tempClient.PreDeleted)
                    {
                        
                        _acceptQueue.Enqueue(tempClient);
                        continue;
                    }

                    
                    OnConnection?.Invoke(tempClient.Socket, tempClient.ListenerId);
                    Interlocked.Increment(ref _totalConnections);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"处理新连接事件失败: {ex.Message}");
                }
            }
        }

        
        
        
        public (long recvBytes, long sendBytes, long recvPackets, long sendPackets, long connections, long disconnections) GetStatistics()
        {
            return (_totalRecvBytes, _totalSendBytes, _totalRecvPackets, _totalSendPackets, _totalConnections, _totalDisconnections);
        }
        #endregion

        #region 私有方法
        
        
        
        private bool BindSocketToCompletionPort(Socket socket, uint completionKey)
        {
            var handle = socket.Handle;
            var result = CreateIoCompletionPort(handle, _completionPort, new UIntPtr(completionKey), 0);
            return result != IntPtr.Zero;
        }

        
        
        
        private void PostAccept(uint listenerId)
        {
            if (!_listeners.TryGetValue(listenerId, out var listener))
                return;

            try
            {
                var acceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var args = _eventArgsPool.Get();

                var context = new IocpContext
                {
                    Socket = acceptSocket,
                    Operation = IocpOperation.Accept,
                    UserToken = listenerId
                };

                args.UserToken = context;
                args.AcceptSocket = acceptSocket;

                
                var handle = GCHandle.Alloc(context);
                _contexts.TryAdd(GCHandle.ToIntPtr(handle), context);

                if (!listener.Socket.AcceptAsync(args))
                {
                    
                    ProcessAccept(args);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"投递Accept操作失败: {ex.Message}");
            }
        }

        
        
        
        private void PostReceive(Socket socket)
        {
            if (!socket.Connected) return;

            try
            {
                var bufferBlock = _zeroCopyBuffer.Rent();
                var args = _eventArgsPool.Get();

                var context = new IocpContext
                {
                    Socket = socket,
                    Operation = IocpOperation.Receive,
                    Buffer = bufferBlock.AsSpan().ToArray(), 
                    UserToken = bufferBlock 
                };

                args.UserToken = context;
                args.SetBuffer(bufferBlock.AsSpan().ToArray(), 0, bufferBlock.Size);

                
                var handle = GCHandle.Alloc(context);
                _contexts.TryAdd(GCHandle.ToIntPtr(handle), context);

                if (!socket.ReceiveAsync(args))
                {
                    
                    ProcessReceive(args);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"投递接收操作失败: {ex.Message}");
                OnError?.Invoke(socket, ex);
            }
        }

        
        
        
        private void OnIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    ProcessAccept(args);
                    break;
                case SocketAsyncOperation.Receive:
                    ProcessReceive(args);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(args);
                    break;
                case SocketAsyncOperation.Disconnect:
                    ProcessDisconnect(args);
                    break;
                default:
                    LogManager.Default.Warning($"未知的Socket操作: {args.LastOperation}");
                    break;
            }
        }

        
        
        
        private void ProcessAccept(SocketAsyncEventArgs args)
        {
            var context = args.UserToken as IocpContext;
            if (context == null) return;

            try
            {
                if (args.SocketError == SocketError.Success)
                {
                    var acceptSocket = args.AcceptSocket;
                    if (acceptSocket != null && acceptSocket.Connected)
                    {
                        
                        acceptSocket.NoDelay = true;
                        acceptSocket.ReceiveBufferSize = DEFAULT_BUFFER_SIZE;
                        acceptSocket.SendBufferSize = DEFAULT_BUFFER_SIZE;

                        
                        uint listenerId = (uint)context.UserToken;
                        if (BindSocketToCompletionPort(acceptSocket, listenerId))
                        {
                            
                            var tempClient = new TempClient
                            {
                                Socket = acceptSocket,
                                ListenerId = listenerId
                            };
                            _acceptQueue.Enqueue(tempClient);

                            
                            PostAccept(listenerId);

                            
                            PostReceive(acceptSocket);
                        }
                        else
                        {
                            acceptSocket.Close();
                        }
                    }
                }
                else
                {
                    LogManager.Default.Warning($"Accept失败: {args.SocketError}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理Accept完成失败: {ex.Message}");
            }
            finally
            {
                
                CleanupContext(context);
                _eventArgsPool.Return(args);
            }
        }

        
        
        
        private void ProcessReceive(SocketAsyncEventArgs args)
        {
            var context = args.UserToken as IocpContext;
            if (context == null) return;

            try
            {
                if (args.SocketError == SocketError.Success && args.BytesTransferred > 0)
                {
                    var socket = context.Socket;
                    var bufferBlock = context.UserToken as ZeroCopyBuffer.BufferBlock;

                    Interlocked.Add(ref _totalRecvBytes, args.BytesTransferred);
                    Interlocked.Increment(ref _totalRecvPackets);

                    
                    if (bufferBlock != null)
                    {
                        var memory = bufferBlock.AsMemory().Slice(0, args.BytesTransferred);
                        
                        
                        OnDataReceived?.Invoke(socket, memory.Span.ToArray(), args.BytesTransferred);
                        
                        
                        _zeroCopyBuffer.Return(bufferBlock);
                    }
                    else
                    {
                        
                        OnDataReceived?.Invoke(socket, context.Buffer, args.BytesTransferred);
                    }

                    
                    PostReceive(socket);
                }
                else if (args.SocketError != SocketError.Success || args.BytesTransferred == 0)
                {
                    
                    var socket = context.Socket;
                    _disconnectQueue.Enqueue(socket);
                    
                    
                    var bufferBlock = context.UserToken as ZeroCopyBuffer.BufferBlock;
                    if (bufferBlock != null)
                    {
                        _zeroCopyBuffer.Return(bufferBlock);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理接收完成失败: {ex.Message}");
                var socket = context.Socket;
                OnError?.Invoke(socket, ex);
                _disconnectQueue.Enqueue(socket);
                
                
                var bufferBlock = context.UserToken as ZeroCopyBuffer.BufferBlock;
                if (bufferBlock != null)
                {
                    _zeroCopyBuffer.Return(bufferBlock);
                }
            }
            finally
            {
                
                CleanupContext(context);
                _eventArgsPool.Return(args);
            }
        }

        
        
        
        private void ProcessSend(SocketAsyncEventArgs args)
        {
            var context = args.UserToken as IocpContext;
            if (context == null) return;

            try
            {
                if (args.SocketError != SocketError.Success)
                {
                    LogManager.Default.Warning($"发送失败: {args.SocketError}");
                    var socket = context.Socket;
                    _disconnectQueue.Enqueue(socket);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理发送完成失败: {ex.Message}");
            }
            finally
            {
                
                CleanupContext(context);
                _eventArgsPool.Return(args);
            }
        }

        
        
        
        private void ProcessDisconnect(SocketAsyncEventArgs args)
        {
            var context = args.UserToken as IocpContext;
            if (context == null) return;

            try
            {
                var socket = context.Socket;
                _disconnectQueue.Enqueue(socket);
            }
            finally
            {
                
                CleanupContext(context);
                _eventArgsPool.Return(args);
            }
        }

        
        
        
        private void WorkerThreadProc()
        {
            while (_isRunning)
            {
                try
                {
                    bool success = GetQueuedCompletionStatus(
                        _completionPort,
                        out uint bytesTransferred,
                        out UIntPtr completionKey,
                        out IntPtr overlapped,
                        1000); 

                    if (!success)
                    {
                        
                        continue;
                    }

                    if (overlapped == IntPtr.Zero && completionKey == UIntPtr.Zero && bytesTransferred == 0)
                    {
                        
                        break;
                    }

                    
                    if (_contexts.TryRemove(overlapped, out var context))
                    {
                        try
                        {
                            var handle = GCHandle.FromIntPtr(overlapped);
                            handle.Free();

                            
                            switch (context.Operation)
                            {
                                case IocpOperation.Accept:
                                    
                                    break;
                                case IocpOperation.Receive:
                                    
                                    break;
                                case IocpOperation.Send:
                                    
                                    break;
                                case IocpOperation.Disconnect:
                                    
                                    var socket = context.Socket;
                                    _disconnectQueue.Enqueue(socket);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Default.Error($"工作线程处理完成状态失败: {ex.Message}");
                        }
                        finally
                        {
                            context.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"工作线程异常: {ex.Message}");
                }
            }
        }

        
        
        
        private void CleanupContext(IocpContext context)
        {
            if (context == null) return;

            try
            {
                
                var handle = GCHandle.Alloc(context);
                var handlePtr = GCHandle.ToIntPtr(handle);
                _contexts.TryRemove(handlePtr, out _);
                handle.Free();

                context.Dispose();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"清理上下文资源失败: {ex.Message}");
            }
        }

        
        
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        
        
        
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            lock (_syncLock)
            {
                if (_isDisposed) return;

                Stop();

                if (disposing)
                {
                    
                    foreach (var context in _contexts.Values)
                    {
                        context.Dispose();
                    }
                    _contexts.Clear();

                    _eventArgsPool.Dispose();
                    _zeroCopyBuffer?.Dispose();
                }

                _isDisposed = true;
            }
        }
        #endregion
    }
}
