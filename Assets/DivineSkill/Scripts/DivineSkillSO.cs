using UnityEngine;

namespace KingdomIdle.Divine
{
    /// <summary>
    /// 신 스킬 카드 한 장의 데이터. 여신·마왕 등 초월자 1인 = 카드 1장 = 스킬 1종.
    /// 마탑 스킬(상시 회전 지속딜)과 달리 파티 공용 1슬롯에 장착되는 궁극기다.
    ///
    /// 아트/연출 필드는 전부 선택 사항이다 — 비어 있어도 수치는 그대로 적용되고
    /// 연출만 생략된다(아트 미완성 상태에서도 게임이 깨지지 않게).
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomIdle/Divine/Skill Card", fileName = "DivineSkill_New")]
    public class DivineSkillSO : ScriptableObject
    {
        [Header("식별")]
        public int id;
        public string nameEng;
        /// <summary>카드(초월자) 이름 — 예: "새벽의 여신 루멘".</summary>
        public string nameKor;
        /// <summary>스킬 이름 — 예: "여명의 심판".</summary>
        public string skillNameKor;
        [TextArea(2, 4)]
        public string description;
        public eDivineGrade grade = eDivineGrade.Hero;
        [Tooltip("시각 컨셉 — 궁극기 버튼 링 프레임/시전 VFX 색 결정.")]
        public eDivineConcept concept = eDivineConcept.Holy;

        [Header("사용")]
        [Tooltip("쿨타임(초). 스테이지 진입 시 초기화된다.")]
        public float cooldown = 30f;

        [Header("아트")]
        [Tooltip("HUD 슬롯·컬렉션 그리드용 정사각 아이콘.")]
        public Sprite icon;
        [Tooltip("컬렉션북(도감) 상세용 스탠딩 일러스트.")]
        public Sprite illustration;
        [Tooltip("컷인 전용 컷씬 컷아웃(투명 배경). 비면 illustration → icon 순으로 대체된다.")]
        public Sprite cutInIllustration;
        [Tooltip("궁극기 버튼 컨셉 링 프레임(176px 원형, 중앙 투명). 비면 기본 청동 링 유지.")]
        public Sprite buttonRingSprite;

        [Header("효과")]
        public eDivineEffectKind effectKind = eDivineEffectKind.AoeBurst;
        /// <summary>
        /// 공격형: 파티 ATK합에 곱할 배율. 회복형: 대상 MAXHP 비율(0.25 = 25%).
        /// 등급 계수가 이미 곱해진 최종 값을 넣는다.
        /// </summary>
        public float skillMult = 12f;
        [Tooltip("Dot 전용 — 총 히트 수. skillMult 는 1히트 기준 배율이다.")]
        public int hitCount = 1;
        [Tooltip("Dot / HealAndGuard / PartyHaste 의 지속시간(초).")]
        public float duration = 0f;
        [Tooltip("군중 제어를 먼저 걸고 데미지를 넣기까지의 지연(초). 0이면 즉발.")]
        public float castDelay = 0f;

        [Header("군중 제어 (공격형 전용)")]
        public eDivineCrowdControl crowdControl = eDivineCrowdControl.None;
        public float ccDuration = 0f;
        [Tooltip("Slow 일 때 감소 비율 (0.5 = 이동속도 -50%).")]
        [Range(0f, 1f)] public float slowPercent = 0.5f;

        [Header("보호 / 가속 (지원형 전용)")]
        [Tooltip("HealAndGuard — 받는 피해 감소 비율 (0.2 = -20%).")]
        [Range(0f, 0.9f)] public float damageReducePercent = 0.2f;
        [Tooltip("PartyHaste — 기본 스킬 간격 감소 비율 (0.3 = -30%).")]
        [Range(0f, 0.9f)] public float skillIntervalReducePercent = 0.3f;
        [Tooltip("PartyHaste — 이동속도 증가 비율 (0.3 = +30%).")]
        [Range(0f, 2f)] public float moveSpeedIncreasePercent = 0.3f;

        // ────────────────────────────────────────────
        //  연출 (Presentation)
        // ────────────────────────────────────────────
        [Header("연출 — VFX")]
        [Tooltip("전장 전체를 덮는 시전 연출 프리팹. DivineVfxInstance.fitToCamera 로 화면을 덮는다.")]
        public GameObject burstVfxPrefab;
        [Tooltip("대상 1기마다 터지는 타격 연출 프리팹.")]
        public GameObject impactVfxPrefab;
        [Tooltip("군중 제어가 걸린 동안 대상 머리 위에 유지되는 상태이상 연출 프리팹.")]
        public GameObject statusVfxPrefab;
        [Tooltip("(구버전 호환) burstVfxPrefab 이 비었을 때 대신 쓰는 프리팹.")]
        public GameObject vfxPrefab;

        [Tooltip("전체 연출 프리팹 유지 시간(초).")]
        public float burstVfxLifetime = 1.2f;
        [Tooltip("타격 연출 프리팹 유지 시간(초).")]
        public float impactVfxLifetime = 0.6f;
        [Tooltip("타격 연출 스케일 배수. 원본이 작을 때 키운다.")]
        public float impactVfxScale = 2f;
        [Tooltip("상태이상 연출을 띄울 대상 기준 높이.")]
        public Vector3 statusVfxOffset = new Vector3(0f, 1.0f, 0f);

        [Header("연출 — 타이밍")]
        [Tooltip("시전 연출이 시작되고 실제 데미지가 들어가기까지의 지연(초). 타격감의 핵심.")]
        public float impactDelay = 0.25f;
        [Tooltip("대상별 타격 간 간격(초). 0이면 전부 동시에 맞는다.")]
        public float impactStagger = 0.04f;

        [Header("연출 — 카메라/사운드")]
        public bool screenShake = true;
        public float shakeDuration = 0.25f;
        public float shakeMagnitude = 0.12f;
        [Tooltip("시전 SFX 이름 (eSFXType 항목과 정확히 일치해야 함).")]
        public string sfxName;
        [Tooltip("타격 SFX 이름 (eSFXType 항목과 정확히 일치해야 함). 대상마다 재생하지 않고 1회만 재생한다.")]
        public string impactSfxName;

        [Header("연출 — 컷인")]
        [Tooltip("수동 시전 시 전용 컷인을 재생한다.")]
        public bool cutInEnabled = true;
        [Tooltip("컷인 길이(초). 이 시간이 지난 뒤 스킬이 실제로 발동한다.")]
        public float cutInDuration = 1.2f;

        // ── 조회 헬퍼 ──
        /// <summary>UI 표시용 이름. 카드명이 비어 있으면 에셋 이름을 쓴다.</summary>
        public string DisplayName => string.IsNullOrEmpty(nameKor) ? name : nameKor;

        /// <summary>전체 연출 프리팹 (구버전 필드 fallback 포함).</summary>
        public GameObject BurstPrefab => burstVfxPrefab != null ? burstVfxPrefab : vfxPrefab;

        /// <summary>공격형(데미지를 넣는) 카드인지.</summary>
        public bool IsOffensive =>
            effectKind == eDivineEffectKind.AoeBurst ||
            effectKind == eDivineEffectKind.SingleBurst ||
            effectKind == eDivineEffectKind.Dot;

        public static string GetGradeName(eDivineGrade grade)
        {
            switch (grade)
            {
                case eDivineGrade.Legend: return "전설";
                case eDivineGrade.Myth:   return "신화";
                default:                  return "영웅";
            }
        }

        /// <summary>등급 대표 색 (UI 테두리·이름 색).</summary>
        public static Color GetGradeColor(eDivineGrade grade)
        {
            switch (grade)
            {
                case eDivineGrade.Legend: return new Color(1f, 0.78f, 0.28f, 1f);       // 금
                case eDivineGrade.Myth:   return new Color(1f, 0.42f, 0.36f, 1f);       // 홍
                default:                  return new Color(0.55f, 0.75f, 1f, 1f);       // 청
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (cooldown < 1f) cooldown = 1f;
            if (hitCount < 1) hitCount = 1;
            if (impactDelay < 0f) impactDelay = 0f;
            if (impactStagger < 0f) impactStagger = 0f;
            if (cutInDuration < 0f) cutInDuration = 0f;
        }
#endif
    }
}
