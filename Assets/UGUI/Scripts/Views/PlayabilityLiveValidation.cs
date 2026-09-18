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
        const string Output = "Recordings/PlayabilityRevision/Editor/Live";
        readonly List<object> _results = new();
        readonly List<string> _errors = new();

        static PlayabilityLiveValidation()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) Install();
            };
        }

        public static void Run()
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
            var run = Exercise();
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
            Time.timeScale = 2;
            for (int mode = 0; mode < 2; mode++)
            for (int id = 0; id < 10; id++)
            {
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
        }
        static void Capture(string name)
        {
            var camera = Camera.main; if (camera == null) return;
            var canvas = UIManager.Instance.GetComponent<Canvas>();
            var mode = canvas.renderMode; var previousCamera = canvas.worldCamera;
            var old = camera.targetTexture; var active = RenderTexture.active;
            var texture = new RenderTexture(1080,2316,24); var image = new Texture2D(1080,2316,TextureFormat.RGB24,false);
            try
            {
                texture.Create(); camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
                image.ReadPixels(new Rect(0,0,1080,2316),0,0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = previousCamera; camera.targetTexture = old; RenderTexture.active = active; texture.Release(); Destroy(texture); Destroy(image); }
        }
    }
}
#endif
