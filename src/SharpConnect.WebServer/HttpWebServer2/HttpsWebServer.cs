﻿//MIT, 2015-present, EngineKit

using System;
using System.Net;
using System.Net.Sockets;

namespace SharpConnect.WebServers
{

    class HttpsWebServer : IHttpServer
    {
        bool _isRunning;
        ReqRespHandler<HttpRequest, HttpResponse> _reqHandler;
        NewConnectionListener _newConnListener; //listen to a new connection
        BufferManager _bufferMan;
        SharedResoucePool<HttpsContext> _contextPool;


        bool _localOnly;
        int _port;
        public HttpsWebServer(int port, bool localOnly, ReqRespHandler<HttpRequest, HttpResponse> reqHandler)
        {
            _port = port;
            _localOnly = localOnly;
            _reqHandler = reqHandler; 
        }

        public LargeFileUploadPermissionReqHandler LargeFileUploadPermissionReqHandler { get; set; }
        public int ListeningOnPort => _newConnListener.ListeningOnPort;

        System.Security.Cryptography.X509Certificates.X509Certificate2 _serverCert;
        public void LoadCertificate(string certFile, string psw)
        {
            _serverCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certFile, psw);
        }

        void CreateContextPool(int maxNumberOfConnnections)
        {
            int recvSize = 1024 * 2;
            int sendSize = 1024 * 2;
            _bufferMan = new BufferManager((recvSize + sendSize) * maxNumberOfConnnections, (recvSize + sendSize));
            //Allocate memory for buffers. We are using a separate buffer space for
            //receive and send, instead of sharing the buffer space, like the Microsoft
            //example does.    
            _contextPool = new SharedResoucePool<HttpsContext>(maxNumberOfConnnections);
            //------------------------------------------------------------------
            //It is NOT mandatory that you preallocate them or reuse them. But, but it is 
            //done this way to illustrate how the API can 
            // easily be used to create ***reusable*** objects to increase server performance. 
            //------------------------------------------------------------------
            //connection session: socket async = 1:1 
            for (int i = maxNumberOfConnnections - 1; i >= 0; --i)
            {
                var context = new HttpsContext(this,
                    recvSize,
                    sendSize);
                context.CreatedFromPool = true;
                context.BindReqHandler(_reqHandler); //client handler

                _contextPool.Push(context);
            }
        }

        internal void SetBufferFor(SocketAsyncEventArgs e)
        {
            _bufferMan.SetBufferFor(e);
        }
        internal void ReleaseChildConn(HttpsContext httpContext)
        {
            if (httpContext != null)
            {

                httpContext.Reset();
                if (httpContext.CreatedFromPool)
                {
                    _contextPool.Push(httpContext);
                }
                _newConnListener.NotifyFreeAcceptQuota();
            }
        }
        public void Start()
        {
            if (_isRunning) return;
            //------------------------------
            try
            {

                //------------------------------
                int maxNumberOfConnections = 500;
                int excessSaeaObjectsInPool = 200;
                int backlog = 100;
                int maxSimultaneousAcceptOps = 100;

                var setting = new NewConnListenerSettings(maxNumberOfConnections,
                       excessSaeaObjectsInPool,
                       backlog,
                       maxSimultaneousAcceptOps,
                       new IPEndPoint(_localOnly ? IPAddress.Loopback : IPAddress.Any, _port));//check only local host or not

                CreateContextPool(maxNumberOfConnections);
                _newConnListener = new NewConnectionListener(setting,
                    clientSocket =>
                    {
                    //when accept new client

                    int recvSize = 1024 * 2;
                        int sendSize = 1024 * 2;
                        HttpsContext context = new HttpsContext(this, recvSize, sendSize);
                        context.BindReqHandler(_reqHandler); //client handler
#if DEBUG
                    context.dbugForHttps = true;
#endif


                    context.BindSocket(clientSocket); //*** bind to client socket                      
                                                      //for ssl -> cert must not be null
                    context.StartReceive(_serverCert);
                    //TODO::
                    //USE https context from Pool????
                    //{
                    //    HttpsContext context = _contextPool.Pop();
                    //    context.BindSocket(clientSocket); //*** bind to client socket                      
                    //    context.StartReceive(UseSsl ? _serverCert : null);
                    //}
                });
                //------------------------------


                //start web server   
                _isRunning = true;
                _newConnListener.StartListening();
            }
            catch (Exception ex)
            {
            }
        }
        public void Stop()
        {
            _newConnListener.DisposePool();
            while (_contextPool.Count > 0)
            {
                _contextPool.Pop().Dispose();
            }
        }

        //--------------------------------------------------
        public WebSocketServer WebSocketServer { get; set; }

        public bool EnableWebSocket => WebSocketServer != null;

        internal bool CheckWebSocketUpgradeRequest(HttpsContext httpConn)
        {
            if (WebSocketServer == null)
            {
                return false;
            }

            HttpRequest httpReq = httpConn.HttpReq;
            HttpResponse httpResp = httpConn.HttpResp;
            string upgradeKey = httpReq.GetHeaderKey("Upgrade");
            if (upgradeKey != null && upgradeKey == "websocket")
            {
                //1. websocket request come here first                
                //2. web server can design what web socket server will handle this request, based on httpCon url

                string sec_websocket_key = httpReq.GetHeaderKey("Sec-WebSocket-Key");
                string sec_websocket_extensions = httpReq.GetHeaderKey("Sec-WebSocket-Extensions");
                Internal2.AbstractAsyncNetworkStream baseStream = httpConn.BaseStream;

#if DEBUG
                baseStream.BeginWebsocketMode = true;
#endif
                //backup data before unbind socket
                string webSocketInitPath = httpReq.Path;
                //--------------------  
                httpConn.UnBindSocket(false);//unbind  but not close client socket  
                                             //--------------------
                WebSocketServer.RegisterNewWebSocket(baseStream, webSocketInitPath, sec_websocket_key, sec_websocket_extensions);//the bind client to websocket server                 
                return true;
            }
            return false;
        }
    }

}