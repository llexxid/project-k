#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    // Compiled only in the existing isolated QA player. Commands never save quest fixtures.
    public sealed class BattleHudDeviceProbe : MonoBehaviour
    {
        [Serializable] class Command { public string id, action; public int value; }
        string _directory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() => DontDestroyOnLoad(new GameObject("BattleHudDeviceProbe", typeof(BattleHudDeviceProbe)));
        IEnumerator Start()
        {
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var files=activity.Call<AndroidJavaObject>("getFilesDir"))
                _directory=files.Call<string>("getAbsolutePath");
            string path=Path.Combine(_directory,"hud-command.json");
            var tick=new WaitForSecondsRealtime(.25f);
            while(true)
            {
                if(File.Exists(path))
                {
                    Command command=null;
                    try
                    {
                        command=JsonConvert.DeserializeObject<Command>(File.ReadAllText(path)); File.Delete(path);
                        if(command.action=="pause") Time.timeScale=command.value==1?0:1;
                        if(command.action=="quest")
                        {
                            Time.timeScale=0;
                            var manager=QuestManager.Instance;
                            var state=manager.GetActiveGuideState();
                            if(state.QuestId==1) { state.IsCompleted=true; manager.ClaimQuestReward(1); }
                            state=manager.GetActiveGuideState();
                            if(state.QuestId!=2) throw new InvalidOperationException("Restart QA player for quest 2 fixture.");
                            state.CurrentProgress=0; state.IsCompleted=false;
                            manager.ApplyQuestEvent(new QuestEvent{EventType=eQuestEventType.MonsterKilled,Amount=command.value});
                        }
                        if(command.action=="reopen") UIManager.Instance.ReplaceScreen(KingdomIdle.UI.UIScreenId.Main);
                        Canvas.ForceUpdateCanvases();
                        File.WriteAllText(Path.Combine(_directory,"hud-"+command.id+".json"),JsonConvert.SerializeObject(Snapshot(),Formatting.Indented));
                    }
                    catch(Exception ex)
                    { File.WriteAllText(Path.Combine(_directory,"hud-"+(command?.id??"error")+".json"),JsonConvert.SerializeObject(new {error=ex.ToString()})); }
                }
                yield return tick;
            }
        }
        object Snapshot()
        {
            var main=FindFirstObjectByType<MainScreenView>();
            return new {
                width=Screen.width,height=Screen.height,dpi=Screen.dpi,
                safeArea=new {Screen.safeArea.x,Screen.safeArea.y,Screen.safeArea.width,Screen.safeArea.height},
                memory=Profiler.GetTotalAllocatedMemoryLong(),textures=Profiler.GetAllocatedMemoryForGraphicsDriver(),
                main=main!=null,menu=main!=null&&main.popupHamburger.activeInHierarchy,
                panels=UIManager.Instance.HasBlockingPanel||UIManager.Instance.HasActiveTabPanel,
                labels=FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t=>t.isActiveAndEnabled).Select(t=>new {t.name,t.text,t.fontSize,t.isTextTruncated,bounds=Bounds(t.rectTransform)}).ToArray(),
                controls=FindObjectsByType<Selectable>(FindObjectsSortMode.None).Where(s=>s.isActiveAndEnabled).Select(s=>new {s.name,interactable=s.IsInteractable(),bounds=Bounds((RectTransform)s.transform)}).ToArray(),
                goals=FindObjectsByType<GuideGoalView>(FindObjectsSortMode.None).Select(g=>new{g.name,g.compact,visible=g.body.activeInHierarchy,description=g.description.text,progress=g.progress.text,action=g.actionLabel.text,bounds=Bounds((RectTransform)g.transform)}).ToArray(),
                stage=main==null?null:new{label=main.waveHud.lblStage.text,bounds=Bounds((RectTransform)main.waveHud.transform),timer=main.waveHud.bossTimerBar.activeInHierarchy}
            };
        }
        static object Bounds(RectTransform rt)
        {
            var corners=new Vector3[4];rt.GetWorldCorners(corners);
            var canvas=rt.GetComponentInParent<Canvas>();
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(camera,p)).ToArray();
            float left=points.Min(p=>p.x),right=points.Max(p=>p.x),bottom=points.Min(p=>p.y),top=points.Max(p=>p.y);
            return new{x=left,y=Screen.height-top,width=right-left,height=top-bottom};
        }
    }
}
#endif
