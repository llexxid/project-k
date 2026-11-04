#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class RubyServerAcceptance
    {
        // 저장·응답 적용만 검사하며 인증이나 서버 요청은 실행하지 않는다.
        public static List<string> Run()
        {
            var checks = new List<string>();
            void Check(bool result, string name)
            { if (!result) throw new InvalidOperationException("Ruby server acceptance: " + name); checks.Add(name); }
            RubyGrowthStateDTO Growth(long sequence, int gold = 0, long spent = 0, int best = 0, string day = "") =>
                new() { NextRequestSequence = sequence, GoldLevel = gold, GoldSpent = spent, BestGoldLevel = best, LastResetDay = day };

            var legacy = new ProgressionState { RubyGoldLevel = 3, RubyGoldSpent = 65, BestRubyGoldLevel = 3, PendingRubyReset = 1 };
            legacy.Wallet[eCurrency.Ruby] = 100;
            Check(RubyProgression.ApplyServerState(legacy, null) && legacy.RubyGoldLevel == 3 &&
                legacy.PendingRubyReset == 1 && legacy.Modules.Count == 0, "Old server preserves local growth and queued legacy intent");
            Check(RubyProgression.ApplyServerState(legacy, Growth(1)) && legacy.RubyGoldLevel == 0 &&
                legacy.RubyGoldSpent == 0 && legacy.PendingRubyReset == 0, "Server snapshot replaces growth and retires local reset intent");
            string archive = legacy.Modules["legacy-ruby-growth"];
            var original = JObject.Parse(archive);
            Check((int)original["RubyGoldLevel"] == 3 && (long)original["RubyGoldSpent"] == 65 &&
                (long)original["Ruby"] == 100, "Legacy growth and balance remain archived before server replacement");
            Check(RubyProgression.ApplyServerState(legacy, Growth(2, 1, 20, 1)) &&
                legacy.Modules["legacy-ruby-growth"] == archive, "Later snapshots never overwrite the legacy archive");
            Check(!RubyProgression.ApplyServerState(legacy, Growth(0)) && legacy.RubyGoldLevel == 1 &&
                legacy.RubyNextRequestSequence == 2, "Malformed growth cannot mutate a draft");
            float previousTimeScale = Time.timeScale;
            var previousStageManager = StageManager.Instance;
            var previousGameManager = GameManager.Instance;
            var pausedStage = new GameObject("RubyPauseAcceptance");
            pausedStage.SetActive(false);
            try
            {
                StageManager.Instance = pausedStage.AddComponent<StageManager>();
                GameManager.Instance = null;
                typeof(StageManager).GetField("_currentState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(StageManager.Instance, eStageRunState.Running);
                // pause 가드가 회귀해도 정산은 즉시 거절되어 실제 API에 도달하지 않는다.
                typeof(StageManager).GetField("_huntSyncFailed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(StageManager.Instance, true);
                Time.timeScale = 0f;
                int revision = RubyProgression.Revision;
                bool sent = false;
                var settle = typeof(RubyProgression).GetMethod("SettleBeforeRequest", BindingFlags.Static | BindingFlags.NonPublic);
                bool accepted = (bool)settle.Invoke(null, new object[] { (Action)(() => sent = true) });
                Check(!accepted && !sent && RubyProgression.Revision == revision,
                    "Another paused transaction is rejected before ruby settlement starts");
            }
            finally
            {
                StageManager.Instance = previousStageManager;
                GameManager.Instance = previousGameManager;
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(pausedStage);
            }

            using (LocalProgression.BeginTestSession())
            {
                string account = "ruby-server-" + Guid.NewGuid().ToString("N");
                LocalProgression.OpenTestAccount(account);
                Check(LocalProgression.Execute("qa-ruby-server-setup", s =>
                {
                    s.Wallet[eCurrency.Ruby] = 100;
                    s.HighestMainClear = 0x200020005;
                    return RubyProgression.ApplyServerState(s, Growth(1));
                }), "Ruby server test account initialized");
                Check(!RubyProgression.Enhance(false) && LocalProgression.Balance(eCurrency.Ruby) == 100,
                    "A stored server snapshot alone cannot authorize purchases");
                Check(LocalProgression.Execute("qa-ruby-request", s => RubyProgression.PrepareRequest(s, false, false)) &&
                    LocalProgression.Balance(eCurrency.Ruby) == 100 && LocalProgression.State.RubyGoldLevel == 0,
                    "Persisting a request never spends ruby or grants growth");
                string id = LocalProgression.State.PendingRubyRequestId;
                LocalProgression.OpenTestAccount(account);
                var restored = JsonConvert.DeserializeObject<ProgressionState>(JsonConvert.SerializeObject(LocalProgression.State));
                restored.RubyGoldLevel = 4;
                Check(RubyProgression.PrepareRequest(restored, false, false) && restored.PendingRubyRequestId == id &&
                    restored.PendingRubyExpectedLevel == 0, "Retry after reopening keeps ID and original expected level");
                Check(!RubyProgression.PrepareRequest(restored, true, false) &&
                    !RubyProgression.PrepareRequest(restored, false, true), "An unresolved request cannot become another action or track");
                var response = new OnEnchantRubyResponseDTO
                { RequestId = id, AlreadyApplied = true, CurrentRuby = 80, SpentRuby = 20, RubyGrowthState = Growth(2, 1, 20, 1) };
                Check(!LocalProgression.TestFailedWrite(s => RubyProgression.ApplyResponse(s, response, response.CurrentRuby)) &&
                    LocalProgression.State.PendingRubyRequestId == id && LocalProgression.Balance(eCurrency.Ruby) == 100,
                    "Failed durable result storage preserves the original request and balance");
                Check(LocalProgression.Execute("qa-ruby-result", s => RubyProgression.ApplyResponse(s, response, response.CurrentRuby)) &&
                    LocalProgression.Balance(eCurrency.Ruby) == 80 && LocalProgression.State.RubyGoldLevel == 1 &&
                    string.IsNullOrEmpty(LocalProgression.State.PendingRubyRequestId), "Replayed success applies the absolute balance once");
                Check(!LocalProgression.Execute("qa-ruby-duplicate", s => RubyProgression.ApplyResponse(s, response, response.CurrentRuby)) &&
                    LocalProgression.Balance(eCurrency.Ruby) == 80, "A response without its pending request cannot spend twice");

                Check(LocalProgression.Execute("qa-ruby-reset-request", s => RubyProgression.PrepareRequest(s, true, false)), "Reset request persisted");
                id = LocalProgression.State.PendingRubyRequestId;
                var pending = LocalProgression.State.DeepClone();
                Check(RubyProgression.ApplyServerState(pending, Growth(2, 1, 20, 1)) && pending.PendingRubyRequestId == id,
                    "An unchanged server sequence does not discard an uncertain request");
                var reset = new OnResetRubyEnhancementResponseDTO
                { RequestId = id, CurrentRuby = 96, RefundedRuby = 16, RubyGrowthState = Growth(3, 0, 0, 1, "2026-11-04") };
                var wrongId = new OnResetRubyEnhancementResponseDTO
                { RequestId = "2:" + Guid.NewGuid().ToString("N"), CurrentRuby = 96, RubyGrowthState = reset.RubyGrowthState };
                Check(!RubyProgression.ApplyResponse(pending, wrongId, 96) && pending.PendingRubyRequestId == id &&
                    pending.Wallet[eCurrency.Ruby] == 80, "A different response ID cannot replace the pending reset");
                Check(LocalProgression.Execute("qa-ruby-reset-result", s => RubyProgression.ApplyResponse(s, reset, reset.CurrentRuby)) &&
                    LocalProgression.Balance(eCurrency.Ruby) == 96 && LocalProgression.State.RubyGoldLevel == 0 &&
                    LocalProgression.State.BestRubyGoldLevel == 1 && LocalProgression.State.RubyResetDay == "2026-11-04",
                    "Reset uses the server refund balance and preserves server best level and day");
                Check(RubyProgression.ApplyServerState(pending, reset.RubyGrowthState) && string.IsNullOrEmpty(pending.PendingRubyRequestId),
                    "A consumed sequence resolves an uncertain request from a fresh snapshot");
                Check(!RubyProgression.PrepareRequest(pending, true, false, id) && string.IsNullOrEmpty(pending.PendingRubyRequestId),
                    "A retry resolved during settlement cannot generate a replacement request ID");
                RubyProgression.ReceiveServerState(true);
                LocalProgression.OpenTestAccount("ruby-other-" + Guid.NewGuid().ToString("N"));
                Check(!RubyProgression.Available, "Changing accounts invalidates runtime server capability");
            }
            return checks;
        }
    }
}
#endif
