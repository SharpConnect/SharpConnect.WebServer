//MIT, 2015-present, EngineKit 
/* The MIT License
*
* Copyright (c) 2012-2015 sta.blockhead
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using SharpConnect.WebServers;

namespace SharpConnect.Internal2
{
    class WebSocketProtocolParser2
    {

        enum ParseState
        {
            Init,
            ReadExtendedPayloadLen,
            ReadMask,
            ExpectBody,
            Complete
        }

        Queue<WebSocketRequest> _incommingReqs = new Queue<WebSocketRequest>();
        SharpConnect.Internal2.RecvIOBufferStream2 _myBufferStream;
        WebSocketRequest _currentReq;

        ParseState _parseState;

        int _currentPacketLen;
        int _currentMaskLen;
        //-----------------------
        byte[] _maskKey = new byte[4];
        byte[] _fullPayloadLengthBuffer = new byte[8];
        bool _useMask;
        Opcode _currentOpCode = Opcode.Cont;//use default                                              

        bool _asClientContext;

        internal WebSocketProtocolParser2(bool asClientContext, SharpConnect.Internal2.RecvIOBufferStream2 recvBufferStream)
        {
            _asClientContext = asClientContext;
            _myBufferStream = recvBufferStream;
        }

        public int ReqCount => _incommingReqs.Count;

        public WebSocketRequest Dequeue() => _incommingReqs.Dequeue();

        bool ReadHeader()
        {
            if (!_myBufferStream.Ensure(2))
            {
                _myBufferStream.BackupRecvIO();
                return false;
            }
            //----------------------------------------------------------
            //when we read header we start a new websocket request
            _currentReq = new WebSocketRequest();
            _incommingReqs.Enqueue(_currentReq);


            byte b1 = _myBufferStream.ReadByte();
            // FIN
            Fin fin = (b1 & (1 << 7)) == (1 << 7) ? Fin.Final : Fin.More;

            // RSV1
            Rsv rsv1 = (b1 & (1 << 6)) == (1 << 6) ? Rsv.On : Rsv.Off;

            // RSV2
            Rsv rsv2 = (b1 & (1 << 5)) == (1 << 5) ? Rsv.On : Rsv.Off;

            // RSV3
            Rsv rsv3 = (b1 & (1 << 4)) == (1 << 4) ? Rsv.On : Rsv.Off;
            //----------------------------------------------------------   
            // Opcode
            _currentOpCode = (Opcode)(b1 & 0x0f);//4 bits  
            //----------------------------------------------------------  
            byte b2 = _myBufferStream.ReadByte();          //mask

            //----------------------------------------------------------  
            //finish first 2 bytes  
            // MASK
            Mask currentMask = (b2 & (1 << 7)) == (1 << 7) ? Mask.On : Mask.Off;
            //we should check receive frame here ... 

            if (_asClientContext)
            {
                //as client context (we are in client context)
                if (currentMask != Mask.Off) throw new NotSupportedException();
                _useMask = false;
            }
            else
            {
                //as server context (we are in server context)
                //data from client must useMask
                if (currentMask != Mask.On) throw new NotSupportedException();
                _useMask = true;
            }
            //----------------------------------------------------------
            // Payload Length
            byte payloadLen = (byte)(b2 & 0x7f); //is 7 bits of the b2 

            if (fin == Fin.More || _currentOpCode == Opcode.Cont)
            {
                //process fragment frame *** 
                throw new NotSupportedException();
            }
            else
            {

            }

            //----------------------------------------------------------
            //translate opcode ....
            string errCode = null;
            switch (_currentOpCode)
            {
                case Opcode.Cont:
                    {
                        //continue
                    }
                    break;
                case Opcode.Text: //this is data
                    {
                        if (rsv1 == Rsv.On)
                        {
                            errCode = "A non data frame is compressed.";
                        }
                    }
                    break;
                case Opcode.Binary: //this is data
                    {
                        if (rsv1 == Rsv.On)
                        {
                            errCode = "A non data frame is compressed.";
                        }
                    }
                    break;
                case Opcode.Close: //control
                    {
                        if (fin == Fin.More)
                        {
                            errCode = "A control frame is fragmented.";
                        }
                        else if (payloadLen > 125)
                        {
                            errCode = "A control frame has a long payload length.";
                        }
                    }
                    break;
                case Opcode.Ping: //control
                    {
                        if (fin == Fin.More)
                        {
                            errCode = "A control frame is fragmented.";
                        }
                        else if (payloadLen > 125)
                        {
                            errCode = "A control frame has a long payload length.";
                        }
                    }
                    break;
                case Opcode.Pong: //control
                    {
                        if (fin == Fin.More)
                        {
                            errCode = "A control frame is fragmented.";
                        }
                        else if (payloadLen > 125)
                        {
                            errCode = "A control frame has a long payload length.";
                        }
                    }
                    break;
                default:
                    {
                        if (fin != Fin.More)
                        {
                            errCode = "An unsupported opcode.";
                        }
                    }
                    break;
            }
            //----------------------------------------------------------
            if (errCode != null)
            {
                //report error
                throw new NotSupportedException();
            }
            //----------------------------------------------------------  
            _currentPacketLen = payloadLen;
            _currentReq.OpCode = _currentOpCode;
            _currentMaskLen = (currentMask == Mask.On) ? 4 : 0;
            //----------------------------------------------------------
            if (payloadLen >= 126)
            {
                _parseState = ParseState.ReadExtendedPayloadLen;
                return true;
            }
            //----------------------------------------------------------
            _parseState = _currentMaskLen > 0 ?
                ParseState.ReadMask :
                ParseState.ExpectBody;
            return true;
        }
        bool ReadPayloadLen()
        {
            int extendedPayloadByteCount = (_currentPacketLen == 126 ? 2 : 8);
            if (!_myBufferStream.Ensure(extendedPayloadByteCount))
            {
                _myBufferStream.BackupRecvIO();
                return false;
            }
            //----------------------------------------------------------

            _myBufferStream.CopyBuffer(_fullPayloadLengthBuffer, extendedPayloadByteCount);

            ulong org_packetLen1 = GetFullPayloadLength(_currentPacketLen, _fullPayloadLengthBuffer);
            if (org_packetLen1 >= int.MaxValue)
            {
                //in this version ***
                throw new NotSupportedException();
            }
            _currentPacketLen = (int)org_packetLen1;
            _parseState = _currentMaskLen > 0 ?
                     ParseState.ReadMask :
                     ParseState.ExpectBody;
            return true;
        }
        bool ReadMask()
        {
            if (!_myBufferStream.Ensure(_currentMaskLen))
            {
                _myBufferStream.BackupRecvIO();
                return false;
            }
            //---------------------------------------------------------- 
            //read mask data                     

            _myBufferStream.CopyBuffer(_maskKey, _currentMaskLen);
            _parseState = ParseState.ExpectBody;
            return true;
        }

        internal ProcessReceiveBufferResult ParseRecvData()
        {
            _myBufferStream.AppendNewRecvData();

            for (; ; )
            {

                switch (_parseState)
                {
                    default:
                        throw new NotSupportedException();
                    case ParseState.Init:

                        if (!ReadHeader())
                        {
                            return ProcessReceiveBufferResult.NeedMore;
                        }
                        break;
                    case ParseState.ReadExtendedPayloadLen:

                        if (!ReadPayloadLen())
                        {
                            return ProcessReceiveBufferResult.NeedMore;
                        }
                        break;
                    case ParseState.ReadMask:

                        if (!ReadMask())
                        {
                            return ProcessReceiveBufferResult.NeedMore;
                        }

                        break;
                    case ParseState.ExpectBody:
                        {
                            //------------------------------------- 
                            switch (_currentOpCode)
                            {
                                //ping,
                                //pong
                                default:
                                    throw new NotSupportedException();
                                case Opcode.Binary:
                                case Opcode.Text:
                                case Opcode.Close:
                                    break;
                            }

                            if (!ReadBodyContent(_currentPacketLen))
                            {
                                return ProcessReceiveBufferResult.NeedMore;
                            }


                            //pass 
                            _parseState = ParseState.Init;

                            _myBufferStream.Clear();
                            //TODO: 
                            return ProcessReceiveBufferResult.Complete;

                            //if (myBufferStream.IsEnd())
                            //{
                            //    myBufferStream.Clear();
                            //    return ProcessReceiveBufferResult.Complete;
                            //}
                            //more than 1 byte 
                        }
                        break;
                }
            }
        }
        bool ReadBodyContent(int readLen)
        {
            if (!_myBufferStream.Ensure(readLen))
            {
                _myBufferStream.BackupRecvIO();
                return false;
            }
            //------------------------------------
            byte[] data = new byte[readLen];
            _myBufferStream.CopyBuffer(data, readLen);
            if (_useMask)
            {
                //unmask
                MaskAgain(data, _maskKey);
            }
            _currentReq.SetData(data);
            return true;
        }

        static ulong GetFullPayloadLength(int payloadLength, byte[] fullPayloadLengthBuffer)
        {
            // Payload length:  7 bits, 7+16 bits, or 7+64 bits

            //The length of the "Payload data", in bytes: if 0-125, that is the
            //payload length.  If 126, the following 2 bytes interpreted as a
            //16-bit unsigned integer are the payload length.  If 127, the
            //following 8 bytes interpreted as a 64-bit unsigned integer (the
            //most significant bit MUST be 0) are the payload length.  Multibyte
            //length quantities are expressed in network byte order.  Note that
            //in all cases, the minimal number of bytes MUST be used to encode
            //the length, for example, the length of a 124-byte-long string
            //can't be encoded as the sequence 126, 0, 124.  The payload length
            //is the length of the "Extension data" + the length of the
            //"Application data".  The length of the "Extension data" may be
            //zero, in which case the payload length is the length of the
            //"Application data". 
            return payloadLength < 126
                       ? (ulong)payloadLength //use that length
                       : payloadLength == 126
                         ? (ulong)(fullPayloadLengthBuffer[0] << 8 | fullPayloadLengthBuffer[1]) // 7+16 bits
                         : BitConverter.ToUInt64(fullPayloadLengthBuffer, 0);

        }

        static void MaskAgain(byte[] data, byte[] key)
        {
            for (int i = data.Length - 1; i >= 0; --i)
            {
                data[i] ^= key[i % 4];
            }
        }
    }


}