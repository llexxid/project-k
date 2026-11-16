#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using KingdomIdle.Rewards;
using Protocol.Message.Dto;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>빈 PlayMode의 실제 프리팹/표시 DTO 검사. 계정·네트워크·실제 지급을 만들지 않는다.</summary>
    public static class LootFeedAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            Guard(); var checks = new List<string>();
            void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Loot feed: " + label); checks.Add(label); }
            using (var fixture = new Fixture())
            {
                var feed = fixture.Feed; var main = fixture.Main;
                Check(feed.rows.Length == 3 && feed.GetComponentsInChildren<Selectable>(true).Length == 0,
                    "Real prefab has three pooled rows and no selectable input");
                Check(feed.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget) &&
                    feed.GetComponentsInChildren<CanvasGroup>(true).All(g => !g.blocksRaycasts && !g.interactable),
                    "Every graphic and canvas group passes through battle input");
                Check(!LootNotifications.Publish(Currency(eCurrency.Gold, 1)) &&
                    !LootNotifications.Publish(Equipment(fixture.Items[0], 1)), "Default Gold and Normal filters discard on arrival");
                LootNotificationSettings.SetCurrency(eCurrency.Gold, true, false);
                LootNotificationSettings.SetEquipment(0, true, false);
                for (int i = 0; i < 3; i++) LootNotifications.Publish(Equipment(fixture.Items[i], i + 1));
                feed.Tick(0f, false);
                Check(feed.VisibleCount == 3 && LootNotifications.Count == 0, "Common/Rare/Epic fill the fixed pool from the bus");
                for (int i = 0; i < 3; i++)
                    Check(feed.rows[i].name.text == fixture.Items[i].DisplayName && feed.rows[i].icon.sprite == fixture.Items[i].icon &&
                        feed.rows[i].border.color == UguiTheme.RarityColor(fixture.Items[i].rarity) && feed.rows[i].quantity.text == "+" + (i + 1),
                        "Actual catalog name/icon/quantity and thin rarity border " + fixture.Items[i].rarity);

                int binds = feed.BindCount, placements = feed.PlacementWrites;
                foreach (var row in feed.rows) row.rect.hasChanged = false;
                for (int i = 1; i <= 100; i++) feed.Tick(i * .001f, false);
                Check(feed.BindCount == binds && feed.PlacementWrites == placements && feed.rows.All(r => !r.rect.hasChanged),
                    "100 stable frames do not rebind text, write placement, rebuild layout or allocate rows");
                var elapsed = feed.rows.Select(r => r.elapsed).ToArray();
                feed.Tick(10f, true); feed.Tick(20f, true);
                Check(feed.group.alpha == 0f && feed.rows.Select((r, i) => r.elapsed == elapsed[i]).All(x => x),
                    "Menu/modal/dropdown gate hides rows and pauses their lifetime");
                LootNotifications.Publish(Currency(eCurrency.Ruby, 7));
                Check(LootNotifications.Count == 1, "Blocked HUD leaves notifications in the bounded core queue");
                feed.Tick(20.01f, false);
                Check(feed.group.alpha == 1f && feed.VisibleCount == 3 && LootNotifications.Count == 1,
                    "Closing a gate resumes existing rows before consuming pending entries");
                LootNotifications.ClearPending();

                foreach (float height in new[] { 2316f, 1920f, 1440f })
                {
                    fixture.Resize(height); feed.RefreshPlacement();
                    var rect = (RectTransform)feed.transform;
                    Check(feed.AvailableSlots >= 1 && rect.sizeDelta.x >= 1080f * .55f && rect.sizeDelta.x <= 1080f * .60f,
                        "Actual main/guide layout fits left 55–60% width at 1080x" + height);
                }
                fixture.Resize(2316f); feed.Tick(20.02f, false);
                bool oneSlot = false;
                for (float height = 1400f; height >= 800f; height -= 10f)
                {
                    fixture.Resize(height);
                    if (feed.RefreshPlacement() && feed.AvailableSlots == 1) { oneSlot = true; break; }
                }
                Check(oneSlot, "An actual short main layout exposes only one available row");
                feed.Tick(20.03f, false);
                var hidden = feed.rows.Where(r => r.occupied && !r.rect.gameObject.activeSelf).ToArray();
                var hiddenElapsed = hidden.Select(r => r.elapsed).ToArray();
                feed.Tick(20.53f, false); feed.Tick(21.03f, false);
                Check(hidden.Length == 2 && hidden.Select((r, i) => r.elapsed == hiddenElapsed[i] && !r.fading).All(x => x),
                    "Rows outside available geometry pause both timer and fade");
                fixture.Resize(2316f); feed.Tick(21.04f, false);
                Check(hidden.All(r => r.rect.gameObject.activeSelf && r.elapsed < HoldSeconds),
                    "Restored geometry resumes hidden rows without losing their hold time");

                main.waveHud.guide.body.SetActive(false); fixture.Layout();
                Check(feed.RefreshPlacement(), "Inactive guide body falls back to the visible wave HUD");
                main.waveHud.mainHud.SetActive(false); main.waveHud.dungeonObjective.SetActive(true); fixture.Layout();
                Check(feed.RefreshPlacement(), "Dungeon objective is the fallback anchor without a guide");
                main.waveHud.mainHud.SetActive(true); main.waveHud.dungeonObjective.SetActive(false);
                main.waveHud.guide.body.SetActive(true); fixture.Resize(2316f);
                feed.OnAccountChanged();
                Check(feed.VisibleCount == 0, "Account-change presentation handler clears every visible item");

                LootNotifications.Publish(new LootNotification(LootNotificationKind.MageSkill, 0, fixture.Skill.id, 1));
                LootNotifications.Publish(new LootNotification(LootNotificationKind.MageSkill, 0, fixture.Skill.id, 30, isFragment: true));
                LootNotifications.Publish(Equipment(fixture.Items[2], long.MaxValue, EquipmentDisposition.Mail));
                feed.Tick(30f, false);
                Check(feed.rows[0].name.text == fixture.Skill.DisplayName(false) && feed.rows[1].name.text == fixture.Skill.DisplayName(false) + " 파편" &&
                    feed.rows[0].icon.sprite == fixture.Skill.DisplayIcon(false) && feed.rows[1].quantity.text == "+30",
                    "Skill and duplicate fragments preserve separate names/icons/quantities without skill rarity");
                Check(feed.rows[2].state.text == "우편 보관" && feed.rows[2].quantity.text == "+" + NumberNotation.Format(long.MaxValue),
                    "Mail disposition and maximum quantity have dedicated labels");
                foreach (NumberStyle style in Enum.GetValues(typeof(NumberStyle)))
                {
                    NumberNotation.SetStyle(style, false);
                    var quantity = feed.rows[2].quantity;
                    quantity.ForceMeshUpdate(true, true);
                    Check(!quantity.isTextTruncated && quantity.fontSize >= 26f && quantity.textBounds.size.x <= quantity.rectTransform.rect.width + 1f,
                        "Maximum long quantity fits its own column in " + style);
                }
                LootNotificationSettings.SetMageSkills(false, false);
                Check(feed.VisibleCount == 1 && !LootNotifications.Publish(new LootNotification(LootNotificationKind.MageSkill, 0, fixture.Skill.id, 1)),
                    "Changed filter removes visible skill/fragment rows and rejects new arrivals");
                PlayerPrefs.SetInt(UIManager.PrefKeyHideItem, 1); GamePresentationSettings.Apply();
                Check(feed.VisibleCount == 0 && !LootNotifications.Publish(Currency(eCurrency.Ruby, 1)),
                    "Master hide removes visible information and rejects arrivals");
                PlayerPrefs.SetInt(UIManager.PrefKeyHideItem, 0); GamePresentationSettings.Apply();
                feed.Tick(31f, false);
                Check(feed.VisibleCount == 0 && LootNotifications.Count == 0, "Re-enabling has no retroactive hidden rewards");
                LootNotifications.Publish(Currency(eCurrency.Ruby, 9)); feed.Tick(31.01f, false);
                feed.gameObject.SetActive(false); feed.gameObject.SetActive(true); feed.Tick(32f, false);
                Check(feed.VisibleCount == 1 && feed.group.alpha == 1f && feed.rows[0].group.alpha == 1f,
                    "Reactivation restores row visibility and unscaled lifetime");
                feed.Unbind(); Check(feed.VisibleCount == 0 && LootNotifications.Count == 0, "Main-screen disposal drops pending display only");
            }
            return new Dictionary<string, object> { ["passed"] = checks.Count, ["checks"] = checks,
                ["scope"] = "Display-only DTOs and actual prefab/catalog; no account, server request, payout, price, chance or ownership changes.",
                ["not_exercised"] = "Actual device battle acquisition and receipt dedup belong to root/core acceptance; no performance improvement claim." };
        }

        private const float HoldSeconds = LootFeedView.HoldSeconds;

        /// <summary>연속 획득·가림 해제·가상 안전 영역의 표시 회귀와 실제 프리팹 렌더링.</summary>
        public static Dictionary<string, object> RunSimulation(string output)
        {
            Guard();
            var checks = new List<string>(); var failures = new List<string>();
            void Check(bool ok, string label) { checks.Add(label); if (!ok) failures.Add(label); }
            using (var fixture = new Fixture())
            {
                var feed = fixture.Feed;
                LootNotifications.Publish(Currency(eCurrency.Ruby, 7)); feed.Tick(0f, false);
                feed.Tick(.4f, false); feed.Tick(.5f, true); feed.Tick(10.5f, false);
                Check(feed.VisibleCount == 1 && feed.rows[0].elapsed < .6f,
                    "A frame gap while a menu covers the feed does not consume the resumed hold time");
                feed.Tick(10.9f, false);
                Check(feed.VisibleCount == 1 && Mathf.Abs(feed.rows[0].elapsed - .8f) < .01f,
                    "Resumed notifications advance normally on the next visible frame");
                feed.Tick(13f, false);
                Check(feed.VisibleCount == 0, "Continuously visible notifications still expire after a long frame gap");
            }
            using (var fixture = new Fixture())
            {
                var feed = fixture.Feed;
                foreach (var type in new[] { eCurrency.Ruby, eCurrency.AncientCoin, eCurrency.ClassFragment })
                    LootNotifications.Publish(Currency(type, 3));
                feed.Tick(0f, false);
                bool oneSlot = false;
                for (float height = 1400; height >= 800; height -= 10)
                { fixture.Resize(height); if (feed.RefreshPlacement() && feed.AvailableSlots == 1) { oneSlot = true; break; } }
                feed.Tick(.1f, false);
                var hidden = feed.rows.Where(row => row.occupied && !row.rect.gameObject.activeSelf).ToArray();
                var visible = feed.rows.First(row => row.occupied && row.rect.gameObject.activeSelf);
                fixture.Resize(1920); feed.Tick(10.1f, false);
                Check(oneSlot && hidden.Length == 2 && hidden.All(row => row.occupied && row.elapsed < .2f),
                    "Rows restored after a long hidden frame gap keep their unread lifetime");
                Check(!visible.occupied, "A row that remained visible expires while hidden rows preserve their time");
            }
            using (var fixture = new Fixture())
            {
                var feed = fixture.Feed;
                var ids = feed.rows.Select(row => row.rect.GetInstanceID()).OrderBy(id => id).ToArray();
                LootNotificationSettings.SetEquipment(0, true, false);
                for (int i = 0; i < 1000; i++)
                    LootNotifications.Publish(Currency((eCurrency)(1 + i % 9), 1));
                Check(LootNotifications.Count == 9, "One thousand repeated currency acquisitions merge into nine pending kinds");
                int displayed = 0;
                for (int i = 0; i < 12; i++) { feed.Tick(i * 2.5f, false); displayed = feed.BindCount; }
                Check(displayed == 9 && LootNotifications.Count == 0 && feed.VisibleCount == 0,
                    "Merged burst drains completely through three rows without losing distinct kinds");
                Check(ids.SequenceEqual(feed.rows.Select(row => row.rect.GetInstanceID()).OrderBy(id => id)),
                    "A thousand-acquisition burst reuses the same three row objects");
                foreach (var spec in new[] { (height: 2316f, name: "long-phone"), (height: 1440f, name: "short-phone"), (height: 1440f, name: "tablet-safe-area") })
                {
                    fixture.Resize(spec.height);
                    if (spec.name == "tablet-safe-area")
                    {
                        var main = (RectTransform)fixture.Main.transform;
                        main.offsetMin = new Vector2(48, 36); main.offsetMax = new Vector2(-48, -72); fixture.Layout();
                    }
                    LootNotifications.Publish(Currency(eCurrency.AncientCoin, 100));
                    LootNotifications.Publish(Equipment(fixture.Items[2], 1, EquipmentDisposition.Mail));
                    LootNotifications.Publish(new LootNotification(LootNotificationKind.MageSkill, 0, fixture.Skill.id, 30, isFragment: true));
                    feed.Tick(100f, false);
                    Check(feed.RefreshPlacement() && feed.AvailableSlots > 0 &&
                        ((RectTransform)feed.transform).anchoredPosition.x >= 0,
                        "Actual HUD placement preserves the simulated viewport: " + spec.name);
                    fixture.Capture(System.IO.Path.Combine(output, "loot-" + spec.name + ".png"));
                    feed.OnAccountChanged(); LootNotifications.ClearPending();
                }
            }
            return new Dictionary<string, object> { ["passed"] = checks.Count - failures.Count,
                ["failures"] = failures, ["checks"] = checks,
                ["scope"] = "Actual Unity prefabs and synthetic display notifications; no account, API request or reward mutation. Captures are simulated HUDs, not device or live battle evidence." };
        }

        public static IEnumerator VerifyUnscaledLifetime(Action<Dictionary<string, object>> completed)
        {
            Guard(); float timeScale = Time.timeScale; Fixture fixture = null;
            try
            {
                fixture = new Fixture();
                LootNotificationSettings.SetCurrency(eCurrency.Ruby, true, false);
                LootNotifications.Publish(Currency(eCurrency.Ruby, 12));
                Time.timeScale = 0f;
                float start = Time.unscaledTime;
                fixture.Feed.Tick(start, false);
                if (fixture.Feed.VisibleCount != 1) throw new InvalidOperationException("Unscaled feed did not display its fixture.");
                while (Time.unscaledTime - start < HoldSeconds + LootFeedView.FadeSeconds + .1f)
                {
                    if (Time.realtimeSinceStartup - fixture.Started > 8f) throw new TimeoutException("Unscaled feed expiry timed out.");
                    fixture.Feed.Tick(Time.unscaledTime, false);
                    yield return null;
                }
                fixture.Feed.Tick(Time.unscaledTime, false);
                if (fixture.Feed.VisibleCount != 0) throw new InvalidOperationException("Feed lifetime must expire at timeScale=0.");
                completed?.Invoke(new Dictionary<string, object> { ["passed"] = 1, ["unscaled_seconds"] = Time.unscaledTime - start,
                    ["check"] = "Real UITween fade and hold expire at timeScale=0 using yield-null updates." });
            }
            finally { Time.timeScale = timeScale; fixture?.Dispose(); }
        }

        /// <summary>통신 없는 실제 프리팹을 Game View에 표시한다. 종료 파일 또는 3분 후 원래 씬으로 돌아간다.</summary>
        public static IEnumerator Preview(string output)
        {
            Guard();
            if (Application.isBatchMode) throw new InvalidOperationException("The preview requires a visible Unity Game View.");
            using (var fixture = new Fixture())
            {
                fixture.Resize(1920);
                fixture.Capture(System.IO.Path.Combine(output, "preview-reference.png"));
                var cameraObject = new GameObject("IsolatedUiPreviewCamera", typeof(Camera));
                try
                {
                    var camera = cameraObject.GetComponent<Camera>();
                    cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    camera.transform.position = new Vector3(0, 0, -100);
                    camera.orthographic = true; camera.orthographicSize = 960;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UguiTheme.RusticPanelDeep;
                    var gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                    gameView.Show(); gameView.Focus();
                    System.IO.File.WriteAllText(System.IO.Path.Combine(output, "preview-ready.txt"), "Synthetic actual-prefab Game View; no account or network.");
                    float start = Time.realtimeSinceStartup, next = 0;
                    bool captured = false;
                    while (Time.realtimeSinceStartup - start < 180 &&
                        !System.IO.File.Exists(System.IO.Path.Combine(output, "preview-stop.txt")))
                    {
                        float aspect = Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height);
                        float width = Mathf.Min(1, (1080f / 1920) / aspect);
                        camera.rect = new Rect((1 - width) / 2, 0, width, 1); camera.aspect = 1080f / 1920;
                        float now = Time.realtimeSinceStartup;
                        if (now >= next)
                        {
                            LootNotifications.Publish(Currency(eCurrency.AncientCoin, 100));
                            LootNotifications.Publish(Equipment(fixture.Items[2], 1, EquipmentDisposition.Mail));
                            LootNotifications.Publish(new LootNotification(LootNotificationKind.MageSkill, 0, fixture.Skill.id, 30, isFragment: true));
                            next = now + 3;
                        }
                        fixture.Feed.Tick(Time.unscaledTime, false);
                        if (!captured && now - start > .5f)
                        {
                            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, "game-view.png"));
                            captured = true;
                        }
                        yield return null;
                    }
                }
                finally { Object.DestroyImmediate(cameraObject); }
            }
        }

        private static void Guard()
        {
            if (!Application.isPlaying || UIManager.Instance != null || Scripts.Core.UserManager.Instance != null ||
                EquipmentManager.Instance != null || MageTowerManager.Instance != null || LocalProgression.IsReady)
                throw new InvalidOperationException("LootFeedAcceptance requires empty PlayMode without an account or live managers.");
        }
        private static LootNotification Currency(eCurrency currency, long amount) => new(LootNotificationKind.Currency, currency, 0, amount);
        private static LootNotification Equipment(EquipmentData data, long amount, EquipmentDisposition disposition = EquipmentDisposition.Inventory) =>
            new(LootNotificationKind.Equipment, 0, data.itemCode, amount, disposition);

        private sealed class Fixture : IDisposable
        {
            private static readonly string[] Keys = { LootNotificationSettings.CurrencyMaskKey, LootNotificationSettings.EquipmentMaskKey,
                LootNotificationSettings.MageSkillsKey, UIManager.PrefKeyHideItem, GamePresentationSettings.LowSpecKey, "title_ambientMotion" };
            private readonly Dictionary<string, (bool exists, int value)> _prefs = new();
            private readonly NumberStyle _style = NumberNotation.Style;
            private readonly int _frameRate = Application.targetFrameRate, _sleep = Screen.sleepTimeout;
            private GameObject _canvas, _managers;
            public LootFeedView Feed;
            public MainScreenView Main;
            public EquipmentData[] Items;
            public MageTowerSkillSO Skill;
            public readonly float Started = Time.realtimeSinceStartup;

            public Fixture()
            {
                foreach (string key in Keys) _prefs[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key));
                try
                {
                    PlayerPrefs.SetInt(UIManager.PrefKeyHideItem, 0); GamePresentationSettings.Apply();
                    LootNotificationSettings.ResetDefaults(false); LootNotifications.ClearPending();
                    var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
                    if (catalog == null || catalog.lootFeed == null || catalog.screenMain == null) throw new InvalidOperationException("Prepare the real loot feed/catalog first.");
                    _managers = new GameObject("LootFeedAcceptance_ReadOnlyCatalogs"); _managers.SetActive(false);
                    var equipment = _managers.AddComponent<EquipmentManager>();
                    var database = Load<EquipmentDatabase>();
                    Set(equipment, "_database", database); EquipmentManager.Instance = equipment;
                    Items = Enumerable.Range(0, 3).Select(r => database.equipmentList.First(d => d != null && (int)d.rarity == r && d.icon != null && !d.equipmentName.Contains("Bow"))).ToArray();
                    var mage = _managers.AddComponent<MageTowerManager>();
                    var registry = Load<MageTowerSkillRegistrySO>(); Set(mage, "skillRegistry", registry);
                    typeof(MageTowerManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, mage);
                    Skill = registry.skills.First(s => s != null && s.icon != null && s.id != 6 && s.id != 8);
                    _canvas = new GameObject("LootFeedAcceptance_Canvas", typeof(RectTransform), typeof(Canvas));
                    _canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    Main = Object.Instantiate(catalog.screenMain, _canvas.transform, false).GetComponent<MainScreenView>();
                    F.Stretch((RectTransform)Main.transform);
                    // 실제 VLG/ContentSizeFitter/TMP는 활성 유지한다. 계정 감시와 파티 HUD 위치 추적만 격리한다.
                    foreach (var guide in Main.GetComponentsInChildren<GuideGoalView>(true)) guide.enabled = false;
                    foreach (var anchor in Main.GetComponentsInChildren<StageBadgeAnchor>(true)) anchor.enabled = false;
                    Main.waveHud.mainHud.SetActive(true); Main.waveHud.dungeonObjective.SetActive(false);
                    Main.waveHud.guide.gameObject.SetActive(true); Main.waveHud.guide.body.SetActive(true);
                    Feed = Object.Instantiate(catalog.lootFeed, Main.transform, false).GetComponent<LootFeedView>();
                    Feed.enabled = false; Feed.Bind(Main, null, catalog); Resize(2316f);
                }
                catch { Dispose(); throw; }
            }
            public void Resize(float height) { ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1080f, height); Layout(); }
            public void Layout() { Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(Main.waveHud.topArea); }
            public void Capture(string path)
            {
                var canvas = _canvas.GetComponent<Canvas>();
                var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
                Main.lblGold.text = "1.23M"; Main.lblAncientCoin.text = "16.3K"; Main.lblProfileLevel.text = "148";
                Main.waveHud.lblStage.text = "스테이지 3-7";
                Main.waveHud.guide.description.text = "UI 테스트 · 보상 달성";
                Main.waveHud.guide.stepLabel.text = "가이드 퀘스트 10";
                Main.waveHud.guide.progress.text = "진행 1 / 1";
                Main.waveHud.guide.actionLabel.text = "수령 가능";
                var party = Object.Instantiate(catalog.hudParty, Main.transform, false);
                var partyRect = (RectTransform)party.transform;
                partyRect.anchorMin = Vector2.zero; partyRect.anchorMax = new Vector2(1, 0); partyRect.pivot = new Vector2(.5f, 0);
                partyRect.sizeDelta = new Vector2(0, UguiTheme.PartyHudHeight); partyRect.anchoredPosition = new Vector2(0, UguiTheme.BottomBarHeight);
                var size = ((RectTransform)_canvas.transform).rect.size;
                var cameraObject = new GameObject("LootFeedAcceptance_CaptureCamera");
                var camera = cameraObject.AddComponent<Camera>();
                var target = new RenderTexture((int)size.x, (int)size.y, 24);
                Texture2D image = null; var previous = RenderTexture.active;
                try
                {
                    camera.transform.position = new Vector3(0, 0, -100);
                    camera.orthographic = true; camera.orthographicSize = size.y / 2;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UguiTheme.RusticPanelDeep;
                    camera.targetTexture = target; canvas.worldCamera = camera;
                    Layout();
                    if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
                    {
                        cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                            new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    }
                    else camera.Render();
                    RenderTexture.active = target;
                    image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                }
                finally
                {
                    canvas.worldCamera = null; RenderTexture.active = previous;
                    if (image != null) Object.DestroyImmediate(image);
                    Object.DestroyImmediate(party);
                    Object.DestroyImmediate(cameraObject); target.Release(); Object.DestroyImmediate(target);
                }
            }
            public void Dispose()
            {
                if (Feed != null) Feed.Unbind();
                if (_canvas != null) Object.DestroyImmediate(_canvas);
                if (_managers != null) Object.DestroyImmediate(_managers);
                EquipmentManager.Instance = null;
                typeof(MageTowerManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
                LootNotifications.ClearPending();
                foreach (var pair in _prefs) Restore(pair.Key, pair.Value);
                LootNotificationSettings.Reload(); GamePresentationSettings.Apply(); NumberNotation.SetStyle(_style, false);
                foreach (var pair in _prefs) Restore(pair.Key, pair.Value); // Apply가 최초 low-spec 키를 보완했더라도 원래 부재까지 복원한다.
                Application.targetFrameRate = _frameRate; Screen.sleepTimeout = _sleep;
            }
            private static void Restore(string key, (bool exists, int value) original)
            { if (original.exists) PlayerPrefs.SetInt(key, original.value); else PlayerPrefs.DeleteKey(key); }
            private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
            private static T Load<T>() where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).First(a => a != null);
        }
    }
}
#endif
