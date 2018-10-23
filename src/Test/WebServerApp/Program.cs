//2015, MIT, EngineKit
using System;
using System.Collections.Generic; 
using SharpConnect.WebServers;

namespace SharpConnect
{
    static class Program
    {

        static void Main(string[] args)
        {
            Main2();
        }


        static List<WebSocketContext> s_contextList = new List<WebSocketContext>();
        static void Main2()
        {
            Console.WriteLine("Hello!, from SharpConnect");

            TestApp testApp = new TestApp();
            try
            {
                //1. create  
                WebServer webServer = new WebServer(8080, true, testApp.HandleRequest);

                //test websocket 
                var webSocketServer = new WebSocketServer();
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