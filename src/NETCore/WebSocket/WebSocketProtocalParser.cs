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
using System.Collections.Generic;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{

    class WebSocketProtocolParser
    {

        enum ParseState
        {
            Init,
            ExpectBody,
            Complete
        }

        Queue<WebSocketRequest> incommingReqs = new Queue<WebSocketRequest>();

        WebSocketRequest currentReq;
        RecvIO recvIO;
        ParseState parseState;
        int remainingBytes;
        int currentPacketLen;

        //-----------------------
        byte[] maskKey = null;
        bool useMask;
        Opcode currentOpCode = Opcode.Cont;//use default 
        //-----------------------

        internal WebSocketProtocolParser(RecvIO recvIO)
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


        void ReadHeader(ref int readpos)
        {

            currentReq = new WebSocketRequest();
            incommingReqs.Enqueue(currentReq);

            int txByteCount = recvIO.BytesTransferred;
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
            currentOpCode = (Opcode)(b1 & 0x0f);//4 bits  
            //----------------------------------------------------------  
            byte b2 = recvIO.ReadByte(readpos); //mask
            readpos++;
            //----------------------------------------------------------  
            //finish first 2 bytes 

            // MASK
            Mask currentMask = (b2 & (1 << 7)) == (1 << 7) ? Mask.On : Mask.Off;
            //we should check receive frame here ...

            this.useMask = currentMask == Mask.On;

            if (currentMask == Mask.Off)
            {
                //if this act as WebSocketServer 
                //erro packet ? 
                throw new NotSupportedException();
            }
            else
            {

            }
            //----------------------------------------------------------
            // Payload Length
            byte payloadLen = (byte)(b2 & 0x7f); //is 7 bits of the b2 
            if (fin == Fin.More || currentOpCode == Opcode.Cont)
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
            switch (currentOpCode)
            {
                case Opcode.Cont:
                    {
                        //continue
                    }
                    break;
                case Opcode.Text: //this is data
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

            this.currentPacketLen = payloadLen;
            if (payloadLen >= 126)
            {
                int extendedPayloadByteCount = (payloadLen == 126 ? 2 : 8);
                if (readpos + extendedPayloadByteCount >= txByteCount)
                {
                    //can't read  
                    //for this version only
                    throw new NotSupportedException();
                }

                byte[] fullPayloadLengthBuffer = new byte[extendedPayloadByteCount];
                recvIO.ReadTo(readpos, fullPayloadLengthBuffer, extendedPayloadByteCount);
                readpos += extendedPayloadByteCount;
                ulong org_packetLen1 = GetFullPayloadLength(payloadLen, fullPayloadLengthBuffer);
                if (org_packetLen1 >= int.MaxValue)
                {
                    //in this version ***
                    throw new NotSupportedException();
                }
                this.currentPacketLen = (int)org_packetLen1;
            }
            currentReq.OpCode = currentOpCode;



            int maskLen = (currentMask == Mask.On) ? 4 : 0;
            if (maskLen > 0)
            {
                if (readpos + maskLen >= txByteCount)
                {
                    //in this version ***
                    throw new NotSupportedException();
                }
                //read mask data                     
                maskKey = new byte[maskLen];
                recvIO.ReadTo(readpos, maskKey, maskLen);
                readpos += maskLen;
                //throw new WebSocketException("The masking key of a frame cannot be read from the stream.");
            }
            //-----------------------------------   
            this.remainingBytes = (currentPacketLen + readpos) - txByteCount;
            //so remainingBytes may be negative
            parseState = ParseState.ExpectBody;

        }
        internal ProcessReceiveBufferResult ParseRecvData()
        {
            //in this round
            int readpos = 0;
            int txByteCount = recvIO.BytesTransferred;//***
            bool readMore = true;
            bool moreThanOnePacket = false;
            int saveRemainingByte = 0;
            if (parseState != ParseState.Init)
            {
                if ((remainingBytes - txByteCount) < 0)
                {
                    saveRemainingByte = remainingBytes;
                    moreThanOnePacket = true;
                }
                remainingBytes -= txByteCount;
            }
            while (readMore)
            {
                switch (parseState)
                {
                    default:
                        throw new NotFiniteNumberException();
                    case ParseState.Init:
                        //read header
                        if (txByteCount < 2)
                        {
                            throw new NotSupportedException();
                        }
                        //-------------------------------------
                        //packet len,  is calculate from this step 
                        ReadHeader(ref readpos);
                        break;
                    case ParseState.ExpectBody:
                        {

                            //control frame 
                            //-------------------------------
                            if (remainingBytes > 0)
                            {
                                ReadBodyContent(ref readpos, txByteCount - readpos, currentOpCode);
                                return ProcessReceiveBufferResult.NeedMore;
                            }
                            //-------------------------------
                            switch (currentOpCode)
                            {
                                //ping,
                                //pong
                                default:
                                    throw new NotSupportedException();
                                case Opcode.Binary:
                                case Opcode.Text:
                                case Opcode.Close:

                                    if (moreThanOnePacket)
                                    {
                                        //other req
                                        //review here
                                        ReadBodyContent(ref readpos, saveRemainingByte - readpos, currentOpCode);
                                        remainingBytes = 0;
                                        saveRemainingByte = 0;
                                        moreThanOnePacket = false;
                                        parseState = ParseState.Init;
                                        break;
                                    }
                                    else
                                    {
                                        ReadBodyContent(ref readpos, txByteCount - readpos, currentOpCode);
                                        parseState = ParseState.Init;
                                        return ProcessReceiveBufferResult.Complete;
                                    }

                            }
                        }
                        break;
                }
            }
            return ProcessReceiveBufferResult.Error;
        }
        void ReadBodyContent(ref int readpos, int readLen, Opcode opCode)
        {
            byte[] data = new byte[readLen];
            recvIO.ReadTo(readpos, data, readLen);
            readpos += readLen;
            if (useMask)
            {
                //unmask
                MaskAgain(data, maskKey);
            }
            currentReq.AppendData(data);
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