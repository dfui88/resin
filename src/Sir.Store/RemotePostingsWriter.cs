﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Write postings to HTTTP endpoint.
    /// </summary>
    public class RemotePostingsWriter
    {
        private IConfigurationService _config;
        private readonly StreamWriter _log;

        public RemotePostingsWriter(IConfigurationService config)
        {
            _config = config;
            _log = Logging.CreateWriter("remotepostingswriter");
        }

        public async Task Write(string collectionId, VectorNode rootNode)
        {
            var timer = new Stopwatch();
            timer.Start();

            var nodes = new List<VectorNode>();
            byte[] payload;

            // create postings message

            using (var message = new MemoryStream())
            using (var header = new MemoryStream())
            using (var body = new MemoryStream())
            {
                // write length of word (i.e. length of list of postings) to header 
                // and word itself to body
                var dirty = rootNode.SerializePostings(
                        collectionId, header, body).ToList();

                nodes.AddRange(dirty);

                if (nodes.Count != header.Length / sizeof(int))
                {
                    throw new DataMisalignedException();
                }

                // first word of message is payload count (i.e. num of words (i.e. posting lists))
                await message.WriteAsync(BitConverter.GetBytes(nodes.Count));

                // next is header
                header.Position = 0;
                await header.CopyToAsync(message);

                // last is body
                body.Position = 0;
                await body.CopyToAsync(message);

                payload = message.ToArray();
            }

            _log.Log(string.Format("built postings message in {0}", timer.Elapsed));

            // send message, recieve list of (remote) file positions, save positions in index.

            var positions = await Send(collectionId, payload);

            if (nodes.Count != positions.Count)
            {
                throw new DataMisalignedException();
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].PostingsOffset = positions[i];
            }
        }

        private async Task<IList<long>> Send(string collectionId, byte[] payload)
        {
            var timer = new Stopwatch();
            timer.Start();

            var result = new List<long>();

            var endpoint = _config.Get("postings_endpoint") + collectionId;

            var request = (HttpWebRequest)WebRequest.Create(endpoint);

            request.ContentType = "application/postings";
            request.Accept = "application/octet-stream";
            request.Method = WebRequestMethods.Http.Post;
            request.ContentLength = payload.Length;

            long responseBodyLen = 0;

            using (var requestBody = await request.GetRequestStreamAsync())
            {
                requestBody.Write(payload, 0, payload.Length);

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    using (var responseBody = response.GetResponseStream())
                    {
                        var mem = new MemoryStream();

                        await responseBody.CopyToAsync(mem);

                        var buf = mem.ToArray();

                        responseBodyLen = buf.LongLength;

                        if (buf.Length != response.ContentLength)
                        {
                            throw new DataMisalignedException();
                        }

                        int read = 0;

                        while (read < response.ContentLength)
                        {
                            result.Add(BitConverter.ToInt64(buf, read));

                            read += sizeof(long);
                        }
                    }
                }
            }

            _log.Log(string.Format("sent {0} bytes and recieved {1} bytes in {2}", payload.Length, responseBodyLen, timer.Elapsed));

            return result;    
        }
    }
}
