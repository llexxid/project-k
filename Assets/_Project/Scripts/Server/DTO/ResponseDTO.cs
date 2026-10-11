using IdleKingdomFunction.Common.enums;

namespace IdleKingdomFunction.Common.Dto;

// 정상 가입·로그인 완료 시 게임 진입에 필요한 데이터. 별도의 복구 상태나 처리 기록은 포함하지 않는다.
public sealed class UserInitResponseDTO : ApiResponseDTO
{
    public string SessionID { get; set; } = null!;
    public int Level { get; set; }
    public long Exp { get; set; }
    public long KillScore { get; set; }
    public eStage CurrentStage { get; set; }
    public long EnchantHPCount { get; set; }
    public long EnchantATKCount { get; set; }
    public List<CharacterDataQuery> CharacterDatas { get; set; } = null!;
    public List<CharacterEquipmentQuery> CharacterEquipmentDatas { get; set; } = null!;
    public CurrencyQueryDTO Currency { get; set; } = null!;
    public List<SkillCode> SkillTreeDatas { get; set; } = null!;
    public List<ItemCode> Inventory { get; set; } = null!;
    public List<JobTreeQuery> JobTrees { get; set; } = null!;
}
public sealed class OnHuntResponseDTO : ApiResponseDTO
{
    // 보상 적용 후의 절대값을 전달한다.
    public int Level { get; set; }
    public long Exp { get; set; }
    public long KillScore { get; set; }
    public long Gold { get; set; }
    public long AncientCoin { get; set; }
    // 다음 사냥 배치의 순번. 지급된 배치는 같은 순번으로 다시 지급하지 않는다.
    public long NextRewardSequence { get; set; }
}
public sealed class OnStageClearResponseDTO : ApiResponseDTO
{
    public eStage Stage { get; set; }
}
public sealed class OnGachaEquipmentClassFragmentResponseDTO : ApiResponseDTO
{
    public List<ItemCode> GachaList { get; set; } = null!;
    public int ClassFragmentCnt { get; set; }
}
public sealed class OnGachaSkillArcaneKnowledgeResponseDTO : ApiResponseDTO
{
    public List<SkillCode> GachaList { get; set; } = null!;
    public int ArcaneKnowledgeCnt { get; set; }
}
public sealed class OnEnchantResponseDTO : ApiResponseDTO
{
    public long CurrentLevel { get; set; }
    public long CurrentGold { get; set; }
}
public sealed class OnEnchantEquipmentResponseDTO : ApiResponseDTO
{
    public ItemCode ItemCode { get; set; }
}
public sealed class OnEnchantSkillResponseDTO : ApiResponseDTO
{
    public SkillCode SkillCode { get; set; }
}
public sealed class OnAwakeningSkillResponseDTO : ApiResponseDTO
{
    public SkillCode SkillCode { get; set; }
}
public sealed class OnGetJobResponseDTO : ApiResponseDTO
{
    public int Index { get; set; }
    public ulong JobCode { get; set; }
}
public sealed class OnChangeJobResponseDTO : ApiResponseDTO
{
    public int Index { get; set; }
    public ulong JobCode { get; set; }
}
public sealed class OnSetJobTreeResponseDTO : ApiResponseDTO
{
    public List<JobTreeQuery> JobTrees { get; set; } = null!;
}
public sealed class OnDeleteAccountResponseDTO : ApiResponseDTO
{
    // PlayFab이 계정 삭제를 접수하면 true. 실패는 ErrorResponseDTO로 전달한다.
    public bool IsSuccess { get; set; }
}
