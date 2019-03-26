//2015-2016, MIT, EngineKit
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using SharpConnect.Internal;

namespace SharpConnect.WebServers
{
     
    class Server1HttpResponse : HttpResponse
    {
        enum WriteContentState : byte
        {
            HttpHead,
            HttpBody,
        }

        readonly HttpContext context;

        //WriteContentState writeContentState;
        ////output stream
        //MemoryStream bodyMs;
        //int contentByteCount;
        //Dictionary<string, string> headers = new Dictionary<string, string>();
        //StringBuilder headerStBuilder = new StringBuilder();
        ISendIO sendIO;


        internal Server1HttpResponse(HttpContext context) : base(context)
        {
            this.context = context;
            //bodyMs = new MemoryStream();
            StatusCode = 200; //init
            this.sendIO = context;
            this.ContentTypeCharSet = WebServers.TextCharSet.Utf8;
        }
        public override bool KeepAlive => context.KeepAlive;


    }

}