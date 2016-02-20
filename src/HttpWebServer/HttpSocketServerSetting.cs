//2015, MIT, EngineKit
using System.Net.Sockets;
using System.Net;
using SharpConnect.Internal;

namespace SharpConnect.WebServer
{
    public delegate void HttpRequestHandler(HttpRequest req, HttpResponse resp);

    public class HttpSocketServerSetting : SocketServerSettings
    {
        BufferManager bufferMan;
        public event HttpRequestHandler RequestHandler;

        public HttpSocketServerSetting(int maxConnections,
            int numOfSocketAsyncEventArgsInPool,
            int backlog, int maxSimultaneousAcceptOps,
            IPEndPoint listenerEndPoint)
            : base(maxConnections, numOfSocketAsyncEventArgsInPool, backlog, maxSimultaneousAcceptOps,
             listenerEndPoint)
        {
            RecvBufferSize = 1024;
            SendBufferSize = 1024;
        }
        public int RecvBufferSize
        {
            get;
            set;
        }
        public int SendBufferSize
        {
            get;
            set;
        }
        internal override BufferManager CreateBufferManager()
        {
            if (bufferMan == null)
            {
                int totalBufferForEachRecvSendArgs = RecvBufferSize + SendBufferSize; //recv + send
                bufferMan = new BufferManager(
                    totalBufferForEachRecvSendArgs * NumberOfSaeaForRecvSend,
                    totalBufferForEachRecvSendArgs);
            }
            return bufferMan;
        }
        internal override SocketConnection CreatePrebuiltReadWriteSession(SocketAsyncEventArgs recvSendArgs)
        {
            SocketConnection socketConnection = new SocketConnection(recvSendArgs, RecvBufferSize, SendBufferSize);
            HttpConnectionSession httpConnSession = new HttpConnectionSession(RequestHandler);
            httpConnSession.Bind(socketConnection);//2 ways bind 
            return socketConnection;
        }
    }

}