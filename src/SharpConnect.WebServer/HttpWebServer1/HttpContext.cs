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
using System.Collections.Generic;
using System.Net.Sockets;
using SharpConnect.Internal;


namespace SharpConnect.WebServers
{

    /// <summary>
    /// http connection session, req-resp model
    /// </summary>
    class HttpContext : IHttpContext, ISendIO
    {
        const int RECV_BUFF_SIZE = 1024 * 16;

        readonly SocketAsyncEventArgs _send_a;
        readonly SocketAsyncEventArgs _recv_a;
        readonly RecvIO _recvIO;
        readonly SendIO _sendIO;

        HttpRequestImpl _httpReq;
        HttpResponseImpl _httpResp;
        ReqRespHandler<HttpRequest, HttpResponse> _reqHandler;
        HttpWebServer _ownerServer;

        public HttpContext(
            HttpWebServer ownerServer,
            int recvBufferSize,
            int sendBufferSize)
        {
            this.EnableWebSocket = true;
            _ownerServer = ownerServer;
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

            _recvIO = new RecvIO(_recv_a, _recv_a.Offset, recvBufferSize, HandleReceive);
            _sendIO = new SendIO(_send_a, _send_a.Offset, sendBufferSize, HandleSend);
            //----------------------------------------------------------------------------------------------------------  
            _httpReq = new HttpRequestImpl(this);
            _httpReq.SetLargeUploadFilePolicyHandler(_ownerServer.LargeFileUploadPermissionReqHandler);

            _httpResp = new HttpResponseImpl(this);

            //common(shared) event listener***
            _recv_a.Completed += (object sender, SocketAsyncEventArgs e) =>
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive:
                        _recvIO.ProcessReceivedData();
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
                        _sendIO.ProcessWaitingData();
                        break;
                    default:
                        throw new ArgumentException("The last operation completed on the socket was not a receive or send");
                }
            };
        }

        public int QueueCount => _sendIO.QueueCount;

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

                                    //                                    Reset();
                                }
                                break;
                            case ProcessReceiveBufferResult.NeedMore:
                                {
                                    _recvIO.StartReceive();
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

        public bool EnableWebSocket { get; set; }

        public bool KeepAlive { get; set; }

        internal HttpRequest HttpReq => _httpReq;

        internal HttpResponse HttpResp => _httpResp;

        internal Socket RemoteSocket => _recv_a.AcceptSocket;

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
            _reqHandler = reqHandler;
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
            _ownerServer.ReleaseChildConn(this);
        }
        internal void StartReceive()
        {
            _recvIO.StartReceive();
        }
        internal void Reset()
        {
            //reset recv and send
            //for next use
            _httpReq.Reset();
            _httpResp.ResetAll();
            _sendIO.Reset();
        }

        protected virtual void OnSocketError()
        {

        }
        protected virtual void OnNoMoreReceiveData()
        {

        }

        public void Dispose()
        {
            _recv_a.Dispose();
        }

        internal HttpWebServer OwnerWebServer => _ownerServer;

        public void SendIOStartSend() => _sendIO.StartSendAsync();

        public void EnqueueSendingData(byte[] dataToSend, int count) => _sendIO.EnqueueOutputData(dataToSend, count);
        public void EnqueueSendingData(DataStream dataStream) => _sendIO.EnqueueOutputData(dataStream);

        public int RecvByteTransfer => _recvIO.BytesTransferred;
        public byte ReadByte(int pos) => _recvIO.ReadByte(pos);
        public void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen) => _recvIO.CopyTo(readpos, dstBuffer, copyLen);

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