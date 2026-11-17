using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>전환 중 열린 실제 선택창이 전투 재개 후 재선택 없이 입력을 복구하는지 검사한다. 이동/계정/API는 호출하지 않는다.</summary>
    public static class StageSelectAvailabilityAcceptance
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static Dictionary<string, object> Run()
        {
            if (Application.isPlaying || StageSelectPopupController.IsOpen)
                throw new InvalidOperationException("Use EditMode without an open stage picker.");
            var checks = new List<string>(); var failures = new List<string>();
            void Check(bool valid, string name) { checks.Add(name); if (!valid) failures.Add(name); }
            var controllerState = typeof(StageSelectPopupController).GetFields(Static).Where(f => !f.IsInitOnly && !f.IsLiteral)
                .ToDictionary(f => f, f => f.GetValue(null));
            var stateField = typeof(LocalProgression).GetField("_state", Static);
            var openingField = typeof(LocalProgression).GetField("_opening", Static);
            var databaseField = typeof(StageCatalogRules).GetField("_database", Static);
            object previousState = stateField.GetValue(null), previousOpening = openingField.GetValue(null), previousDatabase = databaseField.GetValue(null);
            var previousManager = StageManager.Instance;
            GameObject managers = null, popup = null; StageDatabaseSO database = null;
            try
            {
                // Ensure의 파일/계정 열기를 차단하고 새 메모리 객체만 읽도록 한다. 기존 계정 객체는 수정하지 않는다.
                var memory = new ProgressionState { MainStage = (long)Main(1, 1), CycleFrontierStage = (long)Main(3, 5) };
                stateField.SetValue(null, memory); openingField.SetValue(null, true);
                database = Object.Instantiate(AssetDatabase.LoadAssetAtPath<StageDatabaseSO>("Assets/_Project/Resources/StageDatabaseSO.asset"));
                database.hideFlags = HideFlags.HideAndDontSave; database.Init(); databaseField.SetValue(null, database);
                managers = new GameObject("StageSelectAvailabilityAcceptance_Context") { hideFlags = HideFlags.HideAndDontSave };
                managers.SetActive(false);
                var manager = managers.AddComponent<StageManager>(); StageManager.Instance = manager;
                Set(manager, "_provider", new StageDefinitionProvider(database));
                Set(manager, "_currentStage", Main(1, 1)); Set(manager, "_currentStageNumber", 1); Set(manager, "_currentWaveNumber", 1);
                var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
                popup = Object.Instantiate(catalog.popupStageSelect);
                popup.hideFlags = HideFlags.HideAndDontSave; popup.SetActive(true);
                var picker = popup.GetComponent<StageSelectPopupView>();
                SetStatic("_view", picker); SetStatic("_chapter", 2); SetStatic("_wave", 1);
                var main = new MainScreenController();
                Set(main, "_view", managers.AddComponent<MainScreenView>());
                Set(main, "_currencyPollTimer", -1000000f); // Isolate the existing per-frame callback from unrelated currency/account polling.
                var tick = typeof(MainScreenController).GetMethod("OnFrameTick", Instance);
                void Frame() => tick.Invoke(main, null);
                void RunState(eStageRunState state) => Set(manager, "_currentState", state);
                string Snapshot() => Newtonsoft.Json.JsonConvert.SerializeObject(memory);
                string before = Snapshot();

                RunState(eStageRunState.Resolving); StageSelectPopupController.Refresh();
                Check(!picker.travel.interactable && !picker.frontier.interactable, "Actual Refresh during resolving disables both movement actions");
                RunState(eStageRunState.Running);
                Check(!picker.travel.interactable && !picker.frontier.interactable, "Returning to Running without a picker refresh reproduces the stale disabled UI");
                Frame();
                Check(picker.travel.interactable && picker.frontier.interactable, "Existing main frame callback restores both actions without cell reselection or a stage event");
                Check((int)typeof(StageSelectPopupController).GetField("_chapter", Static).GetValue(null) == 2 &&
                    (int)typeof(StageSelectPopupController).GetField("_wave", Static).GetValue(null) == 1,
                    "Availability recovery preserves the selected 2-1 destination");
                foreach (eStageRunState state in Enum.GetValues(typeof(eStageRunState)))
                {
                    RunState(state); Frame();
                    bool running = state == eStageRunState.Running;
                    Check(picker.travel.interactable == running && picker.frontier.interactable == running,
                        "Existing Running-only action policy survives " + state);
                }
                RunState(eStageRunState.Running); Frame();
                var text = popup.GetComponentsInChildren<TMP_Text>(true);
                string[] labels = text.Select(t => t.text).ToArray();
                foreach (var label in text) label.havePropertiesChanged = false;
                for (int i = 0; i < 100; i++) Frame();
                Check(labels.SequenceEqual(text.Select(t => t.text)) && text.All(t => !t.havePropertiesChanged),
                    "One hundred unchanged frames do not rewrite picker text or rebuild selection details");

                SetStatic("_chapter", 3); SetStatic("_wave", 6); StageSelectPopupController.Refresh();
                Check(!picker.travel.interactable && picker.frontier.interactable, "A destination beyond frontier stays locked while frontier movement remains available");
                RunState(eStageRunState.Resolving); Frame(); RunState(eStageRunState.Running); Frame();
                Check(!picker.travel.interactable && picker.frontier.interactable, "Recovery cannot unlock a destination that failed the existing catalog/frontier validation");
                StageSelectPopupController.Hide(); picker.travel.interactable = picker.frontier.interactable = false;
                Frame();
                Check(!popup.activeSelf && !picker.travel.interactable && !picker.frontier.interactable, "Hidden pickers receive no per-frame action changes");
                popup.SetActive(true); SetStatic("_chapter", 2); SetStatic("_wave", 1); StageSelectPopupController.Refresh();
                Check(picker.travel.interactable && picker.frontier.interactable, "Reopen and full refresh recompute selection validity");
                Set(manager, "_currentStage", eStage.GoldDungeon1_1); Frame();
                Check(!StageSelectPopupController.IsOpen, "The existing non-main guard closes a stale picker");
                Set(manager, "_currentStage", Main(1, 1)); popup.SetActive(true); StageSelectPopupController.Refresh();
                StageManager.Instance = null; Frame();
                Check(!StageSelectPopupController.IsOpen, "A missing stage manager closes a stale picker without invoking movement");
                Check(before == Snapshot(), "All UI/state transitions preserve the complete isolated progression object");
            }
            finally
            {
                StageManager.Instance = previousManager;
                foreach (var entry in controllerState) entry.Key.SetValue(null, entry.Value);
                stateField.SetValue(null, previousState); openingField.SetValue(null, previousOpening); databaseField.SetValue(null, previousDatabase);
                if (popup != null) Object.DestroyImmediate(popup);
                if (managers != null) Object.DestroyImmediate(managers);
                if (database != null) Object.DestroyImmediate(database);
            }
            return new Dictionary<string, object> { ["passed"] = checks.Count - failures.Count, ["count"] = checks.Count,
                ["checks"] = checks, ["failures"] = failures,
                ["scope"] = "EditMode actual picker prefab; inactive stage manager; memory progression and cloned catalog; existing MainScreen frame callback only; no network, movement, account or save" };
        }

        private static eStage Main(int chapter, int wave) => StageParser.MakeStage(eStageType.Main, chapter, wave);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Instance).SetValue(owner, value);
        private static void SetStatic(string field, object value) => typeof(StageSelectPopupController).GetField(field, Static).SetValue(null, value);
    }
}
