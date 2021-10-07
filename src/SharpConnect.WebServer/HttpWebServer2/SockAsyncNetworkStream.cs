//MIT, 2018-present, EngineKit
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using SharpConnect.WebServers;

namespace SharpConnect.Internal2
{


    delegate void RecvCompleteHandler(bool success, int byteCount);

    /// <summary>
    /// abstract socket network stream
    /// </summary>
    abstract class AbstractAsyncNetworkStream : Stream, IRecvIO
    {
#if DEBUG
        static int s_dbugTotalId;
        public readonly int dbugId = s_dbugTotalId++;
#endif

        //---------------------
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();


        public abstract int ByteReadTransfered { get; }
        public abstract byte GetByteFromBuffer(int index);
        public abstract void ReadBuffer(int srcIndex, int srcCount, byte[] dstBuffer, int destIndex);

        //


        //----------------
        public abstract bool WriteBuffer(byte[] srcBuffer, int srcIndex, int count);
        public abstract void StartReceive();
        public abstract void StartSend();
        public abstract void ClearReceiveBuffer();
        public abstract void Reset();
        //for notify that there are new packet

        RecvCompleteHandler _recvCompleted;
        EventHandler _sendCompleted;
        public void SetRecvCompleteEventHandler(RecvCompleteHandler recvCompleted)
        {
            _recvCompleted = recvCompleted;
        }
        public void SetSendCompleteEventHandler(EventHandler sendCompleted)
        {
            _sendCompleted = sendCompleted;
        }
        protected void RaiseSendComplete()
        {
            _sendCompleted?.Invoke(null, EventArgs.Empty);
        }
        protected void RaiseRecvCompleted(int byteCount)
        {
            _recvCompleted?.Invoke(true, byteCount);
        }

        internal abstract byte RecvReadByte(int pos);
        internal abstract void EnqueueSendData(byte[] buffer, int len);
        internal abstract void EnqueueSendData(DataStream dataStream);
        internal abstract void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen);

        internal abstract void UnbindSocket();
        // internal abstract int QueueCount { get; }
        //***
        public abstract byte[] UnsafeGetInternalBuffer();
        public int BytesTransferred => ByteReadTransfered;

        internal virtual bool BeginWebsocketMode { get; set; }

        void IRecvIO.RecvClearBuffer() => RecvClearBufferInternal();
        void IRecvIO.RecvCopyTo(byte[] target, int startAt, int len) => RecvCopyToInternal(target, startAt, len);

        protected abstract void RecvClearBufferInternal();
        protected abstract void RecvCopyToInternal(byte[] target, int startAt, int len);
    }

    class LowLevelNetworkStream : AbstractAsyncNetworkStream
    {

        bool _sending1;
        readonly object _sending1Lock = new object();

        bool _hasDataInTempMem;

        Socket _socket;
        readonly SocketAsyncEventArgs _sendAsyncEventArgs;
        readonly SocketAsyncEventArgs _recvAsyncEventArgs;

        IOBuffer _recvBuffer;
        IOBuffer _sendBuffer;

        readonly object _recvLock = new object();
        bool _passHandshake;
        readonly object _recvWaitLock = new object();
        bool _recvComplete = false;


        readonly object _sendWaitLock = new object();
        bool _sendComplete = false;

        readonly SendIO _sendIO;
        MemoryStream _tempBuffer = new MemoryStream();
        int _tempBufferReadPos = 0;

        public LowLevelNetworkStream(IOBuffer recvBuffer, IOBuffer sendBuffer)
        {
            //we assign buffer from external source
            _recvBuffer = recvBuffer;
            _sendBuffer = sendBuffer;

            //
            _sendAsyncEventArgs = new SocketAsyncEventArgs();
            _sendAsyncEventArgs.SetBuffer(_sendBuffer._largeBuffer, _sendBuffer._startAt, _sendBuffer._len);
            _sendAsyncEventArgs.Completed += SendAsyncEventArgs_Completed;
            //
            _recvAsyncEventArgs = new SocketAsyncEventArgs();
            _recvAsyncEventArgs.SetBuffer(_recvBuffer._largeBuffer, _recvBuffer._startAt, _recvBuffer._len);//TODO: swap  buffer for the args
            _recvAsyncEventArgs.Completed += RecvAsyncEventArgs_Completed;


            _sendIO = new SendIO();
            _sendIO.Bind(this);
        }

        public override void SetLength(long value)
        {

        }
        public override byte[] UnsafeGetInternalBuffer() => _recvBuffer._largeBuffer;

        public override void Reset()
        {
            _sendIO.Reset();
        }
        internal override void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen)
        {
            this.ReadBuffer(readpos, copyLen, dstBuffer, 0);
        }
        internal override byte RecvReadByte(int pos)
        {
            return GetByteFromBuffer(pos);
        }

        protected override void RecvClearBufferInternal()
        {
            throw new NotImplementedException();
        }
        protected override void RecvCopyToInternal(byte[] target, int startAt, int len)
        {
            throw new NotImplementedException();
        }


        internal override void EnqueueSendData(byte[] buffer, int len)
        {
            _sendIO.EnqueueOutputData(buffer, len);
        }
        internal override void EnqueueSendData(DataStream dataStream)
        {
            _sendIO.EnqueueOutputData(dataStream);
        }
        public void Bind(Socket socket)
        {

#if DEBUG
            if (_socket != null)
            {
                //please unbind the old one before use
                throw new NotSupportedException();
            }
#endif
            _socket = socket;
            if (socket != null)
            {
                //bind this socket to SocketAsyncEventArgs()
                _sendAsyncEventArgs.AcceptSocket = socket;
                _recvAsyncEventArgs.AcceptSocket = socket;
            }
        }

        internal override void UnbindSocket()
        {
            //TODO: review here...


            //unbind socket ***
            //Socket tmp = _socket;
            //_sendAsyncEventArgs.AcceptSocket = null;
            //_recvAsyncEventArgs.AcceptSocket = null;
            //_socket = null;
            //_passHandshake = false;

        }
        public override void ClearReceiveBuffer()
        {
            _recvAsyncEventArgs.SetBuffer(_recvBuffer._startAt, _recvBuffer._len);
        }

        public override int ByteReadTransfered => _recvAsyncEventArgs.BytesTransferred;

        public override byte GetByteFromBuffer(int index)
        {
            //get byte from recv buffer
            return _recvBuffer.CopyByte(index);
        }
        public override void ReadBuffer(int srcIndex, int srcCount, byte[] dstBuffer, int destIndex)
        {
            _recvBuffer.CopyBuffer(srcIndex, dstBuffer, destIndex, srcCount);
        }
        public override bool WriteBuffer(byte[] srcBuffer, int srcIndex, int count)
        {

            //-------------
            if (_sending1)
            {
                lock (_sending1Lock)
                    while (_sending1)
                        Monitor.Wait(_sending1Lock);
            }
            //-------------

            _sending1 = true;
            //write data to _sendAsyncEventArgs
            _sendBuffer.WriteBuffer(srcBuffer, srcIndex, count);
            //then send***
            return _socket.SendAsync(_sendAsyncEventArgs);
        }

        void ProcessSendCompleteData()
        {
            //send complete 

            if (_hasDataInTempMem)
            {
                ProcessWaitingDataInTempMem();
            }

            if (_passHandshake)
            {
                RaiseSendComplete();
            }
            else
            {
                lock (_sendWaitLock)
                {
                    _sendComplete = true;
                    Monitor.Pulse(_sendWaitLock);
                }
            }

            lock (_sending1Lock)
            {
                _sending1 = false;
                Monitor.Pulse(_sending1Lock);
            }
        }

        //object _recvInvoke = new object();
        int _invokeCheck1 = 0;
        void RecvAsyncEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            //this is called when ...
            //1. recv complete data or
            //2. buffer full, 
#if DEBUG

            if (_recvAsyncStarted != 1)
            {

            }
#endif
            Interlocked.Exchange(ref _recvAsyncStarted, 0);

            switch (e.LastOperation)
            {
                default:
                    {

                    }
                    break;
                case SocketAsyncOperation.Receive:
                    {
                        if (_passHandshake)
                        {
                            if (e.BytesTransferred > 0)
                            {
                                RecvDataAfterHandshake();
                            }

                        }
                        else
                        {
                            int bytesTransferred = e.BytesTransferred;
                            lock (_recvLock)
                            {
                                if (bytesTransferred == 0)
                                {
                                    _recvBuffer.Reset();
                                }
                                else
                                {
                                    //notify recvBuffer that we have some data transfered into
                                    _recvBuffer.AppendWriteByteCount(bytesTransferred);
                                    //if remaining space=0, we can't get more data
                                }
                                e.SetBuffer(_recvBuffer.WriteIndex, _recvBuffer.RemainingWriteSpace);
                            }

                            //ref: http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
                            lock (_recvWaitLock)           // Let's now wake up the thread by
                            {                              // setting _go=true and pulsing.
                                _recvComplete = true;
                                Monitor.Pulse(_recvWaitLock);
                            }
                        }
                    }
                    break;
                case SocketAsyncOperation.Send:
                    {
                        //send complete 

                    }
                    break;
            }
        }

        void SendAsyncEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {

            //TODO: review here,
            //this is not called on .netcore/https??
#if DEBUG
            if (_passHandshake)
            {

            }
#endif

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSendCompleteData();
                    break;
            }
        }

        public bool PassHandShakeMode => _passHandshake;


        int ReadAfterPassHandshake(byte[] buffer, int offset, int count)
        {
            //this is called by upper stream (eg. ssl stream)
            lock (_tempBuffer)
            {
                if (_tempBuffer.Length > 0)
                {
                    //some data in temp buffer
                    //send this out first 
                    int availableLen = (int)(_tempBuffer.Length - _tempBufferReadPos);
                    _tempBuffer.Position = _tempBufferReadPos;//move to readpos
                    if (availableLen > count)
                    {
                        //only available space
                        _tempBuffer.Read(buffer, offset, count);
                        _tempBufferReadPos += count;
                        return count;
                    }
                    else
                    {
                        //clear all
                        _tempBuffer.Read(buffer, offset, availableLen);
                        _tempBuffer.SetLength(0);
                        _tempBufferReadPos = 0;
                        return availableLen;
                    }
                }
                return 0;
            }
        }
        void RecvDataAfterHandshake()
        {

        TRY_AGAIN2:
            if (_invokeCheck1 != 0)
            {
                goto TRY_AGAIN2;
            }
            if (Interlocked.CompareExchange(ref _invokeCheck1, 1, 0) != 0)
            {
                System.Diagnostics.Debugger.Break();

            }
            //call by RecvAsyncEventArgs_Completed thread
#if DEBUG
            if (_recvAsyncStarted == 1)
            {

            }
#endif
            int totalReadLen = 0;
            int newReadLen = _recvAsyncEventArgs.BytesTransferred;
            if (newReadLen > 0)
            {

                totalReadLen += newReadLen;

                lock (_recvLock)
                {
                    _recvBuffer.AppendWriteByteCount(newReadLen);
                    lock (_tempBuffer)
                    {
                        _tempBuffer.Position = _tempBuffer.Length;
                        _recvBuffer.CopyBufferTo(_tempBuffer, newReadLen);
                    }
                    ResetRecvStream();
                }
            }
            else
            {


            }
            int tryAgainCount = 0;
        TRY_AGAIN:
            //trigger get data more ???
            tryAgainCount++;
            //System.Diagnostics.Debug.WriteLine("tryAgain:" + tryAgainCount); 
#if DEBUG
            if (_recvAsyncStarted == 1)
            {

            }
#endif 

            Interlocked.Exchange(ref _recvAsyncStarted, 1);
            if (_socket.ReceiveAsync(_recvAsyncEventArgs))
            {

            }
            else
            {
                Interlocked.Exchange(ref _recvAsyncStarted, 0);
                newReadLen = _recvAsyncEventArgs.BytesTransferred;
                if (newReadLen > 0)
                {
                    totalReadLen += newReadLen;
                    lock (_recvLock)
                    {
                        _recvBuffer.AppendWriteByteCount(newReadLen);
                        lock (_tempBuffer)
                        {
                            _tempBuffer.Position = _tempBuffer.Length;
                            _recvBuffer.CopyBufferTo(_tempBuffer, newReadLen);
                        }
                        ResetRecvStream();
                    }


                    //essential!!!, 
                    //sleep recv thread,
                    //lets other thread (eg.upper ssl thread) do their job
                    Thread.Sleep(1);

                    goto TRY_AGAIN;
                }
            }

            if (_invokeCheck1 == 0)
            {

            }
            if (Interlocked.CompareExchange(ref _invokeCheck1, 0, 1) != 1)
            {
                //System.Diagnostics.Debugger.Break();
            }

            if (totalReadLen > 0)
            {
                EnqueueRecvDataNotification(totalReadLen);
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_passHandshake)
            {
                return ReadAfterPassHandshake(buffer, offset, count);
            }


            //***
            //socket layer => [this layer] => ssl stream  
            //encrypt data from socket (lower layer) are read to ssl stream (upper layer) 
            if (_recvBuffer.DataToReadLength < count)
            {
                //if we don't have enough data
                //then => recv more           
                _recvComplete = false;
                //...               
                int readByteCount = 0;
                Interlocked.Exchange(ref _recvAsyncStarted, 1);
                if (_socket.ReceiveAsync(_recvAsyncEventArgs))
                {
                    //true if the I/O operation is pending.
                    //The Completed event on the e parameter will be raised upon completion of the operation. 
                    //receive async,
                    //but in this method => convert to sync
                    //SYNC, we need to WAIT for data ... 
                    //ref: http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
                    //--------------------------------
                    lock (_recvWaitLock)
                        while (!_recvComplete)
                            Monitor.Wait(_recvWaitLock); //? 


                    Interlocked.Exchange(ref _recvAsyncStarted, 0);
                    if (_passHandshake)
                    {
                        lock (_recvLock)
                        {
                            readByteCount = _recvAsyncEventArgs.BytesTransferred;
                            //--------------
                            //write 'encrypted' data from socket to memory stream
                            //the ssl stream that wrap this stream will decode the data later***
                            //--------------
                            _recvBuffer.AppendWriteByteCount(readByteCount);
                            _recvAsyncEventArgs.SetBuffer(_recvBuffer.WriteIndex, _recvBuffer.RemainingWriteSpace);
                        }
                    }

                }
                else
                {
                    //false if the I/O operation completed synchronously.
                    //In this case, The Completed event on the e parameter will not be raised and
                    //the e object passed as a parameter may be examined immediately after the method call
                    //returns to retrieve the result of the operation. 

                    Interlocked.Exchange(ref _recvAsyncStarted, 0);
                    lock (_recvLock)
                    {
                        readByteCount = _recvAsyncEventArgs.BytesTransferred;
                        //--------------
                        //write 'encrypted' data from socket to memory stream
                        //the ssl stream that wrap this stream will decode the data later***
                        //--------------

                        if (readByteCount == 0)
                        {
                            if (_passHandshake)
                            {
                                RaiseRecvCompleted(readByteCount);
                            }
                            return 0;
                        }

                        _recvBuffer.AppendWriteByteCount(readByteCount);
                        _recvAsyncEventArgs.SetBuffer(_recvBuffer.WriteIndex, _recvBuffer.RemainingWriteSpace);
                    }
                }

                if (_passHandshake)
                {
                    RaiseRecvCompleted(readByteCount);
                }
            }
            //------------------------------------------------------------------------
            //

            lock (_recvLock)
            {
                //read data from _recvBuffer 
                return _recvBuffer.ReadBufferTo(buffer, offset, count);
            }
        }

        void ProcessWaitingDataInTempMem()
        {
            _sendAsyncEventArgs.SetBuffer(_sendBuffer._largeBuffer, 0, _sendBuffer._len);
            _hasDataInTempMem = false;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {

            //buffer contains 'Encrypted' data frorm ssl stream 
            //are write to socket  
            //copy data to buffer and start send
#if DEBUG
            bool snap = _sendComplete;

#endif
            if (_sending1)
            {
                lock (_sending1Lock)
                    while (_sending1)
                        Monitor.Wait(_sending1Lock); //? 

            }

            if (count > _sendBuffer._len)
            {
                byte[] tmpBuffer = new byte[count];
                _sendAsyncEventArgs.SetBuffer(tmpBuffer, 0, count);
                _hasDataInTempMem = true;
            }
            else
            {
                _sendAsyncEventArgs.SetBuffer(0, count); //clear before each write
            }

            Buffer.BlockCopy(buffer, offset, _sendAsyncEventArgs.Buffer, 0, count);
            _sending1 = true;

            if (_socket.SendAsync(_sendAsyncEventArgs))
            {
                //Returns true if data is pending (send async)

                //sending data is pending...
                //send complete event will be raised
                //in this case, this Write() method is sync method
                //so we need to wait when the writing out is complete

                lock (_sendWaitLock)
                    while (!_sendComplete)
                        Monitor.Wait(_sendWaitLock); //? 
            }
            else
            {
                _sending1 = false;
                ProcessSendCompleteData();
                //Returns false if the I / O operation completed synchronously.****
            }
        }
        public void ResetRecvStream()
        {
            lock (_recvLock)
            {
                //since we share the buffer between _recvAsyncEventArgs and
                //recvBuffer so=> we need to set this together

                _recvBuffer.Reset();
                _recvAsyncEventArgs.SetBuffer(_recvBuffer._startAt, _recvBuffer._len);
            }
        }
        public override void Flush()
        {
            //impl flush
        }
        //-------------------------------------------- 


        public override void StartSend()
        {
#if DEBUG
            //System.Diagnostics.Debug.WriteLine("start send");
            _sendComplete = false;
#endif
            _sendIO.StartSendAsync();///***
        }


        int _recvAsyncStarted;
        int _started_recv;

        public override void StartReceive()
        {
            //------------------------------
            _passHandshake = true; //start recv data from client   

            if (Interlocked.CompareExchange(ref _started_recv, 1, 0) == 1)
            {
                return;
            }



            if (Interlocked.CompareExchange(ref _recvAsyncStarted, 1, 0) == 1)
            {
                return;
            }

            if (_socket.ReceiveAsync(_recvAsyncEventArgs))
            {
                //true if the I/O operation is pending. 
                //The Completed event on the e parameter will be raised upon completion of the operation.
            }
            else
            {

                //false if the I/O operation completed synchronously.
                //In this case, The Completed event on the e parameter will not be raised and
                //the e object passed as a parameter may be examined immediately after 
                //the method call returns to retrieve the result of the operation.  
                Interlocked.Exchange(ref _recvAsyncStarted, 0);
                RecvDataAfterHandshake();
            }
        }
        void EnqueueRecvDataNotification(int approxByteCount)
        {
            if (IsInRecvNotiQueue)
            {
                return;
            }
            RaiseRecvQueue.Enqueue(this, approxByteCount);
        }
        public bool HasMoreRecvData => _recvBuffer.HasDataToRead;


        object _notiQueueLock = new object();
        bool _isInNotiQueue;
        internal bool IsInRecvNotiQueue
        {
            get
            {
                lock (_notiQueueLock)
                {
                    return _isInNotiQueue;
                }
            }
            set
            {
                lock (_notiQueueLock)
                {
                    _isInNotiQueue = value;
                }
            }
        }

        internal void RaiseRecvCompleteInternal(int approxByteCount)
        {
            RaiseRecvCompleted(approxByteCount);
        }
    }


    struct RaiseRecvQueueItem
    {
        public readonly LowLevelNetworkStream _s;
        public readonly int _approxByteCount;
        public RaiseRecvQueueItem(LowLevelNetworkStream s, int approxByteCount)
        {
            _s = s;
            _approxByteCount = approxByteCount;
            s.IsInRecvNotiQueue = true;
        }

        public static readonly RaiseRecvQueueItem Empty = new RaiseRecvQueueItem();
    }


    static class RaiseRecvQueue
    {
        static Thread s_notiThread;
        static Queue<RaiseRecvQueueItem> _notiQueues = new Queue<RaiseRecvQueueItem>();

        static int s_running;
        static int s_notiThreadCreated;


        static void RunClearingThread()
        {
            if (Interlocked.CompareExchange(ref s_notiThreadCreated, 1, 0) == 1)
            {
                //has the runninng thread
                return;
            }
            //----
            //if not start thread
#if DEBUG
            if (s_notiThread != null)
            {
                throw new NotSupportedException();
            }
#endif
            //------
            s_notiThread = new Thread(ClearingThread); //run clearing thread
            s_notiThread.Name = "RaiseRecvQueu";

            Interlocked.Exchange(ref s_running, 1);
            s_notiThread.Start();
        }
        public static void Enqueue(LowLevelNetworkStream stream, int approxByteCount)
        {
            lock (_notiQueues)
            {
                _notiQueues.Enqueue(new RaiseRecvQueueItem(stream, approxByteCount));
                RunClearingThread();
                Monitor.Pulse(_notiQueues);
            }
        }
        public static void StopAndExitQueue()
        {
            //stop and exit queue
            Interlocked.Exchange(ref s_running, 0);
            lock (_notiQueues)
            {
                //signal the queue
                Monitor.Pulse(_notiQueues);
            }
        }
        static void ClearingThread(object state)
        {
            RaiseRecvQueueItem item = RaiseRecvQueueItem.Empty;

            while (s_running == 1)
            {
                bool foundJob = false;
                lock (_notiQueues)
                {
                    int count = _notiQueues.Count;
                    if (count > 0)
                    {
                        item = _notiQueues.Dequeue();
                        item._s.IsInRecvNotiQueue = false;
                        foundJob = true;
                        //run this task  
                    }
                    else
                    {
                    }
                }
                //--------------------------------------------
                if (foundJob)
                {
                    item._s.RaiseRecvCompleteInternal(item._approxByteCount);
                }
                else
                {
                    //no job in this thread,
                    //we can exit this

                    int noJobCount = 0;
                    lock (_notiQueues)
                    {
                        while (_notiQueues.Count == 0)
                        {
                            if (noJobCount > 5)//configurable
                            {
                                //stop this loop
                                Interlocked.Exchange(ref s_running, 0);
                                Thread tmpThread = s_notiThread;
                                Interlocked.Exchange(ref s_notiThreadCreated, 0);

                                s_notiThread = null;
                                return;
                            }
                            Monitor.Wait(_notiQueues, 2000);//2s
                            noJobCount++;
                        }
                    }
                }
            }
        }
    }



    delegate void AuthenCallbackDelegate();

    class ReadableRecvBuffer : IDisposable
    {

        MemoryStream _ms;

        public ReadableRecvBuffer()
        {
            _ms = new MemoryStream();

        }
        public void Dispose()
        {
            if (_ms != null)
            {
                _ms.Dispose();
                _ms = null;
            }
        }
        public void Reset()
        {
            lock (_ms)
            {
                _ms.SetLength(0);
            }
        }
        public void CopyBuffer(int readIndex, byte[] dstBuffer, int dstIndex, int count)
        {
            lock (_ms)
            {
                byte[] underlyingBuffer = _ms.GetBuffer();
                Buffer.BlockCopy(underlyingBuffer,
                    readIndex,
                    dstBuffer, dstIndex, count);
            }

        }
        public byte CopyByte(int index)
        {
            lock (_ms)
            {
                return _ms.GetBuffer()[index];
            }
        }

        /// <summary>
        /// read data from inputStream and write to our buffer
        /// </summary>
        /// <param name="sslStream"></param>
        public int WriteBufferFromStream(Stream sslStream)
        {
            lock (_ms)
            {
                //copy data from inputStream to buffer
                //and copy data from buffer to _ms 
                //try read max data from the stream

                byte[] tmpWriteBuffer = GetTempWriteBuffer();
                int readLen = sslStream.Read(tmpWriteBuffer, 0, tmpWriteBuffer.Length);
                _ms.Write(tmpWriteBuffer, 0, readLen);

                //while (readLen > 0)
                //{
                //    _ms.Write(tmpWriteBuffer, 0, readLen);
                //    readLen = sslStream.Read(tmpWriteBuffer, 0, tmpWriteBuffer.Length);
                //}
                return readLen;
            }
        }

        public int BufferLength
        {
            get
            {
                lock (_ms)
                {
                    //get unread data
                    return (int)_ms.Length;
                }
            }
        }
        public byte GetByteFromBuffer(int index)
        {
            lock (_ms)
            {
                return _ms.GetBuffer()[index];
            }
        }


        [ThreadStatic]
        static byte[] s_tempWriteBuffer;
        static byte[] GetTempWriteBuffer()
        {
            return s_tempWriteBuffer ?? (s_tempWriteBuffer = new byte[2048]);
        }

    }

    class SecureSockNetworkStream : AbstractAsyncNetworkStream
    {
        ReadableRecvBuffer _readableRecvBuffer;
        SslStream _sslStream;
        LowLevelNetworkStream _lowLevelStreamForSSL; //base stream
        readonly object _recvAppendLock = new object();
        System.Security.Cryptography.X509Certificates.X509Certificate2 _cert;

        MemoryStream _enqueueOutputData = new MemoryStream();

        readonly object _sendingStateLock = new object();

        public SecureSockNetworkStream(LowLevelNetworkStream socketNetworkStream,
            System.Security.Cryptography.X509Certificates.X509Certificate2 cert,
            System.Net.Security.RemoteCertificateValidationCallback remoteCertValidationCb = null)
        {
            _sslStream = new SslStream(socketNetworkStream, true, remoteCertValidationCb, null);
            _cert = cert;

            _lowLevelStreamForSSL = socketNetworkStream;
            _lowLevelStreamForSSL.SetRecvCompleteEventHandler(LowLevelForSSL_RecvCompleted);
            _lowLevelStreamForSSL.SetSendCompleteEventHandler(LowLevelForSSL_SendCompleted);

            _readableRecvBuffer = new ReadableRecvBuffer();
        }

        internal override bool BeginWebsocketMode
        {
            get => base.BeginWebsocketMode;
            set
            {
                _lowLevelStreamForSSL.BeginWebsocketMode = value;
                base.BeginWebsocketMode = value;
            }
        }
        public override byte[] UnsafeGetInternalBuffer()
        {
            //TODO: impl this again for websocket
            //copy buffer to external
            return null;
            //_readableRecvBuffer._largeBuffer;
        }

        internal override void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen)
        {
            //*** 
            _readableRecvBuffer.CopyBuffer(readpos, dstBuffer, 0, copyLen);
        }

        internal override byte RecvReadByte(int pos)
        {
            return _readableRecvBuffer.CopyByte(pos);
        }

        protected override void RecvCopyToInternal(byte[] target, int startAt, int len)
        {
            _readableRecvBuffer.CopyBuffer(0, target, startAt, len);
        }
        protected override void RecvClearBufferInternal()
        {
            _readableRecvBuffer.Reset();
        }


        internal override void EnqueueSendData(byte[] buffer, int len)
        {
            lock (_sendingStateLock)
            {
                _enqueueOutputData.Write(buffer, 0, len);
            }
        }
        internal override void EnqueueSendData(DataStream dataStream)
        {
            //TODO: implement this
            throw new NotImplementedException();
        }

        readonly object _startSendLock = new object();
        bool _startSending;

        public override void StartSend()
        {
            lock (_sendingStateLock)
            {
                if (_startSending)
                {
                    return;
                }
                _startSending = true;
                //upper layer call this 
                //check waiting data and send it 

                byte[] buffer = _enqueueOutputData.ToArray();
                _enqueueOutputData.SetLength(0);

                _sslStream.Write(buffer);
                _startSending = false;
            }

        }
        public override void Reset()
        {
            _sslStream.SetLength(0);
            _readableRecvBuffer.Reset();
            _lowLevelStreamForSSL.Reset();
        }
        public override void ClearReceiveBuffer()
        {
            _readableRecvBuffer.Reset();
        }
        internal override void UnbindSocket()
        {
            _lowLevelStreamForSSL.UnbindSocket();
        }
        public void AuthenAsServer()
        {
            _sslStream.AuthenticateAsServer(_cert);
        }
        public void AuthenAsServer(AuthenCallbackDelegate whenFinish)
        {
            _sslStream.BeginAuthenticateAsServer(_cert, false, System.Security.Authentication.SslProtocols.Default, false,
                state =>
                {
                    //authen may fail at this stage
                    try
                    {
                        _sslStream.EndAuthenticateAsServer(state);
                        whenFinish();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);

                    }

                }, new object());
        }
        public void AuthenAsClient(string targetHost)
        {
            //targetHost must match with cert => eg. localhost

            _sslStream.AuthenticateAsClient(targetHost);
        }
        public void AuthenAsClient(string targetHost, AuthenCallbackDelegate whenFinish)
        {
            //targetHost must match with cert => eg. localhost
            _sslStream.BeginAuthenticateAsClient(targetHost,
                state =>
                {
                    _sslStream.EndAuthenticateAsClient(state);
                    whenFinish();

                }, new object());
        }

        void LowLevelForSSL_SendCompleted(object sender, EventArgs e)
        {
            lock (_startSendLock)
            {
                _startSending = false;
            }

            RaiseSendComplete();
        }


        private void LowLevelForSSL_RecvCompleted(bool result, int lowLevelByteCount)
        {
            //lowLevelByteCount=> encrypted byte count, we can't use this value directly

#if DEBUG
            if (!_lowLevelStreamForSSL.PassHandShakeMode)
            {
                System.Diagnostics.Debugger.Break();
            }
#endif

            int readableDataLen = 0;
            if (lowLevelByteCount > 0)
            {
                //.... 
                lock (_recvAppendLock)
                {
                    //decrypt data from sslStream to readable data
                    //and write to _readableRecvBuffer  
                    try
                    {
                        int latestReadLen = _readableRecvBuffer.WriteBufferFromStream(_sslStream);
                        while (latestReadLen > 0)
                        {
                            latestReadLen = _readableRecvBuffer.WriteBufferFromStream(_sslStream);
                        }
                        readableDataLen = _readableRecvBuffer.BufferLength;
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            _startRecv = false;
            //-----------
            //this should notify the ssl that we have some data arrive
            if (readableDataLen > 0)
            {
                RaiseRecvCompleted(readableDataLen);
            }
            else
            {
                _readableRecvBuffer.Reset();
            }
        }

        public override void ReadBuffer(int srcIndex, int copyCount, byte[] dstBuffer, int dstIndex)
        {
            _readableRecvBuffer.CopyBuffer(srcIndex, dstBuffer, dstIndex, copyCount);
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _sslStream.Read(buffer, offset, count);
        }
        /// <summary>
        /// async write buffer
        /// </summary>
        /// <param name="srcBuffer"></param>
        /// <param name="srcIndex"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override bool WriteBuffer(byte[] srcBuffer, int srcIndex, int count)
        {

            //write data down to the ssl stream 
            _sslStream.Write(srcBuffer, srcIndex, count);
            return false;
            //return false => all data are sent
            //return true => data is in pending queue
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            //we use sslStream as sync 
            //but underlying stream of this ssl stream sends data async 
            _sslStream.Write(buffer, offset, count);
        }
        public override void Flush()
        {

        }

        bool _startRecv;
        public override void StartReceive()
        {
            _lowLevelStreamForSSL.BeginWebsocketMode = this.BeginWebsocketMode;
            if (_startRecv)
            {
                return;
            }
            _startRecv = true;
            _lowLevelStreamForSSL.StartReceive();
        }

        public override int ByteReadTransfered => _readableRecvBuffer.BufferLength;

        public override byte GetByteFromBuffer(int index)
        {
            return _readableRecvBuffer.GetByteFromBuffer(index);
        }

    }
}