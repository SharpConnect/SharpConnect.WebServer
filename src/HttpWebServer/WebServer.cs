//2015, MIT, EngineKit
using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Text; //for testing
using SharpConnect.Internal;

namespace SharpConnect.WebServer
{

    public class HttpServer
    {

        HttpSocketServerSetting setting;
        SocketServer socketServer;
        bool isRunning;
        public HttpServer(HttpSocketServerSetting setting)
        {
            this.setting = setting;
        }
        public HttpServer(int port, HttpRequestHandler reqHandler)
        {

            int maxNumberOfConnections = 1000;
            int excessSaeaObjectsInPool = 200;
            int backlog = 100;
            int maxSimultaneousAcceptOps = 100;

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
            this.setting = new HttpSocketServerSetting(maxNumberOfConnections,
                   excessSaeaObjectsInPool,
                   backlog,
                   maxSimultaneousAcceptOps,
                   localEndPoint);
            setting.RequestHandler += reqHandler;
        }
        public void Start()
        {
            if (isRunning) return;
            //------------------------------
            try
            {
                //start web server 
                socketServer = new SocketServer(setting);
                isRunning = true;
            }
            catch (Exception ex)
            {
            }
        }
        public void Stop()
        {

        }
    }

}