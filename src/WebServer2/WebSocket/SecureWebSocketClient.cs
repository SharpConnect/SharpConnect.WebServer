//MIT, 2018-present, EngineKit and contributors

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

using SharpConnect.Internal2;

namespace SharpConnect.WebServers.Server2
{
    /// <summary>
    /// web socket over secure protocol (ssl/tls)
    /// </summary>
    class SecureWebSocketClient
    {
        Socket _clientSocket;
        SecureWebSocketContext _wbContext;
        SockNetworkStream _sockNetworkStream;
        SecureSockNetworkStream _secureStream;

        public SecureWebSocketClient()
        {
            _wbContext = new SecureWebSocketContext(true);
            _wbContext.SetMessageHandler((req, resp) =>
            {
                //default
            });
        }
        public void Connect(string url, System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
        {
            Uri uri = new Uri(url);

            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            IPAddress ipAddress = IPAddress.Loopback;
            if (uri.Host != "localhost")
            {
                ipAddress = IPAddress.Parse(uri.Host);
            }

            //_clientSocket.Connect(ipAddress, uri.Port);
            //

            byte[] buffer1 = new byte[2048];
            byte[] buffer2 = new byte[2048];
            IOBuffer recvBuffer = new IOBuffer(buffer1, 0, buffer1.Length);
            IOBuffer sendBuffer = new IOBuffer(buffer2, 0, buffer2.Length);

            _sockNetworkStream = new SockNetworkStream(recvBuffer, sendBuffer);
            _secureStream = new SecureSockNetworkStream(_sockNetworkStream, cert, delegate { return true; }); //***
            _sockNetworkStream.Bind(_clientSocket);
            _clientSocket.Connect(ipAddress, uri.Port);
            //_secureStream.AuthenAsClient(uri.Host);
            _secureStream.AuthenAsClient(uri.Host, () =>
            {
                //--------------
                StringBuilder stbuilder = CreateWebSocketUpgradeReq(uri.AbsolutePath, uri.AbsolutePath + ":" + uri.Port);
                byte[] dataToSend = Encoding.ASCII.GetBytes(stbuilder.ToString());
                //int totalCount = dataToSend.Length;
                //int sendByteCount = _clientSocket.Send(dataToSend);
                _wbContext.Bind(_secureStream, dataToSend);
            });

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