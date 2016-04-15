﻿using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using NetSerializer;

namespace Resin.IO
{
    public class FileBase<T>
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public virtual void Save(string fileName)
        {
            try
            {
                var dir = Path.GetDirectoryName(fileName) ?? string.Empty;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                if (File.Exists(fileName))
                {
                    using (var fs = File.Open(fileName, FileMode.Truncate, FileAccess.Write, FileShare.Read))
                    {
                        Serializer.Serialize(fs, this);
                    }
                }
                else
                {
                    using (var fs = File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        Serializer.Serialize(fs, this);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Error in type {0}", typeof(T)), ex);
            }
        }

        public static T Load(string fileName)
        {
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Log.DebugFormat("loading {0}", fileName);
                return (T)Serializer.Deserialize(fs);
            }
        }

        private static readonly Type[] Types =
        {
            typeof (string), typeof (int), typeof (char), typeof (Trie), typeof (Document),
            typeof (Dictionary<string, string>), typeof (Dictionary<string, Document>),
            typeof (Dictionary<string, Dictionary<string, int>>), typeof(Dictionary<char, Trie>),
            typeof(Dictionary<string, object>),
            typeof(DixFile), typeof(DocFile), typeof(FieldFile), typeof(FixFile), typeof(IxFile),
            typeof(Term)
        };

        private static readonly Serializer Serializer = new Serializer(Types);
    }
}