//2015, MIT, EngineKit
using System;
using System.Net;
using System.Text;
using SharpConnect.WebServers;
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
                        resp.End("hello!");
                    }
                    break;
                case "/version":
                    {
                        resp.End("1.0");
                    }
                    break;
                default:
                    {
                        resp.End("");
                    }
                    break;
            }
        }

    }

}