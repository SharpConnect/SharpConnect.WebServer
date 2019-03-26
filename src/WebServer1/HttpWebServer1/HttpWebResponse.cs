//2015-2016, MIT, EngineKit


namespace SharpConnect.WebServers
{

    class HttpWebResponse : HttpResponse
    {
        enum WriteContentState : byte
        {
            HttpHead,
            HttpBody,
        } 
        readonly HttpContext context;  

        internal HttpWebResponse(HttpContext context) : base(context)
        {
            this.context = context;
            //bodyMs = new MemoryStream();
            StatusCode = 200; //init
            this.context = context;
            this.ContentTypeCharSet = WebServers.TextCharSet.Utf8;
        }
        public override bool KeepAlive => context.KeepAlive;


    }

}