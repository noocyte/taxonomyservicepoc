using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaxonomyServicePOC
{
    public interface IDatabase
    {
        Taxonomy GetById(string taxonomyName, string id);
        IEnumerable<Taxonomy> GetAll(string taxonomyName);
        IEnumerable<Taxonomy> GetByParentId(string taxonomyName, string parentId);
        IEnumerable<Taxonomy> GetByPredicate(string taxonomyName, Predicate<Taxonomy> predicate);
        void InsertOrReplace(string taxonomyName, Taxonomy obj);
    }

    public class Database : IDatabase
    {
        private static readonly Dictionary<string, List<Taxonomy>> _db = new Dictionary<string, List<Taxonomy>>(100);

        public IEnumerable<Taxonomy> GetAll(string taxonomyName)
        {
            EnsureNameExists(taxonomyName);
            return _db[taxonomyName];
        }

        public Taxonomy GetById(string taxonomyName, string id)
        {
            EnsureNameExists(taxonomyName);

            var taxonomyList = _db[taxonomyName];
            var index = taxonomyList.FindIndex(t => t.Id == id);
            if (index < 0)
                throw new TaxonomyNotFoundException();

            return taxonomyList[index];
        }

        public IEnumerable<Taxonomy> GetByParentId(string taxonomyName, string parentId)
            => GetByPredicate(taxonomyName, t => t.ParentId == parentId);

        public IEnumerable<Taxonomy> GetByPredicate(string taxonomyName, Predicate<Taxonomy> predicate)
        {
            EnsureNameExists(taxonomyName);

            var taxonomyList = _db[taxonomyName];
            return taxonomyList
                .Where(t => predicate(t))
                .OrderBy(t => t.Sequence);
        }

        public void InsertOrReplace(string taxonomyName, Taxonomy obj)
        {
            // very naive implementation - should perhaps merge with existing? Perhaps generate sequence? etc.
            EnsureNameExists(taxonomyName);

            var taxonomyList = _db[taxonomyName];
            var index = taxonomyList.FindIndex(t => t.Id == obj.Id);
            if (index >= 0)
                taxonomyList.RemoveAt(index);

            taxonomyList.Add(obj);
        }

        private void EnsureNameExists(string taxonomyName)
        {
            if (_db.ContainsKey(taxonomyName)) return;

            _db[taxonomyName] = new List<Taxonomy>();
        }

    }

    public class TaxonomyJsonConverter : JsonConverter<Taxonomy>
    {
        public override Taxonomy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dictionary = new Taxonomy();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return dictionary;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                var propertyName = reader.GetString();

                var element = (JsonElement)JsonSerializer.Deserialize(ref reader, typeof(object));
                object actual;
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        actual = element.GetString();
                        break;
                    case JsonValueKind.Number:
                        actual = element.GetUInt64();
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        actual = element.GetBoolean();
                        break;
                    default:
                        actual = null;
                        break;
                }

                // Add to dictionary.
                dictionary.Add(propertyName, actual);
            }

            return dictionary;
        }

        public override void Write(Utf8JsonWriter writer, Taxonomy value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var item in value)
            {
                writer.WritePropertyName(item.Key.ToLowerInvariant());
                JsonSerializer.Serialize(writer, item.Value);
            }

            writer.WriteEndObject();
        }
    }

    public class TaxonomyNotFoundException : Exception { }

    [JsonConverter(typeof(TaxonomyJsonConverter))]
    public class Taxonomy : Dictionary<string, object>
    {
        public string Id
        {
            get { return this.GetStringOrDefault(Constants.IdKey); }
            set { this[Constants.IdKey] = value; }
        }

        public string ParentId
        {
            get { return this.GetStringOrDefault(Constants.ParentIdKey); }
            set { this[Constants.ParentIdKey] = value; }
        }

        public long Sequence
        {
            get { return this.GetLongOrDefault(Constants.SequenceKey); }
            set { this[Constants.SequenceKey] = value; }
        }

        public bool IsDisabled
        {
            get { return this.GetBoolOrDefault(Constants.DisabledKey); }
            set { this[Constants.DisabledKey] = value; }
        }

        public new object this[string key]
        {
            get { return this[key.ToLowerInvariant()]; }
            set { this[key.ToLowerInvariant()] = value; }
        }
    }

    public static class TaxonomyExtensions
    {
        public static string GetStringOrDefault(this Taxonomy values, string key, string defaultValue = "")
        {
            if (values.TryGetValue(key.ToLowerInvariant(), out var value))
                return value.ToString();
            return defaultValue;
        }

        public static bool GetBoolOrDefault(this Taxonomy values, string key, bool defaultValue = false)
        {
            var lowerKey = key.ToLowerInvariant();
            if (!values.ContainsKey(lowerKey)) return defaultValue;
            var value = values[lowerKey];

            if (value is bool) return (bool)value;
            return defaultValue;
        }

        public static long GetLongOrDefault(this Taxonomy values, string key, long defaultValue = 0)
        {
            var lowerKey = key.ToLowerInvariant();

            if (!values.ContainsKey(lowerKey))
                return defaultValue;

            var value = values[lowerKey];

            if (value is int)
                return (int)values[lowerKey];

            if (value is long)
                return (long)values[lowerKey];

            if (value is string)
            {
                if (long.TryParse(value.ToString(), out long parsedValue))
                    return parsedValue;
            }

            return defaultValue;
        }
    }

    public static class Constants
    {
        public static readonly string IdKey = "id";
        public static readonly string ParentIdKey = "parentid";
        public static readonly string SequenceKey = "sequence";
        public static readonly string DisabledKey = "disabled";
    }
}
