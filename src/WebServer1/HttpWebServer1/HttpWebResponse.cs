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
        readonly HttpContext _context;

        internal HttpWebResponse(HttpContext context) : base(context)
        {
            this._context = context;
            //bodyMs = new MemoryStream();
            StatusCode = 200; //init
            this._context = context;
            this.ContentTypeCharSet = WebServers.TextCharSet.Utf8;
        }
        public override bool KeepAlive => _context.KeepAlive;
    }

}