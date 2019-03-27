﻿//MIT, 2018-present, EngineKit and contributors

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
        protected WebSocketConnectionBase _wbsocketConn;
        protected ReqRespHandler<WebSocketRequest, WebSocketResponse> _websocketHandler;
        public void SetHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> websocketHandler)
        {
            //set external msg handler
            _websocketHandler = websocketHandler;
        }

        protected static StringBuilder CreateWebSocketUpgradeReq(string host, string url)
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
            _wbsocketConn.Send(data);
        }
    }
}