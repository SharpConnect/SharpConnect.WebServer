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

        //WriteContentState writeContentState;
        ////output stream
        //MemoryStream bodyMs;
        //int contentByteCount;
        //Dictionary<string, string> headers = new Dictionary<string, string>();
        //StringBuilder headerStBuilder = new StringBuilder();
        ISendIO sendIO;


        internal HttpWebResponse(HttpContext context) : base(context)
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