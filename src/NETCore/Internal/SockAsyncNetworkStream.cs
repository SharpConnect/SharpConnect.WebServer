//MIT, 2018, EngineKit
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Text;
using System.Threading;
namespace SharpConnect.Internal
{
    public class DataArrEventArgs : EventArgs
    {
        public int ByteTransferedCount { get; set; }
    }

    /// <summary>
    /// abstract socket network stream
    /// </summary>
    abstract class AbstractAsyncNetworkStream : Stream
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
        public void ClearRecvEvent()
        {
            _recvCompleted = null;
        }
        internal abstract byte RecvReadByte(int pos);
        internal abstract void EnqueueSendData(byte[] buffer, int len);
        internal abstract void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen);

        internal abstract void UnbindSocket();

    }


    class SockNetworkStream : AbstractAsyncNetworkStream
    {

        //resuable socket stream 

        Socket _socket;
        SocketAsyncEventArgs _sendAsyncEventArgs;
        SocketAsyncEventArgs _recvAsyncEventArgs;

        const int BUFFER_SIZE = 2048;


        IOBuffer _recvBuffer;
        IOBuffer _sendBuffer;

        object _recvLock = new object();
        bool _passHandshake;
        readonly int _recvStartOffset = 0;
        readonly int _recvBufferSize = BUFFER_SIZE;

        object _recvWaitLock = new object();
        bool _recvComplete = false;

        object _recvWaitLock2 = new object();
        bool _recvAsyncStarting = false;
        object _sendWaitLock = new object();
        bool _sendComplete = false;

        int _sendingByteTransfered = 0;

        readonly RecvIO recvIO;
        readonly SendIO sendIO;

        public SockNetworkStream(IOBuffer recvBuffer, IOBuffer sendBuffer)
        {
            //we assign buffer from external source
            _recvBuffer = recvBuffer;
            _sendBuffer = sendBuffer;

            //
            _sendAsyncEventArgs = new SocketAsyncEventArgs();
            _sendAsyncEventArgs.SetBuffer(_sendBuffer._largeBuffer, _sendBuffer.BufferStartAtIndex, _sendBuffer.BufferLength);
            _sendAsyncEventArgs.Completed += SendAsyncEventArgs_Completed;
            //
            _recvAsyncEventArgs = new SocketAsyncEventArgs();
            _recvAsyncEventArgs.SetBuffer(_recvBuffer._largeBuffer, _recvBuffer.BufferStartAtIndex, _recvBuffer.BufferLength);//TODO: swap  buffer for the args
            _recvAsyncEventArgs.Completed += RecvAsyncEventArgs_Completed;

            recvIO = new RecvIO(HandleReceive);
            recvIO.Bind(this);
            sendIO = new SendIO(HandleSend);
            sendIO.Bind(this);
        }

        public override void Reset()
        {
            //if (UsedBySslStream)
            //{
            //    _recvBuffer.Reset2();

            //}
            //_recvBuffer.Reset();
            //_sendBuffer.Reset();
            sendIO.Reset();

        }
        internal override void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen)
        {
            recvIO.CopyTo(readpos, dstBuffer, copyLen);
        }
        internal override byte RecvReadByte(int pos)
        {
            return recvIO.ReadByte(pos);
        }
        internal override void EnqueueSendData(byte[] buffer, int len)
        {
            sendIO.EnqueueOutputData(buffer, len);
        }
        void HandleReceive(RecvEventCode recvEventCode)
        {
            //switch (recvEventCode)
            //{
            //    case RecvEventCode.SocketError:
            //        {
            //            UnBindSocket(true);
            //        }
            //        break;
            //    case RecvEventCode.NoMoreReceiveData:
            //        {
            //            //no data to receive
            //            httpResp.End();
            //            //reqHandler(this.httpReq, httpResp);
            //        }
            //        break;
            //    case RecvEventCode.HasSomeData:
            //        {
            //            //process some data
            //            //there some data to process  
            //            switch (httpReq.LoadData(recvIO))
            //            {
            //                case ProcessReceiveBufferResult.Complete:
            //                    {
            //                        //recv and parse complete  
            //                        //goto user action 
            //                        if (this.EnableWebSocket &&
            //                            this.ownerServer.CheckWebSocketUpgradeRequest(this))
            //                        {
            //                            return;
            //                        }
            //                        reqHandler(this.httpReq, httpResp);
            //                    }
            //                    break;
            //                case ProcessReceiveBufferResult.NeedMore:
            //                    {
            //                        recvIO.StartReceive();
            //                    }
            //                    break;
            //                case ProcessReceiveBufferResult.Error:
            //                default:
            //                    throw new NotSupportedException();
            //            }
            //        }
            //        break;
            //}
        }
        void HandleSend(SendIOEventCode sendEventCode)
        {
            //switch (sendEventCode)
            //{
            //    case SendIOEventCode.SocketError:
            //        {
            //            UnBindSocket(true);
            //            KeepAlive = false;
            //        }
            //        break;
            //    case SendIOEventCode.SendComplete:
            //        {
            //            Reset();
            //            if (KeepAlive)
            //            {
            //                //next recv on the same client
            //                StartReceive();
            //            }
            //            else
            //            {
            //                UnBindSocket(true);
            //            }
            //        }
            //        break;
            //}
        }
        public bool UsedBySslStream { get; set; }
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
            //unbind socket ***
            Socket tmp = _socket;
            _sendAsyncEventArgs.AcceptSocket = null;
            _recvAsyncEventArgs.AcceptSocket = null;
            _socket = null;
            _passHandshake = false;

        }
        public override void ClearReceiveBuffer()
        {
            _recvAsyncEventArgs.SetBuffer(_recvStartOffset, _recvBufferSize);
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
            if (this.UsedBySslStream)
            {

            }
            //write data to _sendAsyncEventArgs
            _sendBuffer.WriteBuffer(srcBuffer, srcIndex, count);
            //then send***
            if (!_socket.SendAsync(_sendAsyncEventArgs))
            {
                //sync 
                return false;
            }
            else
            {
                return true;
            }

            //throw new NotImplementedException();
        }

        void RecvAsyncEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    {
                        //----------------------------------------
                        int bytesTransferred = e.BytesTransferred;
                        byte[] data = e.Buffer;
                        //----------------------------------------  


                        if (this.UsedBySslStream)
                        {
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
                        if (_passHandshake)
                        {
                            if (UsedBySslStream)
                            {

                            }
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
                    {

                    }
                    break;
                case SocketAsyncOperation.Send:
                    {
                        ////send complete 
                        if (UsedBySslStream)
                        {
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

                    if (UsedBySslStream)
                    {
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
                                if (UsedBySslStream)
                                {
                                    RaiseRecvCompleted(readByteCount);
                                }
                                else
                                {

                                }
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
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this.UsedBySslStream)
            {
                if (this._passHandshake)
                {
                    //
                }
            }
            //buffer contains 'Encrypted' data frorm ssl stream 
            //are write to socket  
            //copy data to buffer and start send
            _sendAsyncEventArgs.SetBuffer(0, count);
            Buffer.BlockCopy(buffer, offset, _sendAsyncEventArgs.Buffer, 0, count);

            if (!_socket.SendAsync(_sendAsyncEventArgs))
            {
                //Returns false if the I / O operation completed synchronously.****
                //Returns true if data is pending (send async)
                _sendingByteTransfered = count;
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
                _recvAsyncEventArgs.SetBuffer(_recvBuffer.BufferStartAtIndex, _recvBufferSize);

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
            sendIO.StartSendAsync();///***
        }

        int dbugStartRecvCount = 0;
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

            dbugStartRecvCount++;

            if (UsedBySslStream)
            {

            }

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
        bool _sending;
        object _sendingStateLock = new object();

        public SecureSockNetworkStream(SockNetworkStream socketNetworkStream, System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
        {

            _sslStream = new SslStream(socketNetworkStream, true);
            socketNetworkStream.UsedBySslStream = true;

            _cert = cert;

            _socketNetworkStream = socketNetworkStream;
            _socketNetworkStream.SetRecvCompleteEventHandler(SocketNetworkStream_RecvCompleted);
            _socketNetworkStream.SetSendCompleteEventHandler(SocketNetworkStream_SendCompleted);

            //
            byte[] tmpIOBuffer = new byte[2048];//TODO: alloc this from external 
            _recvBuffer = new IOBuffer(tmpIOBuffer, 0, tmpIOBuffer.Length);
        }
        internal override void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen)
        {
            _recvBuffer.CopyBuffer(readpos, dstBuffer, 0, copyLen);
            //System.Diagnostics.Debug.WriteLine("RecvCopyTo");
        }
        internal override byte RecvReadByte(int pos)
        {
            return _recvBuffer.CopyByte(pos);
            ////read byte from recv buffer at specific position
            //System.Diagnostics.Debug.WriteLine("recv_read_byte");
            //return 0;
        }
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
            _enqueueOutputData.SetLength(0);

            lock (_sendingStateLock)
            {
                _sending = true;
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
            _sslStream.BeginAuthenticateAsServer(_cert,
                state =>
                {
                    _sslStream.EndAuthenticateAsServer(state);

                    whenFinish();

                }, new object());
            //_sslStream.AuthenticateAsServer(_cert);
        }
        void SocketNetworkStream_SendCompleted(object sender, EventArgs e)
        {
            lock (_startSendLock)
            {
                _startSending = false;
            }
            lock (_sendingStateLock)
            {
                _sending = false;
            }
            RaiseSendComplete();
        }
        private void SocketNetworkStream_RecvCompleted(object sender, DataArrEventArgs e)
        {
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
                }
                _socketNetworkStream.ResetRecvStream();
            }
            _startRecv = false;
            //-----------
            //this should notify the ssl that we have some data arrive
            RaiseRecvCompleted(_recvBuffer.DataToReadLength);
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
            return false; //TODO: review here? 
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
            lock (_sendingStateLock)
            {
                if (_sending)
                {

                }
            }
            if (_startRecv)
            {
                return;
            }
            _startRecv = true;
            _socketNetworkStream.StartReceive();
        }


        public override int ByteReadTransfered
        {
            get
            {
                return _recvBuffer.DataToReadLength;
            }
        }
        public override byte GetByteFromBuffer(int index)
        {
            return _recvBuffer.GetByteFromBuffer(index);
        }

    }
}