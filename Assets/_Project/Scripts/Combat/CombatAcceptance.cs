#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using UnityEngine;

namespace KingdomIdle.Combat
{
    public static class CombatAcceptance
    {
        public static IEnumerator RunLayeredControl(Action<object> completed)
        {
            var checks = new List<string>(); var failed = new List<string>();
            void Check(bool ok, string name) { checks.Add(name); if (!ok) failed.Add(name); }
            var monster = CombatMotion.Monsters.Where(m => m != null && m.MonAction != Scripts.Core.eMonsterAction.Dead)
                .OrderByDescending(m => m.MaxHp).First();
            monster.gameObject.SetActive(false); monster.gameObject.SetActive(true);
            MonsterCCState.Apply(monster, CrowdControlKind.Slow, 1.2f, .25f);
            MonsterCCState.Apply(monster, CrowdControlKind.Stun, .35f, 0);
            MonsterCCState.Apply(monster, CrowdControlKind.Slow, .7f, .1f);
            var cc = monster.GetComponent<MonsterCCState>();
            Check(cc.DiagnosticKind == "Stun" && Mathf.Approximately((float)monster.SpeedMultiplier, .75f), "Stun and strongest slow coexist");
            Check(monster.MonAction == Scripts.Core.eMonsterAction.Idle, "Stun cancels the visible attack pose");
            yield return new WaitForSeconds(.45f);
            Check(cc.DiagnosticKind == "Slow" && Mathf.Approximately((float)monster.SpeedMultiplier, .75f), "Original poison survives stun expiry");
            yield return new WaitForSeconds(.85f);
            Check(!cc.enabled && monster.SpeedMultiplier == 1, "Control expires without a permanent movement penalty");
            MonsterCCState.Apply(monster, CrowdControlKind.Slow, .25f, .4f);
            MonsterCCState.Apply(monster, CrowdControlKind.Slow, .8f, .2f);
            Check(Mathf.Approximately((float)monster.SpeedMultiplier, .6f), "Weaker slow cannot overwrite stronger slow");
            yield return new WaitForSeconds(.35f);
            Check(Mathf.Approximately((float)monster.SpeedMultiplier, .8f), "Longer weaker slow resumes after stronger expiry");
            monster.gameObject.SetActive(false); monster.gameObject.SetActive(true);
            Check(monster.SpeedMultiplier == 1 && !cc.enabled, "Pool release clears every control clock");
            var prefab = KingdomIdle.MageTower.MageTowerManager.Instance.GetSkillById(0).prefab;
            var first = PooledSpellVfx.Spawn(prefab, Vector3.zero, .5f); int generation = first.SpawnGen;
            first.Release(); var second = PooledSpellVfx.Spawn(prefab, Vector3.zero, .5f);
            first.Release(generation);
            Check(second.gameObject.activeSelf && second.SpawnGen != generation, "Old cast cannot release a reused visual");
            second.Release();
            Check(KingdomIdle.MageTower.MageTowerManager.Instance.GetSkillById(0).bloomRadius == 2.1f, "Thunder radius is a catalog value independent of display width");
            completed(new { passed = failed.Count == 0, count = checks.Count, checks, failed });
        }
        // Only called by the isolated QA player. Restart the stage after this destructive fixture.
        public static IEnumerator RunLiveControl(Action<object> completed)
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
            var pulse = new EnergyPulse(mage,null,1,12,10,0);
            pulse.Execute();
            yield return new WaitForSeconds(.21f);
            pulse.Tick();
            var cc=monster.GetComponent<MonsterCCState>();
            Check(monster.IsBalanceBoss ? cc==null || cc.DiagnosticKind!="Stun" : cc!=null && cc.DiagnosticKind=="Stun",
                monster.IsBalanceBoss ? "Boss immune to EnergyPulse stun" : "Ordinary enemy stunned by EnergyPulse");
            var start = monster.transform.position;
            yield return new WaitForSeconds(.3f);
            Check(monster.IsBalanceBoss || monster.transform.position.x > start.x + .65f,"Pulse visibly displaces ordinary enemies");
            completed(new{passed=checks.Count,checks,boss=monster.IsBalanceBoss});
        }

        public static object RunHealth()
        {
            var rows = new List<object>();
            foreach (var player in CombatMotion.Players.ToArray())
            {
                player.Revive();
                long maximum = player.playerStatus.MaxHP;
                // Reproduce a stat/job clamp without touching the legacy PlayerData HP.
                player.playerStatus.HP = maximum / 2;
                player.TakeDamage(new ActiveSkill.DamageProxy(1, null));
                if (player.playerStatus.HP != maximum / 2 - 1 ||
                    Mathf.Abs(player.HPRatio - (float)player.playerStatus.HP / maximum) > .0001f)
                    throw new InvalidOperationException("Live HP and displayed HP diverged for " + player.playerStatus.JobName);
                player.Heal(long.MaxValue);
                if (player.playerStatus.HP != maximum) throw new InvalidOperationException("Healing exceeded maximum HP");
                rows.Add(new { job = player.playerStatus.JobName, maximum, passed = true });
                player.Revive();
            }
            return new { passed = true, rows };
        }

        public static object Run()
        {
            using var session = LocalProgression.BeginTestSession();
            var checks = new List<string>();
            void Check(bool ok,string name) { if(!ok)throw new InvalidOperationException(name);checks.Add(name); }
            Check(!CombatMotion.InFront(Vector2.zero,new Vector2(.2f,1.2f),1,1),"Vertical enemy is outside melee lane");
            Check(!CombatMotion.InFront(Vector2.zero,new Vector2(-.5f,0),1,1),"Back-facing melee rejected");
            Check(!CombatMotion.InFront(Vector2.zero,new Vector2(.3f,0),1,1),"Overlapping bodies must reposition before attacking");
            Check(CombatMotion.InFront(Vector2.zero,new Vector2(.8f,.1f),1,1),"Aligned forward melee accepted");
            Check(CombatMotion.InFront(Vector2.zero,new Vector2(.8f,.19f),1,1,CombatMotion.MeleeImpactLane),"Committed swing tolerates tiny separation drift");
            Check(!CombatMotion.InFront(Vector2.zero,new Vector2(.8f,.5f),1,1,CombatMotion.MeleeImpactLane),"Impact tolerance still rejects a different combat lane");
            var flank=CombatMotion.Approach(new Vector2(2.4f,2),new Vector2(2.48f,0),1,0);
            Check(flank.x<2.48f && Mathf.Abs(flank.y)<CombatMotion.MeleeLane,"Border target has accessible flank");

            string account="combat-acceptance-"+Guid.NewGuid().ToString("N");
            LocalProgression.OpenTestAccount(account);
            var daily = QuestCatalog.Instance.Get(20003);
            Check(LocalProgression.Execute("seed",s=>{
                s.Wallet[eCurrency.Gold]=100;s.Equipment.Add(new EquipmentSave{Id="copy-test"});
                s.MainClears.Add(0x20001000B);
                s.MageSkills[0]=new MageSave();s.UnlockedJobs[0]=new HashSet<string>{"Spearman"};
                s.CompletedQuests.Add(QuestCatalog.Instance.Definitions.First().QuestId);
                QuestEconomy.Count(s,eQuestObjectiveType.BattleTime,0,daily.RequiredCount);return true;
            }),"Durable isolated fixture");
            var state=LocalProgression.State;var copy=state.DeepClone();
            string pendingKey=QuestEconomy.Key(daily,state);
            Check(JsonConvert.SerializeObject(state)==JsonConvert.SerializeObject(copy),"Deep clone preserves every serialized field");
            Check(typeof(ProgressionState).GetFields().Where(f=>!f.FieldType.IsValueType && f.FieldType!=typeof(string)).All(f=>!ReferenceEquals(f.GetValue(state),f.GetValue(copy))),"All mutable top-level collections copied");
            long rewardAmount=state.PendingQuests[pendingKey].Rewards[0].Amount;
            copy.Equipment[0].Level=10;copy.MageSkills[0].Enhance=10;copy.UnlockedJobs[0].Add("Knight");copy.PendingQuests[pendingKey].Gold=5;
            copy.CompletedQuests.Clear();copy.PendingQuests[pendingKey].Rewards[0].Amount++;copy.PendingQuests[pendingKey].Rewards.Clear();
            Check(state.Equipment[0].Level==0 && state.MageSkills[0].Enhance==0 && !state.UnlockedJobs[0].Contains("Knight") && state.PendingQuests[pendingKey].Gold==0,"Nested draft mutations cannot leak");
            Check(state.CompletedQuests.Count>0 && state.PendingQuests[pendingKey].Rewards[0].Amount==rewardAmount,
                "Completed quests, pending reward lists and reward items are independent copies");
            LocalProgression.RecordSkillCast(0);LocalProgression.RecordSkillCast(1);LocalProgression.RecordSkillCast(1);
            LocalProgression.TickSkillCounters(); // Write an immutable snapshot on a worker.
            LocalProgression.RecordSkillCast(2); // A newer event arrives during that write.
            Check(LocalProgression.Execute("credit-during-autosave",s=>{LocalProgression.Credit(s,eCurrency.Gold,7);return true;}),"Economic transaction orders behind pending autosave");
            string total="L|"+(int)eQuestObjectiveType.SkillCast+"|0";
            Check(LocalProgression.State.Counters[total]==4 && LocalProgression.Balance(eCurrency.Gold)==107,"In-flight save neither loses events nor overwrites currency");
            LocalProgression.FlushSkillCounters();LocalProgression.OpenTestAccount(account);
            Check(LocalProgression.State.Counters[total]==4 && LocalProgression.Balance(eCurrency.Gold)==107,"Counters and economic transaction survive reopen");
            Check(!LocalProgression.TestFailedWrite(s=>{LocalProgression.Credit(s,eCurrency.Gold,50);return true;}) && LocalProgression.Balance(eCurrency.Gold)==107,"Failed write never credits an economic draft");
            int notifications=0;
            Action onChanged=()=>notifications++;
            LocalProgression.Changed+=onChanged;
            try
            {
                LocalProgression.RecordSkillCast(0);
                Check(notifications==0,"Skill collection does not publish before saving");
                // 저장 간격을 기다리는 대신 실제 worker 저장을 시작해 거절 거래와의 순서를 재현한다.
                typeof(LocalProgression).GetField("_nextCounterSave",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).SetValue(null,0f);
                LocalProgression.TickSkillCounters();
                Check(!LocalProgression.Execute("rejected-after-autosave",_=>false) && notifications==1,
                    "Successful autosave still notifies when the following transaction is rejected");
                LocalProgression.RecordSkillCast(0);
                Check(LocalProgression.FlushSkillCounters() && notifications==2,
                    "Explicit counter flush publishes its saved revision exactly once");
            }
            finally { LocalProgression.Changed-=onChanged; }
            return new {passed=checks.Count,checks};
        }
    }
}
#endif
