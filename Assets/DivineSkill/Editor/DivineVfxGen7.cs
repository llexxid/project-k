using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using KingdomIdle.Divine;
using Scripts.Core;
using Object = UnityEngine.Object;

namespace KingdomIdle.Divine.EditorTools
{
    /// <summary>
    /// 아스트라를 제외한 신 스킬 카드 7장(루멘·가이엔·실피르·페룸·호라·이그니스·녹스)의
    /// 전투 VFX 생성기. DivineVfxGen(아스트라)과 같은 파이프라인 —
    /// 외부 팩 시트 → PPtr 클립 → 단일 스테이트 컨트롤러 → SpriteRenderer 월드 프리팹
    /// (+ DivineVfxInstance) → 카드 SO 배선 — 을 그대로 타되, 세 가지를 일반화한다.
    ///
    ///  1) 틴트: SpriteRenderer.color 는 곱연산이라 회색/흰색 아트에만 유효하다.
    ///     (회색 바람 → 연녹색, 흰 참격 → 강철색 등) 색이 이미 진한 아트(파란 전격,
    ///     금색 별)는 틴트로 목표 색을 못 만들므로 픽셀 재채색 '사본'을 굽는다 — 2) 참조.
    ///  2) 재채색 사본: 원본 PNG 를 Assets/DivineSkill/VFX/Art 로 복사하면서 밝기 기반
    ///     램프(금/보라)로 픽셀을 다시 칠하고, 사본의 임포터만 우리가 소유해 슬라이스한다.
    ///     (ExternalAssets 의 임포트 설정은 절대 건드리지 않는다 — 아스트라와 동일 원칙)
    ///  3) 2-레이어 프리팹: 가이엔 임팩트(방어막 + 회복 반짝임)처럼 자식 SpriteRenderer
    ///     +Animator 1개를 추가로 얹을 수 있다. 외부 팩의 합성 프리팹과 같은 구조라
    ///     DivineVfxInstance 가 자식까지 알아서 페이드/되감기한다. 최대 2 렌더러(모바일).
    ///
    /// 멱등(idempotent) — DivineVfxGen 과 동일한 규칙.
    ///  · 클립/컨트롤러/프리팹은 load-or-create, 프리팹은 LoadPrefabContents 재저장.
    ///  · 재채색 사본은 파일이 이미 있으면 픽셀 작업을 건너뛴다(재생성하려면 사본 삭제).
    ///  · 이미 올바르게 슬라이스된 사본은 다시 자르지 않는다(서브 스프라이트 ID 보존).
    ///
    /// 카드별 콘셉트 (스킬 종류별로 실제로 쓰이는 연출만 만든다 — DivineSkillCaster 실증):
    ///  · 루멘(AoeBurst)      금빛 파열 — 재채색 전격 시트. 새벽빛 심판.
    ///  · 가이엔(HealAndGuard) 대지 융기 화면 + 플레이어마다 방어막/회복. 임팩트만 쓰인다.
    ///  · 실피르(PartyHaste)  바람 소용돌이 화면 + 플레이어 추종 질주 궤적. 임팩트 없음.
    ///  · 페룸(SingleBurst)   화면을 가르는 강철 일섬 + 보스에 대검 참격.
    ///  · 호라(AoeBurst+Slow) 시간 결정(서리) 화면 + 시공 파열 임팩트 + 느린 시간 소용돌이.
    ///  · 이그니스(Dot)       화면을 핥는 화염 + 히트당 초경량 폭발(64px, 30마리×6히트).
    ///  · 녹스(AoeBurst+Stun) 심연 소용돌이 3초 루프 → 붕괴 폭발 + 보라 기절 별.
    /// </summary>
    public static class DivineVfxGen7
    {
        // ── 경로 ──
        private const string VfxDir  = "Assets/DivineSkill/VFX";
        private const string AnimDir = VfxDir + "/Anim";
        private const string ArtDir  = VfxDir + "/Art";
        private const string SoDir   = "Assets/DivineSkill/SO";

        private const string TexRoot = "Assets/ExternalAssets/PixelArtRPGVFX/Textures";

        // 아스트라와 동일 — 몬스터가 "Enemy" 정렬 레이어라 그 위에 얹는다.
        private const string SortingLayerName = "Enemy";

        // ── 재채색 원본 ──
        // 전격 시트는 순수 파랑이라 곱셈 틴트로는 금색이 안 나온다 → 금 램프 사본.
        private const string LumenBurstSrc  = TexRoot + "/Electricity/Screen_Anim_Electricity_001.png";
        private const string LumenBurstTex  = ArtDir + "/Lumen_Burst.png";
        private const string LumenImpactSrc = TexRoot + "/Electricity/ElectricExplosion.png";
        private const string LumenImpactTex = ArtDir + "/Lumen_Impact.png";
        // 기절 별은 금색이라 보라 틴트를 곱하면 탁한 갈색이 된다 → 보라 램프 사본.
        // (아스트라의 Astra_StunStars 와 같은 원본, 다른 색 — 두 신화 카드를 구분한다)
        private const string NoxStunSrc = "Assets/ExternalAssets/StateEffect/EffectMaterials/Sprites/Effect_Confuse_Star.png";
        private const string NoxStunTex = ArtDir + "/Nox_StunStars.png";
        // 심연 소용돌이의 중심은 '불투명한 검은 원반'이라 3.6초 루프 동안 전장을 가려 버린다.
        // 렌더러 알파는 Spawn 이 1로 리셋하므로 틴트로는 못 풀고, 픽셀 알파를 0.6 으로 구운
        // 장막(Veil) 사본을 쓴다 — 심연 너머로 기절한 몬스터가 비쳐 보인다.
        private const string NoxBurstSrc = TexRoot + "/Void/Screen_Anim_Void_001.png";
        private const string NoxBurstTex = ArtDir + "/Nox_Burst.png";

        /// <summary>재채색 램프 종류. Veil 은 색은 그대로, 알파만 0.6배(반투명 장막).</summary>
        private enum eRamp { Gold, Purple, Veil }

        /// <summary>프리팹 안의 SpriteRenderer 1장(루트 또는 자식)에 해당하는 스펙.</summary>
        private struct LayerSpec
        {
            public string clipName;        // Anim/<clipName>.anim + .controller, 자식이면 GO 이름으로도 쓴다
            public string texturePath;
            public int    frameCount;
            public float  frameDuration;   // 프레임 1장당 노출 시간(초). frameRate 와 곱이 정수여야 한다
            public float  clipFrameRate;
            public bool   loop;
            public int    sortingOrder;    // 아스트라 관례: 버스트 900 / 임팩트 800(+자식 810) / 상태 950
            public Color  tint;            // 곱연산 틴트. 회색/흰색 아트 전용 — 유채색 아트는 재채색 사본으로
        }

        /// <summary>프리팹 1개(루트 레이어 + 자식 레이어 0..1)의 스펙.</summary>
        private struct PrefabSpec
        {
            public string      prefabName;
            public LayerSpec   root;
            public LayerSpec[] children;   // null 또는 1개 — 렌더러 총 2장 초과 금지(모바일)
            public float       lifetime;   // DivineVfxInstance.lifetime (0 = CC/버프 시스템이 수명 소유)
            public float       fadeOut;
            public bool        fitToCamera;
            public Vector3     localScale;
        }

        /// <summary>카드 1장의 SO 배선 값 전부. WireAstraCard 와 같은 필드를 채운다.</summary>
        private struct CardDef
        {
            public string  nameEng;              // DivineSkill_<nameEng>.asset + nameEng 검증
            public string  burst, impact, status; // 프리팹 이름. null 이면 그 슬롯은 비운다
            public string  sfx, impactSfx;
            public float   impactDelay, impactStagger;
            public float   burstLifetime, impactLifetime, impactScale;
            public Vector3 statusOffset;
            public bool    shake;
            public float   shakeDuration, shakeMagnitude;
        }

        // ────────────────────────────────────────────
        //  프리팹 스펙 16종
        // ────────────────────────────────────────────
        private static readonly Color NoTint = Color.white;

        private static PrefabSpec[] PrefabSpecs => new[]
        {
            // ── 루멘 · 새벽의 여신 (AoeBurst ×12) — 금빛 파열 ──
            // 재채색 전격 화면: 금 파편이 화면을 가르는 '여명의 심판'. 5프레임 x 0.1s 원샷.
            Screen("DivineVFX_Lumen_Burst", "Lumen_Burst", LumenBurstTex,
                   frameDuration: 0.1f, frameRate: 10f, lifetime: 0.9f, fadeOut: 0.25f, tint: NoTint),
            // 재채색 전격 폭발: 흰 섬광 코어(원본 유지) → 금 불꽃. 몬스터마다 터진다.
            Impact("DivineVFX_Lumen_Impact", "Lumen_Impact", LumenImpactTex,
                   frameDuration: 0.06f, frameRate: 50f, lifetime: 0.5f, fadeOut: 0.12f, tint: NoTint),

            // ── 가이엔 · 대지의 여신 (HealAndGuard) — 대지의 포옹 ──
            // 회색 암석 융기 화면에 이끼빛 틴트. 돌은 무거우니 한 박자 느리게(0.6s).
            Screen("DivineVFX_Gaien_Burst", "Gaien_Burst", TexRoot + "/Earth/Screen_Anim_Earth_001.png",
                   frameDuration: 0.12f, frameRate: 25f, lifetime: 1.1f, fadeOut: 0.3f,
                   tint: new Color(0.72f, 0.95f, 0.60f)),
            // 플레이어마다: 루트 = 초록 결정 방어막(가드), 자식 = 회복 반짝임 상승(힐).
            // 2-레이어 1프리팹 — HealAndGuard 는 임팩트만 스폰하므로 두 의미를 한 장에 싣는다.
            new PrefabSpec
            {
                prefabName = "DivineVFX_Gaien_Impact",
                root = new LayerSpec
                {
                    clipName = "Gaien_ImpactShield",
                    texturePath = TexRoot + "/Earth/EarthShield.png",
                    frameCount = 6, frameDuration = 0.09f, clipFrameRate = 100f, loop = false,
                    sortingOrder = 800, tint = NoTint,   // 원본이 이미 초록
                },
                children = new[]
                {
                    new LayerSpec
                    {
                        clipName = "Gaien_ImpactHeal",
                        texturePath = TexRoot + "/Earth/EarthHeal.png",
                        frameCount = 6, frameDuration = 0.09f, clipFrameRate = 100f, loop = false,
                        sortingOrder = 810, tint = NoTint,
                    },
                },
                lifetime = 0.65f, fadeOut = 0.2f, fitToCamera = false, localScale = Vector3.one,
            },

            // ── 실피르 · 질풍의 여신 (PartyHaste) — 질풍 가호 ──
            // 회색 돌풍 소용돌이에 연녹색 틴트. 가속 스킬답게 제일 빠른 박자(0.45s).
            Screen("DivineVFX_Silphir_Burst", "Silphir_Burst", TexRoot + "/Wind/Screen_Anim_Wind_001.png",
                   frameDuration: 0.09f, frameRate: 100f, lifetime: 0.8f, fadeOut: 0.25f,
                   tint: new Color(0.70f, 1f, 0.82f)),
            // 버프 지속(12s) 동안 플레이어를 따라다니는 질주 궤적 루프.
            // 수명 0 — CastPartyHaste 가 Spawn(duration) 으로 덮어쓴다.
            Status("DivineVFX_Silphir_Haste", "Silphir_Haste", TexRoot + "/Wind/WindGust.png",
                   frameDuration: 0.07f, frameRate: 100f, fadeOut: 0.3f,
                   scale: 1.5f, tint: new Color(0.75f, 1f, 0.85f)),

            // ── 페룸 · 강철의 마왕 (SingleBurst ×30, 보스 특화) — 강철 일섬 ──
            // 흰 초승달 참격이 화면을 가른다 — 강철빛 틴트. 임팩트 착탄(0.3s)과 맞물리는 0.4s.
            Screen("DivineVFX_Ferrum_Burst", "Ferrum_Burst", TexRoot + "/Attack Slash/Screen_Anim_Attack_001.png",
                   frameDuration: 0.08f, frameRate: 25f, lifetime: 0.65f, fadeOut: 0.18f,
                   tint: new Color(0.78f, 0.85f, 0.96f)),
            // 대검 참격파(Slash_attack_004 — 팩에서 가장 묵직한 검격. 플레이어 VFX 는 007 사용).
            Impact("DivineVFX_Ferrum_Impact", "Ferrum_Impact", TexRoot + "/Attack Slash/Slash_attack_004.png",
                   frameDuration: 0.06f, frameRate: 50f, lifetime: 0.55f, fadeOut: 0.15f,
                   tint: new Color(0.85f, 0.90f, 1f)),

            // ── 호라 · 시간의 여신 (AoeBurst + Slow 8s) — 시간 동결 ──
            // 서리 결정이 화면 가장자리에서 자란다 — 보랏빛 틴트로 '얼음'이 아니라 '시간 결정'으로 읽힌다.
            Screen("DivineVFX_Hora_Burst", "Hora_Burst", TexRoot + "/Ice/Screen_Anim_Ice_001.png",
                   frameDuration: 0.11f, frameRate: 100f, lifetime: 1.0f, fadeOut: 0.3f,
                   tint: new Color(0.80f, 0.72f, 1f)),
            // 시공 파열(마젠타 별폭발 → 남보라 틴트. 녹스의 무틴트 마젠타 돔과 구분된다).
            Impact("DivineVFX_Hora_Impact", "Hora_Impact", TexRoot + "/Void/VoidExplosion2.png",
                   frameDuration: 0.06f, frameRate: 50f, lifetime: 0.5f, fadeOut: 0.12f,
                   tint: new Color(0.55f, 0.68f, 1f)),
            // Slow 지속(8s) 동안 머리 위에 도는 시간 소용돌이 — 일부러 느린 0.72s 주기.
            Status("DivineVFX_Hora_Slow", "Hora_Slow", TexRoot + "/Void/VoidSpin.png",
                   frameDuration: 0.12f, frameRate: 25f, fadeOut: 0.2f,
                   scale: 2f, tint: new Color(0.55f, 0.75f, 1f)),

            // ── 이그니스 · 폭염의 마왕 (Dot 6히트/6s) — 폭염 지대 ──
            // 화염이 화면을 핥는 점화 컷. 원본이 이미 주황이라 무틴트.
            Screen("DivineVFX_Ignis_Burst", "Ignis_Burst", TexRoot + "/Fire/Screen_Anim_Fire_001.png",
                   frameDuration: 0.1f, frameRate: 10f, lifetime: 1.2f, fadeOut: 0.35f, tint: NoTint),
            // 히트당 임팩트 — 30마리 x 6히트라 가장 싸야 한다: 64px 6프레임, 수명 0.4s.
            Impact("DivineVFX_Ignis_Impact", "Ignis_Impact", TexRoot + "/Fire/FireExplosion1.png",
                   frameDuration: 0.06f, frameRate: 50f, lifetime: 0.4f, fadeOut: 0.1f, tint: NoTint),

            // ── 녹스 · 심연의 마왕 (AoeBurst + Stun 3s 후 폭발, castDelay 3.0) — 심연 개방 ──
            // 유일한 루프 버스트: 원샷이면 3초 정지 연출 동안 화면이 비어 버린다.
            // 0.7s 주기로 소용돌이가 고동치다가(개폐 5프레임) 수명 3.6s 에서 붕괴 페이드.
            new PrefabSpec
            {
                prefabName = "DivineVFX_Nox_Burst",
                root = new LayerSpec
                {
                    clipName = "Nox_Burst",
                    texturePath = NoxBurstTex,           // 알파 0.6 장막 사본 — 색은 원본 그대로
                    frameCount = 5, frameDuration = 0.14f, clipFrameRate = 50f, loop = true,
                    sortingOrder = 900, tint = NoTint,   // 마젠타/흑 원본이 심연 그 자체
                },
                children = null,
                lifetime = 3.6f, fadeOut = 0.5f, fitToCamera = true, localScale = Vector3.one,
            },
            // 붕괴 폭발 — 마젠타 돔 파열. 신화 카드라 임팩트도 한 프레임 길게(0.42s).
            Impact("DivineVFX_Nox_Impact", "Nox_Impact", TexRoot + "/Void/VoidExplosion1.png",
                   frameDuration: 0.07f, frameRate: 100f, lifetime: 0.6f, fadeOut: 0.15f, tint: NoTint),
            // 보라 기절 별(재채색 사본) — 아스트라의 금색 별과 같은 원본, 다른 색.
            // 수명 0 — MonsterCCState 가 남은 CC 시간으로 덮어쓴다. 32px(PPU 32) x 스케일 2.
            Status("DivineVFX_Nox_Stun", "Nox_Stun", NoxStunTex,
                   frameDuration: 0.06f, frameRate: 50f, fadeOut: 0.15f,
                   scale: 2f, tint: NoTint, frameCount: 11),
        };

        // ────────────────────────────────────────────
        //  카드 배선 값 7종
        // ────────────────────────────────────────────
        // SFX 는 eSFXType 실존 항목만(nameof 로 컴파일 타임 검증). 스킬 종류별로 실제
        // 재생되는 슬롯만 채운다 — impactSfx 는 AoeBurst/SingleBurst 에서만 재생된다
        // (Dot/HealAndGuard/PartyHaste 는 DivineSkillCaster 가 호출하지 않음 → 빈 문자열).
        private static CardDef[] CardDefs => new[]
        {
            new CardDef
            {
                // 빛은 빨리 닿는다 — 짧은 지연, 촘촘한 스태거로 금빛 섬광이 전장을 훑는다.
                nameEng = "Lumen",
                burst = "DivineVFX_Lumen_Burst", impact = "DivineVFX_Lumen_Impact", status = null,
                sfx = nameof(eSFXType.Charge_Shot_SFX), impactSfx = nameof(eSFXType.Lightning_SFX),
                impactDelay = 0.3f, impactStagger = 0.03f,
                burstLifetime = 0.9f, impactLifetime = 0.5f, impactScale = 2.2f,
                statusOffset = new Vector3(0f, 1f, 0f),
                shake = true, shakeDuration = 0.25f, shakeMagnitude = 0.1f,
            },
            new CardDef
            {
                // 회복 스킬은 화면을 흔들지 않는다. 방어막 결정이라 '얼음 결정' SFX 가 제일 맞는다.
                // statusVfx 없음 — CastHealAndGuard 는 statusVfxPrefab 을 스폰하지 않는다(실증).
                nameEng = "Gaien",
                burst = "DivineVFX_Gaien_Burst", impact = "DivineVFX_Gaien_Impact", status = null,
                sfx = nameof(eSFXType.Ice_Block_SFX), impactSfx = "",
                impactDelay = 0.25f, impactStagger = 0.04f,
                burstLifetime = 1.1f, impactLifetime = 0.65f, impactScale = 2.4f,
                statusOffset = new Vector3(0f, 1f, 0f),
                shake = false, shakeDuration = 0.2f, shakeMagnitude = 0.08f,
            },
            new CardDef
            {
                // impactVfx 없음 — CastPartyHaste 는 SpawnImpact 를 호출하지 않는다(실증).
                // 질주 궤적은 발치 근처(+0.2)라 캐릭터와 겹쳐 '속도선'으로 읽힌다.
                nameEng = "Silphir",
                burst = "DivineVFX_Silphir_Burst", impact = null, status = "DivineVFX_Silphir_Haste",
                sfx = nameof(eSFXType.Dash), impactSfx = "",
                impactDelay = 0.25f, impactStagger = 0.04f,
                burstLifetime = 0.8f, impactLifetime = 0.6f, impactScale = 2f,
                statusOffset = new Vector3(0f, 0.2f, 0f),
                shake = false, shakeDuration = 0.2f, shakeMagnitude = 0.08f,
            },
            new CardDef
            {
                // 단일 대상 일격 — 일섬이 화면 중앙을 지나는 0.3s 에 착탄. 격한 흔들림 + 강철 클랭.
                nameEng = "Ferrum",
                burst = "DivineVFX_Ferrum_Burst", impact = "DivineVFX_Ferrum_Impact", status = null,
                sfx = nameof(eSFXType.Slash_Attack_SFX), impactSfx = nameof(eSFXType.Parrying_SFX),
                impactDelay = 0.3f, impactStagger = 0f,
                burstLifetime = 0.65f, impactLifetime = 0.55f, impactScale = 3.2f,
                statusOffset = new Vector3(0f, 1f, 0f),
                shake = true, shakeDuration = 0.35f, shakeMagnitude = 0.22f,
            },
            new CardDef
            {
                // 시간이 얼어붙고(결정 SFX) → 한 박자 뒤 시공이 깨진다. 스태거를 넉넉히 줘서
                // 파열이 물결처럼 번지는 그림. Slow 소용돌이는 머리 위(+1.1, 아스트라 관례).
                nameEng = "Hora",
                burst = "DivineVFX_Hora_Burst", impact = "DivineVFX_Hora_Impact", status = "DivineVFX_Hora_Slow",
                sfx = nameof(eSFXType.Ice_Spike_SFX), impactSfx = nameof(eSFXType.Ice_Block_SFX),
                impactDelay = 0.35f, impactStagger = 0.06f,
                burstLifetime = 1.0f, impactLifetime = 0.5f, impactScale = 2f,
                statusOffset = new Vector3(0f, 1.1f, 0f),
                shake = true, shakeDuration = 0.25f, shakeMagnitude = 0.12f,
            },
            new CardDef
            {
                // Dot — 히트마다 전 몬스터가 맞으므로 임팩트는 작게(1.2배)·짧게. 흔들림은
                // 코드상 첫 히트에만 발생하니 가볍게 준다. 스태거는 Dot 루틴이 안 쓴다 → 0.
                nameEng = "Ignis",
                burst = "DivineVFX_Ignis_Burst", impact = "DivineVFX_Ignis_Impact", status = null,
                sfx = nameof(eSFXType.Fire_Tornado_SFX), impactSfx = "",
                impactDelay = 0.3f, impactStagger = 0f,
                burstLifetime = 1.2f, impactLifetime = 0.4f, impactScale = 1.2f,
                statusOffset = new Vector3(0f, 1f, 0f),
                shake = true, shakeDuration = 0.2f, shakeMagnitude = 0.1f,
            },
            new CardDef
            {
                // castDelay 3.0(SO 데이터, 여기서는 건드리지 않는다) 동안 소용돌이가 루프로 고동치고
                // 기절 별이 3초를 세운 뒤, +0.4s 에 심연이 붕괴하며 최대 흔들림. 굉음(회오리) → 뇌격.
                nameEng = "Nox",
                burst = "DivineVFX_Nox_Burst", impact = "DivineVFX_Nox_Impact", status = "DivineVFX_Nox_Stun",
                sfx = nameof(eSFXType.Fire_Tornado2_SFX), impactSfx = nameof(eSFXType.Lightning_SFX),
                impactDelay = 0.4f, impactStagger = 0.04f,
                burstLifetime = 3.6f, impactLifetime = 0.6f, impactScale = 2.6f,
                statusOffset = new Vector3(0f, 1.1f, 0f),
                shake = true, shakeDuration = 0.4f, shakeMagnitude = 0.2f,
            },
        };

        // ────────────────────────────────────────────
        //  메뉴
        // ────────────────────────────────────────────
        [MenuItem("KingdomIdle/Divine/Generate All Card VFX")]
        public static void GenerateAllCardVfx()
        {
            DivineVfxGen.EnsureFolders();

            if (!DivineVfxGen.SortingLayerExists(SortingLayerName))
            {
                Debug.LogError($"[DivineVFX7] 정렬 레이어 '{SortingLayerName}' 가 없습니다 " +
                               "(Project Settings > Tags and Layers). 몬스터 뒤에 가려지므로 생성을 중단합니다.");
                return;
            }

            // 1) 재채색 사본 4종 (루멘 버스트/임팩트 = 금, 녹스 기절 별 = 보라, 녹스 버스트 = 장막)
            bool artOk =
                EnsureRecoloredTexture(LumenBurstSrc, LumenBurstTex, eRamp.Gold,
                                       count: 5, frameW: 512, frameH: 320, vertical: true,  ppu: 64f, maxSize: 2048) &
                EnsureRecoloredTexture(LumenImpactSrc, LumenImpactTex, eRamp.Gold,
                                       count: 6, frameW: 64, frameH: 64, vertical: true,  ppu: 64f, maxSize: 512) &
                EnsureRecoloredTexture(NoxStunSrc, NoxStunTex, eRamp.Purple,
                                       count: 11, frameW: 32, frameH: 32, vertical: false, ppu: 32f, maxSize: 512) &
                EnsureRecoloredTexture(NoxBurstSrc, NoxBurstTex, eRamp.Veil,
                                       count: 5, frameW: 512, frameH: 320, vertical: true,  ppu: 64f, maxSize: 2048);
            if (!artOk)
            {
                Debug.LogError("[DivineVFX7] 재채색 아트 준비에 실패했습니다. 생성을 중단합니다.");
                return;
            }

            // 2) 프리팹 16종
            var specs = PrefabSpecs;
            int made = 0;
            foreach (var spec in specs)
                if (BuildPrefab(spec) != null) made++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DivineVFX7] 7카드 VFX 프리팹 {made}/{specs.Length} 생성/갱신 완료 → {VfxDir}");

            // 3) 카드 배선 — 일부 프리팹이 실패했어도 완성된 카드는 배선한다(카드별 검증 포함)
            WireAllCards();
        }

        [MenuItem("KingdomIdle/Divine/Wire 7 Card Presentation")]
        public static void WireAllCards()
        {
            int wired = 0;
            foreach (var def in CardDefs)
                if (WireCard(def)) wired++;

            AssetDatabase.SaveAssets();
            Debug.Log($"[DivineVFX7] 카드 연출 배선 {wired}/7 완료.");
        }

        // ────────────────────────────────────────────
        //  카드 배선
        // ────────────────────────────────────────────
        private static bool WireCard(CardDef def)
        {
            string cardPath = $"{SoDir}/DivineSkill_{def.nameEng}.asset";
            var card = AssetDatabase.LoadAssetAtPath<DivineSkillSO>(cardPath);
            if (card == null)
            {
                Debug.LogError($"[DivineVFX7] 카드를 찾지 못했습니다: {cardPath}. " +
                               "먼저 'KingdomIdle/Divine/Generate Cards + Registry' 를 실행하세요.");
                return false;
            }
            if (card.nameEng != def.nameEng)
                Debug.LogWarning($"[DivineVFX7] {cardPath} 의 nameEng 가 '{def.nameEng}' 가 아닙니다 " +
                                 $"(현재 '{card.nameEng}', id {card.id}). 그래도 배선은 진행합니다.");

            // 이 카드가 요구하는 프리팹이 하나라도 없으면 배선하지 않는다(반쪽 연출 방지)
            var burst  = def.burst  != null ? DivineVfxGen.LoadPrefab(def.burst)  : null;
            var impact = def.impact != null ? DivineVfxGen.LoadPrefab(def.impact) : null;
            var status = def.status != null ? DivineVfxGen.LoadPrefab(def.status) : null;
            if ((def.burst != null && burst == null) ||
                (def.impact != null && impact == null) ||
                (def.status != null && status == null))
            {
                Debug.LogError($"[DivineVFX7] '{def.nameEng}' 의 VFX 프리팹이 아직 없습니다. " +
                               "먼저 'KingdomIdle/Divine/Generate All Card VFX' 를 실행하세요.");
                return false;
            }

            var so = new SerializedObject(card);
            DivineVfxGen.SetObj (so, "burstVfxPrefab",  burst);
            DivineVfxGen.SetObj (so, "impactVfxPrefab", impact);
            DivineVfxGen.SetObj (so, "statusVfxPrefab", status);

            DivineVfxGen.SetStr (so, "sfxName",       def.sfx);
            DivineVfxGen.SetStr (so, "impactSfxName", def.impactSfx);

            DivineVfxGen.SetF   (so, "impactDelay",       def.impactDelay);
            DivineVfxGen.SetF   (so, "impactStagger",     def.impactStagger);
            DivineVfxGen.SetF   (so, "burstVfxLifetime",  def.burstLifetime);
            DivineVfxGen.SetF   (so, "impactVfxLifetime", def.impactLifetime);
            DivineVfxGen.SetF   (so, "impactVfxScale",    def.impactScale);
            DivineVfxGen.SetVec3(so, "statusVfxOffset",   def.statusOffset);

            DivineVfxGen.SetBool(so, "screenShake",    def.shake);
            DivineVfxGen.SetF   (so, "shakeDuration",  def.shakeDuration);
            DivineVfxGen.SetF   (so, "shakeMagnitude", def.shakeMagnitude);

            DivineVfxGen.SetBool(so, "cutInEnabled",  true);
            DivineVfxGen.SetF   (so, "cutInDuration", 1.2f);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);

            Debug.Log($"[DivineVFX7] '{card.DisplayName} — {card.skillNameKor}' (id {card.id}) 연출 배선 완료: " +
                      $"burst={(burst != null ? burst.name : "-")}, impact={(impact != null ? impact.name : "-")}, " +
                      $"status={(status != null ? status.name : "-")}, sfx={def.sfx}/{def.impactSfx}");
            return true;
        }

        // ────────────────────────────────────────────
        //  프리팹 생성 (루트 + 자식 레이어)
        // ────────────────────────────────────────────
        private static GameObject BuildPrefab(PrefabSpec spec)
        {
            // 레이어별 클립/컨트롤러/첫 프레임을 먼저 전부 준비 — 하나라도 실패하면 프리팹을 건드리지 않는다
            if (!BuildLayerAssets(spec.root, out var rootFrames, out var rootCtrl)) return null;

            var childFrames = new List<Sprite[]>();
            var childCtrls  = new List<UnityEditor.Animations.AnimatorController>();
            if (spec.children != null)
            {
                foreach (var child in spec.children)
                {
                    if (!BuildLayerAssets(child, out var frames, out var ctrl)) return null;
                    childFrames.Add(frames);
                    childCtrls.Add(ctrl);
                }
            }

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

                ApplyLayer(root, spec.root, rootFrames[0], rootCtrl);

                // 자식 레이어 — 이름으로 찾아 재사용(멱등), 스펙에 없는 자식은 정리한다
                var expected = new HashSet<string>();
                if (spec.children != null)
                {
                    for (int i = 0; i < spec.children.Length; i++)
                    {
                        var child = spec.children[i];
                        expected.Add(child.clipName);

                        var t = root.transform.Find(child.clipName);
                        GameObject go = t != null ? t.gameObject : new GameObject(child.clipName);
                        go.transform.SetParent(root.transform, false);
                        go.layer = 0;
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                        go.transform.localScale    = Vector3.one;

                        ApplyLayer(go, child, childFrames[i][0], childCtrls[i]);
                    }
                }
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var c = root.transform.GetChild(i);
                    if (!expected.Contains(c.name)) Object.DestroyImmediate(c.gameObject);
                }

                var inst = DivineVfxGen.GetOrAdd<DivineVfxInstance>(root);
                inst.lifetime     = spec.lifetime;
                inst.fadeOut      = spec.fadeOut;
                inst.fitToCamera  = spec.fitToCamera;
                inst.followTarget = null;
                inst.followOffset = Vector3.zero;

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok || saved == null)
                {
                    Debug.LogError($"[DivineVFX7] 프리팹 저장에 실패했습니다: {path}");
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

        /// <summary>레이어 1장의 클립 + 컨트롤러 + 프레임을 준비한다. 실패 시 false + 에러 로그.</summary>
        private static bool BuildLayerAssets(LayerSpec layer, out Sprite[] frames,
                                             out UnityEditor.Animations.AnimatorController controller)
        {
            controller = null;
            frames = DivineVfxGen.LoadOrderedSprites(layer.texturePath, layer.frameCount);
            if (frames == null) return false;

            var clipSpec = new DivineVfxGen.VfxSpec
            {
                clipName      = layer.clipName,
                frameCount    = layer.frameCount,
                frameDuration = layer.frameDuration,
                clipFrameRate = layer.clipFrameRate,
                loop          = layer.loop,
            };
            var clip = DivineVfxGen.BuildClip(clipSpec, frames);
            if (clip == null) return false;

            controller = DivineVfxGen.BuildController(layer.clipName, clip);
            return controller != null;
        }

        /// <summary>GameObject 1개에 SpriteRenderer + Animator 를 레이어 스펙대로 얹는다.</summary>
        private static void ApplyLayer(GameObject go, LayerSpec layer, Sprite firstFrame,
                                       UnityEditor.Animations.AnimatorController controller)
        {
            var sr = DivineVfxGen.GetOrAdd<SpriteRenderer>(go);
            sr.sprite           = firstFrame;     // fitToCamera 가 bounds 를 읽으므로 비워둘 수 없다
            sr.sharedMaterial   = DivineVfxGen.SpritesDefaultMaterial();
            sr.color            = layer.tint;     // 곱연산 틴트 — DivineVfxInstance 페이드는 알파만 만진다
            sr.sortingLayerName = SortingLayerName;
            sr.sortingOrder     = layer.sortingOrder;
            sr.flipX = false;
            sr.flipY = false;

            var animator = DivineVfxGen.GetOrAdd<Animator>(go);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // ────────────────────────────────────────────
        //  재채색 사본 (복사 + 픽셀 램프 + 슬라이스)
        // ────────────────────────────────────────────
        /// <summary>
        /// 원본 PNG 를 ArtDir 로 재채색 복사하고 임포터 설정 + 그리드 슬라이스까지 마친다.
        /// 사본 파일이 이미 있으면 픽셀 작업은 건너뛴다(재생성하려면 사본을 삭제).
        /// vertical=true 면 세로 스트립 — 프레임 0 이 시트 '맨 위'다(외부 팩 관례와 동일).
        /// </summary>
        private static bool EnsureRecoloredTexture(string srcPath, string dstPath, eRamp ramp,
                                                   int count, int frameW, int frameH,
                                                   bool vertical, float ppu, int maxSize)
        {
            string absDst = DivineVfxGen.ToAbsolutePath(dstPath);
            if (!File.Exists(absDst))
            {
                string absSrc = DivineVfxGen.ToAbsolutePath(srcPath);
                if (!File.Exists(absSrc))
                {
                    Debug.LogError($"[DivineVFX7] 재채색 원본 아트를 찾지 못했습니다: {srcPath}");
                    return false;
                }

                // 임포터의 readable 설정과 무관하게 PNG 바이트에서 직접 읽는다
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(absSrc)))
                {
                    Object.DestroyImmediate(tex);
                    Debug.LogError($"[DivineVFX7] PNG 디코드에 실패했습니다: {srcPath}");
                    return false;
                }

                var pixels = tex.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = MapPixel(pixels[i], ramp);
                tex.SetPixels32(pixels);
                tex.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(absDst));
                File.WriteAllBytes(absDst, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[DivineVFX7] 재채색 사본 생성({ramp}): {srcPath} → {dstPath}");
            }

            if (!ConfigureImporter(dstPath, ppu, maxSize)) return false;

            // 이미 올바르게 잘려 있으면 건드리지 않는다 — 다시 자르면 서브 스프라이트 ID 가 새로 발급돼
            // 이 텍스처를 참조하던 클립/프리팹이 전부 끊긴다.
            if (DivineVfxGen.CountSubSprites(dstPath) == count) return true;

            return SliceGrid(dstPath, Path.GetFileNameWithoutExtension(dstPath),
                             count, frameW, frameH, vertical);
        }

        /// <summary>
        /// 밝기(최대 채널) 기반 색 램프. 원본의 흰색 코어(최소 채널이 높은 픽셀)는 보존해
        /// 섬광의 '뜨거운 중심'이 살아남는다. 알파는 그대로(Veil 만 0.6배).
        /// </summary>
        private static Color32 MapPixel(Color32 c, eRamp ramp)
        {
            if (c.a == 0) return c;   // 완전 투명은 건드릴 필요 없다

            if (ramp == eRamp.Veil)
                return new Color32(c.r, c.g, c.b, (byte)Mathf.RoundToInt(c.a * 0.6f));

            float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / 255f;
            float w = Mathf.Min(c.r, Mathf.Min(c.g, c.b)) / 255f;
            float keepWhite = Mathf.Clamp01((w - 0.6f) / 0.4f);   // 흰색에 가까울수록 원본 유지

            Color target = ramp == eRamp.Gold
                ? new Color(Mathf.Clamp01(v * 1.08f), Mathf.Clamp01(v * 0.86f), Mathf.Clamp01(v * v * 0.42f))
                : new Color(Mathf.Clamp01(v * 0.72f), Mathf.Clamp01(v * 0.40f), Mathf.Clamp01(v * 1.15f));

            var src = new Color(c.r / 255f, c.g / 255f, c.b / 255f);
            Color o = Color.Lerp(target, src, keepWhite);

            return new Color32((byte)Mathf.RoundToInt(o.r * 255f),
                               (byte)Mathf.RoundToInt(o.g * 255f),
                               (byte)Mathf.RoundToInt(o.b * 255f), c.a);
        }

        /// <summary>EnsureStunTexture(DivineVfxGen)와 동일한 픽셀 아트 임포트 설정.</summary>
        private static bool ConfigureImporter(string assetPath, float ppu, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[DivineVFX7] {assetPath} 의 TextureImporter 를 얻지 못했습니다.");
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
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, ppu))
            { importer.spritePixelsPerUnit = ppu; changed = true; }
            if (!importer.alphaIsTransparency)
            { importer.alphaIsTransparency = true; changed = true; }
            if (importer.mipmapEnabled)
            { importer.mipmapEnabled = false; changed = true; }
            if (importer.npotScale != TextureImporterNPOTScale.None)
            { importer.npotScale = TextureImporterNPOTScale.None; changed = true; }
            if (importer.maxTextureSize != maxSize)
            { importer.maxTextureSize = maxSize; changed = true; }
            // 픽셀 아트 — 블록 압축은 하드 에지를 뭉갠다 (아스트라 기절 별과 동일 판단)
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }

            if (changed) importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// 1행/1열 그리드 슬라이스. Unity 6 은 ISpriteEditorDataProvider 를 쓴다.
        /// SpriteRect.rect 는 텍스처 좌표계(좌하단 원점) — 세로 스트립의 프레임 0(맨 위)은
        /// rect.y 가 가장 커야 한다. 가로 스트립은 y 항상 0.
        /// </summary>
        private static bool SliceGrid(string assetPath, string baseName,
                                      int count, int width, int height, bool vertical)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (importer == null || tex == null)
            {
                Debug.LogError($"[DivineVFX7] {assetPath} 를 다시 읽지 못했습니다.");
                return false;
            }

            int needW = vertical ? width : count * width;
            int needH = vertical ? count * height : height;
            if (tex.width < needW || tex.height < needH)
            {
                Debug.LogError($"[DivineVFX7] {assetPath} 크기가 {tex.width}x{tex.height} 라 " +
                               $"{width}x{height} {count}장을 자를 수 없습니다.");
                return false;
            }

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null)
            {
                Debug.LogError($"[DivineVFX7] {assetPath} 의 ISpriteEditorDataProvider 를 얻지 못했습니다.");
                return false;
            }
            provider.InitSpriteEditorDataProvider();

            var rects = new SpriteRect[count];
            for (int i = 0; i < count; i++)
            {
                var rect = vertical
                    ? new Rect(0f, tex.height - (i + 1) * height, width, height)   // 프레임 0 = 맨 위
                    : new Rect(i * width, 0f, width, height);
                rects[i] = new SpriteRect
                {
                    name      = $"{baseName}_{i}",
                    rect      = rect,
                    alignment = SpriteAlignment.Center,
                    pivot     = new Vector2(0.5f, 0.5f),
                    border    = Vector4.zero,
                };
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();

            int have = DivineVfxGen.CountSubSprites(assetPath);
            if (have == count) return true;

            Debug.LogError($"[DivineVFX7] {assetPath} 슬라이스 후 서브 스프라이트가 {have}개입니다 " +
                           $"(기대값 {count}).");
            return false;
        }

        // ────────────────────────────────────────────
        //  스펙 축약 헬퍼 — 표를 짧게 유지한다
        // ────────────────────────────────────────────
        /// <summary>화면 전체 버스트(원샷, fitToCamera, 512x320 5프레임, 정렬 900).</summary>
        private static PrefabSpec Screen(string prefabName, string clipName, string texturePath,
                                         float frameDuration, float frameRate,
                                         float lifetime, float fadeOut, Color tint)
        {
            return new PrefabSpec
            {
                prefabName = prefabName,
                root = new LayerSpec
                {
                    clipName = clipName, texturePath = texturePath,
                    frameCount = 5, frameDuration = frameDuration, clipFrameRate = frameRate,
                    loop = false, sortingOrder = 900, tint = tint,
                },
                children = null,
                lifetime = lifetime, fadeOut = fadeOut, fitToCamera = true, localScale = Vector3.one,
            };
        }

        /// <summary>대상 지점 임팩트(원샷, 64x64 6프레임, 정렬 800).
        /// 프리팹 스케일은 1 — 확대는 카드의 impactVfxScale 이 담당한다(아스트라 관례).</summary>
        private static PrefabSpec Impact(string prefabName, string clipName, string texturePath,
                                         float frameDuration, float frameRate,
                                         float lifetime, float fadeOut, Color tint)
        {
            return new PrefabSpec
            {
                prefabName = prefabName,
                root = new LayerSpec
                {
                    clipName = clipName, texturePath = texturePath,
                    frameCount = 6, frameDuration = frameDuration, clipFrameRate = frameRate,
                    loop = false, sortingOrder = 800, tint = tint,
                },
                children = null,
                lifetime = lifetime, fadeOut = fadeOut, fitToCamera = false, localScale = Vector3.one,
            };
        }

        /// <summary>상태이상/버프 루프(수명 0 — CC/버프 시스템이 Spawn 인자로 수명을 소유, 정렬 950).
        /// 상태이상 연출에는 스케일 필드가 없으므로 프리팹에 스케일을 굽는다(아스트라 관례).</summary>
        private static PrefabSpec Status(string prefabName, string clipName, string texturePath,
                                         float frameDuration, float frameRate, float fadeOut,
                                         float scale, Color tint, int frameCount = 6)
        {
            return new PrefabSpec
            {
                prefabName = prefabName,
                root = new LayerSpec
                {
                    clipName = clipName, texturePath = texturePath,
                    frameCount = frameCount, frameDuration = frameDuration, clipFrameRate = frameRate,
                    loop = true, sortingOrder = 950, tint = tint,
                },
                children = null,
                lifetime = 0f, fadeOut = fadeOut, fitToCamera = false,
                localScale = new Vector3(scale, scale, 1f),
            };
        }
    }
}
