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
            Check(MageSkillRules.ValidateRoster(skills), "Eight stable IDs, retired IDs excluded");
            Check(mage.GetSkillById(6) == null && !mage.Equip(0,6), "Retired skill cannot be found or equipped");
            Check(mage.GetSkillById(8) == null && !mage.Equip(0,8), "Standalone meteor cannot be found or equipped");
            var rift = mage.GetSkillById(9);
            Check(Mathf.Approximately(rift.PullRadius, rift.radius * 1.5f) && Mathf.Approximately(rift.TargetRadius(false), rift.PullRadius), "Void suction and aiming radius extend fifty percent beyond damage rim");
            var counts = new int[MageSkillRules.IdCapacity];
            for (int ticket=0; ticket<skills.Count; ticket++) counts[MageSkillRules.SelectSkill(skills, ticket).id]++;
            Check(counts.SequenceEqual(new[] {1,1,1,1,1,1,0,1,0,1}), "Exhaustive conditional draw partition and stable IDs");
            Check(skills.All(s=>s.icon!=null && s.prefab!=null && s.basePower>0 && s.baseCooldown>0), "Every skill has art and usable base parameters");
            Check(skills[0].bloomCooldownMultiplier==2 && skills[1].bloomControlDuration==0, "Authored lightning cooldown; ice bloom does not stun");
            Check(skills.Where(s=>s.id!=0 && s.id!=1 && s.id!=3).All(s=>s.bloomName=="미정" && Mathf.Approximately(s.bloomPowerMultiplier,1.15f)), "Unfinished bloom identities remain explicit with modest coefficient");
            var starfall = mage.GetSkillById(3);
            Check(starfall.bloomName=="메테오" && starfall.bloomPrefab!=null && starfall.bloomSecondaryPrefab!=null && starfall.bloomSlowFraction==.6f && starfall.bloomDuration==3.5f, "Meteor bloom art and field contract");
            Check(!starfall.CanAimWithBloom(false) && starfall.CanAimWithBloom(true), "Only meteor bloom supports ground dragging");
            foreach (var skill in skills) Check(skill.bloomIcon!=null && skill.DisplayIcon(true)!=skill.DisplayIcon(false), "Distinct bloom icon: "+skill.id);
            var merge = new ProgressionState();
            merge.MageSkills[3]=new MageSave{Enhance=4,Awaken=3,Fragments=10,Spent=43};
            merge.MageSkills[8]=new MageSave{Enhance=7,Awaken=10,Fragments=13,Spent=78};
            merge.MageSlots[0]=8;
            Check(MageCatalogMigration.Apply(merge), "Meteor migration applies once");
            LocalProgression.Validate(merge);
            Check(merge.MageSkills[3].Enhance==7 && merge.MageSkills[3].Awaken==10 && merge.MageSkills[3].Spent==78 && merge.MageSkills[3].Fragments==59 && merge.Wallet[eCurrency.ArcaneKnowledge]==43 && merge.MageSlots[0]==3 && merge.MageSkills[3].BloomEnabled && !merge.MageSkills.ContainsKey(8), "Higher growth retained, duplicate spend/fragments refunded, slot and bloom preserved");
            Check(!MageCatalogMigration.Apply(merge) && merge.Wallet[eCurrency.ArcaneKnowledge]==43, "Meteor migration is idempotent");
            var duplicateSlots=new ProgressionState();duplicateSlots.MageSkills[3]=new MageSave();duplicateSlots.MageSkills[8]=new MageSave();duplicateSlots.MageSlots[0]=3;duplicateSlots.MageSlots[1]=8;
            MageCatalogMigration.Apply(duplicateSlots);LocalProgression.Validate(duplicateSlots);
            Check(duplicateSlots.MageSlots[0]==3 && duplicateSlots.MageSlots[1]==-1 && duplicateSlots.MageSkills[3].Fragments==30, "Two equipped sources never produce duplicate slots");
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

            string migrationAccount = "mage-retirement-" + Guid.NewGuid().ToString("N");
            LocalProgression.OpenTestAccount(migrationAccount);
            Check(LocalProgression.Execute("qa-retired-save", s => {
                s.MageSkills[6] = new MageSave { Enhance = 7, Awaken = 4, Fragments = 13, Spent = 70 };
                s.MageSkills[9] = new MageSave { Enhance = 3 };
                s.MageSkills[3] = new MageSave { Enhance = 4, Awaken = 3, Fragments = 10, Spent = 43 };
                s.MageSkills[8] = new MageSave { Enhance = 7, Awaken = 10, Fragments = 13, Spent = 78 };
                s.Wallet[eCurrency.ArcaneKnowledge] = 0;
                s.MageSlots[0] = 6; s.MageSlots[1] = 9; s.MageSlots[2] = 8; return true;
            }), "Legacy loadout fixture saved");
            LocalProgression.OpenTestAccount(migrationAccount);
            Check(LocalProgression.State.MageSlots[0] == -1 && LocalProgression.State.MageSlots[1] == 9 &&
                LocalProgression.State.MageSkills[6].Spent == 70 && LocalProgression.State.MageSkills[6].Fragments == 13 &&
                LocalProgression.State.MageSkills[9].Enhance == 3, "Retirement preserves investment and other IDs, clears only retired slot");
            var diskMerge=JsonConvert.DeserializeObject<ProgressionState>(File.ReadAllText(LocalProgression.SnapshotPath));
            Check(diskMerge.MageSkills[3].Enhance==7 && diskMerge.MageSkills[3].Awaken==10 && diskMerge.MageSkills[3].BloomEnabled &&
                diskMerge.MageSkills[3].Fragments==59 && diskMerge.Wallet[eCurrency.ArcaneKnowledge]==43 && diskMerge.MageSlots[2]==3 &&
                !diskMerge.MageSkills.ContainsKey(8) && diskMerge.Modules.ContainsKey(MageCatalogMigration.MeteorArchive), "Opening an old save durably commits the meteor merge and audit archive");
            long migratedRevision = LocalProgression.State.Revision;
            LocalProgression.OpenTestAccount(migrationAccount);
            Check(LocalProgression.State.Revision == migratedRevision, "Retirement migration is idempotent");
            Check(LocalProgression.State.Wallet[eCurrency.ArcaneKnowledge]==43 && LocalProgression.State.MageSkills[3].Fragments==59, "Reopening never duplicates the meteor refund");

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
                Check(skills.All(skill=>mage.GetFragments(skill.id)==before[skill.id]+expected[skill.id]),"Every per-skill fragment total reconciles with committed results");
                Check(skillRewards>0 && knowledge>0,"Seed exercises both reward categories");
            }
            finally { UnityEngine.Random.state=rng; }
            return new { passed=true, count=checks.Count, checks };
        }
    }
}
#endif
