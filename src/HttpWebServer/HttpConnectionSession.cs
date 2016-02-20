//2015, MIT, EngineKit 
using System.Net.Sockets;
using System.Text;
using SharpConnect.Internal;

namespace SharpConnect.WebServer
{  
    class HttpConnectionSession : ConnectionSession
    {
       
        HttpRequestHandler reqHandler;
        HttpRequest httpReq;
        HttpResponse httpResp;
        byte[] tmpReadBuffer;
        int receiveState = 0;

        public HttpConnectionSession(HttpRequestHandler reqHandler)
        {
            this.reqHandler = reqHandler;
            httpReq = new HttpRequest(this);
            httpResp = new HttpResponse(this);
            tmpReadBuffer = new byte[512];
        }
       
        //===================
        //protocol specfic
        int ParseHttpRequestHeader(ReceiveCarrier recvCarrier)
        {
            int readpos = 0;
            int lim = recvCarrier.BytesTransferred - 1;
            int i = 0;
            for (; i <= lim; ++i)
            {
                //just read 
                if (recvCarrier.ReadByte(i) == '\r' &&
                    recvCarrier.ReadByte(i + 1) == '\n')
                {
                    //each line
                    //translate
                    if (i - readpos < 512)
                    {
                        //copy 
                        recvCarrier.ReadBytes(tmpReadBuffer, readpos, i - readpos);
                        //translate
                        string line = Encoding.UTF8.GetString(tmpReadBuffer, 0, i - readpos);
                        readpos = i + 2;
                        i++; //skip \n            
                        //translate header ***
                        if (line == "")
                        {
                            //complete http header                           
                            receiveState = 1;
                            return readpos;
                        }
                        else
                        {
                            //parse header line
                            AddReqHeader(line);
                        }
                    }
                    else
                    {
                        //just skip?
                        //skip too long line
                        readpos = i + 2;
                        i++; //skip \n 
                    }
                }
            }
            return readpos;
        }
        void AddReqHeader(string line)
        {
            if (httpReq.Url == null)
            {
                //check if GET or POST
                bool foundHttpMethod = false;
                HttpMethod httpMethod = HttpMethod.Get;
                if (line.StartsWith("GET"))
                {
                    foundHttpMethod = true;
                }
                else if (line.StartsWith("POST"))
                {
                    foundHttpMethod = true;
                    httpMethod = HttpMethod.Post;
                }

                //--------------------------------------------------------------
                if (foundHttpMethod)
                {
                    //parse req url
                    string[] splitedLines = line.Split(' ');
                    if (splitedLines.Length > 1)
                    {

                        string getContent = splitedLines[1];
                        int qpos = getContent.IndexOf('?');
                        if (qpos > -1)
                        {

                            httpReq.Url = getContent.Substring(0, qpos);

                            string[] paramsParts = getContent.Substring(qpos + 1).Split('&');
                            int paramLength = paramsParts.Length;
                            var reqParams = new WebRequestParameter[paramLength];
                            for (int p = 0; p < paramLength; ++p)
                            {
                                string p_piece = paramsParts[p];
                                int eq_pos = p_piece.IndexOf('=');
                                if (eq_pos > -1)
                                {
                                    reqParams[p] = new WebRequestParameter(p_piece.Substring(0, eq_pos),
                                        p_piece.Substring(eq_pos + 1));
                                }
                                else
                                {
                                    reqParams[p] = new WebRequestParameter(p_piece, "");
                                }
                            }
                            httpReq.ReqParameters = reqParams;
                        }
                        else
                        {
                            httpReq.Url = getContent;
                        }
                    }
                    httpReq.HttpMethod = httpMethod;
                    return;
                }
            }

            //--------------------------------------------------------------
            //sep key-value
            int pos = line.IndexOf(':');
            if (pos > -1)
            {
                string key = line.Substring(0, pos);
                string value = line.Substring(pos + 1);
                httpReq.AddHeaderInfo(key.Trim(), value.Trim());
            }
        }
        /// <summary>
        /// read buffer by user specific protocol
        /// </summary>
        /// <param name="saArgs"></param>
        /// <returns></returns>
        public override EndReceiveState ProtocolRecvBuffer(ReceiveCarrier recvCarrier)
        {  
            //read http protocol
            //find header   
            switch (this.receiveState)
            {
                case 0:
                    {
                        //find html header 
                        var readToPos = ParseHttpRequestHeader(recvCarrier);
                        switch (httpReq.HttpMethod)
                        {
                            case HttpMethod.Post:
                                return ProcessHtmlPostBody(recvCarrier, readToPos);
                        }
                    }
                    break;
                default:
                case 1:
                    {
                        switch (httpReq.HttpMethod)
                        {
                            case HttpMethod.Post:
                                return ProcessHtmlPostBody(recvCarrier, 0);
                        }
                    }
                    break;
            }
            return EndReceiveState.Complete;
        }
        EndReceiveState ProcessHtmlPostBody(ReceiveCarrier recvCarrier, int readToPos)
        {
            //parse body
            int transferedBytes = recvCarrier.BytesTransferred;
            int remaining = transferedBytes - readToPos;
            if (!httpReq.IsMsgBodyComplete)
            {
                int wantBytes = httpReq.ContentLength - httpReq.CollectingContentByteCount;
                if (wantBytes <= remaining)
                {
                    //complete here 
                    byte[] buff = new byte[wantBytes];
                    recvCarrier.ReadBytes(buff, readToPos, wantBytes);
                    //add to req  
                    httpReq.AddMsgBody(buff, 0, wantBytes);
                    return EndReceiveState.Complete;
                }
                else
                {

                    //continue read             
                    if (remaining > 0)
                    {
                        byte[] buff = new byte[remaining];
                        recvCarrier.ReadBytes(buff, readToPos, remaining);
                        //add to req  
                        httpReq.AddMsgBody(buff, 0, remaining);
                    }

                    return EndReceiveState.ContinueRead;
                }
            }
            return EndReceiveState.Complete;

        }
        public override void ResetRecvBuffer()
        {
            //clear recv buffer
            httpReq.Reset();
            receiveState = 0;//
        }
        public override void HandleRequest()
        {   
#if DEBUG
            //------------------------------------------------------------------------- 
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                dbugLOG.WriteLine("Mediator HandleInputData() " + this.dbugTokenId);
            }
            ////-------------------------------------------------------------------------
            //preapre send data            
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                dbugLOG.WriteLine("Mediator PrepareOutputData() " + this.dbugTokenId);
            }
#endif      
            //do operation and prepare output ?
            this.reqHandler(this.httpReq, this.httpResp);
            if (httpResp.NeedFlush)
            {
                httpResp.Flush();
            }
           
        }
#if DEBUG
        public string dbugGetDataInHolder()
        {
            return "";
        }
#endif


    }

}