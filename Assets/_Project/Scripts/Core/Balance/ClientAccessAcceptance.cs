#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Scripts.Core;

namespace KingdomIdle.Balance
{
    public static class ClientAccessAcceptance
    {
        public static List<string> Run()
        {
            var checks = new List<string>();
            void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
            var data = new JObject { ["minimumVersion"] = "0.10.0", ["maintenance"] = false, ["updateUrl"] = "https://example.invalid/update" };
            ClientAccessPolicy Policy() => ClientAccessPolicy.Parse(data.ToString());
            Check(Policy().Evaluate("0.9.0") == ClientAccessState.UpdateRequired, "Numeric minor versions reject an old client");
            Check(Policy().Evaluate("0.10.0") == ClientAccessState.Allowed && Policy().Evaluate("0.11.0") == ClientAccessState.Allowed,
                "Equal and newer versions can enter");
            data["minimumVersion"] = "1.2.10";
            Check(Policy().Evaluate("1.2.9") == ClientAccessState.UpdateRequired && Policy().Evaluate("2.0.0") == ClientAccessState.Allowed,
                "Patch and major boundaries compare numerically");
            data["maintenance"] = true;
            data["message"] = "서버 점검 중입니다.";
            Check(Policy().Evaluate("0.9.0") == ClientAccessState.Maintenance && Policy().Message == "서버 점검 중입니다.",
                "Maintenance takes priority and preserves the operator message");
            data["maintenance"] = false;
            foreach (var version in new[] { "0.10", "0.10.0.1", "0.10.0-beta", "00.10.0", "-1.0.0", "2147483648.0.0", "" })
            {
                data["minimumVersion"] = version;
                Reject(data.ToString(), "Malformed version is rejected: " + version);
            }
            data["minimumVersion"] = "0.10.0";
            foreach (var url in new[] { "http://example.invalid", "javascript:alert(1)", "file:///tmp/update", "https://user:password@example.invalid", "/update" })
            {
                data["updateUrl"] = url;
                Reject(data.ToString(), "Unsafe update URL is rejected");
            }
            data["updateUrl"] = "";
            Check(Policy().Evaluate("0.10.0") == ClientAccessState.Allowed, "No update URL is needed for a supported client");
            bool missingUrlRejected = false;
            try { Policy().Evaluate("0.9.0"); } catch (FormatException) { missingUrlRejected = true; }
            Check(missingUrlRejected, "An old client cannot continue when the update URL is missing");
            Reject("{}", "An empty registered policy is invalid");
            Reject("{\"minimumVersion\":\"0.10.0\",\"maintenance\":\"false\"}", "String booleans cannot disable maintenance");
            Reject("{\"minimumVersion\":\"0.10.0\",\"maintenance\":true,\"maintenance\":false}", "Duplicate policy fields are rejected");
            Reject("not json", "Malformed JSON is rejected");
            return checks;

            void Reject(string json, string message)
            {
                bool rejected = false;
                try { ClientAccessPolicy.Parse(json); }
                catch (Exception ex) when (ex is FormatException || ex is Newtonsoft.Json.JsonException) { rejected = true; }
                Check(rejected, message);
            }
        }
    }
}
#endif
