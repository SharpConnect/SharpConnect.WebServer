//2015-2016, MIT, EngineKit 
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
        RecvIO recvIO;
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
    }
  

}