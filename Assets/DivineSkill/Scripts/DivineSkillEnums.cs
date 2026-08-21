namespace KingdomIdle.Divine
{
    /// <summary>
    /// 신 스킬 카드 등급. 등급별 배율 계수(영웅 ×1.0 / 전설 ×1.6 / 신화 ×2.5)는
    /// 카드 SO 의 skillMult 에 이미 반영되어 있다. 여기서는 표시/정렬 용도로만 쓴다.
    /// </summary>
    public enum eDivineGrade
    {
        Hero = 0,
        Legend = 1,
        Myth = 2
    }

    /// <summary>신 스킬의 발동 형태. 카드는 이 중 하나의 실행기를 사용한다.</summary>
    public enum eDivineEffectKind
    {
        /// <summary>전장 전체 즉발 데미지 (파티 ATK합 × 배율).</summary>
        AoeBurst = 0,
        /// <summary>단일 대상 즉발 데미지. 화면 내 최대 HP 대상(=보스) 우선 타게팅.</summary>
        SingleBurst = 1,
        /// <summary>일정 시간 동안 N 히트로 나눠 들어가는 광역 지속 데미지.</summary>
        Dot = 2,
        /// <summary>파티 전체 회복(대상 MAXHP 비율) + 받는 피해 감소.</summary>
        HealAndGuard = 3,
        /// <summary>파티 스킬 간격 단축 + 이동속도 증가 버프.</summary>
        PartyHaste = 4
    }

    /// <summary>공격형 신 스킬에 부가되는 군중 제어. 속박/기절은 Stun 으로 통합한다.</summary>
    public enum eDivineCrowdControl
    {
        None = 0,
        /// <summary>행동 정지 (기절 · 속박).</summary>
        Stun = 1,
        /// <summary>이동속도 감소.</summary>
        Slow = 2
    }

    /// <summary>
    /// 카드의 시각 컨셉 — 궁극기 버튼 링 프레임/시전 VFX 색을 결정한다.
    /// 카드:컨셉은 N:1 (루멘·아스트라가 신성을 공유). 링 아트는 컨셉 단위로 생성·재사용.
    /// </summary>
    public enum eDivineConcept
    {
        /// <summary>자연 — 잎·덩굴 (가이엔).</summary>
        Nature = 0,
        /// <summary>신성 — 깔끔한 금빛 금속 (루멘 · 아스트라).</summary>
        Holy = 1,
        /// <summary>화염 — 용암·붉은 균열 암석 (이그니스).</summary>
        Flame = 2,
        /// <summary>바람 — 소용돌이 은빛 기류 (실피르).</summary>
        Wind = 3,
        /// <summary>강철 — 리벳 박힌 중갑 강철 (페룸).</summary>
        Steel = 4,
        /// <summary>시계 — 황혼빛 톱니·문자반 (호라).</summary>
        Chrono = 5,
        /// <summary>심연 — 먹빛 흑요석·보랏빛 심연 (녹스).</summary>
        Abyss = 6
    }
}
