using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using KingdomIdle.Gacha;
using KingdomIdle.UI;
using Newtonsoft.Json;
using Scripts.Core;
using Scripts.Core.Manager;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>정상 로그인 후 실행하는 UI 검사. 뽑기 요청이나 재화 지급은 하지 않는다.</summary>
    public static class GachaFullScreenValidation
    {
        const string Output = "Recordings/Gacha20261101";
        static readonly List<object> Results = new();
        static UIManager Host => UIManager.Instance;
        static GachaPanelView View => Object.FindFirstObjectByType<GachaPanelView>();

        public static void Run()
        {
            if (!EditorApplication.isPlaying || NetworkManager.Instance?.IsGameDataReady != true)
                throw new InvalidOperationException("Log in normally before running UI validation.");
            Results.Clear(); Directory.CreateDirectory(Output);
            Host.StartCoroutine(Guard(Exercise()));
        }

        static IEnumerator Guard(IEnumerator task)
        {
            Exception failure = null;
            while (true)
            {
                bool next;
                try { next = task.MoveNext(); }
                catch (Exception error) { failure = error; break; }
                if (!next) break;
                yield return task.Current;
            }
            (task as IDisposable)?.Dispose();
            File.WriteAllText(Output + "/ui-validation.json", JsonConvert.SerializeObject(
                new { passed = failure == null, results = Results, failure = failure?.ToString() }, Formatting.Indented));
            if (failure != null) Debug.LogException(failure);
            else Debug.Log("GACHA FULL SCREEN UI VALIDATION PASSED");
        }

        static void Check(bool ok, string test)
        {
            if (!ok) throw new InvalidOperationException(test);
            Results.Add(new { test, passed = true });
        }

        static bool Stretch(RectTransform rect) => rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one
            && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero;

        static void LayoutCheck(string name)
        {
            var view = View;
            Check(Stretch((RectTransform)view.transform) && Stretch(view.sheet), name + ": root/body Stretch zero offsets");
            Check(view.GetComponentsInChildren<SheetSizeFitter>(true).Length == 0 &&
                view.GetComponentsInChildren<ScrollRect>(true).Length == 0, name + ": no sheet fitter or main scroll");
            var bounds = new Vector3[4]; view.sheet.GetWorldCorners(bounds);
            foreach (var row in new[] { "Header", "GachaTabBar", "FeaturedArea", "Details", "Pity", "Wallet", "PullRow" })
            {
                var rect = (RectTransform)view.sheet.Find(row);
                if (!rect.gameObject.activeInHierarchy) continue;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Check(corners[0].y >= bounds[0].y - 1 && corners[2].y <= bounds[2].y + 1 && rect.rect.height > 0,
                    name + ": " + row + " fits");
            }
            Check(view.GetComponentsInChildren<TMPro.TMP_Text>().All(x => !x.isTextOverflowing), name + ": text fits");
            Check(PartyHudController.ModalSuppressCount == 1 && !PartyHudController.Instance.HudRect.gameObject.activeInHierarchy,
                name + ": army HUD suppressed once");
            var manual = Object.FindFirstObjectByType<MageManualCastHud>();
            Check(manual != null && manual.group.alpha == 0 && !manual.group.blocksRaycasts, name + ": manual HUD invisible and input blocked");
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width / 2, bounds[0].y + 30) }, hits);
            Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(view.transform), name + ": bottom menu input covered");
        }

        static void Shot(string name)
        { ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/" + name + ".png")); }

        static IEnumerator Exercise()
        {
            var catalog = Host.Catalog;
            var current = catalog.panelGacha;
            var safe = Object.FindFirstObjectByType<SafeAreaFitter>();
            var safeRect = (RectTransform)safe.transform;
            Vector2 safeMin = safeRect.anchorMin, safeMax = safeRect.anchorMax;
            var mage = KingdomIdle.MageTower.MageTowerManager.Instance;
            bool wasAuto = mage.IsAutoEnabled();
            try
            {
                // 접속 보상은 뒤로가기를 거절하므로 정상 확인 버튼으로 먼저 닫는다.
                foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                    if (button.name == "BtnConfirm") button.onClick.Invoke();
                yield return null;
                for (int i = 0; i < 3 && ModalBackHandler.TryCloseTop(); i++) yield return null;
                Host.ClearPanels(); yield return new WaitForSecondsRealtime(.3f);
                mage.SetAutoEnabled(false); yield return new WaitForSecondsRealtime(.5f);
                Check(PartyHudController.ModalSuppressCount == 0, "initial suppression balanced");
                StageHudValidation.Resolution(1080, 1920); yield return null;
                var old = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Panels/Panel_Gacha_Old.prefab");
                Check(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(old)) == "aafa5a61c0637984c8fc08ba01709101", "Old GUID preserved");
                Check(old != current, "catalog enters new prefab");
                Check(current.GetComponentsInChildren<Component>(true).All(x => x != null), "new prefab has no missing scripts");
                GachaPanelController.SetPendingSkillTab(false);
                catalog.panelGacha = old;
                Host.PushPanel(UIPanelId.Gacha, isTabPanel: true);
                catalog.panelGacha = current;
                yield return new WaitForSecondsRealtime(.5f); Shot("before-old-1080x1920");
                yield return Measure("old");
                Host.ClearPanels(); yield return new WaitForSecondsRealtime(.3f);
                GachaPanelController.SetPendingSkillTab(false);
                Host.PushPanel(UIPanelId.Gacha, isTabPanel: true);
                var position = View.sheet.anchoredPosition;
                for (int i = 0; i < 24; i++) { yield return null; Check(View.sheet.anchoredPosition == position, "open fade has no motion " + i); }
                Check(View.GetComponentsInChildren<GachaCardItemView>(true).Length == 0, "reward cards are lazy");
                foreach (var size in new[] { new Vector2Int(720, 1280), new Vector2Int(1080, 2400), new Vector2Int(1536, 2048) })
                {
                    StageHudValidation.Resolution(size.x, size.y);
                    yield return new WaitForSecondsRealtime(.4f);
                    LayoutCheck(size.ToString()); Shot("equipment-" + size.x + "x" + size.y);
                    yield return null;
                }
                StageHudValidation.Resolution(1080, 1920); yield return new WaitForSecondsRealtime(.3f);
                safe.enabled = false; safeRect.anchorMin = new Vector2(0, .035f); safeRect.anchorMax = new Vector2(1, .95f);
                yield return new WaitForSecondsRealtime(.3f);
                LayoutCheck("simulated top/bottom safe area"); Shot("equipment-safe-area"); yield return null;
                safeRect.anchorMin = safeMin; safeRect.anchorMax = safeMax; safe.enabled = true;
                yield return new WaitForSecondsRealtime(.3f);
                CheckPityDisplay();
                yield return Measure("full-screen"); Shot("after-equipment-1080x1920"); yield return null;
                View.authoredContent.rewardsButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
                var popup = Object.FindFirstObjectByType<GachaRewardsPopupView>();
                Check(popup != null && popup.rewards.rewardGrid.childCount == 13, "equipment popup has 12 equipment + fragments");
                Check(popup.GetComponentsInChildren<GachaCardItemView>().All(x => x.icon.sprite != null), "equipment reward icons present");
                var scroll = popup.GetComponentInChildren<ScrollRect>();
                Check(scroll.content.rect.height > scroll.viewport.rect.height, "equipment popup can scroll");
                scroll.verticalNormalizedPosition = 0; yield return null; Shot("equipment-rewards-bottom"); yield return null;
                Host.RequestBack(); yield return null;
                Check(Object.FindFirstObjectByType<GachaRewardsPopupView>() == null && View != null, "back closes rewards only");
                View.tabBar.GetChild(1).GetComponent<NavTabButtonView>().Button.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.2f);
                Check(!View.authoredContent.pityRoot.gameObject.activeSelf, "mage has no pity");
                Check(View.authoredContent.heroIcon.sprite == View.authoredContent.mageArt, "mage has own art");
                LayoutCheck("mage"); Shot("after-mage-1080x1920"); yield return null;
                View.authoredContent.rewardsButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.2f);
                Check(Object.FindFirstObjectByType<GachaRewardsPopupView>().rewards.rewardGrid.childCount == 11, "mage popup has 8 skills + 3 knowledge rewards");
                Check(Object.FindFirstObjectByType<GachaRewardsPopupView>().GetComponentsInChildren<GachaCardItemView>().All(x => x.icon.sprite != null), "mage reward icons present");
                Shot("mage-rewards"); yield return null; Host.RequestBack(); yield return null;
                Check(View.authoredContent.heroTitle.text == "마탑 뽑기", "popup close preserves selected tab");
                Host.PushPanel(UIPanelId.Development); yield return new WaitForSecondsRealtime(.4f);
                Host.PopPanel(); yield return new WaitForSecondsRealtime(.4f);
                Check(View.GetComponent<CanvasGroup>().alpha == 1 && View.GetComponent<CanvasGroup>().interactable && Stretch(View.sheet), "stack return restores fade/input/position");
                View.authoredContent.rewardsButton.onClick.Invoke(); yield return null;
                Host.PopPanel();
                var closing = View;
                Check(closing.GetComponent<CanvasGroup>().blocksRaycasts && !closing.GetComponent<CanvasGroup>().interactable, "exit blocks input through panel");
                Check(PartyHudController.ModalSuppressCount == 1, "exit keeps HUD suppression");
                yield return new WaitForSecondsRealtime(.3f);
                Check(Object.FindFirstObjectByType<GachaRewardsPopupView>() == null, "owner close removes rewards popup");
                Check(PartyHudController.ModalSuppressCount == 0 && PartyHudController.Instance.HudRect.gameObject.activeInHierarchy, "close restores army HUD");
                yield return new WaitForSecondsRealtime(.5f);
                var manual = Object.FindFirstObjectByType<MageManualCastHud>();
                Check(manual.tray.gameObject.activeInHierarchy && manual.group.blocksRaycasts, "close restores manual HUD");
                for (int i = 0; i < 3; i++)
                { Host.PushPanel(UIPanelId.Gacha, isTabPanel: true); yield return null; Host.RequestBack(); yield return new WaitForSecondsRealtime(.2f); }
                Check(PartyHudController.ModalSuppressCount == 0, "rapid reopen/close does not accumulate suppression");
                Shot("restored-combat"); yield return null;
            }
            finally
            {
                catalog.panelGacha = current;
                mage.SetAutoEnabled(wasAuto);
                safeRect.anchorMin = safeMin; safeRect.anchorMax = safeMax; safe.enabled = true;
            }
        }

        static void CheckPityDisplay()
        {
            var state = LocalProgression.State;
            int saved = state.EquipmentPity;
            var update = typeof(GachaPanelController).GetMethod("UpdateDescription", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                // 같은 프레임 안에서 표시만 검사하며 저장·이벤트·네트워크 요청은 발생하지 않는다.
                foreach (int value in new[] { 0, 38, 39, 0, 7 })
                {
                    var response = new ProgressionState();
                    UserManager.ApplyGachaState(response, new Protocol.Message.Dto.GachaStateDTO { EquipmentPity = value });
                    state.EquipmentPity = response.EquipmentPity; update.Invoke(null, null);
                    var content = View.authoredContent;
                    Check(content.pityLabel.text == value + " / 40회" && content.pityRemaining.text == "에픽 확정까지 " + (40 - value) + "회"
                        && Mathf.Approximately(content.pityFill.fillAmount, value / 40f), "server count display fixture " + value);
                }
            }
            finally { state.EquipmentPity = saved; update.Invoke(null, null); }
        }

        static IEnumerator Measure(string label)
        {
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            using var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            long allocations = 0, time = 0; int batches = 0;
            for (int i = 0; i < 60; i++) { yield return null; allocations += gc.LastValue; time += cpu.LastValue; batches += UnityStats.batches; }
            Results.Add(new { profile = label, environment = "Editor, 60 frames, whole game", gcRecorderValid = gc.Valid, cpuRecorderValid = cpu.Valid,
                gcBytesPerFrame = allocations / 60, mainThreadMs = time / 60d / 1000000, batches = batches / 60d,
                panelGraphics = View.GetComponentsInChildren<Graphic>().Length, panelLayoutGroups = View.GetComponentsInChildren<LayoutGroup>().Length });
        }
    }
}
