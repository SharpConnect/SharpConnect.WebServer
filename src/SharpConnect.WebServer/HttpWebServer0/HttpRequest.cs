//MIT, 2015-present, EngineKit
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;


namespace SharpConnect.WebServers
{
    enum ProcessReceiveBufferResult
    {
        Error,
        NeedMore,
        Complete
    }

    interface IHttpContext : ISendIO
    {
        bool KeepAlive { get; set; }
        int RecvByteTransfer { get; }
        byte ReadByte(int pos);
        void RecvCopyTo(int readpos, byte[] dstBuffer, int copyLen);
    }
    interface ISendIO
    {
        void EnqueueSendingData(byte[] buffer, int len);
        void SendIOStartSend();
    }
    public class WebRequestParameter
    {
        string _name;
        string _value;
        public WebRequestParameter(string name, string value)
        {
            _name = name;
            _value = value;
        }
        public string ParameterName => _name;
        public string Value => _value;
    }

    public enum HttpMethod
    {
        Get,
        Post
    }

    enum HttpParsingState
    {
        Head,
        Body,
        Complete
    }

    public abstract class HttpRequest : IDisposable
    {
        protected Dictionary<string, string> _headerKeyValues = new Dictionary<string, string>();
        protected MemoryStream _bodyMs;
        protected byte[] _tmpReadBuffer = new byte[512];

        protected int _contentByteCount;
        protected int _targetContentLength;
        internal HttpRequest()
        {
            _bodyMs = new MemoryStream();
        }
        public void Dispose()
        {
            OnDispose();
        }
        protected virtual void OnDispose()
        {
            if (_bodyMs != null)
            {
                _bodyMs.Dispose();
                _bodyMs = null;
            }
        }
        public string GetReqParameterValue(string key)
        {
            WebRequestParameter[] reqs = ReqParameters;
            if (reqs != null)
            {
                int j = reqs.Length;
                for (int i = 0; i < j; ++i)
                {
                    if (reqs[i].ParameterName == key)
                    {
                        return reqs[i].Value;
                    }
                }
            }
            return "";

        }
        internal virtual void Reset()
        {

            _headerKeyValues.Clear();
            Path = null;
            ReqParameters = null;
            HttpMethod = HttpMethod.Get;

            _contentByteCount = 0;
            _bodyMs.Position = 0;
            _targetContentLength = 0;
        }

        public WebRequestParameter[] ReqParameters
        {
            get;
            internal set;
        }

        public string GetHeaderKey(string key)
        {
            _headerKeyValues.TryGetValue(key, out string found);
            return found;
        }
        public string GetBodyContentAsString()
        {
            if (_contentByteCount > 0)
            {
                var pos = _bodyMs.Position;
                _bodyMs.Position = 0;
                byte[] buffer = new byte[_contentByteCount];
                _bodyMs.Read(buffer, 0, _contentByteCount);
                _bodyMs.Position = pos;
                return Encoding.UTF8.GetString(buffer);
            }
            else
            {
                return "";
            }
        }

        static readonly byte[] s_empty = new byte[0];
        public byte[] GetBodyContentAsBuffer()
        {
            if (_contentByteCount > 0)
            {
                var pos = _bodyMs.Position;
                _bodyMs.Position = 0;
                byte[] buffer = new byte[_contentByteCount];
                _bodyMs.Read(buffer, 0, _contentByteCount);
                _bodyMs.Position = pos;
                return buffer;
            }
            else
            {
                return s_empty;
            }
        }
        public string Path { get; set; }
        public HttpMethod HttpMethod
        {
            get;
            internal set;
        }

        protected int ContentLength => _targetContentLength;

    }

    class HttpRequestImpl : HttpRequest
    {
        readonly IHttpContext _context;
        internal HttpRequestImpl(IHttpContext context)
            : base()
        {
            _context = context;
            _bodyMs = new MemoryStream();
        }


        //===================
        //parsing 
        HttpParsingState _parseState;
        internal override void Reset()
        {
            _parseState = HttpParsingState.Head;
            base.Reset();//**

        }
        bool IsMsgBodyComplete => _contentByteCount >= _targetContentLength;


        object _bodyLock = new object();
        void AddMsgBody(byte[] buffer, int start, int count)
        {
            try
            {
                lock (_bodyLock)
                {
                    if (count > 2048)
                    {
                        int s1 = start;
                        int blockSize = 2048;
                        int remaining = count;
                        while (remaining > 0)
                        {
                            if (remaining < blockSize)
                            {
                                _bodyMs.Write(buffer, s1, remaining);
                                remaining = 0;
                                _contentByteCount += remaining;
                            }
                            else
                            {
                                _bodyMs.Write(buffer, s1, blockSize);
                                remaining -= blockSize;
                                _contentByteCount += blockSize;
                                s1 += blockSize;
                            }
                        }

                    }
                    else
                    {
                        _bodyMs.Write(buffer, start, count);
                        _contentByteCount += count;
                    }
                }

            }
            catch (Exception ex)
            {

            }

        }
        void AddHeaderInfo(string key, string value)
        {
            //replace if exist
            _headerKeyValues[key] = value;
            //translate some key eg. content-length,encoding
            switch (key)
            {
                case "Content-Length":
                    {
                        int.TryParse(value, out _targetContentLength);
                    }
                    break;
                case "Connection":
                    {
                        _context.KeepAlive = (value.ToLower().Trim() == "keep-alive");
                    }
                    break;
            }
        }
        /// <summary>
        /// add and parse data
        /// </summary>
        /// <param name="buffer"></param>
        internal ProcessReceiveBufferResult LoadData()
        {
            switch (_parseState)
            {
                case HttpParsingState.Head:
                    {
                        //find html header 
                         
                        //check if complete or not
                        _readPos = ParseHttpRequestHeader();
                        if (_parseState == HttpParsingState.Body)
                        {
                            ProcessHtmlPostBody(_readPos);
                        }
                    }
                    break;
                case HttpParsingState.Body:
                    ProcessHtmlPostBody(_readPos);
                    break;
                case HttpParsingState.Complete:
                    break;
                default:
                    throw new NotSupportedException();
            }

            if (_parseState == HttpParsingState.Complete)
            {
                return ProcessReceiveBufferResult.Complete;
            }
            else
            {
                return ProcessReceiveBufferResult.NeedMore;
            }

        }

        void AddReqHeader(string line)
        {
            if (Path == null)
            {
                //check if GET or POST
                bool foundHttpMethod = false;
                HttpMethod httpMethod = HttpMethod.Get;
                if (line.StartsWith("GET"))
                {
                    foundHttpMethod = true;
                }
                else if (line.StartsWith("POST"))
                {
                    foundHttpMethod = true;
                    httpMethod = HttpMethod.Post;
                }

                //--------------------------------------------------------------
                if (foundHttpMethod)
                {

                    //parse req url
                    string[] splitedLines = line.Split(' ');
                    if (splitedLines.Length > 1)
                    {

                        string getContent = splitedLines[1];
                        int qpos = getContent.IndexOf('?');
                        if (qpos > -1)
                        {
                            Path = getContent.Substring(0, qpos);
                            string[] paramsParts = getContent.Substring(qpos + 1).Split('&');
                            int paramLength = paramsParts.Length;
                            var reqParams = new WebRequestParameter[paramLength];
                            for (int p = 0; p < paramLength; ++p)
                            {
                                string p_piece = paramsParts[p];
                                int eq_pos = p_piece.IndexOf('=');
                                if (eq_pos > -1)
                                {
                                    reqParams[p] = new WebRequestParameter(p_piece.Substring(0, eq_pos),
                                        p_piece.Substring(eq_pos + 1));
                                }
                                else
                                {
                                    reqParams[p] = new WebRequestParameter(p_piece, "");
                                }
                            }
                            ReqParameters = reqParams;
                        }
                        else
                        {
                            Path = getContent;
                        }
                    }
                    HttpMethod = httpMethod;
                    return;
                }
            }

            //--------------------------------------------------------------
            //sep key-value
            int pos = line.IndexOf(':');
            if (pos > -1)
            {
                string key = line.Substring(0, pos);
                string value = line.Substring(pos + 1);
                AddHeaderInfo(key.Trim(), value.Trim());
            }
            else
            {
            }
        }

        int ParseHttpRequestHeader()
        {
            //start from pos0
            int readpos = 0;
            int lim = _context.RecvByteTransfer - 1;



            int i = 0;
            for (; i <= lim; ++i)
            {
                //just read 
                if (_context.ReadByte(i) == '\r' &&
                    _context.ReadByte(i + 1) == '\n')
                {
                    //each line
                    //translate
                    if (i - readpos < 512)
                    {
                        //copy     
                        _context.RecvCopyTo(readpos, _tmpReadBuffer, i - readpos);
                        //translate
                        string line = Encoding.UTF8.GetString(_tmpReadBuffer, 0, i - readpos);
                        readpos = i + 2;
                        i++; //skip \n            
                             //translate header ***
                        if (line == "")
                        {
                            //complete http header                           
                            _parseState = HttpParsingState.Body;
                            return readpos;
                        }
                        else
                        {
                            //parse header line
                            AddReqHeader(line);
                        }
                    }
                    else
                    {
                        //just skip?
                        //skip too long line
                        readpos = i + 2;
                        i++; //skip \n 
                    }
                }
            }
            return readpos;
        }

        int _readPos;
        void ProcessHtmlPostBody(int readpos)
        {
            //parse body

            int transferedBytes = _context.RecvByteTransfer;
            int remaining = transferedBytes - readpos;
            if (!IsMsgBodyComplete)
            {   
                int wantBytes = ContentLength - _contentByteCount;
                if (wantBytes <= remaining)
                {
                    //complete here 
                    byte[] buff = new byte[wantBytes];
                    _context.RecvCopyTo(readpos, buff, wantBytes);
                    //add to req  
                    AddMsgBody(buff, 0, wantBytes);
                     
                    //complete 
                    _parseState = HttpParsingState.Complete;
                    return;
                }
                else
                {
                    //continue read             
                    if (remaining > 0)
                    {
                        byte[] buff = new byte[remaining];
                        _context.RecvCopyTo(readpos, buff, remaining);
                        //add to req  
                        AddMsgBody(buff, 0, remaining);
                    }
                    return;
                }
            }
            _parseState = HttpParsingState.Complete;
        }
    }

}