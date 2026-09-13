#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Gacha;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;
namespace KingdomIdle.UGUI
{
    public sealed class BalanceDeviceProbe : MonoBehaviour
    {
        [Serializable] private class Command { public string id,action; public int value; public long stage; }
        private string _directory;
        private const string PlayAccount="device-play-20260914";
        private int _frames; private double _totalMs,_maxMs;private float _nextSample;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            LocalProgression.OpenTestAccount(PlayAccount);
            SeedNew();
            DontDestroyOnLoad(new GameObject("BalanceDeviceProbe",typeof(BalanceDeviceProbe)));
        }
        private static void SeedNew()
        {
            LocalProgression.Execute("qa-isolation",s=> {
                if(s.Modules.ContainsKey("imported"))return false;
                s.Modules["imported"]="QA isolated new profile";s.Modules["inventory-imported"]=s.Modules["mage-imported"]="1";
                for(int i=0;i<3;i++){s.Jobs[i]="Spearman";s.UnlockedJobs[i]=new System.Collections.Generic.HashSet<string>{"Spearman"};}
                return true;
            });
        }
        private IEnumerator Start()
        {
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var files=activity.Call<AndroidJavaObject>("getFilesDir"))_directory=files.Call<string>("getAbsolutePath");
            string path=Path.Combine(_directory,"balance-command.json");
            while(true)
            {
                if(File.Exists(path))
                {
                    Command c=null;
                    try
                    {
                        c=JsonConvert.DeserializeObject<Command>(File.ReadAllText(path));File.Delete(path);
                        object output=null;
                        switch(c.action)
                        {
                            case "acceptance":
                                float previous=Time.timeScale;Time.timeScale=0;
                                try{output=BalanceAcceptance.Run();}finally{LocalProgression.OpenTestAccount(PlayAccount);EquipmentManager.Instance?.RestoreEquipment();MageTowerManager.Instance?.NotifyCommitted();StatEnhanceManager.Instance?.ApplyToAllPlayers();Time.timeScale=previous;}
                                break;
                            case "pause":Time.timeScale=c.value==1?0:1;break;
                            case "enhance":StatEnhanceManager.Instance.TryEnhance(c.value==1?StatEnhanceManager.EnhanceType.Attack:StatEnhanceManager.EnhanceType.MaxHP,1);break;
                            case "claim":QuestManager.Instance.ClaimQuestReward(c.value);break;
                            case "midgame":
                                LocalProgression.Execute("qa-midgame-fixture",s=>{
                                    s.AttackLevel=s.HealthLevel=30;s.AccountLevel=10;s.Experience=0;
                                    s.Wallet[eCurrency.Gold]=200000;s.Wallet[eCurrency.AncientCoin]=1000;s.Wallet[eCurrency.ArcaneKnowledge]=1000;s.Wallet[eCurrency.ClassFragment]=500;s.Wallet[eCurrency.Ruby]=500;
                                    for(int stage=1;stage<=2;stage++)for(int wave=1;wave<=11;wave++)s.MainClears.Add(0x200000000L|((long)stage<<16)| (uint)wave);
                                    s.HighestMainClear=0x20002000B;s.CycleBossStage=2;
                                    s.Jobs[0]="Knight";s.Jobs[1]="Archer";s.Jobs[2]="Mage";
                                    foreach(var pair in s.Jobs)s.UnlockedJobs[pair.Key].Add(pair.Value);
                                    for(int id=0;id<3;id++){s.MageSkills[id]=new MageSave{Enhance=3,Fragments=3};s.MageSlots[id]=id;}
                                    return true;
                                });
                                StageManager.Instance.BeginStage((eStage)0x20002000A);break;
                            case "dungeon":output=new {accepted=StageManager.Instance.TryEnterDungeon((eStage)c.stage)};break;
                            case "return":StageManager.Instance.ReturnToMainStage();break;
                            case "ruby":output=new {success=RubyProgression.Enhance(c.value==1)};break;
                            case "gacha":
                                var table=GachaManager.Instance.GetAllTables().First(t=>t.gachaType==(c.value==1?eGachaType.Skill:eGachaType.Equipment));
                                GachaManager.Instance.TryPull(table,10,r=>output=new{count=r.Count,items=r.Select(x=>new{x.nameKor,x.amount,x.rewardType}).ToArray()},e=>throw new Exception(e));break;
                            case "stage":StageManager.Instance.BeginStage((eStage)c.stage);break;
                            case "measure":_frames=0;_totalMs=_maxMs=0;break;
                        }
                        Write(c.id,new{result=output,state=Snapshot()});
                    }
                    catch(Exception ex){Write(c?.id??"error",new{error=ex.ToString()});}
                }
                yield return new WaitForSecondsRealtime(.25f);
            }
        }
        private void Update()
        {
            double ms=Time.unscaledDeltaTime*1000d;_frames++;_totalMs+=ms;_maxMs=Math.Max(_maxMs,ms);
            if(_directory!=null && Time.unscaledTime>=_nextSample){_nextSample=Time.unscaledTime+10;Write("live",Snapshot());}
        }
        private object Snapshot()
        {
            var s=LocalProgression.State;var stage=StageManager.Instance;var players=UserManager.Instance?.GetPlayers();
            return new {s.BalanceVersion,s.Revision,stage=stage==null?0:(long)stage.CurrentStage,runState=stage?.CurrentRunState.ToString(),s.MainStage,s.Kills,s.AccountLevel,s.Experience,s.Wallet,
                s.AttackLevel,s.HealthLevel,s.RubyGoldLevel,s.RubyExpLevel,s.ReincarnationLevel,s.GoldTickets,s.RubyTickets,s.GoldDungeonClear,s.RubyDungeonClear,
                s.OfflineKpm,s.OfflineStage,equipment=s.Equipment.Count,pending=s.PendingEquipment.Count,pity=s.EquipmentPity,claims=s.Claims.ToArray(),
                party=players?.Select(p=>new{p.PlayerIndex,job=p.playerStatus.JobName,atk=p.playerStatus.Atk,hp=p.playerStatus.HP,maxHP=p.playerStatus.MaxHP,ratio=p.HPRatio,position=new[]{p.transform.position.x,p.transform.position.y,p.transform.position.z},action=p.CurrentAction.ToString(),target=p.currentTarget?.gameobj?.name}).ToArray(),
                cp=CombatPowerCalculator.CalculatePartyPowerV1(players),mage=s.MageSkills,slot=s.MageSlots,
                monsters=FindObjectsByType<Scripts.Monster.Monster>(FindObjectsSortMode.None).Where(m=>m.isActiveAndEnabled && m.MonAction!=eMonsterAction.Dead).Select(m=>new{type=m.Type.ToString(),m.BalanceReward,m.IsBalanceBoss,position=new[]{m.transform.position.x,m.transform.position.y,m.transform.position.z},hp=m.GetHpRatio(),action=m.MonAction.ToString(),colliders=m.GetComponentsInChildren<Collider2D>().Select(c=>c.enabled).ToArray()}).ToArray(),
                memory=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),meanFrameMs=_frames>0?_totalMs/_frames:0,maxFrameMs=_maxMs,frameCount=_frames,
                lastError=LocalProgression.LastError};
        }
        private void Write(string id,object data)=>File.WriteAllText(Path.Combine(_directory,"balance-"+id+".json"),JsonConvert.SerializeObject(data,Formatting.Indented));
    }
}
#endif
