//2015, MIT, EngineKit
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Text;
using SharpConnect.WebServers;

namespace SharpConnect
{
    static class Program
    {

        static void Main(string[] args)
        {
            Main2();
        }
        static List<WebSocketContext> s_connectionList = new List<WebSocketContext>();
        static void Main2()
        {
            Console.WriteLine("Hello!, from SharpConnect");

            TestApp testApp = new TestApp();
            try
            {
                //1. create  
                WebServer webServer = new WebServer(8080, true, testApp.HandleRequest);
                //webServer.LoadCertificate(@"C:\Users\User\cert.p12", "12345");
                //webServer.LoadCertificate(@"D:\WImageTest\mycert.p12", "12345");
                //webServer.UseSsl = true;

                ////----------------------------------------------------------------------

                //test websocket 
                var webSocketServer = new WebSocketServer();
                webSocketServer.SetOnNewConnectionContext(ctx =>
                {
                    s_connectionList.Add(ctx);
                    ctx.SetMessageHandler(testApp.HandleWebSocket);
                });
                webServer.WebSocketServer = webSocketServer;

                webServer.Start();
                //
                //for handle cert error on WebClient
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(delegate { return true; });

                //test connect to the server
                //try
                //{
                //    WebClient wb = new WebClient();
                //    wb.Proxy = null;
                //    //Console.WriteLine(wb.UploadString("https://localhost:8080", "ABCDEFGHI"));
                //    // Console.WriteLine(wb.DownloadString("https://localhost:8080"));
                //    //Console.WriteLine(wb.DownloadString("http://localhost:8080"));
                //}
                //catch (Exception ex)
                //{

                //}

                string userInput = Console.ReadLine();
                while (userInput != "X")
                {
                    userInput = Console.ReadLine();
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