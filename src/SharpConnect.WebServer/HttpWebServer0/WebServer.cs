//MIT, 2015-present, EngineKit


namespace SharpConnect.WebServers
{
    interface IHttpServer
    {
        void Start();
        void Stop();
        WebServers.WebSocketServer WebSocketServer { get; }
    }


    public class WebServer : IHttpServer
    {
        IHttpServer _server;

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
        public LargeFileUploadPermissionReqHandler LargeFileUploadPermissionReqHandler { get; set; }

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
                var httpsServer = new HttpsWebServer(_port, _localOnly, _reqHandler);
                httpsServer.LoadCertificate(_certFile, _certPsw);
                httpsServer.WebSocketServer = WebSocketServer;
                httpsServer.LargeFileUploadPermissionReqHandler = this.LargeFileUploadPermissionReqHandler;
                httpsServer.Start();

                _server = httpsServer;
            }
            else
            {
                var httpServer = new HttpWebServer(_port, _localOnly, _reqHandler);
                httpServer.WebSocketServer = WebSocketServer;
                httpServer.LargeFileUploadPermissionReqHandler = this.LargeFileUploadPermissionReqHandler;
                httpServer.Start();
                _server = httpServer;
            }
        }
        public void Stop()
        {
            _server.Stop();
        }
        //--------------------------------------------------
        public WebSocketServer WebSocketServer { get; set; }
        public bool EnableWebSocket => WebSocketServer != null;
    }
}