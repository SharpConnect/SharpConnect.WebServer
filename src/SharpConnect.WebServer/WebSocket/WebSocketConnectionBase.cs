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

namespace SharpConnect.WebServers
{

    class WebSocketConnectionBase : IDisposable
    {
        protected WebSocketResponse _webSocketResp;
        protected WebSocketProtocolParser _webSocketReqParser;
        protected ReqRespHandler<WebSocketRequest, WebSocketResponse> _webSocketReqHandler;
        protected readonly int _connectionId;
        static int s_connectionIdTotal;
        protected readonly bool _asClientContext;

        protected WebSocketConnectionBase(bool asClient)
        {
            _asClientContext = asClient;
            _connectionId = System.Threading.Interlocked.Increment(ref s_connectionIdTotal);
        }
        public string Name { get; set; }
        public int ConnectionId => _connectionId;
        public object GeneralCustomData { get; set; }
        public bool AsClientContext => _asClientContext;
        public string InitClientRequestUrl { get; set; }

        public virtual void Dispose()
        {
        }
        public virtual void Close() { }
        public void SetMessageHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> webSocketReqHandler)
        {
            _webSocketReqHandler = webSocketReqHandler;
        }

        public void Send(string data)
        {
            //send data to server
            //and wait for result 
            _webSocketResp.Write(data);
        }
    }
}