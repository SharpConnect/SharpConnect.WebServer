//2010, CPOL, Stan Kirk
//MIT, 2015-present, EngineKit and contributors
//https://docs.microsoft.com/en-us/dotnet/framework/network-programming/socket-performance-enhancements-in-version-3-5

using System;
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
}
