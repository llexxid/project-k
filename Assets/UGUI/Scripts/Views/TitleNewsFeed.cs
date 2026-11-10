using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab.ClientModels;

namespace KingdomIdle.UGUI
{
    internal sealed class TitleNewsFeed
    {
        internal readonly string Id;
        internal readonly string Title;
        internal readonly DateTime Timestamp;
        internal readonly string Text;
        internal readonly string AppVersion;

        private TitleNewsFeed(TitleNewsItem item, string text, string appVersion)
        {
            Id = item.NewsId;
            Title = item.Title;
            Timestamp = item.Timestamp;
            Text = text;
            AppVersion = appVersion;
        }

        internal static List<TitleNewsFeed> Read(List<TitleNewsItem> items)
        {
            var articles = new List<TitleNewsFeed>();
            if (items == null) return articles;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.NewsId) || string.IsNullOrWhiteSpace(item.Title) ||
                    string.IsNullOrWhiteSpace(item.Body)) continue;

                try
                {
                    using var reader = new JsonTextReader(new StringReader(item.Body)) { DateParseHandling = DateParseHandling.None };
                    var body = JObject.Load(reader);
                    if (reader.Read() || body["text"]?.Type != JTokenType.String) continue;
                    string text = (string)body["text"];
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var version = body["appVersion"];
                    if (version != null && (version.Type != JTokenType.String || !Version.TryParse((string)version, out _))) continue;
                    articles.Add(new TitleNewsFeed(item, text, (string)version));
                }
                catch (JsonReaderException)
                {
                    // A malformed post must not hide the rest of the board.
                }
            }

            articles.Sort((a, b) =>
            {
                int dateOrder = b.Timestamp.CompareTo(a.Timestamp);
                return dateOrder != 0 ? dateOrder : string.CompareOrdinal(a.Id, b.Id);
            });
            return articles;
        }

        internal static TitleNewsFeed LatestPatch(List<TitleNewsFeed> articles, string version) =>
            articles.Find(article => string.Equals(article.AppVersion, version, StringComparison.Ordinal));
    }
}
