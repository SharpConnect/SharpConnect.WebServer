//MIT, 2019, EngineKit
using System;
using System.Net.Sockets;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{
    public class WebSocketContext
    {
        internal bool _usePlain;
        internal PlainWebSocketContext _plainContext;
        internal SharpConnect.WebServers.Server2.SecureWebSocketContext _secureContext;

        internal WebSocketContext(PlainWebSocketContext plainContext)
        {
            _plainContext = plainContext;
            _usePlain = true;
        }
        internal WebSocketContext(SharpConnect.WebServers.Server2.SecureWebSocketContext secureContext)
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