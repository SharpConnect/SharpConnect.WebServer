//MIT, 2019, EngineKit


namespace SharpConnect.WebServers
{
    public enum WebSocketContentCompression
    {
        NoCompression,
        Deflate,
        Gzip,
    }
    //
    public class WebSocketContext
    {

        readonly WebSocketConnectionBase _webSocketConn;
        internal WebSocketContext(WebSocketConnectionBase webSocketConn)
        {
            _webSocketConn = webSocketConn;
        }
        public string InitClientRequestUrl { get; set; }
        public int ConnectionId => _webSocketConn.ConnectionId;
        public void Send(string str)
        {
            _webSocketConn.Send(str);
        }
        public void SendAsBinaryData(byte[] data, int start, int len)
        {
            _webSocketConn.SendBinaryData(data, start, len);
        }
        public void SetMessageHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            _webSocketConn.SetMessageHandler(webSocketReqHandler);
        }
        public WebSocketContentCompression Compression
        {
            get => _webSocketConn.Compression;
            set => _webSocketConn.Compression = value;
        }
    }
}