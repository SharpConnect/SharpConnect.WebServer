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
using System.Net.Sockets;
using System.Text;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{



    public class WebSocketRequest
    {
        RecvIO recvIO;
        byte[] data;
        List<byte[]> moreFrames = new List<byte[]>();
        int socketRecvState;
        int remainingBytes;

        internal WebSocketRequest(RecvIO recvIO)
        {
            this.recvIO = recvIO;
        }
        public Opcode OpCode { get; set; }
        internal void SetRawBuffer(byte[] data)
        {
            this.data = data;
        }
        public void AddNewFrame(byte[] newFrame)
        {
            moreFrames.Add(newFrame);
        }
        public byte[] GetRawData()
        {
            return data;
        }
        public string ReadAsString()
        {
            if (data != null && this.OpCode == Opcode.Text)
            {
                return System.Text.Encoding.UTF8.GetString(data);
            }
            else
            {
                return null;
            }
        }
        public char[] ReadAsChars()
        {
            if (data != null && this.OpCode == Opcode.Text)
            {
                return System.Text.Encoding.UTF8.GetChars(data);
            }
            else
            {
                return null;
            }
        }
        public void Clear()
        {
            moreFrames.Clear();
            data = null;
        }

        //-------------------------------------------
        internal void LoadData()
        {

        }

    }
    public class WebSocketResponse : IDisposable
    {
        MemoryStream bodyMs = new MemoryStream();
        readonly WebSocketContext conn;
        int contentByteCount;
        SendIO sendIO;
        internal WebSocketResponse(WebSocketContext conn, SendIO sendIO)
        {
            this.conn = conn;
            this.sendIO = sendIO;
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
            conn.Send(content);

            ////write to output stream 
            //var bytes = Encoding.UTF8.GetBytes(content.ToCharArray());
            ////write to stream
            //bodyMs.Write(bytes, 0, bytes.Length);
            //contentByteCount += bytes.Length;
            //NeedFlush = true;
        }
        internal bool NeedFlush
        {
            get;
            set;
        }
        public void Flush()
        {
            byte[] dataToSend = bodyMs.ToArray();
            //----------------------------------------------------
            //copy data to send buffer
            //connProtocol.EnqueueOutputData(dataToSend, dataToSend.Length);
            sendIO.EnqueueOutputData(dataToSend, dataToSend.Length);
            //---------------------------------------------------- 
            ResetAll();
        }
        internal void ResetAll()
        {
            NeedFlush = false;
            bodyMs.Position = 0;
            contentByteCount = 0;
        }

    }

}