﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    [Route("io")]
    public class IOController : Controller, ILogger
    {
        private readonly PluginsCollection _plugins;

        public IOController(PluginsCollection plugins)
        {
            _plugins = plugins;
        }

        [HttpPost("{*collectionName}")]
        public IActionResult Post(string collectionName)
        {
            if (collectionName == null)
            {
                throw new ArgumentNullException(nameof(collectionName));
            }

            var writer = _plugins.Get<IWriter>(Request.ContentType);

            if (writer == null)
            {
                throw new NotSupportedException(); // Media type not supported
            }

            try
            {
                ResponseModel result = writer.Write(collectionName, Request);

                if (result.Stream != null)
                {
                    var buf = result.Stream.ToArray();

                    return new FileContentResult(buf, result.MediaType);
                }
                else
                {
                    return Ok();
                }
            }
            catch (Exception ew)
            {
                this.Log(ew);
                throw ew;
            }
        }

        [HttpGet("{*collectionName}")]
        [HttpPut("{*collectionName}")]
        public async Task<IActionResult> Get(string collectionName)
        {
            var mediaType = Request.Headers["Accept"].ToArray()[0];
            var reader = _plugins.Get<IReader>(mediaType);

            if (reader == null)
            {
                throw new NotSupportedException(); // Media type not supported
            }

            var timer = Stopwatch.StartNew();
            var result = await reader.Read(collectionName, Request);

            this.Log("processed {0} request in {1}", mediaType, timer.Elapsed);

            if (result.Stream == null)
            {
                return new FileContentResult(new byte[0], result.MediaType);
            }
            else
            {
                Response.Headers.Add("X-Total", result.Total.ToString());

                var buf = result.Stream.ToArray();

                this.Log("serialized {0} response in {1}", reader.GetType().ToString(), timer.Elapsed);

                return new FileContentResult(buf, result.MediaType);
            }
        }
    }
}