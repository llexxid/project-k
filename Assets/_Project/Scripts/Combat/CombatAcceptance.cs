#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using UnityEngine;

namespace KingdomIdle.Combat
{
    public static class CombatAcceptance
    {
        // Only called by the isolated QA player. Restart the stage after this destructive fixture.
        public static object RunLiveControl()
        {
            var checks=new List<string>();
            void Check(bool ok,string name) { if(!ok)throw new InvalidOperationException(name);checks.Add(name); }
            var players=CombatMotion.Players.Where(p=>p!=null && !p.IsDead).OrderBy(p=>p.PlayerIndex).ToArray();
            var monster=CombatMotion.Monsters.First(m=>m!=null && m.MonAction!=Scripts.Core.eMonsterAction.Dead);
            var knight=players[0];var other=players[1];var mage=players[2];
            monster.gameObject.SetActive(false);monster.gameObject.SetActive(true);
            Check(monster.TryTaunt(knight) && monster.TauntOwner==knight,"First living taunt owner acquired");
            Check(!monster.TryTaunt(other) && monster.TauntOwner==knight,"Second taunt cannot overwrite owner");
            monster.SetTarget(other);
            Check(ReferenceEquals(monster.Target,knight),"Ordinary retarget cannot override taunt");
            knight.TakeDamage(new ActiveSkill.DamageProxy(ulong.MaxValue,other));
            Check(knight.IsDead && !monster.HasTaunt,"Owner death releases taunt");
            knight.Revive();
            Check(!monster.HasTaunt,"Revived instance cannot reclaim old taunt");
            Check(monster.TryTaunt(other),"New taunt accepted after former owner died");
            monster.gameObject.SetActive(false);monster.gameObject.SetActive(true);
            Check(!monster.HasTaunt,"Pool disable clears taunt ownership");
            knight.GrantShield(100,5);
            float hp=knight.HPRatio;
            knight.TakeDamage(new ActiveSkill.DamageProxy(40,other));
            Check(knight.ShieldHP==60 && Mathf.Approximately(hp,knight.HPRatio),"Shield absorbs damage before health");
            mage.transform.position=Vector3.zero;monster.transform.position=new Vector3(.6f,0,0);
            new EnergyPulse(mage,null,1,12,2,0).Execute();
            var cc=monster.GetComponent<MonsterCCState>();
            Check(monster.IsBalanceBoss ? cc==null || cc.DiagnosticKind!="Stun" : cc!=null && cc.DiagnosticKind=="Stun",
                monster.IsBalanceBoss ? "Boss immune to EnergyPulse stun" : "Ordinary enemy stunned by EnergyPulse");
            return new{passed=checks.Count,checks,boss=monster.IsBalanceBoss};
        }

        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool ok,string name) { if(!ok)throw new InvalidOperationException(name);checks.Add(name); }
            Check(!CombatMotion.InFront(Vector2.zero,new Vector2(.2f,1.2f),1,1),"Vertical enemy is outside melee lane");
            Check(!CombatMotion.InFront(Vector2.zero,new Vector2(-.5f,0),1,1),"Back-facing melee rejected");
            Check(!CombatMotion.InFront(Vector2.zero,new Vector2(.3f,0),1,1),"Overlapping bodies must reposition before attacking");
            Check(CombatMotion.InFront(Vector2.zero,new Vector2(.8f,.1f),1,1),"Aligned forward melee accepted");
            var flank=CombatMotion.Approach(new Vector2(2.4f,2),new Vector2(2.48f,0),1,0);
            Check(flank.x<2.48f && Mathf.Abs(flank.y)<CombatMotion.MeleeLane,"Border target has accessible flank");

            string account="combat-acceptance-"+Guid.NewGuid().ToString("N");
            LocalProgression.OpenTestAccount(account);
            Check(LocalProgression.Execute("seed",s=>{
                s.Wallet[eCurrency.Gold]=100;s.Equipment.Add(new EquipmentSave{Id="copy-test"});
                s.MageSkills[0]=new MageSave();s.UnlockedJobs[0]=new HashSet<string>{"Spearman"};
                s.PendingQuests["copy-test"]=new QuestPending{ExpiresUtc=LocalProgression.UtcNow+86400,Gold=1};return true;
            }),"Durable isolated fixture");
            var state=LocalProgression.State;var copy=state.DeepClone();
            Check(JsonConvert.SerializeObject(state)==JsonConvert.SerializeObject(copy),"Deep clone preserves every serialized field");
            Check(typeof(ProgressionState).GetFields().Where(f=>!f.FieldType.IsValueType && f.FieldType!=typeof(string)).All(f=>!ReferenceEquals(f.GetValue(state),f.GetValue(copy))),"All mutable top-level collections copied");
            copy.Equipment[0].Level=10;copy.MageSkills[0].Enhance=10;copy.UnlockedJobs[0].Add("Knight");copy.PendingQuests["copy-test"].Gold=5;
            Check(state.Equipment[0].Level==0 && state.MageSkills[0].Enhance==0 && !state.UnlockedJobs[0].Contains("Knight") && state.PendingQuests["copy-test"].Gold==1,"Nested draft mutations cannot leak");
            LocalProgression.RecordSkillCast(0);LocalProgression.RecordSkillCast(1);LocalProgression.RecordSkillCast(1);
            LocalProgression.TickSkillCounters(); // Write an immutable snapshot on a worker.
            LocalProgression.RecordSkillCast(2); // A newer event arrives during that write.
            Check(LocalProgression.Execute("credit-during-autosave",s=>{LocalProgression.Credit(s,eCurrency.Gold,7);return true;}),"Economic transaction orders behind pending autosave");
            string total="L|"+(int)eQuestObjectiveType.SkillCast+"|0";
            Check(LocalProgression.State.Counters[total]==4 && LocalProgression.Balance(eCurrency.Gold)==107,"In-flight save neither loses events nor overwrites currency");
            LocalProgression.FlushSkillCounters();LocalProgression.OpenTestAccount(account);
            Check(LocalProgression.State.Counters[total]==4 && LocalProgression.Balance(eCurrency.Gold)==107,"Counters and economic transaction survive reopen");
            Check(!LocalProgression.TestFailedWrite(s=>{LocalProgression.Credit(s,eCurrency.Gold,50);return true;}) && LocalProgression.Balance(eCurrency.Gold)==107,"Failed write never credits an economic draft");
            return new {passed=checks.Count,checks};
        }
    }
}
#endif
