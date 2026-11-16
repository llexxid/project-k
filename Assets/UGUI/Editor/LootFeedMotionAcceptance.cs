#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>실제 피드 프리팹의 모션·풀 재사용·큐 경계를 격리 표시 DTO로 검사한다.</summary>
    public static class LootFeedMotionAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            var cases = new List<Dictionary<string, object>>();
            var failures = new List<string>();
            void Check(bool valid, string label, string actual = null)
            {
                cases.Add(new Dictionary<string, object> { ["case"] = label, ["passed"] = valid, ["actual"] = actual });
                if (!valid) failures.Add(label);
            }
            Dictionary<string, object> Report() => new()
            {
                ["passed"] = cases.Count(row => (bool)row["passed"]), ["failures"] = failures, ["cases"] = cases,
                ["scope"] = "Actual prefab and isolated Fixture; Tick-driven display motion only. No profile, API, reward grant or ownership change."
            };
            if (!Application.isPlaying || UIManager.Instance != null || Scripts.Core.UserManager.Instance != null ||
                Scripts.Core.Manager.NetworkManager.Instance != null || EquipmentManager.Instance != null ||
                KingdomIdle.MageTower.MageTowerManager.Instance != null || LocalProgression.IsReady)
            { Check(false, "Requires empty PlayMode without an account or live managers"); return Report(); }
            void Scenario(string name, Action<LootFeedAcceptance.Fixture, Action<bool, string>> run)
            {
                try
                {
                    using var fixture = new LootFeedAcceptance.Fixture();
                    run(fixture, (valid, detail) => Check(valid, name + ": " + detail));
                }
                catch (Exception error) { Check(false, name + ": fixture or input route exception", error.ToString()); }
            }

            Scenario("Entry", (f, check) =>
            {
                var feed = f.Feed;
                check(feed.rows.Length == 3 && feed.rows.All(row => row.visual != null && row.visual != row.rect &&
                    row.visual.parent == row.rect && row.rect.GetComponent<RectMask2D>() != null) &&
                    feed.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
                    "three masked layout slots have independent visuals and preserve input pass-through");
                LootNotifications.Publish(Currency(eCurrency.Ruby, 10)); feed.Tick(0, false); feed.Tick(.07f, false);
                var row = Find(feed, eCurrency.Ruby);
                check(row.phase == LootFeedView.MotionPhase.Entering && row.group.alpha > 0 && row.group.alpha < 1 &&
                    row.visual.anchoredPosition.y < 0 && row.visual.anchoredPosition.y > -8,
                    "half-entry has an intermediate alpha and slide offset");
                feed.Tick(.15f, false);
                check(row.phase == LootFeedView.MotionPhase.Holding && Near(row.group.alpha, 1) && row.visual.anchoredPosition == Vector2.zero,
                    "entry settles into Holding with a complete visual pose");
            });
            Scenario("Full same-kind refresh", (f, check) =>
            {
                Fill(f); var feed = f.Feed; var row = Find(feed, eCurrency.Ruby); string old = row.quantity.text;
                int binds = feed.BindCount;
                LootNotifications.Publish(Currency(eCurrency.Ruby, 5)); feed.Tick(.21f, false); feed.Tick(.26f, false);
                check(feed.VisibleCount == 3 && LootNotifications.Count == 0 && feed.BindCount == binds &&
                    row.notification.Amount == 15 && row.quantity.text == old && row.phase == LootFeedView.MotionPhase.RefreshOut &&
                    row.group.alpha > 0 && row.group.alpha < 1 && row.visual.anchoredPosition.y > 0,
                    "full pool refresh keeps the old text while moving out and preserves the exact merged amount");
                feed.Tick(.32f, false); bool changed = row.quantity.text == "+" + NumberNotation.Format(15) && row.phase == LootFeedView.MotionPhase.RefreshIn;
                feed.Tick(.45f, false);
                check(changed && row.phase == LootFeedView.MotionPhase.Holding && Near(row.group.alpha, 1) &&
                    row.visual.anchoredPosition == Vector2.zero && row.elapsed >= .44f && row.holdSeconds <= LootFeedView.MaxHoldSeconds,
                    "new text moves in then settles without restarting lifetime");
            });
            Scenario("Rounded quantity refresh", (f, check) =>
            {
                NumberNotation.SetStyle(NumberStyle.Standard, false); Fill(f, 1000000);
                var row = Find(f.Feed, eCurrency.Ruby); string old = row.quantity.text;
                LootNotifications.Publish(Currency(eCurrency.Ruby, 1)); f.Feed.Tick(.21f, false);
                check(NumberNotation.Format(1000000) == NumberNotation.Format(1000001) && row.quantity.text == old &&
                    row.notification.Amount == 1000001 && row.phase == LootFeedView.MotionPhase.RefreshOut,
                    "an exact acquisition still refreshes when abbreviated text is unchanged");
            });
            Scenario("Full pool FIFO roll", (f, check) =>
            {
                Fill(f); var feed = f.Feed; var ids = Pool(feed); var first = Find(feed, eCurrency.Ruby); var second = Find(feed, eCurrency.AncientCoin);
                LootNotifications.Publish(Currency(eCurrency.TrainingTome, 9)); LootNotifications.Publish(Currency(eCurrency.KingdomSupply, 4));
                feed.Tick(.9f, false);
                check(feed.rows.All(row => !row.fading) && LootNotifications.Count == 2, "new kinds wait for the minimum readable age");
                feed.Tick(1.01f, false); feed.Tick(1.11f, false);
                check(first.fading && first.phase == LootFeedView.MotionPhase.Exiting && first.group.alpha > 0 && first.group.alpha < 1 &&
                    first.visual.anchoredPosition.y > 0 && LootNotifications.Count == 2,
                    "only one oldest row exits upward before any FIFO item is consumed");
                feed.Tick(1.22f, false);
                check(Has(feed, eCurrency.TrainingTome) && !Has(feed, eCurrency.KingdomSupply) && LootNotifications.Count == 1 &&
                    second.rect.anchoredPosition.y > -(LootFeedView.RowHeight + LootFeedView.RowGap) && second.rect.anchoredPosition.y <= 0 &&
                    Pool(feed).SequenceEqual(ids) && feed.VisibleCount == 3,
                    "first queued kind reuses a freed slot while the remaining rows reflow within the same three objects");
                feed.Tick(1.4f, false); feed.Tick(1.61f, false);
                check(Has(feed, eCurrency.TrainingTome) && Has(feed, eCurrency.KingdomSupply) && LootNotifications.Count == 0 && Pool(feed).SequenceEqual(ids),
                    "second queued kind follows the first without loss or extra row objects");
            });
            Scenario("Reflow separation", (f, check) =>
            {
                Fill(f); var feed = f.Feed;
                LootNotifications.Publish(Currency(eCurrency.TrainingTome, 9));
                feed.Tick(1.01f, false); feed.Tick(1.11f, false); feed.Tick(1.22f, false);
                var incoming = Find(feed, eCurrency.TrainingTome);
                check(incoming.enterDelay >= .15f && Near(incoming.group.alpha, 0) && LootNotifications.Count == 0,
                    "a consumed new row waits invisibly while the previous rows reflow");
                feed.Tick(1.30f, false);
                check(Near(incoming.group.alpha, 0), "a waiting row cannot cover the moving previous quantity");
                feed.Tick(1.40f, false);
                var visible = feed.rows.Where(row => row.occupied && row.group.alpha > .05f)
                    .OrderByDescending(row => row.rect.anchoredPosition.y).ToArray();
                check(visible.Length == 3 && visible.Zip(visible.Skip(1), (a, b) =>
                    a.rect.anchoredPosition.y - b.rect.anchoredPosition.y >= LootFeedView.RowHeight).All(valid => valid),
                    "different visible rows keep nonoverlapping slots in the middle of re-entry");
                feed.Tick(1.55f, false);
                check(incoming.phase == LootFeedView.MotionPhase.Holding && Near(incoming.group.alpha, 1) &&
                    incoming.visual.anchoredPosition == Vector2.zero, "a delayed row completes its full entry pose");
            });
            Scenario("Long overflow", (f, check) =>
            {
                Fill(f, long.MaxValue - 2); var feed = f.Feed;
                LootNotifications.Publish(Currency(eCurrency.Ruby, 3)); feed.Tick(.21f, false);
                check(Find(feed, eCurrency.Ruby).notification.Amount == long.MaxValue - 2 && LootNotifications.Count == 1,
                    "unmergeable overflow remains intact in the queue");
                feed.Tick(1.01f, false); feed.Tick(1.22f, false);
                check(Find(feed, eCurrency.Ruby).notification.Amount == 3 && LootNotifications.Count == 0,
                    "overflow is later shown separately rather than wrapped, saturated or dropped");
            });
            Scenario("Blocked resume", (f, check) =>
            {
                LootNotifications.Publish(Currency(eCurrency.Ruby, 1)); f.Feed.Tick(0, false); f.Feed.Tick(.05f, false);
                var row = Find(f.Feed, eCurrency.Ruby); float age = row.elapsed, phaseTime = row.phaseTime, alpha = row.group.alpha;
                var offset = row.visual.anchoredPosition;
                f.Feed.Tick(10, true); f.Feed.Tick(20, true); f.Feed.Tick(20.1f, false);
                check(Near(row.elapsed, age) && Near(row.phaseTime, phaseTime) && Near(row.group.alpha, alpha) &&
                    row.visual.anchoredPosition == offset && Near(f.Feed.group.alpha, 1),
                    "a covered entry resumes at the same phase and visual pose after a long gap");
            });
            Scenario("Hidden geometry", (f, check) =>
            {
                Fill(f); var feed = f.Feed; bool one = false;
                for (float height = 1400; height >= 800; height -= 10)
                { f.Resize(height); if (feed.RefreshPlacement() && feed.AvailableSlots == 1) { one = true; break; } }
                feed.Tick(.21f, false);
                var hidden = feed.rows.Where(row => row.occupied && !row.rect.gameObject.activeSelf).ToArray();
                var ages = hidden.Select(row => row.elapsed).ToArray(); var times = hidden.Select(row => row.phaseTime).ToArray();
                feed.Tick(1.11f, false); f.Resize(2316); feed.Tick(2.11f, false);
                check(one && hidden.Length == 2 && hidden.Select((row, i) => row.occupied && row.rect.gameObject.activeSelf &&
                    Near(row.elapsed, ages[i]) && Near(row.phaseTime, times[i]) && !row.fading).All(valid => valid),
                    "hidden unread rows freeze and resume without being retired or charged for invisible time");
            });
            Scenario("Disable resume", (f, check) =>
            {
                // Fixture는 수동 Tick을 위해 컴포넌트를 끈다. 이 경우만 실제 활성 수명주기를 검사한다.
                f.Feed.enabled = true;
                LootNotifications.Publish(Currency(eCurrency.Ruby, 1)); f.Feed.Tick(0, false); f.Feed.Tick(.05f, false);
                var row = Find(f.Feed, eCurrency.Ruby); float age = row.elapsed, phase = row.phaseTime;
                f.Feed.gameObject.SetActive(false); f.Feed.gameObject.SetActive(true); f.Feed.Tick(20, false); bool frozen = Near(row.elapsed, age) && Near(row.phaseTime, phase);
                f.Feed.Tick(20.1f, false);
                check(frozen && row.phase == LootFeedView.MotionPhase.Holding && Near(row.group.alpha, 1) && row.visual.anchoredPosition == Vector2.zero,
                    "reactivation does not leave an entry stuck or consume the disabled interval");
            });
            Scenario("Filter clear", (f, check) =>
            {
                LootNotifications.Publish(Currency(eCurrency.Ruby, 1)); f.Feed.Tick(0, false); f.Feed.Tick(.05f, false);
                LootNotificationSettings.SetCurrency(eCurrency.Ruby, false, false); f.Feed.Tick(1, false);
                check(f.Feed.VisibleCount == 0 && LootNotifications.Count == 0 && f.Feed.rows.All(row => !row.occupied &&
                    !row.rect.gameObject.activeSelf && Near(row.group.alpha, 1) && row.visual.anchoredPosition == Vector2.zero),
                    "filter-off immediately clears in-flight motion and restores the pooled visual pose");
            });
            Scenario("Account clear", (f, check) =>
            {
                Fill(f); var ids = Pool(f.Feed); LootNotifications.Publish(Currency(eCurrency.Ruby, 2)); f.Feed.Tick(.21f, false);
                f.Feed.OnAccountChanged(); f.Feed.Tick(10, false);
                check(f.Feed.VisibleCount == 0 && f.Feed.rows.All(row => !row.refreshPending && !row.fading &&
                    Near(row.group.alpha, 1) && row.visual.anchoredPosition == Vector2.zero) && Pool(f.Feed).SequenceEqual(ids),
                    "account presentation reset cannot leave a refresh or old visual callback behind");
            });
            Scenario("Low-spec motion", (f, check) =>
            {
                GamePresentationSettings.SetLowSpec(true, false);
                LootNotifications.Publish(Currency(eCurrency.Ruby, 1)); f.Feed.Tick(0, false); f.Feed.Tick(.07f, false);
                var row = Find(f.Feed, eCurrency.Ruby); bool reduced = row.visual.anchoredPosition.y < 0 && row.visual.anchoredPosition.y >= -3;
                GamePresentationSettings.SetLowSpec(false, false); f.Feed.Tick(.08f, false); f.Feed.Tick(.15f, false);
                check(reduced && row.notification.Amount == 1 && row.phase == LootFeedView.MotionPhase.Holding &&
                    Near(row.elapsed, .15f) && Near(row.group.alpha, 1) && row.visual.anchoredPosition == Vector2.zero,
                    "low-spec changes reduce travel without restarting age or losing the notification");
            });
            return Report();
        }

        static LootNotification Currency(eCurrency currency, long amount) => new(LootNotificationKind.Currency, currency, 0, amount);
        static bool Near(float a, float b) => Mathf.Abs(a - b) < .002f;
        static bool Has(LootFeedView feed, eCurrency currency) => feed.rows.Any(row => row.occupied && row.notification.Kind == LootNotificationKind.Currency && row.notification.Currency == currency);
        static LootFeedView.Row Find(LootFeedView feed, eCurrency currency) => feed.rows.First(row => row.occupied && row.notification.Kind == LootNotificationKind.Currency && row.notification.Currency == currency);
        static int[] Pool(LootFeedView feed) => feed.rows.Select(row => row.rect.GetInstanceID()).OrderBy(id => id).ToArray();
        static void Fill(LootFeedAcceptance.Fixture fixture, long ruby = 10)
        {
            LootNotifications.Publish(Currency(eCurrency.Ruby, ruby)); LootNotifications.Publish(Currency(eCurrency.AncientCoin, 20));
            LootNotifications.Publish(Currency(eCurrency.EquipmentStone, 30)); fixture.Feed.Tick(0, false); fixture.Feed.Tick(.2f, false);
        }
    }
}
#endif
