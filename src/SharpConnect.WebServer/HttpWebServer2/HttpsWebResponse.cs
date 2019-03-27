//MIT, 2015-present, EngineKit 
namespace SharpConnect.WebServers
{
    class HttpsWebResponse : SharpConnect.WebServers.HttpResponse
    {
        readonly HttpsContext _context;
        internal HttpsWebResponse(HttpsContext context)
            : base(context)
        {
            _context = context;

            StatusCode = 200; //init

            this.ContentTypeCharSet = WebServers.TextCharSet.Utf8;
        }
        public override bool KeepAlive => _context.KeepAlive;
    }
}