/// <summary>
/// 64비트 마탑 스킬 코드 인코더/디코더.
/// 기존 서버 코드 호환용. 개화와 65535개를 넘는 파편은 MageSkillStateSnapshot을 사용한다.
///
/// ── 64비트 레이아웃 ──────────────────────────────────────────────
///   [63-52]  예약 공간     (12bit) → reserved        (향후 확장용)
///   [51-36]  스킬 ID       (16bit) → MageTowerSkillSO.id
///   [35-28]  강화 수치      (8bit) → enhanceLevel    (0~255)
///   [27-16]  각성 수치     (12bit) → awakeningLevel  (0~4095)
///   [15- 0]  개수          (16bit) → quantity / fragments (0~65535)
/// </summary>
public static class MageTowerSkillCode
{
    // ── 시프트 ────────────────────────────────────────────────────
    private const int RESERVED_SHIFT  = 52;
    private const int SKILL_SHIFT     = 36;
    private const int ENHANCE_SHIFT   = 28;
    private const int AWAKENING_SHIFT = 16;
    private const int QUANTITY_SHIFT  = 0;

    // ── 마스크 ────────────────────────────────────────────────────
    private const long RESERVED_MASK  = 0xFFF;   // 12bit
    private const long SKILL_MASK     = 0xFFFF;  // 16bit
    private const long ENHANCE_MASK   = 0xFF;    //  8bit
    private const long AWAKENING_MASK = 0xFFF;   // 12bit
    private const long QUANTITY_MASK  = 0xFFFF;  // 16bit

    // ── 인코딩 ────────────────────────────────────────────────────

    /// <summary>
    /// 스킬 상태를 64비트 long으로 패킹한다.
    /// </summary>
    public static long Pack(int skillId, int awakeningLevel, int enhanceLevel,
                            int quantity, int reserved = 0)
    {
        if (skillId < 0 || skillId > SKILL_MASK || awakeningLevel < 0 || awakeningLevel > AWAKENING_MASK ||
            enhanceLevel < 0 || enhanceLevel > ENHANCE_MASK || quantity < 0 || quantity > QUANTITY_MASK || reserved < 0 || reserved > RESERVED_MASK)
            throw new System.ArgumentOutOfRangeException("Legacy mage code range exceeded; use the versioned snapshot.");
        return ((long)(reserved       & (int)RESERVED_MASK)  << RESERVED_SHIFT)
             | ((long)(skillId        & (int)SKILL_MASK)     << SKILL_SHIFT)
             | ((long)(enhanceLevel   & (int)ENHANCE_MASK)   << ENHANCE_SHIFT)
             | ((long)(awakeningLevel & (int)AWAKENING_MASK) << AWAKENING_SHIFT)
             |  (long)(quantity       & (int)QUANTITY_MASK);
    }

    // ── 디코딩 ────────────────────────────────────────────────────

    public static int UnpackReserved(long packed)
        => (int)((packed >> RESERVED_SHIFT) & RESERVED_MASK);

    public static int UnpackSkillId(long packed)
        => (int)((packed >> SKILL_SHIFT) & SKILL_MASK);

    public static int UnpackEnhanceLevel(long packed)
        => (int)((packed >> ENHANCE_SHIFT) & ENHANCE_MASK);

    public static int UnpackAwakeningLevel(long packed)
        => (int)((packed >> AWAKENING_SHIFT) & AWAKENING_MASK);

    public static int UnpackQuantity(long packed)
        => (int)(packed & QUANTITY_MASK);
}
