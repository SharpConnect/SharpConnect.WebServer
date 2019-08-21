//MIT, 2018-present, EngineKit and contributors

using System;
using System.Collections.Generic;
using System.Threading;

namespace SharpConnect.WebServers
{
    struct WebSocketReqQueueItem
    {
        public readonly WebSocketConnectionBase _conn;
        public readonly WebSocketRequest _request;
        public WebSocketReqQueueItem(WebSocketConnectionBase conn, WebSocketRequest req)
        {
            _conn = conn;
            _request = req;
        }
    }

    static class WebSocketReqInputQueue
    {
        static Thread s_mainClearingThread;
        static Queue<WebSocketReqQueueItem> s_queue = new Queue<WebSocketReqQueueItem>();
        //static bool s_hasSomeData;
        //static bool s_running;
        static WebSocketReqInputQueue()
        {
            s_mainClearingThread = new Thread(ClearingThread);
            SharpConnect.Internal2.GlobalMsgLoop.s_running = true;
#if DEBUG
            s_mainClearingThread.Name = "WebSocketReqInputQueue";
#endif


            s_mainClearingThread.Start();
        }

        public static void Enqueue(WebSocketReqQueueItem item)
        {
            lock (s_queue)
            {
                s_queue.Enqueue(item);
                SharpConnect.Internal2.GlobalMsgLoop.s_running = true;
                Monitor.Pulse(s_queue);
            }
        }
        public static void StopAndExitQueue()
        {
            //stop and exit queue
            SharpConnect.Internal2.GlobalMsgLoop.s_running = false;
            lock (s_queue)
            {
                //signal the queue
                Monitor.Pulse(s_queue);
            }
        }
        static void ClearingThread(object s)
        {
            WebSocketReqQueueItem item = new WebSocketReqQueueItem();
            while (SharpConnect.Internal2.GlobalMsgLoop.s_running)
            {
                bool foundJob = false;
                lock (s_queue)
                {
                    int count = s_queue.Count;
                    if (count > 0)
                    {
                        item = s_queue.Dequeue();
                        foundJob = true;
                        //run this task  
                    }
                    else
                    {
                        SharpConnect.Internal2.GlobalMsgLoop.s_hasSomeData = false;
                    }
                }
                if (foundJob)
                {
                    try
                    {
                        item._conn.InvokeReqHandler(item._request);
                    }
                    catch (Exception ex)
                    {

                    }

                }
                else
                {
                    lock (s_queue)
                        while (!SharpConnect.Internal2.GlobalMsgLoop.s_hasSomeData)
                            Monitor.Wait(s_queue, 10);
                }
            }
        }
    }
}