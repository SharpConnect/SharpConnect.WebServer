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


        static readonly WebSocketReqQueueItem s_empty = new WebSocketReqQueueItem();
        public static WebSocketReqQueueItem Empty => s_empty;
    }

    static class WebSocketReqInputQueue
    {
        static Thread s_mainClearingThread;
        static Queue<WebSocketReqQueueItem> s_queue = new Queue<WebSocketReqQueueItem>();

        static int s_running;
        static int s_threadCreated;

        static WebSocketReqInputQueue()
        {

        }

        static void RunClearingThread()
        {


            if (Interlocked.CompareExchange(ref s_threadCreated, 1, 0) == 1)
            {
                //already created
                return;
            }

            //if not then create this
            s_mainClearingThread = new Thread(ClearingThread);
#if DEBUG
            s_mainClearingThread.Name = "WebSocketReqInputQueue";
#endif

            Interlocked.Exchange(ref s_running, 1);
            s_mainClearingThread.Start();
        }
        public static void Enqueue(WebSocketReqQueueItem item)
        {
            lock (s_queue)
            {
                s_queue.Enqueue(item);
                RunClearingThread();
                Monitor.Pulse(s_queue);
            }
        }
        public static void StopAndExitQueue()
        {
            //stop and exit queue
            Interlocked.Exchange(ref s_running, 0);
            lock (s_queue)
            {
                //signal the queue
                Monitor.Pulse(s_queue);
            }
        }
        static void ClearingThread(object s)
        {
            WebSocketReqQueueItem item = WebSocketReqQueueItem.Empty;

            while (s_running == 1)
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
                    int noJobCount = 0;
                    lock (s_queue)
                    {
                        while (s_queue.Count == 0)
                        {
                            Monitor.Wait(s_queue, 2000);
                            noJobCount++;
                            if (noJobCount > 5)
                            {
                                //auto stop this thread if not job 
                                //within a limit time

                                Interlocked.Exchange(ref s_running, 0);
                                Thread mainThread = s_mainClearingThread;
                                mainThread = null;
                                Interlocked.Exchange(ref s_threadCreated, 0);
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}