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
    public class WebSocketConnection : IDisposable
    {
        readonly WebSocketServer webSocketServer;
        readonly SocketAsyncEventArgs sockAsyncSender;
        readonly SocketAsyncEventArgs sockAsyncListener;

        ConnHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler;
        Socket clientSocket;

        byte[] outputData = new byte[512];


        WebSocketRequest webSocketReq = new WebSocketRequest();
        WebSocketResponse webSocketResp;

        Encoding utf8Enc = new UTF8Encoding();

        RecvIO recvIO;
        SendIO sendIO;

        /// <summary>
        /// TODO: review data type
        /// </summary>
        Queue<string> dataQueue = new Queue<string>();
        bool isSending = false;
        int connectionId;
        static int connectionIdTotal;

        public WebSocketConnection(WebSocketServer webSocketServer, ConnHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            this.webSocketReqHandler = webSocketReqHandler;
            this.webSocketServer = webSocketServer;
            connectionId = System.Threading.Interlocked.Increment(ref  connectionIdTotal);
            //-------------------
            sockAsyncSender = new SocketAsyncEventArgs();
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
                        ParseWebSocketRequestHeader();
                        webSocketReqHandler(webSocketReq, webSocketResp);

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
        void send_Complete()
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

            //int len = dataToSend.Length;
            //outputData[0] = (byte)len;
            //outputData[1] = (byte)(len >> 8);
            //outputData[2] = (byte)(len >> 16);
            //outputData[3] = (byte)(len >> 24);
            ////---------------------------------------------
            //char[] charBuffer = dataToSend.ToCharArray();
            //int byteCount = utf8Enc.GetBytes(charBuffer, 0, charBuffer.Length, outputData, 4);
            //if (byteCount >= 512)
            //{
            //}
            sockAsyncSender.SetBuffer(outputData, 0, outputData.Length);

            ParseWebSocketRequestHeader();

            clientSocket.SendAsync(this.sockAsyncSender);
        }

        int socketRecvState;
        int remainingBytes;

        int ParseWebSocketRequestHeader()
        {
            int readpos = 0;


            if (socketRecvState == 1)
            {
                //skip remaining bytes
                readpos = remainingBytes;
                socketRecvState = 0;
                remainingBytes = 0;
            }
            else if (socketRecvState == 2)
            {
            }

            int txByteCount = recvIO.BytesTransferred;

            int round = 0;
            for (; ; )
            {
                round++;

                //just read  

                byte b1 = recvIO.ReadByte(readpos);
                readpos++;

                if (readpos >= txByteCount)
                {
                    break;
                }

                // FIN
                Fin fin = (b1 & (1 << 7)) == (1 << 7) ? Fin.Final : Fin.More;

                // RSV1
                Rsv rsv1 = (b1 & (1 << 6)) == (1 << 6) ? Rsv.On : Rsv.Off;

                // RSV2
                Rsv rsv2 = (b1 & (1 << 5)) == (1 << 5) ? Rsv.On : Rsv.Off;

                // RSV3
                Rsv rsv3 = (b1 & (1 << 4)) == (1 << 4) ? Rsv.On : Rsv.Off;

                // Opcode
                byte opcode = (byte)(b1 & 0x0f);//4 bits 

                //----------------------------------------------------------  
                byte b2 = recvIO.ReadByte(readpos); //mask
                readpos++;
                if (readpos >= txByteCount)
                {
                    //can't go next 
                    socketRecvState = 2;
                    break;
                }

                // MASK
                Mask mask = (b2 & (1 << 7)) == (1 << 7) ? Mask.On : Mask.Off;
                if (mask == Mask.Off)
                {

                    throw new NotSupportedException();
                }
                // Payload Length
                byte payloadLen = (byte)(b2 & 0x7f);
                //----------------------------------------------------------
                //translate opcode ....
                string errCode = null;
                switch ((Opcode)opcode)
                {
                    case Opcode.Cont:
                        {
                        } break;
                    case Opcode.Text: //this is data
                    case Opcode.Binary: //this is data
                        {
                            if (rsv1 == Rsv.On)
                            {
                                errCode = "A non data frame is compressed.";
                            }
                        } break;
                    case Opcode.Close: //control
                    case Opcode.Ping: //control
                    case Opcode.Pong: //control
                        {
                            if (fin == Fin.More)
                            {
                                errCode = "A control frame is fragmented.";
                            }
                            else if (payloadLen > 125)
                            {
                                errCode = "A control frame has a long payload length.";
                            }
                        } break;
                    default:
                        {
                            errCode = "An unsupported opcode.";
                        } break;
                }
                //----------------------------------------------------------
                if (errCode != null)
                {
                    //report error
                    throw new NotSupportedException();
                }
                //---------------------------------------------------------- 
                int extendedPayloadLen = payloadLen < 126 ? 0 : (payloadLen == 126 ? 2 : 8);
                byte[] fullPayloadLengthBuffer = null;
                if (extendedPayloadLen > 0)
                {
                    if (readpos + extendedPayloadLen >= txByteCount)
                    {
                        break;
                    }
                    fullPayloadLengthBuffer = new byte[extendedPayloadLen];
                    recvIO.ReadTo(readpos, fullPayloadLengthBuffer, extendedPayloadLen);
                    readpos += extendedPayloadLen;
                }
                else
                {
                }
                //---------------------------------------------------------------
                //mask key
                int maskLen = (mask == Mask.On) ? 4 : 0;
                byte[] maskKey = null;
                if (maskLen > 0)
                {
                    if (readpos + maskLen >= txByteCount)
                    {
                        break;
                    }
                    //read mask data                     
                    maskKey = new byte[maskLen];
                    recvIO.ReadTo(readpos, maskKey, maskLen);
                    readpos += maskLen;


                    //throw new WebSocketException("The masking key of a frame cannot be read from the stream.");
                }
                //----------------------------------------------------
                //control frame
                switch ((Opcode)opcode)
                {
                    default:
                        {
                            throw new NotSupportedException();
                        }
                    case Opcode.Close:
                        {
                            //close current protocol
                            //notify onclose ***
                            byte[] data = new byte[payloadLen];
                            recvIO.ReadTo(readpos, data, payloadLen);
                            readpos += payloadLen;

                            if (mask == Mask.On)
                            {
                                //unmask
                                MaskAgain(data, maskKey);
                            }
                            remainingBytes = 0;
                            socketRecvState = 0;
                        } break;
                    case Opcode.Ping:
                        {

                        } break;
                    case Opcode.Pong:
                        {

                        } break;
                    //data frame
                    case Opcode.Cont:
                    case Opcode.Binary:
                    case Opcode.Text:
                        {
                            //----------------------------------------------------
                            //find full payload length
                            ulong fullPayloadLen = GetFullPayloadLength(payloadLen, fullPayloadLengthBuffer);
                            if (fullPayloadLen < 127)
                            {
                                if ((readpos + (int)fullPayloadLen) >= txByteCount)
                                {
                                }


                                //check if we can get data by one loop ?
                                int readLen = 0;
                                if (readpos > txByteCount)
                                {

                                }
                                if (readpos + (int)fullPayloadLen > txByteCount)
                                {
                                    //then this is not complete data frame
                                    readLen = txByteCount - readpos;
                                    socketRecvState = 1;//not complete
                                    remainingBytes = (int)fullPayloadLen - readLen;
                                }
                                else
                                {
                                    readLen = (int)fullPayloadLen;
                                }

                                if (readLen < 0)
                                {

                                }


                                byte[] data = new byte[readLen];
                                recvIO.ReadTo(readpos, data, readLen);
                                readpos += readLen;
                                if (mask == Mask.On)
                                {
                                    //unmask
                                    MaskAgain(data, maskKey);
                                }


                                switch ((Opcode)opcode)
                                {
                                    case Opcode.Text:
                                        {
                                            webSocketReq.OpCode = Opcode.Text;
                                            if (round == 1)
                                            {
                                                webSocketReq.SetRawBuffer(data);
                                            }
                                            else
                                            {
                                                webSocketReq.AddNewFrame(data);
                                            }
                                        } break;
                                    case Opcode.Binary:
                                        {
                                            webSocketReq.OpCode = Opcode.Binary;
                                            if (round == 1)
                                            {
                                                webSocketReq.SetRawBuffer(data);
                                            }
                                            else
                                            {
                                                webSocketReq.AddNewFrame(data);
                                            }
                                        } break;
                                    case Opcode.Cont:

                                        if (round == 1)
                                        {
                                            webSocketReq.SetRawBuffer(data);
                                        }
                                        else
                                        {
                                            webSocketReq.AddNewFrame(data);
                                        }


                                        break;
                                    default:
                                        throw new NotSupportedException();
                                }
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        } break;
                }
            }

            if (readpos != txByteCount + 1)
            {
                if (socketRecvState != 2)
                {

                }
            }

            return readpos;
        }
        static ulong GetFullPayloadLength(int _payloadLength, byte[] fullPayloadLengthBuffer)
        {
            // Payload length:  7 bits, 7+16 bits, or 7+64 bits

            //The length of the "Payload data", in bytes: if 0-125, that is the
            //payload length.  If 126, the following 2 bytes interpreted as a
            //16-bit unsigned integer are the payload length.  If 127, the
            //following 8 bytes interpreted as a 64-bit unsigned integer (the
            //most significant bit MUST be 0) are the payload length.  Multibyte
            //length quantities are expressed in network byte order.  Note that
            //in all cases, the minimal number of bytes MUST be used to encode
            //the length, for example, the length of a 124-byte-long string
            //can't be encoded as the sequence 126, 0, 124.  The payload length
            //is the length of the "Extension data" + the length of the
            //"Application data".  The length of the "Extension data" may be
            //zero, in which case the payload length is the length of the
            //"Application data". 
            return _payloadLength < 126
                       ? (ulong)_payloadLength //use that length
                       : _payloadLength == 126
                         ? (ulong)(fullPayloadLengthBuffer[0] << 8 | fullPayloadLengthBuffer[1]) // 7+16 bits
                         : BitConverter.ToUInt64(fullPayloadLengthBuffer, 0);

        }

        static void MaskAgain(byte[] data, byte[] key)
        {
            int length = data.Length;
            for (int i = length - 1; i >= 0; --i)
            {
                data[i] ^= key[i % 4];
            }
        }

        byte[] CreateHelloDataToClient()
        {
            byte[] data = null;
            using (MemoryStream ms = new MemoryStream())
            {
                //create data  

                byte b1 = ((byte)Fin.Final) << 7;
                //// FIN
                //Fin fin = (b1 & (1 << 7)) == (1 << 7) ? Fin.Final : Fin.More; 
                //// RSV1
                //Rsv rsv1 = (b1 & (1 << 6)) == (1 << 6) ? Rsv.On : Rsv.Off; 
                //// RSV2
                //Rsv rsv2 = (b1 & (1 << 5)) == (1 << 5) ? Rsv.On : Rsv.Off; 
                //// RSV3
                //Rsv rsv3 = (b1 & (1 << 4)) == (1 << 4) ? Rsv.On : Rsv.Off;

                //opcode: 1 = text
                b1 |= 1;

                byte[] dataToClient = Encoding.UTF8.GetBytes("hello!");
                byte b2 = (byte)dataToClient.Length; // < 126
                //-----------------------------
                //no extened payload length
                //no mask key
                ms.WriteByte(b1);
                ms.WriteByte(b2);
                ms.Write(dataToClient, 0, dataToClient.Length);

                //-----------------------------
                //mask : send to client no mask
                data = ms.ToArray();
                ms.Close();
            }

            return data;
        }

        byte[] CreateSendBuffer(string msg)
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