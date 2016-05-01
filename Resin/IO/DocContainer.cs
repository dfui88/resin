﻿using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class DocContainer : CompressedFileBase<DocContainer>
    {
        private readonly string _id;
        public string Id { get { return _id; } }

        private readonly Dictionary<string, Document> _files;
        /// <summary>
        /// docid/file
        /// </summary>
        public Dictionary<string, Document> Files { get { return _files; } }
        
        public DocContainer(string id)
        {
            _id = id;
            _files = new Dictionary<string, Document>();
        }
    }
}