﻿//MIT, 2015-present, EngineKit

using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace SharpConnect.WebServers
{

    class HttpWebServer : IHttpServer
    {
        bool _isRunning;
        ReqRespHandler<HttpRequest, HttpResponse> _reqHandler;
        NewConnectionListener _newConnListener; //listen to a new connection

        WebSocketServer _webSocketServer;
        BufferManager _bufferMan;
        SharedResoucePool<HttpContext> _contextPool;

        int _port;
        bool _localOnly;
        public HttpWebServer(
            int port,
            bool localOnly,
            ReqRespHandler<HttpRequest, HttpResponse> reqHandler)
        {
            _port = port;
            _localOnly = localOnly;
            _reqHandler = reqHandler;

        }

        public LargeFileUploadPermissionReqHandler LargeFileUploadPermissionReqHandler { get; set; }

        void CreateContextPool(int maxNumberOfConnnections)
        {
            int recvSize = 1024;
            int sendSize = 1024;
            _bufferMan = new BufferManager((recvSize + sendSize) * maxNumberOfConnnections, (recvSize + sendSize));
            //Allocate memory for buffers. We are using a separate buffer space for
            //receive and send, instead of sharing the buffer space, like the Microsoft
            //example does.    
            _contextPool = new SharedResoucePool<HttpContext>(maxNumberOfConnnections);
            //------------------------------------------------------------------
            //It is NOT mandatory that you preallocate them or reuse them. But, but it is 
            //done this way to illustrate how the API can 
            // easily be used to create ***reusable*** objects to increase server performance. 
            //------------------------------------------------------------------
            //connection session: socket async = 1:1 
            for (int i = maxNumberOfConnnections - 1; i >= 0; --i)
            {
                var context = new HttpContext(this,
                    recvSize,
                   sendSize);

                context.BindReqHandler(_reqHandler); //client handler

                _contextPool.Push(context);
            }
        }

        internal void SetBufferFor(SocketAsyncEventArgs e)
        {
            _bufferMan.SetBufferFor(e);
        }
        internal void ReleaseChildConn(HttpContext httpConn)
        {
            if (httpConn != null)
            {
                httpConn.Reset();
                _contextPool.Push(httpConn);
                _newConnListener.NotifyFreeAcceptQuota();
            }
        }
        public void Start()
        {
            if (_isRunning) return;
            //------------------------------
            try
            {

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
                        HttpContext context = _contextPool.Pop();
                        context.BindSocket(clientSocket); //*** bind to client socket 
                        context.StartReceive(); //start receive data
                    }); 
                //start web server   
                _isRunning = true;
                _newConnListener.StartListening();
            }
            catch (Exception ex)
            {
            }
        }
        public int ListeningOnPort => _newConnListener.ListeningOnPort;
        public void Stop()
        {
            _newConnListener.DisposePool();
            while (_contextPool.Count > 0)
            {
                _contextPool.Pop().Dispose();
            }
        }

        //--------------------------------------------------
        public WebSocketServer WebSocketServer
        {
            get => _webSocketServer;
            set => _webSocketServer = value;
        }
        public bool EnableWebSocket => _webSocketServer != null;

        internal bool CheckWebSocketUpgradeRequest(HttpContext httpConn)
        {
            if (_webSocketServer == null)
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
                Socket clientSocket = httpConn.RemoteSocket;
                //backup data before unbind socket
                string webSocketInitUrl = httpReq.Path;
                //--------------------  
                httpConn.UnBindSocket(false);//unbind  but not close client socket  
                                             //--------------------
                _webSocketServer.RegisterNewWebSocket(clientSocket, webSocketInitUrl, sec_websocket_key, sec_websocket_extensions);//the bind client to websocket server                 
                return true;
            }
            return false;
        }
    }

}