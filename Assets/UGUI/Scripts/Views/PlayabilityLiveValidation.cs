#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using KingdomIdle.UI;
using Newtonsoft.Json;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Monster;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    [InitializeOnLoad]
    public sealed class PlayabilityLiveValidation : MonoBehaviour
    {
        const string Key = "Playability.LiveValidation";
        const string Account = "playability-editor-20260918";
        static string Output => Environment.GetEnvironmentVariable("PLAYABILITY_LIVE_OUTPUT") ?? "Recordings/PlayabilityRevision/Editor/Live";
        const string RoutesKey = "Playability.SessionRoutes";
        const string UiOnlyKey = "Playability.MageUiOnly";
        const string PresentationKey = "Playability.CombatPresentation";
        readonly List<object> _results = new();
        readonly List<string> _errors = new();

        static PlayabilityLiveValidation()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) Install();
            };
        }

        public static void Run()
        { SessionState.SetBool(UiOnlyKey, false); SessionState.SetBool(RoutesKey, false); StartEditor(); }
        public static void RunManualChecks()
        { SessionState.SetBool(UiOnlyKey, true); SessionState.SetBool(RoutesKey, false); StartEditor(); }
        public static void RunCombatPresentation()
        { SessionState.SetBool(PresentationKey,true); Run(); }
        public static void RunSessionRoutes()
        { SessionState.SetBool(RoutesKey, true); StartEditor(); }
        static void StartEditor()
        {
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/buildScenes/bootstrap.unity");
            EditorApplication.isPlaying = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (Object.FindFirstObjectByType<PlayabilityLiveValidation>() != null) return;
            LocalProgression.OpenTestAccount(Account);
            DontDestroyOnLoad(new GameObject("PlayabilityLiveValidation", typeof(PlayabilityLiveValidation)));
        }
        IEnumerator Start()
        {
            Application.logMessageReceived += Log;
            var run = SessionState.GetBool(RoutesKey, false) ? ExerciseRoutes() : Exercise();
            while (true)
            {
                bool next;
                try { next = run.MoveNext(); }
                catch (Exception e) { _errors.Add(e.ToString()); Finish(false); yield break; }
                if (!next) break;
                yield return run.Current;
            }
            Finish(_errors.Count == 0);
        }
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) _errors.Add(message + "\n" + stack); }
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        void Finish(bool passed)
        {
            Time.timeScale = 1;
            Application.logMessageReceived -= Log;
            SessionState.SetBool(Key, false);
            File.WriteAllText(Output + "/results.json", JsonConvert.SerializeObject(new { passed, results = _results, errors = _errors }, Formatting.Indented));
            Debug.Log("PLAYABILITY LIVE " + (passed ? "PASSED" : "FAILED"));
            EditorApplication.Exit(passed ? 0 : 1);
        }
        IEnumerator Exercise()
        {
            float deadline = Time.realtimeSinceStartup + 120;
            while (Object.FindFirstObjectByType<TitleScreenView>() == null)
            { Require(Time.realtimeSinceStartup < deadline, "Title load timeout"); yield return null; }
            var title = Object.FindFirstObjectByType<TitleScreenView>();
            title.btnLogin.onClick.Invoke(); title.btnLoginGuest.onClick.Invoke();
            while (UIManager.Instance.ActiveScreenId != UIScreenId.Main || StageManager.Instance == null || UserManager.Instance.GetPlayers().Count == 0)
            { Require(Time.realtimeSinceStartup < deadline, "Guest login timeout"); yield return null; }
            yield return new WaitForSecondsRealtime(2);
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (button.name == "BtnConfirm") button.onClick.Invoke();
            _results.Add(new { test = "live-mage-acceptance", result = MageSkillAcceptance.Run() });
            LocalProgression.OpenTestAccount(Account);
            LocalProgression.Execute("qa-live-fixture", s => {
                s.Modules["imported"] = s.Modules["inventory-imported"] = s.Modules["mage-imported"] = "1";
                s.HealthLevel = 136; s.AttackLevel = 0;
                s.Wallet[eCurrency.AncientCoin] = 100000;
                for (int i = 0; i < 3; i++) { s.Jobs[i] = i == 0 ? "Knight" : i == 1 ? "Spearman" : "Mage"; s.UnlockedJobs[i].Add(s.Jobs[i]); }
                for (int id = 0; id < 10; id++) s.MageSkills[id] = new MageSave();
                for (int i = 0; i < 5; i++) s.MageSlots[i] = -1;
                return true;
            });
            EquipmentManager.Instance.RestoreEquipment(); StatEnhanceManager.Instance.ApplyToAllPlayers();
            var mage = MageTowerManager.Instance; mage.SetAutoEnabled(false);
            if(SessionState.GetBool(PresentationKey,false))
            {
                SessionState.SetBool(PresentationKey,false);
                StageManager.Instance.BeginStage((eStage)0x20003000A);
                yield return new WaitForSeconds(2);
                KingdomIdle.Combat.CombatPresentationAcceptance.Report report=null;
                yield return KingdomIdle.Combat.CombatPresentationAcceptance.Run(r=>report=r,Capture);
                _results.Add(new{test="combat-presentation",result=report});
                Require(report!=null&&report.passed,"Combat presentation: "+string.Join(",",report?.failed??new List<string>()));
            }
            Time.timeScale = 2;
            for (int mode = 0; mode < (SessionState.GetBool(UiOnlyKey, false) ? 0 : 2); mode++)
            for (int id = 0; id < 10; id++)
            {
                if (!MageSkillRules.IsAvailable(id)) continue;
                int skillId = id; bool bloom = mode == 1;
                LocalProgression.Execute("qa-live-skill", s => { s.MageSkills[skillId] = new MageSave { Awaken = bloom ? 10 : 0, BloomEnabled = bloom }; return true; });
                mage.NotifyCommitted();
                StageManager.Instance.BeginStage((eStage)0x20003000A);
                yield return new WaitForSeconds(2);
                mage.Equip(0,id);
                deadline = Time.realtimeSinceStartup + 25;
                while (mage.IsOnCooldown(0) || mage.IsCasting(0))
                { Require(Time.realtimeSinceStartup < deadline, "Cooldown did not finish"); yield return null; }
                if (id == 7)
                {
                    var p = UserManager.Instance.GetPlayers()[0];
                    p.TakeDamage(new ActiveSkill.DamageProxy((ulong)(p.playerStatus.MaxHP / 2), p));
                }
                MageSkillDiagnostics.Events.Clear();
                Require(mage.CastSkill(0), "Cast refused " + id + "/" + mode);
                yield return new WaitForSeconds(id == 0 && bloom ? 1.1f : .4f);
                Capture("skill-" + id + (bloom ? "-bloom" : "-base"));
                deadline = Time.realtimeSinceStartup + 15;
                while (mage.IsCasting(0))
                { Require(Time.realtimeSinceStartup < deadline, "Cast did not end " + id); yield return null; }
                var events = MageSkillDiagnostics.Events.ToArray();
                Require(events.Any(e => e.kind == "end"), "Missing cleanup " + id);
                Require(events.Any(e => (id == 7 ? e.kind == "heal" : e.kind == "damage") && e.amount > 0), "No effect " + id + "/" + mode);
                _results.Add(new { test = "spell", id, bloom, events });
                Debug.Log("PLAYABILITY SPELL " + id + " BLOOM=" + bloom + " PASSED");
            }
            _results.Add(new { test = "aim-rules", invalid = !MageTowerManager.IsValidAimPoint(new Vector3(float.NaN,0,0)), randomIds = mage.GetAllSkills().Where(s=>!s.CanAim).Select(s=>s.id).ToArray() });
            Require(!mage.GetSkillById(1).CanAim && !mage.GetSkillById(3).CanAim, "Random spells cannot aim");
            for (int i = 0; i < 5; i++) mage.Equip(i, new[] {0,1,5,7,8}[i]);
            mage.SetAutoEnabled(false); Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.7f);
            var hud = Object.FindFirstObjectByType<MageManualCastHud>();
            Require(hud != null && hud.tray.gameObject.activeInHierarchy && hud.group.blocksRaycasts, "Manual tray did not open");
            var before = ((RectTransform)hud.buttons[4].transform).anchoredPosition;
            mage.SetAutoEnabled(true);
            yield return new WaitForSecondsRealtime(.16f);
            Require(hud.tray.gameObject.activeSelf && !hud.group.blocksRaycasts, "Exit must animate without accepting input");
            Require(((RectTransform)hud.buttons[4].transform).anchoredPosition != before, "Exit did not move");
            mage.SetAutoEnabled(false);
            yield return new WaitForSecondsRealtime(.7f);
            Require(hud.tray.gameObject.activeSelf && hud.group.blocksRaycasts, "Interrupted exit failed to reopen");
            Require(Vector2.Distance(((RectTransform)hud.buttons[4].transform).anchoredPosition, before) < 1, "Reopened tray drifted");
            Capture("manual-reopened");
            MageTowerDetailPopupController.Show(0);
            yield return new WaitForSecondsRealtime(.7f);
            Require(!hud.tray.gameObject.activeSelf && !hud.group.blocksRaycasts, "Detail popup did not suppress manual tray");
            MageTowerDetailPopupController.Hide();
            yield return new WaitForSecondsRealtime(.7f);
            Require(hud.tray.gameObject.activeInHierarchy && hud.group.blocksRaycasts, "Closing detail did not restore manual tray");
            mage.SetAutoEnabled(true);
            yield return new WaitForSecondsRealtime(.7f);
            Require(!hud.tray.gameObject.activeSelf, "Exit did not deactivate tray");
            _results.Add(new {test="manual-unscaled-exit-reversal", passed=true});
            Time.timeScale = 1;
        }

        IEnumerator ExerciseRoutes()
        {
            float deadline = Time.realtimeSinceStartup + 120;
            while (Object.FindFirstObjectByType<TitleScreenView>() == null)
            { Require(Time.realtimeSinceStartup < deadline, "Title load timeout"); yield return null; }
            var title = Object.FindFirstObjectByType<TitleScreenView>();
            title.btnLogin.onClick.Invoke(); title.btnLoginGuest.onClick.Invoke();
            while (UIManager.Instance.ActiveScreenId != UIScreenId.Main || StageManager.Instance == null || UserManager.Instance.GetPlayers().Count == 0)
            { Require(Time.realtimeSinceStartup < deadline, "Guest login timeout"); yield return null; }
            yield return new WaitForSecondsRealtime(2);
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (button.name == "BtnConfirm") button.onClick.Invoke();
            LocalProgression.OpenTestAccount("player-session-routes-20260918");
            LocalProgression.Execute("qa-route-fixture", s => {
                s.Modules["imported"] = s.Modules["inventory-imported"] = s.Modules["mage-imported"] = "1";
                s.HealthLevel = 136; s.AttackLevel = 0;
                for (int i = 0; i < 3; i++) { s.Jobs[i] = "Spearman"; s.UnlockedJobs[i] = new HashSet<string>{"Spearman"}; }
                s.MageSkills.Clear(); s.MageSkills[0] = new MageSave();
                for (int i = 0; i < 5; i++) s.MageSlots[i] = -1;
                foreach (var item in s.Equipment) item.Player = null;
                return true;
            });
            EquipmentManager.Instance.RestoreEquipment(); StatEnhanceManager.Instance.ApplyToAllPlayers();
            StageManager.Instance.BeginStage((eStage)0x20002000A);
            yield return new WaitForSecondsRealtime(1);
            foreach (int guideId in new[]{10007,10014,10016,10017,10018,10028})
            {
                LocalProgression.Execute("qa-route-guide", s => {
                    s.Claims.RemoveWhere(k => k.StartsWith("quest:100", StringComparison.Ordinal));
                    foreach (var q in QuestEconomy.Definitions.Where(q => q.Category == eQuestCategory.Guide && q.QuestId < guideId))
                        s.Claims.Add(QuestEconomy.Key(q,s));
                    return true;
                });
                yield return null;
                var guide = Object.FindObjectsByType<GuideGoalView>(FindObjectsSortMode.None).First(g=>g.compact);
                Require(guide.actionButton.interactable, "Guide action disabled " + guideId);
                guide.actionButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.35f);
                bool passed = guideId == 10007 ? Object.FindFirstObjectByType<KAEquipmentView>() != null
                    : guideId == 10014 ? Object.FindFirstObjectByType<KAJobChangeView>() != null
                    : guideId == 10016 ? Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(t=>t.name=="Desc" && t.text.Contains("스킬 10종"))
                    : Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(t=>t.text=="마탑 스킬 편성");
                Require(passed, "Wrong guide destination " + guideId);
                Capture("guide-" + guideId);
                _results.Add(new {test="guide-route",guideId,passed});
                MageTowerPopupController.Hide();
                if (UIManager.Instance.HasActiveTabPanel || UIManager.Instance.HasBlockingPanel) UIManager.Instance.PopPanel();
                yield return null;
            }
            var manager = StageManager.Instance;
            manager.SetLoopMode(true); manager.SetBossAutoChallenge(false);
            var wave = Object.FindFirstObjectByType<WaveHudView>();
            Require(wave.btnStageAction != null && wave.btnStageAction.interactable, "Stage retry action missing");
            Require(wave.lblStage.text.Contains("터치"), "Retry hint missing");
            wave.btnStageAction.onClick.Invoke();
            Require(!manager.IsLoopMode && manager.BossAutoChallenge, "Retry did not release loop and boss gate");
            _results.Add(new {test="stage-retry",passed=true});
            Capture("stage-retry");
            LocalProgression.Execute("qa-reincarnation-ready", s => {
                s.CycleBossStage = 2; s.CycleStartedUtc = LocalProgression.UtcNow - 1200;
                s.LastReincarnationUtc = 0; s.PendingReincarnation = false;
                s.ReincarnationsToday = 0; return true;
            });
            ReincarnationPopupController.Show();
            yield return new WaitForSecondsRealtime(.4f);
            var reincarnation = Object.FindFirstObjectByType<ReincarnationPopupView>();
            Require(reincarnation.confirmButton.interactable, "Ready reincarnation disabled");
            manager.BeginStage((eStage)0x20002000B);
            yield return new WaitForSecondsRealtime(.5f);
            Require(!reincarnation.confirmButton.interactable && reincarnation.infoLabel.text.Contains("일반 웨이브"), "Open popup did not follow boss entry");
            manager.BeginStage((eStage)0x20002000A);
            deadline = Time.realtimeSinceStartup + 8;
            while (!reincarnation.confirmButton.interactable)
            {
                Require(Time.realtimeSinceStartup < deadline, "Open popup did not recover after boss exit: " + reincarnation.infoLabel.text);
                yield return null;
            }
            _results.Add(new {test="reincarnation-open-during-stage-change",passed=true});
            Capture("reincarnation-live-state");
            ReincarnationPopupController.Hide();
            GachaPanelController.SetPendingSkillTab(false);
            UIManager.Instance.PushPanel(UIPanelId.Gacha, null, true, true);
            yield return new WaitForSecondsRealtime(.5f);
            var description = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Desc" && t.text.Contains("에픽 확정까지"));
            Require(description != null && description.gameObject.activeInHierarchy, "Equipment guarantee hidden on first open");
            Capture("equipment-guarantee");
            var table = KingdomIdle.Gacha.GachaManager.Instance.GetAllTables().First(t => t.gachaType == KingdomIdle.Gacha.eGachaType.Equipment);
            var longNames = table.rewards.Where(r => r.equipmentData != null)
                .OrderByDescending(r => r.equipmentData.DisplayName.Length).Take(9).ToList();
            GachaResultPopupController.Show(UIManager.Instance, longNames, table, 10);
            yield return new WaitForSecondsRealtime(.5f);
            var cards = Object.FindFirstObjectByType<GachaResultPopupView>().grid.GetComponentsInChildren<GachaCardItemView>();
            Require(cards.Length == 9, "Long-name result cards missing");
            foreach (var card in cards)
            {
                card.nameLabel.ForceMeshUpdate();
                Require(!card.nameLabel.isTextTruncated, "Reward name clipped: " + card.nameLabel.text);
            }
            Capture("long-reward-names");
            _results.Add(new {test="equipment-guarantee-and-reward-names",passed=true,cards=cards.Length});
            GachaResultPopupController.Close();
            UIManager.Instance.PopPanel();
            LocalProgression.Execute("qa-profile-progress", s => {
                s.HighestMainClear = 0x20002000B; s.Kills = 1234;
                s.ReincarnationLevel = 25; s.ReincarnationCount = 4;
                s.GoldDungeonClear = 2; s.RubyDungeonClear = 3;
                s.UnlockedJobs[0].Add("Knight"); s.UnlockedJobs[0].Add("Archer");
                return true;
            });
            Object.FindFirstObjectByType<MainScreenView>().btnProfile.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.4f);
            var profile = Object.FindFirstObjectByType<ProfilePopupView>();
            Require(profile.statValues[0].text == "2-11" && profile.statValues[1].text == "25" && profile.statValues[4].text == NumberNotation.Format(LocalProgression.State.Kills), "Profile not linked to progression");
            Require(profile.totalJobsLabel.text == "2종", "Unavailable jobs counted in profile");
            Require(!profile.trophyLabel.gameObject.activeInHierarchy && !profile.guildLabel.gameObject.activeInHierarchy && !profile.powerButton.interactable, "Sample social data still exposed");
            Capture("profile-progress");
            _results.Add(new {test="profile-progress-and-sample-removal",passed=true});
            profile.closeButton.onClick.Invoke();
            LocalProgression.Execute("qa-reincarnation-daily-cap", s => {
                s.ReincarnationDay = LocalProgression.KstDay; s.ReincarnationsToday = 3;
                s.CycleBossStage = 0; s.CycleStartedUtc = LocalProgression.UtcNow; return true;
            });
            ReincarnationPopupController.Show();
            Require(!reincarnation.confirmButton.interactable && reincarnation.infoLabel.text.Contains("오늘 환생 3회"), "Daily cap guidance is misleading");
            Capture("reincarnation-daily-cap");
            _results.Add(new {test="reincarnation-daily-cap-guidance",passed=true});
            ReincarnationPopupController.Hide();
            LocalProgression.Execute("qa-dungeon-transition", s => {
                s.GoldTickets = 1; s.GoldDungeonClear = 0;
                s.MainClears.Add(0x20001000B); return true;
            });
            UIManager.Instance.PushPanel(UIPanelId.Dungeon, null, true, true);
            yield return new WaitForSecondsRealtime(.4f);
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == "DungeonCard_Gold").onClick.Invoke();
            var dungeon = Object.FindFirstObjectByType<DungeonDifficultyPopupView>();
            var enter = dungeon.GetComponentsInChildren<Button>().First(b => b.name == "EnterButton");
            manager.BeginStage((eStage)0x20002000A);
            Require(manager.CurrentRunState != eStageRunState.Running, "Dungeon boundary was not exercised");
            enter.onClick.Invoke();
            enter.onClick.Invoke(); // Duplicate press must not consume another ticket.
            deadline = Time.realtimeSinceStartup + 10;
            while (manager.CurrentDefinition?.Type != eStageType.GoldDungeon || manager.CurrentRunState != eStageRunState.Running)
            { Require(Time.realtimeSinceStartup < deadline, "Dungeon request lost during wave transition"); yield return null; }
            Require(LocalProgression.State.GoldTickets == 0 && (dungeon == null || !dungeon.gameObject.activeInHierarchy), "Dungeon ticket or popup transition incorrect");
            _results.Add(new {test="dungeon-enter-during-transition",passed=true,ticketsUsed=1});
            Capture("dungeon-transition-entry");
            manager.ReturnToMainStage();
            yield return new WaitForSecondsRealtime(1);
            LocalProgression.Execute("qa-dungeon-cancel", s => { s.GoldTickets = 1; return true; });
            UIManager.Instance.PushPanel(UIPanelId.Dungeon, null, true, true);
            yield return new WaitForSecondsRealtime(.4f);
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == "DungeonCard_Gold").onClick.Invoke();
            dungeon = Object.FindFirstObjectByType<DungeonDifficultyPopupView>();
            enter = dungeon.GetComponentsInChildren<Button>().First(b => b.name == "EnterButton");
            manager.BeginStage((eStage)0x20002000A);
            enter.onClick.Invoke();
            dungeon.Hide();
            yield return new WaitForSecondsRealtime(2);
            Require(LocalProgression.State.GoldTickets == 1 && manager.CurrentDefinition.Type == eStageType.Main, "Closed popup still entered dungeon");
            _results.Add(new {test="dungeon-pending-entry-cancel",passed=true});
        }
        static void Capture(string name)
        {
            var camera = Camera.main; if (camera == null) return;
            var canvas = UIManager.Instance.GetComponent<Canvas>();
            var mode = canvas.renderMode; var previousCamera = canvas.worldCamera;
            int sortingLayer = canvas.sortingLayerID, sortingOrder = canvas.sortingOrder;
            var old = camera.targetTexture; var active = RenderTexture.active;
            var texture = new RenderTexture(1080,2316,24); var image = new Texture2D(1080,2316,TextureFormat.RGB24,false);
            try
            {
                texture.Create(); camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                // Reproduce the normal overlay canvas when rendering to a texture.
                canvas.sortingLayerID = SortingLayer.layers.OrderBy(layer => layer.value).Last().id;
                canvas.sortingOrder = short.MaxValue;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
                image.ReadPixels(new Rect(0,0,1080,2316),0,0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.sortingLayerID = sortingLayer; canvas.sortingOrder = sortingOrder; camera.targetTexture = old; RenderTexture.active = active; texture.Release(); Destroy(texture); Destroy(image); }
        }
    }
}
#endif
