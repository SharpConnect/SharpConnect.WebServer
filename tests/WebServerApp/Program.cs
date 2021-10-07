//MIT, 2015-present, EngineKit
using System;
using System.Collections.Generic;


namespace SharpConnect
{
    static class Program
    {
        static void Main(string[] args)
        {         
            Main_Http();
            //Main_Https();
        }
        
        static void HandleWebReq(WebServers.HttpRequest req, WebServers.HttpResponse resp)
        {
            resp.End("OK");
        }


        static List<SharpConnect.WebServers.WebSocketContext> s_contextList = new List<WebServers.WebSocketContext>();

        static void Main_Http()
        {
            Console.WriteLine("Hello!, from SharpConnect Http");

            TestApp testApp = new TestApp();
            try
            {
                //1. create  
#if DEBUG
                var webServer = new SharpConnect.WebServers.WebServer(8080, false, testApp.HandleRequest);
#else
                var webServer = new SharpConnect.WebServers.WebServer(8080, false, testApp.HandleRequest);
#endif
                //test websocket 
                var webSocketServer = new SharpConnect.WebServers.WebSocketServer();
                webSocketServer.SetOnNewConnectionContext(ctx =>
                {
                    s_contextList.Add(ctx);
                    ctx.SetMessageHandler(testApp.HandleWebSocket);
                });
                webServer.WebSocketServer = webSocketServer;
                webServer.Start();

                string cmd = "";
                while (cmd != "X")
                {
                    cmd = Console.ReadLine();
                    switch (cmd)
                    {
                        case "B":
                            {
                                //test broadcast
                                int j = s_contextList.Count;
                                for (int i = 0; i < j; ++i)
                                {
                                    s_contextList[i].Send("hello!");
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                // close the stream for test file writing
                try
                {
#if DEBUG

#endif
                }
                catch
                {
                    Console.WriteLine("Could not close log properly.");
                }
            }
        }


        static void Main_Https()
        {
            Console.WriteLine("Hello!, from SharpConnect Https");

            TestApp testApp = new TestApp();

            try
            {
                //1. create  
#if DEBUG
                var webServer = new SharpConnect.WebServers.WebServer(8080, false, testApp.HandleRequest);
#else
                var webServer = new SharpConnect.WebServers.WebServer(8080, false, testApp.HandleRequest);
#endif

                webServer.LoadCertificate(@"mycert.p12", "12345");
                webServer.UseSsl = true;

                //test websocket 
                var webSocketServer = new SharpConnect.WebServers.WebSocketServer();
                webSocketServer.SetOnNewConnectionContext(ctx =>
                {
                    s_contextList.Add(ctx);
                    ctx.SetMessageHandler(testApp.HandleWebSocket);
                });
                webServer.WebSocketServer = webSocketServer;
                webServer.Start();

                string cmd = "";
                while (cmd != "X")
                {
                    cmd = Console.ReadLine();
                    switch (cmd)
                    {
                        case "B":
                            {
                                //test broadcast
                                int j = s_contextList.Count;
                                for (int i = 0; i < j; ++i)
                                {
                                    s_contextList[i].Send("hello!");
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                // close the stream for test file writing
                try
                {
#if DEBUG

#endif
                }
                catch
                {
                    Console.WriteLine("Could not close log properly.");
                }
            }
        }
    }
}