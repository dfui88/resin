﻿using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Sir.HttpServer.Controllers
{
    public class QueryParserController : UIController
    {
        private IServiceProvider _serviceProvider;
        private readonly PluginsCollection _plugins;

        public QueryParserController(PluginsCollection plugins, IServiceProvider serviceProvider, IConfigurationProvider config) : base(config)
        {
            _serviceProvider = serviceProvider;
            _plugins = plugins;
        }

        [HttpGet("/queryparser/")]
        [HttpPost("/queryparser/")]
        public IActionResult Index(string q, string qf, string collection, string newCollection, string[] fields)
        {
            var formatter = _serviceProvider.GetService<IQueryFormatter>();
            var formatted = qf ?? formatter.Format(collection, Request);

            ViewData["qf"] = formatted;
            ViewData["q"] = q;

            var reader = _plugins.Get<IReader>("application/json");

            if (reader == null)
            {
                throw new NotSupportedException();
            }

            var timer = new Stopwatch();
            timer.Start();

            var result = reader.Read(collection, Request);

            ViewData["time_ms"] = timer.ElapsedMilliseconds;
            ViewData["collection"] = collection;
            ViewData["total"] = result.Total;
            ViewData["newCollection"] = newCollection;

            if (result.Total == 0)
            {
                return View(new SearchResultModel[0]);
            }

            var documents = result.Documents.Select(x => new SearchResultModel { Document = x });

            return View(documents);
        }
    }
}