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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{

    public class WebSocketServer
    {

        ConnHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler;
        Dictionary<int, WebSocketConnection> workingWebSocketConns = new Dictionary<int, WebSocketConnection>();

        public WebSocketServer(ConnHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            this.webSocketReqHandler = webSocketReqHandler;
        }
        internal bool CheckWebSocketUpgradeRequest(HttpContext httpConn)
        {
            HttpRequest httpReq = httpConn.HttpReq;
            HttpResponse httpResp = httpConn.HttpResp;

            string upgradeKey = httpReq.GetHeaderKey("Upgrade");
            if (upgradeKey != null && upgradeKey == "websocket")
            {
               
                string sec_websocket_key = httpReq.GetHeaderKey("Sec-WebSocket-Key"); 

                WebSocketConnection wbSocketConn = new WebSocketConnection(this, webSocketReqHandler);
                workingWebSocketConns.Add(wbSocketConn.ConnectionId, wbSocketConn);//add to working socket 

                Socket clientSocket = httpConn.RemoteSocket;
                httpConn.UnBindSocket(false);//unbind  but not close client socket          

                wbSocketConn.Bind(clientSocket); //move client socket to webSocketConn  
                wbSocketConn.SendUpgradeResponse(sec_websocket_key);
                 
                return true;
            }
            return false;
        }
     

    }


}