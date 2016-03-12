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

        /// <summary>
        /// TODO: review data type
        /// </summary>
        Queue<string> dataQueue = new Queue<string>();
        bool isSending = false;
        int connectionId;
        static int connectionIdTotal;

        public WebSocketContext(WebSocketServer webSocketServer, ConnHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
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
                            isSending = false;
                            if (dataQueue.Count > 0)
                            {
                                string dataToSend = dataQueue.Dequeue();
                                Send(dataToSend);
                            }
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

            if (isSending)
            {
                //push to queue
                dataQueue.Enqueue(dataToSend);
                return;
            }

            isSending = true; //start sending
            //---------------------------------------------
            //format data for websocket client
            byte[] outputData = CreateSendBuffer(dataToSend);
            sockAsyncSender.SetBuffer(outputData, 0, outputData.Length);
            clientSocket.SendAsync(this.sockAsyncSender);
        }
        static byte[] CreateSendBuffer(string msg)
        {
            byte[] data = null;
            using (MemoryStream ms = new MemoryStream())
            {
                //create data  

                byte b1 = ((byte)Fin.Final) << 7; //final
                //// FIN
                //Fin fin = (b1 & (1 << 7)) == (1 << 7) ? Fin.Final : Fin.More; 
                //// RSV1
                //Rsv rsv1 = (b1 & (1 << 6)) == (1 << 6) ? Rsv.On : Rsv.Off;  //on compress
                //// RSV2
                //Rsv rsv2 = (b1 & (1 << 5)) == (1 << 5) ? Rsv.On : Rsv.Off; 
                //// RSV3
                //Rsv rsv3 = (b1 & (1 << 4)) == (1 << 4) ? Rsv.On : Rsv.Off;


                //opcode: 1 = text
                b1 |= 1;

                byte[] dataToClient = Encoding.UTF8.GetBytes(msg);
                //if len <126  then               
                byte b2 = (byte)dataToClient.Length; // < 126
                //-----------------------------
                //no extened payload length
                //no mask key
                ms.WriteByte(b1);
                ms.WriteByte(b2);
                ms.Write(dataToClient, 0, dataToClient.Length);
                ms.Flush();
                //-----------------------------
                //mask : send to client no mask
                data = ms.ToArray();
                ms.Close();
            }

            return data;
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