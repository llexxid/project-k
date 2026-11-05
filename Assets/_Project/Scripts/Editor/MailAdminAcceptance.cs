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
            const long now = 1793400000;
            MailAdminWindow.Draft Draft() => new() { Target = "TEST_PLAYER", StartKst = MailAdminWindow.FormatKst(now), EndKst = MailAdminWindow.FormatKst(now + 3600) };
            var draft = Draft();
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
                var invalid = Draft(); mutate(invalid);
                bool rejected = false; try { MailAdminWindow.Prepare("", invalid, now); } catch (ArgumentException) { rejected = true; }
                Check(rejected, label);
            }
            Reject(d => d.Target = "", "missing target rejected");
            Reject(d => d.Target = "A/B", "invalid target rejected");
            Reject(d => d.Gold = 0, "nonpositive gold rejected");
            Reject(d => d.ValidDays = -1, "negative validity rejected");
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
            Reject(d => d.RequestId = "unsafe/id", "invalid operator request ID rejected");
            Reject(d => d.Reason = "", "reason required");
            Reject(d => d.Target = string.Join("\n", Enumerable.Range(0, 101).Select(i => "P" + i)), "101 targets rejected");
            Reject(d => d.EndKst = MailAdminWindow.FormatKst(now), "elapsed end rejected");
            Reject(d => d.EndKst = "2026/11/06", "ambiguous time format rejected");
            Reject(d => { d.AllPlayers = true; d.StartKst = d.EndKst; }, "broadcast requires start before end");
            Reject(d => { d.UseKeys = true; d.TitleKey = ""; }, "Key mode requires both keys");
            var multiple = Draft(); multiple.Target = "a\nA,b;C";
            var targets = JObject.Parse(MailAdminWindow.Prepare("", multiple, now))["TargetPlayFabIds"].ToObject<string[]>();
            Check(targets.SequenceEqual(new[] { "A", "B", "C" }), "multiple targets normalized and deduplicated");
            multiple.Target = string.Join("\n", Enumerable.Range(0, 100).Select(i => "P" + i));
            Check(JObject.Parse(MailAdminWindow.Prepare("", multiple, now))["TargetPlayFabIds"].Count() == 100, "100 targets supported");
            var all = Draft(); all.AllPlayers = true; all.UseKeys = true; all.RequestId = "cbt-start-2026"; all.ValidDays = 0;
            var broadcast = JObject.Parse(MailAdminWindow.Prepare("", all, now));
            Check(broadcast.Value<string>("RequestId") == "cbt-start-2026" && broadcast.Value<int>("TargetType") == 2 && broadcast["TargetPlayFabIds"].Type == JTokenType.Null &&
                broadcast.Value<long>("DeliveryStartUtc") == now && broadcast["ValidDays"].Type == JTokenType.Null, "stable CBT ID and broadcast window with default validity");
            Check(broadcast.Value<int>("TextMode") == 0 && broadcast.Value<string>("TitleKey") == "mail.cbt.title" && broadcast["LocalizedTexts"].Type == JTokenType.Null &&
                broadcast["DefaultLocale"].Type == JTokenType.Null, "CBT keys exclude Text originals");
            var restoredAll = MailAdminWindow.Draft.Restore(broadcast.ToString());
            Check(restoredAll.AllPlayers && restoredAll.UseKeys && restoredAll.ValidDays == 0 && restoredAll.StartKst == all.StartKst && restoredAll.Target == "", "broadcast and Key drafts restore exactly");
            Check(MailAdminWindow.ParseKst("2026-11-06 09:00:00") == DateTimeOffset.Parse("2026-11-06T00:00:00Z").ToUnixTimeSeconds(), "KST conversion independent of machine time zone");
            Check(MailAdminWindow.Describe(200, Response(",\"StoppedUtc\":1793400010,\"Results\":[]"), id, true).Contains("중지 상태"), "stopped campaign remains visible with no recipients");
            Check(MailAdminWindow.Describe(200, Response().Replace("\"AlreadyRegistered\":false", "\"AlreadyRegistered\":true,\"StoppedUtc\":1793400010"), id, false).Contains("재개하지"), "stopped requests cannot be reactivated by duplicate registration");
            var historyBody = new JObject { ["HistoryItems"] = new JArray(new JObject { ["Request"] = broadcast, ["AcceptedUtc"] = now, ["StoppedUtc"] = null }), ["NextCursor"] = "opaque-page-token" };
            Check(MailAdminWindow.ReadHistory(historyBody).Count == 1 && MailAdminWindow.ReadCursor(historyBody) == "opaque-page-token", "history pages retain original request and opaque continuation");
            bool invalidHistory = false;
            try { MailAdminWindow.ReadHistory(new JObject { ["HistoryItems"] = new JArray(new JObject { ["Request"] = new JObject(), ["AcceptedUtc"] = now }) }); }
            catch (FormatException) { invalidHistory = true; }
            Check(invalidHistory, "malformed history items rejected");
            string previous = SessionState.GetString(MailAdminWindow.PendingKey, "");
            var window = ScriptableObject.CreateInstance<MailAdminWindow>();
            try
            {
                SessionState.SetString(MailAdminWindow.PendingKey, prepared);
                window.ShowUtility();
                window.CreateGUI();
                var fields = window.rootVisualElement.Query<TextField>().ToList();
                Check(fields.Single(f => f.label == "PlayFabId (줄바꿈 구분)").value == "TEST_PLAYER" &&
                    window.rootVisualElement.Q<LongField>().value == 100 && fields.Single(f => f.label == "한국어 제목").value == body["LocalizedTexts"]["ko"].Value<string>("Title"),
                    "reopened form displays the actual fixed target, reward and originals");
                var passwords = fields.Where(f => f.isPasswordField).ToList();
                Check(passwords.Count == 2 && passwords.All(f => string.IsNullOrEmpty(f.value) && string.IsNullOrEmpty(f.viewDataKey)), "restored window never restores credentials");
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(MailAdminWindow).GetMethod("SelectHistory", flags).Invoke(window, new object[] { historyBody["HistoryItems"][0] });
                Check(fields.Single(f => f.label == "조회·중지할 요청 ID").value == "cbt-start-2026" &&
                    fields.Single(f => f.label == "선택한 발송 내용").value.Contains("mail.cbt.title"), "selecting history shows the original request independent of the pending send");
                var lookup = fields.Single(f => f.label == "조회·중지할 요청 ID");
                lookup.value = "ANOTHER";
                Check(!window.rootVisualElement.Query<Button>().ToList().Single(b => b.text == "선택한 발송의 신규 전달 중지").enabledInHierarchy,
                    "changing lookup ID clears stale stop eligibility and page state");
                Check(string.IsNullOrEmpty(fields.Single(f => f.label == "선택한 발송 내용").value), "changing lookup ID clears previous campaign preview");
                passwords.ForEach(f => f.value = "qa-only-secret");
                Check(!EditorJsonUtility.ToJson(window).Contains("qa-only-secret") && !SessionState.GetString(MailAdminWindow.PendingKey, "").Contains("qa-only-secret"),
                    "credentials excluded from window serialization and pending snapshot");
                using (var inFlight = new UnityWebRequest())
                {
                    typeof(MailAdminWindow).GetField("_request", flags).SetValue(window, inFlight);
                    typeof(MailAdminWindow).GetMethod("UpdateControls", flags).Invoke(window, null);
                    Check(window.rootVisualElement.Query<Button>().ToList().All(b => !b.enabledInHierarchy), "in-flight request blocks send, query, new mail and draft changes");
                    typeof(MailAdminWindow).GetField("_request", flags).SetValue(window, null);
                }
                Check(SessionState.GetString(MailAdminWindow.PendingKey, "") == prepared, "opening and inspecting a pending window never replaces its request");
                typeof(MailAdminWindow).GetMethod("NewMail", flags).Invoke(window, null);
                Check(string.IsNullOrEmpty(fields.Single(f => f.label == "요청 ID (빈 값은 자동)").value) && string.IsNullOrEmpty(SessionState.GetString(MailAdminWindow.PendingKey, "")),
                    "explicit new mail clears a restored request ID instead of reusing the previous campaign");
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
