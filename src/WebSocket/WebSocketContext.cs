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

        ConnHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler;
        Socket clientSocket;

        byte[] outputData = new byte[512];


        WebSocketRequest webSocketReq;
        WebSocketResponse webSocketResp;

        RecvIO recvIO;
        SendIO sendIO; 

        int connectionId;
        static int connectionIdTotal;

        public WebSocketContext(WebSocketServer webSocketServer, ConnHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            this.webSocketReqHandler = webSocketReqHandler;
            this.webSocketServer = webSocketServer;
            connectionId = System.Threading.Interlocked.Increment(ref  connectionIdTotal);
            //-------------------
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
                            sendIO.ProcessSend();
                        }
                        break;
                    case SocketAsyncOperation.Receive:
                        {


                        }
                        break;
                }
            });
           

            //------------------------------------------------------------------------------------
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
            webSocketResp = new WebSocketResponse(this, sendIO);
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

        internal void SendUpgradeResponse(string sec_websocket_key)
        {
            //this is http msg, first time after accept client
            byte[] data = MakeWebSocketUpgradeResponse(MakeResponseMagicCode(sec_websocket_key));
            sockAsyncSender.SetBuffer(data, 0, data.Length);
            clientSocket.SendAsync(sockAsyncSender);
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
                            //req is complete then invoke clint
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
        void send_Complete(SendIOEventCode sendEventCode)
        {
            switch (sendEventCode)
            {
                case SendIOEventCode.SendComplete:
                    break;
                case SendIOEventCode.SocketError:
                    break;

            }

        }
        public void Dispose()
        {

        }
        public int ConnectionId
        {
            get { return this.connectionId; }
        }
        public void SetMessageHandler(ConnHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
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
            
        }
        

        static byte[] MakeWebSocketUpgradeResponse(string webSocketSecCode)
        {
            int contentByteCount = 0; // "" empty string 
            StringBuilder headerStBuilder = new StringBuilder();
            headerStBuilder.Length = 0;
            headerStBuilder.Append("HTTP/1.1 ");
            headerStBuilder.Append("101 Switching Protocols\r\n");
            headerStBuilder.Append("Upgrade: websocket\r\n");
            headerStBuilder.Append("Connection: Upgrade\r\n");
            headerStBuilder.Append("Sec-WebSocket-Accept: " + webSocketSecCode + "\r\n");
            headerStBuilder.Append("Content-Length: " + contentByteCount + "\r\n");
            headerStBuilder.Append("\r\n");

            //-----------------------------------------------------------------
            //switch transfer encoding method of the body***
            var headBuffer = Encoding.UTF8.GetBytes(headerStBuilder.ToString().ToCharArray());
            byte[] dataToSend = new byte[headBuffer.Length + contentByteCount];
            Buffer.BlockCopy(headBuffer, 0, dataToSend, 0, headBuffer.Length);
            return dataToSend;
        }
        //----------------------------
        //websocket
        const string magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        static string MakeResponseMagicCode(string reqMagicString)
        {
            string total = reqMagicString + magicString;
            var sha1 = SHA1.Create();
            byte[] shaHash = sha1.ComputeHash(Encoding.ASCII.GetBytes(total));
            return Convert.ToBase64String(shaHash);

        }
        //----------------------------
    }

}