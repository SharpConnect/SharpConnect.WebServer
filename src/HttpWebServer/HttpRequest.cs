//2015, MIT, EngineKit 
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;


namespace SharpConnect.WebServer
{

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


    public class HttpRequest : System.IDisposable
    {
        readonly HttpConnectionSession connSession;
        Dictionary<string, string> headerKeyValues = new Dictionary<string, string>();
        MemoryStream bodyMs;
        int contentByteCount;
        int targetContentLength;

        internal HttpRequest(HttpConnectionSession connSession)
        {
            this.connSession = connSession;
            bodyMs = new MemoryStream();
        }
        public void Dispose()
        {
            if (bodyMs != null)
            {
                bodyMs.Dispose();
                bodyMs = null;
            }
        }
        public WebRequestParameter[] ReqParameters
        {
            get;
            internal set;
        }
        internal void Reset()
        {
            headerKeyValues.Clear();
            Url = null;
            ReqParameters = null;
            HttpMethod = HttpMethod.Get;

            contentByteCount = 0;
            bodyMs.Position = 0;
            targetContentLength = 0;
        }
        internal void AddHeaderInfo(string key, string value)
        {
            //replace if exist
            headerKeyValues[key] = value;
            //translate some key eg. content-length,encoding
            switch (key)
            {
                case "Content-Length":
                    {
                        int.TryParse(value, out this.targetContentLength);
                    }
                    break;
            }
        }
        internal int ContentLength
        {
            get { return targetContentLength; }
        }
        internal int CollectingContentByteCount
        {
            get { return contentByteCount; }
        }
        internal bool IsMsgBodyComplete
        {
            get { return contentByteCount >= targetContentLength; }
        }
        internal void AddMsgBody(byte[] buffer, int start, int count)
        {
            bodyMs.Write(buffer, start, count);
            contentByteCount += count;
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
            internal set;
        }
        public HttpMethod HttpMethod
        {
            get;
            internal set;
        }


    }

}