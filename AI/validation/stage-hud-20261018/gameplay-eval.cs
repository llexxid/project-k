var manager=Scripts.Core.Manager.StageManager.Instance;
if(!KingdomIdle.Balance.LocalProgression.AccountKey.StartsWith("balance-qa-stage-hud-")) throw new System.InvalidOperationException("QA only");
var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var checks=new System.Collections.Generic.List<object>();
var failures=new System.Collections.Generic.List<string>();
T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,flags).GetValue(target);
var main=UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.MainScreenView>();
var view=Field<KingdomIdle.UGUI.WaveHudView>(main,"waveHud");
void Check(bool valid,string name){if(!valid)throw new System.InvalidOperationException(name); checks.Add(new{name,passed=true});}
UnityEngine.Rect Bounds(UnityEngine.Transform t){var c=new UnityEngine.Vector3[4];((UnityEngine.RectTransform)t).GetWorldCorners(c);return UnityEngine.Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
bool Hits(UnityEngine.UI.Button b){var r=Bounds(b.transform);var e=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=r.center};var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(e,hits);return hits.Count>0 && (hits[0].gameObject==b.gameObject || hits[0].gameObject.transform.IsChildOf(b.transform));}
void Click(UnityEngine.UI.Button b){Check(Hits(b)&&b.interactable,"raycast "+b.name);var e=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=Bounds(b.transform).center};UnityEngine.EventSystems.ExecuteEvents.Execute(b.gameObject,e,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);}
System.Collections.IEnumerator Ready(Scripts.Core.eStage stage){UnityEngine.Time.timeScale=1;float end=UnityEngine.Time.realtimeSinceStartup+25;while(manager.CurrentStage!=stage||manager.CurrentRunState!=Scripts.Core.Manager.eStageRunState.Running){if(UnityEngine.Time.realtimeSinceStartup>end)throw new System.TimeoutException(stage.ToString());yield return null;}UnityEngine.Time.timeScale=0;yield return new UnityEngine.WaitForSecondsRealtime(.5f);}
System.Collections.IEnumerator Capture(string name){yield return new UnityEngine.WaitForEndOfFrame();var t=UnityEngine.ScreenCapture.CaptureScreenshotAsTexture();System.IO.File.WriteAllBytes("Recordings/StageHud20261018/"+name+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
System.Collections.IEnumerator Exercise(){
    var stage=manager.CurrentStage;
    Click(Field<UnityEngine.UI.Button>(view,"btnStageSelect")); yield return new UnityEngine.WaitForSecondsRealtime(.4f);
    var popup=UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.StageSelectPopupView>();
    var cells=Field<KingdomIdle.UGUI.StageSelectPopupView.Cell[]>(popup,"cells");Click(cells[5].button);Click(Field<UnityEngine.UI.Button>(popup,"travel"));
    var cleared=Scripts.Core.Manager.StageParser.MakeStage(Scripts.Core.eStageType.Main,9,6);yield return Ready(cleared);
    Check(manager.CurrentStage==cleared,"selected stage travel via pointer");
    yield return Capture("manual-main-720x1280");
    Check(manager.TryEnterDungeon(Scripts.Core.eStage.GoldDungeon1_1),"gold re-entry");yield return Ready(Scripts.Core.eStage.GoldDungeon1_1);
    var skill=UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.MageManualCastHud>();var tray=Field<UnityEngine.RectTransform>(skill,"tray");
    var active=tray.Cast<UnityEngine.Transform>().Where(x=>x.gameObject.activeSelf).ToArray();var returnRow=Field<UnityEngine.GameObject>(view,"dungeonReturn");
    Check(active.Length>0&&active.All(x=>Bounds(x).yMin>Bounds(returnRow.transform).yMax),"manual skills above dungeon return row");
    manager.TryGetHudSnapshot(out var before);long wallet=KingdomIdle.Balance.LocalProgression.State.Wallet[eCurrency.Gold];
    var session=Field<Scripts.Core.StageSession>(manager,"_currentSession");var enemy=session.Monsters.First();
    enemy.TakeDamage(new ActiveSkill.DamageProxy(ulong.MaxValue,null));manager.TryGetHudSnapshot(out var after);
    Check(after.Kills==before.Kills+1&&after.EarnedGold>0&&after.EarnedGold-before.EarnedGold==KingdomIdle.Balance.LocalProgression.State.Wallet[eCurrency.Gold]-wallet,"gold kill counter and earned amount match committed wallet");
    Check(Field<TMPro.TMP_Text>(view,"dungeonTitle").text.Contains(after.Kills+" /"),"gold kill label updates immediately");
    UnityEngine.Time.timeScale=1;yield return new UnityEngine.WaitForSecondsRealtime(1.2f);UnityEngine.Time.timeScale=0;
    Check(int.Parse(Field<TMPro.TMP_Text>(view,"dungeonTime").text.Replace("초",""))<60,"dungeon seconds tick in combat");
    yield return Capture("gold-live-manual-720x1280");
    var returnButton=Field<UnityEngine.UI.Button>(view,"btnDungeonReturn");Click(returnButton);yield return new UnityEngine.WaitForSecondsRealtime(.3f);
    Check(!Hits(returnButton),"return modal blocks background input");Click(Field<UnityEngine.UI.Button>(view,"returnCancel"));
    Check(manager.CurrentDefinition.Type==Scripts.Core.eStageType.GoldDungeon,"pointer cancel stays in dungeon");
    Click(returnButton);yield return new UnityEngine.WaitForSecondsRealtime(.3f);Click(Field<UnityEngine.UI.Button>(view,"returnConfirm"));yield return Ready(cleared);
    Check(manager.TryEnterDungeon(Scripts.Core.eStage.RubyDungeon1_1),"ruby re-entry");yield return Ready(Scripts.Core.eStage.RubyDungeon1_1);
    manager.TryGetHudSnapshot(out var ruby);var boss=ruby.Boss;
    boss.TakeDamage(new ActiveSkill.DamageProxy((ulong)System.Math.Max(1,ruby.Definition.MonsterEntries[0].Combat.Numbers.HP/4),null));
    Check(Field<UnityEngine.UI.Image>(view,"dungeonFill").fillAmount<1&&Field<UnityEngine.UI.Image>(view,"dungeonFill").fillAmount>0,"ruby HP fill follows damage event");
    yield return Capture("ruby-live-manual-720x1280");
    boss.TakeDamage(new ActiveSkill.DamageProxy(ulong.MaxValue,null));UnityEngine.Time.timeScale=1;
    yield return new UnityEngine.WaitForSecondsRealtime(1);UnityEngine.Time.timeScale=0;manager.TryGetHudSnapshot(out var next);
    Check(next.Kills>=1&&next.Boss!=null&&next.Boss!=boss,"next ruby boss binding");
    Check(Field<TMPro.TMP_Text>(view,"dungeonTitle").text=="보스 2 / 3","ruby boss index updates");
    yield return Capture("ruby-second-boss-720x1280");
    Click(returnButton);yield return new UnityEngine.WaitForSecondsRealtime(.3f);Click(Field<UnityEngine.UI.Button>(view,"returnConfirm"));yield return Ready(cleared);
    var fitter=UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.SafeAreaFitter>();var safe=(UnityEngine.RectTransform)fitter.transform;var min=safe.anchorMin;var max=safe.anchorMax;
    fitter.enabled=false;safe.anchorMin=new UnityEngine.Vector2(0,.03f);safe.anchorMax=new UnityEngine.Vector2(1,.96f);
    yield return new UnityEngine.WaitForSecondsRealtime(.4f);
    Check(Bounds(Field<UnityEngine.RectTransform>(view,"topArea")).yMax<Bounds(safe).yMax,"stage HUD stays within simulated safe area");
    yield return Capture("safe-area-main-720x1280");safe.anchorMin=min;safe.anchorMax=max;fitter.enabled=true;
}
System.Collections.IEnumerator Guard(System.Collections.IEnumerator task,bool root=false){
    while(true){bool next;try{next=task.MoveNext();}catch(System.Exception e){failures.Add(e.ToString());break;}if(!next)break;if(task.Current is System.Collections.IEnumerator nested)yield return Guard(nested);else yield return task.Current;if(failures.Count>0)break;}
    if(root){UnityEngine.Time.timeScale=0;System.IO.File.WriteAllText("Recordings/StageHud20261018/gameplay-results.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=failures.Count==0,checks,failures},Newtonsoft.Json.Formatting.Indented));}
}
manager.StartCoroutine(Guard(Exercise(),true));return "gameplay validation running";

