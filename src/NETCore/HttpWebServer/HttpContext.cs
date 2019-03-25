//2015-2016, MIT, EngineKit 
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
using System.Collections.Generic;
using System.Net.Sockets;
using SharpConnect.Internal;


namespace SharpConnect.WebServers
{
    /// <summary>
    /// http connection session, req-resp model
    /// </summary>
    class HttpContext
    {
        readonly SocketAsyncEventArgs _send_a;
        readonly SocketAsyncEventArgs _recv_a;
        readonly RecvIO recvIO;
        readonly SendIO sendIO;

        HttpRequest httpReq;
        HttpResponse httpResp;
        ReqRespHandler<HttpRequest, HttpResponse> reqHandler;
        WebServer ownerServer;

        const int RECV_BUFF_SIZE = 1024;

        public HttpContext(
            WebServer ownerServer,
            int recvBufferSize,
            int sendBufferSize)
        {
            this.EnableWebSocket = true;
            this.ownerServer = ownerServer;
            //each recvSendArgs is created for this connection session only ***
            //---------------------------------------------------------------------------------------------------------- 

            KeepAlive = false;
            //set buffer for newly created saArgs
            _recv_a = new SocketAsyncEventArgs();
            _send_a = new SocketAsyncEventArgs();
            _recv_a.SetBuffer(new byte[RECV_BUFF_SIZE], 0, RECV_BUFF_SIZE);
            _send_a.SetBuffer(new byte[RECV_BUFF_SIZE], 0, RECV_BUFF_SIZE);
            //ownerServer.SetBufferFor();
            //ownerServer.SetBufferFor();

            recvIO = new RecvIO(_recv_a, _recv_a.Offset, recvBufferSize, HandleReceive);
            sendIO = new SendIO(_send_a, _send_a.Offset, sendBufferSize, HandleSend);
            //----------------------------------------------------------------------------------------------------------  
            httpReq = new HttpRequest(this);
            httpResp = new HttpResponse(this, sendIO);

            //common(shared) event listener***
            _recv_a.Completed += (object sender, SocketAsyncEventArgs e) =>
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive:
                        recvIO.ProcessReceivedData();
                        break;
                    case SocketAsyncOperation.Send:
                        //sendIO.ProcessWaitingData();
                        break;
                    default:
                        throw new ArgumentException("The last operation completed on the socket was not a receive or send");
                }
            };
            _send_a.Completed += (object sender, SocketAsyncEventArgs e) =>
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive:
                        //recvIO.ProcessReceivedData();
                        break;
                    case SocketAsyncOperation.Send:
                        sendIO.ProcessWaitingData();
                        break;
                    default:
                        throw new ArgumentException("The last operation completed on the socket was not a receive or send");
                }
            };
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
                        httpResp.End();
                        //reqHandler(this.httpReq, httpResp);
                    }
                    break;
                case RecvEventCode.HasSomeData:
                    {
                        //process some data
                        //there some data to process  
                        switch (httpReq.LoadData(recvIO))
                        {
                            case ProcessReceiveBufferResult.Complete:
                                {
                                    //recv and parse complete  
                                    //goto user action

                                    if (this.EnableWebSocket &&
                                        this.ownerServer.CheckWebSocketUpgradeRequest(this))
                                    {
                                        return;
                                    }
                                    reqHandler(this.httpReq, httpResp);
                                }
                                break;
                            case ProcessReceiveBufferResult.NeedMore:
                                {
                                    recvIO.StartReceive();
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
                        Reset();
                        if (KeepAlive)
                        {
                            //next recv on the same client
                            StartReceive();
                        }
                        else
                        {
                            UnBindSocket(true);
                        }
                    }
                    break;
            }
        }

        public bool EnableWebSocket
        {
            get;
            set;
        }
        public bool KeepAlive
        {
            get;
            set;
        }
        internal HttpRequest HttpReq
        {
            get { return this.httpReq; }
        }
        internal HttpResponse HttpResp
        {
            get { return this.httpResp; }
        }
        internal Socket RemoteSocket
        {
            get { return _recv_a.AcceptSocket; }
        }
        /// <summary>
        /// bind to client socket
        /// </summary>
        /// <param name="clientSocket"></param>
        internal void BindSocket(Socket clientSocket)
        {
            _recv_a.AcceptSocket = clientSocket;
            _send_a.AcceptSocket = clientSocket;
        }
        internal void BindReqHandler(ReqRespHandler<HttpRequest, HttpResponse> reqHandler)
        {
            this.reqHandler = reqHandler;
        }
        internal void UnBindSocket(bool closeClientSocket)
        {
            //cut connection from current socket
            Socket clientSocket = _recv_a.AcceptSocket;
            if (closeClientSocket)
            {
                try
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                }
                // throws if socket was already closed
                catch (Exception)
                {
                    // dbugSendLog(connSession, "CloseClientSocket, Shutdown catch");
                }
                clientSocket.Close();
            }
            _recv_a.AcceptSocket = null;
            _send_a.AcceptSocket = null;

            Reset();//reset 
            ownerServer.ReleaseChildConn(this);
        }
        internal void StartReceive()
        {
            recvIO.StartReceive();
        }
        internal void Reset()
        {
            //reset recv and send
            //for next use
            httpReq.Reset();
            httpResp.ResetAll();
            sendIO.Reset();
        }

        protected virtual void OnSocketError()
        {

        }
        protected virtual void OnNoMoreReceiveData()
        {

        }
        public void Dispose()
        {
            this._recv_a.Dispose();
        }


        internal WebServer OwnerWebServer
        {
            get { return this.ownerServer; }
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
        public Int32 dbugSessionId
        {
            get
            {
                return this._dbugSessionId;
            }
        }
        int _dbugSessionId;
        public void dbugSetInfo(int tokenId)
        {
            this._dbugTokenId = tokenId;
        }
        public Int32 dbugTokenId
        {
            //Let's use an ID for this object during testing, just so we can see what
            //is happening better if we want to.

            get
            {
                return this._dbugTokenId;
            }
        }
        int _dbugTokenId; //for testing only    

#endif

    }

    enum ProcessReceiveBufferResult
    {
        Error,
        NeedMore,
        Complete
    }
}