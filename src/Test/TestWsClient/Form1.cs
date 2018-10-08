using System;
using System.Collections.Generic;
using System.ComponentModel;

using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SharpConnect.WebServers;


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

        int outputMsgCount = 0;
        private void button2_Click(object sender, EventArgs e)
        {
            _client.SendData("LOOPBACK: hello server " + outputMsgCount++);
        }
    }
}
