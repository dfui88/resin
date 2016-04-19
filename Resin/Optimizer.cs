﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    /// <summary>
    /// Initialize an Optimizer with your directory's current baseline and then fast-forward to the directory's latest commit by calling Optimizer.Rebase().
    /// Save that state as a new baseline by calling Optimizer.Save().
    /// </summary>
    public class Optimizer : DocumentReader
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Optimizer));
        private readonly string _directory;
        private readonly Dictionary<string, FieldFile> _fieldFiles;
        private readonly Dictionary<string, Trie> _trieFiles;
        private readonly FixFile _fix;
        private readonly string _ixFileName;
        private readonly IList<string> _processedCommits; 

        public Optimizer(string directory, string ixFileName, DixFile dix, FixFile fix) : base(directory, dix)
        {
            _ixFileName = ixFileName;
            _fieldFiles = new Dictionary<string, FieldFile>();
            _trieFiles = new Dictionary<string, Trie>();
            _directory = directory;
            _fix = fix;
            _processedCommits = new List<string>();
        }

        /// <summary>
        /// Rewind the state of your directory to a commit older than the current baseline.
        /// </summary>
        /// <param name="indexOrCommitFileName"></param>
        public void RebaseHard(string indexOrCommitFileName)
        {
            //TODO: implement set head
        }

        /// <summary>
        /// Rewind the state of your directory to a commit older than the current baseline.
        /// </summary>
        /// <param name="commitFileName"></param>
        public void RewindTo(string commitFileName)
        {
            //TODO: implement rewind
        }

        /// <summary>
        /// Irreversibly delete commits that are older than the latest baseline.
        /// </summary>
        public void Truncate()
        {
            //TODO: implement truncate
        }

        public void Rebase()
        {
            // get all commits
            var commits = Helper.GetFilesOrderedChronologically(_directory, "*.co").ToList();
            
            // rewind
            var nextCommit = Helper.GetNextCommit(_ixFileName, commits);
            
            // apply commits younger than the baseline, i.e. younger than _ixFileName
            while (nextCommit != null)
            {
                Apply(nextCommit);
                nextCommit = Helper.GetNextCommit(nextCommit, commits);
            }
        }

        public IxFile Save()
        {
            return Helper.Save(_directory, ".ix", Dix, _fix, Docs, _fieldFiles, _trieFiles);
        }

        /// <summary>
        /// Apply a commit 
        /// </summary>
        /// <param name="commitFileName">The *.co file name of the commit.</param>
        private void Apply(string commitFileName)
        {
            if (commitFileName == null) throw new ArgumentNullException("commitFileName");

            Log.InfoFormat("applying {0}", commitFileName);
            var timer = new Stopwatch();
            timer.Start();
            var ix = IxFile.Load(commitFileName);
            foreach (var term in ix.Deletions)
            {
                var collector = new Collector(Directory, _fix, _fieldFiles, _trieFiles);
                var docIds = collector.Collect(new QueryContext(term.Field, term.Token), 0, int.MaxValue).Select(ds => ds.DocId).ToList();

                // delete docs
                // loads into memory all cleaned doc files
                // loads all field files except last gen
                foreach (var docId in docIds)
                {
                    var docFile = GetDocFile(docId);
                    docFile.Docs.Remove(docId);
                    Dix.DocIdToFileId.Remove(docId);

                    foreach (var field in _fix.FieldToFileId)
                    {
                        var fileId = field.Value;
                        var fileName = Path.Combine(Directory, fileId + ".f");
                        FieldFile ff;
                        if (!_fieldFiles.TryGetValue(fileId, out ff))
                        {
                            ff = FieldFile.Load(fileName);
                            _fieldFiles[fileId] = ff;
                        }
                        ff.Remove(docId);
                    }
                }
            }

            // upsert docs
            // loads all updated docs
            var dix = DixFile.Load(Path.Combine(Directory, ix.DixFileName));
            foreach (var fileId in dix.DocIdToFileId.Values.Distinct())
            {
                var fileName = Path.Combine(_directory, fileId + ".d");
                var file = DocFile.Load(fileName);
                foreach (var doc in file.Docs)
                {
                    if (Dix.DocIdToFileId.ContainsKey(doc.Key))
                    {
                        var prevDoc = GetDoc(doc.Key);
                        foreach (var field in doc.Value.Fields)
                        {
                            prevDoc[field.Key] = field.Value; // field-wise upsert
                        }
                        Docs[doc.Key] = new Document(prevDoc);
                    }
                    else
                    {
                        Dix.DocIdToFileId[doc.Key] = null; // value will be set when saving
                        Docs[doc.Key] = doc.Value;
                    }
                }
            }

            // update field files
            // loads into memory all updated field files
            var fix = FixFile.Load(Path.Combine(Directory, ix.FixFileName));
            foreach (var field in fix.FieldToFileId)
            {
                var fieldName = field.Key;
                var newFileId = field.Value;
                var newFileName = Path.Combine(Directory, newFileId + ".f");
                var newFile = FieldFile.Load(newFileName);

                if (_fix.FieldToFileId.ContainsKey(fieldName))
                {
                    // we already know about this field

                    var previousFileId = _fix.FieldToFileId[fieldName];
                    var previousFileName = Path.Combine(Directory, previousFileId + ".f");

                    FieldFile prevFieldFile;
                    if (!_fieldFiles.TryGetValue(previousFileId, out prevFieldFile))
                    {
                        prevFieldFile = FieldFile.Load(previousFileName);
                        _fieldFiles[previousFileId] = prevFieldFile;
                    }

                    Trie prevTri;
                    if (!_trieFiles.TryGetValue(previousFileId, out prevTri))
                    {
                        prevTri = Trie.Load(previousFileName + ".tri");
                        _trieFiles[previousFileId] = prevTri;
                    }

                    foreach (var entry in newFile.Entries)
                    {
                        var token = entry.Key;
                        foreach (var posting in entry.Value)
                        {
                            var docId = posting.Key;
                            var freq = posting.Value;
                            prevFieldFile.AddOrOverwrite(docId, token, freq);
                        }
                        prevTri.Add(token);
                    }

                    var rebasedFileId = Path.GetRandomFileName();

                    _fix.FieldToFileId[fieldName] = rebasedFileId;
                    _fieldFiles[rebasedFileId] = prevFieldFile;
                    _trieFiles[rebasedFileId] = prevTri;
                }
                else
                {
                    // completely new field
                    _fix.FieldToFileId[fieldName] = newFileId;
                    _fieldFiles[newFileId] = newFile;
                    _trieFiles[newFileId] = Trie.Load(newFileName + ".tri");
                }
            }
            _processedCommits.Add(commitFileName);
            Log.InfoFormat("rebased {0} in {1}", commitFileName, timer.Elapsed);
        }
    }
}