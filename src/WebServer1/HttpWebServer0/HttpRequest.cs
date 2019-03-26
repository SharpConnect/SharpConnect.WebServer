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
        protected Dictionary<string, string> headerKeyValues = new Dictionary<string, string>();
        protected MemoryStream bodyMs;
        protected byte[] tmpReadBuffer = new byte[512];

        protected int contentByteCount;
        protected int targetContentLength;
        internal HttpRequest()
        {
            bodyMs = new MemoryStream();
        }
        public void Dispose()
        {

        }
        protected virtual void OnDispose()
        {
            if (bodyMs != null)
            {
                bodyMs.Dispose();
                bodyMs = null;
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

            headerKeyValues.Clear();
            Url = null;
            ReqParameters = null;
            HttpMethod = HttpMethod.Get;

            contentByteCount = 0;
            bodyMs.Position = 0;
            targetContentLength = 0;
        }

        public WebRequestParameter[] ReqParameters
        {
            get;
            internal set;
        }

        public string GetHeaderKey(string key)
        {
            string found;
            headerKeyValues.TryGetValue(key, out found);
            return found;
        }
        public string GetBodyContentAsString()
        {
            if (contentByteCount > 0)
            {
                var pos = bodyMs.Position;
                bodyMs.Position = 0;
                byte[] buffer = new byte[contentByteCount];
                bodyMs.Read(buffer, 0, contentByteCount);
                bodyMs.Position = pos;
                return Encoding.UTF8.GetString(buffer);
            }
            else
            {
                return "";
            }
        }
        public string Url
        {
            get;
            set;
        }

        public HttpMethod HttpMethod
        {
            get;
            internal set;
        }
        protected int ContentLength
        {
            get { return targetContentLength; }
        }
    }



}