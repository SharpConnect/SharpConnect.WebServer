//MIT, 2018-present, EngineKit and contributors

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SharpConnect.WebServers.Server2
{
    /// <summary>
    /// web socket over secure protocol (ssl/tls)
    /// </summary>
    class SecureWebSocketClient
    {
        Socket _clientSocket;
        WebSocketContext _wbContext;
        public SecureWebSocketClient()
        {
            _wbContext = new WebSocketContext(true);
            _wbContext.SetMessageHandler((req, resp) =>
            {
                //default
            });
        }

        public void Connect(string url)
        {
            Uri uri = new Uri(url);
            //1. send websocket request upgrade protocol
            //2. 
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            IPAddress ipAddress = IPAddress.Loopback;
            if (uri.Host != "localhost")
            {
                ipAddress = IPAddress.Parse(uri.Host);
            }

            _clientSocket.Connect(ipAddress, uri.Port);
            //create http webreq  

            StringBuilder stbuilder = CreateWebSocketUpgradeReq(uri.AbsolutePath, uri.AbsolutePath + ":" + uri.Port);
            byte[] dataToSend = Encoding.ASCII.GetBytes(stbuilder.ToString());
            int totalCount = dataToSend.Length;
            int sendByteCount = _clientSocket.Send(dataToSend);

            if (sendByteCount != totalCount)
            {

            }

            //get first confirm server upgrade resp from server
            byte[] firstRespBuffer = new byte[1024];
            int firstMsg = _clientSocket.Receive(firstRespBuffer, 1024, SocketFlags.None);

            //****
            //add event listener to our socket  
            _wbContext.Bind(_clientSocket);
        }
        public void SetHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> websocketHandler)
        {
            //set external msg handler
            _wbContext.SetMessageHandler(websocketHandler);
        }
        /// <summary>
        /// send text data to the server
        /// </summary>
        /// <param name="data"></param>
        public void SendData(string data)
        {
            _wbContext.Send(data);
        }

        StringBuilder CreateWebSocketUpgradeReq(string host, string url)
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
    }
}