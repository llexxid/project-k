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

        internal static TitleNewsFeed Parse(string json, string version)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var body = JObject.Parse(json);
                if (body["title"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)body["title"])) return null;
                return TitleNewsFeed.LatestPatch(TitleNewsFeed.Read(new List<TitleNewsItem>
                {
                    new() { NewsId = "bundled-patch-" + version, Title = (string)body["title"], Body = json }
                }), version);
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }
    }
}
