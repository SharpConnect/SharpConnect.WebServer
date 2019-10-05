using System;
using SharpConnect.WebServers;
namespace TestWsClient_NETSTD
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("test no...\r\n0\r\n1");
            //------------------------------------------------
            WebSocketClient _client = null;
            string line = Console.ReadLine();
            int count = 0;
            while (line != null)
            {
                if (int.TryParse(line, out int line_count))
                {
                    if (line_count == 0)
                    {
                        if (_client == null)
                        {
                            _client = new WebSocketClient();
                            _client.UseSsl = true;

                            //test send-recv with defalte compression
                            //client request this value but server may not accept the req compression
                            _client.Compression = WebSocketContentCompression.Gzip;


                            _client.SetHandler((req, resp) =>
                            {
                                string serverMsg = req.ReadAsString();
                                Console.WriteLine(serverMsg);
                            });
                            _client.Connect("https://localhost:8080/websocket");
                            //------------------------------------------------
                            Console.WriteLine("connected!");
                        }
                        else
                        {
                            Console.WriteLine("already connected!");
                        }
                    }
                    else if (line_count > 0 && line_count <= 5000)
                    {
                        byte[] binaryData = System.Text.Encoding.UTF8.GetBytes("abcde");
                        for (int i = 0; i < line_count; ++i)
                        {
                            //_client.SendData("A" + (count++));
                            _client.SendBinaryData(binaryData);
                        }
                    }
                    else
                    {
                        Console.WriteLine("input between 0-5000");
                    }
                }

                line = Console.ReadLine();
            }


        }
    }
}
