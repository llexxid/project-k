#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace KingdomIdle.Editor
{
    public static class MailAdminAcceptance
    {
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool value, string label) { if (!value) throw new InvalidOperationException("MAIL ADMIN ASSERT: " + label); checks.Add(label); }
            var draft = new MailAdminWindow.Draft { Target = "TEST_PLAYER" };
            const long now = 1793400000;
            string prepared = MailAdminWindow.Prepare("", draft, now);
            var body = JObject.Parse(prepared);
            Check(body.Value<int>("ProtocolVersion") == 2 && body.Value<int>("TargetType") == 1 && body["TargetPlayFabIds"].ToObject<string[]>().Length == 1,
                "only a specified single player is targeted");
            Check(body.Value<int>("TextMode") == 1 && body["TitleKey"].Type == JTokenType.Null && body["LocalizedTexts"]["ko"] != null && body["LocalizedTexts"]["en"] != null,
                "localized Text mode matches server contract");
            Check(body["Rewards"][0].Value<int>("Kind") == 2 && body["Rewards"][0].Value<int>("CurrencyType") == 0 && body["Rewards"][0].Value<long>("Amount") == 100 &&
                body.Value<long>("DeliveryEndUtc") == now + 3600 && body.Value<int>("ValidDays") == 1, "gold and independent deadlines match server contract");
            draft.Target = "OTHER"; draft.Gold = 200;
            Check(MailAdminWindow.Prepare(prepared, draft, now + 3600) == prepared, "retry preserves request ID, payload and deadline despite edited draft");
            Check(MailAdminWindow.Prepare("", draft, now) != prepared, "explicit new draft creates a distinct request");
            void Reject(Action<MailAdminWindow.Draft> mutate, string label)
            {
                var invalid = new MailAdminWindow.Draft { Target = "TEST_PLAYER" }; mutate(invalid);
                bool rejected = false; try { MailAdminWindow.Prepare("", invalid, now); } catch (ArgumentException) { rejected = true; }
                Check(rejected, label);
            }
            Reject(d => d.Target = "", "missing target rejected");
            Reject(d => d.Target = "A\nB", "invalid target rejected");
            Reject(d => d.Gold = 0, "nonpositive gold rejected");
            Reject(d => d.ValidDays = 0, "nonpositive validity rejected");
            Reject(d => d.TitleKo = new string('가', 86), "UTF8 title byte limit enforced");
            Reject(d => d.BodyKo = new string('x', 2049), "body byte limit enforced");
            Reject(d => d.TitleEn = "", "partially configured English rejected");
            draft.TitleEn = draft.BodyEn = "";
            Check(JObject.Parse(MailAdminWindow.Prepare("", draft, now))["LocalizedTexts"]["en"] == null, "Korean fallback can be used without English originals");
            Check(!MailAdminWindow.ValidKey("") && !MailAdminWindow.ValidKey("secret\r\nheader") && MailAdminWindow.ValidKey("sample-key_123="), "credential header validation rejects control characters");
            string id = body.Value<string>("RequestId");
            string Response(string result = "") => "{\"ProtocolVersion\":2,\"ServerVersion\":1,\"RequestId\":\"" + id + "\"" +
                (result == "" ? ",\"AcceptedUtc\":1793400000,\"AlreadyRegistered\":false" : result) + "}";
            Check(MailAdminWindow.Describe(200, Response(), id, false).Contains("아직"), "registration is not shown as delivered");
            Check(MailAdminWindow.Describe(200, Response(",\"Results\":[]"), id, true).Contains("아직"), "empty results are pending");
            Check(MailAdminWindow.Describe(200, Response(",\"Results\":[{\"PlayFabId\":\"TEST_PLAYER\",\"Status\":2}]"), id, true).Contains("보상 수령 여부는 별도"), "sent is distinguished from reward claim");
            Check(MailAdminWindow.Describe(200, Response(",\"Results\":[{\"PlayFabId\":\"TEST_PLAYER\",\"Status\":4}]"), id, true).Contains("재발송하지"), "uncertain writes require operator verification");
            Check(MailAdminWindow.Describe(0, "", id, false).Contains("유지"), "transport failure preserves the existing request");
            Check(MailAdminWindow.Describe(401, "", id, false).Contains("키"), "empty unauthorized response explains credentials");
            bool mismatch = false; try { MailAdminWindow.Describe(200, Response(), "OTHER", false); } catch (FormatException) { mismatch = true; }
            Check(mismatch, "foreign response ID rejected");
            string previous = SessionState.GetString(MailAdminWindow.PendingKey, "");
            var window = ScriptableObject.CreateInstance<MailAdminWindow>();
            try
            {
                SessionState.SetString(MailAdminWindow.PendingKey, prepared);
                window.CreateGUI();
                var fields = window.rootVisualElement.Query<TextField>().ToList();
                Check(fields.Single(f => f.label == "대상 PlayFabId").value == "TEST_PLAYER" &&
                    window.rootVisualElement.Q<LongField>().value == 100 && fields.Single(f => f.label == "한국어 제목").value == body["LocalizedTexts"]["ko"].Value<string>("Title"),
                    "reopened form displays the actual fixed target, reward and originals");
                var passwords = fields.Where(f => f.isPasswordField).ToList();
                Check(passwords.Count == 2 && passwords.All(f => string.IsNullOrEmpty(f.value) && string.IsNullOrEmpty(f.viewDataKey)), "restored window never restores credentials");
                passwords.ForEach(f => f.value = "qa-only-secret");
                Check(!EditorJsonUtility.ToJson(window).Contains("qa-only-secret") && !SessionState.GetString(MailAdminWindow.PendingKey, "").Contains("qa-only-secret"),
                    "credentials excluded from window serialization and pending snapshot");
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                using (var inFlight = new UnityWebRequest())
                {
                    typeof(MailAdminWindow).GetField("_request", flags).SetValue(window, inFlight);
                    typeof(MailAdminWindow).GetMethod("UpdateControls", flags).Invoke(window, null);
                    Check(window.rootVisualElement.Query<Button>().ToList().All(b => !b.enabledInHierarchy), "in-flight request blocks send, query, new mail and draft changes");
                    typeof(MailAdminWindow).GetField("_request", flags).SetValue(window, null);
                }
                Check(SessionState.GetString(MailAdminWindow.PendingKey, "") == prepared, "opening and inspecting a pending window never replaces its request");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (string.IsNullOrEmpty(previous)) SessionState.EraseString(MailAdminWindow.PendingKey); else SessionState.SetString(MailAdminWindow.PendingKey, previous);
            }
            return new { passed = checks.Count, checks };
        }
    }
}
#endif
