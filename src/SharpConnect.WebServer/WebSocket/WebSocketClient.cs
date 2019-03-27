//MIT, 2018-present, EngineKit and contributors

namespace SharpConnect.WebServers
{

    public class WebSocketClient
    {
        PlainWebSocketClient _plainWebSocketClient;
        SecureWebSocketClient _secureWebSocketClient;
        ReqRespHandler<WebSocketRequest, WebSocketResponse> _websocketHandler;
        System.Security.Cryptography.X509Certificates.X509Certificate2 _serverCert;

        public WebSocketClient()
        {

        }
        public bool UseSsl { get; set; }
        public void SetHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> websocketHandler)
        {
            _websocketHandler = websocketHandler;
        }

        public void LoadCertificate(string certFile, string psw)
        {
            _serverCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certFile, psw);
        }

        public void Connect(string url)
        {
            if (UseSsl)
            {
                _secureWebSocketClient = new SecureWebSocketClient();
                _secureWebSocketClient.SetHandler(_websocketHandler);
                _secureWebSocketClient.Connect(url, _serverCert);
            }
            else
            {
                _plainWebSocketClient = new PlainWebSocketClient();
                _plainWebSocketClient.SetHandler(_websocketHandler);
                _plainWebSocketClient.Connect(url);
            }
        }
        public void SendData(string data)
        {
            if (UseSsl)
            {
                _secureWebSocketClient.SendData(data);
            }
            else
            {
                _plainWebSocketClient.SendData(data);
            }

        }
    }



}
