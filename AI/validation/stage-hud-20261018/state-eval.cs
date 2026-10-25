if(!KingdomIdle.Balance.LocalProgression.AccountKey.StartsWith("balance-qa-stage-hud-")) throw new System.InvalidOperationException("QA only");
var checks=new System.Collections.Generic.List<string>();
void Check(bool ok,string name){if(!ok)throw new System.InvalidOperationException(name);checks.Add(name);}
using(var scope=KingdomIdle.Balance.LocalProgression.BeginTestSession())
{
    var id="stage-hud-migration-20261018";
    KingdomIdle.Balance.LocalProgression.OpenTestAccount(id);
    Check(KingdomIdle.Balance.LocalProgression.Execute("legacy-fixture",s=>{s.CycleFrontierStage=0;s.MainStage=0x200090006;s.CycleBossStage=8;s.HighestMainClear=0x20063000B;return true;}),"legacy fixture saved");
    KingdomIdle.Balance.LocalProgression.OpenTestAccount(id);
    Check(KingdomIdle.Balance.LocalProgression.State.CycleFrontierStage==0x200090006,"legacy frontier uses confirmed current location, not permanent high");
    Check(KingdomIdle.Balance.LocalProgression.Execute("legacy-boss-fixture",s=>{s.CycleFrontierStage=0;s.MainStage=0x20008000A;s.CycleBossStage=8;return true;}),"legacy boss fixture saved");
    KingdomIdle.Balance.LocalProgression.OpenTestAccount(id);
    Check(KingdomIdle.Balance.LocalProgression.State.CycleFrontierStage==0x200090001,"confirmed boss clear restores next chapter");
    Check(KingdomIdle.Balance.LocalProgression.Execute("frontier-fixture",s=>{s.CycleFrontierStage=0x200090009;s.MainStage=0x200090006;return true;}),"frontier fixture saved");
    KingdomIdle.Balance.LocalProgression.OpenTestAccount(id);
    Check(KingdomIdle.Balance.LocalProgression.State.CycleFrontierStage==0x200090009,"frontier persists after reload from cleared stage");
}
var f=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var main=UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.MainScreenView>();
var view=(KingdomIdle.UGUI.WaveHudView)main.GetType().GetField("waveHud",f).GetValue(main);
var manager=Scripts.Core.Manager.StageManager.Instance;
var handler=typeof(Scripts.Core.Manager.StageManager).GetField("OnStageEnter",f);
int before=((System.Delegate)handler.GetValue(manager)).GetInvocationList().Length;
KingdomIdle.UGUI.WaveUIController.Init(view);KingdomIdle.UGUI.WaveUIController.Init(view);
Check(((System.Delegate)handler.GetValue(manager)).GetInvocationList().Length==before,"rebinding does not duplicate stage subscriptions");
var fields=typeof(KingdomIdle.UGUI.WaveHudView).GetFields(f).Where(x=>typeof(UnityEngine.Object).IsAssignableFrom(x.FieldType));
var missing=fields.Where(x=>x.Name!="btnLoopIcon" && (UnityEngine.Object)x.GetValue(view)==null).Select(x=>x.Name).ToArray();
Check(missing.Length==0,"all stage HUD serialized references resolve");
System.IO.File.WriteAllText("Recordings/StageHud20261018/state-results.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,checks},Newtonsoft.Json.Formatting.Indented));
return new{passed=true,checks=checks.Count};
