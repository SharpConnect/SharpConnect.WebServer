//MIT, 2019, EngineKit


namespace SharpConnect.WebServers
{
    public class WebSocketContext
    {
        readonly WebSocketConnectionBase _webSocketConn;
        internal WebSocketContext(WebSocketConnectionBase webSocketConn)
        {
            _webSocketConn = webSocketConn;
        }
        public int ConnectionId => _webSocketConn.ConnectionId;
        public void Send(string str)
        {
            _webSocketConn.Send(str);
        }
        public void SetMessageHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            _webSocketConn.SetMessageHandler(webSocketReqHandler);
        }
    }
}