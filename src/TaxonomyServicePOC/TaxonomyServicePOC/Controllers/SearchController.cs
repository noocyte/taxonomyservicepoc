using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MimeKit.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TaxonomyServicePOC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SearchController : ControllerBase
    {

        [HttpPost("index")]
        public async Task<IActionResult> Index()
        {
            await Indexer.Index("maildir");
            return Ok();
        }

        [HttpPost("{taxonomyName}")]
        public async ValueTask<IEnumerable<Taxonomy>> Search(string taxonomyName, string q)
        {
            if (!Indexer.IsIndexed)
                await Indexer.Index("maildir");

            return Indexer.SearchBody(q);

        }
    }

    public static class Indexer
    {
        static char[] spearators = new char[] { ' ', '\t', ',', '!', '\r', '(', ')', '?', '-', '"', '\n', '/' };
        static char[] trim = new char[] { '.', };

        public class DocRef
        {
            public string Id;
            public string Path;
            public long Number;
            public Taxonomy Actual;
        }

        public enum Fields
        {
            Body,
            Subject,
            Date,
            From,
            To
        }
        static HashSet<string> stopWords = new HashSet<string>
        {
            "a", "an", "and", "are", "as", "at", "be", "but", "by",
            "for", "if", "in", "into", "is", "it",
            "no", "not", "of", "on", "or", "such",
            "that", "the", "their", "then", "there", "these",
            "they", "this", "to", "was", "will", "with", "you", "have", "has" ,
            "please"
        };

        public static bool IsIndexed { get; set; }
        static ConcurrentDictionary<long, DocRef> docs = new ConcurrentDictionary<long, DocRef>();
        static ConcurrentDictionary<string, ConcurrentQueue<long>>[] fields = new ConcurrentDictionary<string, ConcurrentQueue<long>>[]
           {
                new ConcurrentDictionary<string, ConcurrentQueue<long>>(), // Body
                new ConcurrentDictionary<string, ConcurrentQueue<long>>(), // Subject
                new ConcurrentDictionary<string, ConcurrentQueue<long>>(), // Date
                new ConcurrentDictionary<string, ConcurrentQueue<long>>(), // From
                new ConcurrentDictionary<string, ConcurrentQueue<long>>(), // To 
           };

        public static IEnumerable<Taxonomy> SearchBody(string term)
        {
            if (fields[(int)Fields.Body].TryGetValue(term.ToLower(), out var matches))
            {
                var counter = 0;
                foreach (var item in matches)
                {
                    counter++;
                    yield return docs[item].Actual;
                    if (counter >= 25) break;
                }
            }

        }

        public static async Task Index(string dir)
        {
            long index = 0;

            var blockingCollection = new BlockingCollection<string>(2048);
            var tasks = new List<Task>();
            for (int i = 0; i < 8; i++)
            {
                var task = Task.Run(() =>
                {
                    while (blockingCollection.IsCompleted == false)
                    {
                        using var stream = File.OpenRead(blockingCollection.Take());

                        var parser = new MimeParser(stream, MimeFormat.Entity);
                        while (parser.IsEndOfStream == false)
                        {
                            var entity = parser.ParseMessage();
                            var actual = new Taxonomy();
                            actual["id"] = entity.MessageId;
                            actual["name"] = entity.Subject;

                            var cur = Interlocked.Increment(ref index);
                            foreach (var item in entity.To)
                            {
                                var to = actual.GetStringOrDefault("to") + " " + item;
                                actual["to"] = to.Trim();

                                fields[(int)Fields.To]
                                    .GetOrAdd(item.ToString().ToLower(), _ => new ConcurrentQueue<long>()).Enqueue(cur);
                            }

                            foreach (var item in entity.From)
                            {
                                var from = actual.GetStringOrDefault("from") + " " + item;
                                actual["from"] = from.Trim();

                                fields[(int)Fields.From]
                                    .GetOrAdd(item.ToString().ToLower(), _ => new ConcurrentQueue<long>()).Enqueue(cur);
                            }

                            foreach (var item in Process(entity.Subject))
                            {
                                fields[(int)Fields.Subject]
                                    .GetOrAdd(item, _ => new ConcurrentQueue<long>()).Enqueue(cur);
                            }

                            var bodyText = entity.GetTextBody(TextFormat.Plain);
                            foreach (var item in Process(bodyText))
                            {
                                actual["body"] = bodyText;

                                fields[(int)Fields.Body]
                                    .GetOrAdd(item, _ => new ConcurrentQueue<long>()).Enqueue(cur);
                            }

                            // skipping date in actual
                            fields[(int)Fields.Date].GetOrAdd(entity.Date.ToString("r"), _ => new ConcurrentQueue<long>()).Enqueue(cur);

                            docs.TryAdd(cur, new DocRef
                            {
                                Id = entity.MessageId,
                                Path = stream.Name,
                                Number = cur,
                                Actual = actual
                            });
                        }
                    }
                });
                tasks.Add(task);
            }

            tasks.Add(Task.Run(() =>
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    blockingCollection.Add(file);
                }
                blockingCollection.CompleteAdding();
            }));

            await Task.WhenAll(tasks.ToArray());

            IsIndexed = true;
        }

        private static HashSet<string> Process(string text)
        {
            return text
                .Split(spearators, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim(trim).ToLower())
                .Where(x =>
                {
                    if (stopWords.Contains(x))
                        return false;
                    if (x.Length >= 3)
                        return true;
                    if (x.Length == 0)
                        return false;
                    return char.IsDigit(x[0]);
                })
                .ToHashSet();
        }
    }
}
