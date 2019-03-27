//2010, CPOL, Stan Kirk
//MIT, 2015-present, EngineKit and contributors
//https://docs.microsoft.com/en-us/dotnet/framework/network-programming/socket-performance-enhancements-in-version-3-5

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;


namespace SharpConnect.Internal
{

    class RecvIO
    {

        readonly int _recvStartOffset;
        readonly int _recvBufferSize;
        readonly SocketAsyncEventArgs _recvArgs;
        Action<RecvEventCode> _recvNotify;

        public RecvIO(SocketAsyncEventArgs recvArgs, int recvStartOffset, int recvBufferSize, Action<RecvEventCode> recvNotify)
        {
            _recvArgs = recvArgs;
            _recvStartOffset = recvStartOffset;
            _recvBufferSize = recvBufferSize;
            _recvNotify = recvNotify;
        }

        public byte ReadByte(int index) => _recvArgs.Buffer[_recvStartOffset + index];

        public void CopyTo(int srcIndex, byte[] destBuffer, int destIndex, int count)
        {
            Buffer.BlockCopy(_recvArgs.Buffer,
                _recvStartOffset + srcIndex,
                destBuffer,
                destIndex, count);
        }
        public void CopyTo(int srcIndex, byte[] destBuffer, int count)
        {

            Buffer.BlockCopy(_recvArgs.Buffer,
                _recvStartOffset + srcIndex,
                destBuffer,
                0, count);
        }

        public void CopyTo(int srcIndex, MemoryStream ms, int count)
        {

            ms.Write(_recvArgs.Buffer,
                _recvStartOffset + srcIndex,
                count);
        }


#if DEBUG
        internal int dbugStartRecvPos => _recvStartOffset;

        public byte[] dbugReadToBytes()
        {
            int bytesTransfer = _recvArgs.BytesTransferred;
            byte[] destBuffer = new byte[bytesTransfer];

            Buffer.BlockCopy(_recvArgs.Buffer,
                _recvStartOffset,
                destBuffer,
                0, bytesTransfer);

            return destBuffer;
        }
#endif

        /// <summary>
        /// process just received data, called when IO complete
        /// </summary>
        public void ProcessReceivedData()
        {
            //1. socket error
            if (_recvArgs.SocketError != SocketError.Success)
            {
                _recvNotify(RecvEventCode.SocketError);
                return;
            }
            //2. no more receive 
            if (_recvArgs.BytesTransferred == 0)
            {
                _recvNotify(RecvEventCode.NoMoreReceiveData);
                return;
            }
            _recvNotify(RecvEventCode.HasSomeData);
        }

        /// <summary>
        /// start new receive
        /// </summary>
        public void StartReceive()
        {
            _recvArgs.SetBuffer(_recvStartOffset, _recvBufferSize);
            if (!_recvArgs.AcceptSocket.ReceiveAsync(_recvArgs))
            {
                ProcessReceivedData();
            }
        }

        public int BytesTransferred => _recvArgs.BytesTransferred;

        internal byte[] UnsafeGetInternalBuffer() => _recvArgs.Buffer;

    }



    class SendIO
    {
        //send,
        //resp 
        readonly int _sendStartOffset;
        readonly int _sendBufferSize;
        readonly SocketAsyncEventArgs _sendArgs;
        int _sendingTargetBytes; //target to send
        int _sendingTransferredBytes; //has transfered bytes
        byte[] _currentSendingData = null;
        Queue<byte[]> _sendingQueue = new Queue<byte[]>();
        Action<SendIOEventCode> _notify;
        object _stateLock = new object();
        object _queueLock = new object();
        //SendIOState _sendingState = SendIOState.ReadyNextSend;
        SendIOState sendingState;

#if DEBUG && !NETSTANDARD1_6
        readonly int dbugThradId = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
        public SendIO(SocketAsyncEventArgs sendArgs,
            int sendStartOffset,
            int sendBufferSize,
            Action<SendIOEventCode> notify)
        {
            _sendArgs = sendArgs;
            _sendStartOffset = sendStartOffset;
            _sendBufferSize = sendBufferSize;
            _notify = notify;
        }


        //        {
        //            get => _sendingState;
        //            set
        //            {
        //#if DEBUG
        //                switch (_sendingState)
        //                {
        //                    case SendIOState.Error:
        //                        {
        //                        }
        //                        break;
        //                    case SendIOState.ProcessSending:
        //                        {
        //                            if (value != SendIOState.ReadyNextSend)
        //                            {

        //                            }
        //                            else
        //                            {
        //                            }
        //                        }
        //                        break;
        //                    case SendIOState.ReadyNextSend:
        //                        {
        //                            if (value != SendIOState.Sending)
        //                            {

        //                            }
        //                            else
        //                            {
        //                            }
        //                        }
        //                        break;
        //                    case SendIOState.Sending:
        //                        {
        //                            if (value != SendIOState.ProcessSending)
        //                            {
        //                            }
        //                            else
        //                            {
        //                            }
        //                        }
        //                        break;

        //                }
        //#endif
        //                _sendingState = value;
        //            }
        //        }
        void ResetBuffer()
        {
            _currentSendingData = null;
            _sendingTransferredBytes = 0;
            _sendingTargetBytes = 0;
        }
        public void Reset()
        {
            lock (_stateLock)
            {
                if (sendingState != SendIOState.ReadyNextSend)
                {
                }
            }
            //#if DEBUG

            //            int currentTheadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            //            if (currentTheadId != this.dbugThradId)
            //            { 
            //            }
            //#endif
            //TODO: review reset
            _sendingTargetBytes = _sendingTransferredBytes = 0;
            _currentSendingData = null;
            //#if DEBUG
            //            if (sendingQueue.Count > 0)
            //            { 
            //            }
            //#endif
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
                SendIOState snap1 = this.sendingState;
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

#if DEBUG
        int dbugSendingTheadId;
#endif
        public void StartSendAsync()
        {
            lock (_stateLock)
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
                    sendingState = SendIOState.ReadyNextSend;
                    return;
                }

            }
            else if (remaining < 0)
            {
                //?
                throw new NotSupportedException();
            }


            if (remaining <= _sendBufferSize)
            {
                _sendArgs.SetBuffer(_sendStartOffset, remaining);
                //*** copy from src to dest
                if (_currentSendingData != null)
                {
                    Buffer.BlockCopy(_currentSendingData, //src
                        _sendingTransferredBytes,
                        _sendArgs.Buffer, //dest
                        _sendStartOffset,
                        remaining);
                }
            }
            else
            {
                //We cannot try to set the buffer any larger than its size.
                //So since receiveSendToken.sendBytesRemainingCount > BufferSize, we just
                //set it to the maximum size, to send the most data possible.
                _sendArgs.SetBuffer(_sendStartOffset, _sendBufferSize);
                //Copy the bytes to the buffer associated with this SAEA object.
                Buffer.BlockCopy(_currentSendingData,
                    _sendingTransferredBytes,
                    _sendArgs.Buffer,
                    _sendStartOffset,
                    _sendBufferSize);
            }


            if (!_sendArgs.AcceptSocket.SendAsync(_sendArgs))
            {
                //when SendAsync return false 
                //this means the socket can't do async send     
                ProcessWaitingData();
            }
        }
        /// <summary>
        /// send next data, after prev IO complete
        /// </summary>
        public void ProcessWaitingData()
        {
            // This method is called by I/O Completed() when an asynchronous send completes.   
            //after IO completed, what to do next.... 
            sendingState = SendIOState.ProcessSending;
            switch (_sendArgs.SocketError)
            {
                default:
                    {
                        //error, socket error

                        ResetBuffer();
                        sendingState = SendIOState.Error;
                        _notify(SendIOEventCode.SocketError);
                        //manage socket errors here
                    }
                    break;
                case SocketError.Success:
                    {
                        _sendingTransferredBytes += _sendArgs.BytesTransferred;
                        int remainingBytes = _sendingTargetBytes - _sendingTransferredBytes;
                        if (remainingBytes > 0)
                        {
                            //no complete!, 
                            //start next send ...
                            //****
                            sendingState = SendIOState.ReadyNextSend;
                            StartSendAsync();
                            //****
                        }
                        else if (remainingBytes == 0)
                        {
                            //complete sending  
                            //check the queue again ...

                            bool hasSomeData = false;
                            lock (_queueLock)
                            {
                                if (_sendingQueue.Count > 0)
                                {
                                    //move new chunck to current Sending data
                                    _currentSendingData = _sendingQueue.Dequeue();
                                    hasSomeData = true;
                                }
                            }

                            if (hasSomeData)
                            {
                                _sendingTargetBytes = _currentSendingData.Length;
                                _sendingTransferredBytes = 0;
                                //****
                                sendingState = SendIOState.ReadyNextSend;
                                StartSendAsync();
                                //****
                            }
                            else
                            {
                                //no data
                                ResetBuffer();
                                //notify no more data
                                //****
                                sendingState = SendIOState.ReadyNextSend;
                                _notify(SendIOEventCode.SendComplete);
                                //****   
                            }
                        }
                        else
                        {   //< 0 ????
                            throw new NotSupportedException();
                        }
                    }
                    break;
            }

        }
    }


    class RecvIOBufferStream : IDisposable
    {
        SimpleBufferReader _simpleBufferReader = new SimpleBufferReader();
        List<byte[]> _otherBuffers = new List<byte[]>();
        int _currentBufferIndex;

        bool _multipartMode;
        int _readpos = 0;
        int _totalLen = 0;
        int _bufferCount = 0;
        RecvIO _latestRecvIO;
        public RecvIOBufferStream(RecvIO recvIO)
        {
            _latestRecvIO = recvIO;
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
                _totalLen = _latestRecvIO.BytesTransferred;
                _simpleBufferReader.SetBuffer(_latestRecvIO.UnsafeGetInternalBuffer(), 0, _totalLen);
                _bufferCount++;
            }
            else
            {
                //more than 1 buffer
                if (_multipartMode)
                {
                    int thisPartLen = _latestRecvIO.BytesTransferred;
                    byte[] o2copy = new byte[thisPartLen];
                    Buffer.BlockCopy(_latestRecvIO.UnsafeGetInternalBuffer(), 0, o2copy, 0, thisPartLen);
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

        public int Length => _totalLen;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="len"></param>
        /// <returns></returns>
        public bool Ensure(int len) => _readpos + len <= _totalLen;

        public void BackupRecvIO()
        {
            if (_bufferCount == 1 && !_multipartMode)
            {
                //only in single mode
                int thisPartLen = _latestRecvIO.BytesTransferred;
                byte[] o2copy = new byte[thisPartLen];
                Buffer.BlockCopy(_latestRecvIO.UnsafeGetInternalBuffer(), 0, o2copy, 0, thisPartLen);
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
        public bool IsEnd() => _readpos >= _totalLen;
    }
}