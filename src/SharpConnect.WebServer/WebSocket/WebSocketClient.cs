//MIT, 2018-present, EngineKit and contributors

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

using SharpConnect.Internal2;

namespace SharpConnect.WebServers
{

    public class WebSocketClient
    {

        ReqRespHandler<WebSocketRequest, WebSocketResponse> _websocketHandler;
        System.Security.Cryptography.X509Certificates.X509Certificate2 _serverCert;
        WsClientSessionBase _clientBase;

        public WebSocketClient()
        {

        }
        public bool UseSsl { get; set; }
        public void SetHandler(ReqRespHandler<WebSocketRequest, WebSocketResponse> websocketHandler)
        {
            _websocketHandler = websocketHandler;
        }

        public void LoadCertificate(string certFile, string psw)
        {
            _serverCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certFile, psw);
        }

        public void Connect(string url)
        {
            if (UseSsl)
            {
                var secureWebSocketClient = new SecureWsSession();
                secureWebSocketClient.SetHandler(_websocketHandler);
                secureWebSocketClient.Connect(url, _serverCert);

                _clientBase = secureWebSocketClient;
            }
            else
            {
                var plainWebSocketClient = new PlainWsSession();
                plainWebSocketClient.SetHandler(_websocketHandler);
                plainWebSocketClient.Connect(url);

                _clientBase = plainWebSocketClient;
            }
        }
        public void SendData(string data)
        {
            _clientBase.SendData(data);
        }

        //--------------
        class PlainWsSession : WsClientSessionBase
        {
            public PlainWsSession()
            {

            }
            public void Connect(string url)
            {
                var plainWsConn = new PlainWebSocketConn(true);
                plainWsConn.SetMessageHandler(_websocketHandler);
                _wbsocketConn = plainWsConn;

                Uri uri = new Uri(url);
                InitClientSocket(uri);

                _clientSocket.Connect(_hostIP, uri.Port);
                //create http webreq  

                StringBuilder stbuilder = CreateWebSocketUpgradeReq(uri.AbsolutePath, uri.AbsolutePath + ":" + uri.Port);
                byte[] dataToSend = Encoding.ASCII.GetBytes(stbuilder.ToString());

                //get first confirm server upgrade resp from server
                byte[] firstRespBuffer = new byte[1024];
                int firstMsg = _clientSocket.Receive(firstRespBuffer, 1024, SocketFlags.None);

                //****
                //add event listener to our socket  
                plainWsConn.Bind(_clientSocket, dataToSend);
            }
        }


        /// <summary>
        /// web socket over secure protocol (ssl/tls)
        /// </summary>
        class SecureWsSession : WsClientSessionBase
        {
            public SecureWsSession()
            {

            }
            public void Connect(string url, System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
            {
                var secureWsConn = new SecureWebSocketConn(true);
                secureWsConn.SetMessageHandler(_websocketHandler);
                _wbsocketConn = secureWsConn;

                

                //TODO: review buffer management here***
                byte[] buffer1 = new byte[2048];
                byte[] buffer2 = new byte[2048];
                IOBuffer recvBuffer = new IOBuffer(buffer1, 0, buffer1.Length);
                IOBuffer sendBuffer = new IOBuffer(buffer2, 0, buffer2.Length);

                var sockNetworkStream = new SockNetworkStream(recvBuffer, sendBuffer);
                var secureStream = new SecureSockNetworkStream(sockNetworkStream, cert, delegate { return true; }); //***


                Uri uri = new Uri(url);
                InitClientSocket(uri);

                sockNetworkStream.Bind(_clientSocket);
                _clientSocket.Connect(_hostIP, uri.Port);

                //_secureStream.AuthenAsClient(uri.Host);
                secureStream.AuthenAsClient(uri.Host, () =>
                {
                    //--------------
                    StringBuilder stbuilder = CreateWebSocketUpgradeReq(uri.AbsolutePath, uri.AbsolutePath + ":" + uri.Port);
                    byte[] dataToSend = Encoding.ASCII.GetBytes(stbuilder.ToString());
                    //int totalCount = dataToSend.Length;
                    //int sendByteCount = _clientSocket.Send(dataToSend);
                    secureWsConn.Bind(secureStream, dataToSend);
                });

            }
        }
    }



}
