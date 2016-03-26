//2015-2016, MIT, EngineKit 
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
using System.IO;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{

    public class WebSocketRequest
    {
        byte[] data;
        List<byte[]> moreFrames = new List<byte[]>();
        RecvIO recvIO;
        int socketRecvState;
        int remainingBytes;

        internal WebSocketRequest(RecvIO recvIO)
        {
            this.recvIO = recvIO;
        }

        public Opcode OpCode { get; set; }
        void SetRawBuffer(byte[] data)
        {
            this.data = data;
        }
        public void AddNewFrame(byte[] newFrame)
        {
            moreFrames.Add(newFrame);
        }
        public byte[] GetRawData()
        {
            return data;
        }
        public string ReadAsString()
        {
            if (data != null && this.OpCode == Opcode.Text)
            {
                return System.Text.Encoding.UTF8.GetString(data);
            }
            else
            {
                return null;
            }
        }
        public char[] ReadAsChars()
        {
            if (data != null && this.OpCode == Opcode.Text)
            {
                return System.Text.Encoding.UTF8.GetChars(data);
            }
            else
            {
                return null;
            }
        }
        public bool LoadData()
        {
            ParseWebSocketRequestHeader();
            return true;
        }
        public void Clear()
        {
            moreFrames.Clear();
            data = null;
        }
        int ParseWebSocketRequestHeader()
        {
            int readpos = 0;
            if (socketRecvState == 1)
            {
                //skip remaining bytes
                readpos = remainingBytes;
                socketRecvState = 0;
                remainingBytes = 0;
            }
            else if (socketRecvState == 2)
            {
            }

            int txByteCount = recvIO.BytesTransferred;

            int round = 0;
            for (; ; )
            {
                round++;

                //just read  

                byte b1 = recvIO.ReadByte(readpos);
                readpos++;

                if (readpos >= txByteCount)
                {
                    break;
                }

                // FIN
                Fin fin = (b1 & (1 << 7)) == (1 << 7) ? Fin.Final : Fin.More;

                // RSV1
                Rsv rsv1 = (b1 & (1 << 6)) == (1 << 6) ? Rsv.On : Rsv.Off;

                // RSV2
                Rsv rsv2 = (b1 & (1 << 5)) == (1 << 5) ? Rsv.On : Rsv.Off;

                // RSV3
                Rsv rsv3 = (b1 & (1 << 4)) == (1 << 4) ? Rsv.On : Rsv.Off;

                // Opcode
                byte opcode = (byte)(b1 & 0x0f);//4 bits 

                //----------------------------------------------------------  
                byte b2 = recvIO.ReadByte(readpos); //mask
                readpos++;
                if (readpos >= txByteCount)
                {
                    //can't go next 
                    socketRecvState = 2;
                    break;
                }

                // MASK
                Mask mask = (b2 & (1 << 7)) == (1 << 7) ? Mask.On : Mask.Off;
                if (mask == Mask.Off)
                {

                    throw new NotSupportedException();
                }
                // Payload Length
                byte payloadLen = (byte)(b2 & 0x7f);
                //----------------------------------------------------------
                //translate opcode ....
                string errCode = null;
                switch ((Opcode)opcode)
                {
                    case Opcode.Cont:
                        {
                        } break;
                    case Opcode.Text: //this is data
                    case Opcode.Binary: //this is data
                        {
                            if (rsv1 == Rsv.On)
                            {
                                errCode = "A non data frame is compressed.";
                            }
                        } break;
                    case Opcode.Close: //control
                    case Opcode.Ping: //control
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
                        } break;
                    default:
                        {
                            errCode = "An unsupported opcode.";
                        } break;
                }
                //----------------------------------------------------------
                if (errCode != null)
                {
                    //report error
                    throw new NotSupportedException();
                }
                //---------------------------------------------------------- 
                int extendedPayloadLen = payloadLen < 126 ? 0 : (payloadLen == 126 ? 2 : 8);
                byte[] fullPayloadLengthBuffer = null;
                if (extendedPayloadLen > 0)
                {
                    if (readpos + extendedPayloadLen >= txByteCount)
                    {
                        break;
                    }
                    fullPayloadLengthBuffer = new byte[extendedPayloadLen];
                    recvIO.ReadTo(readpos, fullPayloadLengthBuffer, extendedPayloadLen);
                    readpos += extendedPayloadLen;
                }
                else
                {
                }
                //---------------------------------------------------------------
                //mask key
                int maskLen = (mask == Mask.On) ? 4 : 0;
                byte[] maskKey = null;
                if (maskLen > 0)
                {
                    if (readpos + maskLen >= txByteCount)
                    {
                        break;
                    }
                    //read mask data                     
                    maskKey = new byte[maskLen];
                    recvIO.ReadTo(readpos, maskKey, maskLen);
                    readpos += maskLen;
                    //throw new WebSocketException("The masking key of a frame cannot be read from the stream.");
                }
                //----------------------------------------------------
                //control frame
                switch ((Opcode)opcode)
                {
                    default:
                        {
                            throw new NotSupportedException();
                        }
                    case Opcode.Close:
                        {
                            //close current protocol
                            //notify onclose ***
                            byte[] data = new byte[payloadLen];
                            recvIO.ReadTo(readpos, data, payloadLen);
                            readpos += payloadLen;

                            if (mask == Mask.On)
                            {
                                //unmask
                                MaskAgain(data, maskKey);
                            }
                            remainingBytes = 0;
                            socketRecvState = 0;
                        } break;
                    case Opcode.Ping:
                        {

                        } break;
                    case Opcode.Pong:
                        {

                        } break;
                    //data frame
                    case Opcode.Cont:
                    case Opcode.Binary:
                    case Opcode.Text:
                        {
                            //----------------------------------------------------
                            //find full payload length
                            ulong fullPayloadLen = GetFullPayloadLength(payloadLen, fullPayloadLengthBuffer);
                            if (fullPayloadLen < 127)
                            {
                                if ((readpos + (int)fullPayloadLen) >= txByteCount)
                                {
                                }


                                //check if we can get data by one loop ?
                                int readLen = 0;
                                if (readpos > txByteCount)
                                {

                                }
                                if (readpos + (int)fullPayloadLen > txByteCount)
                                {
                                    //then this is not complete data frame
                                    readLen = txByteCount - readpos;
                                    socketRecvState = 1;//not complete
                                    remainingBytes = (int)fullPayloadLen - readLen;
                                }
                                else
                                {
                                    readLen = (int)fullPayloadLen;
                                }

                                if (readLen < 0)
                                {

                                }


                                byte[] data = new byte[readLen];
                                recvIO.ReadTo(readpos, data, readLen);
                                readpos += readLen;
                                if (mask == Mask.On)
                                {
                                    //unmask
                                    MaskAgain(data, maskKey);
                                }


                                switch ((Opcode)opcode)
                                {
                                    case Opcode.Text:
                                        {
                                            OpCode = Opcode.Text;
                                            if (round == 1)
                                            {
                                                SetRawBuffer(data);
                                            }
                                            else
                                            {
                                                AddNewFrame(data);
                                            }
                                        } break;
                                    case Opcode.Binary:
                                        {
                                            OpCode = Opcode.Binary;
                                            if (round == 1)
                                            {
                                                SetRawBuffer(data);
                                            }
                                            else
                                            {
                                                AddNewFrame(data);
                                            }
                                        } break;
                                    case Opcode.Cont:

                                        if (round == 1)
                                        {
                                            SetRawBuffer(data);
                                        }
                                        else
                                        {
                                            AddNewFrame(data);
                                        }


                                        break;
                                    default:
                                        throw new NotSupportedException();
                                }
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        } break;
                }
            }

            if (readpos != txByteCount + 1)
            {
                if (socketRecvState != 2)
                {

                }
            }

            return readpos;
        }
        static ulong GetFullPayloadLength(int _payloadLength, byte[] fullPayloadLengthBuffer)
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
            return _payloadLength < 126
                       ? (ulong)_payloadLength //use that length
                       : _payloadLength == 126
                         ? (ulong)(fullPayloadLengthBuffer[0] << 8 | fullPayloadLengthBuffer[1]) // 7+16 bits
                         : BitConverter.ToUInt64(fullPayloadLengthBuffer, 0);

        }

        static void MaskAgain(byte[] data, byte[] key)
        {
            int length = data.Length;
            for (int i = length - 1; i >= 0; --i)
            {
                data[i] ^= key[i % 4];
            }
        }



    }


}