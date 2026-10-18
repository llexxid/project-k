if(!KingdomIdle.Balance.LocalProgression.AccountKey.StartsWith("balance-qa-stage-hud-")) throw new System.InvalidOperationException("QA only");
var manager=Scripts.Core.Manager.StageManager.Instance;
var checks=new System.Collections.Generic.List<string>();var failures=new System.Collections.Generic.List<string>();
void Check(bool ok,string name){if(!ok)throw new System.InvalidOperationException(name);checks.Add(name);}
Scripts.Core.eStage Main(int c,int w)=>Scripts.Core.Manager.StageParser.MakeStage(Scripts.Core.eStageType.Main,c,w);
System.Collections.IEnumerator Wait(System.Func<bool> predicate){float end=UnityEngine.Time.realtimeSinceStartup+25;while(!predicate()){if(UnityEngine.Time.realtimeSinceStartup>end)throw new System.TimeoutException("stage boundary");yield return null;}}
System.Collections.IEnumerator Run(){
    UnityEngine.Time.timeScale=1;manager.BeginStage(Main(9,10));yield return Wait(()=>manager.CurrentStage==Main(9,10)&&manager.CurrentRunState==Scripts.Core.Manager.eStageRunState.Running);
    manager.SetBossAutoChallenge(false);Check(manager.TestClearStage(),"main clear accepted through existing result path");
    yield return Wait(()=>manager.CycleFrontierStage==Main(9,11)&&manager.CurrentRunState==Scripts.Core.Manager.eStageRunState.Running);
    Check(manager.CurrentStage==Main(9,10)&&!manager.BossAutoChallenge,"auto off stays on stage 9-10 while boss becomes highest");
    Check(manager.TryMoveToMainStage(manager.CycleFrontierStage),"manual highest jump enters unlocked boss");
    yield return Wait(()=>manager.CurrentStage==Main(9,11)&&manager.CurrentRunState==Scripts.Core.Manager.eStageRunState.Running);
    Check(!manager.BossAutoChallenge,"manual boss entry keeps auto off");
    Check(manager.TestClearStage(),"boss clear accepted");
    yield return Wait(()=>manager.CurrentStage==Main(10,1)&&manager.CurrentRunState==Scripts.Core.Manager.eStageRunState.Running);
    Check(manager.CycleFrontierStage==Main(10,1),"boss clear advances cycle frontier to next chapter");
    var quest=QuestManager.Instance;
    var before=quest.GetActiveGuideState();
    if(before!=null&&before.IsCompleted){var guide=UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.GuideGoalView>();long id=before.QuestId;guide.actionButton.onClick.Invoke();Check(quest.GetActiveGuideState()?.QuestId!=id,"compact guide retains reward and next-quest action");}
    float start=UnityEngine.Time.realtimeSinceStartup;int frame=UnityEngine.Time.frameCount;var batches=new System.Collections.Generic.List<int>();
    for(int i=0;i<240;i++){yield return null;batches.Add(UnityEditor.UnityStats.batches);}
    float seconds=UnityEngine.Time.realtimeSinceStartup-start;int frames=UnityEngine.Time.frameCount-frame;
    System.IO.File.WriteAllText("Recordings/StageHud20261018/editor-performance.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{scope="Editor QA, active main combat, not Android or before/after benchmark",frames,seconds,fps=frames/seconds,batchesMin=batches.Min(),batchesMax=batches.Max(),batchesAverage=batches.Average()},Newtonsoft.Json.Formatting.Indented));
}
System.Collections.IEnumerator Guard(System.Collections.IEnumerator task,bool root=false){while(true){bool next;try{next=task.MoveNext();}catch(System.Exception e){failures.Add(e.ToString());break;}if(!next)break;if(task.Current is System.Collections.IEnumerator child)yield return Guard(child);else yield return task.Current;if(failures.Count>0)break;}if(root){UnityEngine.Time.timeScale=0;System.IO.File.WriteAllText("Recordings/StageHud20261018/boundary-results.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=failures.Count==0,checks,failures},Newtonsoft.Json.Formatting.Indented));}}
manager.StartCoroutine(Guard(Run(),true));return "boundary validation running";



