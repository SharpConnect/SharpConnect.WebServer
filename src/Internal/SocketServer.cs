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
        BufferManager bufferMx;
        //A Semaphore has two parameters, the initial number of available slots
        // and the maximum number of slots. We'll make them the same. 
        //This Semaphore is used to keep from going over max connection number. (It is not about 
        //controlling threading really here.)   
        Semaphore maxConnEnforcer;
        SocketServerSettings setting;

        public SocketServer(SocketServerSettings setting)
        {
            this.setting = setting;

#if DEBUG
            dbugLOG.StartLog();
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                dbugLOG.WriteLine("SocketListener constructor");
            }
            if (dbugLOG.watchThreads)   //for testing
            {
                dbugProcess = Process.GetCurrentProcess(); //for testing only             
                dbugDealWithThreadsForTesting("constructor");
            }

#endif
            this.NumberOfActiveRecvSendConnSession = 0; //for testing
            //Allocate memory for buffers. We are using a separate buffer space for
            //receive and send, instead of sharing the buffer space, like the Microsoft
            //example does.            

            this.bufferMx = setting.CreateBufferManager();
            this.recvSendArgPool = new SharedResoucePool<SocketConnection>(this.setting.NumberOfSaeaForRecvSend);
            this.acceptArgsPool = new SharedResoucePool<SocketAsyncEventArgs>(this.setting.MaxAcceptOps);

            // Create connections count enforcer
            this.maxConnEnforcer = new Semaphore(this.setting.MaxConnections, this.setting.MaxConnections);


            InitPools();
            InitListenSocket();
            StartAccept();
        }
        void InitPools()
        {
            //It is NOT mandatory that you preallocate them or reuse them. But, but it is 
            //done this way to illustrate how the API can 
            // easily be used to create ***reusable*** objects to increase server performance.

#if DEBUG
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                dbugLOG.WriteLine("Init method");
            }
            if (dbugLOG.watchThreads)   //for testing
            {
                dbugDealWithThreadsForTesting("Init()");
            }
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                dbugLOG.WriteLine("Starting creation of accept SocketAsyncEventArgs pool:");
            }
#endif
            // preallocate pool of SocketAsyncEventArgs objects for accept operations           
            for (int i = this.setting.MaxAcceptOps - 1; i >= 0; --i)
            {
                // add SocketAsyncEventArg to the pool                 
                this.acceptArgsPool.Push(CreateSocketAsyncEventArgsForAccept());
            }
            //------------------------------------------------------------------------------
            //The pool that we built ABOVE is for SocketAsyncEventArgs objects that do
            // accept operations. 
            //Now we will build a separate pool for SAEAs objects 
            //that do receive/send operations. One reason to separate them is that accept
            //operations do NOT need a buffer, but receive/send operations do. 
            //ReceiveAsync and SendAsync require
            //a parameter for buffer size in SocketAsyncEventArgs.Buffer.
            // So, create pool of SAEA objects for receive/send operations.

#if DEBUG
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                dbugLOG.WriteLine("Starting creation of receive/send SocketAsyncEventArgs pool");
            }

#endif

            //------------------------------------------------------------------
            //connection session: socket async = 1:1
            for (int i = this.setting.NumberOfSaeaForRecvSend - 1; i >= 0; --i)
            {
                //Allocate the SocketAsyncEventArgs object for this loop, 
                //to go in its place in the stack which will be the pool
                //for receive/send operation context objects.
                SocketAsyncEventArgs recvSendArg = new SocketAsyncEventArgs();
                //set buffer for newly created saArgs
                this.bufferMx.SetBufferTo(recvSendArg);
                //We can store data in the UserToken property of SAEA object. 
                SocketConnection connSession = setting.CreatePrebuiltReadWriteSession(recvSendArg);
                connSession.SetConnectionSesssionClosedHandler(ConnectionSessionClosed);

#if DEBUG
                connSession.dbugSetInfo(recvSendArgPool.dbugGetNewTokenId() + 1000000);
#endif

                //We'll have an object that we call DataHolder, that we can remove from
                //the UserToken when we are finished with it. So, we can hang on to the
                //DataHolder, pass it to an app, serialize it, or whatever. 
                recvSendArg.UserToken = connSession;

                // add this SocketAsyncEventArg object to the pool.
                this.recvSendArgPool.Push(connSession);
            }
        }
        void ConnectionSessionClosed(SocketConnection connSession)
        {
            //return recvSendArgs back to server   
            SocketAsyncEventArgs recvSendArg = connSession.GetAsyncSocketEventArgs();
#if DEBUG

            dbugSendLog(connSession, "CloseClientSocket");
            if (dbugLOG.watchThreads)   //for testing
            {

                dbugDealWithThreadsForTesting("CloseClientSocket()", connSession);
            }

#endif
            // do a shutdown before you close the socket
            try
            {
                dbugSendLog(connSession, "CloseClietSocket,Shutdown");
                recvSendArg.AcceptSocket.Shutdown(SocketShutdown.Both);
            }
            // throws if socket was already closed
            catch (Exception)
            {
                dbugSendLog(connSession, "CloseClientSocket, Shutdown catch");
            }

            //This method closes the socket and releases all resources, both
            //managed and unmanaged. It internally calls Dispose.
            recvSendArg.AcceptSocket.Close();
            // Put the SocketAsyncEventArg back into the pool,
            // to be used by another client. This 
            this.recvSendArgPool.Push(connSession);

            // decrement the counter keeping track of the total number of clients 
            //connected to the server, for testing
            Interlocked.Decrement(ref this.NumberOfActiveRecvSendConnSession);
#if DEBUG

            if (dbugLOG.watchConnectAndDisconnect)   //for testing
            {

                dbugLOG.WriteLine(connSession.dbugTokenId + " disconnected. "
                    + this.NumberOfActiveRecvSendConnSession + " client(s) connected.");
            }

#endif
            //Release Semaphore so that its connection counter will be decremented.
            //This must be done AFTER putting the SocketAsyncEventArg back into the pool,
            //or you can run into problems.
            this.maxConnEnforcer.Release();

        }
        void CleanUpOnExit()
        {
            DisposePools();
        }
        void DisposePools()
        {
            SocketAsyncEventArgs eventArgs;
            while (this.acceptArgsPool.Count > 0)
            {
                eventArgs = acceptArgsPool.Pop();
                eventArgs.Dispose();
            }
            while (this.recvSendArgPool.Count > 0)
            {
                SocketConnection conn = recvSendArgPool.Pop();
                conn.Dispose();
            }
        }

        //total clients connected to the server, excluding backlog
        internal int NumberOfActiveRecvSendConnSession;

#if DEBUG
        //__variables for testing ____________________________________________



        //****for testing threads
        Process dbugProcess; //for testing only
        ProcessThreadCollection dbugLiveThreadsInThisProcess;   //for testing 
        Dictionary<int, Thread> dbugManagedThreads = new Dictionary<int, Thread>();//for testing

        //object that will be used to lock the HashSet of thread references 
        //that we use for testing.
        object dbugLockerManagedThreads = new object();

        //****end variables for displaying what's happening with threads       

        //__END variables for testing ____________________________________________
        /// <summary>
        /// Display thread info.,Note that there is NOT a 1:1 ratio between managed threads and system (native) threads.,Use this one after the DataHoldingUserToken is available.
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="receiveSendToken"></param>
        void dbugDealWithThreadsForTesting(string methodName, SocketConnection receiveSendToken)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(" In " + methodName + ", receiveSendToken id " + receiveSendToken.dbugTokenId +
                ". Thread id " + Thread.CurrentThread.ManagedThreadId);
            sb.Append(dbugDealWithNewThreads());

            dbugLOG.WriteLine(sb.ToString());
        }



        /// <summary>
        /// Use this for testing, when there is NOT a UserToken yet. Use in SocketListener,method or Init().
        /// </summary>
        /// <param name="methodName"></param>
        void dbugDealWithThreadsForTesting(string methodName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(" In " + methodName + ", no usertoken yet. Thread id " + Thread.CurrentThread.ManagedThreadId);
            sb.Append(dbugDealWithNewThreads());
            dbugLOG.WriteLine(sb.ToString());
        }


        /// <summary>
        /// /Display thread info.,Use this one in method where AcceptOpUserToken is available.
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="acceptToken"></param>
        void dbugDealWithThreadsForTesting(string methodName, dbugAcceptOpUserToken acceptToken)
        {
            StringBuilder sb = new StringBuilder();
            string hString = hString = ". Socket handle " + acceptToken.dbugSocketHandleNumber;
            sb.Append(" In " + methodName + ", acceptToken id " + acceptToken.dbugTokenId + ". Thread id " + Thread.CurrentThread.ManagedThreadId + hString + ".");
            sb.Append(dbugDealWithNewThreads());
            dbugLOG.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Display thread info.
        /// called by DealWithThreadsForTesting
        /// </summary>
        /// <returns></returns>
        string dbugDealWithNewThreads()
        {

            StringBuilder sb = new StringBuilder();
            bool newThreadChecker = false;
            lock (this.dbugLockerManagedThreads)
            {
                int currentThId = Thread.CurrentThread.ManagedThreadId;
                if (!dbugManagedThreads.ContainsKey(currentThId))
                {
                    dbugManagedThreads.Add(currentThId, Thread.CurrentThread);
                    newThreadChecker = true;
                }
            }
            if (newThreadChecker == true)
            {

                //Display system threads
                //Note that there is NOT a 1:1 ratio between managed threads 
                //and system (native) threads.
                sb.Append("\r\n**** New managed thread.  Threading info:\r\nSystem thread numbers: ");
                dbugLiveThreadsInThisProcess = dbugProcess.Threads; //for testing only

                foreach (ProcessThread theNativeThread in dbugLiveThreadsInThisProcess)
                {
                    sb.Append(theNativeThread.Id.ToString() + ", ");
                }
                //Display managed threads
                //Note that there is NOT a 1:1 ratio between managed threads 
                //and system (native) threads.
                sb.Append("\r\nManaged threads that have been used: ");
                foreach (var kp in dbugManagedThreads)
                {
                    sb.Append(kp.Key + ",");
                }
                //Managed threads above were/are being used.
                //Managed threads below are still being used now.
                sb.Append("\r\nManagedthread.IsAlive true: ");
                foreach (var thd in dbugManagedThreads.Values)
                {
                    if (thd.IsAlive == true)
                    {
                        sb.Append(thd.ManagedThreadId.ToString() + ", ");
                    }
                }
                sb.Append("\r\nEnd thread info.");
            }
            return sb.ToString();
        }


#endif
    }
}
