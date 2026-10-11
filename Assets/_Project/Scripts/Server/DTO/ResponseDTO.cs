using IdleKingdomFunction.Common.enums;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IdleKingdomFunction.Common.Dto
{

// 정상 가입·로그인 완료 시 게임 진입에 필요한 데이터. 별도의 복구 상태나 처리 기록은 포함하지 않는다.
public sealed class UserInitResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public string SessionID { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public int Level { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long Exp { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long KillScore { get; set; }
    [JsonProperty(Required = Required.Always)]
    public eStage CurrentStage { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long EnchantHPCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long EnchantATKCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<CharacterDataQuery> CharacterDatas { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<CharacterEquipmentQuery> CharacterEquipmentDatas { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<SkillCode> SkillTreeDatas { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<ItemCode> Inventory { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<JobTreeQuery> JobTrees { get; set; } = null!;
}
public sealed class OnHuntResponseDTO : ApiResponseDTO
{
    // 보상 적용 후의 절대값을 전달한다.
    [JsonProperty(Required = Required.Always)]
    public int Level { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long Exp { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long KillScore { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long Gold { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long AncientCoin { get; set; }
    // 다음 사냥 배치의 순번. 지급된 배치는 같은 순번으로 다시 지급하지 않는다.
    [JsonProperty(Required = Required.Always)]
    public long NextRewardSequence { get; set; }
}
public sealed class OnStageClearResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public eStage Stage { get; set; }
}
public sealed class OnGachaEquipmentClassFragmentResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public List<ItemCode> GachaList { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public int ClassFragmentCnt { get; set; }
}
public sealed class OnGachaSkillArcaneKnowledgeResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public List<SkillCode> GachaList { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public int ArcaneKnowledgeCnt { get; set; }
}
public sealed class OnEnchantResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public long CurrentLevel { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long CurrentGold { get; set; }
}
public sealed class OnEnchantEquipmentResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public ItemCode ItemCode { get; set; }
}
public sealed class OnEnchantSkillResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public SkillCode SkillCode { get; set; }
}
public sealed class OnAwakeningSkillResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public SkillCode SkillCode { get; set; }
}
public sealed class OnGetJobResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public int Index { get; set; }
    [JsonProperty(Required = Required.Always)]
    public ulong JobCode { get; set; }
}
public sealed class OnChangeJobResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public int Index { get; set; }
    [JsonProperty(Required = Required.Always)]
    public ulong JobCode { get; set; }
}
public sealed class OnSetJobTreeResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public List<JobTreeQuery> JobTrees { get; set; } = null!;
}
public sealed class OnDeleteAccountResponseDTO : ApiResponseDTO
{
    // PlayFab이 계정 삭제를 접수하면 true. 실패는 ErrorResponseDTO로 전달한다.
    [JsonProperty(Required = Required.Always)]
    public bool IsSuccess { get; set; }
}

}
