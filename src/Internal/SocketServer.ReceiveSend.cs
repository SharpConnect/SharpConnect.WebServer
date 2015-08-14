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
        SocketAsyncEventArgsPool recvSendArgPool;

        /// <summary>
        ///  Does the normal destroying of sockets after 
        ///   we finish receiving and sending on a connection.        
        /// </summary>
        /// <param name="e"></param>
        internal void CloseClientSocket(SocketAsyncEventArgs e)
        {


#if DEBUG

            dbugSendLog(e, "CloseClientSocket");
            if (dbugLOG.watchThreads)   //for testing
            {
                var connSession = e.UserToken as ConnectionSession;
                dbugDealWithThreadsForTesting("CloseClientSocket()", connSession);
            }

#endif
            // do a shutdown before you close the socket
            try
            {
                dbugSendLog(e, "CloseClietSocket,Shutdown");
                e.AcceptSocket.Shutdown(SocketShutdown.Both);
            }
            // throws if socket was already closed
            catch (Exception)
            {
                dbugSendLog(e, "CloseClientSocket, Shutdown catch");
            }

            //This method closes the socket and releases all resources, both
            //managed and unmanaged. It internally calls Dispose.
            e.AcceptSocket.Close();
            // Put the SocketAsyncEventArg back into the pool,
            // to be used by another client. This 
            this.recvSendArgPool.Push(e);

            // decrement the counter keeping track of the total number of clients 
            //connected to the server, for testing
            Interlocked.Decrement(ref this.NumberOfAcceptedSockets);
#if DEBUG

            if (dbugLOG.watchConnectAndDisconnect)   //for testing
            {
                var connSession = e.UserToken as ConnectionSession;
                dbugLOG.WriteLine(connSession.dbugTokenId + " disconnected. "
                    + this.NumberOfAcceptedSockets + " client(s) connected.");
            }

#endif
            //Release Semaphore so that its connection counter will be decremented.
            //This must be done AFTER putting the SocketAsyncEventArg back into the pool,
            //or you can run into problems.
            this.maxConnEnforcer.Release();
        }

        [System.Diagnostics.Conditional("DEBUG")]
        static void dbugRecvLog(SocketAsyncEventArgs saArgs, string logMsg)
        {
#if DEBUG
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                var connSession = saArgs.UserToken as ConnectionSession;
                dbugLOG.WriteLine("recv_log:" + connSession.dbugTokenId + " " + logMsg);
            }
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        static void dbugSendLog(SocketAsyncEventArgs saArgs, string logMsg)
        {
#if DEBUG
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                var connSession = saArgs.UserToken as ConnectionSession;
                dbugLOG.WriteLine("send_log:" + connSession.dbugTokenId + " " + logMsg);
            }
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        static void dbugSendLog(ConnectionSession connSession, string logMsg)
        {
#if DEBUG
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                dbugLOG.WriteLine("send_log:" + connSession.dbugTokenId + " " + logMsg);
            }
#endif
        }
    }
}
