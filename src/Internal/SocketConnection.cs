//2010, CPOL, Stan Kirk
//2015, MIT, EngineKit

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Text; //for testing

namespace SharpConnect.Internal
{
    enum EndReceiveState
    {
        Error,
        NoMoreData,
        ContinueRead,
        Complete
    }
    enum EndSendState
    {
        Error,
        Continue,
        Complete
    }

    class ReceiveCarrier
    {
        readonly SocketAsyncEventArgs recvSendArgs;
        readonly int recvStartBufferOffset;
        public ReceiveCarrier(SocketAsyncEventArgs recvSendArgs)
        {
            this.recvSendArgs = recvSendArgs;
            this.recvStartBufferOffset = recvSendArgs.Offset;
        }
        public int BytesTransferred
        {
            get { return this.recvSendArgs.BytesTransferred; }
        }
        public void CopyTo(int srcIndex, byte[] destBuffer, int destIndex, int count)
        {
            Buffer.BlockCopy(recvSendArgs.Buffer,
                recvStartBufferOffset + srcIndex,
                destBuffer,
                destIndex, count);
        }
        /// <summary>
        /// copy all data to target
        /// </summary>
        /// <param name="targetBuffer"></param>
        public void CopyTo(byte[] destBuffer, int destIndex)
        {
            Buffer.BlockCopy(recvSendArgs.Buffer,
                recvStartBufferOffset,
                destBuffer,
                destIndex, BytesTransferred);
        }
        public byte[] ToArray()
        {
            byte[] buffer = new byte[this.BytesTransferred];
            CopyTo(buffer, 0);
            return buffer;
        }
        public byte ReadByte(int index)
        {
            return recvSendArgs.Buffer[this.recvStartBufferOffset + index];
        }
        public void ReadBytes(byte[] output, int start, int count)
        {
            Buffer.BlockCopy(recvSendArgs.Buffer,
                 recvStartBufferOffset + start,
                 output, 0, count);
        }
    }

    static class GlobalSessionNumber
    {

        internal static int mainTransMissionId = 10000;

        internal static int maxSimultaneousClientsThatWereConnected;

    }


    delegate void ConnectionSessionClosed(SocketConnection connSession);


    abstract class ConnectionSession
    {
        SocketConnection socketConn;
        public ConnectionSession()
        {
            KeepAlive = true;
        }
        public abstract void ResetRecvBuffer();
        public abstract void HandleRequest();
        public abstract EndReceiveState ProtocolRecvBuffer(ReceiveCarrier recvCarrier);
        public void Bind(SocketConnection socketConn)
        {
            this.socketConn = socketConn;
            socketConn.SetConnectionSession(this);
        }

        public void SetDataToSend(byte[] dataToSend, int count)
        {
            socketConn.SetDataToSend(dataToSend, count);
        }

        internal bool KeepAlive
        {
            get;
            set;
        }
#if DEBUG
        public int dbugTokenId
        {
            get
            {
                return socketConn.dbugTokenId;
            }
        }
#endif
    }


    /// <summary>
    /// connection session
    /// </summary>
    sealed class SocketConnection
    {

        //The session ID correlates with all the data sent in a connected session.
        //It is different from the transmission ID in the DataHolder, which relates
        //to one TCP message. A connected session could have many messages, if you
        //set up your app to allow it.

        readonly SocketAsyncEventArgs recvSendArgs;

        //recv
        ReceiveCarrier recvCarrier;
        readonly int startBufferOffset;
        readonly int recvBufferSize;

        //send
        readonly int initSentOffset;
        readonly int sendBufferSize;

        int sendingTargetBytes;
        int sendingTransferredBytes;
        byte[] currentSendingData = null;

        Queue<byte[]> sendingQueue = new Queue<byte[]>();
        ConnectionSessionClosed connectionSessionClosedHandler;
        ConnectionSession connSession;

        public SocketConnection(SocketAsyncEventArgs recvSendArgs, int recvBufferSize, int sendBufferSize)
        {
            //each recvSendArgs is created for this connection session only 

            this.recvSendArgs = recvSendArgs;
            this.recvCarrier = new ReceiveCarrier(recvSendArgs);
            this.startBufferOffset = recvSendArgs.Offset;

            this.recvBufferSize = recvBufferSize;
            this.initSentOffset = startBufferOffset + recvBufferSize;
            this.sendBufferSize = sendBufferSize;


            //this.KeepAlive = true;
            //Attach the SocketAsyncEventArgs object
            //to its event handler. Since this SocketAsyncEventArgs object is 
            //used for both receive and send operations, whenever either of those 
            //completes, the IO_Completed method will be called. 
            recvSendArgs.Completed += (object sender, SocketAsyncEventArgs e) =>
            {
                // This method is called whenever a receive or send operation completes.
                // Here "e" represents the SocketAsyncEventArgs object associated 
                //with the completed receive or send operation 
                //Any code that you put in this method will NOT be called if
                //the operation completes synchronously, which will probably happen when
                //there is some kind of socket error. 
#if DEBUG
                if (dbugLOG.watchThreads)   //for testing
                {
                    //dbugDealWithThreadsForTesting("ReceiveSendIO_Completed()", (ReadWriteSession)e.UserToken);
                }
#endif

                // determine which type of operation just completed and call the associated handler
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive: //receive data from client  
                        //dbugRecvLog(e, "ReceiveSendIO_Completed , Recv");
                        ProcessReceive();
                        break;

                    case SocketAsyncOperation.Send: //send data to client
                        //dbugRecvLog(e, "ReceiveSendIO_Completed , Send");
                        ProcessSend();
                        break;
                    default:
                        //This exception will occur if you code the Completed event of some
                        //operation to come to this method, by mistake.
                        throw new ArgumentException("The last operation completed on the socket was not a receive or send");
                }
            };
        }

        void ResetRecvBuffer()
        {
            connSession.ResetRecvBuffer();
        }
        public void SetConnectionSession(ConnectionSession connSession)
        {
            this.connSession = connSession;
        }


        /// <summary>
        /// receive data
        /// </summary>
        /// <param name="recvCarrier"></param>
        /// <returns>return true if finished</returns>
        EndReceiveState ProtocolRecvBuffer(ReceiveCarrier recvCarrier)
        {
            return connSession.ProtocolRecvBuffer(recvCarrier);
        }
        public void HandleRequest()
        {
            connSession.HandleRequest();
            StartSend(); //***
        }
        EndReceiveState EndReceive()
        {
            if (recvSendArgs.SocketError != SocketError.Success)
            {
                this.ResetRecvBuffer();
                //Jump out of the ProcessReceive method.
                return EndReceiveState.Error;
            }
            if (recvSendArgs.BytesTransferred == 0)
            {
                // If no data was received, close the connection. This is a NORMAL
                // situation that shows when the client has finished sending data.
                this.ResetRecvBuffer();
                return EndReceiveState.NoMoreData;
            }

            //--------------------
            return this.ProtocolRecvBuffer(this.recvCarrier);
        }


        internal void SetConnectionSesssionClosedHandler(ConnectionSessionClosed connectionSessionClosedHandler)
        {
            this.connectionSessionClosedHandler = connectionSessionClosedHandler;
        }

        internal void StartReceive()
        {
            //Set the buffer for the receive operation
            this.recvSendArgs.SetBuffer(this.startBufferOffset, this.recvBufferSize);
            // Post async receive operation on the socket. 
            //Socket.ReceiveAsync returns true if the I/O operation is pending. 
            //The SocketAsyncEventArgs.Completed event on the e parameter will be raised 
            //upon completion of the operation. So, true will cause the IO_Completed
            //method to be called when the receive operation completes. 

            //That's because of the event handler we created when building
            //the pool of SocketAsyncEventArgs objects that perform receive/send.
            //It was the line that said
            //eventArgObjectForPool.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);

            //Socket.ReceiveAsync returns false if I/O operation completed synchronously. 
            //In that case, the SocketAsyncEventArgs.Completed event on the e parameter 
            //will not be raised and the e object passed as a parameter may be 
            //examined immediately after the method call 
            //returns to retrieve the result of the operation.
            // It may be false in the case of a socket error. 
            if (!recvSendArgs.AcceptSocket.ReceiveAsync(recvSendArgs))
            {
                //If the op completed synchronously, we need to call ProcessReceive 
                //method directly. This will probably be used rarely, as you will 
                //see in testing. 
                ProcessReceive();
            }
        }
        void CloseClientSocket()
        {
            connectionSessionClosedHandler(this);
        }
        void ProcessReceive()
        {
            // This method is invoked by the IO_Completed method
            // when an asynchronous receive operation completes. 
            // If the remote host closed the connection, then the socket is closed.
            // Otherwise, we process the received data. And if a complete message was
            // received, then we do some additional processing, to 
            // respond to the client. 
            switch (this.EndReceive())
            {
                case EndReceiveState.Error:
                    //dbugRecvLog(recvSendArg, "ProcessReceive ERROR, receiveSendToken");
                    CloseClientSocket();
                    return;
                case EndReceiveState.NoMoreData:
                    //dbugRecvLog(recvSendArg, "ProcessReceive NO DATA");
                    //if close 
                    CloseClientSocket();
                    return;
                case EndReceiveState.ContinueRead:
                    //continue read
                    StartReceive();  //again
                    return;
                case EndReceiveState.Complete:

                    //read is complete
                    //handle read data 
#if DEBUG
                    //dbugRecvLog(recvSendArg, " Message in DataHolder = " + connSession.dbugGetDataInHolder());
                    //for testing only
                    if (dbugLOG.msDelayAfterGettingMessage > -1)
                    {
                        //A Thread.Sleep here can be used to simulate delaying the 
                        //return of the SocketAsyncEventArgs object for receive/send
                        //to the pool. Simulates doing some work here.
                        //dbugRecvLog(recvSendArg, " waiting after read");
                        Thread.Sleep(dbugLOG.msDelayAfterGettingMessage);
                    }
#endif
                    //*******
                    // Pass the DataHolder object to the Mediator here. The data in
                    // this DataHolder can be used for all kinds of things that an
                    // intelligent and creative person like you might think of. ***
                    //******* 
                    HandleRequest();
                    ////send data to cilent
                    //StartSend();
                    return;
            }
        }

        //--------------------------------------------------------------------------------
        internal void SetDataToSend(byte[] dataToSend, int count)
        {
            if (currentSendingData == null)
            {
                currentSendingData = dataToSend;
                sendingTargetBytes = count;
            }
            else
            {
                //add to queue
                sendingQueue.Enqueue(dataToSend);
            }
        }

        internal void StartSend()
        {
            //dbugSendLog(connSession.dbugGetAsyncSocketEventArgs(), "start send ...");
            //Set the buffer. You can see on Microsoft's page at 
            //http://msdn.microsoft.com/en-us/library/system.net.sockets.socketasynceventargs.setbuffer.aspx
            //that there are two overloads. One of the overloads has 3 parameters.
            //When setting the buffer, you need 3 parameters the first time you set it,
            //which we did in the Init method. The first of the three parameters
            //tells what byte array to use as the buffer. After we tell what byte array
            //to use we do not need to use the overload with 3 parameters any more.
            //(That is the whole reason for using the buffer block. You keep the same
            //byte array as buffer always, and keep it all in one block.)
            //Now we use the overload with two parameters. We tell 
            // (1) the offset and
            // (2) the number of bytes to use, starting at the offset.

            //The number of bytes to send depends on whether the message is larger than
            //the buffer or not. If it is larger than the buffer, then we will have
            //to post more than one send operation. If it is less than or equal to the
            //size of the send buffer, then we can accomplish it in one send op. 


            //send this data first
            int remaining = this.sendingTargetBytes - this.sendingTransferredBytes;
            if (remaining <= this.sendBufferSize)
            {
                recvSendArgs.SetBuffer(this.initSentOffset, remaining);
                //*** copy from src to dest
                if (currentSendingData != null)
                {
                    Buffer.BlockCopy(this.currentSendingData, //src
                        this.sendingTransferredBytes,
                        recvSendArgs.Buffer, //dest
                        this.initSentOffset,
                        remaining);
                }
            }
            else
            {
                //We cannot try to set the buffer any larger than its size.
                //So since receiveSendToken.sendBytesRemainingCount > BufferSize, we just
                //set it to the maximum size, to send the most data possible.
                recvSendArgs.SetBuffer(this.initSentOffset, this.sendBufferSize);
                //Copy the bytes to the buffer associated with this SAEA object.
                Buffer.BlockCopy(this.currentSendingData,
                    this.sendingTransferredBytes,
                    recvSendArgs.Buffer,
                    this.initSentOffset,
                    this.sendBufferSize);

                //We'll change the value of sendUserToken.sendBytesRemainingCount
                //in the ProcessSend method.
            }


            if (!recvSendArgs.AcceptSocket.SendAsync(recvSendArgs))
            {
                //dbugSendLog(connSession.dbugGetAsyncSocketEventArgs(), "start send( not async)");
                ProcessSend();
            }
        }

        void ProcessSend()
        {
            // This method is called by I/O Completed() when an asynchronous send completes.  
            // If all of the data has been sent, then this method calls StartReceive
            //to start another receive op on the socket to read any additional 
            // data sent from the client. If all of the data has NOT been sent, then it 
            //calls StartSend to send more data.          

            // dbugSendLog(connSession, "ProcessSend");
            switch (EndSend())
            {
                case EndSendState.Error:
                    CloseClientSocket();
                    return;
                case EndSendState.Continue:
                    // So let's loop back to StartSend().
                    StartSend();
                    return;
                case EndSendState.Complete:
                    //finished send
                    // If we are within this if-statement, then all the bytes in
                    // the message have been sent. -> so .. just StartReceiveFromClient()
                    if (connSession.KeepAlive)
                    {

                        StartReceive();
                    }
                    else
                    {
                        CloseClientSocket();
                    }
                    return;
            }
        }
        EndSendState EndSend()
        {
            if (recvSendArgs.SocketError == SocketError.Success)
            {
                //success !                 

                this.sendingTransferredBytes += recvSendArgs.BytesTransferred;
                if ((this.sendingTargetBytes - sendingTransferredBytes) <= 0)
                {
                    //check if no other data in chuck 
                    if (sendingQueue.Count > 0)
                    {
                        //move new chunck to current Sending data
                        this.currentSendingData = sendingQueue.Dequeue();
                        this.sendingTargetBytes = currentSendingData.Length;
                        this.sendingTransferredBytes = 0;
                        return EndSendState.Continue;
                    }
                    else
                    {
                        //no data
                        ResetSentBuffer();
                        ResetRecvBuffer();
                        return EndSendState.Complete;
                    }
                }
                else
                {

                    return EndSendState.Continue;
                }
            }
            else
            {
                //error, socket error
                ResetSentBuffer();
                ResetRecvBuffer();
                return EndSendState.Error;
            }
        }
        void ResetSentBuffer()
        {
            currentSendingData = null;
            sendingTransferredBytes = 0;
            sendingTargetBytes = 0;

        }

        internal SocketAsyncEventArgs GetAsyncSocketEventArgs()
        {
            return recvSendArgs;
        }
        public void Dispose()
        {
            this.recvSendArgs.Dispose();
        }

        internal void AcceptSocket(Socket socket)
        {
            this.recvSendArgs.AcceptSocket = socket;
        }

        internal System.Net.EndPoint RemoteEndPoint
        {
            get
            {
                return this.recvSendArgs.AcceptSocket.RemoteEndPoint;
            }
        }

#if DEBUG
        internal static int dbug_s_mainSessionId = 1000000000;
        /// <summary>
        /// create new session id
        /// </summary>
        internal void dbugCreateSessionId()
        {
            //new session id
            _dbugSessionId = Interlocked.Increment(ref dbug_s_mainSessionId);
        }
        public Int32 dbugSessionId
        {
            get
            {
                return this._dbugSessionId;
            }
        }
        int _dbugSessionId;
        public void dbugSetInfo(int tokenId)
        {
            this._dbugTokenId = tokenId;
        }
        public Int32 dbugTokenId
        {
            //Let's use an ID for this object during testing, just so we can see what
            //is happening better if we want to.

            get
            {
                return this._dbugTokenId;
            }
        }

        int _dbugTokenId; //for testing only    


        internal System.Net.EndPoint dbugGetRemoteEndpoint()
        {
            return recvSendArgs.AcceptSocket.RemoteEndPoint;
        }

#endif

    }
}
