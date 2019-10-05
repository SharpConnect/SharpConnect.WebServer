//MIT, 2015-present, EngineKit

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
                        var send_count=0;
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
                                 send_count++;
                                //console.log('send_count='+ send_count);
	                            websocket.send(data +' '+ send_count);
                        } 
                        function get_wsurl(){
                               
                                if(window.location.protocol==""https:""){
                                    return   ""wss://""+ window.location.hostname +"":8080"";
                                }else{
                                    return   ""ws://""+ window.location.hostname +"":8080"";
                                }
                        } 
                       
                </script>                
                </head>
                <body>
                        hello-websocket
	                    <input type=""button"" id=""mytxt"" onclick=""send_data('hello')""></input>	
                        <div>AAA</div>
                </body></html>
    
       ";

        string html2 = null;

        public void HandleRequest(HttpRequest req, HttpResponse resp)
        {
            switch (req.Path)
            {
                case "/":
                    {
                        resp.TransferEncoding = ResponseTransferEncoding.Chunked;
                        resp.End("hello!");
                    }
                    break;
                case "/long_html":
                    {

                        if (html2 == null)
                        {
                            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
                            stbuilder.AppendLine("<html><head></head><body>");
                            for (int i = 0; i < 1000; ++i)
                            {
                                stbuilder.AppendLine("<div>" + i + "</div>");
                            }
                            stbuilder.AppendLine("<div>ZZZ</div>");
                            stbuilder.AppendLine("</body></html>");
                            html2 = stbuilder.ToString();
                        }

                        resp.ContentType = WebResponseContentType.TextHtml;
                        resp.End(html2);
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

            if (req.OpCode == Opcode.Text)
            {
                string clientMsg = req.ReadAsString();

                if (clientMsg == null)
                {
                    resp.Write("");
                    return;
                }

                string serverMsg = null;
                if (clientMsg.StartsWith("LOOPBACK"))
                {
                    serverMsg = "from SERVER " + clientMsg;
                }
                else
                {
                    serverMsg = "server:" + (count++);
                }

                resp.Write(serverMsg);
#if DEBUG
                System.Diagnostics.Debug.WriteLine(serverMsg);
#endif
            }
            else if (req.OpCode == Opcode.Binary)
            {
                //this is binary data
                byte[] binaryData = req.ReadAsBinary();
#if DEBUG
                count++;
                string serverMsg = count + " binary_len" + binaryData.Length;
                System.Diagnostics.Debug.WriteLine(serverMsg);
                resp.Write(serverMsg);
#endif
            }

        }
    }
}