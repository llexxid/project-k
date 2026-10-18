using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>명시적으로 연 QA 계정에서만 실행하는 스테이지 HUD 회귀 검사.</summary>
    public static class StageHudValidation
    {
        private const string Output = "Recordings/StageHud20261018";
        private static readonly List<object> Results = new();
        private static readonly List<string> Errors = new();
        private static StageManager Manager => StageManager.Instance;
        private static WaveHudView View => Object.FindFirstObjectByType<MainScreenView>().waveHud;
        private static eStage Main(int chapter, int wave) => StageParser.MakeStage(eStageType.Main, chapter, wave);

        public static void Run()
        {
            if (!EditorApplication.isPlaying || !LocalProgression.AccountKey.StartsWith("balance-qa-stage-hud-"))
                throw new InvalidOperationException("Open an isolated stage-hud QA account first.");
            Directory.CreateDirectory(Output); Results.Clear(); Errors.Clear();
            Application.logMessageReceived += Log;
            Manager.StartCoroutine(Guard(Exercise()));
        }

        private static void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(message); }
        private static IEnumerator Guard(IEnumerator task)
        {
            while (true)
            {
                bool next;
                try { next=task.MoveNext(); }
                catch (Exception error) { Errors.Add(error.ToString()); break; }
                if (!next) break;
                if (task.Current is IEnumerator child) yield return Guard(child);
                else yield return task.Current;
                if (Errors.Count>0) break;
            }
            if (task is IDisposable disposable) disposable.Dispose();
            if (task.GetType().Name.Contains("Exercise"))
            {
                Time.timeScale=0; Application.logMessageReceived-=Log;
                File.WriteAllText(Output+"/results.json",JsonConvert.SerializeObject(new { passed=Errors.Count==0, results=Results, errors=Errors },Formatting.Indented));
                Debug.Log("STAGE HUD VALIDATION "+(Errors.Count==0?"PASSED":"FAILED"));
            }
        }
        private static void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); Results.Add(new { test=name, passed=true }); }
        private static IEnumerator Ready(eStage stage)
        {
            Time.timeScale=1; float deadline=Time.realtimeSinceStartup+25;
            while (Manager.CurrentStage!=stage || Manager.CurrentRunState!=eStageRunState.Running)
            { if(Time.realtimeSinceStartup>deadline) throw new TimeoutException("Stage ready: "+stage+" / "+Manager.CurrentRunState); yield return null; }
            Time.timeScale=0; yield return new WaitForSecondsRealtime(.4f);
        }
        private static IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(.3f); yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG()); }
            finally { Object.Destroy(image); }
            Results.Add(new { capture=name, width=Screen.width, height=Screen.height, batches=UnityStats.batches,
                graphics=Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None).Count(x=>x.isActiveAndEnabled),
                monoBytes=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() });
        }
        public static void Resolution(int width,int height)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance").GetValue(null);
            var groupType=sizesType.GetProperty("currentGroupType").GetValue(sizes);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{groupType});
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");
            var mode=Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
            var size=Activator.CreateInstance(sizeType,new[]{mode,(object)width,height,"Stage HUD QA"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
            var game=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            game.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(game,index);
            game.Focus(); Application.runInBackground=true;
        }
        private static IEnumerator Exercise()
        {
            Time.timeScale=0;
            while (ModalBackHandler.TryCloseTop()) { }
            Check(LocalProgression.Execute("stage-hud-fixture",s=>{
                s.MainStage=(long)Main(9,9); s.CycleFrontierStage=s.MainStage; s.PendingReincarnation=false;
                s.MainClears.Add((long)Main(1,11)); s.MainClears.Add((long)Main(2,5));
                s.HealthLevel=300; s.GoldTickets=2; s.RubyTickets=2; return true;
            }),"QA fixture committed");
            StatEnhanceManager.Instance.ApplyToAllPlayers();
            Manager.BeginStage(Main(9,9)); yield return Ready(Main(9,9)); Manager.SetBossAutoChallenge(false);
            Resolution(1080,1920); yield return Shot("main-1080x1920");
            Check(View.mainProgress.activeSelf && View.guide.gameObject.activeSelf && !View.btnStageAction.gameObject.activeSelf,"frontier HUD / guide visible");
            Check(!Manager.TryMoveToMainStage(Main(9,10)),"locked stage refused");
            long clear=LocalProgression.State.HighestMainClear;
            Check(Manager.TryMoveToMainStage(Main(9,6)),"cleared stage move accepted"); yield return Ready(Main(9,6));
            Check(!View.mainProgress.activeSelf && !View.chapterProgress.activeSelf && !View.bossChallengeRoot.activeSelf &&
                View.btnStageSelect.gameObject.activeInHierarchy && View.btnStageAction.gameObject.activeInHierarchy,"cleared HUD has two buttons only");
            Check(Manager.IsLoopMode && !Manager.BossAutoChallenge && LocalProgression.State.HighestMainClear==clear,"move preserves boss auto and clear ledger");
            yield return Shot("cleared-1080x1920");
            View.btnStageSelect.onClick.Invoke();
            Check(StageSelectPopupController.IsOpen,"stage button opens selection");
            yield return Shot("selection-1080x1920");
            Check(ModalBackHandler.TryCloseTop() && !StageSelectPopupController.IsOpen,"selection back closes");
            View.btnStageAction.onClick.Invoke(); yield return Ready(Main(9,9));
            Check(!Manager.IsLoopMode && !Manager.BossAutoChallenge,"highest button jumps directly without enabling auto");
            Manager.SetBossAutoChallenge(true);
            Check(Manager.TryMoveToMainStage(Main(9,6)),"move with auto enabled"); yield return Ready(Main(9,6));
            Check(Manager.BossAutoChallenge,"cleared travel preserves enabled auto");
            Check(Manager.TryEnterDungeon(eStage.GoldDungeon1_1),"gold dungeon entry"); yield return Ready(eStage.GoldDungeon1_1);
            Check(View.dungeonObjective.activeSelf && View.dungeonReturn.activeSelf && !View.mainHud.activeSelf && !View.guide.gameObject.activeSelf,"dungeon replaces main and guide");
            yield return Shot("gold-1080x1920");
            View.btnDungeonReturn.onClick.Invoke(); Check(View.returnPopup.activeSelf,"return opens confirmation");
            yield return Shot("return-confirm-1080x1920");
            View.returnCancel.onClick.Invoke(); Check(!View.returnPopup.activeSelf && Manager.CurrentDefinition.Type==eStageType.GoldDungeon,"cancel keeps dungeon");
            View.btnDungeonReturn.onClick.Invoke(); Check(ModalBackHandler.TryCloseTop() && !View.returnPopup.activeSelf,"return dialog back cancels");
            View.btnDungeonReturn.onClick.Invoke(); View.returnConfirm.onClick.Invoke(); yield return Ready(Main(9,6));
            Check(Manager.IsLoopMode && Manager.BossAutoChallenge && View.guide.gameObject.activeSelf,"confirm restores main stage, loop, auto and guide");
            Check(Manager.TryEnterDungeon(eStage.RubyDungeon1_1),"ruby dungeon entry"); yield return Ready(eStage.RubyDungeon1_1);
            Check(View.dungeonTime.text.EndsWith("초") && !View.dungeonTime.text.Contains("보스"),"ruby time contains seconds only");
            yield return Shot("ruby-1080x1920");
            Resolution(1080,2400); yield return Shot("ruby-1080x2400");
            View.btnDungeonReturn.onClick.Invoke(); View.returnConfirm.onClick.Invoke(); yield return Ready(Main(9,6));
            yield return Shot("cleared-1080x2400");
            View.btnStageAction.onClick.Invoke(); yield return Ready(Main(9,9));
            yield return Shot("main-1080x2400");
            View.btnStageSelect.onClick.Invoke(); yield return Shot("selection-1080x2400");
            Resolution(1536,2048); yield return Shot("selection-1536x2048"); StageSelectPopupController.Hide();
            yield return Shot("main-1536x2048");
            Resolution(720,1280); yield return Shot("main-720x1280");
            View.btnStageSelect.onClick.Invoke(); yield return Shot("selection-720x1280"); StageSelectPopupController.Hide();
            var saved=LocalProgression.State;
            var draft=JsonConvert.DeserializeObject<ProgressionState>(JsonConvert.SerializeObject(saved));
            draft.CycleBossStage=8; draft.PendingReincarnation=true; draft.CycleStartedUtc=0; draft.LastReincarnationUtc=0;
            draft.ReincarnationLevel=0; draft.ReincarnationsToday=0;
            Reincarnation.ReincarnationService.CommitAtBoundary(draft,"stage-hud-validation");
            Check(draft.CycleFrontierStage==(long)Main(1,1) && draft.MainClears.SetEquals(saved.MainClears),"reincarnation resets frontier and preserves permanent clears");
        }
    }
}
