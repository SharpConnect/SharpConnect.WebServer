//2015, MIT, EngineKit 
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;


namespace SharpConnect.WebServers.Server2
{
    class Server2HttpResponse : SharpConnect.WebServers.HttpResponse
    {
        readonly HttpContext context;
        internal Server2HttpResponse(HttpContext context)
            : base(context)
        {
            this.context = context;

            StatusCode = 200; //init

            this.ContentTypeCharSet = WebServers.TextCharSet.Utf8;
        }
        public override bool KeepAlive => context.KeepAlive;
    }
}