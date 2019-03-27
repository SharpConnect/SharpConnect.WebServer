using System;
using System.Collections.Generic;
using System.ComponentModel;

using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SharpConnect.WebServers;
using System.Net;
using System.Web;
using System.Net.Security;
using System.Net.Sockets;

namespace TestWsClient
{
    public partial class Form1 : Form
    {

        WebSocketClient _client;
        public Form1()
        {
            InitializeComponent();


        }

        private void button1_Click(object sender, EventArgs e)
        {
            

            if (_client != null) return;
            //------------------------------------------------
            _client = new WebSocketClient();
            _client.UseSsl = true;
            _client.LoadCertificate(@"D:\WImageTest\mycert.p12", "12345");
            _client.SetHandler((req, resp) =>
            {
                string serverMsg = req.ReadAsString();
                //Console.WriteLine(serverMsg);       
                this.Invoke(new MethodInvoker(() =>
                {
                    listBox1.Items.Add(serverMsg);

                }));
            });
            //.
            _client.Connect("https://localhost:8080/websocket");
        }

        int outputMsgCount = 0;
        private void button2_Click(object sender, EventArgs e)
        {
            _client.SendData("LOOPBACK: hello server " + outputMsgCount++);
        }

        private void button3_Click(object sender, EventArgs e)
        {
 

            Uri uri = new Uri("https://localhost:8080/websocket");

            //1. send websocket request upgrade protocol
            //2. 
            //var _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            //IPAddress ipAddress = IPAddress.Loopback;
            //if (uri.Host != "localhost")
            //{
            //    ipAddress = IPAddress.Parse(uri.Host);
            //}


            ////
            //NetworkStream netstream = new NetworkStream(_clientSocket);
            //SslStream ssl1 = new SslStream(netstream);
            //ssl1.AuthenticateAsClient("O=company, S=AA, C=US, CN=localhost");
            //_clientSocket.Connect(ipAddress, uri.Port);


            TcpClient client = new TcpClient("127.0.0.1", 8080);
            Console.WriteLine("Client connected.");
            // Create an SSL stream that will close the client's stream. 


            SslStream sslStream = new SslStream(
                client.GetStream(),
                false,
                (sender1, cert, chain, policy) =>
                {
                    //DO NOT USE THIS IN PRODUCTION CODE
                    return true;
                },
                null
                );
            sslStream.AuthenticateAsClient("localhost");

        }

        private void button4_Click(object sender, EventArgs e)
        {

            Uri uri = new Uri("https://localhost:8080/websocket"); 
            var _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            IPAddress ipAddress = IPAddress.Loopback;
            if (uri.Host != "localhost")
            {
                ipAddress = IPAddress.Parse(uri.Host);
            }
            //
            _clientSocket.Connect(ipAddress, uri.Port);

            NetworkStream netstream = new NetworkStream(_clientSocket);
            SslStream ssl1 = new SslStream(netstream,
                false,
                (sender1, cert, chain, policy) =>
                {
                    //DO NOT USE THIS IN PRODUCTION CODE
                    return true;
                },
                null
                );
            ssl1.AuthenticateAsClient("localhost");
        }
    }
}
