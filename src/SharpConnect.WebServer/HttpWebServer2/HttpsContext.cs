//MIT, 2015-present, EngineKit 
/*
 * ServerState.cs
 *
 * The MIT License
 *
 * Copyright (c) 2013-2014 sta.blockhead
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Net;
using System.Net.Sockets;
using SharpConnect.Internal2;
namespace SharpConnect.WebServers
{

    /// <summary>
    /// http connection session, req-resp model
    /// </summary>
    class HttpsContext : IHttpContext, ISendIO
    {
        HttpRequestImpl _httpReq;
        HttpResponse _httpResp;
        ReqRespHandler<HttpRequest, HttpResponse> _reqHandler;
        HttpsWebServer _ownerServer;

        LowLevelNetworkStream _baseSockStream;
        AbstractAsyncNetworkStream _sockStream;

        Socket _clientSocket;
        public EndPoint RemoteEndPoint => RemoteSocket.RemoteEndPoint;

#if DEBUG
        static int dbugTotalId;
        public readonly int dbugId = dbugTotalId++;

        bool _dbugForHttps;
        public bool dbugForHttps
        {
            get => _dbugForHttps;
            set
            {
                _dbugForHttps = value;
            }
        }

#endif

        public HttpsContext(
            HttpsWebServer ownerServer,
            int recvBufferSize,
            int sendBufferSize)
        {
            //we create http context with default IO buffer 
            this.EnableWebSocket = true;
            _ownerServer = ownerServer;

            byte[] recvBuff = new byte[recvBufferSize];
            byte[] sendBuffer = new byte[sendBufferSize];

            IOBuffer recvIOBuffer = new IOBuffer(recvBuff, 0, recvBuff.Length);
            IOBuffer sendIOBuffer = new IOBuffer(sendBuffer, 0, sendBuffer.Length);
            _baseSockStream = new LowLevelNetworkStream(recvIOBuffer, sendIOBuffer);
            //
            //
            //each recvSendArgs is created for this connection session only *** 
            //KeepAlive = false;

            //set buffer for newly created saArgs
            //ownerServer.SetBufferFor(this.recvSendArgs = new SocketAsyncEventArgs()); 
            //----------------------------------------------------------------------------------------------------------  
            _httpReq = new HttpRequestImpl(this);
            _httpReq.SetLargeUploadFilePolicyHandler(_ownerServer.LargeFileUploadPermissionReqHandler);

            _httpResp = new HttpResponseImpl(this);
        }


        internal AbstractAsyncNetworkStream BaseStream => _sockStream;

        internal bool CreatedFromPool { get; set; }

        public void EnqueueSendingData(byte[] buffer, int len) => _sockStream.EnqueueSendData(buffer, len);

        public int RecvByteTransfer => _sockStream.ByteReadTransfered;
        public byte ReadByte(int pos) => _sockStream.RecvReadByte(pos);
        public void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen) => _sockStream.RecvCopyTo(readpos, dstBuffer, copyLen);

        public void SendIOStartSend() => _sockStream.StartSend();


        public bool EnableWebSocket { get; set; }
        public bool KeepAlive { get; set; }

        internal HttpRequest HttpReq => _httpReq;

        internal HttpResponse HttpResp => _httpResp;

        internal Socket RemoteSocket => _clientSocket;

        /// <summary>
        /// bind to client socket
        /// </summary>
        /// <param name="clientSocket"></param>
        internal void BindSocket(Socket clientSocket)
        {
            _clientSocket = clientSocket;
            //bind socket to the base stream
            _baseSockStream.Bind(clientSocket);
        }
        internal void BindReqHandler(ReqRespHandler<HttpRequest, HttpResponse> reqHandler)
        {
            _reqHandler = reqHandler;
        }
        internal void UnBindSocket(bool closeClientSocket)
        {
            //cut connection from current socket
            _baseSockStream.UnbindSocket();
            //
            if (closeClientSocket)
            {
                try
                {
                    _clientSocket.Shutdown(SocketShutdown.Both);
                }
                // throws if socket was already closed
                catch (Exception)
                {
                    // dbugSendLog(connSession, "CloseClientSocket, Shutdown catch");
                }
                _clientSocket.Close();
            }

            //TODO: 
            //this.recvSendArgs.AcceptSocket = null;
            Reset();//reset 
            _ownerServer.ReleaseChildConn(this);
            _isFirstTime = true;
        }
        void StartReceive()
        {
#if DEBUG
            int debugId = this.dbugId;
#endif
            _sockStream.StartReceive();
        }

        bool _isFirstTime = true;
        /// <summary>
        /// start authen and receive
        /// </summary>
        /// <param name="cert"></param>
        internal void StartReceive(System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
        {
            if (cert == null)
            {
                //if no cert then just start recv                
                //JustBypassSocketNetworkStream bypass = new JustBypassSocketNetworkStream(_baseSockStream, cert);
                _sockStream = _baseSockStream;


                ////-----------------------------
                //recvIO.Bind(_sockStream);
                //sendIO.Bind(_sockStream);
                ////-----------------------------
                if (_isFirstTime)
                {
                    _isFirstTime = false;
                    _sockStream.SetRecvCompleteEventHandler((r, byteCount) =>
                    {
                        if (byteCount == 0)
                        {
                            HandleReceive(RecvEventCode.NoMoreReceiveData);
                        }
                        else
                        {
                            HandleReceive(RecvEventCode.HasSomeData);
                        }
                    });
                    _sockStream.SetSendCompleteEventHandler((s, e) =>
                    {
                        HandleSend(SendIOEventCode.SendComplete);
                    });
                }
                _sockStream.StartReceive();

            }
            else
            {
                //with cert , we need ssl stream

                SecureSockNetworkStream secureStream = new SecureSockNetworkStream(_baseSockStream, cert);
                _sockStream = secureStream; //**  
                if (_isFirstTime)
                {
                    _isFirstTime = false;
                    _sockStream.SetRecvCompleteEventHandler((r, byteCount) =>
                    {
                        if (byteCount == 0)
                        {
                            HandleReceive(RecvEventCode.NoMoreReceiveData);
                        }
                        else
                        {
                            HandleReceive(RecvEventCode.HasSomeData);
                        }

                    });
                    _sockStream.SetSendCompleteEventHandler((s, e) =>
                    {
                        HandleSend(SendIOEventCode.SendComplete);
                    });
                }
                try
                {
                    //we encapsulate ssl inside the secure socket stream 
                    secureStream.AuthenAsServer(() => _sockStream.StartReceive());

                }
                catch (System.IO.IOException ex)
                {
                    //eg. unexpected format
                    //can't start receive
                    //the we must exit this context ***
                    return;
                }

            }

        }
        internal void Reset()
        {
            //reset recv and send
            //for next use
            _httpReq.Reset();
            _httpResp.ResetAll();
            _sockStream.Reset();
        }

#if DEBUG
        int dbugSendComplete = 0;
#endif
        void HandleSend(SendIOEventCode sendEventCode)
        {
            switch (sendEventCode)
            {
                case SendIOEventCode.SocketError:
                    {
                        UnBindSocket(true);
                        KeepAlive = false;
                    }
                    break;
                case SendIOEventCode.SendComplete:
                    {

                        //TODO: review , this is not called on .netcore/https
#if DEBUG
                        dbugSendComplete++;
#endif

                        Reset();
                        //next recv on the same client
                        StartReceive();

                    }
                    break;
            }
        }


        void HandleReceive(RecvEventCode recvEventCode)
        {
            switch (recvEventCode)
            {
                case RecvEventCode.SocketError:
                    {
                        UnBindSocket(true);
                    }
                    break;
                case RecvEventCode.NoMoreReceiveData:
                    {
                        //no data to receive
                        _httpResp.End();
                        //reqHandler(this.httpReq, httpResp);
                    }
                    break;
                case RecvEventCode.HasSomeData:
                    {
                        //process some data
                        //there some data to process  
                        switch (_httpReq.LoadData())
                        {
                            case ProcessReceiveBufferResult.Complete:
                                {
                                    //recv and parse complete  
                                    //goto user action 
                                    if (this.EnableWebSocket &&
                                        _ownerServer.CheckWebSocketUpgradeRequest(this))
                                    {
                                        return;
                                    }

                                    _reqHandler(_httpReq, _httpResp);
                                    if (_httpResp._actualEnd)
                                    {
                                        _httpResp.ActualEnd();
                                    }

                                    Reset();
                                }
                                break;
                            case ProcessReceiveBufferResult.NeedMore:
                                { 

                                }
                                break;
                            case ProcessReceiveBufferResult.Error:
                            default:
                                throw new NotSupportedException();
                        }
                    }
                    break;
            }
        }
        protected virtual void OnSocketError()
        {

        }
        protected virtual void OnNoMoreReceiveData()
        {

        }
        public void Dispose()
        {
            //   this.recvSendArgs.Dispose();
        }

#if DEBUG

        internal static int dbug_s_mainSessionId = 1000000000;
        /// <summary>
        /// create new session id
        /// </summary>
        internal void dbugCreateSessionId()
        {
            //new session id
            _dbugSessionId = System.Threading.Interlocked.Increment(ref dbug_s_mainSessionId);
        }
        public Int32 dbugSessionId => _dbugSessionId;

        int _dbugSessionId;
        public void dbugSetInfo(int tokenId)
        {
            _dbugTokenId = tokenId;
        }
        public Int32 dbugTokenId
        {
            //Let's use an ID for this object during testing, just so we can see what
            //is happening better if we want to. 
            get
            {
                return _dbugTokenId;
            }
        }


        int _dbugTokenId; //for testing only    

#endif

    }


}