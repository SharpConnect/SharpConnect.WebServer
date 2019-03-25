//2015-2016, MIT, EngineKit

using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{

    public class WebServer
    {
        bool isRunning;
        ReqRespHandler<HttpRequest, HttpResponse> reqHandler;
        NewConnectionListener newConnListener; //listen to a new connection

        WebSocketServer webSocketServer;
        BufferManager bufferMan;
        SharedResoucePool<HttpContext> contextPool;
        public WebServer(int port, bool localOnly, ReqRespHandler<HttpRequest, HttpResponse> reqHandler)
        {
            this.reqHandler = reqHandler;

            int maxNumberOfConnections = 1000;
            int excessSaeaObjectsInPool = 200;
            int backlog = 100;
            int maxSimultaneousAcceptOps = 100;

            var setting = new NewConnListenerSettings(maxNumberOfConnections,
                   excessSaeaObjectsInPool,
                   backlog,
                   maxSimultaneousAcceptOps,
                   new IPEndPoint(localOnly ? IPAddress.Loopback : IPAddress.Any, port));//check only local host or not

            CreateContextPool(maxNumberOfConnections);
            newConnListener = new NewConnectionListener(setting,
                clientSocket =>
                {
                    //when accept new client

                    int recvBufferSize = 1024 * 2;
                    int sendBufferSize = 1024 * 2;

                    if (UseSsl)
                    {

                        HttpContext context = new HttpContext(this, recvBufferSize, sendBufferSize);
                        context.BindReqHandler(this.reqHandler); //client handler
#if DEBUG
                        context.dbugForHttps = true;
#endif

                        byte[] recvBuff = new byte[recvBufferSize];
                        byte[] sendBuffer = new byte[sendBufferSize];
                        IOBuffer recvIOBuffer = new IOBuffer(recvBuff, 0, recvBuff.Length);
                        IOBuffer sendIOBuffer = new IOBuffer(sendBuffer, 0, sendBuffer.Length);
                        var baseSockStream = new SockNetworkStream(recvIOBuffer, sendIOBuffer);
                        baseSockStream.Bind(clientSocket);
                        context.BindClient(baseSockStream); //*** bind to client socket                      
                        context.StartReceive(UseSsl ? _serverCert : null);
                    }
                    else
                    {

                        byte[] recvBuff = new byte[recvBufferSize];
                        byte[] sendBuffer = new byte[sendBufferSize];

                        IOBuffer recvIOBuffer = new IOBuffer(recvBuff, 0, recvBuff.Length);
                        IOBuffer sendIOBuffer = new IOBuffer(sendBuffer, 0, sendBuffer.Length);
                        var baseSockStream = new SockNetworkStream(recvIOBuffer, sendIOBuffer);
                        baseSockStream.Bind(clientSocket);

                        HttpContext context = this.contextPool.Pop();
                        context.BindClient(baseSockStream); //*** bind to client socket                      
                        context.StartReceive(UseSsl ? _serverCert : null);
                    }



                });
        }

        public bool UseSsl { get; set; }
        System.Security.Cryptography.X509Certificates.X509Certificate2 _serverCert;
        public void LoadCertificate(string certFile, string psw)
        {
            _serverCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certFile, psw);
        }

        void CreateContextPool(int maxNumberOfConnnections)
        {
            int recvSize = 1024 * 2;
            int sendSize = 1024 * 2;
            bufferMan = new BufferManager((recvSize + sendSize) * maxNumberOfConnnections, (recvSize + sendSize));
            //Allocate memory for buffers. We are using a separate buffer space for
            //receive and send, instead of sharing the buffer space, like the Microsoft
            //example does.    
            this.contextPool = new SharedResoucePool<HttpContext>(maxNumberOfConnnections);
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
                context.CreatedFromPool = true;
                context.BindReqHandler(this.reqHandler); //client handler

                this.contextPool.Push(context);
            }
        }

        internal void SetBufferFor(SocketAsyncEventArgs e)
        {
            this.bufferMan.SetBufferFor(e);
        }
        internal void ReleaseChildConn(HttpContext httpContext)
        {
            if (httpContext != null)
            {

                httpContext.Reset();
                if (httpContext.CreatedFromPool)
                {
                    this.contextPool.Push(httpContext);
                }
                newConnListener.NotifyFreeAcceptQuota();
            }
        }
        public void Start()
        {
            if (isRunning) return;
            //------------------------------
            try
            {
                //start web server   
                isRunning = true;
                newConnListener.StartListening();
            }
            catch (Exception ex)
            {
            }
        }
        public void Stop()
        {
            newConnListener.DisposePool();
            while (this.contextPool.Count > 0)
            {
                contextPool.Pop().Dispose();
            }
        }

        //--------------------------------------------------
        public WebSocketServer WebSocketServer
        {
            get { return webSocketServer; }
            set
            {
                webSocketServer = value;
            }
        }
        public bool EnableWebSocket
        {
            get { return webSocketServer != null; }
        }

        internal bool CheckWebSocketUpgradeRequest(HttpContext httpConn)
        {
            if (webSocketServer == null)
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
                //Socket clientSocket = httpConn.RemoteSocket;
                var clientStream = httpConn.AbstractClientNetworkStream;
                //backup data before unbind socket
                string webSocketInitUrl = httpReq.Url;
                //--------------------  
                httpConn.UnBindSocket(false);//unbind  but not close client socket  
                                             //--------------------
                webSocketServer.RegisterNewWebSocket(
                    clientStream,
                    webSocketInitUrl,
                    sec_websocket_key);//the bind client to websocket server                 
                return true;
            }
            return false;
        }
    }

}