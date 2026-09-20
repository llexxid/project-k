using System;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using KingdomIdle.Gacha;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

namespace KingdomIdle.UGUI.Editor
{
    public static class MageRevisionPreparation
    {
        const string Output = "Recordings/MageRevision/Editor";
        public static void Prepare()
        {
            Directory.CreateDirectory(Output);
            MageSkillAssetPreparation.Build();
            PlayabilityRevisionPreparation.CreateManualHud();
            F.Init(); F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            var detail = MageTowerDetailPopupPrefabGens.GenerateMageTowerDetailPopup();
            string path = AssetDatabase.GetAssetPath(detail);
            var root = PrefabUtility.LoadPrefabContents(path);
            try { UguiPolishPass.ApplyTo(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // Only the explicitly retired spell's runtime assets; historical source records remain intact.
            foreach (string retired in new[] {
                "Assets/MageTower/SO/GaleBlades",
                "Assets/_Project/Prefabs/VFX/MageTower/GaleBlades.prefab",
                "Assets/_Project/Art/Icons/MageTower/GaleBlades.png",
                "Assets/_Project/Art/Icons/MageTower/GaleBlades_Bloom.png" })
                if (AssetDatabase.LoadMainAssetAtPath(retired) != null) AssetDatabase.DeleteAsset(retired);
            MagePolishPreparation.BakeAuthoredGlyphs();
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            AssetDatabase.SaveAssets();
            Validate();
        }
        public static void Validate()
        {
            Directory.CreateDirectory(Output);
            var roster = AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>("Assets/MageTower/SO/MageTowerSkillList.asset").skills;
            if (!MageSkillRules.ValidateRoster(roster)) throw new Exception("Roster invalid");
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Huds/Hud_MageTowerEnv.prefab").GetComponent<MageManualCastHud>();
            if (hud.buttons.Any(b => b.frame == null || b.visibility == null || b.cooldownLabel == null || b.cooldown.sprite == null || b.cooldown.fillMethod != Image.FillMethod.Radial360 || b.cooldown.fillClockwise)) throw new Exception("Manual HUD not serialized");
            var detail = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Popups/Panel_MageTowerDetail.prefab").GetComponent<MageTowerDetailPopupView>();
            if (detail.awakeningEffects == null || detail.nextAwakening == null) throw new Exception("Awakening labels missing");
            var tables=AssetDatabase.FindAssets("t:GachaTableSO",new[]{"Assets/Gacha"}).Select(g=>AssetDatabase.LoadAssetAtPath<GachaTableSO>(AssetDatabase.GUIDToAssetPath(g))).Where(t=>t.gachaType==eGachaType.Skill);
            foreach(var table in tables)
            {
                var rewards=table.rewards.Where(r=>r.rewardType==eGachaRewardType.Skill).ToArray();
                if(rewards.Length!=9 || rewards.Any(r=>r.skillId==6) || rewards.Any(r=>Mathf.Abs(r.weight-rewards[0].weight)>.001f) || Mathf.Abs(rewards.Sum(r=>r.weight)/table.rewards.Sum(r=>r.weight)-.5f)>.001f)throw new Exception("Skill draw odds changed");
            }
            MagePolishPreparation.Validate();
            BalanceEditorValidation.Run();
            var prefabs=roster.SelectMany(s=>new[]{s.prefab,s.secondaryPrefab,s.castingPrefab,s.bloomPrefab,s.bloomCastingPrefab}).Where(p=>p!=null).Distinct().ToArray();
            var audit=prefabs.SelectMany(p=>p.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.sprite!=null).Select(r=>new {
                prefab=p.name, layer=r.name, source=AssetDatabase.GetAssetPath(r.sprite), rect=new { x=r.sprite.rect.x, y=r.sprite.rect.y, width=r.sprite.rect.width, height=r.sprite.rect.height },
                worldPixelX=Mathf.Abs(r.transform.lossyScale.x)/r.sprite.pixelsPerUnit,
                worldPixelY=Mathf.Abs(r.transform.lossyScale.y)/r.sprite.pixelsPerUnit,
                filter=r.sprite.texture.filterMode.ToString(), texture=r.sprite.texture.width+"x"+r.sprite.texture.height
            })).ToArray();
            File.WriteAllText(Output+"/pixel-audit.json",JsonConvert.SerializeObject(audit,Formatting.Indented));
            File.WriteAllText(Output+"/catalog.json","["+string.Join(",",roster.Select(s=>JsonUtility.ToJson(s,true)))+"]");
            File.WriteAllText(Output+"/validation.json",JsonConvert.SerializeObject(new{passed=true, ids=roster.Select(s=>s.id), hudSlots=hud.buttons.Length, radius=roster.Single(s=>s.id==9).PullRadius, checks="serialized HUD, clockwise clearance, detail wiring, retired roster, equal odds/50% skill category, balance/reference validation"},Formatting.Indented));
            Debug.Log("MAGE REVISION PREPARATION PASSED");
        }
        public static void BuildDevice() { Prepare(); TitleLobbyDeviceBuild.Build(); }
        public static void BuildFinal() { Validate(); TitleLobbyDeviceBuild.BuildForManualTesting(); }
    }
}
