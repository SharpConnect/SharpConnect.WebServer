//MIT, 2018-present, EngineKit and contributors

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SharpConnect.WebServers
{
    /// <summary>
    /// web socket client session
    /// </summary>
    abstract class WsClientSessionBase
    {
        protected Socket _clientSocket;
        protected IPAddress _hostIP;
        protected WebSocketConnectionBase _wbsocketConn;
        protected ReqRespHandler<WebSocketRequest, WebSocketResponse> _websocketHandler;
        public void SetHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> websocketHandler)
        {
            //set external msg handler
            _websocketHandler = websocketHandler;
        }
        public WebSocketContentCompression Compression { get; set; }

        protected static StringBuilder CreateWebSocketUpgradeReq(string host, string url, WebSocketContentCompression compression)
        {
            //GET / HTTP / 1.1
            //Host: 127.0.0.1
            //Connection: keep - alive
            //Accept: text / html
            //User - Agent: CSharpTests

            string sock_key = "12345";  //test only
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append($"GET {url} HTTP /1.1\r\n");
            stbuilder.Append("Host: " + host + "\r\n");
            stbuilder.Append("Upgrade: websocket\r\n"); //***

            switch (compression)
            {
                case WebSocketContentCompression.Deflate:
                    stbuilder.Append("Sec-WebSocket-Extensions: deflate-stream\r\n");
                    break;
                case WebSocketContentCompression.Gzip:
                    stbuilder.Append("Sec-WebSocket-Extensions: gzip-stream\r\n");
                    break;
            }

            stbuilder.Append("Sec-WebSocket-Key: " + sock_key);
            //end with \r\n\r\n
            stbuilder.Append("\r\n\r\n");
            return stbuilder;
        }
        /// <summary>
        /// send text data to the server
        /// </summary>
        /// <param name="data"></param>
        public void SendData(string data)
        {
            _wbsocketConn.Compression = Compression;
            _wbsocketConn.SendTextData(data);
        }
        public void SendBinaryData(byte[] data, int start, int len)
        {
            _wbsocketConn.Compression = Compression;
            _wbsocketConn.SendBinaryData(data, start, len);
        }
        protected void InitClientSocket(Uri uri)
        {

            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            _hostIP = IPAddress.Loopback;
            if (uri.Host != "localhost")
            {
                _hostIP = IPAddress.Parse(uri.Host);
            }
        }
    }
}