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



    class RecvIOBuffer : IDisposable
    {
        SimpleBufferReader simpleBufferReader = new SimpleBufferReader();
        List<byte[]> otherBuffers = new List<byte[]>();
        int currentBufferIndex;

        bool multipartMode;
        int readpos = 0;
        int totalLen = 0;
        int bufferCount = 0;
        RecvIO _latestRecvIO;
        public RecvIOBuffer(RecvIO recvIO)
        {
            _latestRecvIO = recvIO;
            AutoClearPrevBufferBlock = true;
        }
        public bool AutoClearPrevBufferBlock
        {
            get;
            set;
        }
        public void Dispose()
        {

        }
        public void Clear()
        {

            otherBuffers.Clear();
            multipartMode = false;
            bufferCount = 0;
            currentBufferIndex = 0;
            readpos = 0;
            totalLen = 0;
            simpleBufferReader.SetBuffer(null, 0, 0);
        }

        public void AppendNewRecvData()
        {
            if (bufferCount == 0)
            {
                //single part mode                             
                totalLen = _latestRecvIO.BytesTransferred;
                simpleBufferReader.SetBuffer(_latestRecvIO.UnsafeGetInternalBuffer(), 0, totalLen);
                bufferCount++;
            }
            else
            {
                //more than 1 buffer
                if (multipartMode)
                {
                    int thisPartLen = _latestRecvIO.BytesTransferred;
                    byte[] o2copy = new byte[thisPartLen];
                    Buffer.BlockCopy(_latestRecvIO.UnsafeGetInternalBuffer(), 0, o2copy, 0, thisPartLen);
                    otherBuffers.Add(o2copy);
                    totalLen += thisPartLen;
                }
                else
                {
                    //should not be here
                    throw new NotSupportedException();
                }
                bufferCount++;
            }
        }

        public int Length
        {
            get
            {
                return this.totalLen;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="len"></param>
        /// <returns></returns>
        public bool Ensure(int len)
        {
            return readpos + len <= totalLen;
        }
        public void BackupRecvIO()
        {
            if (bufferCount == 1 && !multipartMode)
            {
                //only in single mode
                int thisPartLen = _latestRecvIO.BytesTransferred;
                byte[] o2copy = new byte[thisPartLen];
                Buffer.BlockCopy(_latestRecvIO.UnsafeGetInternalBuffer(), 0, o2copy, 0, thisPartLen);
                otherBuffers.Add(o2copy);
                multipartMode = true;
                int prevIndex = simpleBufferReader.Position;
                simpleBufferReader.SetBuffer(o2copy, 0, thisPartLen);
                simpleBufferReader.Position = prevIndex;
            }
        }
        public byte ReadByte()
        {
            if (simpleBufferReader.Ensure(1))
            {
                readpos++;
                return simpleBufferReader.ReadByte();
            }
            else
            {
                if (multipartMode)
                {
                    //this end of current buffer
                    //so we switch to the new one
                    if (currentBufferIndex < otherBuffers.Count)
                    {
                        MoveToNextBufferBlock();
                        readpos++;
                        return simpleBufferReader.ReadByte();
                    }
                }
            }
            throw new Exception();
        }
        void MoveToNextBufferBlock()
        {
            if (AutoClearPrevBufferBlock)
            {
                otherBuffers[currentBufferIndex] = null;
            }

            currentBufferIndex++;
            byte[] buff = otherBuffers[currentBufferIndex];
            simpleBufferReader.SetBuffer(buff, 0, buff.Length);
        }
        /// <summary>
        /// copy data from current pos to output
        /// </summary>
        /// <param name="output"></param>
        /// <param name="len"></param>
        public void CopyBuffer(byte[] output, int len)
        {
            if (simpleBufferReader.Ensure(len))
            {
                simpleBufferReader.CopyBytes(output, 0, len);
                readpos += len;
            }
            else
            {
                //need more than 1
                int toCopyLen = simpleBufferReader.AvaialbleByteCount;
                int remain = len;
                int targetIndex = 0;
                do
                {
                    simpleBufferReader.CopyBytes(output, targetIndex, toCopyLen);
                    readpos += toCopyLen;
                    targetIndex += toCopyLen;
                    remain -= toCopyLen;
                    //move to another
                    if (remain > 0)
                    {
                        if (currentBufferIndex < otherBuffers.Count - 1)
                        {
                            MoveToNextBufferBlock();
                            //-------------------------- 
                            //evaluate after copy
                            if (simpleBufferReader.Ensure(remain))
                            {
                                //end
                                simpleBufferReader.CopyBytes(output, targetIndex, remain);
                                readpos += remain;
                                remain = 0;
                                return;
                            }
                            else
                            {
                                //not complete on this round
                                toCopyLen = simpleBufferReader.UsedBufferDataLen;
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
        public bool IsEnd()
        {
            return readpos >= totalLen;
        }

    }

    class SimpleBufferReader
    {
        //TODO: check endian  ***
        byte[] originalBuffer;
        int bufferStartIndex;
        int readIndex;
        int usedBuffersize;
        byte[] buffer = new byte[16];
        public SimpleBufferReader()
        {

#if DEBUG

            if (dbug_EnableLog)
            {
                dbugInit();
            }
#endif
        }
        public void SetBuffer(byte[] originalBuffer, int bufferStartIndex, int bufferSize)
        {
            this.originalBuffer = originalBuffer;
            this.usedBuffersize = bufferSize;
            this.bufferStartIndex = bufferStartIndex;
            this.readIndex = bufferStartIndex; //auto
        }
        public bool Ensure(int len)
        {
            return readIndex + len <= usedBuffersize;
        }
        public int AvaialbleByteCount
        {
            get
            {
                return usedBuffersize - readIndex;
            }
        }
        public int Position
        {
            get
            {
                return readIndex;
            }
            set
            {
                readIndex = value;
            }
        }
        public void Close()
        {
        }
        public bool EndOfStream
        {
            get
            {
                return readIndex == usedBuffersize;
            }
        }
        public byte ReadByte()
        {

#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {
                //read from current index 
                //and advanced the readIndex to next***
                dbugWriteInfo(Position - 1 + " (byte) " + originalBuffer[readIndex + 1]);
            }
#endif          

            return originalBuffer[readIndex++];
        }
        public uint ReadUInt32()
        {
            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += 4;
            uint u = (uint)(mybuffer[s] | mybuffer[s + 1] << 8 |
                mybuffer[s + 2] << 16 | mybuffer[s + 3] << 24);

#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {
                dbugWriteInfo(Position - 4 + " (uint32) " + u);
            }
#endif

            return u;
        }
        public unsafe double ReadDouble()
        {
            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += 8;

            uint num = (uint)(((mybuffer[s] | (mybuffer[s + 1] << 8)) | (mybuffer[s + 2] << 0x10)) | (mybuffer[s + 3] << 0x18));
            uint num2 = (uint)(((mybuffer[s + 4] | (mybuffer[s + 5] << 8)) | (mybuffer[s + 6] << 0x10)) | (mybuffer[s + 7] << 0x18));
            ulong num3 = (num2 << 0x20) | num;

#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {
                dbugWriteInfo(Position - 8 + " (double) " + *(((double*)&num3)));
            }
#endif

            return *(((double*)&num3));
        }
        public unsafe float ReadFloat()
        {

            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += 4;

            uint num = (uint)(((mybuffer[s] | (mybuffer[s + 1] << 8)) | (mybuffer[s + 2] << 0x10)) | (mybuffer[s + 3] << 0x18));
#if DEBUG


            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {
                dbugWriteInfo(Position - 4 + " (float)");
            }
#endif

            return *(((float*)&num));
        }
        public int ReadInt32()
        {
            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += 4;
            int i32 = (mybuffer[s] | mybuffer[s + 1] << 8 |
                    mybuffer[s + 2] << 16 | mybuffer[s + 3] << 24);

#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {
                dbugWriteInfo(Position - 4 + " (int32) " + i32);
            }

#endif          
            return i32;

        }
        public short ReadInt16()
        {
            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += 2;
            short i16 = (Int16)(mybuffer[s] | mybuffer[s + 1] << 8);

#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }

            if (dbug_EnableLog)
            {

                dbugWriteInfo(Position - 2 + " (int16) " + i16);
            }
#endif

            return i16;
        }
        public ushort ReadUInt16()
        {
            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += 2;
            ushort ui16 = (ushort)(mybuffer[s + 0] | mybuffer[s + 1] << 8);
#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {
                dbugWriteInfo(Position - 2 + " (uint16) " + ui16);
            }

#endif
            return ui16;
        }
        public long ReadInt64()
        {
            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += 8;
            //
            uint num = (uint)(((mybuffer[s] | (mybuffer[s + 1] << 8)) | (mybuffer[s + 2] << 0x10)) | (mybuffer[s + 3] << 0x18));
            uint num2 = (uint)(((mybuffer[s + 4] | (mybuffer[s + 5] << 8)) | (mybuffer[s + 6] << 0x10)) | (mybuffer[s + 7] << 0x18));
            long i64 = ((long)num2 << 0x20) | num;
#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {

                dbugWriteInfo(Position - 8 + " (int64) " + i64);

            }
#endif
            return i64;
        }
        public ulong ReadUInt64()
        {
            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += 8;
            //
            uint num = (uint)(((mybuffer[s] | (mybuffer[s + 1] << 8)) | (mybuffer[s + 2] << 0x10)) | (mybuffer[s + 3] << 0x18));
            uint num2 = (uint)(((mybuffer[s + 4] | (mybuffer[s + 5] << 8)) | (mybuffer[s + 6] << 0x10)) | (mybuffer[s + 7] << 0x18));
            ulong ui64 = ((ulong)num2 << 0x20) | num;

#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {
                dbugWriteInfo(Position - 8 + " (int64) " + ui64);
            }
#endif

            return ui64;
        }
        public byte[] ReadBytes(int num)
        {
            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += num;
            byte[] buffer = new byte[num];

#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {
                dbugWriteInfo(Position - num + " (buffer:" + num + ")");
            }
#endif
            Buffer.BlockCopy(originalBuffer, s, buffer, 0, num);
            return buffer;
        }
        public void CopyBytes(byte[] buffer, int targetIndex, int num)
        {
            byte[] mybuffer = originalBuffer;
            int s = bufferStartIndex + readIndex;
            readIndex += num;

#if DEBUG
            if (dbug_enableBreak)
            {
                dbugCheckBreakPoint();
            }
            if (dbug_EnableLog)
            {
                dbugWriteInfo(Position - num + " (buffer:" + num + ")");
            }
#endif
            Buffer.BlockCopy(originalBuffer, s, buffer, targetIndex, num);
        }
        internal byte[] UnsafeGetInternalBuffer()
        {
            return this.originalBuffer;
        }
        internal int UsedBufferDataLen
        {
            get { return usedBuffersize; }
        }

#if DEBUG
        void dbugCheckBreakPoint()
        {
            if (dbug_enableBreak)
            {
                //if (Position == 35)
                //{
                //}
            }
        }

        bool dbug_EnableLog = false;
        bool dbug_enableBreak = false;
        FileStream dbug_fs;
        StreamWriter dbug_fsWriter;


        void dbugWriteInfo(string info)
        {
            if (dbug_EnableLog)
            {
                dbug_fsWriter.WriteLine(info);
                dbug_fsWriter.Flush();
            }
        }
        void dbugInit()
        {
            if (dbug_EnableLog)
            {
                //if (this.stream.Position > 0)
                //{

                //    dbug_fs = new FileStream(((FileStream)stream).Name + ".r_bin_debug", FileMode.Append);
                //    dbug_fsWriter = new StreamWriter(dbug_fs);
                //}
                //else
                //{
                //    dbug_fs = new FileStream(((FileStream)stream).Name + ".r_bin_debug", FileMode.Create);
                //    dbug_fsWriter = new StreamWriter(dbug_fs);
                //} 
            }
        }
        void dbugClose()
        {
            if (dbug_EnableLog)
            {

                dbug_fs.Dispose();
                dbug_fsWriter = null;
                dbug_fs = null;
            }

        }

#endif
    }


}