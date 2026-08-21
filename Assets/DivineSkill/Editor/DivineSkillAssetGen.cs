using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KingdomIdle.Divine;

namespace KingdomIdle.Divine.EditorTools
{
    /// <summary>
    /// 신 스킬 초기 카탈로그(8종) + 레지스트리 생성기, 그리고 bootstrap 씬 설치 도구.
    /// 멱등 — 이미 있는 에셋은 필드만 갱신하고 GUID 는 유지한다.
    /// 아트(아이콘/일러스트/VFX)와 SFX 이름은 여기서 건드리지 않으므로, 인스펙터에서 배정한 값이 보존된다.
    /// </summary>
    public static class DivineSkillAssetGen
    {
        private const string SoDir = "Assets/DivineSkill/SO";
        private const string RegistryPath = SoDir + "/DivineSkillRegistry.asset";
        private const string BootstrapScene = "Assets/_Project/Scenes/buildScenes/bootstrap.unity";
        private const string ManagerHostName = "GameManager";

        private struct CardDef
        {
            public int id;
            public string assetName;
            public string nameEng;
            public string nameKor;
            public string skillNameKor;
            public string description;
            public eDivineGrade grade;
            public eDivineConcept concept;
            public float cooldown;
            public eDivineEffectKind kind;
            public float mult;
            public int hits;
            public float duration;
            public float castDelay;
            public eDivineCrowdControl cc;
            public float ccDuration;
            public float slowPercent;
        }

        // 기획서 3.4.5 초기 8종. 배율에는 등급 계수가 이미 반영돼 있다.
        private static readonly CardDef[] Cards =
        {
            new CardDef {
                id = 1, assetName = "DivineSkill_Lumen", nameEng = "Lumen",
                nameKor = "새벽의 여신 루멘", skillNameKor = "여명의 심판",
                description = "전장 전체에 즉발 데미지. 파티 ATK합 × 12.",
                grade = eDivineGrade.Hero, concept = eDivineConcept.Holy, cooldown = 30f,
                kind = eDivineEffectKind.AoeBurst, mult = 12f
            },
            new CardDef {
                id = 2, assetName = "DivineSkill_Gaien", nameEng = "Gaien",
                nameKor = "대지의 여신 가이엔", skillNameKor = "대지의 포옹",
                description = "파티 전체 MAXHP 25% 즉시 회복 + 10초간 받는 피해 -20%.",
                grade = eDivineGrade.Hero, concept = eDivineConcept.Nature, cooldown = 40f,
                kind = eDivineEffectKind.HealAndGuard, mult = 0.25f, duration = 10f
            },
            new CardDef {
                id = 3, assetName = "DivineSkill_Silphir", nameEng = "Silphir",
                nameKor = "질풍의 여신 실피르", skillNameKor = "폭풍 가속",
                description = "12초간 파티 기본 스킬 간격 -30%, 이동속도 +30%.",
                grade = eDivineGrade.Hero, concept = eDivineConcept.Wind, cooldown = 40f,
                kind = eDivineEffectKind.PartyHaste, mult = 0f, duration = 12f
            },
            new CardDef {
                id = 4, assetName = "DivineSkill_Ferrum", nameEng = "Ferrum",
                nameKor = "강철의 마왕 페룸", skillNameKor = "파멸의 참격",
                description = "단일 대상(보스 우선) 파티 ATK합 × 30. 보스전 특화.",
                grade = eDivineGrade.Hero, concept = eDivineConcept.Steel, cooldown = 35f,
                kind = eDivineEffectKind.SingleBurst, mult = 30f
            },
            new CardDef {
                id = 5, assetName = "DivineSkill_Hora", nameEng = "Hora",
                nameKor = "시간의 여신 호라", skillNameKor = "시간의 균열",
                description = "8초간 적 전체 이동속도 -50% + 파티 ATK합 × 20 광역 데미지.",
                grade = eDivineGrade.Legend, concept = eDivineConcept.Chrono, cooldown = 45f,
                kind = eDivineEffectKind.AoeBurst, mult = 20f,
                cc = eDivineCrowdControl.Slow, ccDuration = 8f, slowPercent = 0.5f
            },
            new CardDef {
                id = 6, assetName = "DivineSkill_Ignis", nameEng = "Ignis",
                nameKor = "폭염의 마왕 이그니스", skillNameKor = "지옥불 강림",
                description = "전장 광역 지속 피해. 파티 ATK합 × 6 × 6히트 (6초간).",
                grade = eDivineGrade.Legend, concept = eDivineConcept.Flame, cooldown = 45f,
                kind = eDivineEffectKind.Dot, mult = 6f, hits = 6, duration = 6f
            },
            new CardDef {
                id = 7, assetName = "DivineSkill_Astra", nameEng = "Astra",
                nameKor = "심판의 여신 아스트라", skillNameKor = "별의 낙하",
                description = "전장 전체 파티 ATK합 × 40 즉발 + 3초 기절.",
                grade = eDivineGrade.Myth, concept = eDivineConcept.Holy, cooldown = 50f,
                kind = eDivineEffectKind.AoeBurst, mult = 40f,
                cc = eDivineCrowdControl.Stun, ccDuration = 3f
            },
            new CardDef {
                id = 8, assetName = "DivineSkill_Nox", nameEng = "Nox",
                nameKor = "심연의 마왕 녹스", skillNameKor = "심연의 손아귀",
                description = "적 전체 3초 속박 후 파티 ATK합 × 36. (처치 골드 +100% 는 미구현)",
                grade = eDivineGrade.Myth, concept = eDivineConcept.Abyss, cooldown = 50f,
                kind = eDivineEffectKind.AoeBurst, mult = 36f, castDelay = 3f,
                cc = eDivineCrowdControl.Stun, ccDuration = 3f
            },
        };

        [MenuItem("KingdomIdle/Divine/Generate Cards + Registry")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(SoDir);

            var created = new List<DivineSkillSO>(Cards.Length);
            foreach (var def in Cards)
                created.Add(CreateOrUpdateCard(def));

            var registry = AssetDatabase.LoadAssetAtPath<DivineSkillRegistrySO>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<DivineSkillRegistrySO>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            registry.cards.Clear();
            registry.cards.AddRange(created);
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DivineSkill] 카드 {created.Count}종 + 레지스트리 생성/갱신 완료 → {RegistryPath}");
        }

        private static DivineSkillSO CreateOrUpdateCard(CardDef def)
        {
            string path = $"{SoDir}/{def.assetName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<DivineSkillSO>(path);
            bool isNew = so == null;

            if (isNew)
            {
                so = ScriptableObject.CreateInstance<DivineSkillSO>();
                AssetDatabase.CreateAsset(so, path);
            }

            so.id = def.id;
            so.nameEng = def.nameEng;
            so.nameKor = def.nameKor;
            so.skillNameKor = def.skillNameKor;
            so.description = def.description;
            so.grade = def.grade;
            so.concept = def.concept;
            so.cooldown = def.cooldown;

            so.effectKind = def.kind;
            so.skillMult = def.mult;
            so.hitCount = Mathf.Max(1, def.hits);
            so.duration = def.duration;
            so.castDelay = def.castDelay;

            so.crowdControl = def.cc;
            so.ccDuration = def.ccDuration;
            if (def.slowPercent > 0f) so.slowPercent = def.slowPercent;

            EditorUtility.SetDirty(so);
            return so;
        }

        [MenuItem("KingdomIdle/Divine/Install Manager Into Bootstrap")]
        public static void InstallManager()
        {
            // 현재 열려 있는 씬의 미저장 변경사항을 말없이 버리지 않는다
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);

            // 레지스트리는 반드시 씬 오픈 "뒤"에 로드한다 — OpenScene(Single) 이 씬을 교체하면서
            // 미리 들고 있던 에셋 참조를 언로드(fake-null)시킬 수 있고, 그 참조를 컴포넌트에
            // 배선하면 저장 시 {fileID: 0} 으로 직렬화되는 사고가 실제로 났었다.
            var registry = AssetDatabase.LoadAssetAtPath<DivineSkillRegistrySO>(RegistryPath);
            if (registry == null)
            {
                Debug.LogError("[DivineSkill] 레지스트리가 없습니다. 먼저 'Generate Cards + Registry' 를 실행하세요.");
                return;
            }

            GameObject host = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == ManagerHostName) { host = root; break; }
            }

            if (host == null)
            {
                Debug.LogError($"[DivineSkill] bootstrap 씬에서 '{ManagerHostName}' 오브젝트를 찾지 못했습니다.");
                return;
            }

            var manager = host.GetComponent<DivineSkillManager>();
            if (manager == null)
                manager = host.AddComponent<DivineSkillManager>();

            var so = new SerializedObject(manager);
            so.Update();
            var prop = so.FindProperty("registry");
            if (prop != null)
            {
                prop.objectReferenceValue = registry;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 배치 모드에서 SerializedObject 쓰기가 방금 AddComponent 된 컴포넌트에
            // 반영되지 않는 사례가 있어, 실제 값을 확인하고 리플렉션으로 한 번 더 못 박는다.
            var field = typeof(DivineSkillManager).GetField("registry",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null && field.GetValue(manager) as DivineSkillRegistrySO != registry)
            {
                field.SetValue(manager, registry);
                EditorUtility.SetDirty(manager);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 저장 결과 검증 — 인메모리가 아니라 "디스크에 기록된 파일"을 직접 확인한다
            // (인메모리 검증은 fake-null 참조도 통과시켜 거짓 성공을 낸 전적이 있다)
            string registryGuid = AssetDatabase.AssetPathToGUID(RegistryPath);
            string sceneText = System.IO.File.ReadAllText(BootstrapScene);
            bool wired = !string.IsNullOrEmpty(registryGuid) &&
                         sceneText.Contains($"registry: {{fileID: 11400000, guid: {registryGuid}");
            if (wired)
                Debug.Log($"[DivineSkill] {ManagerHostName} 에 DivineSkillManager 설치 + 레지스트리 배선 완료.");
            else
                Debug.LogError("[DivineSkill] 레지스트리 배선이 씬 파일에 기록되지 않았습니다 — " +
                               "bootstrap 씬을 열어 registry 필드를 확인하세요.");
        }

        // ── 개발용 ──
        [MenuItem("KingdomIdle/Divine/Debug/Unlock System + Grant All Cards")]
        public static void GrantAll()
        {
            var mgr = DivineSkillManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning("[DivineSkill] 플레이 모드에서 실행하세요 (DivineSkillManager 인스턴스 필요).");
                return;
            }

            mgr.UnlockSystem();
            var cards = mgr.GetAllCards();
            for (int i = 0; i < cards.Count; i++)
                mgr.Acquire(cards[i].id);

            Debug.Log($"[DivineSkill] 카드 {cards.Count}종 지급 + 시스템 해금 완료.");
        }

        [MenuItem("KingdomIdle/Divine/Debug/Clear Progress")]
        public static void ClearProgress()
        {
            var mgr = DivineSkillManager.Instance;
            if (mgr != null)
            {
                mgr.ClearAllProgress();
                Debug.Log("[DivineSkill] 보유 상태 초기화 완료.");
                return;
            }

            PlayerPrefs.DeleteKey("divine_save");
            PlayerPrefs.Save();
            Debug.Log("[DivineSkill] 저장 데이터(divine_save) 삭제 완료.");
        }
    }
}
