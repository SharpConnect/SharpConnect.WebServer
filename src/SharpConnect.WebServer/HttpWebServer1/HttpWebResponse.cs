//MIT, 2015-present, EngineKit


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
            _context = context;
            //bodyMs = new MemoryStream();
            StatusCode = 200; //init
            _context = context;
            this.ContentTypeCharSet = WebServers.TextCharSet.Utf8;
        }
        public override bool KeepAlive => _context.KeepAlive;
    }

}