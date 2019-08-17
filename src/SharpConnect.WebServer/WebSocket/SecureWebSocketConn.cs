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

namespace SharpConnect.WebServers
{

    sealed class SecureWebSocketConn : WebSocketConnectionBase, ISendIO
    {
        SharpConnect.Internal2.AbstractAsyncNetworkStream _clientStream;
        RecvIOBufferStream _recvIOStream;
        object _recvIOLock = new object();
        public SecureWebSocketConn(bool asClient)
            : base(asClient)
        {
            _webSocketResp = new WebSocketResponse(_connectionId, asClient, this);
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

        public void Bind(SharpConnect.Internal2.AbstractAsyncNetworkStream clientStream, byte[] wsUpgradeResponseMsg)
        {
            _recvIOStream = new RecvIOBufferStream();
            _webSocketReqParser = new WebSocketProtocolParser(this, _recvIOStream);
            _webSocketReqParser.SetNewParseResultHandler(req =>
            {
                WebSocketReqInputQueue.Enqueue(new WebSocketReqQueueItem(this, req));
            });
            //-------
            _clientStream = clientStream;
            _clientStream.SetRecvCompleteEventHandler((r, byteCount) =>
            {
                lock (_recvIOLock)
                {
                    if (byteCount == 0)
                    {
                        HandleReceivedData(RecvEventCode.NoMoreReceiveData);
                    }
                    else
                    {
                        int recvByteCount = _clientStream.ByteReadTransfered;
                        if (recvByteCount == 0)
                        {
                            HandleReceivedData(RecvEventCode.NoMoreReceiveData);
                        }
                        else
                        {

                            byte[] tmp1 = new byte[recvByteCount];
                            _clientStream.ReadBuffer(0, recvByteCount, tmp1, 0);
                            _recvIOStream.WriteData(tmp1, recvByteCount);
                            //copy data and write to recvIOStream
                            HandleReceivedData(RecvEventCode.HasSomeData);
                        }
                    }
                }
            });
            _clientStream.SetSendCompleteEventHandler((s, e) =>
            {
                //blank here
            });


            _clientStream.StartReceive();
            //_clientStream.BeginWebsocketMode = true;//

            //--------
            //send websocket reply
            _clientStream.EnqueueSendData(wsUpgradeResponseMsg, wsUpgradeResponseMsg.Length);
            _clientStream.StartSend();
            //--------

        }
        void ISendIO.EnqueueSendingData(byte[] buffer, int len)
        {
            _clientStream.EnqueueSendData(buffer, len);
        }
        void ISendIO.SendIOStartSend() => _clientStream.StartSend();


        void HandleReceivedData(RecvEventCode recvCode)
        {
            switch (recvCode)
            {
                case RecvEventCode.HasSomeData:
                    {
                        if (_asClientContext && !_clientStream.BeginWebsocketMode)
                        {
                            int recvByteCount = _clientStream.ByteReadTransfered;
                            byte[] tmp1 = new byte[2048];//TODO:....
                            _clientStream.ReadBuffer(0, recvByteCount, tmp1, 0);
                            string text = System.Text.Encoding.UTF8.GetString(tmp1, 0, recvByteCount);
                            if (text.StartsWith("HTTP/1.1 101 Switching Protocols\r\nUpgrade"))
                            {
                                //*** clear prev buffer before new recv

                                _recvIOStream.ForceClear();

                                _clientStream.ClearReceiveBuffer();
                                _clientStream.BeginWebsocketMode = true; //***
                                _clientStream.StartReceive();//***

                                return;
                            }
                        }
                        //------

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
#if DEBUG
                                    //System.Diagnostics.Debug.WriteLine("complete:" + _webSocketReqParser.ReqCount.ToString());
#endif

                                    //                                    //debug
                                    //                                    int reqCount = _webSocketReqParser.ReqCount;
                                    //                                    int reqCountBackup = reqCount;
                                    //                                    while (_webSocketReqParser.ReqCount > 0)
                                    //                                    {
                                    //#if DEBUG
                                    //                                        System.Diagnostics.Debug.WriteLine("req_count:" + _webSocketReqParser.ReqCount.ToString());
                                    //#endif
                                    //                                        reqCount--;
                                    //                                        WebSocketRequest req = _webSocketReqParser.Dequeue();
                                    //                                        _webSocketReqHandler(req, _webSocketResp);
                                    //                                    }

                                    //                                    if (reqCount != 0)
                                    //                                    {

                                    //                                    }

                                    _clientStream.ClearReceiveBuffer();
                                    _recvIOStream.ForceClear();
                                    _clientStream.StartReceive();
                                    //***no code after StartReceive***
                                }
                                return;
                            case ProcessReceiveBufferResult.NeedMore:
                                {
                                    //#if DEBUG
                                    //                                    System.Diagnostics.Debug.WriteLine("need_more:" + _webSocketReqParser.ReqCount.ToString());
                                    //#endif
                                    //                                    while (_webSocketReqParser.ReqCount > 0)
                                    //                                    {
                                    //#if DEBUG
                                    //                                        System.Diagnostics.Debug.WriteLine("req_count_x:" + _webSocketReqParser.ReqCount.ToString());
                                    //#endif
                                    //                                        WebSocketRequest req = _webSocketReqParser.Dequeue();
                                    //                                        _webSocketReqHandler(req, _webSocketResp);
                                    //                                    }
                                    //_clientStream.StartReceive();
                                    //recvIO.StartReceive();
                                    //***no code after StartReceive***
                                }
                                return;
                            case ProcessReceiveBufferResult.Error:
                            default:
                                throw new NotSupportedException();
                        }
                    }
                case RecvEventCode.NoMoreReceiveData:
                    {
                        _clientStream.StartReceive();
                    }
                    break;
                case RecvEventCode.SocketError:
                    {
                    }
                    break;
            }
        }

        public override void Close()
        {
            System.Diagnostics.Debug.WriteLine("please impl close:");
        }
    }

}