//2015-2016, MIT, EngineKit
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

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
        public bool EnableWebSocket
        {
            get { return WebSocketServer != null; }
        }

        //internal bool CheckWebSocketUpgradeRequest(HttpContext httpConn)
        //{
        //    if (webSocketServer == null)
        //    {
        //        return false;
        //    }

        //    HttpRequest httpReq = httpConn.HttpReq;
        //    HttpResponse httpResp = httpConn.HttpResp;
        //    string upgradeKey = httpReq.GetHeaderKey("Upgrade");
        //    if (upgradeKey != null && upgradeKey == "websocket")
        //    {
        //        //1. websocket request come here first                
        //        //2. web server can design what web socket server will handle this request, based on httpCon url

        //        string sec_websocket_key = httpReq.GetHeaderKey("Sec-WebSocket-Key");
        //        Socket clientSocket = httpConn.RemoteSocket;
        //        //backup data before unbind socket
        //        string webSocketInitUrl = httpReq.Url;
        //        //--------------------  
        //        httpConn.UnBindSocket(false);//unbind  but not close client socket  
        //                                     //--------------------
        //        webSocketServer.RegisterNewWebSocket(clientSocket, webSocketInitUrl, sec_websocket_key);//the bind client to websocket server                 
        //        return true;
        //    }
        //    return false;
        //}
    }
}