//2015-2016, MIT, EngineKit 
using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{
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