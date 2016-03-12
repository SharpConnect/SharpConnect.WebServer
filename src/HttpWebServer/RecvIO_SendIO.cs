//2010, CPOL, Stan Kirk
//2015-2016, MIT, EngineKit

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Text; //for testing

namespace SharpConnect.Internal
{
    enum RecvEventCode
    {
        SocketError,
        HasSomeData,
        NoMoreReceiveData,

    }
    class RecvIO
    {
        //receive
        //req 
        readonly int recvStartOffset;
        readonly int recvBufferSize;
        readonly SocketAsyncEventArgs recvArgs;
        Action<RecvEventCode> recvNotify;

        public RecvIO(SocketAsyncEventArgs recvArgs, int recvStartOffset, int recvBufferSize, Action<RecvEventCode> recvNotify)
        {
            this.recvArgs = recvArgs;
            this.recvStartOffset = recvStartOffset;
            this.recvBufferSize = recvBufferSize;
            this.recvNotify = recvNotify;
        }

        public byte ReadByte(int index)
        {
            return recvArgs.Buffer[this.recvStartOffset + index];
        }
        public void ReadTo(int srcIndex, byte[] destBuffer, int destIndex, int count)
        {
            Buffer.BlockCopy(recvArgs.Buffer,
                recvStartOffset + srcIndex,
                destBuffer,
                destIndex, count);
        }
        public void ReadTo(int srcIndex, byte[] destBuffer, int count)
        {
            Buffer.BlockCopy(recvArgs.Buffer,
                recvStartOffset + srcIndex,
                destBuffer,
                0, count);
        }
        public void ReadTo(int srcIndex, MemoryStream ms, int count)
        {
            ms.Write(recvArgs.Buffer,
                recvStartOffset + srcIndex,
                count);
        }
        public byte[] ReadToBytes()
        {
            int bytesTransfer = recvArgs.BytesTransferred;
            byte[] destBuffer = new byte[bytesTransfer];

            Buffer.BlockCopy(recvArgs.Buffer,
                recvStartOffset,
                destBuffer,
                0, bytesTransfer);
            return destBuffer;
        }

        public void ProcessReceive()
        {
            // This method is invoked by the IO_Completed method
            // when an asynchronous receive operation completes. 
            // If the remote host closed the connection, then the socket is closed.
            // Otherwise, we process the received data. And if a complete message was
            // received, then we do some additional processing, to 
            // respond to the client.  
            //1. socket error
            if (recvArgs.SocketError != SocketError.Success)
            {

                recvNotify(RecvEventCode.SocketError);
                return;
            }
            //2. no more receive 
            if (recvArgs.BytesTransferred == 0)
            {
                recvNotify(RecvEventCode.NoMoreReceiveData);
                return;
            }
            recvNotify(RecvEventCode.HasSomeData);
        }
        public void StartReceive()
        {

            recvArgs.SetBuffer(this.recvStartOffset, this.recvBufferSize);
            recvArgs.AcceptSocket.ReceiveAsync(recvArgs);
        }
        public void StartReceive(byte[] buffer, int len)
        {
            recvArgs.SetBuffer(buffer, 0, len);
            recvArgs.AcceptSocket.ReceiveAsync(recvArgs);
        }
        public int BytesTransferred
        {
            get { return recvArgs.BytesTransferred; }
        }
    }

    enum SendIOEventCode
    {
        SendComplete,
        SocketError,
    }

    class SendIO
    {
        //send,
        //resp 
        readonly int sendStartOffset;
        readonly int sendBufferSize;
        readonly SocketAsyncEventArgs sendArgs;

        int sendingTargetBytes; //target to send
        int sendingTransferredBytes; //has transfered bytes
        byte[] currentSendingData = null;
        Queue<byte[]> sendingQueue = new Queue<byte[]>();
        Action<SendIOEventCode> notify;

        bool isSending;

        public SendIO(SocketAsyncEventArgs sendArgs, int sendStartOffset, int sendBufferSize, Action<SendIOEventCode> notify)
        {
            this.sendArgs = sendArgs;
            this.sendStartOffset = sendStartOffset;
            this.sendBufferSize = sendBufferSize;
            this.notify = notify;
        }
        public void ResetBuffer()
        {
            currentSendingData = null;
            sendingTransferredBytes = 0;
            sendingTargetBytes = 0;
        }
        public void EnqueueOutputData(byte[] dataToSend, int count)
        {
            if (currentSendingData == null)
            {
                currentSendingData = dataToSend;
                sendingTargetBytes = count;
            }
            else
            {
                sendingQueue.Enqueue(dataToSend);
            }
        }
        public void StartSendAsync()
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

            if (isSending)
            {
                return;
            }

            isSending = true;

            //send this data first
            int remaining = this.sendingTargetBytes - this.sendingTransferredBytes;
            if (remaining == 0)
            {
                //no data to send ?
                isSending = false;
                return;
            }

            if (remaining <= this.sendBufferSize)
            {
                sendArgs.SetBuffer(this.sendStartOffset, remaining);
                //*** copy from src to dest
                if (currentSendingData != null)
                {
                    Buffer.BlockCopy(this.currentSendingData, //src
                        this.sendingTransferredBytes,
                        sendArgs.Buffer, //dest
                        this.sendStartOffset,
                        remaining);
                }
            }
            else
            {
                //We cannot try to set the buffer any larger than its size.
                //So since receiveSendToken.sendBytesRemainingCount > BufferSize, we just
                //set it to the maximum size, to send the most data possible.
                sendArgs.SetBuffer(this.sendStartOffset, this.sendBufferSize);
                //Copy the bytes to the buffer associated with this SAEA object.
                Buffer.BlockCopy(this.currentSendingData,
                    this.sendingTransferredBytes,
                    sendArgs.Buffer,
                    this.sendStartOffset,
                    this.sendBufferSize);
            }


            if (!sendArgs.AcceptSocket.SendAsync(sendArgs))
            {
                //dbugSendLog(connSession.dbugGetAsyncSocketEventArgs(), "start send( not async)");
                ProcessSend();
            }
        }
        public void ProcessSend()
        {
            // This method is called by I/O Completed() when an asynchronous send completes.  
            // If all of the data has been sent, then this method calls StartReceive
            //to start another receive op on the socket to read any additional 
            // data sent from the client. If all of the data has NOT been sent, then it 
            //calls StartSend to send more data.          
            // dbugSendLog(connSession, "ProcessSend"); 


            if (sendArgs.SocketError == SocketError.Success)
            {
                isSending = false;
                //success !                 
                this.sendingTransferredBytes += sendArgs.BytesTransferred;
                if ((this.sendingTargetBytes - sendingTransferredBytes) <= 0)
                {
                    //check if no other data in chuck 
                    if (sendingQueue.Count > 0)
                    {
                        //move new chunck to current Sending data
                        this.currentSendingData = sendingQueue.Dequeue();
                        this.sendingTargetBytes = currentSendingData.Length;
                        this.sendingTransferredBytes = 0;

                        //conitnue read 
                        //So let's loop back to StartSend().
                        StartSendAsync();
                        return;
                    }
                    else
                    {
                        //no data
                        ResetBuffer();
                        //notify no more data
                        notify(SendIOEventCode.SendComplete);

                        return;
                    }
                }
                else
                {

                    //conitnue read 
                    //So let's loop back to StartSend().
                    StartSendAsync();
                    return;
                }
            }
            else
            {
                isSending = false;
                //error, socket error
                ResetBuffer();
                notify(SendIOEventCode.SocketError);
            }
        }

        public void Reset()
        {
            sendingTargetBytes = sendingTransferredBytes = 0;
            currentSendingData = null;
            sendingQueue.Clear();
        }
    }

}