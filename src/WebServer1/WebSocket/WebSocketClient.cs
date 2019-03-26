//MIT, 2018-present, EngineKit and contributors

namespace SharpConnect.WebServers
{

    public class WebSocketClient
    {
        PlainWebSocketClient _plainWebSocketClient;
        SharpConnect.WebServers.Server2.SecureWebSocketClient _secureWebSocketClient;

        ReqRespHandler<WebSocketRequest, WebSocketResponse> _websocketHandler;

        public WebSocketClient()
        {

        }
        public bool UseSsl { get; set; }
        public void SetHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> websocketHandler)
        {
            _websocketHandler = websocketHandler;
        }
        public void Connect(string url)
        {
            if (UseSsl)
            {
                _secureWebSocketClient = new Server2.SecureWebSocketClient();
                _secureWebSocketClient.SetHandler(_websocketHandler);
            }
            else
            {
                _plainWebSocketClient = new PlainWebSocketClient();
                _plainWebSocketClient.SetHandler(_websocketHandler);
            }
        }
        public void SendData(string data)
        {
            _plainWebSocketClient.SendData(data);
        }
    }



}
