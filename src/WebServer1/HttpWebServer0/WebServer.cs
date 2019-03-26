//2015-2016, MIT, EngineKit


namespace SharpConnect.WebServers
{
    public class WebServer
    {
        SharpConnect.WebServers.HttpWebServer _server1;
        SharpConnect.WebServers.Server2.HttpsWebServer _server2;

        int _port;
        bool _localOnly;
        ReqRespHandler<HttpRequest, HttpResponse> _reqHandler;
        string _certFile;
        string _certPsw;
        bool _isRunning;

        public WebServer(int port, bool localOnly, ReqRespHandler<HttpRequest, HttpResponse> reqHandler)
        {
            _port = port;
            _localOnly = localOnly;
            _reqHandler = reqHandler;
        }
        public void LoadCertificate(string certFile, string psw)
        {
            _certFile = certFile;
            _certPsw = psw;
        }

        /// <summary>
        /// use https
        /// </summary>
        public bool UseSsl { get; set; }
        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;

            if (UseSsl)
            {
                _server2 = new Server2.HttpsWebServer(_port, _localOnly, _reqHandler);
                _server2.LoadCertificate(_certFile, _certPsw);
                _server2.UseSsl = true;
                _server2.WebSocketServer = WebSocketServer;
                _server2.Start();
            }
            else
            {
                _server1 = new HttpWebServer(_port, _localOnly, _reqHandler);
                _server1.WebSocketServer = WebSocketServer;
                _server1.Start();

            }
        }
        public void Stop()
        {
            if (_server1 != null)
            {
                _server1.Stop();
            }
            else if (_server2 != null)
            {
                _server2.Stop();
            }
        }

        //--------------------------------------------------
        public WebSocketServer WebSocketServer { get; set; }
        public bool EnableWebSocket => WebSocketServer != null;
    }
}