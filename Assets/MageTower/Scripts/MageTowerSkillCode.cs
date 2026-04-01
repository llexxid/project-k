/// <summary>
/// 64비트 마탑 스킬 코드 인코더/디코더.
/// 서버 전송·DB 저장 시 스킬 한 건의 전체 상태를 단일 long 값으로 표현한다.
///
/// ── 64비트 레이아웃 ──────────────────────────────────────────────
///   [63-48]  스킬 코드    (16bit) → MageTowerSkillSO.id
///   [47-40]  각성 수치     (8bit)  → awakeningLevel  (0~255)
///   [39-28]  강화 수치    (12bit)  → enhanceLevel    (0~4095)
///   [27-12]  개수         (16bit)  → quantity / fragments (0~65535)
///   [11- 0]  여분         (12bit)  → reserved        (향후 확장용)
/// </summary>
public static class MageTowerSkillCode
{
    // ── 시프트 ────────────────────────────────────────────────────
    private const int SKILL_SHIFT     = 48;
    private const int AWAKENING_SHIFT = 40;
    private const int ENHANCE_SHIFT   = 28;
    private const int QUANTITY_SHIFT  = 12;

    // ── 마스크 ────────────────────────────────────────────────────
    private const long SKILL_MASK     = 0xFFFF;  // 16bit
    private const long AWAKENING_MASK = 0xFF;    //  8bit
    private const long ENHANCE_MASK   = 0xFFF;   // 12bit
    private const long QUANTITY_MASK  = 0xFFFF;  // 16bit
    private const long RESERVED_MASK  = 0xFFF;   // 12bit

    // ── 인코딩 ────────────────────────────────────────────────────

    /// <summary>
    /// 스킬 상태를 64비트 long으로 패킹한다.
    /// </summary>
    public static long Pack(int skillId, int awakeningLevel, int enhanceLevel,
                            int quantity, int reserved = 0)
    {
        return ((long)(skillId        & (int)SKILL_MASK)     << SKILL_SHIFT)
             | ((long)(awakeningLevel & (int)AWAKENING_MASK) << AWAKENING_SHIFT)
             | ((long)(enhanceLevel   & (int)ENHANCE_MASK)   << ENHANCE_SHIFT)
             | ((long)(quantity       & (int)QUANTITY_MASK)   << QUANTITY_SHIFT)
             |  (long)(reserved       & (int)RESERVED_MASK);
    }

    // ── 디코딩 ────────────────────────────────────────────────────

    public static int UnpackSkillId(long packed)
        => (int)((packed >> SKILL_SHIFT) & SKILL_MASK);

    public static int UnpackAwakeningLevel(long packed)
        => (int)((packed >> AWAKENING_SHIFT) & AWAKENING_MASK);

    public static int UnpackEnhanceLevel(long packed)
        => (int)((packed >> ENHANCE_SHIFT) & ENHANCE_MASK);

    public static int UnpackQuantity(long packed)
        => (int)((packed >> QUANTITY_SHIFT) & QUANTITY_MASK);

    public static int UnpackReserved(long packed)
        => (int)(packed & RESERVED_MASK);
}
