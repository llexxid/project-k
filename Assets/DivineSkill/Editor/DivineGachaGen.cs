using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KingdomIdle.Gacha;

namespace KingdomIdle.Divine.EditorTools
{
    /// <summary>
    /// 신 스킬 뽑기 테이블 생성기 + bootstrap 씬 GachaManager 배선 도구.
    /// 멱등 — 테이블 에셋은 필드만 갱신하고 GUID 를 유지하며, 씬 배선은 이미 있으면 건너뛴다.
    /// 확률은 등급 기준 고정: 영웅 80% / 전설 15% / 신화 5% (카드별 균등 분배).
    /// 비용은 Gold 10,000 — 테스트용 클라이언트 롤이며 다른 테이블처럼 서버 가챠로 마이그레이션 예정.
    /// </summary>
    public static class DivineGachaGen
    {
        private const string RegistryPath = "Assets/DivineSkill/SO/DivineSkillRegistry.asset";
        private const string TablePath = "Assets/Gacha/SO/GachaTable_DivineSkill.asset";
        private const string BootstrapScene = "Assets/_Project/Scenes/buildScenes/bootstrap.unity";
        private const string GachaManagerHostName = "GachaManager";

        // 등급별 카드 1장당 가중치 (영웅 4장 × 20 = 80% / 전설 2장 × 7.5 = 15% / 신화 2장 × 2.5 = 5%)
        private const float WeightPerHero = 20f;
        private const float WeightPerLegend = 7.5f;
        private const float WeightPerMyth = 2.5f;

        [MenuItem("KingdomIdle/Divine/Generate Gacha Table")]
        public static void GenerateTable()
        {
            var registry = AssetDatabase.LoadAssetAtPath<DivineSkillRegistrySO>(RegistryPath);
            if (registry == null || registry.cards == null || registry.cards.Count == 0)
            {
                Debug.LogError("[DivineGacha] 레지스트리가 없거나 비어 있습니다. " +
                               "먼저 'Generate Cards + Registry' 를 실행하세요.");
                return;
            }

            var table = AssetDatabase.LoadAssetAtPath<GachaTableSO>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<GachaTableSO>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            table.nameKor = "신 스킬 뽑기";
            table.nameEng = "DivineSkill";
            table.description = "여신과 마왕의 카드를 수집해 신 스킬을 획득한다.\n" +
                                "중복 카드는 레벨업 재료로 적립된다. (영웅 80% / 전설 15% / 신화 5%)";
            table.gachaType = eGachaType.DivineCard;
            // 테스트용 클라이언트 롤 비용 — 서버 가챠 마이그레이션 시 전용 재화/서버 검증으로 교체
            table.costCurrency = eCurrency.Gold;
            table.costAmount = 10000;
            table.isImplemented = true;

            // 보상은 레지스트리에서 매번 재구성한다 (아이콘은 아트 배선 후 재실행하면 따라온다)
            table.rewards.Clear();
            foreach (var card in registry.cards)
            {
                if (card == null) continue;

                table.rewards.Add(new GachaRewardEntry
                {
                    nameKor = card.nameKor,
                    icon = card.icon, // 아트 미완성 카드는 null — UI 가 등급명 텍스트로 대체 표시
                    rewardType = eGachaRewardType.DivineCard,
                    divineCardId = card.id,
                    amount = 1,
                    weight = GetWeightForGrade(card.grade),
                });
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DivineGacha] 신 스킬 뽑기 테이블 생성/갱신 완료 " +
                      $"({table.rewards.Count}종) → {TablePath}");
        }

        private static float GetWeightForGrade(eDivineGrade grade)
        {
            switch (grade)
            {
                case eDivineGrade.Myth: return WeightPerMyth;
                case eDivineGrade.Legend: return WeightPerLegend;
                default: return WeightPerHero;
            }
        }

        [MenuItem("KingdomIdle/Divine/Wire Gacha Table Into Scene")]
        public static void WireTableIntoScene()
        {
            // 현재 열려 있는 씬의 미저장 변경사항을 말없이 버리지 않는다
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);

            // 테이블은 반드시 씬 오픈 "뒤"에 로드한다 — OpenScene(Single) 이 씬을 교체하면서
            // 미리 들고 있던 에셋 참조를 언로드(fake-null)시킬 수 있고, 그 참조를 컴포넌트에
            // 배선하면 저장 시 {fileID: 0} 으로 직렬화된다 (InstallManager 와 동일한 교훈).
            var table = AssetDatabase.LoadAssetAtPath<GachaTableSO>(TablePath);
            if (table == null)
            {
                Debug.LogError("[DivineGacha] 테이블 에셋이 없습니다. " +
                               "먼저 'Generate Gacha Table' 을 실행하세요.");
                return;
            }

            GachaManager manager = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                manager = root.GetComponentInChildren<GachaManager>(true);
                if (manager != null) break;
            }

            if (manager == null)
            {
                Debug.LogError($"[DivineGacha] bootstrap 씬에서 GachaManager 컴포넌트를 찾지 못했습니다 " +
                               $"(기대 오브젝트명: '{GachaManagerHostName}').");
                return;
            }

            // 기존 항목은 건드리지 않고 없을 때만 뒤에 추가한다 (멱등)
            var so = new SerializedObject(manager);
            so.Update();
            var listProp = so.FindProperty("gachaTables");
            if (listProp == null || !listProp.isArray)
            {
                Debug.LogError("[DivineGacha] GachaManager.gachaTables 직렬화 필드를 찾지 못했습니다.");
                return;
            }

            bool alreadyWired = false;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == table)
                {
                    alreadyWired = true;
                    break;
                }
            }

            if (!alreadyWired)
            {
                int idx = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(idx);
                listProp.GetArrayElementAtIndex(idx).objectReferenceValue = table;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 배치 모드에서 SerializedObject 쓰기가 반영되지 않는 사례가 있어
            // 실제 리스트를 확인하고 리플렉션으로 한 번 더 못 박는다 (InstallManager 패턴).
            var field = typeof(GachaManager).GetField("gachaTables",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                var list = field.GetValue(manager) as List<GachaTableSO>;
                if (list != null && !list.Contains(table))
                {
                    list.Add(table);
                    EditorUtility.SetDirty(manager);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 저장 결과 검증 — 인메모리가 아니라 "디스크에 기록된 파일"을 직접 확인한다
            // (인메모리 검증은 fake-null 참조도 통과시켜 거짓 성공을 낸 전적이 있다)
            string tableGuid = AssetDatabase.AssetPathToGUID(TablePath);
            string sceneText = System.IO.File.ReadAllText(BootstrapScene);
            bool wired = !string.IsNullOrEmpty(tableGuid) &&
                         sceneText.Contains($"{{fileID: 11400000, guid: {tableGuid}, type: 2}}");
            if (wired)
                Debug.Log("[DivineGacha] GachaManager.gachaTables 에 신 스킬 뽑기 테이블 배선 완료" +
                          (alreadyWired ? " (기존 배선 유지)." : "."));
            else
                Debug.LogError("[DivineGacha] 테이블 배선이 씬 파일에 기록되지 않았습니다 — " +
                               "bootstrap 씬을 열어 GachaManager.gachaTables 를 확인하세요.");
        }
    }
}
