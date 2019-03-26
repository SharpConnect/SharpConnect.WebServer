//MIT, 2015-present, EngineKit
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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{
    public class WebSocketClient
    {

        Socket _clientSocket;
        WebSocketContext _wbContext;
        public WebSocketClient()
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
            if (uri.Host == "localhost")
            {
            }
            else
            {
                IPAddress.Parse(uri.Host);
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
