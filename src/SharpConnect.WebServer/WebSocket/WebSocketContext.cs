//MIT, 2019, EngineKit


namespace SharpConnect.WebServers
{
    public class WebSocketContext
    {
        internal bool _usePlain;
        internal PlainWebSocketContext _plainContext;
        internal SecureWebSocketContext _secureContext;

        internal WebSocketContext(PlainWebSocketContext plainContext)
        {
            _plainContext = plainContext;
            _usePlain = true;
        }
        internal WebSocketContext(SecureWebSocketContext secureContext)
        {
            _secureContext = secureContext;
        }
        public void Send(string str)
        {
            if (_usePlain)
            {
                _plainContext.Send(str);
            }
            else
            {
                _secureContext.Send(str);
            }
        }
        public void SetMessageHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            if (_usePlain)
            {
                _plainContext.SetMessageHandler(webSocketReqHandler);
            }
            else
            {
                _secureContext.SetMessageHandler(webSocketReqHandler);
            }
        }
    }
}