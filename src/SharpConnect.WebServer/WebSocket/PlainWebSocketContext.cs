//MIT, 2015-present, EngineKit
/* The MIT License
*
* Copyright (c) 2013-2015 sta.blockhead
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
using System.Net.Sockets;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{


    class PlainWebSocketContext : IDisposable, ISendIO
    {

        readonly SocketAsyncEventArgs _sockAsyncSender;
        readonly SocketAsyncEventArgs _sockAsyncListener;

        ReqRespHandler<WebSocketRequest, WebSocketResponse> _webSocketReqHandler;
        Socket _clientSocket;

        const int RECV_BUFF_SIZE = 1024;

        WebSocketResponse _webSocketResp;
        WebSocketProtocolParser _webSocketReqParser;

        RecvIO _recvIO;
        SendIO _sendIO;

        readonly bool _asClientContext;
        readonly int _connectionId;
        static int s_connectionIdTotal;

        public PlainWebSocketContext(bool asClient)
        {
            _asClientContext = asClient;
            _connectionId = System.Threading.Interlocked.Increment(ref s_connectionIdTotal);
            //-------------------
            //send,resp 
            _sockAsyncSender = new SocketAsyncEventArgs();
            _sockAsyncSender.SetBuffer(new byte[RECV_BUFF_SIZE], 0, RECV_BUFF_SIZE);
            _sendIO = new SendIO(_sockAsyncSender, 0, RECV_BUFF_SIZE, HandleSendCompleted);
            _sockAsyncSender.Completed += new EventHandler<SocketAsyncEventArgs>((s, e) =>
            {
                switch (e.LastOperation)
                {
                    default:
                        {
                        }
                        break;
                    case SocketAsyncOperation.Send:
                        {
                            _sendIO.ProcessWaitingData();
                        }
                        break;
                    case SocketAsyncOperation.Receive:
                        {
                        }
                        break;
                }
            });
            _webSocketResp = new WebSocketResponse(_connectionId, asClient, this);

            //------------------------------------------------------------------------------------
            //recv,req ,new socket
            _sockAsyncListener = new SocketAsyncEventArgs();
            _sockAsyncListener.SetBuffer(new byte[RECV_BUFF_SIZE], 0, RECV_BUFF_SIZE);
            _recvIO = new RecvIO(_sockAsyncListener, 0, RECV_BUFF_SIZE, HandleReceivedData);
            _sockAsyncListener.Completed += new EventHandler<SocketAsyncEventArgs>((s, e) =>
            {
                switch (e.LastOperation)
                {
                    default:
                        {
                        }
                        break;
                    case SocketAsyncOperation.Send:
                        {
                        }
                        break;
                    case SocketAsyncOperation.Receive:
                        {
                            _recvIO.ProcessReceivedData();
                        }
                        break;
                }
            });
            //------------------------------------------------------------------------------------             
            _webSocketReqParser = new WebSocketProtocolParser(this.AsClientContext, new RecvIOBufferStream(_recvIO));

        }
        public bool AsClientContext => _asClientContext;
        public void Bind(Socket clientSocket)
        {
            _clientSocket = clientSocket;
            //sender
            _sockAsyncSender.AcceptSocket = clientSocket;
            //------------------------------------------------------
            //listener   
            _sockAsyncListener.AcceptSocket = clientSocket;
            //sockAsyncListener.SetBuffer(new byte[RECV_BUFF_SIZE], 0, RECV_BUFF_SIZE);
            //------------------------------------------------------
            //when bind we start listening 
            clientSocket.ReceiveAsync(_sockAsyncListener);
            //------------------------------------------------------  
        }
        void ISendIO.EnqueueSendingData(byte[] buffer, int len) => _sendIO.EnqueueOutputData(buffer, len);
        void ISendIO.SendIOStartSend() => _sendIO.StartSendAsync();
        int ISendIO.QueueCount => _sendIO.QueueCount;
        void HandleReceivedData(RecvEventCode recvCode)
        {
            switch (recvCode)
            {
                case RecvEventCode.HasSomeData:

                    //parse recv msg
                    switch (_webSocketReqParser.ParseRecvData())
                    {
                        //in this version all data is copy into WebSocketRequest
                        //so we can reuse recv buffer 
                        //TODO: review this, if we need to copy?,  

                        case ProcessReceiveBufferResult.Complete:
                            {
                                //you can choose ...
                                //invoke webSocketReqHandler in this thread or another thread
                                while (_webSocketReqParser.ReqCount > 0)
                                {
                                    WebSocketRequest req = _webSocketReqParser.Dequeue();
                                    _webSocketReqHandler(req, _webSocketResp);
                                }
                                _recvIO.StartReceive();
                                //***no code after StartReceive***
                            }
                            return;
                        case ProcessReceiveBufferResult.NeedMore:
                            {
                                _recvIO.StartReceive();
                                //***no code after StartReceive***
                            }
                            return;
                        case ProcessReceiveBufferResult.Error:
                        default:
                            throw new NotSupportedException();
                    }

                case RecvEventCode.NoMoreReceiveData:
                    {
                    }
                    break;
                case RecvEventCode.SocketError:
                    {
                    }
                    break;
            }
        }
        void HandleSendCompleted(SendIOEventCode eventCode)
        {

        }
        public string Name { get; set; }

        public string InitClientRequestUrl { get; set; }

        public void Dispose()
        {


        }
        public int ConnectionId => _connectionId;

        public void SetMessageHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            _webSocketReqHandler = webSocketReqHandler;
        }

        public void Close() => _clientSocket.Close();

        public void Send(string dataToSend)
        {
            //send data to server
            //and wait for result 
            _webSocketResp.Write(dataToSend);
        }

        public int SendQueueCount => _webSocketResp.SendQueueCount;

        internal void SendExternalRaw(byte[] data)
        {
            _sendIO.EnqueueOutputData(data, data.Length);
            _sendIO.StartSendAsync();
        }
    }

}