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
        readonly WebSocketContext conn;

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
            byte[] dataToSend = CreateSendBuffer(content);
            sendIO.EnqueueOutputData(dataToSend, dataToSend.Length);
            sendIO.StartSendAsync();
        }
        internal void ResetAll()
        {
            //NeedFlush = false;
            bodyMs.Position = 0;
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
    }
}