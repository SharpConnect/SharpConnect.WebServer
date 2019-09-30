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
    sealed class PlainWebSocketConn : WebSocketConnectionBase, ISendIO
    {

        readonly SocketAsyncEventArgs _sockAsyncSender;
        readonly SocketAsyncEventArgs _sockAsyncListener;

        Socket _clientSocket;
        const int RECV_BUFF_SIZE = 1024;
        RecvIO _recvIO;
        SendIO _sendIO;

        public PlainWebSocketConn(bool asClient) : base(asClient)
        {

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


            RecvIOBufferStream recvIOStream = new RecvIOBufferStream();
            _webSocketReqParser = new WebSocketProtocolParser(this, recvIOStream);
            _webSocketReqParser.SetNewParseResultHandler(req =>
            {
                WebSocketReqInputQueue.Enqueue(new WebSocketReqQueueItem(this, req));
            });
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
                            //copy data and write to recvIO stream

                            _recvIO.ProcessReceivedData();
                        }
                        break;
                }
            });
            //------------------------------------------------------------------------------------             


        }
        public override WebSocketContentCompression Compression
        {
            get => base.Compression;
            set
            {
                base.Compression = value;
                _webSocketResp.Compression = value;
            }
        }
        public void Bind(Socket clientSocket, byte[] connReplMsg)
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

            //send websocket reply
            _sendIO.EnqueueOutputData(connReplMsg, connReplMsg.Length);
            _sendIO.StartSendAsync();
            //--------            
        }
        void ISendIO.EnqueueSendingData(byte[] buffer, int len) => _sendIO.EnqueueOutputData(buffer, len);
        void ISendIO.SendIOStartSend() => _sendIO.StartSendAsync();

        bool _beginWebSocketMode;
        void HandleReceivedData(RecvEventCode recvCode)
        {
            switch (recvCode)
            {
                case RecvEventCode.HasSomeData:
                    {
                        if (_asClientContext && !_beginWebSocketMode)
                        {

                            int recvByteCount = _recvIO.BytesTransferred;
                            byte[] tmp1 = new byte[2048];
                            _recvIO.CopyTo(0, tmp1, recvByteCount);
                            //_clientStream.ReadBuffer(0, recvByteCount, tmp1, 0);
                            string text = System.Text.Encoding.UTF8.GetString(tmp1, 0, recvByteCount);

                            if (text.StartsWith("HTTP/1.1 101 Switching Protocols\r\nUpgrade"))
                            {
                                _beginWebSocketMode = true;
                                _recvIO.StartReceive();

                                ////*** clear prev buffer before new recv
                                //_clientStream.ClearReceiveBuffer();
                                //_clientStream.BeginWebsocketMode = true; //***
                                //_clientStream.StartReceive();//***
                                return;
                            }
                            else if (text.StartsWith("HTTP/1.1"))
                            {
                                _recvIO.StartReceive();
                                return;
                            }
                            //-- 
                        }

                        //parse recv msg
                        switch (_webSocketReqParser.ParseRecvData())
                        {
                            //in this version all data is copy into WebSocketRequest
                            //so we can reuse recv buffer 
                            //TODO: review this, if we need to copy?,  

                            case ProcessReceiveBufferResult.Complete:

                                //you can choose ...
                                //invoke webSocketReqHandler in this thread or another thread 
                                _recvIO.StartReceive();
                                //***no code after StartReceive*** 
                                return;
                            case ProcessReceiveBufferResult.NeedMore:
                                _recvIO.StartReceive();
                                //***no code after StartReceive*** 
                                return;
                            case ProcessReceiveBufferResult.Error:
                            default:
                                throw new NotSupportedException();
                        }
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
        public override void Close() => _clientSocket.Close();


    }

}