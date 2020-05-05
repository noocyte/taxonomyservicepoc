using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TaxonomyServicePOC.Controllers
{
    public class Index
    {
        public long _index = 0;

        public ConcurrentDictionary<long, DocRef> docs = new ConcurrentDictionary<long, DocRef>();
        public ConcurrentDictionary<string, ConcurrentQueue<long>>[] fields = new ConcurrentDictionary<string, ConcurrentQueue<long>>[]
           {
                new ConcurrentDictionary<string, ConcurrentQueue<long>>(),
                new ConcurrentDictionary<string, ConcurrentQueue<long>>(),
                new ConcurrentDictionary<string, ConcurrentQueue<long>>(),
                new ConcurrentDictionary<string, ConcurrentQueue<long>>()
           };


    }
    public class DocRef
    {
        public string Id;
        public long Number;
        public Taxonomy Actual;
        public string TaxonomyName;
    }

    public enum Fields
    {
        Name,
        Id,
        ParentId,
        Full
    }

    public static class Indexer
    {

        public static Dictionary<string, Index> Indexes = new Dictionary<string, Index>();


        static char[] _seperators = new char[] { ' ', '\t', ',', '!', '\r', '(', ')', '?', '-', '"', '\n', '/' };
        static char[] _trim = new char[] { '.', };


        static HashSet<string> _stopWords = new HashSet<string>
        {
            "a", "an", "and", "are", "as", "at", "be", "but", "by",
            "for", "if", "in", "into", "is", "it",
            "no", "not", "of", "on", "or", "such",
            "that", "the", "their", "then", "there", "these",
            "they", "this", "to", "was", "will", "with", "you", "have", "has" ,
            "please"
        };

        public static IEnumerable<Taxonomy> Search(string taxonomyName, SearchParameters p)
        {
            if (!Indexes.TryGetValue(taxonomyName.ToLowerInvariant(), out var index))
            {
                yield break;
            }

            var pageSize = p.PageSize == 0 || p.PageSize > 500
            ? 25
            : p.PageSize;
            var counter = 0;

            index.fields[(int)Fields.Id].TryGetValue(p.Id, out var idMatches);
            foreach (var item in idMatches)
            {
                counter++;
                if (counter >= pageSize) yield break;
                yield return index.docs[item].Actual;
            }

            index.fields[(int)Fields.Name].TryGetValue(p.Query.ToLowerInvariant(), out var nameMatches);
            foreach (var item in nameMatches)
            {
                counter++;
                if (counter >= pageSize) yield break;
                yield return index.docs[item].Actual;
            }

            index.fields[(int)Fields.Full].TryGetValue(p.Query.ToLower(), out var fullMatches);
            foreach (var item in fullMatches)
            {
                counter++;
                if (counter >= pageSize) yield break;
                yield return index.docs[item].Actual;
            }
        }

        

        public static void IndexTaxonomy(string taxonomyName, Taxonomy tax)
        {
            var success = false;
            if (!Indexes.TryGetValue(taxonomyName, out var index))
            {
                index = new Index();
                Indexes[taxonomyName] = index;
            }

            do
            {
                var cur = Interlocked.Increment(ref index._index);

                index.fields[(int)Fields.Id].GetOrAdd(tax.Id, _ => new ConcurrentQueue<long>()).Enqueue(cur);
                index.fields[(int)Fields.ParentId].GetOrAdd(tax.ParentId, _ => new ConcurrentQueue<long>()).Enqueue(cur);

                foreach (var item in Process(tax.Name))
                {
                    index.fields[(int)Fields.Name]
                        .GetOrAdd(item, _ => new ConcurrentQueue<long>()).Enqueue(cur);
                }

                foreach (var item in Process(string.Join(' ', tax.Select(t => t.Value))))
                {
                    index.fields[(int)Fields.Full]
                        .GetOrAdd(item, _ => new ConcurrentQueue<long>()).Enqueue(cur);
                }

                success = index.docs.TryAdd(cur, new DocRef
                {
                    Id = tax.Id,
                    Number = cur,
                    Actual = tax
                });

            } while (!success);
        }

        private static HashSet<string> Process(string text)
        {
            return text
                .Split(_seperators, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim(_trim).ToLower())
                .Where(x =>
                {
                    if (_stopWords.Contains(x))
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
