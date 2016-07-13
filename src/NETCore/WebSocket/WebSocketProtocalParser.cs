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

    class WebSocketProtocalParser
    {

        Queue<WebSocketRequest> incommingReqs = new Queue<WebSocketRequest>();
        WebSocketRequest currentReq; 
        RecvIO recvIO;
        int socketRecvState;
        int remainingBytes;
        internal const int RECV_BUFFER_SIZE = 512;
        byte[] maskKey = null;

        internal WebSocketProtocalParser(RecvIO recvIO)
        {
            this.recvIO = recvIO;
        }

        public int ReqCount
        {
            get
            {
                return incommingReqs.Count;
            }
        }
        public WebSocketRequest Dequeue()
        {
            return incommingReqs.Dequeue();
        } 
        internal ProcessReceiveBufferResult ParseRecvData()
        {
            int readpos = 0;
            int txByteCount = recvIO.BytesTransferred;

            if (socketRecvState == 1)
            {
                //TODO: review here 
                readpos = 0;
                if (remainingBytes > 0)
                {
                    if (remainingBytes <= txByteCount)
                    {
                        //must complete prev round
                        //and start new frame
                        byte[] data = new byte[remainingBytes];
                        recvIO.ReadTo(readpos, data, remainingBytes);
                        readpos += remainingBytes;
                        socketRecvState = 0;
                        remainingBytes = 0; //complete at this frame 
                    }
                    else
                    {
                        //not complete in this frame
                    }

                }
                else
                {

                }
            }
            else if (socketRecvState == 2)
            {
            }

            if (socketRecvState == 1)
            {

            }

            if (txByteCount > RECV_BUFFER_SIZE)
            {
                return 0;
            }
            int round = 0;
            for (; ; )
            {
                round++;
                //just read  
                if (socketRecvState == 1)
                {
                    //not complete from prev recv
                    //so  we need more byte= remainingBytes
                    if (remainingBytes > 0)
                    {

                    }

                }
                if (txByteCount == RECV_BUFFER_SIZE && (readpos == txByteCount))
                {
                    //read more? 
                    //max then need to receive next
                    socketRecvState = 1;
                    return ProcessReceiveBufferResult.Continue;
                }
                else if (readpos == txByteCount)
                {
                    //finished
                    return ProcessReceiveBufferResult.Complete;
                }

            START_NEW_REQ:

                currentReq = new WebSocketRequest();
                incommingReqs.Enqueue(currentReq);


                byte b1 = recvIO.ReadByte(readpos);
                readpos++;
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
                Opcode opCode = (Opcode)(b1 & 0x0f);//4 bits 
                currentReq.OpCode = opCode;
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

                //we should check receive frame here ...
                if (mask == Mask.Off)
                {
                    throw new NotSupportedException();
                }
                //----------------------------------------------------------
                // Payload Length
                byte payloadLen = (byte)(b2 & 0x7f); //is 7 bits of the b2 
                if (fin == Fin.More || opCode == Opcode.Cont)
                {
                    //process fragment frame ***

                }
                else
                {

                }

                //----------------------------------------------------------
                //translate opcode ....
                string errCode = null;
                switch (opCode)
                {
                    case Opcode.Cont:
                        {
                            //continue
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
                            if (fin != Fin.More)
                            {
                                errCode = "An unsupported opcode.";
                            }
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
                switch (opCode)
                {
                    default:
                        {
                            if (fin == Fin.More)
                            {
                                //ulong fullPayloadLen = GetFullPayloadLength(payloadLen, fullPayloadLengthBuffer);

                                //if (fullPayloadLen > 126)
                                //{
                                //    //large data sendback

                                //} 
                                ////check if we can get data by one loop ?
                                //int readLen = 0;
                                //if (readpos > txByteCount)
                                //{

                                //}
                                //if (readpos + (int)fullPayloadLen > txByteCount)
                                //{
                                //    //then this is not complete data frame
                                //    readLen = txByteCount - readpos;
                                //    socketRecvState = 1;//not complete
                                //    remainingBytes = (int)fullPayloadLen - readLen;
                                //}
                                //else
                                //{
                                //    readLen = (int)fullPayloadLen;
                                //}

                                //ReadBodyContentFragmentMode(ref readpos, readLen, round, mask == Mask.On);

                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        break;
                    case Opcode.Close:
                        {
                            //close current protocol
                            //notify onclose ***
                            byte[] data = new byte[payloadLen];
                            recvIO.ReadTo(readpos, data, payloadLen);
                            readpos += payloadLen;
                            //-------------------------------
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
                            throw new NotSupportedException();
                        } break;
                    case Opcode.Pong:
                        {
                            throw new NotSupportedException();
                        } break;
                    //data frame
                    case Opcode.Cont:
                    case Opcode.Binary:
                    case Opcode.Text:
                        {
                            //----------------------------------------------------
                            //find full payload length
                            ulong fullPayloadLen = GetFullPayloadLength(payloadLen, fullPayloadLengthBuffer);
                            if (fullPayloadLen > 126)
                            {
                                //large data sendback 
                            }

                            //check if we can get data by one loop ?
                            int readLen = 0;
                            if (readpos > txByteCount)
                            {

                            }

                            //-------------------------------------------------
                            int txDiff = (txByteCount - (readpos + (int)fullPayloadLen));
                            if (txDiff == 0)
                            {
                                //complete
                                readLen = (int)fullPayloadLen;
                                ReadBodyContent(ref readpos, readLen, round, mask == Mask.On, opCode);
                                return ProcessReceiveBufferResult.Complete;
                            }
                            else if (txDiff > 0)
                            {
                                //more data for next frame
                                readLen = (int)fullPayloadLen;
                                ReadBodyContent(ref readpos, readLen, round, mask == Mask.On, opCode);
                                goto START_NEW_REQ;
                            }
                            else
                            {
                                //need more data for this frame
                                //then this is not complete data frame
                                readLen = txByteCount - readpos;
                                socketRecvState = 1;//not complete
                                remainingBytes = (int)fullPayloadLen - readLen;

                                ReadBodyContent(ref readpos, readLen, round, mask == Mask.On, opCode);

                                socketRecvState = 1;
                                return ProcessReceiveBufferResult.Continue;
                            }
                        } break;
                }
            }

            return ProcessReceiveBufferResult.Complete;
        }
        void ReadBodyContent(ref int readpos, int readLen, int round, bool mask, Opcode opCode)
        {
            byte[] data = new byte[readLen];
            recvIO.ReadTo(readpos, data, readLen);
            readpos += readLen;
            if (mask)
            {
                //unmask
                MaskAgain(data, maskKey);
            }
            switch (opCode)
            {
                case Opcode.Text:
                    {

                        if (round == 1)
                        {
                            currentReq.SetRawBuffer(data);
                        }
                        else
                        {
                            currentReq.AddNewFrame(data);
                        }
                    } break;
                case Opcode.Binary:
                    {

                        if (round == 1)
                        {
                            currentReq.SetRawBuffer(data);
                        }
                        else
                        {
                            currentReq.AddNewFrame(data);
                        }
                    } break;
                case Opcode.Cont:

                    if (round == 1)
                    {
                        currentReq.SetRawBuffer(data);
                    }
                    else
                    {
                        currentReq.AddNewFrame(data);
                    }
                    break;
                default:
                    {
                        throw new NotSupportedException();
                    }
            }
        }
        void ReadBodyContentFragmentMode(ref int readpos, int readLen, int round, bool mask)
        {
            byte[] data = new byte[readLen];
            recvIO.ReadTo(readpos, data, readLen);
            readpos += readLen;
            if (mask)
            {
                //unmask
                MaskAgain(data, maskKey);
            }
            if (round == 1)
            {
                currentReq.SetRawBuffer(data);
            }
            else
            {
                currentReq.AddNewFrame(data);
            }

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