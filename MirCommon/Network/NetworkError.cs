using System;
using System.Net.Sockets;

namespace MirCommon.Network
{
    
    
    
    public enum NetworkErrorCode
    {
        
        
        
        ME_OK = 0,
        
        
        
        
        ME_FAIL = -1,
        
        
        
        
        ME_SOCKETWOULDBLOCK = -2,
        
        
        
        
        ME_SOCKETCLOSED = -3,
        
        
        
        
        ME_CONNECTIONREFUSED = -4,
        
        
        
        
        ME_CONNECTIONTIMEOUT = -5,
        
        
        
        
        ME_NETWORKUNREACHABLE = -6,
        
        
        
        
        ME_HOSTUNREACHABLE = -7,
        
        
        
        
        ME_CONNECTIONRESET = -8,
        
        
        
        
        ME_CONNECTIONABORTED = -9,
        
        
        
        
        ME_BUFFEROVERFLOW = -10,
        
        
        
        
        ME_INVALIDPARAMETER = -11,
        
        
        
        
        ME_OUTOFMEMORY = -12,
        
        
        
        
        ME_UNKNOWN = -999
    }

    
    
    
    public class NetworkError
    {
        private NetworkErrorCode _errorCode;
        private string _errorMessage;

        public NetworkError()
        {
            _errorCode = NetworkErrorCode.ME_OK;
            _errorMessage = string.Empty;
        }

        
        
        
        public void SetError(NetworkErrorCode errorCode, string format, params object[] args)
        {
            _errorCode = errorCode;
            _errorMessage = string.Format(format, args);
        }

        
        
        
        public void SetError(NetworkError error)
        {
            if (error != null)
            {
                _errorCode = error._errorCode;
                _errorMessage = error._errorMessage;
            }
        }

        
        
        
        public void SetErrorFromSocketException(SocketException ex)
        {
            _errorCode = ConvertSocketErrorToNetworkError(ex.SocketErrorCode);
            _errorMessage = ex.Message;
        }

        
        
        
        public void SetErrorFromException(Exception ex)
        {
            if (ex is SocketException socketEx)
            {
                SetErrorFromSocketException(socketEx);
            }
            else
            {
                _errorCode = NetworkErrorCode.ME_UNKNOWN;
                _errorMessage = ex.Message;
            }
        }

        
        
        
        public NetworkErrorCode GetErrorCode() => _errorCode;

        
        
        
        public string GetErrorMessage() => _errorMessage;

        
        
        
        public void Clear()
        {
            _errorCode = NetworkErrorCode.ME_OK;
            _errorMessage = string.Empty;
        }

        
        
        
        public bool IsSuccess() => _errorCode == NetworkErrorCode.ME_OK;

        
        
        
        public bool IsFailure() => _errorCode != NetworkErrorCode.ME_OK;

        
        
        
        public static NetworkErrorCode ConvertSocketErrorToNetworkError(SocketError socketError)
        {
            return socketError switch
            {
                SocketError.Success => NetworkErrorCode.ME_OK,
                SocketError.WouldBlock => NetworkErrorCode.ME_SOCKETWOULDBLOCK,
                SocketError.ConnectionRefused => NetworkErrorCode.ME_CONNECTIONREFUSED,
                SocketError.TimedOut => NetworkErrorCode.ME_CONNECTIONTIMEOUT,
                SocketError.NetworkUnreachable => NetworkErrorCode.ME_NETWORKUNREACHABLE,
                SocketError.HostUnreachable => NetworkErrorCode.ME_HOSTUNREACHABLE,
                SocketError.ConnectionReset => NetworkErrorCode.ME_CONNECTIONRESET,
                SocketError.ConnectionAborted => NetworkErrorCode.ME_CONNECTIONABORTED,
                SocketError.NoBufferSpaceAvailable => NetworkErrorCode.ME_BUFFEROVERFLOW,
                SocketError.InvalidArgument => NetworkErrorCode.ME_INVALIDPARAMETER,
                
                
                _ when (int)socketError == 10012 => NetworkErrorCode.ME_OUTOFMEMORY, 
                _ => NetworkErrorCode.ME_UNKNOWN
            };
        }

        
        
        
        public static NetworkErrorCode ConvertWinSockErrorToNetworkError(int winSockError)
        {
            return winSockError switch
            {
                0 => NetworkErrorCode.ME_OK,
                (int)SocketError.WouldBlock => NetworkErrorCode.ME_SOCKETWOULDBLOCK,
                (int)SocketError.ConnectionRefused => NetworkErrorCode.ME_CONNECTIONREFUSED,
                (int)SocketError.TimedOut => NetworkErrorCode.ME_CONNECTIONTIMEOUT,
                (int)SocketError.NetworkUnreachable => NetworkErrorCode.ME_NETWORKUNREACHABLE,
                (int)SocketError.HostUnreachable => NetworkErrorCode.ME_HOSTUNREACHABLE,
                (int)SocketError.ConnectionReset => NetworkErrorCode.ME_CONNECTIONRESET,
                (int)SocketError.ConnectionAborted => NetworkErrorCode.ME_CONNECTIONABORTED,
                (int)SocketError.NoBufferSpaceAvailable => NetworkErrorCode.ME_BUFFEROVERFLOW,
                (int)SocketError.InvalidArgument => NetworkErrorCode.ME_INVALIDPARAMETER,
                10012 => NetworkErrorCode.ME_OUTOFMEMORY, 
                _ => NetworkErrorCode.ME_UNKNOWN
            };
        }

        
        
        
        public static string GetErrorCodeString(NetworkErrorCode errorCode)
        {
            return errorCode switch
            {
                NetworkErrorCode.ME_OK => "ME_OK",
                NetworkErrorCode.ME_FAIL => "ME_FAIL",
                NetworkErrorCode.ME_SOCKETWOULDBLOCK => "ME_SOCKETWOULDBLOCK",
                NetworkErrorCode.ME_SOCKETCLOSED => "ME_SOCKETCLOSED",
                NetworkErrorCode.ME_CONNECTIONREFUSED => "ME_CONNECTIONREFUSED",
                NetworkErrorCode.ME_CONNECTIONTIMEOUT => "ME_CONNECTIONTIMEOUT",
                NetworkErrorCode.ME_NETWORKUNREACHABLE => "ME_NETWORKUNREACHABLE",
                NetworkErrorCode.ME_HOSTUNREACHABLE => "ME_HOSTUNREACHABLE",
                NetworkErrorCode.ME_CONNECTIONRESET => "ME_CONNECTIONRESET",
                NetworkErrorCode.ME_CONNECTIONABORTED => "ME_CONNECTIONABORTED",
                NetworkErrorCode.ME_BUFFEROVERFLOW => "ME_BUFFEROVERFLOW",
                NetworkErrorCode.ME_INVALIDPARAMETER => "ME_INVALIDPARAMETER",
                NetworkErrorCode.ME_OUTOFMEMORY => "ME_OUTOFMEMORY",
                NetworkErrorCode.ME_UNKNOWN => "ME_UNKNOWN",
                _ => $"UNKNOWN_ERROR({(int)errorCode})"
            };
        }

        
        
        
        public string GetFullErrorMessage()
        {
            if (string.IsNullOrEmpty(_errorMessage))
                return GetErrorCodeString(_errorCode);
            
            return $"{GetErrorCodeString(_errorCode)}: {_errorMessage}";
        }

        
        
        
        public override string ToString()
        {
            return GetFullErrorMessage();
        }
    }

    
    
    
    public class NetworkResult
    {
        public NetworkErrorCode ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public int BytesTransferred { get; set; }
        public bool IsSuccess => ErrorCode == NetworkErrorCode.ME_OK;

        public NetworkResult()
        {
            ErrorCode = NetworkErrorCode.ME_OK;
            ErrorMessage = string.Empty;
            BytesTransferred = 0;
        }

        public NetworkResult(NetworkErrorCode errorCode, string errorMessage = "", int bytesTransferred = 0)
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            BytesTransferred = bytesTransferred;
        }

        public static NetworkResult Success(int bytesTransferred = 0)
        {
            return new NetworkResult(NetworkErrorCode.ME_OK, "", bytesTransferred);
        }

        public static NetworkResult Failure(NetworkErrorCode errorCode, string errorMessage = "")
        {
            return new NetworkResult(errorCode, errorMessage);
        }

        public static NetworkResult FromException(Exception ex)
        {
            if (ex is SocketException socketEx)
            {
                var errorCode = NetworkError.ConvertSocketErrorToNetworkError(socketEx.SocketErrorCode);
                return new NetworkResult(errorCode, socketEx.Message);
            }
            else
            {
                return new NetworkResult(NetworkErrorCode.ME_UNKNOWN, ex.Message);
            }
        }
    }
}
