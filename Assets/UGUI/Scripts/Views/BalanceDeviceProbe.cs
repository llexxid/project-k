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
        [Serializable] private class Command { public string id,action; public int value, awaken, enhance, captureMs; public bool bloom; public long stage; public float x,y; }
        private string _directory;
        private const string PlayAccount="device-play-20260914";
        private int _frames; private double _totalMs,_maxMs;private float _nextSample;
        private int _captureLease;
        private bool _capturePaused;
        private float _captureResumeAt;
        private float _peakShakeOffset;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if !NATURAL_PLAYER_QA
            LocalProgression.OpenTestAccount(PlayAccount);
            SeedNew();
#endif
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
#if NATURAL_PLAYER_QA
                        // Natural progression must never consume the fixture/cheat commands below.
                        if (c.action != "state" && c.action != "timescale")
                            throw new InvalidOperationException("Natural player QA allows observation and explicit time scaling only.");
#endif
                        if (c.action != "state") ResumeCapture();
                        object output=null;
                        switch(c.action)
                        {
                            case "crowded-fixture":
                                LocalProgression.Execute("qa-crowded-inventory",s=>{
                                    s.Equipment.Clear(); s.PendingEquipment.Clear(); s.LegacyEquipment.Clear();
                                    s.AutoDismantleMask=0;s.EquipmentRarityFilter=-1;s.EquipmentUsableOnly=false;s.EquipmentSort=0;
                                    var pool=EquipmentManager.Instance.GetByRarity(eEquipmentRarity.Normal).Concat(EquipmentManager.Instance.GetByRarity(eEquipmentRarity.Rare)).Concat(EquipmentManager.Instance.GetByRarity(eEquipmentRarity.Epic)).ToList();
                                    for(int i=0;i<Math.Max(376,c.value);i++)
                                        if(!EquipmentManager.Grant(s,new EquipmentSave{Id="qa-crowded-"+i,Code=pool[i%pool.Count].itemCode},true))return false;
                                    s.Wallet[eCurrency.AncientCoin]=20000;
                                    s.Wallet[eCurrency.EquipmentStone]=100;
                                    s.MainClears.Add(0x20001000B);s.MainClears.Add(0x200020005);
                                    s.HealthLevel=136;s.AttackLevel=75;
                                    return true;
                                });
                                EquipmentManager.Instance.RestoreEquipment();StatEnhanceManager.Instance.ApplyToAllPlayers();
                                StageManager.Instance.BeginStage((eStage)0x20003000A);break;
                            case "mage-aim":
                                var before=LocalProgression.State.Revision;
                                bool aimed=MageTowerManager.Instance.CastSkillAt(c.value,new Vector3(c.x,c.y,0));
                                output=new{accepted=aimed,revisionBefore=before};break;
                            case "polish-acceptance":
                                StartCoroutine(KingdomIdle.Combat.CombatAcceptance.RunLayeredControl(result => Write(c.id, new { result, state = Snapshot() })));
                                continue;
                            case "presentation-acceptance":
                                StartCoroutine(KingdomIdle.Combat.CombatPresentationAcceptance.Run(result=>Write(c.id,new{result,state=Snapshot()})));
                                continue;
                            case "status-fixture":
                                var statusTarget=KingdomIdle.Combat.CombatMotion.Monsters.First(m=>m!=null&&m.MonAction!=eMonsterAction.Dead);
                                if(c.value==0)KingdomIdle.Combat.MonsterCCState.Apply(statusTarget,KingdomIdle.Combat.CrowdControlKind.Stun,4,0);
                                if(c.value==1)KingdomIdle.Combat.MonsterCCState.Apply(statusTarget,KingdomIdle.Combat.CrowdControlKind.Slow,4,.25f,slowStyle:KingdomIdle.Combat.SlowVisualKind.Venom);
                                if(c.value==2)KingdomIdle.Combat.MonsterCCState.Apply(statusTarget,KingdomIdle.Combat.CrowdControlKind.Slow,4,.20f,slowStyle:KingdomIdle.Combat.SlowVisualKind.Void);
                                if(c.value==3){statusTarget.TryTaunt(UserManager.Instance.GetPlayers()[0]);UserManager.Instance.GetPlayers()[0].GrantShield(1000000,4);}
                                if(c.captureMs>0)StartCoroutine(FreezeAfter(c.captureMs/1000f));
                                break;
                            case "combat-acceptance":
                                float combatPrevious=Time.timeScale;Time.timeScale=0;
                                try{output=KingdomIdle.Combat.CombatAcceptance.Run();}finally{LocalProgression.OpenTestAccount(PlayAccount);EquipmentManager.Instance?.RestoreEquipment();MageTowerManager.Instance?.NotifyCommitted();StatEnhanceManager.Instance?.ApplyToAllPlayers();Time.timeScale=combatPrevious;}
                                break;
                            case "combat-hp":
                                foreach(var p in UserManager.Instance.GetPlayers()) if(p.PlayerIndex==c.value)
                                    p.TakeDamage(new ActiveSkill.DamageProxy((ulong)Math.Max(0,BalanceMath.Floor(p.playerStatus.MaxHP*(decimal)(p.HPRatio-.4f))),p));
                                break;
                            case "health-audit":
                                output=KingdomIdle.Combat.CombatAcceptance.RunHealth();break;
                            case "boss-solo":
                                foreach(var p in UserManager.Instance.GetPlayers())
                                    if(p.PlayerIndex!=c.value)p.gameObject.SetActive(false);
                                KingdomIdle.Combat.CombatDiagnostics.Events.Clear();break;
                            case "combat-control":
                                StartCoroutine(KingdomIdle.Combat.CombatAcceptance.RunLiveControl(result=>Write(c.id,new{result,state=Snapshot()})));
                                continue;
                            case "combat-fixture":
                                MageTowerManager.Instance.SetAutoEnabled(false);
                                LocalProgression.Execute("qa-combat-fixture",s=>{
                                    s.AttackLevel=c.enhance;s.HealthLevel=136;
                                    s.Jobs[0]=c.value==1?"Elite_Knight":"Knight";s.Jobs[1]="Spearman";s.Jobs[2]=c.value==1?"Elite_Mage":"Mage";
                                    foreach(var pair in s.Jobs)
                                    {
                                        if(!s.UnlockedJobs.ContainsKey(pair.Key))s.UnlockedJobs[pair.Key]=new System.Collections.Generic.HashSet<string>();
                                        s.UnlockedJobs[pair.Key].Add(pair.Value);
                                    }
                                    foreach(var item in s.Equipment)item.Player=null;
                                    return true;
                                });
                                EquipmentManager.Instance.RestoreEquipment();StatEnhanceManager.Instance.ApplyToAllPlayers();
                                StageManager.Instance.BeginStage((eStage)c.stage);
                                KingdomIdle.Combat.CombatDiagnostics.Events.Clear();Time.timeScale=1;
                                break;
                            case "combat-reset":KingdomIdle.Combat.CombatDiagnostics.Events.Clear();_frames=0;_totalMs=_maxMs=0;break;
                            case "mage-acceptance":
                                float magePrevious=Time.timeScale;Time.timeScale=0;
                                try{output=MageSkillAcceptance.Run();}finally{LocalProgression.OpenTestAccount(PlayAccount);EquipmentManager.Instance?.RestoreEquipment();MageTowerManager.Instance?.NotifyCommitted();StatEnhanceManager.Instance?.ApplyToAllPlayers();Time.timeScale=magePrevious;}
                                break;
                            case "mage-fixture":
                                MageTowerManager.Instance.SetAutoEnabled(false);
                                LocalProgression.Execute("qa-mage-fixture",s=>{
                                    s.AttackLevel=0;s.HealthLevel=136;
                                    s.Wallet[eCurrency.AncientCoin]=10000;s.Wallet[eCurrency.ArcaneKnowledge]=10000;
                                    s.MageSkills.Clear();
                                    for(int id=0;id<MageSkillRules.IdCapacity;id++)if(MageSkillRules.IsAvailable(id))s.MageSkills[id]=new MageSave{Enhance=c.enhance,Awaken=c.awaken,Fragments=55,BloomEnabled=c.bloom && c.awaken==10};
                                    for(int slot=0;slot<5;slot++)s.MageSlots[slot]=-1;
                                    return true;
                                });
                                MageTowerManager.Instance.NotifyCommitted();StatEnhanceManager.Instance.ApplyToAllPlayers();
                                break;
                            case "mage-list":MageTowerPopupController.Show();break;
                            case "mage-detail":MageTowerDetailPopupController.Show(c.value);break;
                            case "mage-configure":
                                LocalProgression.Execute("qa-mage-configure",s=>{s.MageSkills[c.value]=new MageSave{Enhance=c.enhance,Awaken=c.awaken,Fragments=55,BloomEnabled=c.bloom && c.awaken==10};return true;});
                                MageTowerManager.Instance.NotifyCommitted();break;
                            case "mage-cast":
                                MageSkillDiagnostics.Events.Clear();
                                _peakShakeOffset=0;
                                MageTowerManager.Instance.SetAutoEnabled(false);
                                for(int slot=0;slot<5;slot++)MageTowerManager.Instance.Unequip(slot);
                                MageTowerManager.Instance.Equip(0,c.value);
                                bool accepted=MageTowerManager.Instance.CastSkill(0);
                                if(accepted && c.captureMs>0)StartCoroutine(FreezeAfter(c.captureMs/1000f));
                                output=new{accepted};break;
                            case "mage-bloom":output=new{accepted=MageTowerManager.Instance.SetBloomEnabled(c.value,c.bloom),cooldown=MageTowerManager.Instance.GetCooldownRatio(0)};break;
                            case "mage-auto":MageTowerManager.Instance.SetAutoEnabled(c.value==1);break;
                            case "mage-equip":output=new{accepted=MageTowerManager.Instance.Equip(c.awaken,c.value)};break;
                            case "mage-slot-cast":
                                bool slotAccepted=MageTowerManager.Instance.CastSkill(c.value);
                                if(slotAccepted && c.captureMs>0)StartCoroutine(FreezeAfter(c.captureMs/1000f));
                                output=new{accepted=slotAccepted};break;
                            case "timescale":Time.timeScale=Mathf.Clamp(c.value/100f,0f,1f);_capturePaused=Time.timeScale<1;_captureResumeAt=Time.unscaledTime+15;break;
                            case "mage-close":MageTowerDetailPopupController.Hide();MageTowerPopupController.Hide();break;
                            case "equipment-acceptance":
                                float equipmentPrevious=Time.timeScale;Time.timeScale=0;
                                try{output=EquipmentEconomyAcceptance.Run();}finally{LocalProgression.OpenTestAccount(PlayAccount);EquipmentManager.Instance?.RestoreEquipment();StatEnhanceManager.Instance?.ApplyToAllPlayers();Time.timeScale=equipmentPrevious;}
                                break;
                            case "acceptance":
                                float previous=Time.timeScale;Time.timeScale=0;
                                try{output=BalanceAcceptance.Run();}finally{LocalProgression.OpenTestAccount(PlayAccount);EquipmentManager.Instance?.RestoreEquipment();MageTowerManager.Instance?.NotifyCommitted();StatEnhanceManager.Instance?.ApplyToAllPlayers();Time.timeScale=previous;}
                                break;
                            case "pause":Time.timeScale=c.value==1?0:1;_capturePaused=Time.timeScale<1;_captureResumeAt=Time.unscaledTime+15;break;
                            case "enhance":StatEnhanceManager.Instance.TryEnhance(c.value==1?StatEnhanceManager.EnhanceType.Attack:StatEnhanceManager.EnhanceType.MaxHP,1);break;
                            case "claim":QuestManager.Instance.ClaimQuestReward(c.value);break;
                            case "midgame":
                                LocalProgression.Execute("qa-midgame-fixture",s=>{
                                    s.AttackLevel=s.HealthLevel=30;s.AccountLevel=10;s.Experience=0;
                                    s.Wallet[eCurrency.Gold]=200000;s.Wallet[eCurrency.AncientCoin]=1000;s.Wallet[eCurrency.ArcaneKnowledge]=1000;s.Wallet[eCurrency.ClassFragment]=500;s.Wallet[eCurrency.Ruby]=500;
                                    for(int stage=1;stage<=2;stage++)for(int wave=1;wave<=11;wave++)s.MainClears.Add(0x200000000L|((long)stage<<16)| (uint)wave);
                                    s.HighestMainClear=0x20002000B;s.CycleBossStage=2;
                                    s.Jobs[0]="Knight";s.Jobs[1]="Spearman";s.Jobs[2]="Mage";
                                    foreach(var pair in s.Jobs)s.UnlockedJobs[pair.Key].Add(pair.Value);
                                    for(int id=0;id<3;id++){s.MageSkills[id]=new MageSave{Enhance=3,Fragments=3};s.MageSlots[id]=id;}
                                    return true;
                                });
                                StageManager.Instance.BeginStage((eStage)0x20002000A);break;
                            case "dungeon-fixture":
                                LocalProgression.Execute("qa-dungeon-access", s => {
                                    s.GoldTickets = s.RubyTickets = 2;
                                    s.GoldDungeonClear = s.RubyDungeonClear = 4;
                                    return true;
                                });
                                break;
                            case "dungeon":output=new {accepted=StageManager.Instance.TryEnterDungeon((eStage)c.stage)};break;
                            case "return":StageManager.Instance.ReturnToMainStage();break;
                            case "stage-acceptance":output=StageProgressionAcceptance.Run();break;
                            case "stage-clear":output=new{accepted=StageManager.Instance.TestClearStage()};break;
                            case "stage-defeat":StageManager.Instance.DefeatWave();break;
                            case "boss-auto":StageManager.Instance.SetBossAutoChallenge(c.value==1);break;
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
        private IEnumerator FreezeAfter(float seconds)
        {
            int lease=++_captureLease;
            float end=Time.time+seconds;
            while(Time.time<end && lease==_captureLease)yield return null;
            if(lease!=_captureLease)yield break;
            Time.timeScale=0;_capturePaused=true;_captureResumeAt=Time.unscaledTime+8;
        }
        private void ResumeCapture() { _captureLease++;if(_capturePaused)Time.timeScale=1;_capturePaused=false; }
        private void OnApplicationPause(bool paused) { if(!paused)ResumeCapture(); }
        private void Update()
        {
            if(_capturePaused && Time.unscaledTime>=_captureResumeAt)ResumeCapture();
            double ms=Time.unscaledDeltaTime*1000d;_frames++;_totalMs+=ms;_maxMs=Math.Max(_maxMs,ms);
            var shaker=Camera.main?.GetComponent<CameraShaker>();
            if(shaker!=null)_peakShakeOffset=Mathf.Max(_peakShakeOffset,shaker.DiagnosticOffset.magnitude);
            if(_directory!=null && Time.unscaledTime>=_nextSample){_nextSample=Time.unscaledTime+10;Write("live",Snapshot());}
        }
        private object Snapshot()
        {
            var s=LocalProgression.State;var stage=StageManager.Instance;var players=UserManager.Instance?.GetPlayers();
            var background=FindFirstObjectByType<StageBackgroundController>();
            var aimGraphic=FindFirstObjectByType<MagicAimGraphic>();
            var aimSize=aimGraphic==null?Vector2.zero:((RectTransform)aimGraphic.transform).rect.size;
            return new {s.BalanceVersion,s.Revision,stage=stage==null?0:(long)stage.CurrentStage,runState=stage?.CurrentRunState.ToString(),s.MainStage,s.Kills,s.AccountLevel,s.Experience,s.Wallet,
                returnRemaining=stage?.ReturnCountdownRemaining,returnDuration=stage?.ReturnCountdownDuration,bossAuto=stage?.BossAutoChallenge,
                s.AttackLevel,s.HealthLevel,s.RubyGoldLevel,s.RubyExpLevel,s.ReincarnationLevel,s.GoldTickets,s.RubyTickets,s.GoldDungeonClear,s.RubyDungeonClear,
                s.OfflineKpm,s.OfflineStage,equipment=s.Equipment.Count,pending=s.PendingEquipment.Count,reserve=s.LegacyEquipment.Sum(x=>(long)x.Count),pity=s.EquipmentPity,claims=s.Claims.ToArray(),
                manualAuto=MageTowerManager.Instance?.IsAutoEnabled(),
                equipmentCards=FindObjectsByType<EquipCellView>(FindObjectsSortMode.None).Count(x=>x.isActiveAndEnabled),
                aim=aimGraphic==null?null:new{valid=aimGraphic.valid,size=new[]{aimSize.x,aimSize.y}},
                party=players?.Select(p=>new{p.PlayerIndex,id=p.GetInstanceID(),active=p.isActiveAndEnabled,dead=p.IsDead,job=p.playerStatus.JobName,atk=p.playerStatus.Atk,hp=p.playerStatus.HP,maxHP=p.playerStatus.MaxHP,ratio=p.HPRatio,position=new[]{p.transform.position.x,p.transform.position.y,p.transform.position.z},feet=new[]{p.VfxFootPosition.x,p.VfxFootPosition.y},action=p.CurrentAction.ToString(),target=p.currentTarget?.gameobj?.name}).ToArray(),
                cp=CombatPowerCalculator.CalculatePartyPowerV1(players),mage=s.MageSkills,slot=s.MageSlots,
                mageEvents=MageSkillDiagnostics.Events.ToArray(),
                peakShakeOffset=_peakShakeOffset,screenShake=GamePresentationSettings.ScreenShake,
                combatEvents=KingdomIdle.Combat.CombatDiagnostics.Events.ToArray(),
                combatParty=players?.Select(p=>new{p.PlayerIndex,p.ShieldHP,p.BasicAttackSerial,p.LifeGeneration,facing=p.transform.localScale.x,sprite=p.GetComponent<SpriteRenderer>()?.sprite?.name,retreat=p.playerOrder?._move?.IsRetreating}).ToArray(),
                mageCooldown=Enumerable.Range(0,5).Select(i=>new{slot=i,casting=MageTowerManager.Instance?.IsCasting(i),ratio=MageTowerManager.Instance?.GetCooldownRatio(i)}).ToArray(),
                crowdControl=FindObjectsByType<KingdomIdle.Combat.MonsterCCState>(FindObjectsSortMode.None).Where(x=>x.isActiveAndEnabled).Select(x=>new{target=x.name,active=x.enabled,kind=x.DiagnosticKind,remaining=x.DiagnosticRemaining}).ToArray(),
                mageVisuals=FindObjectsByType<KingdomIdle.Combat.PooledSpellVfx>(FindObjectsSortMode.None).Where(x=>x.isActiveAndEnabled).Select(x=>new{x.name,generation=x.SpawnGen,position=new[]{x.transform.position.x,x.transform.position.y},renderers=x.GetComponentsInChildren<SpriteRenderer>().Select(r=>new{r.name,sprite=r.sprite?.name,alpha=r.color.a,size=new[]{r.bounds.size.x,r.bounds.size.y},localPosition=new[]{r.transform.localPosition.x,r.transform.localPosition.y},layer=r.sortingLayerName,order=r.sortingOrder}).ToArray()}).ToArray(),
                statusVisuals=FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.name.StartsWith("Status_")).Select(r=>new{r.name,sprite=r.sprite?.name,position=new[]{r.transform.position.x,r.transform.position.y},layer=r.sortingLayerName,order=r.sortingOrder}).ToArray(),
                time=Time.time,timeScale=Time.timeScale,
                monsters=FindObjectsByType<Scripts.Monster.Monster>(FindObjectsSortMode.None).Where(m=>m.isActiveAndEnabled && m.MonAction!=eMonsterAction.Dead).Select(m=>new{type=m.Type.ToString(),m.BalanceReward,m.IsBalanceBoss,m.FacingDir,tauntOwner=m.TauntOwner?.PlayerIndex,position=new[]{m.transform.position.x,m.transform.position.y,m.transform.position.z},hp=m.GetHpRatio(),action=m.MonAction.ToString(),colliders=m.GetComponentsInChildren<Collider2D>().Select(c=>c.enabled).ToArray()}).ToArray(),
                visualBounds=FindObjectsByType<Scripts.Monster.Monster>(FindObjectsSortMode.None).Where(m=>m.isActiveAndEnabled && m.MonAction!=eMonsterAction.Dead).Select(m=>{
                    var renderer=m.GetComponentInChildren<SpriteRenderer>();var camera=Camera.main;
                    var min=renderer!=null && camera!=null?camera.WorldToViewportPoint(renderer.bounds.min):Vector3.zero;
                    var max=renderer!=null && camera!=null?camera.WorldToViewportPoint(renderer.bounds.max):Vector3.zero;
                    return new {type=m.Type.ToString(),min=new[]{min.x,min.y},max=new[]{max.x,max.y}};}).ToArray(),
                memory=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),meanFrameMs=_frames>0?_totalMs/_frames:0,maxFrameMs=_maxMs,frameCount=_frames,
                environment=background==null?null:new {background.CurrentPoolId,background.CurrentPresetId,renderers=background.GetComponentsInChildren<Renderer>().Length},
                catalogHash=StageCatalogRules.Database.CatalogHash,
                lastError=LocalProgression.LastError};
        }
        private void Write(string id,object data)=>File.WriteAllText(Path.Combine(_directory,"balance-"+id+".json"),JsonConvert.SerializeObject(data,Formatting.Indented));
    }
}
#endif
