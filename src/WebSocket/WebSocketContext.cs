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
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{
    public class WebSocketContext : IDisposable
    {
        readonly WebSocketServer webSocketServer;
        readonly SocketAsyncEventArgs sockAsyncSender;
        readonly SocketAsyncEventArgs sockAsyncListener;

        ReqRespHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler;
        Socket clientSocket;

        byte[] outputData = new byte[512];


        WebSocketRequest webSocketReq;
        WebSocketResponse webSocketResp;

        RecvIO recvIO;
        SendIO sendIO;


        int connectionId;
        static int connectionIdTotal;

        public WebSocketContext(WebSocketServer webSocketServer, ReqRespHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            this.webSocketReqHandler = webSocketReqHandler;
            this.webSocketServer = webSocketServer;
            connectionId = System.Threading.Interlocked.Increment(ref  connectionIdTotal);
            //-------------------
            //send,resp
            sockAsyncSender = new SocketAsyncEventArgs();
            sendIO = new SendIO(sockAsyncSender, 0, 512, send_Complete);
            sockAsyncSender.Completed += new EventHandler<SocketAsyncEventArgs>((s, e) =>
            {
                switch (e.LastOperation)
                {
                    default:
                        {
                        }
                        break;
                    case SocketAsyncOperation.Send:
                        {
                            //switch sockAsync to receive 
                            //when complete send
                            sendIO.ProcessSend();

                            //isSending = false;
                            //if (dataQueue.Count > 0)
                            //{
                            //    string dataToSend = dataQueue.Dequeue();
                            //    Send(dataToSend);
                            //}
                        }
                        break;
                    case SocketAsyncOperation.Receive:
                        {


                        }
                        break;
                }
            });
            webSocketResp = new WebSocketResponse(this, sendIO);

            //------------------------------------------------------------------------------------
            //recv,req
            sockAsyncListener = new SocketAsyncEventArgs();
            recvIO = new RecvIO(sockAsyncListener, 0, 512, recv_Complete);
            sockAsyncListener.Completed += new EventHandler<SocketAsyncEventArgs>((s, e) =>
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
                            recvIO.ProcessReceive();
                        }
                        break;
                }
            });
            //------------------------------------------------------------------------------------

            webSocketReq = new WebSocketRequest(recvIO);
        }
        public void Bind(Socket clientSocket)
        {
            this.clientSocket = clientSocket;
            sockAsyncSender.AcceptSocket = clientSocket;
            //------------------------------------------------------
            sockAsyncListener.AcceptSocket = clientSocket;
            //------------------------------------------------------
            sockAsyncListener.SetBuffer(new byte[1024], 0, 1024);
            //------------------------------------------------------
            //when bind we start listening 
            clientSocket.ReceiveAsync(sockAsyncListener);
            //------------------------------------------------------  
        }
        void recv_Complete(RecvEventCode recvCode)
        {
            switch (recvCode)
            {
                case RecvEventCode.HasSomeData:
                    {
                        //parse recv msg
                        if (webSocketReq.LoadData())
                        {
                            webSocketReqHandler(webSocketReq, webSocketResp);
                        }
                        //start next recv
                        byte[] newRecvBuffer = new byte[512];
                        recvIO.StartReceive(newRecvBuffer, 512);
                    } break;
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
        void send_Complete(SendIOEventCode eventCode)
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
            clientSocket.Close();
        }
        public void Send(string dataToSend)
        {
            //send data to server
            //and wait for result 
            webSocketResp.Write(dataToSend);
        }
        internal void SendExternalRaw(byte[] data)
        {
            sockAsyncSender.SetBuffer(data, 0, data.Length);
            clientSocket.SendAsync(sockAsyncSender);
        }

    }

}