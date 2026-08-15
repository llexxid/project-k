/// <summary>
/// 64비트 신 스킬 카드 코드 인코더/디코더. (MageTowerSkillCode 와 동일한 규약)
/// 서버 전송·DB 저장 시 카드 한 장의 전체 상태를 단일 long 값으로 표현한다.
///
/// ── 64비트 레이아웃 ──────────────────────────────────────────────
///   [63-52]  예약 공간     (12bit) → reserved   (향후 확장용)
///   [51-36]  카드 ID       (16bit) → DivineSkillSO.id
///   [35-16]  레벨          (20bit) → level      (0 = 미보유, 1부터 보유 · 상한 없음)
///   [15- 0]  중복 보유량    (16bit) → duplicates (레벨업 재료)
/// </summary>
public static class DivineSkillCode
{
    // ── 시프트 ────────────────────────────────────────────────────
    private const int RESERVED_SHIFT = 52;
    private const int CARD_SHIFT     = 36;
    private const int LEVEL_SHIFT    = 16;
    private const int DUP_SHIFT      = 0;

    // ── 마스크 ────────────────────────────────────────────────────
    private const long RESERVED_MASK = 0xFFF;    // 12bit
    private const long CARD_MASK     = 0xFFFF;   // 16bit
    private const long LEVEL_MASK    = 0xFFFFF;  // 20bit
    private const long DUP_MASK      = 0xFFFF;   // 16bit

    // ── 인코딩 ────────────────────────────────────────────────────
    public static long Pack(int cardId, int level, int duplicates, int reserved = 0)
    {
        return ((long)(reserved   & (int)RESERVED_MASK) << RESERVED_SHIFT)
             | ((long)(cardId     & (int)CARD_MASK)     << CARD_SHIFT)
             | ((long)(level      & (int)LEVEL_MASK)    << LEVEL_SHIFT)
             |  (long)(duplicates & (int)DUP_MASK);
    }

    // ── 디코딩 ────────────────────────────────────────────────────
    public static int UnpackReserved(long packed)
        => (int)((packed >> RESERVED_SHIFT) & RESERVED_MASK);

    public static int UnpackCardId(long packed)
        => (int)((packed >> CARD_SHIFT) & CARD_MASK);

    public static int UnpackLevel(long packed)
        => (int)((packed >> LEVEL_SHIFT) & LEVEL_MASK);

    public static int UnpackDuplicates(long packed)
        => (int)(packed & DUP_MASK);
}
