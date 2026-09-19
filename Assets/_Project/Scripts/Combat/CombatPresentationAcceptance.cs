#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.MageTower;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.Combat
{
    public static class CombatPresentationAcceptance
    {
        public sealed class Report { public bool passed; public int count; public List<string> checks=new(), failed=new(); }
        public static IEnumerator Run(Action<Report> completed, Action<string> capture = null)
        {
            var report=new Report();
            void Check(bool ok,string label){report.checks.Add(label);if(!ok)report.failed.Add(label);}
            SpriteRenderer Icon(Component owner,string name)=>owner.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r=>r.name=="Status_"+name);
            bool Visible(Component owner,string name)=>Icon(owner,name)!=null && Icon(owner,name).enabled;
            var monster=CombatMotion.Monsters.Where(m=>m!=null&&m.MonAction!=eMonsterAction.Dead).OrderByDescending(m=>m.MaxHp).First();
            var party=CombatMotion.Players.Where(p=>p!=null&&!p.IsDead).OrderBy(p=>p.PlayerIndex).ToArray();
            var player=party[0];
            foreach(var p in party)p.playerOrder.InterruptBT();
            foreach(var m in CombatMotion.Monsters)m.InterruptBehaviourTree();
            monster.gameObject.SetActive(false);monster.gameObject.SetActive(true);
            MonsterCCState.Apply(monster,CrowdControlKind.Slow,1.6f,.25f,slowStyle:SlowVisualKind.Venom);
            MonsterCCState.Apply(monster,CrowdControlKind.Stun,.55f,0);
            var cc=monster.GetComponent<MonsterCCState>();
            yield return null;yield return null;
            Check(cc.IsStunned&&Mathf.Approximately((float)monster.SpeedMultiplier,.75f),"Stun and venom movement penalty apply together");
            Check(Visible(monster,"Stun")&&Visible(monster,"Slow"),"Independent stun and slow visuals coexist");
            Check(Icon(monster,"Stun").transform.position.y>monster.HeadPosition.y+.25f,"Stun indicator clears head and HP bar");
            Check(Vector3.Distance(Icon(monster,"Slow").transform.position,monster.FootPosition+Vector3.up*.38f)<.01f,"Slow ring uses stable body foot anchor");
            var initial=Icon(monster,"Stun").transform.position;monster.transform.position+=Vector3.right*.2f;
            yield return null;yield return null;
            Check(Mathf.Abs(Icon(monster,"Stun").transform.position.x-initial.x-.2f)<.01f,"Sustained indicator follows movement");
            capture?.Invoke("status-stun-venom");
            yield return new WaitForSeconds(.7f);
            Check(!cc.IsStunned && !Visible(monster,"Stun") && Visible(monster,"Slow"),"Stun visual expires while longer slow remains");
            MonsterCCState.Apply(monster,CrowdControlKind.Slow,.35f,.4f,slowStyle:SlowVisualKind.Void);
            yield return null;yield return null;
            Check(cc.SlowStyle==SlowVisualKind.Void&&Mathf.Approximately((float)monster.SpeedMultiplier,.6f),"Stronger void slow wins without multiplying slows");
            yield return new WaitForSeconds(.45f);
            Check(cc.SlowStyle==SlowVisualKind.Venom&&Mathf.Approximately((float)monster.SpeedMultiplier,.75f),"Original venom slow and color resume after stronger slow expires");
            MonsterCCState.Apply(monster,CrowdControlKind.Slow,.7f,.25f,slowStyle:SlowVisualKind.Venom);
            yield return new WaitForSeconds(.4f);
            Check(Visible(monster,"Slow"),"Reapplication extends active visual without restarting it");
            yield return new WaitForSeconds(.4f);
            Check(!cc.enabled&&monster.SpeedMultiplier==1&&!Visible(monster,"Slow"),"Final slow expiry restores speed and removes ring");
            Check(monster.TryTaunt(player),"Taunt acquires first living owner");
            player.GrantShield(100,.6f);
            MonsterCCState.Apply(monster,CrowdControlKind.Stun,2,0);
            yield return null;yield return null;
            Check(Visible(monster,"Taunt")&&Visible(player,"Shield"),"Taunt and protective shield states have live indicators");
            Check(Vector3.Distance(Icon(monster,"Taunt").transform.position,Icon(monster,"Stun").transform.position)>.4f,"Coexisting overhead indicators do not overlap");
            float hp=player.HPRatio;player.TakeDamage(new ActiveSkill.DamageProxy(40,party[1]));
            Check(player.ShieldHP==60&&Mathf.Approximately(hp,player.HPRatio),"Shield absorbs actual incoming damage");
            capture?.Invoke("status-taunt-shield");
            yield return new WaitForSeconds(.7f);
            Check(player.ShieldHP==0&&!Visible(player,"Shield"),"Shield timeout removes its visual");
            var heal=MageTowerManager.Instance.GetSkillById(7).secondaryPrefab;
            var effect=PooledSpellVfx.Spawn(heal,player.VfxFootPosition,2,player.transform,player.VfxFootPosition-player.transform.position);
            player.transform.position+=new Vector3(.25f,.1f,0);
            yield return null;yield return null;
            Check(Vector3.Distance(effect.transform.position,player.VfxFootPosition)<.01f,"Healing pulse follows the recipient's feet while moving");
            Check(effect.GetComponentInChildren<SpriteRenderer>().sortingLayerName=="Default"&&effect.GetComponentInChildren<SpriteRenderer>().sortingOrder<2,"Healing pulse stays below the recipient body");
            capture?.Invoke("healing-feet");
            player.TakeDamage(new ActiveSkill.DamageProxy(ulong.MaxValue,party[1]));
            yield return null;yield return null;
            Check(!monster.HasTaunt&&!Visible(monster,"Taunt"),"Taunt owner death clears its indicator");
            Check(!effect.gameObject.activeSelf,"Healing visual releases on recipient death");
            player.Revive();
            Check(!monster.HasTaunt,"Revival cannot restore stale taunt ownership");
            MonsterCCState.Apply(monster,CrowdControlKind.Slow,10,.25f);
            monster.gameObject.SetActive(false);monster.gameObject.SetActive(true);
            yield return null;yield return null;
            Check(!cc.IsStunned&&cc.SlowFraction==0&&!Visible(monster,"Stun")&&!Visible(monster,"Slow")&&!Visible(monster,"Taunt"),"Pool disable/reuse clears every persistent status visual");
            report.count=report.checks.Count;report.passed=report.failed.Count==0;completed(report);
        }
    }
}
#endif
