using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab.ClientModels;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    internal static class BundledPatchNotes
    {
        internal static TitleNewsFeed Load(string version)
        {
            if (!Version.TryParse(version, out _)) return null;
            var asset = Resources.Load<TextAsset>("PatchNotes/" + version);
            return asset != null ? Parse(asset.text, version) : null;
        }

        internal static List<TitleNewsFeed> MergeBoard(List<TitleNewsFeed> online, string version)
        {
            var board = new List<TitleNewsFeed>(online);
            if (!Version.TryParse(version, out var installed)) return board;
            var known = new HashSet<string>();
            foreach (var article in online)
                if (article.AppVersion != null) known.Add(article.AppVersion);
            foreach (var asset in Resources.LoadAll<TextAsset>("PatchNotes"))
            {
                if (!Version.TryParse(asset.name, out var release) || release.CompareTo(installed) > 0 || !known.Add(asset.name)) continue;
                var article = Parse(asset.text, asset.name);
                if (article != null) board.Add(article);
            }
            board.Sort((a, b) => {
                int byTime = b.Timestamp.CompareTo(a.Timestamp);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.Id, b.Id);
            });
            return board;
        }

        internal static TitleNewsFeed Parse(string json, string version)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var body = JObject.Parse(json);
                if (body["title"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)body["title"])) return null;
                var stamp = body["publishedUtc"]?.ToObject<DateTime?>();
                return TitleNewsFeed.LatestPatch(TitleNewsFeed.Read(new List<TitleNewsItem>
                {
                    new() { NewsId = "bundled-patch-" + version, Title = (string)body["title"], Body = json, Timestamp = stamp?.ToUniversalTime() ?? DateTime.MinValue }
                }), version);
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }
    }
}
