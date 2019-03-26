//2010, CPOL, Stan Kirk
//MIT, 2015-2016, EngineKit and contributors
//https://docs.microsoft.com/en-us/dotnet/framework/network-programming/socket-performance-enhancements-in-version-3-5

using System;
using System.Collections.Generic;
using System.IO;

namespace SharpConnect.Internal2
{


    class RecvIO
    {
        AbstractAsyncNetworkStream _networkStream;
        public RecvIO()
        {

        }
        public void Bind(AbstractAsyncNetworkStream networkStream)
        {
            _networkStream = networkStream;
        }
        public byte ReadByte(int index)
        {
            //read one byte from specific index from stream
            return _networkStream.GetByteFromBuffer(index);
        }
        public void CopyTo(int srcIndex, byte[] destBuffer, int count)
        {
            _networkStream.ReadBuffer(srcIndex, count, destBuffer, 0);
        }

        /// <summary>
        /// start new receive
        /// </summary>
        public void StartReceive()
        {
            _networkStream.ClearReceiveBuffer();
            _networkStream.StartReceive();
        }
        public int BytesTransferred => _networkStream.ByteReadTransfered;
        internal byte[] UnsafeGetInternalBuffer()
        {
            return null;
        }
    }



    struct IOBuffer
    {

        internal readonly byte[] _largeBuffer;
        readonly int _startAt;
        readonly int _len;
        int _readIndex;
        int x_writeIndex;

        //write then read

#if DEBUG
        bool _isSendIO;
        static int dbugTotalId;
        int debugId;
#endif

        public IOBuffer(byte[] largeBuffer, int beginAt, int len)
        {
#if DEBUG
            _isSendIO = false;
            debugId = dbugTotalId++;
            if (debugId == 2000)
            {

            }
#endif
            _largeBuffer = largeBuffer;
            _startAt = beginAt;
            _len = len;
            _readIndex = x_writeIndex = 0;
        }
#if DEBUG
        public bool IsSendIO => _isSendIO;
#endif
        public int BufferStartAtIndex => _startAt;
        public int BufferLength => _len;
        int _writeIndex
        {
            get => x_writeIndex;
            set
            {
                x_writeIndex = value;
            }
        }
        public void Reset()
        {
            _readIndex = _writeIndex = 0;
        }
        public void Reset2()
        {
            if (_readIndex == _writeIndex)
            {
                _readIndex = _writeIndex = 0;
            }
        }
        public void WriteBuffer(byte[] srcBuffer, int srcIndex, int count)
        {
            //copy data from srcBuffer and place to _largeBuffer
            //make sure that we don't run out-of-length

            if (_writeIndex + count < _len)
            {
                Buffer.BlockCopy(srcBuffer, srcIndex, _largeBuffer, _startAt + _writeIndex, count);
                _writeIndex += count;
#if DEBUG
                if (_writeIndex == 2048)
                {

                }
#endif
            }
            else
            {
                //out-of-range!
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// append new write byte count (when we share a large buffer with external object)
        /// </summary>
        /// <param name="newWriteBytes"></param>
        public void AppendWriteByteCount(int newWriteBytes)
        {
            if (newWriteBytes + _writeIndex > _len)
            {
                //throw new out-of-range
                throw new IndexOutOfRangeException();
            }
            _writeIndex += newWriteBytes;
#if DEBUG

            if (_writeIndex == 2048)
            {
                int dbugID = this.debugId;
            }
#endif
        }


        public void CopyBuffer(int readIndex, byte[] dstBuffer, int dstIndex, int count)
        {
            if (readIndex + count <= _writeIndex) //***
            {
                Buffer.BlockCopy(_largeBuffer, _startAt + readIndex, dstBuffer, _startAt + dstIndex, count);
                //not move readIndex in this case?
            }
            else
            {
                //out-of-range!
                throw new NotSupportedException();
            }
        }
        public byte CopyByte(int index)
        {
            return _largeBuffer[_startAt + _readIndex + index];
        }
        public void ReadBuffer(byte[] dstBuffer, int dstIndex, int count)
        {
            //read data from the latest pos
            if (_readIndex + count <= _writeIndex) //***
            {
                Buffer.BlockCopy(_largeBuffer, _startAt + _readIndex, dstBuffer, _startAt + dstIndex, count);
                _readIndex += count;
            }
            else
            {
                //out-of-range!
                throw new NotSupportedException();
            }
        }


        /// <summary>
        /// read data from inputStream and write to our buffer
        /// </summary>
        /// <param name="inputStream"></param>
        public void WriteBufferFromStream(Stream inputStream)
        {
            if (_writeIndex >= _len)
            {

            }
            //try read max data from the stream
            _writeIndex += inputStream.Read(_largeBuffer, _writeIndex, _len - _writeIndex);
#if DEBUG
            if (_writeIndex == 2048)
            {

            }
#endif
        }

        //
        public int WriteIndex => _writeIndex;
        public int ReadIndex => _readIndex;
        public bool HasDataToRead => _readIndex < _writeIndex;
        public int DataToReadLength => _writeIndex - _readIndex;
        public byte GetByteFromBuffer(int index)
        {
            return _largeBuffer[_startAt + _readIndex + index];
        }
        public int RemainingWriteSpace => _len - _writeIndex;
    }


    class SendIO
    {
        //send,
        //resp 

        int sendingTargetBytes; //target to send
        int sendingTransferredBytes; //has transfered bytes
        byte[] currentSendingData = null;
        Queue<byte[]> sendingQueue = new Queue<byte[]>();

        object stateLock = new object();
        object queueLock = new object();
        SendIOState _sendingState = SendIOState.ReadyNextSend;
        AbstractAsyncNetworkStream _networkStream;


#if DEBUG && !NETSTANDARD1_6
        readonly int dbugThradId = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif

        public SendIO()
        {
        }
        public void Bind(AbstractAsyncNetworkStream networkStream)
        {
            _networkStream = networkStream;
        }
        SendIOState sendingState
        {
            get { return _sendingState; }
            set
            {
#if DEBUG
                switch (_sendingState)
                {
                    case SendIOState.Error:
                        {
                        }
                        break;
                    case SendIOState.ProcessSending:
                        {
                            if (value != SendIOState.ReadyNextSend)
                            {

                            }
                            else
                            {
                            }
                        }
                        break;
                    case SendIOState.ReadyNextSend:
                        {
                            if (value != SendIOState.Sending)
                            {

                            }
                            else
                            {
                            }
                        }
                        break;
                    case SendIOState.Sending:
                        {
                            if (value != SendIOState.ProcessSending)
                            {
                            }
                            else
                            {
                            }
                        }
                        break;

                }
#endif
                _sendingState = value;
            }
        }

        public void Reset()
        {
            lock (stateLock)
            {
                if (sendingState != SendIOState.ReadyNextSend)
                {
                }
            }

            sendingTargetBytes = sendingTransferredBytes = 0;
            currentSendingData = null;
            lock (queueLock)
            {
                if (sendingQueue.Count > 0)
                {

                }
                sendingQueue.Clear();
            }
        }
        public void EnqueueOutputData(byte[] dataToSend, int count)
        {
            lock (stateLock)
            {
                SendIOState snap1 = this.sendingState;
#if DEBUG && !NETSTANDARD1_6
                int currentThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (snap1 != SendIOState.ReadyNextSend)
                {

                }
#endif
            }
            lock (queueLock)
            {
                sendingQueue.Enqueue(dataToSend);
            }
        }
        public int QueueCount
        {
            get
            {
                return sendingQueue.Count;
            }
        }
#if DEBUG
        int dbugSendingTheadId;
#endif

        public void StartSendAsync()
        {
            lock (stateLock)
            {
                if (sendingState != SendIOState.ReadyNextSend)
                {
                    //if in other state then return
                    return;
                }


#if DEBUG && !NETSTANDARD1_6
                dbugSendingTheadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
                sendingState = SendIOState.Sending;
            }

            //------------------------------------------------------------------------
            //send this data first 
            int remaining = this.sendingTargetBytes - this.sendingTransferredBytes;
            if (remaining == 0)
            {
                bool hasSomeData = false;
                lock (queueLock)
                {
                    if (this.sendingQueue.Count > 0)
                    {
                        this.currentSendingData = sendingQueue.Dequeue();
                        remaining = this.sendingTargetBytes = currentSendingData.Length;
                        this.sendingTransferredBytes = 0;
                        hasSomeData = true;
                    }
                }
                if (!hasSomeData)
                {
                    //no data to send ?
                    sendingState = SendIOState.ReadyNextSend;
                    return;
                }
            }
            else if (remaining < 0)
            {
                //?
                throw new NotSupportedException();
            }


            //-----------------------------------------------------------

            //send to network stream
            //....

            byte[] sendingData = null;// CreateTestHtmlRespMsg("hello!");
            using (MemoryStream ms1 = new MemoryStream())
            {
                ms1.Write(currentSendingData, 0, currentSendingData.Length);
                while (sendingQueue.Count > 0)
                {
                    byte[] anotherBuffer = sendingQueue.Dequeue();
                    ms1.Write(anotherBuffer, 0, anotherBuffer.Length);
                }
                sendingData = ms1.ToArray();
            }

            if (!_networkStream.WriteBuffer(sendingData, 0, sendingData.Length))
            {
                remaining = 0;
                sendingTargetBytes = sendingTransferredBytes;
            }
            else
            {
                //some data pending ...

            }

            //
            //_networkStream.WriteBuffer(currentSendingData, 0, remaining);
            //ProcessWaitingData();

            ////-----------------------------------------------------------
            //if (remaining <= _sendBufferSize)
            //{
            //    _sendArgs.SetBuffer(_sendStartOffset, remaining); //set position to send data
            //    //*** copy from src to dest
            //    if (currentSendingData != null)
            //    {
            //        Buffer.BlockCopy(this.currentSendingData, //src
            //            this.sendingTransferredBytes,
            //            _sendArgs.Buffer, //dest
            //            _sendStartOffset,
            //            remaining);
            //    }
            //}
            //else
            //{
            //    //We cannot try to set the buffer any larger than its size.
            //    //So since receiveSendToken.sendBytesRemainingCount > BufferSize, we just
            //    //set it to the maximum size, to send the most data possible.
            //    _sendArgs.SetBuffer(_sendStartOffset, _sendBufferSize);
            //    //Copy the bytes to the buffer associated with this SAEA object.
            //    Buffer.BlockCopy(this.currentSendingData,
            //        this.sendingTransferredBytes,
            //        _sendArgs.Buffer,
            //        _sendStartOffset,
            //        _sendBufferSize);
            //}

            //if (!_sendArgs.AcceptSocket.SendAsync(_sendArgs))
            //{
            //    //when SendAsync return false 
            //    //Returns false if the I/O operation completed synchronously.                 
            //    ProcessWaitingData();
            //}
        }

    }


 

}