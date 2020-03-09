//MIT, 2015-present, EngineKit
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace SharpConnect.WebServers
{
    /// <summary>
    /// content type
    /// </summary>
    public enum WebResponseContentType : byte
    {
        TextHtml,
        TextPlain,
        TextXml,
        TextJavascript,
        TextCss,

        ImagePng,
        ImageJpeg,

        ApplicationOctetStream,
        ApplicationJson,
    }

    public enum TextCharSet : byte
    {
        Ascii,
        Utf8
    }

    public enum ResponseTransferEncoding : byte
    {
        Identity,//no encoding
        Chunked,
        Gzip,
        Compress,
        Deflate
    }

    public enum ContentEncoding : byte
    {
        Plain,//no encoding
        Gzip,
        Deflate,
    }

    public enum AllowCrossOrigin : byte
    {
        None,
        All,
        Some
    }

    public class CrossOriginPolicy
    {
        public CrossOriginPolicy(AllowCrossOrigin allowKind, string originList)
        {
            this.AllowCrossOriginKind = allowKind;
            this.AllowOriginList = originList;

#if DEBUG
            if (allowKind == AllowCrossOrigin.Some && originList == null)
            {
                throw new NotSupportedException();
            }
#endif
        }
        public string AllowOriginList { get; private set; }
        public AllowCrossOrigin AllowCrossOriginKind { get; private set; }
        public string AllowMethods { get; set; }
        public string AllowHeaders { get; set; }
        internal void WriteHeader(StringBuilder stbuilder)
        {
            switch (AllowCrossOriginKind)
            {
                default:
                case AllowCrossOrigin.None:
                    return;
                case AllowCrossOrigin.All:
                    stbuilder.Append("Access-Control-Allow-Origin: *\r\n");
                    break;
                case AllowCrossOrigin.Some:
                    stbuilder.Append("Access-Control-Allow-Origin: ");
                    stbuilder.Append(AllowOriginList);
                    stbuilder.Append("\r\n");
                    break;
            }
            if (AllowMethods != null)
            {
                stbuilder.Append("Access-Control-Allow-Methods: ");
                stbuilder.Append(AllowMethods);
                stbuilder.Append("\r\n");
            }

            if (AllowHeaders != null)
            {
                stbuilder.Append("Access-Control-Allow-Headers: ");
                stbuilder.Append(AllowHeaders);
                stbuilder.Append("\r\n");
            }
        }
    }


    public abstract class HttpResponse : IDisposable
    {
        enum WriteContentState : byte
        {
            HttpHead,
            HttpBody,
        }


        WriteContentState _writeContentState;
        //output stream
        MemoryStream _bodyMs;
        int _contentByteCount;
        Dictionary<string, string> _headers = new Dictionary<string, string>();
        StringBuilder _headerStBuilder = new StringBuilder();
        ISendIO _sendIO;

        internal HttpResponse(ISendIO sendIO)
        {
            _sendIO = sendIO;
            _bodyMs = new MemoryStream();
        }
        public virtual bool KeepAlive => false;
        public CrossOriginPolicy AllowCrossOriginPolicy { get; set; }
        internal void ResetAll()
        {
            _actualEnd = false;
            _headerStBuilder.Length = 0;
            StatusCode = 200;

            _isSend = false;
            TransferEncoding = ResponseTransferEncoding.Identity;
            _writeContentState = WriteContentState.HttpHead;
            ContentType = WebResponseContentType.TextPlain;//reset content type
            ContentEncoding = ContentEncoding.Plain;
            this.ContentTypeCharSet = TextCharSet.Utf8;
            AllowCrossOriginPolicy = null;
            _headers.Clear();
            ResetWritingBuffer();
        }
        void ResetWritingBuffer()
        {
            _bodyMs.Position = 0;
            _contentByteCount = 0;
        }
        public void Dispose()
        {
            if (_bodyMs != null)
            {
                _bodyMs.Dispose();
                _bodyMs = null;
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
            _headers[key] = value;
        }
        public WebResponseContentType ContentType { get; set; }
        public ContentEncoding ContentEncoding { get; set; }
        public TextCharSet ContentTypeCharSet { get; set; }

        /// <summary>
        /// write to output
        /// </summary>
        /// <param name="str"></param>
        public void Write(string str)
        {
            //write to output stream 
            byte[] bytes = Encoding.UTF8.GetBytes(str.ToCharArray());
            //write to stream
            _bodyMs.Write(bytes, 0, bytes.Length);
            _contentByteCount += bytes.Length;
        }
        /// <summary>
        /// write to output
        /// </summary>
        /// <param name="str"></param>
        public void Write(byte[] rawBuffer)
        {
            _bodyMs.Write(rawBuffer, 0, rawBuffer.Length);
            _contentByteCount += rawBuffer.Length;
        }

        public bool _actualEnd;

        public void End(string str)
        {
            //Write and End
            Write(str);
            End();
        }
        public void End(byte[] data)
        {
            _bodyMs.Write(data, 0, data.Length);
            _contentByteCount += data.Length;
            End();
        }

        public void ActualEnd()
        {

            switch (_writeContentState)
            {
                //generate head 
                case WriteContentState.HttpHead:
                    {
                        _headerStBuilder.Length = 0;
                        _headerStBuilder.Append("HTTP/1.1 ");
                        HeaderAppendStatusCode(_headerStBuilder, StatusCode);
                        HeaderAppendConnectionType(_headerStBuilder, this.KeepAlive);
                        //--------------------------------------------------------------------------------------------------------
                        _headerStBuilder.Append("Content-Type: " + GetContentType(this.ContentType));
                        switch (ContentTypeCharSet)
                        {
                            case TextCharSet.Utf8:
                                _headerStBuilder.Append(" ; charset=utf-8\r\n");
                                break;
                            case TextCharSet.Ascii:
                                _headerStBuilder.Append("\r\n");
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                        if (_headers.Count > 0)
                        {
                            foreach (var kv in _headers)
                            {
                                _headerStBuilder.Append(kv.Key);
                                _headerStBuilder.Append(": ");
                                _headerStBuilder.Append(kv.Value);
                                _headerStBuilder.Append("\r\n");
                            }
                        }

                        //--------------------------------------------------------------------------------------------------------
                        switch (ContentEncoding)
                        {
                            case ContentEncoding.Plain:
                                //nothing
                                break;
                            case ContentEncoding.Gzip:
                                _headerStBuilder.Append("Content-Encoding: gzip\r\n");
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                        //--------------------------------------------------------------------------------------------------------
                        //Access-Control-Allow-Origin
                        if (AllowCrossOriginPolicy != null)
                        {
                            AllowCrossOriginPolicy.WriteHeader(_headerStBuilder);
                        }
                        //--------------------------------------------------------------------------------------------------------
                        switch (this.TransferEncoding)
                        {
                            default:
                            case ResponseTransferEncoding.Identity:
                                {
                                    _headerStBuilder.Append("Content-Length: ");
                                    _headerStBuilder.Append(_contentByteCount);
                                    _headerStBuilder.Append("\r\n");
                                    //-----------------------------------------------------------------                                    
                                    _headerStBuilder.Append("\r\n");//end header part                                     
                                    _writeContentState = WriteContentState.HttpBody;
                                    //-----------------------------------------------------------------
                                    //switch transfer encoding method of the body***
                                    byte[] headBuffer = Encoding.UTF8.GetBytes(_headerStBuilder.ToString().ToCharArray());
                                    byte[] dataToSend = new byte[headBuffer.Length + _contentByteCount];
                                    Buffer.BlockCopy(headBuffer, 0, dataToSend, 0, headBuffer.Length);
                                    var pos = _bodyMs.Position;
                                    _bodyMs.Position = 0;
                                    _bodyMs.Read(dataToSend, headBuffer.Length, _contentByteCount);
                                    //----------------------------------------------------
                                    //copy data to send buffer

                                    _sendIO.EnqueueSendingData(dataToSend, dataToSend.Length);
                                    //---------------------------------------------------- 
                                    ResetAll();
                                }
                                break;
                            case ResponseTransferEncoding.Chunked:
                                {
                                    _headerStBuilder.Append("Transfer-Encoding: " + GetTransferEncoding(TransferEncoding) + "\r\n");
                                    _headerStBuilder.Append("\r\n");
                                    _writeContentState = WriteContentState.HttpBody;

                                    //chunked transfer
                                    byte[] headBuffer = Encoding.UTF8.GetBytes(_headerStBuilder.ToString().ToCharArray());

                                    _sendIO.EnqueueSendingData(headBuffer, headBuffer.Length);
                                    WriteContentBodyInChunkMode();
                                    ResetAll();
                                }
                                break;
                        }
                    }
                    break;
                //==============================
                case WriteContentState.HttpBody:
                    {
                        //in chunked case, 
                        WriteContentBodyInChunkMode();
                        ResetAll();
                    }
                    break;
                default:
                    {
                        throw new NotSupportedException();
                    }
            }

            //-----------------------
            //send  
            StartSend();
        }
        public void End()
        {
            _actualEnd = true;
        }
        bool _isSend = false;
        void StartSend()
        {
            if (_isSend)
            {
                return;
            }
            _isSend = true;

            _sendIO.SendIOStartSend();
        }
        void WriteContentBodyInChunkMode()
        {
            //---------------------------------------------------- 
            var pos = _bodyMs.Position;
            _bodyMs.Position = 0;
            byte[] bodyLengthInHex = Encoding.UTF8.GetBytes(_contentByteCount.ToString("X"));
            int chuckedPrefixLength = bodyLengthInHex.Length;
            byte[] bodyBuffer = new byte[chuckedPrefixLength + _contentByteCount + 4];
            int w = 0;
            Buffer.BlockCopy(bodyLengthInHex, 0, bodyBuffer, 0, chuckedPrefixLength);
            w += chuckedPrefixLength;
            bodyBuffer[w] = (byte)'\r';
            w++;
            bodyBuffer[w] = (byte)'\n';
            w++;
            _bodyMs.Read(bodyBuffer, w, _contentByteCount);
            w += _contentByteCount;
            bodyBuffer[w] = (byte)'\r';
            w++;
            bodyBuffer[w] = (byte)'\n';
            w++;

            _sendIO.EnqueueSendingData(bodyBuffer, bodyBuffer.Length);
            //---------------------------------------------------- 

            //end body
            byte[] endChuckedBlock = new byte[] { (byte)'0', (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
            _sendIO.EnqueueSendingData(endChuckedBlock, endChuckedBlock.Length);
            //---------------------------------------------------- 
            ResetWritingBuffer();
        }
        public ResponseTransferEncoding TransferEncoding { get; set; }
        internal int StatusCode { get; set; }

        //-------------------------------------------------
        static string GetTransferEncoding(ResponseTransferEncoding te)
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
                case WebResponseContentType.TextCss:
                    return "text/css";
                case WebResponseContentType.TextPlain:
                    return "text/plain";
                default:
                    throw new NotSupportedException();
            }
        }

        static void HeaderAppendConnectionType(StringBuilder headerStBuilder, bool keepAlive)
        {
            //always close connection
            //headerStBuilder.Append("Connection: close\r\n");
            if (keepAlive)
            {
                headerStBuilder.Append("Connection: keep-alive\r\n");
            }
            else
            {
                headerStBuilder.Append("Connection: close\r\n");
            }
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
    }

    class HttpResponseImpl : HttpResponse
    {

        readonly IHttpContext _context;

        internal HttpResponseImpl(IHttpContext context) : base(context)
        {
            _context = context;
            //bodyMs = new MemoryStream();
            StatusCode = 200; //init
            _context = context;
            this.ContentTypeCharSet = WebServers.TextCharSet.Utf8;
        }
        public override bool KeepAlive => _context.KeepAlive;
    }
}