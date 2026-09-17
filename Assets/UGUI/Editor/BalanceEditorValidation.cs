using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using KingdomIdle.Balance;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
public static class BalanceEditorValidation
{
    public static void BuildAndroid() { Run(); KingdomIdle.UGUI.Editor.TitleLobbyDeviceBuild.Build(); }
    public static void BuildAndroidForManualTesting()
    {
        Run();
        KingdomIdle.UGUI.Editor.MagePolishPreparation.BakeAuthoredGlyphs();
        KingdomIdle.UGUI.Editor.TitleLobbyDeviceBuild.BuildForManualTesting();
    }
    public static void Run()
    {
        string output="Recordings/BalanceRevision/Editor";Directory.CreateDirectory(output);
        try
        {
            var report=BalanceAcceptance.Run();
            var jobs=AssetDatabase.FindAssets("t:JobData",new[]{"Assets/_Project"}).Select(x=>AssetDatabase.LoadAssetAtPath<JobData>(AssetDatabase.GUIDToAssetPath(x))).ToArray();
            if(jobs.Length!=7)throw new Exception("Expected 7 jobs.");
            var errors=new List<string>();int references=0;
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/UGUI/Prefabs","Assets/_Project"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach(string path in paths)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach(var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if(component==null){errors.Add(path+": missing script");continue;}
                    var serialized=new SerializedObject(component);var property=serialized.GetIterator();
                    while(property.NextVisible(true))if(property.propertyType==SerializedPropertyType.ObjectReference){references++;if(property.objectReferenceValue==null && property.objectReferenceInstanceIDValue!=0)errors.Add(path+": "+property.propertyPath);}
                }
            }
            var stages=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/_Project/Resources/StageDatabaseSO.asset");
            var rows=new SerializedObject(stages).FindProperty("_stages");if(rows.arraySize!=43)throw new Exception("Expected 43 explicit stages.");
            var weapons=AssetDatabase.LoadAssetAtPath<EquipmentDatabase>("Assets/_Project/Scripts/Player/Equipment/Prefab/Equipment.asset").equipmentList.ToArray();
            if(weapons.Length!=18 || weapons.Count(x=>x.rarity==eEquipmentRarity.Normal)!=9 || weapons.Count(x=>x.rarity==eEquipmentRarity.Rare)!=6)throw new Exception("Equipment catalog mismatch.");
            foreach(var item in weapons){var instance=new EquipmentInstance(item);if(instance.GetFinalAtk()!=item.bonusAtk)throw new Exception("Equipment preview mismatch: "+item.name);}
            foreach(var item in weapons)foreach(string job in new[]{"Knight","Archer","Mage"})
                if(item.IsAllowedForJob(job)!=item.IsAllowedForJob("Elite_"+job))throw new Exception("Promotion lost weapon-family eligibility: "+item.name+" / "+job);
            report["equipmentFamilyPairsChecked"]=weapons.Length*3;
            var addresses=UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.groups.Where(g=>g!=null).SelectMany(g=>g.entries).ToDictionary(e=>e.address,e=>e.AssetPath);
            var usedMonsters=new HashSet<string>();
            for(int i=0;i<rows.arraySize;i++)
            {
                var entries=rows.GetArrayElementAtIndex(i).FindPropertyRelative("_monsterEntries");
                for(int j=0;j<entries.arraySize;j++)
                {
                    string key=((Scripts.Core.eMonsterType)(ulong)entries.GetArrayElementAtIndex(j).FindPropertyRelative("_monsterTypeValue").longValue).ToString();
                    usedMonsters.Add(key);
                    if(!addresses.TryGetValue(key,out var path) || AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<Scripts.Monster.Monster>()==null)
                        throw new Exception("Unresolvable stage monster address: "+key);
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var animator=prefab.GetComponentInChildren<Animator>(true);
                    if(animator==null || animator.runtimeAnimatorController==null || new SerializedObject(prefab.GetComponent<Scripts.Monster.Monster>()).FindProperty("_AnimationClipSO").objectReferenceValue==null)
                        throw new Exception("Incomplete monster animation wiring: "+key);
                }
            }
            report["monsterAddresses"]=usedMonsters.ToArray();
            report["jobs"]=jobs.Select(x=>new{x.jobName,x.atk,x.maxHP,interval=x.basicAttack.cooldown}).ToArray();report["stageCount"]=rows.arraySize;report["equipmentCount"]=weapons.Length;report["serializedReferencesChecked"]=references;report["missingReferences"]=errors;
            File.WriteAllText(output+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));
            if(errors.Count>0)throw new Exception("Missing serialized references: "+errors.Count);
            if(File.Exists(output+"/failure.txt"))File.Delete(output+"/failure.txt");
            Debug.Log("BALANCE VALIDATION PASSED "+report["passed"]);
        }
        catch(Exception error){File.WriteAllText(output+"/failure.txt",error.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
