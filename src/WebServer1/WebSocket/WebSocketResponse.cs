//2015-2016, MIT, EngineKit 
/* The MIT License
*
* Copyright (c) 2012-2015 sta.blockhead
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
using System.Text;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{
    public class WebSocketResponse : IDisposable
    {
        MemoryStream bodyMs = new MemoryStream();
        readonly WebSocketContext conn;
        ISendIO sendIO;
        Random _rdForMask;
        internal WebSocketResponse(WebSocketContext conn)
        {
            this.conn = conn;
            this.sendIO = conn;
            if (conn.AsClientContext)
            {
                _rdForMask = new Random();
            }
        }
        public int ConnectionId
        {
            get
            {
                return conn.ConnectionId;
            }
        }
        public WebSocketContext OwnerContext
        {
            get { return this.conn; }
        }
        public void Dispose()
        {
            if (bodyMs != null)
            {
                bodyMs.Dispose();
                bodyMs = null;
            }
        }

        public void Write(string content)
        {
            int maskKey = conn.AsClientContext ? _rdForMask.Next() : 0;
            byte[] dataToSend = CreateSendBuffer(content, maskKey);
            sendIO.EnqueueSendingData(dataToSend, dataToSend.Length);
            sendIO.SendIOStartSend();
        }
        public int SendQueueCount
        {
            get
            {
                return sendIO.QueueCount;
            }
        }
        static void MaskAgain(byte[] data, byte[] key)
        {
            for (int i = data.Length - 1; i >= 0; --i)
            {
                data[i] ^= key[i % 4];
            }
        }
        static byte[] CreateSendBuffer(string msg, int maskKey)
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

                //-------------
                //opcode: 1 = text
                b1 |= 1;
                //-------------


                byte[] sendingData = Encoding.UTF8.GetBytes(msg);
                //if len <126  then               
                int dataLen = sendingData.Length;

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

                ms.WriteByte(b1);
                bool sendToServer = maskKey != 0;
                byte[] maskKeyBuffer = null;
                if (sendToServer)
                {
                    maskKeyBuffer = new byte[]
                    {
                        (byte)((maskKey>>24)&0xff),
                        (byte)((maskKey>>16)&0xff),
                        (byte)((maskKey>>8)&0xff),
                        (byte)((maskKey)&0xff)
                    };
                    MaskAgain(sendingData, maskKeyBuffer);
                }

                if (dataLen < 126)
                {
                    //-----------------------------
                    //no extened payload length
                    //no mask key when sending data to client
                    //BUT when we send from client to server => must use mask
                    byte b2 = (byte)(sendToServer ?
                                (dataLen | (1 << 7)) : //to server
                                 dataLen); // < 126 //to client 
                    ms.WriteByte(b2);
                }
                else if (dataLen < ushort.MaxValue)
                {
                    //If 126, the following 2 bytes interpreted as a
                    //16-bit unsigned integer are the payload length 
                    ms.WriteByte((byte)(sendToServer ? (126 | 1 << 7) : 126));
                    //use 2 bytes for dataLen  
                    ms.WriteByte((byte)(dataLen >> 8));
                    ms.WriteByte((byte)(dataLen & 0xff));
                }
                else
                {
                    //If 127, the
                    //following 8 bytes interpreted as a 64-bit unsigned integer (the
                    //most significant bit MUST be 0) are the payload length 
                    //this version we limit data len < int.MaxValue
                    if (dataLen > int.MaxValue)
                    {
                        throw new NotSupportedException();
                    }
                    //-----------------------------------------

                    ms.WriteByte((byte)(sendToServer ? (127 | 1 << 7) : 127));
                    //use 8 bytes for dataLen  
                    //so... first 4 bytes= 0

                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    ms.WriteByte((byte)((dataLen >> 24) & 0xff));
                    ms.WriteByte((byte)((dataLen >> 16) & 0xff));
                    ms.WriteByte((byte)((dataLen >> 8) & 0xff));
                    ms.WriteByte((byte)(dataLen & 0xff));
                }
                if (sendToServer)
                {
                    ms.Write(maskKeyBuffer, 0, 4);
                }
                ms.Write(sendingData, 0, sendingData.Length);
                ms.Flush();
                //-----------------------------
                //mask : send to client no mask
                data = ms.ToArray();
                ms.Close();
            }

            return data;
        }
    }
}