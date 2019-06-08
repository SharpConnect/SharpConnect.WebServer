//2010, CPOL, Stan Kirk
//MIT, 2015-present, EngineKit and contributors
//https://docs.microsoft.com/en-us/dotnet/framework/network-programming/socket-performance-enhancements-in-version-3-5

using System;
using System.Collections.Generic;
using System.IO;

namespace SharpConnect
{
    enum RecvEventCode
    {
        SocketError,
        HasSomeData,
        NoMoreReceiveData,
    }
    enum SendIOEventCode
    {
        SendComplete,
        SocketError,
    }
    enum SendIOState : byte
    {
        ReadyNextSend,
        Sending,
        ProcessSending,
        Error,
    }
    class SimpleBufferReader
    {
        //TODO: check endian  ***
        byte[] _originalBuffer;
        int _bufferStartIndex;
        int _readIndex;
        int _usedBuffersize;
        byte[] _buffer = new byte[16];
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
            _originalBuffer = originalBuffer;
            _usedBuffersize = bufferSize;
            _bufferStartIndex = bufferStartIndex;
            _readIndex = bufferStartIndex; //auto
        }
        public bool Ensure(int len) => _readIndex + len <= _usedBuffersize;

        public int AvaialbleByteCount => _usedBuffersize - _readIndex;

        public int Position
        {
            get => _readIndex;
            set => _readIndex = value;
        }
        public void Close()
        {
        }

        public bool EndOfStream => _readIndex == _usedBuffersize;

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
                dbugWriteInfo(Position - 1 + " (byte) " + _originalBuffer[_readIndex + 1]);
            }
#endif          

            return _originalBuffer[_readIndex++];
        }
        public uint ReadUInt32()
        {
            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += 4;
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
            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += 8;

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

            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += 4;

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
            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += 4;
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
            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += 2;
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
            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += 2;
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
            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += 8;
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
            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += 8;
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
            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += num;
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
            Buffer.BlockCopy(_originalBuffer, s, buffer, 0, num);
            return buffer;
        }
        public void CopyBytes(byte[] buffer, int targetIndex, int num)
        {
            byte[] mybuffer = _originalBuffer;
            int s = _bufferStartIndex + _readIndex;
            _readIndex += num;

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
            Buffer.BlockCopy(_originalBuffer, s, buffer, targetIndex, num);
        }
        internal byte[] UnsafeGetInternalBuffer()
        {
            return _originalBuffer;
        }
        internal int UsedBufferDataLen
        {
            get { return _usedBuffersize; }
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

    interface IRecvIO
    {
        byte[] UnsafeGetInternalBuffer();
        int BytesTransferred { get; }
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

        IRecvIO _recvIO;
        public RecvIOBufferStream(IRecvIO recvIO)
        {
            _recvIO = recvIO;
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
                _totalLen = _recvIO.BytesTransferred;
                _simpleBufferReader.SetBuffer(_recvIO.UnsafeGetInternalBuffer(), 0, _totalLen);
                _bufferCount++;
            }
            else
            {
                //more than 1 buffer
                if (_multipartMode)
                {
                    int thisPartLen = _recvIO.BytesTransferred;
                    byte[] o2copy = new byte[thisPartLen];
                    Buffer.BlockCopy(_recvIO.UnsafeGetInternalBuffer(), 0, o2copy, 0, thisPartLen);
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
                int thisPartLen = _recvIO.BytesTransferred;
                byte[] o2copy = new byte[thisPartLen];
                Buffer.BlockCopy(_recvIO.UnsafeGetInternalBuffer(), 0, o2copy, 0, thisPartLen);
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
