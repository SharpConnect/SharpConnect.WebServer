using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Text;

namespace TestLoad
{
    class Program
    {
        static void Main(string[] args)
        {

            //for handle cert error on WebClient
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(delegate { return true; });

            //System.Threading.ThreadPool.QueueUserWorkItem(TestHttpsCall, 1);
            //System.Threading.ThreadPool.QueueUserWorkItem(TestHttpsCall, 2);
            //System.Threading.ThreadPool.QueueUserWorkItem(TestHttpsCall, 3);
            //System.Threading.ThreadPool.QueueUserWorkItem(TestHttpsCall, 4);

            System.Threading.Thread th1 = new System.Threading.Thread(TestHttpsCall);
            //System.Threading.Thread th2 = new System.Threading.Thread(TestHttpsCall2);
            //System.Threading.Thread th3 = new System.Threading.Thread(TestHttpsCall);
            //System.Threading.Thread th4 = new System.Threading.Thread(TestHttpsCall);

            th1.Start(1);
            //th2.Start(2);
            //th3.Start(3);
            //th4.Start(4);

            //th1.Join();
            //th2.Join();
            //th3.Join();
            //th4.Join();

            Console.ReadLine();
        }

        static void TestHttpsCall(object s)
        {
            int workname = (int)s;
            WebClient wb = new WebClient();
            wb.Proxy = null;
            try
            {
                for (int i = 0; i < 1010; ++i)
                {
                    Console.WriteLine(i.ToString());
                    Console.WriteLine(workname + " : " + wb.DownloadString("https://localhost:8080"));

                }
            }
            catch (Exception ex)
            {

            }
        }
        static void TestHttpsCall2(object s)
        {
            int workname = (int)s;
            WebClient wb = new WebClient();
            wb.Proxy = null;
            try
            {
                for (int i = 0; i < 5000; ++i)
                {
                    Console.WriteLine(i.ToString());
                    Console.WriteLine(workname + " : " + wb.DownloadString("http://localhost:8081"));

                    //Console.WriteLine(wb.DownloadString("http://localhost:8080"));
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
