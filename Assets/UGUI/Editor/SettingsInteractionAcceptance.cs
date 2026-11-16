using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Dto = Protocol.Message.Dto;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>실시간 UI 갱신 경계만 검사한다. 계정·통신·보상 지급을 만들지 않으며 실패 뒤에도 다음 사례를 수집한다.</summary>
    public static class SettingsInteractionAcceptance
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static IEnumerator Run(Action<Dictionary<string, object>> completed)
        {
            if (!Application.isPlaying || LocalProgression.IsReady || NetworkManager.Instance != null || UserManager.Instance != null ||
                QuestManager.Instance != null || UIManager.Instance != null)
                throw new InvalidOperationException("Run SettingsInteractionAcceptance in empty-scene PlayMode without an account or production managers.");
            var cases = new List<Case>();
            var toggle = new Case("ToggleSettingsReset"); cases.Add(toggle);
            yield return Collect(ToggleReset(toggle), toggle);
            var guide = new Case("GuideBusy"); cases.Add(guide);
            yield return Collect(GuideBusy(guide), guide);
            var failures = cases.SelectMany(value => value.Failures).ToArray();
            completed?.Invoke(new Dictionary<string, object>
            {
                ["passed"] = failures.Length == 0 ? cases.Sum(value => value.Checks.Count) : 0,
                ["failures"] = failures,
                ["cases"] = cases.Select(value => new Dictionary<string, object>
                    { ["name"] = value.Name, ["checks"] = value.Checks, ["failures"] = value.Failures.ToArray() }).ToArray()
            });
        }

        // MoveNext exceptions are recorded so the other independent fixture still runs.
        private static IEnumerator Collect(IEnumerator routine, Case result)
        {
            try
            {
                while (true)
                {
                    bool next; object current;
                    try { next = routine.MoveNext(); current = next ? routine.Current : null; }
                    catch (Exception exception) { result.Check(false, "fixture executes", exception.ToString()); break; }
                    if (!next) break;
                    yield return current;
                }
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }

        private static IEnumerator ToggleReset(Case result)
        {
            GameObject root = null;
            float scale = Time.timeScale;
            long generation = LocalProgression.AccountGeneration;
            string account = LocalProgression.AccountKey;
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                var template = catalog.overlaySettings.GetComponentsInChildren<ToggleSwitchView>(true)
                    .Single(view => view.gameObject.name == "LootMageSkills");
                root = new GameObject("SettingsInteraction_Toggle", typeof(RectTransform)); root.SetActive(false);
                var go = Object.Instantiate(template.gameObject, root.transform, false);
                var visual = go.GetComponent<ToggleSwitchView>();
                var toggle = go.GetComponent<Toggle>();
                // Keep real switch components; do not run optional click-audio observers.
                foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != toggle && behaviour != visual) behaviour.enabled = false;
                toggle.SetIsOnWithoutNotify(true); go.SetActive(true); root.SetActive(true);
                result.Check(SwitchMatches(visual, toggle), "initial ON matches knob and track", SwitchState(visual, toggle));
                Time.timeScale = 0;
                toggle.isOn = false;
                // Exact view sequence after defaults are restored by RefreshLootSettings.
                // Existing settings acceptance covers the actual reset button/PlayerPrefs;
                // this fixture touches neither preferences nor category choices.
                toggle.SetIsOnWithoutNotify(true); visual.Refresh();
                result.Check(SwitchMatches(visual, toggle), "immediate restored ON matches appearance", SwitchState(visual, toggle));
                yield return new WaitForSecondsRealtime(.2f); yield return null;
                result.Check(SwitchMatches(visual, toggle), "restored ON still matches after 0.2 real seconds", SwitchState(visual, toggle));
                go.SetActive(false); go.SetActive(true);
                result.Check(SwitchMatches(visual, toggle), "reactivation restores current switch appearance", SwitchState(visual, toggle));
            }
            finally
            {
                Time.timeScale = scale;
                if (root != null) { root.SetActive(false); Object.DestroyImmediate(root); }
                result.Check(Time.timeScale == scale && !LocalProgression.IsReady && LocalProgression.AccountGeneration == generation &&
                    LocalProgression.AccountKey == account && NetworkManager.Instance == null && UserManager.Instance == null,
                    "toggle fixture restores timeScale and leaves account/network unopened");
            }
        }

        private static IEnumerator GuideBusy(Case result)
        {
            var originalQuest = QuestManager.Instance;
            var originalUi = UIManager.Instance;
            long generation = LocalProgression.AccountGeneration;
            string account = LocalProgression.AccountKey;
            var numberListeners = typeof(NumberNotation).GetField("Changed", Fields)?.GetValue(null);
            var presentationListeners = typeof(GamePresentationSettings).GetField("Changed", Fields)?.GetValue(null);
            var cachedSettings = new[] { typeof(NumberNotation), typeof(GamePresentationSettings) }
                .SelectMany(type => type.GetFields(Fields)).Where(field => field.IsStatic && !field.IsInitOnly && !field.IsLiteral)
                .ToDictionary(field => field, field => field.GetValue(null));
            GameObject managerRoot = null, uiRoot = null, canvas = null;
            QuestManager manager = null;
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                managerRoot = new GameObject("SettingsInteraction_QuestManager"); managerRoot.SetActive(false);
                manager = managerRoot.AddComponent<QuestManager>(); QuestManager.Instance = manager;
                uiRoot = new GameObject("SettingsInteraction_UIHost"); uiRoot.SetActive(false);
                var host = uiRoot.AddComponent<UIManager>(); Set(host, "catalog", catalog); SetUi(host);
                // Same display sample shape as QuestTabAcceptance. Manager stays inactive so
                // Start/Update/Fetch/actual claim completion never runs.
                manager.Apply(GuideState(), "qa-guide-ui-only"); Publish(manager);
                var before = manager.GetSnapshot(eQuestCategory.Guide).Rows.Single(row => row.Token.QuestId == 10015);

                canvas = new GameObject("SettingsInteraction_MainCanvas", typeof(RectTransform), typeof(Canvas)); canvas.SetActive(false);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 1920);
                var main = Object.Instantiate(catalog.screenMain, canvas.transform, false).GetComponent<MainScreenView>();
                var goal = main.GetComponentsInChildren<GuideGoalView>(true).Single(view => view.compact && view.hideWithPanels);
                foreach (var behaviour in main.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour.GetType().Assembly == typeof(UIManager).Assembly && behaviour != goal) behaviour.enabled = false;
                main.waveHud.mainHud.SetActive(true); main.waveHud.dungeonObjective.SetActive(false);
                goal.gameObject.SetActive(true); goal.body.SetActive(true);
                main.gameObject.SetActive(true); canvas.SetActive(true);
                yield return null; yield return null;
                if (Get<QuestManager>(goal, "_manager") != manager) throw new InvalidOperationException("Actual guide HUD did not connect.");
                Read(goal);
                string idle = goal.actionLabel.text;
                result.Check(goal.actionButton.interactable && Pulse(goal), "warm claimable guide has input and border emphasis", HudState(goal, manager));

                foreach (bool bulk in new[] { false, true })
                {
                    string mode = bulk ? "bulk" : "single";
                    // UI-only incomplete operation. No network object or OnClaimCompleted
                    // success is fabricated; only the existing public IsBusy value changes.
                    SetBusy(manager, before.Token, bulk);
                    yield return null; yield return null; yield return null;
                    result.Check(manager.IsBusy && !goal.actionButton.interactable && goal.actionLabel.text == "수령 중…" && !Pulse(goal) &&
                        ColorDistance(goal.claimBorder.color, UguiTheme.Bronze) < .001f,
                        mode + " busy transition gates input, text and border within three frames", HudState(goal, manager));
                    // A number-format refresh or a guarded HUD tap can read during busy.
                    // Force that same presentation read so the completion assertion also
                    // detects the stale disabled state on the original implementation.
                    Read(goal);
                    Set(manager, "_claim", null);
                    yield return null; yield return null; yield return null;
                    result.Check(!manager.IsBusy && goal.actionButton.interactable && goal.actionLabel.text == idle && Pulse(goal),
                        mode + " busy completion restores input, text and border without a guide change", HudState(goal, manager));
                    var current = manager.GetSnapshot(eQuestCategory.Guide).Rows.Single(row => row.Token == before.Token);
                    result.Check(before.ContentEquals(current), mode + " leaves the supplied guide DTO projection unchanged");
                    // Reset only between cases; never refresh before the completion assertion.
                    Read(goal);
                }
            }
            finally
            {
                if (canvas != null) canvas.SetActive(false);
                if (manager != null)
                {
                    result.Check(Get<int>(manager, "_hudWatchers") == 0, "disable removes the HUD watcher");
                    Set(manager, "_claim", null);
                }
                QuestManager.Instance = originalQuest; SetUi(originalUi);
                if (canvas != null) Object.DestroyImmediate(canvas);
                if (managerRoot != null) Object.DestroyImmediate(managerRoot);
                if (uiRoot != null) Object.DestroyImmediate(uiRoot);
                result.Check(Equals(numberListeners, typeof(NumberNotation).GetField("Changed", Fields)?.GetValue(null)) &&
                    Equals(presentationListeners, typeof(GamePresentationSettings).GetField("Changed", Fields)?.GetValue(null)),
                    "fixture removes its notation and tween presentation listeners");
                foreach (var cached in cachedSettings) cached.Key.SetValue(null, cached.Value);
                result.Check(QuestManager.Instance == originalQuest && UIManager.Instance == originalUi && !LocalProgression.IsReady &&
                    LocalProgression.AccountGeneration == generation && LocalProgression.AccountKey == account &&
                    NetworkManager.Instance == null && UserManager.Instance == null,
                    "fixture restores singletons and never opens account/network");
            }
        }

        private static Dto.QuestStateDTO GuideState() => new Dto.QuestStateDTO
        {
            ServerTimeUtc = 1794448800, DailyPeriodKey = "2026-11-12", DailyResetUtc = 1794495600,
            WeeklyPeriodKey = "2026-11-09", WeeklyResetUtc = 1794754800,
            Rows = new List<Dto.QuestRowDTO>
            {
                new Dto.QuestRowDTO { QuestId = 10015, Category = Dto.QuestCategory.Guide, PeriodKey = "permanent",
                    State = Dto.QuestRowState.Claimable, Progress = 1, RequiredCount = 1, Title = "표시 검사", Description = "가이드 표시 검사",
                    PresentationType = "None", ObjectiveType = "MonsterKill", TargetId = 0,
                    Rewards = new List<Dto.QuestRewardDTO> { new Dto.QuestRewardDTO { Kind = Dto.RewardKind.Currency,
                        CurrencyType = Protocol.Message.Data.eCurrency.AncientCoin, Amount = 5 } } }
            }
        };
        private static void SetBusy(QuestManager manager, QuestClaimToken token, bool bulk)
        {
            var type = typeof(QuestManager).GetNestedType("ClaimOperation", BindingFlags.NonPublic);
            var operation = Activator.CreateInstance(type, true);
            Set(operation, "Tokens", bulk ? new[] { token, new QuestClaimToken(token.AccountGeneration, 20002, "2026-11-12") } : new[] { token });
            Set(operation, "Category", bulk ? (eQuestCategory?)eQuestCategory.Daily : null);
            Set(manager, "_claim", operation);
        }
        private static void Publish(QuestManager manager) => typeof(QuestManager).GetMethod("LateUpdate", Fields).Invoke(manager, null);
        private static void Read(GuideGoalView goal) => typeof(GuideGoalView).GetMethod("ReadCurrent", Fields).Invoke(goal, null);
        private static bool Pulse(GuideGoalView goal)
        {
            var tween = goal.claimBorder.GetComponent<UITween>();
            var actual = goal.claimBorder.color;
            var dim = UguiTheme.Bronze; var bright = UguiTheme.ActionGold;
            bool InRange(float value, float a, float b) => value >= Mathf.Min(a, b) - .001f && value <= Mathf.Max(a, b) + .001f;
            return tween != null && Get<bool>(tween, "_pulseRequested") && Get<bool>(goal, "_breathing") &&
                InRange(actual.r, dim.r, bright.r) && InRange(actual.g, dim.g, bright.g) &&
                InRange(actual.b, dim.b, bright.b) && InRange(actual.a, dim.a, bright.a);
        }
        private static bool SwitchMatches(ToggleSwitchView visual, Toggle toggle) => toggle.isOn &&
            Mathf.Abs(visual.knob.anchoredPosition.x - 34f) < .01f && ColorDistance(visual.track.color, UguiTheme.Bronze) < .001f;
        private static float ColorDistance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);
        private static object SwitchState(ToggleSwitchView visual, Toggle toggle) => new Dictionary<string, object>
            { ["isOn"] = toggle.isOn, ["knobX"] = visual.knob.anchoredPosition.x, ["track"] = ColorUtility.ToHtmlStringRGBA(visual.track.color) };
        private static object HudState(GuideGoalView goal, QuestManager manager) => new Dictionary<string, object>
            { ["busy"] = manager.IsBusy, ["interactable"] = goal.actionButton.interactable, ["text"] = goal.actionLabel.text,
                ["pulse"] = Pulse(goal), ["border"] = ColorUtility.ToHtmlStringRGBA(goal.claimBorder.color) };
        private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Fields).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
        private static void SetUi(UIManager host) => typeof(UIManager).GetProperty("Instance").SetValue(null, host);

        private sealed class Case
        {
            internal readonly string Name;
            internal readonly List<Dictionary<string, object>> Checks = new();
            internal readonly List<string> Failures = new();
            internal Case(string name) { Name = name; }
            internal void Check(bool passed, string text, object actual = null)
            {
                Checks.Add(new Dictionary<string, object> { ["check"] = text, ["passed"] = passed, ["actual"] = actual });
                if (!passed) Failures.Add(Name + ": " + text);
            }
        }
    }
}
