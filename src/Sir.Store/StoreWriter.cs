﻿using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Sir.Store
{
    /// <summary>
    /// Write into a collection.
    /// </summary>
    public class StoreWriter : IWriter, ILogger
    {
        public string ContentType => "application/json";

        private readonly SessionFactory _sessionFactory;
        private readonly ITokenizer _tokenizer;
        private readonly Stopwatch _timer;

        public StoreWriter(SessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _timer = new Stopwatch();
        }

        public ResponseModel Write(string collectionName, HttpRequest request)
        {
            var documents = Deserialize<IEnumerable<IDictionary<string, object>>>(request.Body);
            var job = new Job(collectionName, documents);

            _sessionFactory.Commit(job);

            return new ResponseModel();
        }

        private static T Deserialize<T>(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer ser = new JsonSerializer();
                return ser.Deserialize<T>(jsonReader);
            }
        }

        public void Dispose()
        {
        }
    }
}