//2015, MIT, EngineKit

using System;
using System.Net;
using System.Text;
using System.IO;

using SharpConnect.WebServers;
namespace SharpConnect
{
    class TestApp
    {
        //test cross origin policy 1
        static CrossOriginPolicy crossOriginPolicy = new CrossOriginPolicy(AllowCrossOrigin.All, "*");
        static TestApp()
        {
            //eg.
            //stBuilder.Append("Access-Control-Allow-Methods: GET, POST\r\n");
            //stBuilder.Append("Access-Control-Allow-Headers: Content-Type\r\n");
            crossOriginPolicy.AllowHeaders = "Content-Type";
            crossOriginPolicy.AllowMethods = "GET, POST";
        }

        const string html = @"<html>
                <head>
                <script> 
                         
                        var wsUri=get_wsurl(); 
                        var websocket= null;
                        (function init(){
	  
		                        //command queue 
		                        websocket = new WebSocket(wsUri);
		                        websocket.onopen = function(evt) { 
			                        console.log('open');
			                        websocket.send('client: Hello!');
		                        };
		                        websocket.onclose = function(evt) { 
			                        console.log('close');
		                        };
		                        websocket.onmessage = function(evt) {  
			                        console.log(evt.data);
		                        };
		                        websocket.onerror = function(evt) {  
		                        };		
                         })();
                        function send_data(data){
	                            websocket.send(data);
                        } 
                        function get_wsurl(){
                               
                                if(window.location.protocol==""https:""){
                                    return  ""wss://localhost:8000"";
                                }else{
                                    return  ""ws://localhost:8080"";
                                }
                        }
                </script>                
                </head>
                <body>
                        hello-websocket
	                    <input type=""button"" id=""mytxt"" onclick=""send_data('hello')""></input>	
                </body>    
        </html>";
        public void HandleRequest(HttpRequest req, HttpResponse resp)
        {

            //string rootFolder = @"C:\apache2\htdocs\";
            //string absFile = rootFolder + "\\" + req.Url;  
            //if (File.Exists(absFile))
            //{
            //    byte[] buffer = File.ReadAllBytes(absFile);
            //    resp.AllowCrossOriginPolicy = crossOriginPolicy;
            //    switch (Path.GetExtension(absFile))
            //    {
            //        case ".jpg":
            //            resp.ContentType = WebResponseContentType.ImageJpeg;
            //            break;
            //        case ".png":
            //            resp.ContentType = WebResponseContentType.ImagePng;
            //            break;
            //        case ".php":
            //        case ".html":
            //            resp.ContentType = WebResponseContentType.TextHtml;
            //            break;
            //        case ".js":
            //            resp.ContentType = WebResponseContentType.TextJavascript;
            //            break;
            //        case ".css":
            //            resp.ContentType = WebResponseContentType.TextCss;
            //            break;
            //    }
            //    resp.End(buffer);
            //}
            //else
            //{
            //    resp.End("something wrong");
            //}


            switch (req.Url)
            {
                case "/":
                    {
                        resp.TransferEncoding = ResponseTransferEncoding.Chunked;
                        resp.End("hello!");
                    }
                    break;
                case "/websocket":
                    {
                        resp.ContentType = WebResponseContentType.TextHtml;
                        resp.End(html);
                    }
                    break;
                case "/version":
                    {
                        resp.End("1.0");
                    }
                    break;
                case "/cross":
                    {
                        resp.AllowCrossOriginPolicy = crossOriginPolicy;
                        resp.End("ok");
                    }
                    break;
                default:
                    {
                        resp.End("");
                    }
                    break;
            }
        }
        int count = 0;
        public void HandleWebSocket(WebSocketRequest req, WebSocketResponse resp)
        {
            //server-side handle websocket request***
            string clientMsg = req.ReadAsString();
            if (clientMsg.StartsWith("LOOPBACK"))
            {
                resp.Write("from SERVER " + clientMsg);
            }
            else
            {
                resp.Write("server:" + (count++));
            }
        }
    }
}