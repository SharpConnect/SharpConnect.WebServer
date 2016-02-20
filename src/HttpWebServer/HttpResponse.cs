//2015, MIT, EngineKit
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace SharpConnect.WebServer
{  /// <summary>
    /// content type
    /// </summary>
    public enum WebResponseContentType
    {
        TextHtml,
        TextPlain,
        TextXml,
        ApplicationJson,
        TextJavascript,

        ImagePng,
        ImageJpeg,
        ApplicationOctetStream
    }
    public enum ContentEncoding
    {
        Ascii,
        Utf8,
        Hex
    }
    public enum ResponseTransferEncoding
    {
        Identity,//no encoding
        Chunked,
        Gzip,
        Compress,
        Deflate
    }



    public class HttpResponse : IDisposable
    {
        enum WriteContentState : byte
        {
            HttpHead,
            HttpBody,

            //-------------------
            //experiment
            SocketClientHanshake,
            SocketClientOK,
        }


        readonly HttpConnectionSession connSession;
        WriteContentState writeContentState;
        //output stream
        MemoryStream bodyMs;
        int contentByteCount;
        Dictionary<string, string> headers;
        StringBuilder headerStBuilder = new StringBuilder();


        internal HttpResponse(HttpConnectionSession connSession)
        {
            this.connSession = connSession;
            bodyMs = new MemoryStream();
            headers = new Dictionary<string, string>();
            StatusCode = 200; //init
        }
        internal void ResetAll()
        {
            headerStBuilder.Length = 0;
            StatusCode = 200;
            NeedFlush = false;
            TransferEncoding = ResponseTransferEncoding.Identity;
            writeContentState = WriteContentState.HttpHead;

            ResetWritingBuffer();
        }
        internal void ResetWritingBuffer()
        {
            bodyMs.Position = 0;
            contentByteCount = 0;
        }
        public void Dispose()
        {
            if (bodyMs != null)
            {
                bodyMs.Dispose();
                bodyMs = null;
            }
        }


        /// <summary>
        /// add new or replace if exists
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetHeader(string key, string value)
        {
            //replace exiting values
            //TODO: review custom header here
            headers[key] = value;
        }
        public WebResponseContentType ContentType
        {
            get;
            set;
        }
        /// <summary>
        /// write to output
        /// </summary>
        /// <param name="str"></param>
        public void Write(string str)
        {
            //write to output stream 
            var bytes = Encoding.UTF8.GetBytes(str.ToCharArray());
            //write to stream
            bodyMs.Write(bytes, 0, bytes.Length);
            contentByteCount += bytes.Length;
            NeedFlush = true;
        }
        public void Flush()
        {
            switch (writeContentState)
            {
                //generate head 
                case WriteContentState.HttpHead:
                    {

                        if (UpgradeToWebSocket)
                        {
                            headerStBuilder.Length = 0;
                            headerStBuilder.Append("HTTP/1.1 ");
                            headerStBuilder.Append("101 Switching Protocols\r\n");
                            headerStBuilder.Append("Upgrade: websocket\r\n");
                            headerStBuilder.Append("Connection: Upgrade\r\n");
                            headerStBuilder.Append("Sec-WebSocket-Accept: " + this.ResponseSecWebSocket + "\r\n");
                            headerStBuilder.Append("Content-Length: " + contentByteCount + "\r\n");
                            headerStBuilder.Append("\r\n");


                            writeContentState = WriteContentState.HttpBody;
                            //-----------------------------------------------------------------
                            //switch transfer encoding method of the body***
                            var headBuffer = Encoding.UTF8.GetBytes(headerStBuilder.ToString().ToCharArray());
                            byte[] dataToSend = new byte[headBuffer.Length + contentByteCount];
                            Buffer.BlockCopy(headBuffer, 0, dataToSend, 0, headBuffer.Length);
                            var pos = bodyMs.Position;
                            bodyMs.Position = 0;
                            bodyMs.Read(dataToSend, headBuffer.Length, contentByteCount);
                            //----------------------------------------------------
                            //copy data to send buffer
                            connSession.SetDataToSend(dataToSend, dataToSend.Length);
                            //---------------------------------------------------- 
                            ResetAll();
                            writeContentState = WriteContentState.SocketClientOK;
                            return;


                            //                               HTTP/1.1 101 WebSocket Protocol Handshake
                            //Date: Fri, 10 Feb 2012 17:38:18 GMT
                            //Connection: Upgrade
                            //Server: Kaazing Gateway
                            //Upgrade: WebSocket
                            //Access-Control-Allow-Origin: http://websocket.org
                            //Access-Control-Allow-Credentials: true
                            //Sec-WebSocket-Accept: rLHCkw/SKsO9GAH/ZSFhBATDKrU=
                            //Access-Control-Allow-Headers: content-type 
                        } 
                        headerStBuilder.Length = 0;
                        headerStBuilder.Append("HTTP/1.1 ");
                        HeaderAppendStatusCode(headerStBuilder, StatusCode);
                        HeaderAppendConnectionType(headerStBuilder, this.connSession.KeepAlive);

                        //TODO: review content encoding ***
                        headerStBuilder.Append("Content-Type: " + GetContentType(this.ContentType) + " ; charset=utf-8\r\n");
                        //-----------------------------------------------------------------

                        switch (this.TransferEncoding)
                        {
                            default:
                            case ResponseTransferEncoding.Identity:
                                {
                                    headerStBuilder.Append("Content-Length: " + contentByteCount + "\r\n");
                                    headerStBuilder.Append("\r\n");

                                    writeContentState = WriteContentState.HttpBody;
                                    //-----------------------------------------------------------------
                                    //switch transfer encoding method of the body***
                                    var headBuffer = Encoding.UTF8.GetBytes(headerStBuilder.ToString().ToCharArray());
                                    byte[] dataToSend = new byte[headBuffer.Length + contentByteCount];
                                    Buffer.BlockCopy(headBuffer, 0, dataToSend, 0, headBuffer.Length);
                                    var pos = bodyMs.Position;
                                    bodyMs.Position = 0;
                                    bodyMs.Read(dataToSend, headBuffer.Length, contentByteCount);
                                    //----------------------------------------------------
                                    //copy data to send buffer
                                    connSession.SetDataToSend(dataToSend, dataToSend.Length);
                                    //---------------------------------------------------- 
                                    ResetAll();
                                } break;
                            case ResponseTransferEncoding.Chunked:
                                {
                                    headerStBuilder.Append("Transfer-Encoding: " + GetTransferEncoing(TransferEncoding) + "\r\n");
                                    headerStBuilder.Append("\r\n");
                                    writeContentState = WriteContentState.HttpBody;

                                    //chunked transfer
                                    var headBuffer = Encoding.UTF8.GetBytes(headerStBuilder.ToString().ToCharArray());
                                    connSession.SetDataToSend(headBuffer, headBuffer.Length);
                                    WriteContentBodyInChunkMode();
                                    ResetAll();
                                } break;
                        }
                    } break;
                //==============================
                case WriteContentState.HttpBody:
                    {
                        //in chunked case, 
                        WriteContentBodyInChunkMode();
                        ResetAll();
                    } break;
                case WriteContentState.SocketClientOK:
                    {
                        ResetAll();
                    } break;
                default:
                    {
                    } break;
            }
        }
        void WriteContentBodyInChunkMode()
        {
            //---------------------------------------------------- 
            var pos = bodyMs.Position;
            bodyMs.Position = 0;
            byte[] bodyLengthInHex = Encoding.UTF8.GetBytes(contentByteCount.ToString("X"));
            int chuckedPrefixLength = bodyLengthInHex.Length;
            byte[] bodyBuffer = new byte[chuckedPrefixLength + contentByteCount + 4];
            int w = 0;
            Buffer.BlockCopy(bodyLengthInHex, 0, bodyBuffer, 0, chuckedPrefixLength);
            w += chuckedPrefixLength;
            bodyBuffer[w] = (byte)'\r';
            w++;
            bodyBuffer[w] = (byte)'\n';
            w++;
            bodyMs.Read(bodyBuffer, w, contentByteCount);
            w += contentByteCount;
            bodyBuffer[w] = (byte)'\r';
            w++;
            bodyBuffer[w] = (byte)'\n';
            w++;
            connSession.SetDataToSend(bodyBuffer, bodyBuffer.Length);
            //---------------------------------------------------- 

            //end body
            byte[] endChuckedBlock = new byte[] { (byte)'0', (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
            connSession.SetDataToSend(endChuckedBlock, endChuckedBlock.Length);
            //---------------------------------------------------- 
            ResetWritingBuffer();
        }

        internal bool NeedFlush
        {
            get;
            set;
        }
        public ResponseTransferEncoding TransferEncoding
        {
            get;
            set;
        }
        internal int StatusCode
        {
            get;
            set;
        }



        //-------------------------------------------------
        static string GetTransferEncoing(ResponseTransferEncoding te)
        {
            switch (te)
            {
                case ResponseTransferEncoding.Chunked:
                    return "chunked";
                case ResponseTransferEncoding.Compress:
                    return "compress";
                case ResponseTransferEncoding.Deflate:
                    return "deflate";
                case ResponseTransferEncoding.Gzip:
                    return "gzip";
                default:
                    return "";
            }
        }

        static string GetContentType(WebResponseContentType contentType)
        {
            //TODO: review here again
            switch (contentType)
            {
                case WebResponseContentType.ImageJpeg:
                    return "image/jpeg";
                case WebResponseContentType.ImagePng:
                    return "image/png";
                case WebResponseContentType.ApplicationOctetStream:
                    return "application/octet-stream";
                case WebResponseContentType.ApplicationJson:
                    return "application/json";
                case WebResponseContentType.TextXml:
                    return "text/xml";
                case WebResponseContentType.TextHtml:
                    return "text/html";
                case WebResponseContentType.TextJavascript:
                    return "text/javascript";

                case WebResponseContentType.TextPlain:
                    return "text/plain";
                default:
                    throw new NotSupportedException();
            }
        }

        static void HeaderAppendConnectionType(StringBuilder headerStBuilder, bool keepAlive)
        {
            if (keepAlive)
                headerStBuilder.Append("Connection: keep-alive\r\n");
            else
                headerStBuilder.Append("Connection: close\r\n");

        }
        static void HeaderAppendStatusCode(StringBuilder stBuilder, int statusCode)
        {
            switch (statusCode)
            {
                case 200:
                    stBuilder.Append("200 OK\r\n");
                    return;
                case 500:
                    stBuilder.Append("500 InternalServerError\r\n");
                    return;
                default:
                    //from 'Nowin' project
                    stBuilder.Append((byte)('0' + statusCode / 100));
                    stBuilder.Append((byte)('0' + statusCode / 10 % 10));
                    stBuilder.Append((byte)('0' + statusCode % 10));
                    stBuilder.Append("\r\n");
                    return;
            }
        }

        //---------
        public bool UpgradeToWebSocket
        {
            get;
            set;
        }
        public string ResponseSecWebSocket
        {
            get;
            set;
        }


    }

}