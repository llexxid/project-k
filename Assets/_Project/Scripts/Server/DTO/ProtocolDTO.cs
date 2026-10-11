namespace IdleKingdomFunction.Common.Dto;

// 각 API 응답이 상속하는 공통 헤더. 게임 결과는 개별 응답 DTO의 필드로 함께 반환한다.
public class ApiResponseDTO
{
    // 클라이언트가 응답 형식을 해석할 때 사용하는 프로토콜 버전.
    public int ProtocolVersion { get; set; } = ApiVersions.ProtocolVersion;
    // 서버 릴리스 버전. 플레이어 저장이나 API 호출에 따라 증가하지 않는다.
    public long ServerVersion { get; set; } = ApiVersions.ServerVersion;
}
