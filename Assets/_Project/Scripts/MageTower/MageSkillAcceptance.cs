#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Gacha;
using Newtonsoft.Json;
using UnityEngine;

namespace KingdomIdle.MageTower
{
    public static class MageSkillAcceptance
    {
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool value, string label) { if (!value) throw new InvalidOperationException("MAGE ASSERT: " + label); checks.Add(label); }
            var mage = MageTowerManager.Instance;
            Check(mage != null && GachaManager.Instance != null, "Live managers available");
            var skills = mage.GetAllSkills();
            Check(MageSkillRules.ValidateRoster(skills), "Ten unique stable IDs");
            var counts = new int[10];
            for (int ticket=0; ticket<10; ticket++) counts[MageSkillRules.SelectSkill(skills, ticket).id]++;
            Check(counts.SequenceEqual(new[] {1,1,1,1,1,1,1,1,1,1}), "Exhaustive conditional draw partition and stable IDs");
            Check(skills.All(s=>s.icon!=null && s.prefab!=null && s.basePower>0 && s.baseCooldown>0), "Every skill has art and usable base parameters");
            Check(skills[0].bloomCooldownMultiplier==2 && skills[1].bloomControlDuration==2, "Authored lightning cooldown and ice stun contract");
            Check(skills.Skip(2).All(s=>s.bloomName=="미정" && Mathf.Approximately(s.bloomPowerMultiplier,1.15f)), "Unfinished bloom identities remain explicit with modest coefficient");
            var old = JsonConvert.DeserializeObject<MageSave>("{\"Enhance\":7,\"Awaken\":4,\"Fragments\":13,\"Spent\":70}");
            Check(old.Enhance==7 && old.Awaken==4 && old.Fragments==13 && old.Spent==70 && !old.BloomEnabled, "Legacy snapshot retains progress and defaults bloom off");
            var draft = new ProgressionState();
            foreach (var skill in skills)
            {
                MageTowerManager.Grant(draft, skill.id, MageSkillRules.DuplicateFragments);
                Check(draft.MageSkills[skill.id].Fragments==0, "First unlock is not a duplicate: " + skill.id);
                MageTowerManager.Grant(draft, skill.id, MageSkillRules.DuplicateFragments);
                MageTowerManager.Grant(draft, skill.id);
                Check(draft.MageSkills[skill.id].Fragments==MageSkillRules.DuplicateFragments+1, "Duplicate yield and raw first-clear fragment remain separate: " + skill.id);
            }
            LocalProgression.Validate(draft);
            draft.MageSkills[9].BloomEnabled=true;
            bool rejected=false;try { LocalProgression.Validate(draft); } catch (InvalidDataException) { rejected=true; }
            Check(rejected, "Locked bloom is rejected in snapshots");
            draft.MageSkills[9].Awaken=10; LocalProgression.Validate(draft);
            draft.MageSkills[10]=new MageSave();
            rejected=false;try { LocalProgression.Validate(draft); } catch (InvalidDataException) { rejected=true; }
            Check(rejected, "Unknown future skill ID requires a catalog migration");
            draft.MageSkills.Remove(10); draft.MageSkills[9].Fragments=70000;
            var export=MageSkillStateSnapshot.Capture(draft);
            Check(export.skills.Single(s=>s.id==9).fragments==70000 && export.skills.Single(s=>s.id==9).bloomEnabled, "Versioned export preserves large fragment counts and bloom");
            rejected=false;try { MageTowerSkillCode.Pack(9,10,0,70000); } catch(ArgumentOutOfRangeException) { rejected=true; }
            Check(rejected,"Legacy packed code refuses silent fragment truncation");

            // The caller restores its QA profile; this run never writes the gameplay profile.
            LocalProgression.OpenTestAccount("mage-acceptance-" + Guid.NewGuid().ToString("N"));
            Check(LocalProgression.Execute("qa-mage-fixture", s=>{
                s.Modules["imported"] = s.Modules["mage-imported"] = "1";
                s.Wallet[eCurrency.AncientCoin]=100000;
                foreach(var skill in skills)s.MageSkills[skill.id]=new MageSave{Fragments=55};
                return true;
            }), "Isolated fixture persisted");
            Check(!mage.SetBloomEnabled(0,true), "Bloom cannot unlock before awakening ten");
            for(int i=0;i<10;i++)Check(mage.Awaken(0), "Awakening step " +(i+1));
            Check(mage.GetFragments(0)==0 && !mage.Awaken(0), "Exactly 55 fragments reach the cap");
            Check(mage.SetBloomEnabled(0,true) && mage.IsBloomEnabled(0), "Bloom can be enabled at ten");
            string path=LocalProgression.SnapshotPath;
            var persisted=JsonConvert.DeserializeObject<ProgressionState>(File.ReadAllText(path));
            Check(persisted.MageSkills[0].BloomEnabled && persisted.MageSkills[0].Awaken==10, "Bloom and awakening persist together");
            Check(mage.SetBloomEnabled(0,false) && !mage.IsBloomEnabled(0), "Bloom can be disabled without losing awakening");
            long revision=LocalProgression.State.Revision;
            Check(!LocalProgression.TestFailedWrite(s=>{s.MageSkills[0].BloomEnabled=true;return true;}) && LocalProgression.State.Revision==revision && !mage.IsBloomEnabled(0), "Failed durable write does not publish bloom");

            var table=GachaManager.Instance.GetAllTables().Single(t=>t.gachaType==eGachaType.Skill);
            var rng=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(9162026);
                var before=LocalProgression.State.MageSkills.ToDictionary(p=>p.Key,p=>p.Value.Fragments);
                var expected=new int[10]; long knowledge=0; int skillRewards=0;
                for(int batch=0;batch<20;batch++)
                {
                    bool received=false;
                    GachaManager.Instance.TryPull(table,10, rewards=>{
                        received=true; Check(rewards.Count==10,"Ten results in batch "+batch);
                        foreach(var reward in rewards)
                            if(reward.rewardType==eGachaRewardType.Skill)
                            { Check(reward.amount==MageSkillRules.DuplicateFragments,"Result reports duplicate yield");expected[reward.skillId]+=reward.amount;skillRewards++; }
                            else knowledge+=reward.amount;
                    }, error=>throw new InvalidOperationException(error));
                    Check(received,"Transaction callback completed");
                }
                Check(LocalProgression.Balance(eCurrency.AncientCoin)==90000,"Two hundred pulls debit exactly ten thousand coins");
                Check(LocalProgression.Balance(eCurrency.ArcaneKnowledge)==knowledge,"Knowledge result totals reconcile");
                Check(Enumerable.Range(0,10).All(id=>mage.GetFragments(id)==before[id]+expected[id]),"Every per-skill fragment total reconciles with committed results");
                Check(skillRewards>0 && knowledge>0,"Seed exercises both reward categories");
            }
            finally { UnityEngine.Random.state=rng; }
            return new { passed=true, count=checks.Count, checks };
        }
    }
}
#endif
