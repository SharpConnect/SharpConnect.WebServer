//2015, MIT, EngineKit 
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
        byte[] data;
        List<byte[]> moreFrames = new List<byte[]>();
        internal WebSocketRequest()
        {
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
    }
    public class WebSocketResponse : IDisposable
    {
        MemoryStream bodyMs = new MemoryStream();
        readonly WebSocketConnection conn;
        int contentByteCount;
        SendIO sendIO;
        internal WebSocketResponse(WebSocketConnection conn, SendIO sendIO)
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