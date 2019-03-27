//MIT, 2018-present, EngineKit
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;

namespace SharpConnect.Internal2
{
    class DataArrEventArgs : EventArgs
    {
        public int ByteTransferedCount { get; set; }
    }

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

        EventHandler<DataArrEventArgs> _recvCompleted;
        EventHandler _sendCompleted;
        public void SetRecvCompleteEventHandler(EventHandler<DataArrEventArgs> recvCompleted)
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
            _recvCompleted?.Invoke(this, new DataArrEventArgs() { ByteTransferedCount = byteCount });
        }

        internal abstract byte RecvReadByte(int pos);
        internal abstract void EnqueueSendData(byte[] buffer, int len);
        internal abstract void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen);

        internal abstract void UnbindSocket();
        // internal abstract int QueueCount { get; }
        //***
        public abstract byte[] UnsafeGetInternalBuffer();
        public int BytesTransferred => ByteReadTransfered;

        internal bool BeginWebsocketMode { get; set; }
    }


    class SockNetworkStream : AbstractAsyncNetworkStream
    {

        //resuable socket stream 

        Socket _socket;
        SocketAsyncEventArgs _sendAsyncEventArgs;
        SocketAsyncEventArgs _recvAsyncEventArgs;


        IOBuffer _recvBuffer;
        IOBuffer _sendBuffer;

        object _recvLock = new object();
        bool _passHandshake;
        object _recvWaitLock = new object();
        bool _recvComplete = false;

        object _recvWaitLock2 = new object();
        object _sendWaitLock = new object();
        bool _sendComplete = false;
        //int _sendingByteTransfered = 0;
        readonly SendIO _sendIO;


        public SockNetworkStream(IOBuffer recvBuffer, IOBuffer sendBuffer)
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
        //internal override int QueueCount => _sendIO.QueueCount;

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
        internal override void EnqueueSendData(byte[] buffer, int len)
        {
            _sendIO.EnqueueOutputData(buffer, len);
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
            //write data to _sendAsyncEventArgs
            _sendBuffer.WriteBuffer(srcBuffer, srcIndex, count);
            //then send***
            return _socket.SendAsync(_sendAsyncEventArgs);
        }

        void RecvAsyncEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            lock (_startRecvLock)
            {
                _startRecv = false;
            }

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    {
                        //----------------------------------------
                        int bytesTransferred = e.BytesTransferred;
                        byte[] data = e.Buffer;
                        //----------------------------------------   

                        if (bytesTransferred == 0)
                        {
                            lock (_recvLock)
                            {
                                _recvBuffer.Reset();
                                //since we use share data
                                e.SetBuffer(_recvBuffer.WriteIndex, _recvBuffer.RemainingWriteSpace);
                            }
                        }
                        else
                        {
                            lock (_recvLock)
                            {
                                _recvBuffer.AppendWriteByteCount(bytesTransferred);
                                //since we use share data
                                e.SetBuffer(_recvBuffer.WriteIndex, _recvBuffer.RemainingWriteSpace);
                            }
                        }



                        //ref: http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
                        lock (_recvWaitLock)                 // Let's now wake up the thread by
                        {                              // setting _go=true and pulsing.
                            _recvComplete = true;
                            Monitor.Pulse(_recvWaitLock);
                        }
                        //
                        if (_passHandshake)
                        {
                            RaiseRecvCompleted(bytesTransferred);
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
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    break;
                case SocketAsyncOperation.Send:
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
                    }
                    break;
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
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

                            //_recvBuffer.WriteBuffer(_recvAsyncEventArgs.Buffer, _recvAsyncEventArgs.Offset, readByteCount);
                        }
                    }

                }
                else
                {
                    //false if the I/O operation completed synchronously.
                    //In this case, The Completed event on the e parameter will not be raised and
                    //the e object passed as a parameter may be examined immediately after the method call
                    //returns to retrieve the result of the operation.


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

                        //_recvBuffer.WriteBuffer(_recvAsyncEventArgs.Buffer, _recvAsyncEventArgs.Offset, readByteCount);
                    }

                }

                //if (!_socket.ReceiveAsync(_recvAsyncEventArgs))
                //{

                //    //Returns
                //    //Boolean 
                //}
                //else
                //{
                //    //receive async,
                //    //but in this method => convert to sync
                //    //SYNC, we need to WAIT for data ... 
                //    //ref: http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
                //    //--------------------------------
                //    lock (_waitLock)
                //        while (!_recvComplete)
                //            Monitor.Wait(_waitLock); //?
                //}
                //------------------
                //sync model
                //------------------
                if (_passHandshake)
                {
                    RaiseRecvCompleted(readByteCount);
                }
            }
            //------------------------------------------------------------------------
            //
            int actualReadByteCount = 0;
            lock (_recvLock)
            {
                //read data from _recvBuffer 
                _recvBuffer.ReadBuffer(buffer, offset, count);
                actualReadByteCount = count;//??
                //_recvMemStream.Position = _recvReadPos; //goto latest read poos
                ////
                //actualReadByteCount = _recvMemStream.Read(buffer, offset, count);
            }
            //_recvReadPos += actualReadByteCount;
            return actualReadByteCount;
        }


        //MemoryStream _msTemp;
        //int _msTempStartAt = 0;
        //int _msTempLen = 0;
        bool _hasDataInTempMem;

        void ProcessWaitingDataInTempMem()
        {
            _sendAsyncEventArgs.SetBuffer(_sendBuffer._largeBuffer, 0, _sendBuffer._len);
            _hasDataInTempMem = false;
        }
        //void ProcessWaitingDataInTempMem()
        //{
        //    //recursive ***

        //    //copy data from temp mem and write to buffer
        //    int count = _recvBuffer._len;
        //    if (_msTempLen > count)
        //    {
        //        count = _recvBuffer._len;
        //        _msTempLen -= count;
        //        _msTempStartAt += count;
        //    }
        //    else
        //    {
        //        _hasDataInTempMem = false;
        //    }

        //    _sendAsyncEventArgs.SetBuffer(0, _msTempLen); //clear before each write

        //    _msTemp.Read(_sendBuffer._largeBuffer, _msTempStartAt, _msTempLen);

        //    if (!_socket.SendAsync(_sendAsyncEventArgs))
        //    {
        //        //Returns false if the I / O operation completed synchronously.****
        //        //Returns true if data is pending (send async)
        //        _sendingByteTransfered = count;
        //        if (_hasDataInTempMem)
        //        {
        //            ProcessWaitingDataInTempMem();//recursive
        //        }
        //        else
        //        {
        //            lock (_sendWaitLock)
        //            {
        //                _sendComplete = true;
        //                Monitor.Pulse(_sendWaitLock);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        //sending data is pending...
        //        //send complete event will be raised
        //        //in this case, this Write() method is sync method
        //        //so we need to wait when the writing out is complete 

        //    } 
        //}
        public override void Write(byte[] buffer, int offset, int count)
        {

            //buffer contains 'Encrypted' data frorm ssl stream 
            //are write to socket  
            //copy data to buffer and start send
#if DEBUG

#endif

            if (count > _sendBuffer._len)
            {
                //if (!_passHandshake)
                //{
                //}
                ////if input data is larger than buffer
                ////we must copy it into temp mem
                //if (_msTemp == null)
                //{
                //    _msTemp = new MemoryStream();
                //}

                //_msTemp.SetLength(0);
                //_msTempStartAt = 0;
                //_hasDataInTempMem = true;
                //_msTempLen = count - _recvBuffer._len;
                //_msTemp.Write(buffer, offset + _recvBuffer._len - 1, _msTempLen);

                ////****
                //count = _recvBuffer._len; //changed
                ////*****
                ///
                if (_passHandshake)
                {

                }
                byte[] tmpBuffer = new byte[count];
                _sendAsyncEventArgs.SetBuffer(tmpBuffer, 0, count);
                _hasDataInTempMem = true;
            }
            else
            {
                _sendAsyncEventArgs.SetBuffer(0, count); //clear before each write
            }

            Buffer.BlockCopy(buffer, offset, _sendAsyncEventArgs.Buffer, 0, count);

            if (!_socket.SendAsync(_sendAsyncEventArgs))
            {
                //Returns false if the I / O operation completed synchronously.****
                //Returns true if data is pending (send async)
                //_sendingByteTransfered = count;
            }
            else
            {
                //sending data is pending...
                //send complete event will be raised
                //in this case, this Write() method is sync method
                //so we need to wait when the writing out is complete

                lock (_sendWaitLock)
                    while (!_sendComplete)
                        Monitor.Wait(_sendWaitLock); //? 
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
            System.Diagnostics.Debug.WriteLine("start send");
            _sendIO.StartSendAsync();///***
        }

#if DEBUG
        int dbugStartRecvCount = 0;
#endif


        object _startRecvLock = new object();
        bool _startRecv = false;

        public override void StartReceive()
        {
            _passHandshake = true; //start recv data from client 

            //lock (_recvWaitLock2)                 // Let's now wake up the thread by
            //{                              // setting _go=true and pulsing.
            //    if (_recvAsyncStarting)
            //    {
            //        return;
            //    }
            //    _recvAsyncStarting = true;
            //}
            //
            lock (_startRecvLock)
            {
                if (_startRecv)
                {
                    return;
                }
                else
                {

                }
                _startRecv = true;
            }
            //----------------------

#if DEBUG
            dbugStartRecvCount++;
#endif
            //if (dbugStartRecvCount > 100)
            //{

            //}

            if (!_socket.ReceiveAsync(_recvAsyncEventArgs))
            {
                //lock (_recvWaitLock2)                 // Let's now wake up the thread by
                //{
                //    if (_recvAsyncStarting)
                //    {
                //        _recvAsyncStarting = false;
                //    }
                //}

                //Returns
                //Boolean

                //true if the I/O operation is pending. 
                //The Completed event on the e parameter will be raised upon completion of the operation.

                //false if the I/O operation completed synchronously.
                //In this case, The Completed event on the e parameter will not be raised and
                //the e object passed as a parameter may be examined immediately after 
                //the method call returns to retrieve the result of the operation. 
                //
                int readByteCount = 0;
                lock (_recvLock)
                {
                    readByteCount = _recvAsyncEventArgs.BytesTransferred;
                    //--------------
                    //write 'encrypted' data from socket to memory stream
                    //the ssl stream that wrap this stream will decode the data later***
                    //--------------
                    _recvBuffer.AppendWriteByteCount(readByteCount);
                    _recvAsyncEventArgs.SetBuffer(_recvBuffer.WriteIndex, _recvBuffer.RemainingWriteSpace);
                    //_recvBuffer.WriteBuffer(_recvAsyncEventArgs.Buffer, _recvAsyncEventArgs.Offset, readByteCount);
                }
                if (_passHandshake)
                {

                    //after complete we clear recv buffer
                    RaiseRecvCompleted(readByteCount);
                    //
                    _recvBuffer.Reset();
                    _recvAsyncEventArgs.SetBuffer(_recvBuffer.BufferStartAtIndex, _recvBuffer.BufferLength);
                }
            }

        }
        public bool HasMoreRecvData()
        {
            return _recvBuffer.HasDataToRead;
        }
    }

    delegate void AuthenCallbackDelegate();




    class SecureSockNetworkStream : AbstractAsyncNetworkStream
    {
        IOBuffer _recvBuffer;
        SslStream _sslStream;
        SockNetworkStream _socketNetworkStream; //base stream
        object _recvAppendLock = new object();
        System.Security.Cryptography.X509Certificates.X509Certificate2 _cert;

        System.IO.MemoryStream _enqueueOutputData = new MemoryStream();

        object _sendingStateLock = new object();

        public SecureSockNetworkStream(SockNetworkStream socketNetworkStream,
            System.Security.Cryptography.X509Certificates.X509Certificate2 cert,
            System.Net.Security.RemoteCertificateValidationCallback remoteCertValidationCb = null)
        {
            _sslStream = new SslStream(socketNetworkStream, true, remoteCertValidationCb, null);
            _cert = cert;

            _socketNetworkStream = socketNetworkStream;
            _socketNetworkStream.SetRecvCompleteEventHandler(SocketNetworkStream_RecvCompleted);
            _socketNetworkStream.SetSendCompleteEventHandler(SocketNetworkStream_SendCompleted);

            //
            byte[] tmpIOBuffer = new byte[2048];//TODO: alloc this from external 
            _recvBuffer = new IOBuffer(tmpIOBuffer, 0, tmpIOBuffer.Length);
        }

        public override byte[] UnsafeGetInternalBuffer() => _recvBuffer._largeBuffer;

        internal override void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen)
        {
            _recvBuffer.CopyBuffer(readpos, dstBuffer, 0, copyLen);
        }

        internal override byte RecvReadByte(int pos) => _recvBuffer.CopyByte(pos);

        internal override void EnqueueSendData(byte[] buffer, int len)
        {
            _enqueueOutputData.Write(buffer, 0, len);
        }


        object _startSendLock = new object();
        bool _startSending;

        public override void StartSend()
        {
            lock (_startSendLock)
            {
                if (_startSending)
                {
                    return;
                }
                _startSending = true;
            }
            //upper layer call this 
            //: check waiting data and send it 
            byte[] buffer = _enqueueOutputData.ToArray();

#if DEBUG
            System.Diagnostics.Debug.WriteLine(System.Text.Encoding.UTF8.GetString(buffer));
#endif

            _enqueueOutputData.SetLength(0);

            lock (_sendingStateLock)
            {
                _sslStream.Write(buffer);
            }
        }
        public override void Reset()
        {
            _recvBuffer.Reset();
            _socketNetworkStream.Reset();
        }
        public override void ClearReceiveBuffer()
        {
            _recvBuffer.Reset();
            _socketNetworkStream.ClearReceiveBuffer();
        }
        internal override void UnbindSocket()
        {
            _socketNetworkStream.UnbindSocket();
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
                    _sslStream.EndAuthenticateAsServer(state);
                    whenFinish();
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

        void SocketNetworkStream_SendCompleted(object sender, EventArgs e)
        {
            lock (_startSendLock)
            {
                _startSending = false;
            }

            RaiseSendComplete();
        }
        private void SocketNetworkStream_RecvCompleted(object sender, DataArrEventArgs e)
        {
#if  DEBUG
            if (BeginWebsocketMode)
            {

            }
#endif
            if (e.ByteTransferedCount > 0)
            {
                //.... 
                lock (_recvAppendLock)
                {
                    _recvBuffer.WriteBufferFromStream(_sslStream);
                    while (_socketNetworkStream.HasMoreRecvData())
                    {
                        _recvBuffer.WriteBufferFromStream(_sslStream);
                    }
                    _socketNetworkStream.ResetRecvStream();
                }

            }
            _startRecv = false;
            //-----------
            //this should notify the ssl that we have some data arrive
            RaiseRecvCompleted(_recvBuffer.DataToReadLength);

            _recvBuffer.Reset();
        }

        public override void ReadBuffer(int srcIndex, int copyCount, byte[] dstBuffer, int dstIndex)
        {
            _recvBuffer.CopyBuffer(srcIndex, dstBuffer, dstIndex, copyCount);
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

            if (_startRecv)
            {
                return;
            }
            _startRecv = true;
            _socketNetworkStream.StartReceive();
        }

        public override int ByteReadTransfered => _recvBuffer.DataToReadLength;

        public override byte GetByteFromBuffer(int index)
        {
            return _recvBuffer.GetByteFromBuffer(index);
        }

    }
}