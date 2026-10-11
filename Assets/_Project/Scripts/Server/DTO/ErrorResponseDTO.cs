using IdleKingdomFunction.Common.enums;
using Newtonsoft.Json;

namespace IdleKingdomFunction.Common.Dto
{

// Controller에서 HTTP 400을 반환할 때 사용하는 응답.
public sealed class ErrorResponseDTO : ApiResponseDTO
{
    // 클라이언트가 오류 원인을 구분하는 고정 코드. JSON에는 enum의 숫자값을 전달한다.
    [JsonProperty(Required = Required.Always)]
    public eErrorCode ErrorCode { get; set; }

    // 로그나 디버깅에서 오류 원인을 읽기 위한 설명.
    [JsonProperty(Required = Required.Always)]
    public string Message { get; set; }
}
}
