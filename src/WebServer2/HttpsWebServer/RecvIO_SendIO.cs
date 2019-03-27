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
        readonly int _startAt;
        readonly int _len;
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



    class RecvIOBufferStream2 : IDisposable
    {
        SharpConnect.Internal.SimpleBufferReader _simpleBufferReader = new SharpConnect.Internal.SimpleBufferReader();
        List<byte[]> _otherBuffers = new List<byte[]>();
        int _currentBufferIndex;

        bool _multipartMode;
        int _readpos = 0;
        int _totalLen = 0;
        int _bufferCount = 0;

        SharpConnect.Internal2.AbstractAsyncNetworkStream _networkStream;

        public RecvIOBufferStream2(SharpConnect.Internal2.AbstractAsyncNetworkStream networkStream)
        {
            _networkStream = networkStream;
            AutoClearPrevBufferBlock = true;
        }

        public bool AutoClearPrevBufferBlock { get; set; }

        public void Dispose()
        {

        }
        public void Clear()
        {

            _otherBuffers.Clear();
            _multipartMode = false;
            _bufferCount = 0;
            _currentBufferIndex = 0;
            _readpos = 0;
            _totalLen = 0;
            _simpleBufferReader.SetBuffer(null, 0, 0);
        }

        public void AppendNewRecvData()
        {
            if (_bufferCount == 0)
            {
                //single part mode   

                _totalLen = _networkStream.ByteReadTransfered;
                _simpleBufferReader.SetBuffer(_networkStream.UnsafeGetRecvInternalBuffer(), 0, _totalLen);
                _bufferCount++;
            }
            else
            {
                //more than 1 buffer
                if (_multipartMode)
                {
                    int thisPartLen = _networkStream.ByteReadTransfered;
                    byte[] o2copy = new byte[thisPartLen];
                    Buffer.BlockCopy(_networkStream.UnsafeGetRecvInternalBuffer(), 0, o2copy, 0, thisPartLen);
                    _otherBuffers.Add(o2copy);
                    _totalLen += thisPartLen;
                }
                else
                {
                    //should not be here
                    throw new NotSupportedException();
                }
                _bufferCount++;
            }
        }

        public bool IsEnd() => _readpos >= _totalLen;

        public int Length => _totalLen;

        public bool Ensure(int len) => _readpos + len <= _totalLen;

        public void BackupRecvIO()
        {
            if (_bufferCount == 1 && !_multipartMode)
            {
                //only in single mode
                int thisPartLen = _networkStream.ByteReadTransfered;
                byte[] o2copy = new byte[thisPartLen];
                Buffer.BlockCopy(_networkStream.UnsafeGetRecvInternalBuffer(), 0, o2copy, 0, thisPartLen);
                _otherBuffers.Add(o2copy);
                _multipartMode = true;
                int prevIndex = _simpleBufferReader.Position;
                _simpleBufferReader.SetBuffer(o2copy, 0, thisPartLen);
                _simpleBufferReader.Position = prevIndex;
            }
        }
        public byte ReadByte()
        {
            if (_simpleBufferReader.Ensure(1))
            {
                _readpos++;
                return _simpleBufferReader.ReadByte();
            }
            else
            {
                if (_multipartMode)
                {
                    //this end of current buffer
                    //so we switch to the new one
                    if (_currentBufferIndex < _otherBuffers.Count)
                    {
                        MoveToNextBufferBlock();
                        _readpos++;
                        return _simpleBufferReader.ReadByte();
                    }
                }
            }
            throw new Exception();
        }
        void MoveToNextBufferBlock()
        {
            if (AutoClearPrevBufferBlock)
            {
                _otherBuffers[_currentBufferIndex] = null;
            }

            _currentBufferIndex++;
            byte[] buff = _otherBuffers[_currentBufferIndex];
            _simpleBufferReader.SetBuffer(buff, 0, buff.Length);
        }
        /// <summary>
        /// copy data from current pos to output
        /// </summary>
        /// <param name="output"></param>
        /// <param name="len"></param>
        public void CopyBuffer(byte[] output, int len)
        {
            if (_simpleBufferReader.Ensure(len))
            {
                _simpleBufferReader.CopyBytes(output, 0, len);
                _readpos += len;
            }
            else
            {
                //need more than 1
                int toCopyLen = _simpleBufferReader.AvaialbleByteCount;
                int remain = len;
                int targetIndex = 0;
                do
                {
                    _simpleBufferReader.CopyBytes(output, targetIndex, toCopyLen);
                    _readpos += toCopyLen;
                    targetIndex += toCopyLen;
                    remain -= toCopyLen;
                    //move to another
                    if (remain > 0)
                    {
                        if (_currentBufferIndex < _otherBuffers.Count - 1)
                        {
                            MoveToNextBufferBlock();
                            //-------------------------- 
                            //evaluate after copy
                            if (_simpleBufferReader.Ensure(remain))
                            {
                                //end
                                _simpleBufferReader.CopyBytes(output, targetIndex, remain);
                                _readpos += remain;
                                remain = 0;
                                return;
                            }
                            else
                            {
                                //not complete on this round
                                toCopyLen = _simpleBufferReader.UsedBufferDataLen;
                                //copy all
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                } while (remain > 0);

            }
        }



    }


}