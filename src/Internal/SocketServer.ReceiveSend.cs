//2010, CPOL, Stan Kirk
//2015, MIT, EngineKit

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Text;
using System.Diagnostics;

namespace SharpConnect.Internal
{
    partial class SocketServer
    {
        /// <summary>
        /// reuseable AsyncEventArgs pool for  receive/send ops
        /// </summary>
        SharedResoucePool<SocketConnection> recvSendArgPool;

        [System.Diagnostics.Conditional("DEBUG")]
        static void dbugRecvLog(SocketConnection socketConn, string logMsg)
        {
#if DEBUG
            if (dbugLOG.watchProgramFlow)   //for testing
            {

                dbugLOG.WriteLine("recv_log:" + socketConn.dbugTokenId + " " + logMsg);
            }
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        static void dbugSendLog(SocketConnection socketConn, string logMsg)
        {
#if DEBUG
            if (dbugLOG.watchProgramFlow)   //for testing
            {

                dbugLOG.WriteLine("send_log:" + socketConn.dbugTokenId + " " + logMsg);
            }
#endif
        }
    }
}
