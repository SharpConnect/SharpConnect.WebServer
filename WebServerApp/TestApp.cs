//2015, MIT, EngineKit
using System;
using System.Net;
using System.Text;
using SharpConnect.WebServer;
namespace SharpConnect
{

    class TestApp
    {
        public void HandleRequest(HttpRequest req, HttpResponse resp)
        {
            switch (req.Url)
            {
                case "/":
                    {
                        resp.TransferEncoding = ResponseTransferEncoding.Chunked;
                        resp.Write("hello!");
                    }
                    break;
                case "/version":
                    {
                        resp.Write("1.0");
                    }
                    break;
                default:
                    {
                        resp.Write("");
                    }
                    break;
            }
        }

    }

}