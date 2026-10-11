using IdleKingdomFunction.Common.enums;
using IdleKingdomFunction.Common.enums.Scripts.Core;
using Newtonsoft.Json;

namespace IdleKingdomFunction.Common.Dto;

public abstract class RequestDTO
{
    public int ProtocolVersion { get; set; }
}

// 가입·로그인을 제외한 API의 요청. Redis의 현재 세션과 비교하여 해당 플레이어의 접속 상태를 확인한다.
public abstract class SessionRequestDTO : RequestDTO
{
    public string SessionID { get; set; } = string.Empty;
}

// 가입·로그인은 서버에서 세션을 발급받는 API라 SessionID가 없다. 발급한 세션은 응답으로 전달한다.
public sealed class InitUserRequestDTO : RequestDTO { }
public sealed class AuthRequestDTO : RequestDTO { }

public struct HuntResult
{
    public eMonsterType MonsterType { get; set; }
    public short Count { get; set; }
}
public sealed class OnRewardRequestDTO : SessionRequestDTO
{
    public long RewardSequence { get; set; }
    public eStage Stage { get; set; }
    public List<HuntResult> Loots { get; set; } = null!;
}
public sealed class OnStageClearRequestDTO : SessionRequestDTO
{
    public eStage ClearedStage { get; set; }
}
public sealed class OnGachaRequestDTO : SessionRequestDTO
{
    public int Count { get; set; }
}
public sealed class OnEnchantRequestDTO : SessionRequestDTO
{
    public int Count { get; set; }
}
public sealed class OnEnchantEquipmentRequestDTO : SessionRequestDTO
{
    // 대상 코드를 생략한 요청이 기본값 아이템을 선택하지 않도록 필수로 한다.
    [JsonProperty(Required = Required.Always)]
    public ItemCode ItemCode { get; set; }
}
public class OnEnchantSkillBaseRequest : SessionRequestDTO
{
    // 스킬 ID 0도 유효하므로 필드 누락과 명시적으로 보낸 코드 0을 구분한다.
    [JsonProperty(Required = Required.Always)]
    public SkillCode SkillCode { get; set; }
}
public sealed class OnEnchantSkillRequestDTO : OnEnchantSkillBaseRequest { }
public sealed class OnAwakeningSkillRequestDTO : OnEnchantSkillBaseRequest { }
public class OnJobRequestBaseDTO : SessionRequestDTO
{
    // 생략된 인덱스를 캐릭터 0으로 처리하지 않도록 대상을 명시하게 한다.
    [JsonProperty(Required = Required.Always)]
    public int Index { get; set; }
    public ulong JobCode { get; set; }
}
public sealed class OnGetJobRequestDTO : OnJobRequestBaseDTO { }
public sealed class OnChangeJobRequestDTO : OnJobRequestBaseDTO { }
public sealed class OnSetJobTreeRequestDTO : SessionRequestDTO { }
// 탈퇴는 가입·로그인에서 발급받은 SessionID를 전달한다. 그 외 입력은 없다.
public sealed class OnDeleteAccountRequestDTO : SessionRequestDTO { }
