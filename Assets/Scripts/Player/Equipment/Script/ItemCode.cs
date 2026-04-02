/// <summary>
/// 32비트 아이템 코드 인코더/디코더 및 64비트 인스턴스 패킹 유틸리티.
///
/// ── 32비트 아이템 코드 레이아웃 ──────────────────────────────────
///   [31-16]  아이템 ID  (16bit) → EquipmentData._itemId
///   [15-12]  레어도     (4bit)  → eEquipmentRarity
///   [11- 8]  슬롯       (4bit)  → eEquipmentSlot
///   [ 7- 0]  직업 마스크 (8bit)  → eJobFlag  (0 = 모든 직업 공용)
///
/// ── 64비트 인스턴스 패킹 레이아웃 ────────────────────────────────
///   [63-56]  예약 공간    (8bit)
///   [55-40]  아이템 ID   (16bit) ┐
///   [39-36]  레어도      (4bit)  │ ← 32비트 아이템 코드 (itemCode << 24)
///   [35-32]  슬롯        (4bit)  │
///   [31-24]  직업 마스크  (8bit)  ┘
///   [23-16]  강화 수치    (8bit)
///   [15- 0]  개수        (16bit)
/// </summary>
public static class ItemCode
{
    // ── 32비트 시프트 / 마스크 ─────────────────────────────────────
    private const int ID_SHIFT     = 16;
    private const int RARITY_SHIFT = 12;
    private const int SLOT_SHIFT   =  8;

    private const int ID_MASK     = 0xFFFF; // 16bit
    private const int RARITY_MASK = 0x0F;   // 4bit
    private const int SLOT_MASK   = 0x0F;   // 4bit
    private const int JOB_MASK    = 0xFF;   // 8bit

    // ── 64비트 시프트 ──────────────────────────────────────────────
    private const int PACKED_CODE_SHIFT    = 24;
    private const int PACKED_ENHANCE_SHIFT = 16;

    // ── 인코딩 ────────────────────────────────────────────────────

    /// <summary>
    /// itemId, rarity, slot, jobMask를 합쳐 32비트 아이템 코드를 반환한다.
    /// </summary>
    public static int Encode(eEquipmentRarity rarity, eEquipmentSlot slot, eJobFlag jobMask, int itemId)
    {
        return ((itemId       & ID_MASK)    << ID_SHIFT)
             | (((int)rarity  & RARITY_MASK) << RARITY_SHIFT)
             | (((int)slot    & SLOT_MASK)   << SLOT_SHIFT)
             |  ((int)jobMask & JOB_MASK);
    }

    // ── 디코딩 ────────────────────────────────────────────────────

    public static int            DecodeItemId  (int code) => (code >> ID_SHIFT)     & ID_MASK;
    public static eEquipmentRarity DecodeRarity(int code) => (eEquipmentRarity)((code >> RARITY_SHIFT) & RARITY_MASK);
    public static eEquipmentSlot   DecodeSlot  (int code) => (eEquipmentSlot)  ((code >> SLOT_SHIFT)   & SLOT_MASK);
    public static eJobFlag      DecodeJobMask  (int code) => (eJobFlag)         (code                  & JOB_MASK);

    // ── 직업 허용 검사 ────────────────────────────────────────────

    /// <summary>
    /// 아이템 코드가 해당 직업명을 허용하는지 비트 연산으로 검사한다.
    /// jobMask == None(0)이면 모든 직업 허용.
    /// jobName이 eJobFlag 멤버 이름과 다르면 false 반환.
    /// </summary>
    public static bool IsAllowedForJob(int code, string jobName)
    {
        eJobFlag mask = DecodeJobMask(code);
        if (mask == eJobFlag.None) return true;

        if (!System.Enum.TryParse(jobName, out eJobFlag flag)) return false;
        return (mask & flag) != 0;
    }

    // ── 64비트 인스턴스 패킹 ──────────────────────────────────────

    /// <summary>
    /// itemCode + enhancementLevel + quantity를 64비트 long으로 패킹한다.
    /// 네트워크 전송, DB 저장, 디버그 로그에 활용한다.
    /// </summary>
    public static long PackInstance(int itemCode, int enhancementLevel, int quantity = 1)
    {
        return ((long)itemCode                     << PACKED_CODE_SHIFT)
             | ((long)(enhancementLevel & 0xFF)    << PACKED_ENHANCE_SHIFT)
             |  (long)(quantity         & 0xFFFF);
    }

    public static int UnpackItemCode(long packed)
        => (int)((packed >> PACKED_CODE_SHIFT) & 0xFFFFFFFFL);

    public static int UnpackEnhancementLevel(long packed)
        => (int)((packed >> PACKED_ENHANCE_SHIFT) & 0xFF);

    public static int UnpackQuantity(long packed)
        => (int)(packed & 0xFFFF);
}
