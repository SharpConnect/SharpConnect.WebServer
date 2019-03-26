//2015, MIT, EngineKit 
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

    interface ISendIO
    {
        void EnqueueSendingData(byte[] buffer, int len);
        void SendIOStartSend();
        int QueueCount { get; }

    }
    public class WebRequestParameter
    {
        string name;
        string value;

        public WebRequestParameter(string name, string value)
        {
            this.name = name;
            this.value = value;
        }
        public string ParameterName
        {
            get
            {
                return this.name;
            }
        }
        public string Value
        {
            get
            {
                return value;
            }
        }

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
            Url = null;
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
            string found;
            _headerKeyValues.TryGetValue(key, out found);
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
        public string Url { get; set; }
        public HttpMethod HttpMethod
        {
            get;
            internal set;
        }

        protected int ContentLength => _targetContentLength;

    }



}