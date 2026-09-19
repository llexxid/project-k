using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using KingdomIdle.UI;
using KingdomIdle.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Library 전용 외부 검사. 컴파일된 helper를 불러온 뒤 Start(), Status()를 호출한다.
public static class QuestTabInteraction
{
    private static readonly List<string> Checks = new List<string>();
    private static string _status = "idle", _error;
    private static readonly List<string> Captures = new List<string>();

    public static object Start()
    {
        if (_status == "running") throw new InvalidOperationException("Interaction check already running.");
        if (!Application.isPlaying || LocalProgression.IsReady)
            throw new InvalidOperationException("Enter the empty PlayMode scene without opening an account first.");
        Checks.Clear(); Captures.Clear(); _error = null; _status = "running";
        var runner = new GameObject("QuestTabInteraction_Runner").AddComponent<QuestTabInteractionRunner>();
        runner.Begin(Drive(runner));
        return Status();
    }

    public static object Status() => new { status = _status, passed = Checks.Count, checks = Checks.ToArray(), error = _error,
        captures = Captures.ToArray(), scope = "Real UIManager/prefabs in isolated non-combat PlayMode fixture; synthetic pointer events through real raycast handlers" };

    private static IEnumerator Drive(QuestTabInteractionRunner runner)
    {
        IEnumerator routine = Run();
        while (true)
        {
            bool next = false; object value = null;
            try { next = routine.MoveNext(); if (next) value = routine.Current; }
            catch (Exception exception) { _error = exception.ToString(); _status = "failed"; }
            if (_status == "failed" || !next) break;
            yield return value;
        }
        (routine as IDisposable)?.Dispose();
        if (_status == "running") _status = "passed";
        Object.Destroy(runner.gameObject);
    }

    private static IEnumerator Run()
    {
        var fixture = new Fixture();
        try
        {
            fixture.Open();
            UIManager ui = fixture.Ui;
            ui.PushPanel(UIPanelId.Guide);
            yield return new WaitForSecondsRealtime(.55f);
            GuidePanelView guide = ActiveGuide(ui);
            BalanceQuestPanel panel = guide.GetComponent<BalanceQuestPanel>();
            Check(panel != null && panel.SelectedCategory == eQuestCategory.Guide, "PushPanel creates the Guide default through real Populate/Bind");

            Button[] tabs = Field<RectTransform>(guide, "tabBar").GetComponentsInChildren<Button>(false);
            Check(tabs.Length == 4, "Real UIManager panel has four live tab buttons");
            for (int i = 0; i < tabs.Length; i++)
            {
                Click(tabs[i], fixture.Events);
                Check(panel.SelectedCategory == (eQuestCategory)i, "Raycast and pointer click reach tab " + i + " without an overlay blocker");
            }

            // 현재 탭 인스턴스는 커버 패널 아래에서 비활성화되었다가 같은 선택으로 돌아와야 한다.
            ui.PushPanel(UIPanelId.Notice);
            yield return new WaitForSecondsRealtime(.45f);
            Check(!guide.gameObject.activeSelf, "PushPanel cover deactivates the existing quest panel");
            ui.PopPanel();
            yield return new WaitForSecondsRealtime(.45f);
            Check(guide != null && guide.gameObject.activeInHierarchy && panel.SelectedCategory == eQuestCategory.Achievement,
                "PopPanel returns to the same instance and preserves Achievement selection");

            ScrollRect scroll = Field<ScrollRect>(guide, "scroll");
            Canvas.ForceUpdateCanvases();
            Check(scroll.vertical && scroll.content.rect.height > scroll.viewport.rect.height,
                "Achievement content exceeds the viewport and permits vertical scrolling");
            Vector2 start = ScreenCenter(scroll.viewport);
            PointerEventData drag = Pointer(fixture.Events, start);
            RaycastResult hit = TopHit(fixture.Events, drag);
            GameObject dragHandler = ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject);
            Check(dragHandler == scroll.gameObject, "Viewport raycast resolves to the real ScrollRect drag handler");
            drag.pointerPressRaycast = hit; drag.pointerCurrentRaycast = hit; drag.pointerDrag = dragHandler;
            ExecuteEvents.Execute(dragHandler, drag, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(dragHandler, drag, ExecuteEvents.beginDragHandler);
            Vector2 before = scroll.content.anchoredPosition;
            drag.position = start + Vector2.up * 160;
            drag.delta = Vector2.up * 160;
            ExecuteEvents.Execute(dragHandler, drag, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(dragHandler, drag, ExecuteEvents.endDragHandler);
            Check(Mathf.Abs(scroll.content.anchoredPosition.y - before.y) > 10, "Real ScrollRect pointer drag moves the content");
            scroll.StopMovement();
            yield return new WaitForEndOfFrame();
            Capture("live-achievement.png");
            yield return null;

            // 닫기 버튼도 직접 onClick 대신 최상단 레이캐스트에서 찾아 실제 포인터로 누른다.
            Button close = Field<Button>(guide, "closeButton");
            Click(close, fixture.Events);
            yield return new WaitForSecondsRealtime(.4f);
            Check(guide == null && !ui.HasBlockingPanel, "Raycast close button invokes PopPanel and destroys the closed instance");
            ui.PushPanel(UIPanelId.Guide);
            yield return new WaitForSecondsRealtime(.45f);
            guide = ActiveGuide(ui); panel = guide.GetComponent<BalanceQuestPanel>();
            Check(panel.SelectedCategory == eQuestCategory.Guide, "Reopening after close creates a new instance on Guide");

            tabs = Field<RectTransform>(guide, "tabBar").GetComponentsInChildren<Button>(false);
            Click(tabs[1], fixture.Events);
            var rows = Visible(panel);
            Check(rows.All(x => x.Category == eQuestCategory.Daily) && rows.Any(x => x.IsPending) && rows.Any(x => !x.IsPending),
                "Daily shows old pending rewards alongside the current period in one category");
            QuestRowSnapshot pending = rows.First(x => x.IsPending && x.Token.QuestId == 20002);
            Button pendingButton = ClaimButton(panel, pending.Token);
            long revision = LocalProgression.State.Revision;
            string key = QuestEconomy.Key(pending.Token.QuestId, pending.Token.Period);
            LocalProgression.TestFailedCommit(() => { pendingButton.onClick.Invoke(); return LocalProgression.State.Claims.Contains(key); });
            Check(LocalProgression.State.Revision == revision && !LocalProgression.State.Claims.Contains(key) &&
                panel.SelectedCategory == eQuestCategory.Daily && Visible(panel).Any(x => x.Token == pending.Token),
                "SaveFailed keeps Daily selection and the same pending claim token");
            ClaimButton(panel, pending.Token).onClick.Invoke();
            Check(LocalProgression.State.Claims.Contains(key) && !Visible(panel).Any(x => x.Token == pending.Token),
                "Pending-row retry claims exactly its displayed old-period token");
            // 실패 알림의 일시적 토스트가 다음 탭 레이캐스트에 영향을 주지 않도록 정상 종료를 기다린다.
            yield return new WaitForSecondsRealtime(1.6f);
            yield return new WaitForEndOfFrame();
            Capture("live-daily.png");
            yield return null;
            Click(tabs[2], fixture.Events);
            Check(Visible(panel).All(x => x.Category == eQuestCategory.Weekly) && Visible(panel).Any(x => x.IsPending) && Visible(panel).Any(x => !x.IsPending),
                "Weekly independently includes its old pending and current-period rows");
            yield return new WaitForEndOfFrame();
            Capture("live-weekly.png");
            yield return new WaitForSecondsRealtime(.3f);
        }
        finally { fixture.Dispose(); }
        Check(!LocalProgression.IsReady, "Fixture cleanup restores the unopened account context");
    }

    private static void Capture(string name)
    {
        string directory = Path.GetFullPath("AI/validation/quest-tabs-20260918");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name);
        ScreenCapture.CaptureScreenshot(path);
        Captures.Add(path);
    }

    private static void Click(Button button, EventSystem events)
    {
        if (button == null || !button.interactable || !button.gameObject.activeInHierarchy) throw new InvalidOperationException("Expected an active button.");
        PointerEventData pointer = Pointer(events, ScreenCenter((RectTransform)button.transform));
        RaycastResult hit = TopHit(events, pointer);
        GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
        if (handler != button.gameObject) throw new InvalidOperationException("Pointer blocked: expected " + button.name + ", hit " + hit.gameObject.name);
        pointer.pointerCurrentRaycast = hit; pointer.pointerPressRaycast = hit;
        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static PointerEventData Pointer(EventSystem events, Vector2 point) => new PointerEventData(events)
        { position = point, pressPosition = point, button = PointerEventData.InputButton.Left };
    private static RaycastResult TopHit(EventSystem events, PointerEventData pointer)
    {
        Canvas.ForceUpdateCanvases();
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        if (hits.Count == 0) throw new InvalidOperationException("No GraphicRaycaster result at " + pointer.position);
        return hits[0];
    }
    private static Vector2 ScreenCenter(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
    private static GuidePanelView ActiveGuide(UIManager ui) => ui.LayerPanels.GetComponentsInChildren<GuidePanelView>(false).Single();
    private static List<QuestRowSnapshot> Visible(BalanceQuestPanel panel) => Field<List<QuestRowSnapshot>>(panel, "_visible");
    private static Button ClaimButton(BalanceQuestPanel panel, QuestClaimToken token)
    {
        foreach (object row in Field<IEnumerable>(panel, "_rows"))
            if (Field<QuestRowSnapshot>(row, "Snapshot").Token == token) return Field<GuideStepRowView>(row, "View").checkButton;
        throw new InvalidOperationException("Missing pending row.");
    }
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); Checks.Add(label); }
    private static T Field<T>(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return (T)field.GetValue(target);
        }
        throw new MissingFieldException(name);
    }
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);
    private static void SetUi(UIManager value) => typeof(UIManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { value });

    private sealed class Fixture : IDisposable
    {
        public UIManager Ui;
        public EventSystem Events;
        private GameObject _root, _questRoot, _eventsRoot;
        private IDisposable _scope;
        private readonly QuestManager _previousQuest = QuestManager.Instance;
        private readonly UIManager _previousUi = UIManager.Instance;
        private readonly EventSystem _previousEvents = EventSystem.current;
        private readonly float _volume = AudioListener.volume;
        private readonly int _frameRate = Application.targetFrameRate, _sleep = Screen.sleepTimeout;
        private readonly bool _hadLowSpec = PlayerPrefs.HasKey(GamePresentationSettings.LowSpecKey);
        private readonly int _lowSpec = PlayerPrefs.GetInt(GamePresentationSettings.LowSpecKey);
        private readonly Dictionary<FieldInfo, object> _settings = new Dictionary<FieldInfo, object>();

        public void Open()
        {
            foreach (Type type in new[] { typeof(GameAudioSettings), typeof(GamePresentationSettings), typeof(NumberNotation) })
                foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                    if (field.Name.EndsWith("k__BackingField") && !field.IsInitOnly) _settings[field] = field.GetValue(null);
            _scope = LocalProgression.BeginTestSession();
            LocalProgression.TestUtcNow = new DateTimeOffset(2026, 9, 20, 14, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds();
            LocalProgression.OpenTestAccount("quest-tab-input-" + Guid.NewGuid().ToString("N"));
            Check(LocalProgression.Execute("qa-interaction-pending", state => { state.MainClears.Add(0x20001000B); QuestEconomy.Count(state, eQuestObjectiveType.BossKill, 0, 14); return true; }), "Pending daily and weekly reward fixture commits");
            LocalProgression.TestUtcNow += 2;
            Check(LocalProgression.SynchronizeQuests(), "Monday rollover retains old pending rewards");

            _questRoot = new GameObject("QuestTabInteraction_Quest"); _questRoot.SetActive(false);
            var quest = _questRoot.AddComponent<QuestManager>(); QuestManager.Instance = quest; _questRoot.SetActive(true);
            _root = new GameObject("QuestTabInteraction_Ui"); _root.SetActive(false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/UGUI_UIRoot.prefab");
            GameObject copy = Object.Instantiate(source, _root.transform, false);
            // HUD/전투 매니저는 입력 검사 대상이 아니다. Awake 전 자기 복제본에서만 제거한다.
            foreach (MonoBehaviour component in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Assembly == typeof(UIManager).Assembly && !(component is UIManager) && !(component is SafeAreaFitter))
                    Object.DestroyImmediate(component);
            Ui = copy.GetComponent<UIManager>(); Set(Ui, "dontDestroyOnLoad", false); SetUi(Ui);
            _eventsRoot = new GameObject("QuestTabInteraction_Events");
            Events = _eventsRoot.AddComponent<EventSystem>(); EventSystem.current = Events;
            _root.SetActive(true);
            Check(Ui.enabled && Ui.LayerPanels != null && copy.GetComponent<GraphicRaycaster>() != null, "Real UIManager Awake initializes the copied UI root and input raycaster");
        }

        public void Dispose()
        {
            if (_root != null) _root.SetActive(false);
            if (_questRoot != null) _questRoot.SetActive(false);
            QuestManager.Instance = _previousQuest; SetUi(_previousUi); EventSystem.current = _previousEvents;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_questRoot != null) Object.DestroyImmediate(_questRoot);
            if (_eventsRoot != null) Object.DestroyImmediate(_eventsRoot);
            _scope?.Dispose();
            // Awake가 읽어 적용한 전역 UI 설정과 신규 호환 키를 원상복구한다. 원본 에셋은 저장하지 않는다.
            foreach (var pair in _settings) pair.Key.SetValue(null, pair.Value);
            if (_hadLowSpec) PlayerPrefs.SetInt(GamePresentationSettings.LowSpecKey, _lowSpec); else PlayerPrefs.DeleteKey(GamePresentationSettings.LowSpecKey);
            AudioListener.volume = _volume; Application.targetFrameRate = _frameRate; Screen.sleepTimeout = _sleep;
        }
    }
}

public sealed class QuestTabInteractionRunner : MonoBehaviour
{
    public void Begin(IEnumerator routine) { StartCoroutine(routine); }
}
