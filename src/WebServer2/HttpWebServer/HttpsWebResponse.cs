﻿//2015, MIT, EngineKit  
namespace SharpConnect.WebServers.Server2
{
    class HttpsWebResponse : SharpConnect.WebServers.HttpResponse
    {
        readonly HttpsContext context;
        internal HttpsWebResponse(HttpsContext context)
            : base(context)
        {
            this.context = context;

            StatusCode = 200; //init

            this.ContentTypeCharSet = WebServers.TextCharSet.Utf8;
        }
        public override bool KeepAlive => context.KeepAlive;
    }
}