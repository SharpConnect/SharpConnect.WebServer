﻿//MIT, 2015-present, EngineKit
using System;
using System.IO;
using System.Text;

namespace SharpConnect.WebServers
{

    class HttpWebRequest : HttpRequest
    {
        HttpContext _context;
        internal HttpWebRequest(HttpContext context)
            : base()
        {
            _context = context;
            _bodyMs = new MemoryStream();
        }


        //===================
        //parsing 
        HttpParsingState _parseState;
        bool IsMsgBodyComplete => _contentByteCount >= _targetContentLength;

        void AddMsgBody(byte[] buffer, int start, int count)
        {
            _bodyMs.Write(buffer, start, count);
            _contentByteCount += count;
        }
        void AddHeaderInfo(string key, string value)
        {
            //replace if exist
            _headerKeyValues[key] = value;
            //translate some key eg. content-length,encoding
            switch (key)
            {
                case "Content-Length":
                    {
                        int.TryParse(value, out _targetContentLength);
                    }
                    break;
                case "Connection":
                    {
                        _context.KeepAlive = (value.ToLower().Trim() == "keep-alive");

                    }
                    break;
            }
        }
        /// <summary>
        /// add and parse data
        /// </summary>
        /// <param name="buffer"></param>
        internal ProcessReceiveBufferResult LoadData(Internal.RecvIO recvIO)
        {
            switch (_parseState)
            {
                case HttpParsingState.Head:
                    {
                        //find html header 
                        int readpos = ParseHttpRequestHeader(recvIO);
                        //check if complete or not
                        if (_parseState == HttpParsingState.Body)
                        {
                            ProcessHtmlPostBody(readpos, recvIO);
                        }
                    }
                    break;
                case HttpParsingState.Body:
                    ProcessHtmlPostBody(0, recvIO);
                    break;
                case HttpParsingState.Complete:
                    break;
                default:
                    throw new NotSupportedException();
            }

            if (_parseState == HttpParsingState.Complete)
            {
                return ProcessReceiveBufferResult.Complete;
            }
            else
            {
                return ProcessReceiveBufferResult.NeedMore;
            }

        }
        //===================
        void AddReqHeader(string line)
        {
            if (Url == null)
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
                            Url = getContent.Substring(0, qpos);
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
                            ReqParameters = reqParams;
                        }
                        else
                        {
                            Url = getContent;
                        }
                    }
                    HttpMethod = httpMethod;
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
                AddHeaderInfo(key.Trim(), value.Trim());
            }
            else
            {
            }
        }

        int ParseHttpRequestHeader(Internal.RecvIO recvIO)
        {
            //start from pos0
            int readpos = 0;
            int lim = recvIO.BytesTransferred - 1;
            int i = 0;
            for (; i <= lim; ++i)
            {
                //just read 
                if (recvIO.ReadByte(i) == '\r' &&
                    recvIO.ReadByte(i + 1) == '\n')
                {
                    //each line
                    //translate
                    if (i - readpos < 512)
                    {
                        //copy     
                        recvIO.CopyTo(readpos, _tmpReadBuffer, i - readpos);
                        //translate
                        string line = Encoding.UTF8.GetString(_tmpReadBuffer, 0, i - readpos);
                        readpos = i + 2;
                        i++; //skip \n            
                             //translate header ***
                        if (line == "")
                        {
                            //complete http header                           
                            _parseState = HttpParsingState.Body;
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
        void ProcessHtmlPostBody(int readpos, Internal.RecvIO recvIO)
        {
            //parse body
            int transferedBytes = recvIO.BytesTransferred;
            int remaining = transferedBytes - readpos;
            if (!IsMsgBodyComplete)
            {
                int wantBytes = ContentLength - _contentByteCount;
                if (wantBytes <= remaining)
                {
                    //complete here 
                    byte[] buff = new byte[wantBytes];
                    recvIO.CopyTo(readpos, buff, wantBytes);
                    //add to req  
                    AddMsgBody(buff, 0, wantBytes);
                    //complete 
                    _parseState = HttpParsingState.Complete;
                    return;
                }
                else
                {
                    //continue read             
                    if (remaining > 0)
                    {
                        byte[] buff = new byte[remaining];
                        recvIO.CopyTo(readpos, buff, remaining);
                        //add to req  
                        AddMsgBody(buff, 0, remaining);
                    }

                    return;
                }
            }
            _parseState = HttpParsingState.Complete; 
        } 
    }

}