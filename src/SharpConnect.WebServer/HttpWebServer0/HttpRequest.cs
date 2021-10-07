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
    public abstract class DataStream
    {
        public abstract int CurrentReadPos { get; set; }
        public abstract IntPtr GetUnManagedPtr();
        public abstract byte[] GetManagedBuffer();
        public abstract bool IsUnmanaged { get; }
        public abstract int GetLength();
        public DataStream Next { get; set; }
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
        void EnqueueSendingData(DataStream dataStream);
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

        public WebRequestParameter[] ReqParameters { get; internal set; }

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

        /// <summary>
        /// max byte count of body in mem stream
        /// </summary>
        public int InMemMaxUploadBodySize { get; protected set; }

        public string UploadTempFileName { get; protected set; }

        public byte[] GetBodyContentAsBuffer()
        {
            if (_contentByteCount > 0)
            {
                if (UploadTempFileName != null)
                {
                    //read all data from temp filename
                    //TODO: check if it is large file or not
                    //if it is a large file ***
                    //we should read it as file stream 

                    return null;
                }
                else
                {
                    //copy from mem-stream
                    var pos = _bodyMs.Position;
                    _bodyMs.Position = 0;
                    byte[] buffer = new byte[_contentByteCount];
                    _bodyMs.Read(buffer, 0, _contentByteCount);
                    _bodyMs.Position = pos;
                    return buffer;
                }
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

    public class LargetFileUploadPolicyResult
    {
        public bool Cancel { get; set; }
        public string TempUploadFilename { get; set; }
    }

    public delegate void LargeFileUploadPermissionReqHandler(HttpRequest req, LargetFileUploadPolicyResult result);

    class HttpRequestImpl : HttpRequest
    {
        readonly IHttpContext _context;
        LargeFileUploadPermissionReqHandler _largeFileUploadPermissionReqHandler;

        internal HttpRequestImpl(IHttpContext context)
            : base()
        {
            _context = context;
            _bodyMs = new MemoryStream();
            InMemMaxUploadBodySize = 1024 * 4;//default 4k
        }


        public void SetLargeUploadFilePolicyHandler(LargeFileUploadPermissionReqHandler largeFileUploadPermissionReqHandler)
        {
            _largeFileUploadPermissionReqHandler = largeFileUploadPermissionReqHandler;
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

        readonly object _bodyLock = new object();
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
                            }
                            else
                            {
                                _bodyMs.Write(buffer, s1, blockSize);
                                remaining -= blockSize;
                                s1 += blockSize;
                            }
                        }

                    }
                    else
                    {
                        _bodyMs.Write(buffer, start, count);
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
                        _contentByteCount = 0; //reset
                        _uploadCanceled = false;//reset
                        _readPos = ParseHttpRequestHeader();

                        if (ContentLength > InMemMaxUploadBodySize)
                        {
                            if (_largeFileUploadPermissionReqHandler != null)
                            {
                                //what to do with large file
                                LargetFileUploadPolicyResult permissionResult = new LargetFileUploadPolicyResult();
                                _largeFileUploadPermissionReqHandler(this, permissionResult);
                                if (!permissionResult.Cancel && permissionResult.TempUploadFilename != null)
                                {
                                    UploadTempFileName = permissionResult.TempUploadFilename;
                                }
                                else
                                {
                                    UploadTempFileName = null;
                                    _uploadCanceled = true;
                                }
                            }

                        }
                        else
                        {
                            UploadTempFileName = null; //reset
                        }

                        if (_parseState == HttpParsingState.Body)
                        {
                            ProcessHtmlPostBody();
                        }
                    }
                    break;
                case HttpParsingState.Body:
                    ProcessHtmlPostBody();
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
        FileStream _uploadTempFile = null;
        bool _uploadCanceled = false;

        void ProcessHtmlPostBody()
        {
            //parse body
            int transferedBytes = _context.RecvByteTransfer;
            int len1 = transferedBytes - _readPos;

            if (!_uploadCanceled && len1 > 0)
            {
                //TODO review here
                //write to file first, or write to mem ***
                //if we upload large content, we should write to file
                // 
                if (UploadTempFileName != null)
                {
                    //write data to file
                    if (_uploadTempFile == null)
                    {
                        _uploadTempFile = new FileStream(UploadTempFileName, FileMode.Create);
                    }

                    byte[] buff = new byte[len1]; //TODO: reuse the buffer
                    _context.RecvCopyTo(_readPos, buff, len1);
                    _uploadTempFile.Write(buff, 0, len1);
                    _uploadTempFile.Flush();//** ensure write to disk
                }
                else
                {
                    //write to mem
                    byte[] buff = new byte[len1]; //TODO: reuse the buffer
                    _context.RecvCopyTo(_readPos, buff, len1);
                    AddMsgBody(buff, 0, len1);
                }
            }

            _contentByteCount += len1; //***
            _readPos = 0;
            //read until end of buffer

            //System.Diagnostics.Debug.WriteLine("expect" + ContentLength + "tx:" + _contentByteCount + ",rem:" + (ContentLength - _contentByteCount));

            if (!IsMsgBodyComplete)
            {
                return;
            }

            //finish
            //close the file stream
            if (_uploadTempFile != null)
            {
                _uploadTempFile.Close();
                _uploadTempFile.Dispose();
                _uploadTempFile = null;
            }

            _parseState = HttpParsingState.Complete;
        }
    }

}