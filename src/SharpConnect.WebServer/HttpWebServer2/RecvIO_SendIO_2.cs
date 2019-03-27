﻿//2010, CPOL, Stan Kirk
//MIT, 2015-present, EngineKit and contributors
//https://docs.microsoft.com/en-us/dotnet/framework/network-programming/socket-performance-enhancements-in-version-3-5

using System;
using System.Collections.Generic;
using System.IO;

namespace SharpConnect.Internal2
{

    struct IOBuffer
    {

        internal readonly byte[] _largeBuffer;
        internal readonly int _startAt;
        internal readonly int _len;
        int _readIndex;
        int _writeIndex;

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
#endif
            _largeBuffer = largeBuffer;
            _startAt = beginAt;
            _len = len;
            _readIndex = _writeIndex = 0;
        }
#if DEBUG
        public bool IsSendIO => _isSendIO;
#endif
        public int BufferStartAtIndex => _startAt;
        public int BufferLength => _len;
        public void Reset()
        {
            _readIndex = _writeIndex = 0;
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

        int _sendingTargetBytes; //target to send
        int _sendingTransferredBytes; //has transfered bytes
        byte[] _currentSendingData = null;
        Queue<byte[]> _sendingQueue = new Queue<byte[]>();

        object _stateLock = new object();
        object _queueLock = new object();
        SendIOState _sendingState;
#if DEBUG && !NETSTANDARD1_6
        readonly int dbugThradId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        int dbugSendingTheadId;
#endif
        //-----------
        AbstractAsyncNetworkStream _networkStream;

        public SendIO()
        {
        }
        public void Bind(AbstractAsyncNetworkStream networkStream)
        {
            _networkStream = networkStream;
        }
        public void Reset()
        {
            _sendingTargetBytes = _sendingTransferredBytes = 0;
            _currentSendingData = null;
            lock (_queueLock)
            {
                if (_sendingQueue.Count > 0)
                {

                }
                _sendingQueue.Clear();
            }
        }
        public void EnqueueOutputData(byte[] dataToSend, int count)
        {
            lock (_stateLock)
            {
                SendIOState snap1 = _sendingState;
#if DEBUG && !NETSTANDARD1_6
                int currentThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (snap1 != SendIOState.ReadyNextSend)
                {

                }
#endif
            }
            lock (_queueLock)
            {
                _sendingQueue.Enqueue(dataToSend);
            }
        }
        public int QueueCount => _sendingQueue.Count;

        public void StartSendAsync()
        {
            lock (_stateLock)
            {
                if (_sendingState != SendIOState.ReadyNextSend)
                {
                    //if in other state then return
                    return;
                }


#if DEBUG && !NETSTANDARD1_6
                dbugSendingTheadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
                _sendingState = SendIOState.Sending;
            }

            //------------------------------------------------------------------------
            //send this data first 
            int remaining = _sendingTargetBytes - _sendingTransferredBytes;
            if (remaining == 0)
            {
                bool hasSomeData = false;
                lock (_queueLock)
                {
                    if (_sendingQueue.Count > 0)
                    {
                        _currentSendingData = _sendingQueue.Dequeue();
                        remaining = _sendingTargetBytes = _currentSendingData.Length;
                        _sendingTransferredBytes = 0;
                        hasSomeData = true;
                    }
                }
                if (!hasSomeData)
                {
                    //no data to send ?
                    _sendingState = SendIOState.ReadyNextSend;
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
                ms1.Write(_currentSendingData, 0, _currentSendingData.Length);
                while (_sendingQueue.Count > 0)
                {
                    byte[] anotherBuffer = _sendingQueue.Dequeue();
                    ms1.Write(anotherBuffer, 0, anotherBuffer.Length);
                }
                sendingData = ms1.ToArray();
            }

            if (!_networkStream.WriteBuffer(sendingData, 0, sendingData.Length))
            {
                remaining = 0;
                _sendingTargetBytes = _sendingTransferredBytes;
            }
            else
            {
                //some data pending ...

            }
        }

    }


}