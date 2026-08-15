using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using KingdomIdle.Divine;
using Scripts.Core;
using Object = UnityEngine.Object;

namespace KingdomIdle.Divine.EditorTools
{
    /// <summary>
    /// 신화 카드 '심판의 여신 아스트라 — 별의 낙하' 전용 VFX 생성기.
    ///
    /// 외부 이펙트 팩의 시트를 프로젝트가 이미 검증한 방식
    /// (Assets/_Project/Scripts/Player/PlayerVFX/Animation/Slash_Attack.anim +
    ///  Assets/_Project/Prefabs/VFX/Slash_Attack.prefab — SpriteRenderer 의 m_Sprite 에
    ///  PPtr 커브를 거는 월드 스페이스 프리팹)으로 변환한다.
    /// PixelArtRPGVFX 의 원본 프리팹은 UGUI(RectTransform + Image, Layer 5) 라서
    /// Canvas 밖에서는 아무것도 그리지 않으므로 그대로 쓸 수 없다.
    ///
    /// 멱등(idempotent) — 여러 번 실행해도 안전하다.
    ///  · 에셋은 경로 기준 load-or-create 라서 GUID 가 유지된다.
    ///  · 프리팹은 이미 있으면 LoadPrefabContents 로 열어 재저장한다.
    ///    (새 GameObject 로 덮어쓰면 내부 fileID 가 전부 바뀌어 기존 참조가 끊긴다)
    ///  · 이미 올바르게 슬라이스된 텍스처는 다시 자르지 않는다(서브 스프라이트 ID 보존).
    ///
    /// ExternalAssets 의 임포트 설정은 건드리지 않는다. 슬라이스가 필요한 아트는
    /// Assets/DivineSkill/VFX/Art 로 사본을 떠서 그 사본의 임포터만 우리가 소유한다.
    /// </summary>
    public static class DivineVfxGen
    {
        // ── 경로 ──
        private const string VfxDir  = "Assets/DivineSkill/VFX";
        private const string AnimDir = VfxDir + "/Anim";
        private const string ArtDir  = VfxDir + "/Art";

        private const string AstraCardPath = "Assets/DivineSkill/SO/DivineSkill_Astra.asset";

        // ── 원본 아트 ──
        // 512x1600 = 512x320 5프레임. 이미 슬라이스되어 있다(Screen_Anim_HolyWing_001_0.._4).
        private const string BurstTexPath =
            "Assets/ExternalAssets/PixelArtRPGVFX/Textures/Holy/Screen_Anim_HolyWing_001.png";
        // 64x384 세로 스트립 = 64x64 6프레임. 이미 슬라이스되어 있다(HolyCross_0.._5).
        private const string ImpactTexPath =
            "Assets/ExternalAssets/PixelArtRPGVFX/Textures/Holy/HolyCross.png";
        // 352x32 = 32x32 11프레임의 금빛 별 2개가 도는 그림. 원본은 파티클 머티리얼용이라
        // spriteMode 가 Single 이다 → 사본을 떠서 Multiple 로 다시 자른다.
        private const string StunSrcTexPath =
            "Assets/ExternalAssets/StateEffect/EffectMaterials/Sprites/Effect_Confuse_Star.png";
        private const string StunTexPath = ArtDir + "/Astra_StunStars.png";

        private const int    StunFrameCount = 11;
        private const int    StunFrameSize  = 32;
        private const float  StunPpu        = 32f;
        private const int    StunMaxTexSize = 512;

        // 몬스터는 Monster.Awake 에서 자기 SpriteRenderer 를 "Enemy" 정렬 레이어로 올린다.
        // 정렬 '레이어'가 정렬 '순서'를 이기므로, Default 레이어에 두면 sortingOrder 를
        // 아무리 올려도 몬스터 뒤에 가려진다. 신 스킬 연출은 전부 Enemy 레이어에 얹는다.
        private const string SortingLayerName = "Enemy";

        private const string BurstName  = "DivineVFX_Astra_Burst";
        private const string ImpactName = "DivineVFX_Astra_Impact";
        private const string StunName   = "DivineVFX_Astra_Stun";

        private const string ClipBurstName  = "Astra_Burst";
        private const string ClipImpactName = "Astra_Impact";
        private const string ClipStunName   = "Astra_Stun";

        /// <summary>VFX 하나(클립 + 컨트롤러 + 프리팹)를 만드는 데 필요한 값 전부.</summary>
        private struct VfxSpec
        {
            public string prefabName;
            public string clipName;
            public string texturePath;
            public int    frameCount;
            public float  frameDuration;   // 프레임 1장당 노출 시간(초)
            public float  clipFrameRate;   // 모든 키가 정수 프레임에 떨어지도록 고른 값
            public bool   loop;
            public int    sortingOrder;
            public float  lifetime;        // DivineVfxInstance.lifetime (0 = 자동 파괴 없음)
            public float  fadeOut;
            public bool   fitToCamera;
            public Vector3 localScale;
        }

        // A. 화면 전체를 덮는 심판의 강림. 5프레임 x 0.1s = 0.5s 원샷,
        //    수명 1.0s 라 마지막 '날개가 솟구친 프레임'을 0.5s 유지한 뒤 0.25s 페이드아웃.
        private static readonly VfxSpec BurstSpec = new VfxSpec
        {
            prefabName = BurstName, clipName = ClipBurstName, texturePath = BurstTexPath,
            frameCount = 5, frameDuration = 0.1f, clipFrameRate = 10f, loop = false,
            sortingOrder = 900, lifetime = 1.0f, fadeOut = 0.25f,
            fitToCamera = true, localScale = Vector3.one,
        };

        // B. 대상 1기마다 솟는 심판의 기둥. 6프레임 x 0.06s = 0.36s 원샷,
        //    수명 0.6s 라 완성된 십자가를 0.24s 보여준 뒤 0.12s 페이드아웃.
        //    프리팹 스케일은 1 로 두고 확대는 카드의 impactVfxScale(=2)이 담당한다.
        private static readonly VfxSpec ImpactSpec = new VfxSpec
        {
            prefabName = ImpactName, clipName = ClipImpactName, texturePath = ImpactTexPath,
            frameCount = 6, frameDuration = 0.06f, clipFrameRate = 50f, loop = false,
            sortingOrder = 800, lifetime = 0.6f, fadeOut = 0.12f,
            fitToCamera = false, localScale = Vector3.one,
        };

        // C. 기절 상태이상 루프. 11프레임 x 0.06s = 0.66s 무한 루프.
        //    lifetime 0 = 자동 파괴 없음 — 실제 수명은 MonsterCCState 가 스폰 시점에
        //    남은 CC 지속시간으로 덮어쓰고, 해제될 때 DivineVfxInstance.Release() 를 부른다.
        //    32px(=1 월드 유닛, PPU 32) 별 2개는 3.125 유닛짜리 몬스터 머리 위에서 너무 작아
        //    프리팹 스케일 2 를 구워 넣는다(상태이상 연출에는 스케일 필드가 없다).
        private static readonly VfxSpec StunSpec = new VfxSpec
        {
            prefabName = StunName, clipName = ClipStunName, texturePath = StunTexPath,
            frameCount = StunFrameCount, frameDuration = 0.06f, clipFrameRate = 50f, loop = true,
            sortingOrder = 950, lifetime = 0f, fadeOut = 0.15f,
            fitToCamera = false, localScale = new Vector3(2f, 2f, 1f),
        };

        // ────────────────────────────────────────────
        //  메뉴
        // ────────────────────────────────────────────
        [MenuItem("KingdomIdle/Divine/Generate Astra VFX")]
        public static void GenerateAstraVfx()
        {
            EnsureFolders();

            if (!SortingLayerExists(SortingLayerName))
            {
                Debug.LogError($"[DivineVFX] 정렬 레이어 '{SortingLayerName}' 가 없습니다 " +
                               "(Project Settings > Tags and Layers). 몬스터 뒤에 가려지므로 생성을 중단합니다.");
                return;
            }

            int made = 0;
            if (BuildVfx(BurstSpec)  != null) made++;
            if (BuildVfx(ImpactSpec) != null) made++;
            if (EnsureStunTexture() && BuildVfx(StunSpec) != null) made++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DivineVFX] 아스트라 VFX {made}/3 생성/갱신 완료 → {VfxDir}");

            if (made == 3) WireAstraCard();
            else Debug.LogError("[DivineVFX] 일부 VFX 생성에 실패했습니다. 카드 배선을 건너뜁니다.");
        }

        [MenuItem("KingdomIdle/Divine/Wire Astra Card")]
        public static void WireAstraCard()
        {
            var card = AssetDatabase.LoadAssetAtPath<DivineSkillSO>(AstraCardPath);
            if (card == null)
            {
                Debug.LogError($"[DivineVFX] 아스트라 카드를 찾지 못했습니다: {AstraCardPath}. " +
                               "먼저 'KingdomIdle/Divine/Generate Cards + Registry' 를 실행하세요.");
                return;
            }
            if (card.nameEng != "Astra")
                Debug.LogWarning($"[DivineVFX] {AstraCardPath} 의 nameEng 가 'Astra' 가 아닙니다 " +
                                 $"(현재 '{card.nameEng}', id {card.id}). 그래도 배선은 진행합니다.");

            var burst  = LoadPrefab(BurstName);
            var impact = LoadPrefab(ImpactName);
            var status = LoadPrefab(StunName);
            if (burst == null || impact == null || status == null)
            {
                Debug.LogError("[DivineVFX] VFX 프리팹이 아직 없습니다. " +
                               "먼저 'KingdomIdle/Divine/Generate Astra VFX' 를 실행하세요.");
                return;
            }

            // eSFXType 은 xlsx 에서 자동 생성되는 열거형이라 손대지 않는다.
            // 이름이 어긋나면 DivineSkillCaster.PlaySfx 의 Enum.TryParse 가 조용히 실패하므로
            // 여기서 미리 검증만 한다.
            const string castSfx   = nameof(eSFXType.Lightning_SFX);
            const string impactSfx = nameof(eSFXType.Parrying_SFX);

            var so = new SerializedObject(card);
            SetObj (so, "burstVfxPrefab",  burst);
            SetObj (so, "impactVfxPrefab", impact);
            SetObj (so, "statusVfxPrefab", status);

            SetStr (so, "sfxName",         castSfx);
            SetStr (so, "impactSfxName",   impactSfx);

            SetF   (so, "impactDelay",       0.35f);
            SetF   (so, "impactStagger",     0.05f);
            SetF   (so, "burstVfxLifetime",  1.0f);
            SetF   (so, "impactVfxLifetime", 0.6f);
            SetF   (so, "impactVfxScale",    2f);
            SetVec3(so, "statusVfxOffset",   new Vector3(0f, 1.1f, 0f));

            SetBool(so, "screenShake",     true);
            SetF   (so, "shakeDuration",   0.3f);
            SetF   (so, "shakeMagnitude",  0.14f);

            SetBool(so, "cutInEnabled",   true);
            SetF   (so, "cutInDuration",  1.2f);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);
            AssetDatabase.SaveAssets();

            Debug.Log($"[DivineVFX] '{card.DisplayName} — {card.skillNameKor}' (id {card.id}) 연출 배선 완료: " +
                      $"burst={burst.name}, impact={impact.name}, status={status.name}, " +
                      $"sfx={castSfx}/{impactSfx}");
        }

        // ────────────────────────────────────────────
        //  생성 파이프라인
        // ────────────────────────────────────────────
        private static GameObject BuildVfx(VfxSpec spec)
        {
            var frames = LoadOrderedSprites(spec.texturePath, spec.frameCount);
            if (frames == null) return null;   // 원인은 LoadOrderedSprites 가 이미 로그로 남긴다

            var clip = BuildClip(spec, frames);
            if (clip == null) return null;

            var controller = BuildController(spec.clipName, clip);
            if (controller == null) return null;

            return BuildPrefab(spec, frames[0], controller);
        }

        /// <summary>
        /// 시트의 서브 스프라이트를 재생 순서대로 돌려준다. 개수가 다르면 null + 에러 로그.
        ///
        /// AssetDatabase.LoadAllAssetRepresentationsAtPath 의 반환 순서는 문서상 보장되지 않는다.
        /// 이 팩들의 서브 스프라이트 이름은 예외 없이 "&lt;시트이름&gt;_&lt;프레임번호&gt;" 규칙이므로
        /// 이름 끝 숫자로 정렬하면 결정적이다. 세로 스트립(HolyCross / Screen_Anim_HolyWing_001)의
        /// _0 은 .meta 의 rect.y 가 가장 큰 = 시트 '맨 위' 프레임이고, 실제로 그 프레임이
        /// 애니메이션의 시작(작은 씨앗)이라 이름 순서 = 재생 순서가 맞다.
        /// </summary>
        private static Sprite[] LoadOrderedSprites(string texturePath, int expected)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
            {
                Debug.LogError($"[DivineVFX] 원본 텍스처를 찾지 못했습니다: {texturePath}");
                return null;
            }

            var list = new List<Sprite>(expected);
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(texturePath))
                if (o is Sprite s) list.Add(s);

            if (list.Count != expected)
            {
                Debug.LogError($"[DivineVFX] {texturePath} 의 서브 스프라이트가 {list.Count}개입니다 " +
                               $"(기대값 {expected}). 슬라이스 상태를 확인하세요 — 이 에셋 생성을 중단합니다.");
                return null;
            }

            list.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));

            for (int i = 0; i < list.Count; i++)
            {
                if (FrameIndex(list[i].name) == i) continue;
                Debug.LogError($"[DivineVFX] {texturePath} 의 프레임 번호가 0..{expected - 1} 연속이 아닙니다 " +
                               $"('{list[i].name}'). 이 에셋 생성을 중단합니다.");
                return null;
            }

            return list.ToArray();
        }

        /// <summary>"HolyCross_3" → 3. 규칙에서 벗어난 이름은 맨 뒤로 밀어 검증 단계에서 걸리게 한다.</summary>
        private static int FrameIndex(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return int.MaxValue;
            int u = spriteName.LastIndexOf('_');
            if (u >= 0 && u + 1 < spriteName.Length &&
                int.TryParse(spriteName.Substring(u + 1), out int idx)) return idx;
            return int.MaxValue;
        }

        /// <summary>
        /// 루트 SpriteRenderer 의 m_Sprite 에 PPtr 커브를 건 클립.
        /// Slash_Attack.anim 과 동일한 형태(attribute m_Sprite, classID 212, path "")다.
        /// </summary>
        private static AnimationClip BuildClip(VfxSpec spec, Sprite[] frames)
        {
            string path = $"{AnimDir}/{spec.clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip { name = spec.clipName };

            clip.ClearCurves();
            clip.frameRate = spec.clipFrameRate;

            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i * spec.frameDuration, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            // 마지막 키는 (n-1)*dt 에 있고 stopTime 은 n*dt — 마지막 프레임도 dt 만큼 노출된다.
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f;
            settings.stopTime  = frames.Length * spec.frameDuration;
            settings.loopTime  = spec.loop;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (isNew) AssetDatabase.CreateAsset(clip, path);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>단일 스테이트 컨트롤러. 이미 있으면 기본 스테이트의 모션만 교체해 GUID 를 지킨다.</summary>
        private static AnimatorController BuildController(string name, AnimationClip clip)
        {
            string path = $"{AnimDir}/{name}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);

            var layers = controller.layers;
            if (layers == null || layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
                layers = controller.layers;
            }

            var machine = layers[0].stateMachine;
            if (machine == null)
            {
                Debug.LogError($"[DivineVFX] {path} 의 레이어 0 에 스테이트 머신이 없습니다.");
                return null;
            }

            var state = machine.defaultState;
            if (state == null)
            {
                state = machine.AddState(clip.name);
                machine.defaultState = state;
            }
            state.motion = clip;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Assets/_Project/Prefabs/VFX/Slash_Attack.prefab 과 같은 형태의 월드 스페이스 프리팹.
        /// Transform + SpriteRenderer(Sprites-Default) + Animator + DivineVfxInstance.
        /// </summary>
        private static GameObject BuildPrefab(VfxSpec spec, Sprite firstFrame, AnimatorController controller)
        {
            string path = $"{VfxDir}/{spec.prefabName}.prefab";
            bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;

            GameObject root = existed
                ? PrefabUtility.LoadPrefabContents(path)
                : new GameObject(spec.prefabName);

            try
            {
                root.name  = spec.prefabName;
                root.layer = 0;                       // 월드 오브젝트. UI(5) 가 아니다.
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale    = spec.localScale;

                var sr = GetOrAdd<SpriteRenderer>(root);
                sr.sprite          = firstFrame;      // fitToCamera 가 Start 에서 bounds 를 읽으므로 비워둘 수 없다
                sr.sharedMaterial  = SpritesDefaultMaterial();
                sr.color           = Color.white;
                sr.sortingLayerName = SortingLayerName;
                sr.sortingOrder    = spec.sortingOrder;
                sr.flipX = false;
                sr.flipY = false;

                var animator = GetOrAdd<Animator>(root);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var inst = GetOrAdd<DivineVfxInstance>(root);
                inst.lifetime     = spec.lifetime;
                inst.fadeOut      = spec.fadeOut;
                inst.fitToCamera  = spec.fitToCamera;
                inst.followTarget = null;
                inst.followOffset = Vector3.zero;

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok || saved == null)
                {
                    Debug.LogError($"[DivineVFX] 프리팹 저장에 실패했습니다: {path}");
                    return null;
                }
                return saved;
            }
            finally
            {
                if (existed) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        // ────────────────────────────────────────────
        //  기절 상태이상 아트 (사본 + 슬라이스)
        // ────────────────────────────────────────────
        /// <summary>
        /// Effect_Confuse_Star.png 는 파티클 머티리얼의 _MainTex 로 쓰이는 시트라
        /// spriteMode 가 Single 이다. ExternalAssets 의 임포트 설정을 바꾸면 StateEffect 팩의
        /// 기존 프리팹에 영향이 가므로, 사본을 떠서 사본의 임포터만 우리가 설정한다.
        /// </summary>
        private static bool EnsureStunTexture()
        {
            string absDst = ToAbsolutePath(StunTexPath);
            if (!File.Exists(absDst))
            {
                string absSrc = ToAbsolutePath(StunSrcTexPath);
                if (!File.Exists(absSrc))
                {
                    Debug.LogError($"[DivineVFX] 기절 연출 원본 아트를 찾지 못했습니다: {StunSrcTexPath}");
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(absDst));
                File.Copy(absSrc, absDst);
                AssetDatabase.ImportAsset(StunTexPath, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[DivineVFX] 원본 아트 사본 생성: {StunSrcTexPath} → {StunTexPath}");
            }

            var importer = AssetImporter.GetAtPath(StunTexPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[DivineVFX] {StunTexPath} 의 TextureImporter 를 얻지 못했습니다.");
                return false;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            { importer.spriteImportMode = SpriteImportMode.Multiple; changed = true; }
            if (importer.filterMode != FilterMode.Point)
            { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, StunPpu))
            { importer.spritePixelsPerUnit = StunPpu; changed = true; }
            if (!importer.alphaIsTransparency)
            { importer.alphaIsTransparency = true; changed = true; }
            if (importer.mipmapEnabled)
            { importer.mipmapEnabled = false; changed = true; }
            if (importer.npotScale != TextureImporterNPOTScale.None)
            { importer.npotScale = TextureImporterNPOTScale.None; changed = true; }
            if (importer.maxTextureSize != StunMaxTexSize)
            { importer.maxTextureSize = StunMaxTexSize; changed = true; }
            // 352x32 짜리 픽셀 아트라 압축 이득이 사실상 없고, 블록 압축은 별 모양을 뭉갠다.
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }

            if (changed)
            {
                importer.SaveAndReimport();
                importer = AssetImporter.GetAtPath(StunTexPath) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[DivineVFX] {StunTexPath} 재임포트 후 임포터를 다시 얻지 못했습니다.");
                    return false;
                }
            }

            // 이미 올바르게 잘려 있으면 건드리지 않는다 — 다시 자르면 서브 스프라이트 ID 가 새로 발급돼
            // 이 텍스처를 참조하던 클립/프리팹이 전부 끊긴다.
            if (CountSubSprites(StunTexPath) == StunFrameCount) return true;

            return SliceHorizontalGrid(importer, Path.GetFileNameWithoutExtension(StunTexPath),
                                       StunFrameCount, StunFrameSize, StunFrameSize);
        }

        /// <summary>
        /// 가로 1행 그리드 슬라이스. TextureImporter.spritesheet 은 Unity 6 에서 폐기됐으므로
        /// com.unity.2d.sprite 의 ISpriteEditorDataProvider 를 쓴다.
        /// </summary>
        private static bool SliceHorizontalGrid(TextureImporter importer, string baseName,
                                                int count, int width, int height)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
            if (tex == null)
            {
                Debug.LogError($"[DivineVFX] {importer.assetPath} 를 Texture2D 로 읽지 못했습니다.");
                return false;
            }
            if (tex.width < count * width || tex.height < height)
            {
                Debug.LogError($"[DivineVFX] {importer.assetPath} 크기가 {tex.width}x{tex.height} 라 " +
                               $"{width}x{height} {count}장을 자를 수 없습니다.");
                return false;
            }

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null)
            {
                Debug.LogError($"[DivineVFX] {importer.assetPath} 의 ISpriteEditorDataProvider 를 얻지 못했습니다.");
                return false;
            }
            provider.InitSpriteEditorDataProvider();

            // SpriteRect.rect 는 텍스처 좌표계(좌하단 원점)다. 1행짜리 시트라 y 는 항상 0.
            var rects = new SpriteRect[count];
            for (int i = 0; i < count; i++)
            {
                rects[i] = new SpriteRect
                {
                    name      = $"{baseName}_{i}",
                    rect      = new Rect(i * width, 0f, width, height),
                    alignment = SpriteAlignment.Center,
                    pivot     = new Vector2(0.5f, 0.5f),
                    border    = Vector4.zero,
                };
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();

            int have = CountSubSprites(importer.assetPath);
            if (have == count) return true;

            Debug.LogError($"[DivineVFX] {importer.assetPath} 슬라이스 후 서브 스프라이트가 {have}개입니다 " +
                           $"(기대값 {count}).");
            return false;
        }

        private static int CountSubSprites(string assetPath)
        {
            int n = 0;
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                if (o is Sprite) n++;
            return n;
        }

        // ────────────────────────────────────────────
        //  잡동사니
        // ────────────────────────────────────────────
        private static void EnsureFolders()
        {
            EnsureFolder("Assets/DivineSkill", "VFX");
            EnsureFolder(VfxDir, "Anim");
            EnsureFolder(VfxDir, "Art");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static GameObject LoadPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxDir}/{name}.prefab");

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static Material SpritesDefaultMaterial() =>
            AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        private static bool SortingLayerExists(string name)
        {
            var layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
                if (layers[i].name == name) return true;
            return false;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            string root = projectRoot != null ? projectRoot.FullName : Environment.CurrentDirectory;
            return Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        // ── SerializedObject 헬퍼: 필드가 없으면 조용히 넘어가지 않고 경고를 남긴다 ──
        private static SerializedProperty Find(SerializedObject so, string field)
        {
            var p = so.FindProperty(field);
            if (p == null)
                Debug.LogWarning($"[DivineVFX] DivineSkillSO 에 '{field}' 필드가 없습니다. 건너뜁니다.");
            return p;
        }

        private static void SetObj(SerializedObject so, string field, Object value)
        {
            var p = Find(so, field);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetStr(SerializedObject so, string field, string value)
        {
            var p = Find(so, field);
            if (p != null) p.stringValue = value;
        }

        private static void SetF(SerializedObject so, string field, float value)
        {
            var p = Find(so, field);
            if (p != null) p.floatValue = value;
        }

        private static void SetBool(SerializedObject so, string field, bool value)
        {
            var p = Find(so, field);
            if (p != null) p.boolValue = value;
        }

        private static void SetVec3(SerializedObject so, string field, Vector3 value)
        {
            var p = Find(so, field);
            if (p != null) p.vector3Value = value;
        }
    }
}
