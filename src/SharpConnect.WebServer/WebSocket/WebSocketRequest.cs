﻿//MIT, 2015-present, EngineKit 
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
namespace SharpConnect.WebServers
{

    public class WebSocketRequest
    {
        byte[] _data;
        internal WebSocketRequest()
        {
        }
        public Opcode OpCode
        {
            get;
            internal set;
        }
        internal void SetData(byte[] newDataBuffer)
        {
            if (_data != null)
            {
                throw new NotSupportedException();
            }
            _data = newDataBuffer;
        }
        public string ReadAsString()
        {
            if (_data != null && this.OpCode == Opcode.Text)
            {
                return System.Text.Encoding.UTF8.GetString(_data);
            }
            else
            {
                return null;
            }
        }
        public char[] ReadAsChars()
        {
            if (_data != null && this.OpCode == Opcode.Text)
            {
                return System.Text.Encoding.UTF8.GetChars(_data);
            }
            else
            {
                return null;
            }
        }
    }
}