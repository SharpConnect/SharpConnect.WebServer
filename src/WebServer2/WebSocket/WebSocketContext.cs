//2015-2016, MIT, EngineKit
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

namespace SharpConnect.WebServers.Server2
{
    public class WebSocketContext : IDisposable, ISendIO
    {

        ReqRespHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler;
        SharpConnect.Internal2.AbstractAsyncNetworkStream _clientStream;

        const int RECV_BUFF_SIZE = 1024;

        WebSocketResponse webSocketResp;
        WebSocketProtocolParser webSocketReqParser;


        int connectionId;
        static int connectionIdTotal;

        bool _asClientContext;
        public WebSocketContext(bool asClient)
        {
            _asClientContext = asClient;
            connectionId = System.Threading.Interlocked.Increment(ref connectionIdTotal);
            webSocketResp = new WebSocketResponse(asClient, this);
        }
        //
        public bool AsClientContext => _asClientContext;
        internal void Bind(SharpConnect.Internal2.AbstractAsyncNetworkStream clientStream, byte[] wsUpgradeResponseMsg)
        {

            this.webSocketReqParser = new WebSocketProtocolParser(this.AsClientContext, new SharpConnect.Internal2.RecvIOBufferStream2(clientStream));
            _clientStream = clientStream;

            _clientStream.SetRecvCompleteEventHandler((s, e) =>
            {
                if (e.ByteTransferedCount == 0)
                {
                    HandleReceivedData(RecvEventCode.NoMoreReceiveData);
                }
                else
                {
                    HandleReceivedData(RecvEventCode.HasSomeData);
                }

            });
            _clientStream.SetSendCompleteEventHandler((s, e) =>
            {
                sendIO_SendCompleted(SendIOEventCode.SendComplete);
            });


            _clientStream.StartReceive();
            SendExternalRaw(wsUpgradeResponseMsg);

        }
        void ISendIO.EnqueueSendingData(byte[] buffer, int len) => _clientStream.EnqueueSendData(buffer, len);
        void ISendIO.SendIOStartSend() => _clientStream.StartSend();
        int ISendIO.QueueCount => _clientStream.QueueCount;

        void HandleReceivedData(RecvEventCode recvCode)
        {
            switch (recvCode)
            {
                case RecvEventCode.HasSomeData:

                    //parse recv msg
                    switch (this.webSocketReqParser.ParseRecvData())
                    {
                        //in this version all data is copy into WebSocketRequest
                        //so we can reuse recv buffer 
                        //TODO: review this, if we need to copy?,  

                        case ProcessReceiveBufferResult.Complete:
                            {
                                //you can choose ...
                                //invoke webSocketReqHandler in this thread or another thread
                                while (webSocketReqParser.ReqCount > 0)
                                {
                                    WebSocketRequest req = webSocketReqParser.Dequeue();
                                    webSocketReqHandler(req, webSocketResp);
                                }
                                _clientStream.StartReceive();
                                //***no code after StartReceive***
                            }
                            return;
                        case ProcessReceiveBufferResult.NeedMore:
                            {
                                _clientStream.StartReceive();
                                //recvIO.StartReceive();
                                //***no code after StartReceive***
                            }
                            return;
                        case ProcessReceiveBufferResult.Error:
                        default:
                            throw new NotSupportedException();
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
        void sendIO_SendCompleted(SendIOEventCode eventCode)
        {

        }
        public string Name
        {
            get;
            set;
        }
        public void Dispose()
        {

        }

        public int ConnectionId
        {
            get { return this.connectionId; }
        }
        public void SetMessageHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            this.webSocketReqHandler = webSocketReqHandler;
        }
        public void Close()
        {
            System.Diagnostics.Debug.WriteLine("write line:");
            //clientSocket.Close();
        }
        public void Send(string dataToSend)
        {
            //send data to server
            //and wait for result 
            webSocketResp.Write(dataToSend);
        }
        public int SendQueueCount
        {
            get { return webSocketResp.SendQueueCount; }
        }
        internal void SendExternalRaw(byte[] data)
        {
            _clientStream.BeginWebsocketMode = true;
            _clientStream.EnqueueSendData(data, data.Length);
            _clientStream.StartSend();

            //sendIO.EnqueueOutputData(data, data.Length);
            //sendIO.StartSendAsync();
        }
        //---------------------------------------------
        public string InitClientRequestUrl
        {
            get;
            set;
        }
    }

}