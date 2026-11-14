using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;

namespace KingdomIdle.UGUI.Editor
{
    public static class AuthoredGlyphAcceptance
    {
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException("Authored glyph acceptance: " + message);
                checks.Add(message);
            }
            var json = MagePolishPreparation.CollectJsonCharacters("{\"text\":\"\\uAE50\\uD76C\",\"rows\":[\"\\uC5D8\",42,false,null]}");
            Check(json.Contains(0xAE50u) && json.Contains(0xD76Cu) && json.Contains(0xC5D8u) && !json.Contains((uint)'x'),
                "JSON string values decode escapes and ignore property names");
            Check(MagePolishPreparation.CollectJsonCharacters("\"\\uD83D\\uDE00\"").SequenceEqual(new[] { 0x1F600u }),
                "A supplementary character is collected as one Unicode scalar");
            bool rejected = false;
            try { MagePolishPreparation.CollectJsonCharacters("{"); }
            catch (Newtonsoft.Json.JsonException) { rejected = true; }
            Check(rejected, "Malformed authored JSON cannot silently pass the glyph collection");
            rejected = false;
            try { MagePolishPreparation.RequireAuthoredHangul(new[] { 0xAE50u }); }
            catch (InvalidOperationException error) { rejected = error.Message.Contains("U+AE50") && error.Message.Contains("Bake Authored Korean Glyphs"); }
            Check(rejected, "The build validation rejects an unbaked authored Hangul character with an actionable error");
            MagePolishPreparation.RequireAuthoredHangul(new[] { 0x1F600u });
            Check(true, "Optional decorative fallback does not turn into a new mandatory Hangul requirement");
            var authored = MagePolishPreparation.CollectAuthoredCharacters();
            Check(authored.Contains(0xAE50u) && authored.Contains(0xD76Cu) && authored.Contains(0xC5D8u),
                "The shared collection includes bundled notes and balance JSON Hangul");
            MagePolishPreparation.ValidateAuthoredGlyphs();
            Check(true, "Current authored UI Hangul is already baked before the build");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UGUI/Art/Font/Galmuri11 SDF.asset");
            Check(font.atlasPopulationMode == AtlasPopulationMode.Dynamic && font.sourceFontFile != null,
                "Dynamic characters from future notices and player names remain supported");
            return new { passed = checks.Count, checks };
        }
    }
}
