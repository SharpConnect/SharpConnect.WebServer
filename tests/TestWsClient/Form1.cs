using System;
using System.Windows.Forms;
using SharpConnect.WebServers;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;

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
            //plain websocket
            if (_client != null) return;
            //------------------------------------------------
            _client = new WebSocketClient();
            _client.SetHandler((req, resp) =>
            {
                string serverMsg = req.ReadAsString();
                //Console.WriteLine(serverMsg);       
                this.Invoke(new MethodInvoker(() =>
                {
                    listBox1.Items.Add(serverMsg);

                }));
            });
            _client.Connect("http://localhost:8080/websocket");
        }

        int _outputMsgCount = 0;


        private void button3_Click(object sender, EventArgs e)
        {


            //secure websocket
            if (_client != null) return;
            //------------------------------------------------
            _client = new WebSocketClient();
            _client.UseSsl = true;
            _client.SetHandler((req, resp) =>
            {
                string serverMsg = req.ReadAsString();
                //Console.WriteLine(serverMsg);       
                this.Invoke(new MethodInvoker(() =>
                {
                    listBox1.Items.Add(serverMsg);

                    //if (_outputMsgCount < 100)
                    //{
                    //    _client.SendData("LOOPBACK: hello server " + _outputMsgCount++);
                    //}
                }));
            });
            //_client.Connect("https://localhost:8080/websocket");
            //_client.Connect("https://localhost:8080/websocket");
            //_client.LoadCertificate("mycert.pfx", "12345");
            _client.Connect("https://localhost:8080/");
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            _client.SendTextData("LOOPBACK: hello server " + _outputMsgCount++);

        }
        private void button4_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 100; ++i)
            {
                _client.SendTextData("LOOPBACK: hello server " + _outputMsgCount++);
                //_client.SendData("LOOPBACK: hello server " + _outputMsgCount++);
                //_client.SendData("LOOPBACK: hello server " + _outputMsgCount++);
                //_client.SendData("LOOPBACK: hello server " + _outputMsgCount++);
                //_client.SendData("LOOPBACK: hello server " + _outputMsgCount++);
                //_client.SendData("LOOPBACK: hello server " + _outputMsgCount++);
                //_client.SendData("LOOPBACK: hello server " + _outputMsgCount++);
                //_client.SendData("LOOPBACK: hello server " + _outputMsgCount++);
                //_client.SendData("LOOPBACK: hello server " + _outputMsgCount++);
                //_client.SendData("LOOPBACK: hello server " + _outputMsgCount++);

            }

        }

        private void button5_Click(object sender, EventArgs e)
        {
            
        }
    }
}
