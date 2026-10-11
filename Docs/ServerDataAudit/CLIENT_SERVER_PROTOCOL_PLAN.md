# 클라이언트 기능 보존을 위한 서버 프로토콜 설계와 실행계획

- 작성일: 2026-10-11 (KST)
- 상태: 사용자 검토용 설계안. 게임 코드와 DTO 소스는 변경하지 않았다.
- 기준: 현재 작업 트리의 새 Server/DTO, 실제 콘텐츠 구현, 사용자의 2026-10-11 추가 지시.
- 서버 구현 소스와 PlayFab 등록 목록은 이 저장소에서 확인하지 못했다. 아래의 ‘기존’은 DTO/클라이언트 호출이 존재한다는 뜻이며 배포 확인을 뜻하지 않는다.
- 목적: 장비·마탑 스킬·직업·뽑기·퀘스트/업적의 현행 기능과 데이터를 보존하는 요청/응답 계약 및 서버 구현 책임을 정한다. 나머지 콘텐츠는 의존성 조사와 후속 계약 제안만 한다.

## 1. 이번 수정 계획에 적용하는 기준

1. ProtocolVersion은 서버와 클라이언트가 공유하는 상수이며 정확히 일치해야 한다. 범위 협상·자동 낮춤·응답 값으로 클라이언트 상수 덮어쓰기를 하지 않는다.
2. RPC 실패의 공개 분기는 PlayFab 호출 실패와 PlayFab 성공 콜백 안의 응답 실패 두 가지다.
3. 진입 단계는 인증 → 가입 또는 로그인 → 서버 데이터 로딩/적용 → 메인 진입이다.
4. 이번 단계에서는 Markdown 문서만 작성한다. RequestDTO/ResponseDTO를 포함한 C#·프리팹·설정·저장 데이터는 변경하지 않는다.
5. ‘클라이언트 변경 없이 설계’는 현재 모델·규칙·UI가 요구하는 정보를 서버 계약에 담는다는 뜻이다. 현 소스를 전혀 수정하지 않고 서버 권위로 동작시키는 것은 불가능하다. 실제 구현 승인 후에는 로컬 변경 호출의 RPC 연결, 응답 적용, 비동기 완료 연결이 필요하다.
6. 콘텐츠 기능·확률·비용·수령 정책을 서버의 현재 부족한 DTO에 맞춰 축소하지 않는다. 서버가 현행 기능을 구현할 수 있도록 계약을 보완한다.

## 2. 확인된 현재 구조와 정정

| 근거 | 확인 내용 | 설계 영향 |
|---|---|---|
| ResponseDTO.cs의 UserInitResponseDTO | Inventory: List<ItemCode>, CharacterDatas, CharacterEquipmentDatas, Currency, SkillTreeDatas, JobTrees가 이미 있다. | 인벤토리가 아예 없는 것이 아니다. 손실 없는 상세 상태와 독립 재조회 계약이 부족하다. |
| QueryDTO.cs의 InventoryQueryDTO | Items: List<ItemCode>가 존재한다. | 코드/수량 요약으로 개별 잠금·장착·투자 이력까지 표현할 수 없다. |
| LocalProgression.cs | IsLocalAuthority는 항상 true. 계정별 JSON을 열고 Execute가 상태 변경과 저장을 확정한다. | DTO 추가만으로 데이터 권한은 바뀌지 않는다. |
| UserManager.cs / User.cs | 서버 데이터를 최초 한 번만 로컬 상태에 이관한다. | 매 로그인마다 서버 스냅샷을 검증·적용하는 별도 경로가 필요하다. |
| GachaManager.cs | 클라이언트가 난수, 비용, 지급, 천장, 퀘스트 집계를 한 번에 처리한다. | 서버에도 같은 원자적 처리와 재시도 중복 방지가 필요하다. |
| QuestManager / QuestEconomy / QuestCatalog | 가이드·일일·주간·업적이 공통 모델을 쓰며 로컬 정의와 상태를 읽는다. | 업적 전용 중복 시스템을 만들지 않고 공통 프로토콜을 사용한다. |
| EquipmentManager.TrySynthesize | null을 반환하는 비활성 메서드다. | 구현된 합성 콘텐츠나 필수 서버 API로 분류하지 않는다. |

현재 DTO에는 서버용 namespace, Unity C# 9 설정과 맞지 않는 파일 범위 namespace, 일부 List using 누락, 정의가 없는 ApiVersions 참조가 있다. 기존 Authentication은 없어진 UserOnSignUpInitResponseDTO/OnAuthInitResponseDTO를 사용한다. 이들은 추후 프로토콜 호환 작업의 대상이며, 현재 Unity 컴파일을 실행해 검증한 것은 아니다.

## 3. 공통 프로토콜

### 3.1 버전과 식별자

| 필드 | 타입 | 의미와 규칙 |
|---|---|---|
| ProtocolVersion | int | 공유 상수 K. 실제 값은 서버/클라이언트 공통 소스에서 확정한다. 이번 문서에서 숫자를 임의 지정하지 않는다. |
| ServerVersion | long | 서버 릴리스 버전. 계정 상태 변경 순번이나 충돌 검출에 사용하지 않는다. |
| SessionID | string | 가입/로그인 서버가 발급한 세션. PlayFabId, PlayFab SessionTicket과 구분한다. |
| RequestId | string | 상태 변경 요청마다 생성한 불투명 ID. 동일 요청 재시도 시 유지한다. 조회에는 필수가 아니다. |
| StateRevision | long | 서버 계정 상태의 커밋 순번. ProtocolVersion/ServerVersion과 별개인 추가 제안이다. |
| ServerTimeUtc | long | 서버 UTC Unix 초. 퀘스트 기간·만료 판단의 기준이다. |
| DefinitionVersion | string | 운영 정의의 버전/해시. 프로토콜 버전과 구분한다. |

- 서버는 모든 요청에서 K를 먼저 검증하고 불일치 시 저장/재화 처리를 시작하지 않는다. 기존 UnsupportedProtocolVersion 오류를 사용한다.
- 클라이언트도 모든 성공/업무 오류 응답의 ProtocolVersion을 검사한다. 불일치 시 데이터 적용과 메인 진입을 중단하고 업데이트/지원하지 않는 클라이언트 안내로 연결한다.
- 버전 불일치는 업데이트 불일치 또는 변조 의심 분류의 근거로 사용할 수 있다. 이 상수 하나만으로 변조 사실이 입증되는 것은 아니며 자동 제재를 이 계약에 추가하지 않는다.
- 서버는 인증된 PlayFab 호출 컨텍스트에서 계정을 얻는다. 클라이언트가 임의의 AccountId를 보내 다른 계정의 장비를 조작할 수 없게 한다.
- long/ulong 코드는 기존 숫자 표현을 보존한다. JSON 처리 중 double/float로 변환하지 않는다. 실제 SDK serializer의 64비트 왕복을 테스트하며, 문자열로 바꾸려면 공유 계약을 함께 개정한다.
- 없는 필드와 명시적 0/null/빈 배열을 구별한다. 장착 캐릭터 0, 스킬 ID 0, 미소유 빈 목록은 유효한 값이다.

### 3.2 RPC 성공과 실패의 두 갈래

공통 호출의 형태는 Call<TRequest,TResponse>(functionName, request, onSuccess, onFailure)로 제안한다. 콘텐츠에서 JSON 해석을 반복하지 않는다.

| 분기 | 판별 지점 | 처리 |
|---|---|---|
| PlayFabFailure | PlayFab SDK error callback의 PlayFabError | 통신/인증/호출 실패 정보를 보존하고 안내한다. 응답 유실 시 서버 처리 여부는 미확정이다. |
| ResponseFailure | SDK success callback | 먼저 ExecuteFunctionResult.Error를 검사한다. 이어 FunctionResult의 ErrorResponseDTO, 버전, 필수 필드와 형식을 검사한다. |
| Success | SDK success callback의 모든 검사 통과 | 해당 성공 DTO로 역직렬화하고 상태 적용 및 콘텐츠 성공 콜백을 실행한다. |

ResponseFailure 안에서는 FunctionExecution, Business, UnsupportedProtocol, InvalidPayload를 내부 원인으로 구별한다. 사용자에게 새로운 네트워크 분기 네 가지를 노출하는 구조는 아니다. 클라이언트의 잘못된 입력 등 전송 전 검증 실패는 RPC 응답으로 위장하지 않는다.

실패 분류와 서버 처리 여부는 별개다. 타임아웃뿐 아니라 FunctionExecution/InvalidPayload도 서버 커밋 이후 응답 작성·전달 중 발생할 수 있다. 변경 요청의 ErrorResponseDTO에는 RequestId와 Outcome(Rejected/Unknown)을 추가하는 안을 사용한다. 서버가 미실행 또는 완전 롤백을 보장할 때만 Rejected를 반환하고, 결과를 확정하지 못하면 Unknown이다. 이 정보가 없거나 응답 자체가 유효하지 않으면 Unknown으로 취급한다. Unknown은 기존 RequestId 결과 조회/안전한 동일 ID 재시도로 복구하며 새 RequestId로 같은 작업을 다시 실행하지 않는다. 두 공개 실패 분기는 그대로 유지한다.

**서버가 보장할 업무 오류 전달 계약:** 정상적으로 처리한 업무 거절은 Azure Function의 HTTP 200 본문에 기존 ErrorResponseDTO(ProtocolVersion, ServerVersion, ErrorCode, Message)를 반환하여 FunctionResult에 담기게 하는 방식을 권장한다. ErrorCode 필드가 존재하면 성공 DTO로 해석하지 않는다. 성공 DTO에는 ErrorCode가 없다. ErrorCode는 숫자이며 기존 값은 재사용/재번호하지 않는다.

현재 ErrorResponseDTO 주석은 HTTP 400이다. HTTP 400이 언제나 FunctionResult에 원문으로 보존된다고 가정하면 안 된다. 서버는 위 전달 방식으로 맞추거나, 실제 ExecuteFunctionResult.Error/PlayFabError에 도달하는 예시를 제공해 매핑을 확정해야 한다. SDK result.Error와 업무 오류는 모두 두 번째 공개 분기의 하위 원인이다. 제어되지 않은 예외는 업무 성공으로 변환하지 않는다.

기본 성공/오류 DTO의 평평한 JSON 구조는 유지한다. 불필요하게 모든 API를 { Success, Data, Error } 형태로 한 번에 감싸지 않는다. 오류 메시지는 로그 보조 정보이며, 사용자 안내와 분기는 ErrorCode로 결정한다. 비밀 키·세션·서버 스택은 UI에 노출하지 않는다.

### 3.3 상태 변경 응답과 원자성

신규 변경 요청은 SessionRequestDTO에 RequestId를 추가한 MutationRequestDTO를 공통으로 사용한다. 기존 변경 요청도 합의한 프로토콜 버전에서 같은 정보를 받도록 확장한다.

~~~text
MutationReceiptDTO
  RequestId: string
  BaseStateRevision: long
  StateRevision: long
  ServerTimeUtc: long
  Changes: StateChangesDTO

StateChangesDTO
  User: UserProgressDTO?              // 변경된 경우 전체 해당 블록
  Currency: CurrencyStateDTO?         // 변경된 경우 전체 잔액 블록
  Equipment: EquipmentInventoryDTO?   // 변경된 경우 전체 장비 블록
  MageTower: MageTowerStateDTO?
  Jobs: CharacterJobStateDTO[]?
  Quests: QuestStateDTO?
  Gacha: GachaStateDTO?
  GuidePractice: GuidePracticeStateDTO? // 실습 지급/성공 연결이 별도 승인된 경우
  // 추후 승인된 콘텐츠 블록을 명시적으로 추가한다. 임의 Modules 전체 쓰기는 제공하지 않는다.
~~~

- 서버는 검증, 비용 차감, 보상 지급, 장착 변경, 퀘스트 집계, RequestId 처리 기록, revision 증가를 하나의 논리적 커밋으로 처리한다.
- 저장소가 여러 개면 단순한 순차 저장을 원자적이라고 부르지 않는다. 확정 원장과 복구 가능한 작업 기록 등으로 중간 실패 후에도 지급과 차감이 일치하도록 구현한다.
- 멱등 키는 계정+RequestId이고 함수 이름과 정규화된 업무 인자의 해시를 묶는다. 같은 키/같은 인자에는 원래 확정 결과, 같은 키/다른 인자에는 충돌 오류를 반환한다. 세션 갱신은 같은 업무 요청의 인자를 바꾼 것으로 간주하지 않는다.
- 타임아웃 후 새 RequestId로 같은 구매를 재전송하지 않는다. 재로그인 후에도 유지되는 요청 기록과 서버 조회로 완료 여부를 확인한다. 멱등 기록 보존 기간과 클라이언트 재시도 유효 기간을 함께 정하고, 만료된 ID를 신규 요청으로 조용히 실행하지 않는다.
- 복구 조회는 OnGetMutationStatusRequestDTO(SessionID, RequestId) → OnGetMutationStatusResponseDTO(Status: Processing/Committed/Rejected/Unknown/Expired, OriginalFunction, 확정 시 OriginalResult)로 제안한다. 계정 소유 요청만 조회할 수 있고 RequestId로 새 작업을 시작하지 않는다. Unknown/Expired는 미실행의 증거가 아니므로 동일 구매를 새 ID로 자동 재실행하지 않는다.
- 응답에는 실제 소비/지급 내역과 최종 잔액을 구분한다. 클라이언트가 보상 숫자를 잔액에 다시 더하지 않는다.
- 초기 구현은 변경된 콘텐츠 블록 전체를 반환한다. 신규 블록 추가가 기존 블록 삭제를 의미하지 않으며, 생략된 블록은 ‘변경 없음’이다.
- BaseStateRevision은 요청의 포괄적인 실행 조건이 아니다. 특히 사냥 수입 때문에 분해 요청을 계속 거절하지 않는다. 서버가 이번 커밋 직전에 본 revision을 응답에 넣는다.
- 클라이언트의 확정 revision이 BaseStateRevision과 일치할 때만 Changes 전체를 원자 적용하고 StateRevision으로 전진한다. 불일치하면 일부만 덮지 않고 OnGetUserState로 전체 서버 스냅샷을 재조회한다. 서버에서 이미 성공한 작업을 다시 구매하지 않는다.
- 독립 인벤토리/퀘스트 조회 응답은 계정·세션 세대와 StateRevision을 먼저 검증한다. 현재 확정 revision보다 오래되면 폐기하고, 같을 때만 동일 버전의 해당 화면 자료로 사용한다. 더 최신이면 부분 적용하지 않고 OnGetUserState를 조회한다. 인벤토리만 받고 지갑은 옛 상태인 조합을 공개하거나 계정 revision을 올리지 않는다.
- OnGetUserState 응답도 같은 계정·세션 세대인지 확인하고 이미 적용한 revision보다 오래되면 폐기한다. 요청 당시보다 새 mutation을 먼저 적용했다면 뒤늦게 온 전체 스냅샷이 그 결과를 되돌릴 수 없다. 전체 스냅샷을 검증 완료한 뒤 한 번에 적용한다. 이미 처리한 RequestId의 영수증으로 상태/성공 연출을 중복 적용하지 않는다.
- 첫 배포 전에 장비 400개·예비 스택·퀘스트 최대 상태의 직렬화 크기와 지연을 측정한다. 실제 제한을 넘으면 동일 snapshot revision에 고정된 페이지 조회로 바꾸고, 페이지 수집 완료 전에 부분 상태를 공개하지 않는다.

## 4. 인증 → 가입/로그인 → 데이터 로딩 → 메인

권장 기본 흐름은 기존 UserInitResponseDTO를 가입과 로그인에서 공통으로 받는 것이다. ‘데이터 로딩’을 별도의 화면 단계와 초기화 단계로 명확히 두되, 이미 받은 전체 데이터를 다시 요청하는 RPC를 의무적으로 추가하지 않는다.

1. PlayFab 인증: PlayFabId·SDK 인증 컨텍스트 확보. 아직 메인 진입 불가.
2. 가입 또는 로그인 RPC: InitUserRequestDTO 또는 AuthRequestDTO에 K를 전달. 세션을 아직 발급받기 전이므로 SessionID는 보내지 않는다.
3. 서버: 신규 계정은 한 번만 초기화하고, 기존 계정은 조회한다. 세션을 발급하고 일관된 UserInitResponseDTO를 반환한다.
4. 데이터 로딩: 응답 검증 → 계정/세션 연결 → 캐릭터 3명 인덱스 정렬 → 재화·인벤토리·스킬·직업·퀘스트 준비 → 기존 에셋 ID 연결 → 한 번에 공개.
5. 모든 필수 데이터가 준비된 후에만 메인 진입. 초기화 실패 시 데이터 로딩 상태에서 오류/재시도를 제공한다. 실제 빈 인벤토리와 필수 필드 누락을 구별한다.

캐릭터는 정확히 3명이며 Index가 각각 0, 1, 2여야 한다. 배열 순서를 캐릭터 번호로 가정하지 않는다. Index 중복/누락, 알 수 없는 아이템 정의, 중복 장비 ID, 유효하지 않은 장착 조합은 빈 계정으로 덮어쓰지 않는다. 준비 중 다른 계정으로 바뀌면 이전 응답을 폐기한다.

현재 클라이언트의 등록 함수 문자열은 OnSignUpInitUser와 OnExistUserInit이다. OnLogin은 사용자가 설명한 로그인 단계 이름으로 사용하며, 실제 서버 등록명이 확인되기 전 FunctionName을 임의 변경하지 않는다.

### 4.1 초기화 DTO의 확장

기존 UserInitResponseDTO의 계정/캐릭터/재화 필드는 유지하면서 다음 블록을 추가하는 안이다. OnGetUserStateRequestDTO(SessionID, ProtocolVersion) / OnGetUserStateResponseDTO는 같은 사용자 상태를 재조회하되 새 세션을 발급하지 않는다.

| 필드/블록 | 클라이언트가 받을 정보 |
|---|---|
| SessionID | 가입/로그인 응답에만 필요한 서버 세션 |
| StateRevision, ServerTimeUtc | 일관된 계정 스냅샷의 순번과 서버 시간 |
| UserProgress | Level, Exp, KillScore, CurrentStage, EnchantHPCount, EnchantATKCount 및 퀘스트에 필요한 승인된 진행 상태 |
| CharacterDatas | Index, NickName, JobCode, Hp, Atk를 가진 캐릭터 3명. Hp/Atk가 기존 Player 초기화에서 기대하는 기초값인지 서버 최종값인지 명시하고 강화·장비 보너스를 두 번 적용하지 않는다. |
| Currency | Gold, AncientCoin, ArcaneKnowledge, ClassFragment, Ruby, EquipmentStone의 long 절대 잔액. 기존 KingdomSupply/TrainingTome 소유값도 보존·명시 매핑하며 다른 재화로 재해석하지 않는다. |
| EquipmentInventory | 5절의 개별 보유·추가 보관·예비 스택·자동 분해 설정 |
| MageTower | 6절의 명시적 스킬 상태와 5개 슬롯 |
| CharacterJobs / JobTrees | 캐릭터별 선택 직업, 해금 목록 |
| Gacha | 장비 천장 카운트와 정의 버전 |
| Quests | 진행·완료·수령·이월 보상·기간·정의 버전 |
| NextRewardSequence | 기존 사냥 요청을 실제 연결할 때 필요한 다음 서버 승인 순번. 로그인에서 복구할 출처가 현재 DTO에 없어 확장 필요. |

UserStateSnapshotDTO는 위에서 SessionID를 뺀 재조회용 구조다. 기존 UserInitResponseDTO의 평평한 필드와 새 블록 사이의 최종 필드 배치는 DTO 구현 전 합의하며, 같은 상태를 두 군데서 수정하는 구조는 만들지 않는다.

기존 Inventory, CharacterEquipmentDatas, SkillTreeDatas는 압축 요약이다. 새 K의 계약에서는 이 legacy 필드를 optional/deprecated로 두고 상세 블록을 필수로 사용한다. 수량이 65,535를 넘는 등 압축 형식으로 완전하게 표현할 수 없으면 해당 legacy 목록 전체를 생략한다. 일부 항목을 빼거나 수량을 자르거나 Pack 실패 때문에 새 클라이언트 로그인을 막지 않는다. 상세 블록 소비자는 legacy 목록 생략을 오류로 취급하지 않는다.

이전 호출부 호환을 위해 표현 가능한 legacy 목록을 남기더라도 상세 상태에서 생성하는 읽기 전용 투영일 뿐이다. 둘을 독립 갱신하거나 병합하지 않는다. 모순되는 요약은 계약 오류이며, 신규 연동은 상세 블록만 상태 복원에 사용하고 매번 임의 장비 ID를 재생성하지 않는다. 기존 K 소비자가 이런 확장 상태를 처리할 수 없다면 프로토콜 불일치 정책에 따라 업데이트가 필요하다.

기존 로컬 계정 저장은 삭제/자동 업로드하지 않는다. 서버 초기 상태와 로컬 성장 기록을 합치는 정책은 별도의 계정 이관 작업이다. 강화를 레벨만 보고 과거 지출로 역산하여 환급액을 만들어서는 안 된다.

## 5. 장비 인벤토리 프로토콜

### 5.1 클라이언트 구조와 손실 없는 응답

~~~text
EquipmentInventoryDTO
  Items: EquipmentInstanceDTO[]             // 현재 Equipment, 최대 300
  PendingItems: EquipmentInstanceDTO[]      // 현재 PendingEquipment, 최대 100
  ReserveStacks: EquipmentReserveStackDTO[] // 현재 LegacyEquipment
  AutoDismantleMask: int                    // 0..7

EquipmentInstanceDTO
  InstanceId: string
  DefinitionCode: int                      // 현재 EquipmentSave.Code / EquipmentData.itemCode
  EnhancementLevel: int                    // 현재 Level
  EquippedCharacterIndex: int?             // 현재 Player: null 또는 0..2
  IsLocked: bool                           // 현재 Locked
  ExpiresUtc: long                         // 현재 ExpiresUtc, 무기한 0
  EnhancementStonesSpent: long             // 실제 누적 지출

EquipmentReserveStackDTO
  DefinitionCode: int
  EnhancementLevel: int
  Count: int                               // 16비트 압축 수량으로 보내지 않는다
~~~

현재 EquipmentSave/EquipmentInstance로 직접 투영할 수 있는 정보다. 전송상의 이름만 다르고 게임 모델의 의미는 그대로다. 응답 Items는 획득순으로 안정적으로 정렬한다. 현재 ‘최근 획득’ 정렬은 배열 위치에 의존하므로 서버가 매번 다른 순서로 반환하면 안 된다. 서버 내부 획득 순번을 유지하고 조회에 사용한다.

ItemCode.Code는 종류·강화·수량의 압축 표현이지 개별 소유 장비 ID가 아니다. Equals도 정의 코드를 비교한다. 강화·장착·잠금·개별 분해 요청은 InstanceId로 대상을 지정해야 한다. 기존 OnEnchantEquipment.ItemCode를 받는 과도기에도 복수 개체 중 임의의 하나를 고르지 않는다.

### 5.2 요청/응답 DTO 목록

아래 변경 요청은 모두 MutationRequestDTO의 K, SessionID, RequestId를 포함한다. 모든 성공 응답은 ApiResponseDTO와 MutationReceiptDTO를 포함하며 변경된 장비/재화/직업/퀘스트 블록을 함께 반환한다. ‘추가 응답’은 연출이나 결과 안내에 필요한 작업별 자료다.

| 요청 DTO / 함수명 제안 | 작업 인자 | 추가 성공 응답 |
|---|---|---|
| OnGetEquipmentInventoryRequestDTO / OnGetEquipmentInventory | 세션 조회, 변경 없음 | OnGetEquipmentInventoryResponseDTO: StateRevision, EquipmentInventory, Currency, ServerTimeUtc |
| OnEnchantEquipmentRequestDTO / 기존 OnEnchantEquipment 확장 | InstanceId, ExpectedLevel | OnEnchantEquipmentResponseDTO: UpdatedItem, ConsumedEquipmentStone |
| OnEquipEquipmentRequestDTO / OnEquipEquipment | CharacterIndex, InstanceId, ExpectedPreviousInstanceId(null 허용) | OnEquipEquipmentResponseDTO: EquippedInstanceId, UnequippedInstanceId |
| OnUnequipEquipmentRequestDTO / OnUnequipEquipment | CharacterIndex, ExpectedInstanceId | OnUnequipEquipmentResponseDTO: UnequippedInstanceId |
| OnSetEquipmentLockedRequestDTO / OnSetEquipmentLocked | InstanceId, Locked | OnSetEquipmentLockedResponseDTO: UpdatedItem |
| OnDismantleEquipmentRequestDTO / OnDismantleEquipment | Mode(Single/Bulk), 아래 확정 대상 목록 | OnDismantleEquipmentResponseDTO: RemovedInstanceIds, ConsumedStacks, GrantedEquipmentStone |
| OnSetAutoDismantleRequestDTO / OnSetAutoDismantle | RarityMask: 0..7 | OnSetAutoDismantleResponseDTO: 적용된 mask |
| OnClaimPendingEquipmentRequestDTO / OnClaimPendingEquipment | InstanceId | OnClaimPendingEquipmentResponseDTO: 이동된 UpdatedItem |
| OnClaimReserveEquipmentRequestDTO / OnClaimReserveEquipment | DefinitionCode, EnhancementLevel | OnClaimReserveEquipmentResponseDTO: 신규 InstanceId를 가진 1개 장비, 해당 스택 최종 수량 |

서버 소유권 확인, 캐릭터 인덱스, 현재 직업의 사용 가능 무기, 동일 개체 중복 장착, 기존 무기 교체를 검증한다. 잠금은 분해 보호이며 현재 클라이언트가 허용하는 강화·장착까지 금지하는 새 규칙으로 확대하지 않는다. 최근 응답에 담긴 다른 캐릭터가 장착 중인 장비를 강제로 빼앗아 장착하지 않는다.

### 5.3 분해 확인창의 의미 보존

~~~text
OnDismantleEquipmentRequestDTO
  Mode: Single | Bulk
  Items: DismantleItemTargetDTO[]
    InstanceId: string
    ExpectedDefinitionCode: int
    ExpectedLevel: int
    ExpectedStonesSpent: long
    ExpectedStorage: Inventory | Pending
  Stacks: DismantleStackTargetDTO[]
    DefinitionCode: int
    EnhancementLevel: int
    ConfirmedCount: int
~~~

서버 구현 순서:

1. 중복 RequestId를 먼저 조회한다. 이미 확정한 동일 요청이면 결과를 재반환한다.
2. 목록은 합쳐서 최소 1개 대상이 있어야 하며 크기 상한, InstanceId 형식, enum/레벨/지출 범위, 중복 개체 ID와 중복 스택 키를 검증한다. 모든 대상의 소유·미잠금·미장착·위치·코드·레벨·실제 지출을 서버 상태와 비교한다. Single은 단일 개체이며 스택을 받지 않고, Bulk는 현재 정책대로 +0만 허용한다.
3. ConfirmedCount는 반드시 1 이상 int 범위이며 서버 수량 이하여야 한다. 0·음수·범위 초과를 거절한다. 수량 합계·차감·지급액 계산은 checked 범위 검증을 하고 하나라도 실패하면 전부 취소한다. 확인창을 연 뒤 수량이 늘어도 확정했던 수량만 소모한다.
4. 서버가 분해 지급량을 계산한다. 요청에는 승인할 지급량을 받지 않는다.
5. 대상 제거/스택 차감/강화석 지급/완료 기록을 함께 확정한다. 하나라도 검증 실패하면 전부 취소한다.

새로 획득한 장비를 서버에서 필터 재실행하여 기존 분해에 포함하면 안 된다. 관계없는 사냥 수입이나 신규 장비 때문에 계정 revision이 바뀌어도 확정 대상이 유효하면 분해를 허용한다. 강화·장착·잠금·이동·삭제로 대상이 바뀌면 재확인을 요구한다. 요청의 기대값은 지급 근거가 아니라 충돌 검출용이며, 서버 데이터가 항상 기준이다.

### 5.4 서버가 구현할 현행 장비 규칙

- 보유 300개 → 추가 보관 100개 → 코드/강화별 예비 스택. 보상 획득 자체는 공간 부족으로 거절하지 않는다.
- 추가 보관은 무기한이다. 지급 성공과 ‘보유 칸으로 꺼내기 실패’를 구분한다. 꺼내기만 공간 부족으로 거절될 수 있다.
- 예비 스택은 현재 Code/Level/Count만 담는다. 신규 일반 +0 보상처럼 별도 속성이 없는 개체만 합친다. 잠금·장착·실제 지출 이력이 있는 개체를 이 형태로 압축하지 않는다.
- 현 모델이 표현하지 못하는 특수 초과 개체/Count가 int 범위를 넘는 데이터는 서버 원본에 보존하고 명시적인 이관/스키마 조정 대상으로 둔다. 수량을 자르거나 중복 스택으로 위장하지 않는다. 기본 보상 공급에서는 이 상태를 만들지 않도록 생성 규칙을 맞춘다.
- 강화는 확정 성공이며 일반/희귀/영웅의 기본 분해량은 1/4/12이다. 강화 비용은 2 × 기본 분해량 × (1 + floor(현재 레벨/3)). 실제 성공한 소비량을 EnhancementStonesSpent에 누적한다. 최대 레벨은 기존 정의를 따르며 현재 저장 검증 상한은 15다.
- 분해 지급은 기본 분해량 + floor(실제 강화석 누적 지출 × 0.8). 옛 지출이 불명확하면 추정 환급하지 않는다.
- 자동 분해는 이후 획득한 알려진 +0·미잠금·미장착 장비에만 적용한다. 설정 변경으로 기존 소유 장비를 일괄 삭제하지 않는다.
- 활은 신규 보상 풀에서 제외하되 기존 소유와 원본 코드는 보존한다. 등급 확률·천장은 유지한다.
- 필터·정렬·사용 가능 장비만 보기 같은 화면 선호는 로컬에 둘 수 있다. 자동 분해 설정은 실제 지급에 영향을 주므로 서버에 둔다.

장비 지급은 서버 내부 공용 서비스로 묶어 뽑기·필드·최초 클리어가 같은 보관/분해 규칙을 사용하게 한다. 클라이언트가 임의 장비와 수량을 보내 지급받는 일반 Grant API는 만들지 않는다.

## 6. 마탑 스킬·직업·뽑기의 부족한 계약

### 6.1 마탑 스킬

기존 OnEnchantSkill/OnAwakeningSkill 응답은 SkillCode만 반환한다. 현행 MageSkillStateSnapshot은 이미 손실 없는 서버 어댑터를 위한 구조를 제공한다.

~~~text
MageTowerStateDTO
  CatalogVersion: string
  Skills: MageSkillStateDTO[]
    SkillId: int
    EnhancementLevel: int
    AwakeningLevel: int
    Fragments: int
    KnowledgeSpent: long
    BloomEnabled: bool
  EquippedSkillIds: int[5]   // -1은 빈 슬롯, 중복 장착 금지
~~~

SkillCode의 16비트 조각 수량만 사용하면 int로 저장하는 현재 Fragments가 잘릴 수 있다. 상세 DTO를 기준으로 하고 압축 코드는 호환 요약으로만 취급한다.

| 요청 DTO / 함수명 | 인자 | 응답 DTO의 작업별 자료 / 서버 책임 |
|---|---|---|
| 기존 OnEnchantSkillRequestDTO | SkillCode, RequestId. 향후 상세 SkillId와의 매핑을 명시 | OnEnchantSkillResponseDTO + Receipt, 변경 스킬, 소비 지식. 서버가 현재 레벨과 비용 계산 |
| 기존 OnAwakeningSkillRequestDTO | SkillCode, RequestId | OnAwakeningSkillResponseDTO + Receipt, 변경 스킬, 소비 조각 |
| OnSetSkillLoadoutRequestDTO | EquippedSkillIds[5], ExpectedEquippedSkillIds[5] | OnSetSkillLoadoutResponseDTO + Receipt. 전체 슬롯을 한 번에 검증·교체하여 기존 슬롯 교환 동작 보존 |
| OnSetSkillBloomRequestDTO | SkillId, Enabled | OnSetSkillBloomResponseDTO + Receipt. 각성 10 이상 검증 |
| OnResetSkillEnhancementRequestDTO | SkillId, ExpectedEnhancementLevel, ExpectedKnowledgeSpent | OnResetSkillEnhancementResponseDTO + Receipt, RefundedKnowledge |

서버 규칙: 실제 8종 ID는 0..9 중 6/8을 제외한다. 현재 서버 공유 enum의 0..2만으로 제한하지 않는다. 강화 상한 100, 각성 상한 10, 중복 획득 조각 30, 초기 획득은 보유 등록, 강화 초기화 환급은 실제 지출의 floor(80%)이다. 개화 전환으로 진행 중인 시전이나 쿨다운을 초기화하지 않는다. 자동 시전 선택은 현재의 로컬 설정으로 유지할 수 있다.

독립 운석 ID 8의 기존 투자와 원본 내역은 서버 이관에서 보존하고 ID 3 유성우에 통합하는 기존 MageCatalogMigration을 기준으로 이전한다. ID 6/8을 신규 뽑기·장착·시전에 되살리지 않는다. 실시간 VFX/조준/타격마다 RPC를 새로 요구하는 설계는 하지 않는다.

### 6.2 직업

현재 클라이언트의 직업 선택 한 번은 미해금이면 해금 비용 차감·해금·선택·비호환 장비 해제를 한 번에 처리한다. 이를 보존하려면 신규 OnSelectJob을 단일 원자 작업으로 추가하는 안이 적절하다. 기존 OnGetJob과 OnChangeJob 계약을 남기더라도 UI 클릭을 이 둘의 순차 호출로 바꿔 중간 실패 상태를 새로 만들지 않는다.

~~~text
CharacterJobStateDTO
  Index: int                   // 0..2
  SelectedJobCode: ulong
  UnlockedJobCodes: ulong[]
~~~

- OnSelectJobRequestDTO(Index, JobCode, RequestId) → OnSelectJobResponseDTO(Index, JobCode, WasNewlyUnlocked, ConsumedClassFragment, Receipt). 미해금이면 선행 조건과 비용을 검사한 뒤 해금·선택·비호환 장비 해제를 함께 확정한다. 이미 해금됐으면 소비는 0이다. 실패하면 해금/소비를 포함해 전부 취소한다.
- 기존 OnGetJobRequestDTO/ResponseDTO는 해금 전용, OnChangeJobRequestDTO/ResponseDTO는 기존 서버 의미를 확인해 호환용으로 유지한다. 기존 요청에서도 Index를 반드시 채운다. OnSetJobTree는 빈 요청이므로 이름만 보고 클라이언트가 임의 해금 목록을 쓰는 API로 확대하지 않는다.
- 선택 직업은 현재 ProgressionState.Jobs로 투영한다. 실제 전투 외형/행동은 현재와 같이 적용 가능한 웨이브 경계에서 반영한다. 서버가 선택 상태와 실제 전투 적용 시점을 혼동하지 않도록 향후 전투 세션과 연결한다.
- 창병은 기본, 일반 전직은 40, 정예 전직은 120 조각이며 정예에는 선행 직업 해금이 필요하다. 궁수/정예 궁수는 선택 불가, 기존 투자·해금 기록은 보존한다.
- 변경 가능 시점은 현재 메인 일반 웨이브 조건을 유지한다. 서버에 전투 상태 검증이 없으면 클라이언트의 ‘변경 가능’ 주장만으로 서버 검증이 완료됐다고 하지 않는다.

### 6.3 뽑기

기존 OnGachaEquipmentClassFragment / OnGachaSkillArcaneKnowledge를 유지한다. 요청은 Count(1 또는 10)+RequestId다. 뽑기 결과 목록과 실제 반영된 상태는 분리한다.

~~~text
GachaStateDTO
  EquipmentPity: int           // 0..39, 영웅 획득 시 0
  DefinitionVersion: string

GachaRewardDTO
  RollIndex: int
  RewardType: Equipment | Skill | Currency
  DefinitionCode: int?        // 장비 정의
  InstanceId: string?         // 실제 개별 보관된 장비만, 스택화 시 필수 아님
  SkillId: int?
  Currency: CurrencyId?
  Amount: long                // 이번에 지급된 수량, 최종 잔액이 아님
  IsDuplicate: bool
  Disposition: Inventory | Pending | Reserve | AutoDismantled | SkillOwned | SkillFragments | Wallet

기존 두 뽑기 ResponseDTO 확장
  Rewards: GachaRewardDTO[]    // 뽑기 순서 보존
  Receipt: MutationReceiptDTO // 재화·천장·장비/스킬·퀘스트 최종 상태
~~~

클라이언트 GachaRewardEntry의 Sprite/name/EquipmentData는 정의 ID로 기존 에셋에 연결한다. 자동 분해 결과는 강화석 보상으로 표시하면서 원래 장비 정의를 보조 정보로 남긴다. 10연 뽑기 중 중복/최초 획득 여부를 순서대로 계산한다. 기존 GachaList와 ClassFragmentCnt/ArcaneKnowledgeCnt만으로는 혼합 보상 순서·최종 잔액·천장·보관 결과를 복원할 수 없다. 기존 Cnt의 의미는 서버 확인 전 지급량/최종 잔액 어느 쪽으로도 추측하지 않는다.

현행 규칙을 서버에 이식한다: 회당 고대주화 50, 장비 뽑기는 일반 70%/희귀 20%/영웅 5%/전직 조각 2개 5%, 40번째 천장. 스킬 뽑기는 스킬 50%(활성 8종 동일 확률), 비전 지식 10개 30%/20개 15%/50개 5%. 비용·순서별 난수·천장·보상·퀘스트·영수증이 한 번에 확정되어야 한다. 클라이언트가 서버 결과를 다시 랜덤으로 뽑거나 중복 조각을 다시 더하지 않는다.

## 7. 퀘스트와 업적의 신규 계약

### 7.1 공통 모델

현재 eQuestCategory의 Guide=0, Daily=1, Weekly=2, Achievement=3과 eQuestObjectiveType 0..25, ProgressMode를 유지한다. 새로운 enum의 숫자를 임의 재배열하지 않는다. 서버에는 Unity 오브젝트가 없는 공통 정의/상태를 둔다.

~~~text
QuestDefinitionDTO
  QuestId: long, Category: int, Title: string, Description: string
  ObjectiveType: int, ProgressMode: int, TargetId: long, RequiredCount: int
  NextQuestId: long, RewardGroupId: int, IsRepeatable: bool, PresentationType: int

QuestRewardDTO
  Currency: CurrencyId, Amount: long, IsDynamicGold: bool

QuestRowDTO
  QuestId: long, PeriodKey: string, DefinitionVersion: string
  Category: int, Title: string, Description: string
  ObjectiveType: int, TargetId: long, RequiredCount: int, PresentationType: int
  Progress: long, State: Locked | InProgress | Claimable | Claimed
  IsPending: bool, ExpiresUtc: long
  Rewards: QuestRewardDTO[]
  HasFixedGold: bool, FixedGold: decimal?  // 과거 확정 보상의 보존용

QuestStateDTO
  DefinitionVersion: string, ServerTimeUtc: long
  DayKey: string, WeekKey: string, DailyResetUtc: long, WeeklyResetUtc: long
  Rows: QuestRowDTO[]                     // 이월 보상은 같은 QuestId의 다른 PeriodKey로 공존
  CompletedQuestIds: long[], ClaimedKeys: QuestPeriodKeyDTO[]
  ActiveGuideQuestId: long?
~~~

서버 내부에는 기간별/평생 카운터, PendingQuests의 달성 당시 정의/보상, BestStatTotal/BestMageTotal/BestEquipmentTotal, 계정 진행 근거도 보존한다. 클라이언트 UI는 서버가 만든 행 스냅샷을 사용하고, 로컬 검증기에 동일 저장 모델을 투영하는 구현을 택하면 이 내부 데이터까지 추가 DTO로 받아야 한다. 이 두 경로를 섞어 서버 표시와 로컬 재계산이 충돌하게 하지 않는다. 권장 경로는 서버 행 스냅샷을 기존 QuestBoardSnapshot/QuestRowSnapshot으로 변환하는 것이다.

QuestClaimToken.AccountGeneration은 클라이언트의 이전 계정 콜백 차단용이다. 서버의 계정 ID로 전송하지 않는다. 서버 수령 키는 인증 계정+QuestId+PeriodKey이며 RequestId가 달라도 이 키로 이중 지급을 방지한다.

### 7.2 요청/응답

| 요청 DTO / 함수명 | 인자 | 응답 DTO / 필요한 데이터 |
|---|---|---|
| OnGetQuestCatalogRequestDTO / OnGetQuestCatalog | 세션, KnownDefinitionVersion(선택) | OnGetQuestCatalogResponseDTO: DefinitionVersion, NotModified, CatalogJson. 현재 guide/quests/achievements/rewards JSON 형태를 그대로 제공해 기존 QuestCatalog.Parse와 연결 가능 |
| OnGetQuestStateRequestDTO / OnGetQuestState | 세션, 전체 카테고리 조회 | OnGetQuestStateResponseDTO: StateRevision, 완전한 QuestState. 카테고리 필터는 UI에서 수행하고 부분 Rows를 전체 상태로 교체하지 않음 |
| OnClaimQuestRewardRequestDTO / OnClaimQuestReward | QuestId, PeriodKey, RequestId | OnClaimQuestRewardResponseDTO: QuestId, PeriodKey, GrantedRewards, Receipt. 다음 가이드·업적·전체 완료 목표도 같은 변경에 포함 |
| OnReportQuestActivityRequestDTO / OnReportQuestActivity | BatchId, 서버 발급 ActivitySessionId, Sequence, 활동 목록 | OnReportQuestActivityResponseDTO: AcknowledgedSequence, NextSequence, 서버 승인 진행 상태/Receipt |

CatalogJson은 현재 정적 정의를 서버에서 공급하려는 경우의 계약이다. 이 요청이 장비/직업/스킬의 모든 아트·전투 SO를 CDN으로 옮기는 작업까지 의미하지 않는다. NextQuestId의 현 클라이언트 필드는 int이므로 long 제안값도 실제 투영 가능한 범위로 검증한다. 현재 ID를 바꾸지 않는다.

활동 보고는 SkillCast(SkillId, 횟수)·BattleTime(서버가 검증 가능한 활동 구간) 등 서버가 직접 알 수 없는 사건에 한정한다. 유저가 퀘스트 Progress나 RewardAmount를 지정하는 API를 만들지 않는다. 뽑기·강화·전직·장착·획득은 해당 서버 트랜잭션에서 집계하여 이 보고로 중복 증가시키지 않는다. 같은 배치 재전송은 재집계하지 않는다. 배치 크기·허용 시간·순번·계정·세션을 검증하고, 다음 로그인에서 승인 순번을 복구한다.

서버 전투 세션과 유효 시간 검증이 없는 상태에서 ActivitySessionId만 추가했다고 서버 검증이 완성됐다고 보지 않는다. 해당 세션/검증 계약은 8절의 전투 의존성이며 실제 연결 전 필요하다.

### 7.3 서버가 그대로 보존할 규칙

- 일일 기준은 KST 자정, 주간 기준은 KST 월요일 자정. 서버 시각으로 기간을 판정한다.
- 일일/주간은 현재 메인 1-11 클리어 조건을 유지한다. 가이드는 선행 수령 조건을 따르고 업적은 기존 체인/최고 기록 정책을 따른다.
- 일일/주간 달성 시 해당 기간 종료 시각+7일을 만료 시각으로 하는 미수령 기록을 보존한다. 정의가 이후 비활성화되어도 달성 당시 표시/보상으로 수령할 수 있다.
- 일반 동적 골드는 수령 시점의 서버 상태로 계산한다. 과거 HasFixedGold=true인 이관 기록만 기존 확정액을 보존한다. 모든 동적 보상을 달성 시 고정하는 정책으로 바꾸지 않는다.
- 보상 계산·지급·수령 표시·다음 목표 갱신은 원자적이다. 수령 실패와 이미 수령/만료/잠금/미완료를 구분한다.
- 스테이지·몬스터·던전·환생·오프라인 수령 목표는 각각의 서버 성공 근거가 필요하다. 퀘스트 API만 작성했다고 이 근거가 생기는 것은 아니다.
- 서버는 정적 퀘스트 카탈로그와 클라이언트 코드에 존재하는 조건을 함께 참고한다. 아직 구현되지 않았거나 폐기된 신 스킬 관련 목표는 복원하지 않는다.

## 8. 클라이언트에는 있으나 서버 계약이 없거나 부족한 기능 목록

‘계약 없음’은 현재 RequestDTO/ResponseDTO에 대응 API가 없다는 뜻이다. 서버 저장소를 확인한 구현 부재 판정은 아니다. 요청 콘텐츠 외 항목은 읽기/문서 설계만 수행하며 이번 코드 수정 승인 대상으로 자동 편입하지 않는다.

| 기능 | 현재 계약 상태 | 필요한 서버 처리와 받을 데이터 |
|---|---|---|
| 개별 장비 장착/해제·잠금·분해·자동 분해·추가 보관 회수 | 없음 | 5절의 개별 ID 계약, 원자적 변경, 재화와 보관 결과 |
| 장비 강화 | 있으나 불충분 | 개체 식별, 강화석 비용·잔액·실제 지출 이력 |
| 스킬 장착·개화·강화 초기화 | 없음 | 5슬롯/개화/실제 지출과 환급 |
| 스킬 강화·각성 | 있으나 불충분 | 전체 활성 로스터, 손실 없는 조각, 소비 재화/각성 상태 |
| 직업 해금·선택 | 있으나 불충분 | 캐릭터 Index 전달, 해금/선택 구분, 재화·장비 자동 해제 |
| 뽑기 | 있으나 불충분 | 서버 난수·천장·순서별 결과·중복 보상·재화·보관·실습 영수증 |
| 가이드/일일/주간/업적 | 없음 | 7절 전체 |
| 골드 공격력/체력 강화 | 있음, 현 규칙 대응은 확인 필요 | 실제 구매 횟수(1/10/MAX), 상한 300, 서버 비용/잔액, 퀘스트·실습 성공 |
| 루비 영구 성장/초기화 | 없음 | 강화 종류·레벨·실지출, 일일 초기화 제한, 웨이브 경계 예약/환급 |
| 환생 | 없음 | 조건·예상 보상·예약·웨이브 경계 확정·초기화/보존 항목 |
| 던전 입장/종료/최초 보상 | 별도 계약 없음 | 서버 전투 ID, 티켓·리셋 시간, 난도 기록, 최초 보상 중복 방지, 종료 지급 |
| 오프라인 보상 | 전용 계약 없음 | 서버 활동 시각, 확정 보상 구간·사냥 속도 근거·골드/경험치, 중복 수령 방지 |
| 필드 장비 드롭·최초 클리어 장비/스킬 지급 | OnHunt/OnStageClear만으로 불충분 | 승인 전투/처치 순번, 서버 보상 결정, 인벤토리·스킬·재화 변경 |
| 튜토리얼 실습 재화와 성공 영수증 | 없음 | 단계별 고대주화 50의 일회 지급과 실제 뽑기/강화 성공의 연결 |

다음은 범위 밖 기능의 후속 DTO 설계 개요다. 모든 변경은 공통 MutationRequestDTO/Receipt를 사용한다.

| 요청/응답 DTO 제안 | 요청 핵심 | 성공 응답 핵심 |
|---|---|---|
| OnGetRubyGrowthState / OnGetRubyGrowthStateResponseDTO | 세션 조회 | GoldLevel/ExpLevel, 각 Spent/BestLevel, ResetDay, PendingReset |
| OnEnhanceRubyGrowth / OnEnhanceRubyGrowthResponseDTO | Kind(Gold/Exp), ExpectedLevel | 확정 레벨·실제 비용·Currency·Receipt |
| OnRequestRubyGrowthReset / OnRequestRubyGrowthResetResponseDTO | Kind, 예상 레벨/실지출 | 예약 상태. 실제 환급은 현재 정책의 경계에서 확정하고 별도 완료 상태로 통지 |
| OnGetReincarnationPreview / OnGetReincarnationPreviewResponseDTO | 세션 조회 | 가능 여부/거절 이유, 예상 상승치, 횟수·쿨다운·일일 잔여 |
| OnRequestReincarnation / OnRequestReincarnationResponseDTO | 서버 BattleId | 예약 ID/상태. 웨이브 종료 처리에서 확정한 Receipt는 골드·골드 강화 초기화와 MainStage=1-1 및 보존 성장 상태 포함 |
| OnEnterDungeon / OnEnterDungeonResponseDTO | DungeonId, Difficulty | 서버 BattleId, 티켓 최종 잔액, 기간, 적용 난도 |
| OnCompleteDungeon / OnCompleteDungeonResponseDTO | BattleId, 최종 승인 이벤트 순번 | 검증된 결과, 실제 보상, 최초 클리어 여부, 기록·티켓·Receipt |
| OnGetOfflineRewardPreview / OnGetOfflineRewardPreviewResponseDTO | 세션 조회 | 서버 발급 ClaimId, FromUtc/ToUtc, 적용 시간·골드·경험치 |
| OnClaimOfflineReward / OnClaimOfflineRewardResponseDTO | ClaimId | 실제 지급량, 계정 레벨/경험치·재화·새 활동 기준 시각·Receipt |
| OnActivityCheckpoint / OnActivityCheckpointResponseDTO | 서버 활동 세션/순번 | 승인 시각·다음 순번. 클라이언트 시계나 임의 사냥 속도를 그대로 승인하지 않음 |
| OnClaimGuidePracticeCoins / OnClaimGuidePracticeCoinsResponseDTO | SequenceId, StepId/Action | 서버 단계 검증 후 기존 claim key 의미에 따른 일회 지급/기지급 상태·Receipt |

위 API별 RequestDTO는 표의 이름에 RequestDTO를 붙인 클래스이며 실배포 이름은 미정이다. 루비 초기화/환생은 요청 수락과 실제 확정을 구별한다. 던전 실패/중도 종료는 종료 사유를 포함하는 종료 계약을 추가하여 입장 티켓·중복 종료·보상을 일관되게 처리해야 한다.

실습 연결이 승인되면 GuidePracticeStateDTO에 서버가 확정한 지급 키(SequenceId+Action), 행동 성공 키(SequenceId+StepId)를 명시적인 목록으로 담는다. 클라이언트는 이를 기존 Claims 및 game-direct-action 관련 Modules 키에 제한적으로 투영한다. 뽑기/강화 요청에는 필요한 GuideContext(SequenceId, StepId)를 선택적으로 포함하고, 서버가 현재 단계·행동·1회 조건을 검증한 뒤 같은 소비 트랜잭션에 성공 영수증을 기록한다. 클라이언트가 임의 성공 키를 제출해 저장하는 API는 제공하지 않는다.

참고할 현행 정책: 루비 강화 2-5 해금/상한 50/초기화 실지출 80% 환급과 하루 한 번, 환생은 일반 메인 전투·주기 보스 조건·600초·하루 3회·레벨 상한 300을 따른다. 오프라인은 최대 8시간, 안전한 일반 메인 스테이지, 최대 분당 30처치의 60% 반영이다. 수치는 현재 코드에 근거하며 서버 정책 변경을 제안하는 문서가 아니다.

골드 강화의 기존 OnEnchantRequestDTO.Count에 MAX=-1을 그대로 보내지 않는다. 서버가 MAX 구매 모드를 지원할지, 명시 횟수 요청으로 한정할지 공유 계약으로 정한다. ‘최대 구매’ 버튼의 현재 의미를 유지하려면 서버가 자신의 잔액/상한으로 실제 횟수를 계산하는 PurchaseMode 확장이 적절하다.

## 9. 공통 데이터·카탈로그와 수정 범위

- 공통 enum은 숫자와 의미를 명시적으로 매핑한다. 현재 서버 eCurrency에 Ruby/EquipmentStone이 없고, CurrencyQueryDTO에는 Ruby가 있으나 EquipmentStone이 없다. 현재 클라이언트 Ruby=6, EquipmentStone=7을 포함해 양쪽 정의를 합의한다.
- 서버 공유 스킬 enum은 0..2뿐이고 실제 클라이언트는 8종이다. 폐기 ID를 재사용하지 않고 지원 범위를 맞춘다. 현재 서버 상수의 장비/스킬 상한 9와 클라이언트의 15/100도 그대로 일치한다고 가정하지 않는다.
- 장비·직업·스킬의 정적 정의와 Sprite/Prefab 참조는 기존 자산 ID를 유지한다. 서버가 비용·확률·보상 계산에 쓰는 정의는 클라이언트와 버전을 맞춘다. 모든 SO와 전투 수치를 서버 카탈로그 다운로드로 교체하는 작업은 이번 요구에서 자동 확대하지 않는다.
- 퀘스트 정의 JSON의 서버 공급은 7절 계약으로 표현한다. 뽑기 확률/가격을 서버에서 표시하도록 확장한다면 GachaCatalogDTO에 비용, 허용 횟수, 등급/보상 확률, 활성 풀 ID, 천장 기준을 담되 실제 서버 추첨 규칙과 같은 정의에서 생성한다.
- 사냥·최초 클리어·던전·환생·오프라인·튜토리얼은 같은 재화/장비/스킬을 변경한다. 따라서 대상 매니저만 바꾸고 LocalProgression 전체를 서버 모드로 전환해서는 안 된다.
- 위 보상 출처의 서버 연결 또는 명확히 승인된 이행 정책이 완료되기 전에는 계정 전체가 서버 권위로 전환되었다고 표시하거나 배포하지 않는다. 범위 밖 파일 수정은 사용자 별도 승인 전 수행하지 않는다.
- 서버 미지원 기능을 로컬에서 몰래 성공시키는 fallback은 사용하지 않는다. 테스트용 응답은 명시적 테스트 환경에만 사용하고 운영 연결 완료와 구분한다.

## 10. 단계별 실행계획과 완료 조건

### 단계 A — 이번 턴: 설계 문서와 독립 검토

현재 코드 읽기 → 계약의 존재/부족/부재 구분 → 본 문서 작성 → 별도 에이전트 검토 → 근거 있는 지적 반영 → 사용자에게 범위·서버 필요 작업 제시. 코드 변경 없음.

### 단계 B — 승인 후: 공유 계약 확정 및 호환성 정리

- 공유 K와 버전 상수 위치, 실제 함수명, 업무 오류 전달 방식을 확정한다.
- 장비 개체 ID, 상세 초기화 블록, 재화/스킬 enum, RequestId·상태 커밋 계약을 확정한다.
- DTO/서버 enum과 Unity 호환 namespace/문법/using을 정리한다. 범위 밖 소비자에 파급되면 호환 어댑터 또는 추가 허용 범위를 먼저 확정한다.
- 서버용 수치·정적 정의와 클라이언트 현재 규칙 차이를 표로 검증한다. 서버값에 맞추려고 클라이언트 기능을 축소하지 않는다.
- 기존 미커밋 변경은 사용자 작업으로 보존한다. 완료된 변경만 분리해 보존 커밋하고, 불완전한 새 DTO나 무관한 설정을 임의로 완료 작업으로 묶지 않는다.

완료 조건: DTO 직렬화 예시와 서버/클라이언트 계약 테스트가 같은 필드·숫자·오류를 사용한다. 실제 값이 미확정인 부분을 하드코딩으로 숨기지 않는다.

### 단계 C — 승인 후: RPC 공통화와 초기 진입

- NetworkManager에 공통 호출·두 실패 분기·세션/계정 세대 검사를 적용한다.
- Authentication의 가입/로그인 응답을 UserInitResponseDTO로 통일하고 데이터 로딩 완료까지 진입을 막는다.
- UserManager 및 타이틀 진입 조건에 검증된 서버 상태를 연결한다.
- 서버 데이터를 한번 가져와 로컬 권위로 고정하는 경로와 분리한다. 기존 로컬 저장을 삭제하거나 서버로 자동 이관하지 않는다.

완료 조건: 신규/기존 계정 각각 캐릭터 3명·재화·장비·스킬·직업·퀘스트를 서버 응답으로 복원하며 중간 실패/다른 계정의 늦은 응답에 메인으로 들어가지 않는다.

### 단계 D — 서버 구현과 대상 콘텐츠 연결

서버는 장비 지급/재화 원장/멱등 처리부터 준비한다. 그 위에 장비·스킬·직업·뽑기·퀘스트 작업을 붙인다. 클라이언트는 현재 매니저의 게임 규칙·화면 구조를 보존하고 요청 전송과 서버 결과 적용 경계를 교체한다.

동기 bool 성공을 ‘요청 전송 성공’으로 바꿔 UI를 먼저 성공시키지 않는다. 비동기 대기/중복 클릭 방지/최종 성공 콜백을 연결한다. 서버가 없는 API는 계약 테스트로 검증하되 실제 연동 완료로 보고하지 않는다.

완료 조건: 같은 계정 재로그인/다른 기기에서 같은 상태 복원, 요청 응답 유실 후 중복 차감 없음, 콘텐츠 성공 시 최종 잔액·장비·스킬·퀘스트가 하나의 서버 커밋과 일치.

### 단계 E — 별도 범위 승인 후: 공용 보상 출처 연결

필드·최초 클리어·던전·환생·오프라인·실습 재화의 서버 권한과 지급 연결을 완성한다. 정책 자체를 변경하는 단계가 아니다. 이 단계가 승인되지 않으면 코드 수정하지 않고 전체 경제 서버 전환 완료의 차단 요인으로 남긴다.

### 단계 F — 통합 검증과 전달

- Unity 6000.3.21f1 import/컴파일, 프리팹 직렬화 참조, 실제 로그인→메인→대상 메뉴/전투 확인.
- 기존 클라이언트 인수 검증의 정책 기대값을 재사용하되 로컬 저장 성공만 검사하는 테스트를 서버 성공으로 간주하지 않는다.
- 주요 변경 묶음마다 연결된 Android 기기에서 신규/기존 계정·앱 재시작·통신 실패·백그라운드 복귀 검증. 실제 사용한 기기와 미검증 범위를 기록한다.
- 코드 변경은 승인 범위 안에서 단계별 커밋한다. 설정/에셋/SDK/CDN/자동화 등 무관한 변경을 섞지 않는다.

## 11. 구체적인 검증 시나리오

1. K 일치/불일치, 필수 필드 누락, 스킬 ID 0, 캐릭터 0, long/ulong 최댓값 근처를 통한 정확한 왕복.
2. SDK error callback, success callback의 result.Error, FunctionResult의 ErrorResponseDTO, 정상 DTO 각각을 구분. 서버 HTTP 400의 실제 SDK 전달 형태도 확인.
3. 기존 세션 취소/계정 전환 중 응답 도착, 세션 만료 뒤 같은 업무 요청 복구, 초기화 중 메인 탭 입력.
4. 동일 정의 장비 2개의 ID·잠금·강화·장착이 독립적으로 유지됨. 배열 순서/최근 획득 정렬 유지.
5. 보유 300+추가 보관 100 초과 시 뽑기·전투 보상이 보존됨. 장비 수량 65,535 초과와 알 수 없는 정의를 잘라내지 않음.
6. 분해 미리보기 후 신규 획득은 남기고, 동일 스택의 증가분도 남김. 대상 하나의 강화/잠금/장착 변경 시 전체 분해와 지급 취소.
7. 동일 RequestId 재요청, 다른 인자에 같은 ID, 서버 저장 중단, 응답 유실 후 재로그인으로 이중 차감/지급 방지.
8. 스킬 조각 65,535 초과, 강화 초기화 실지출 80%, 개화 전환 시 기존 시전·쿨다운 유지, 폐기 ID 6/8 복원 금지.
9. 장비 천장 39에서 다음 뽑기, 10연 중 첫 획득→중복 획득, 자동 분해된 뽑기 보상의 표시·지급 일치.
10. 캐릭터별 전직 해금+선택+장비 해제의 원자성, 중간 실패 전체 롤백, 동일 요청 재시도 시 이중 차감 없음, 궁수 사용 차단과 기존 기록 보존.
11. 퀘스트 KST 일/주 경계, 7일 이월 만료, 과거 기간 버튼, 동일 퀘스트 중복 수령, 정의 비활성화 후 이월 수령, 동적/과거 고정 골드 구분.
12. 상태 revision 누락/역전 시 전체 재조회, 작업 성공 뒤 늦은 이전 응답이 재화를 되돌리지 않음. 변경 블록 누락을 빈 목록으로 처리하지 않음.
13. 튜토리얼 지급으로 시작하는 1회 뽑기/강화의 재화와 성공 영수증이 일치함. 범위 밖 연결 미완료 시 이 시험은 미완료로 기록.
14. 최대 보유 데이터의 요청/응답 크기·메모리·지연 측정, 프레임별 RPC나 변하지 않은 전체 UI 반복 갱신 없음.

## 12. 구현 전 확정할 서버 정보

- 공유 ProtocolVersion 상수 K와 공유 소스/생성 경로.
- 가입/로그인 실제 FunctionName과 두 응답의 실제 샘플.
- 업무 오류를 HTTP 200의 ErrorResponseDTO로 반환할지, 기존 HTTP 400을 유지할지 및 SDK에서 관측한 결과.
- 장비 개체 ID와 실제 지출 이력의 서버 저장 현황, 현재 압축 Inventory의 저장 단위, 기존 데이터 이관 정책.
- 서버의 실제 기능별 상한/비용/확률과 클라이언트 현행 규칙 차이.
- 변경 요청 멱등 기록 보존/복구 방식, 상태 커밋 경계, 실제 최대 응답 크기.
- 범위 밖 공용 보상 지급 연결을 이번 구현에 허용할지. 별도 승인 없이는 수정하지 않는다.

## 13. 코드 근거

아래 링크는 현재 동작의 출처다. 기존 문서의 과거 설명보다 실제 구현을 우선했다.

- [RequestDTO](../../Assets/_Project/Scripts/Server/DTO/RequestDTO.cs), [ResponseDTO](../../Assets/_Project/Scripts/Server/DTO/ResponseDTO.cs), [QueryDTO](../../Assets/_Project/Scripts/Server/DTO/QueryDTO.cs), [ProtocolDTO](../../Assets/_Project/Scripts/Server/DTO/ProtocolDTO.cs), [ErrorResponseDTO](../../Assets/_Project/Scripts/Server/DTO/ErrorResponseDTO.cs)
- [NetworkManager](../../Assets/_Project/Scripts/Core/Manager/NetworkManager.cs), [Authentication](../../Assets/_Project/Scripts/Server/Auth/Authentication.cs), [UserManager](../../Assets/_Project/Scripts/Core/Manager/UserManager.cs), [TitleScreenController](../../Assets/UGUI/Scripts/Views/TitleScreenController.cs)
- [ProgressionState](../../Assets/_Project/Scripts/Core/Balance/ProgressionState.cs), [LocalProgression](../../Assets/_Project/Scripts/Core/Balance/LocalProgression.cs), [Wallet](../../Assets/_Project/Scripts/Wallet/Wallet.cs)
- [EquipmentManager](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentManager.cs), [EquipmentEconomy](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentEconomy.cs), [EquipmentInstance](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentInstance.cs), [EquipmentToolbarView](../../Assets/UGUI/Scripts/Views/EquipmentToolbarView.cs), [EquipmentEconomyAcceptance](../../Assets/_Project/Scripts/Core/Balance/EquipmentEconomyAcceptance.cs)
- [MageTowerManager](../../Assets/MageTower/Scripts/MageTowerManager.cs), [MageSkillStateSnapshot](../../Assets/_Project/Scripts/MageTower/MageSkillStateSnapshot.cs), [MageSkillRules](../../Assets/_Project/Scripts/MageTower/MageSkillRules.cs), [MageCatalogMigration](../../Assets/_Project/Scripts/MageTower/MageCatalogMigration.cs)
- [ChangeJob](../../Assets/_Project/Scripts/Player/Job/Script/ChangeJob.cs), [KingdomArmyManager](../../Assets/_Project/Scripts/Core/KingdomArmyManager.cs), [GachaManager](../../Assets/Gacha/Scripts/GachaManager.cs), [BalanceMath](../../Assets/_Project/Scripts/Core/Balance/BalanceMath.cs)
- [QuestCatalog](../../Assets/_Project/Scripts/Quest/QuestCatalog.cs), [QuestContracts](../../Assets/_Project/Scripts/Quest/QuestContracts.cs), [QuestEconomy](../../Assets/_Project/Scripts/Core/Balance/QuestEconomy.cs), [QuestPeriod](../../Assets/_Project/Scripts/Core/Balance/QuestPeriod.cs), [QuestProgressEvaluator](../../Assets/_Project/Scripts/Quest/QuestProgressEvaluator.cs)
- [BattleEconomy](../../Assets/_Project/Scripts/Core/Balance/BattleEconomy.cs), [RubyProgression](../../Assets/_Project/Scripts/Core/Balance/RubyProgression.cs), [ReincarnationService](../../Assets/_Project/Scripts/Reincarnation/ReincarnationService.cs), [OfflineRewardManager](../../Assets/_Project/Scripts/OfflineReward/OfflineRewardManager.cs), [OfflineRewardCalculator](../../Assets/_Project/Scripts/OfflineReward/OfflineRewardCalculator.cs), [GameDirectInteraction](../../Assets/_Project/Scripts/Direction/GameDirectInteraction.cs)

## 14. 독립 검토 기록

초안 작성 전 별도 에이전트 2명이 장비 계약과 콘텐츠 누락을 읽기 전용으로 조사했다. 그 결과 기존 인벤토리 DTO의 존재, 개별 ID 필요성, 분해 확인 목록의 의미, 배열 순서, 16비트 수량 제한, 동적 골드 수령 시점, 비활성 합성, 튜토리얼 실습 재화 의존성을 본문에 반영했다.

문서 초안 자체도 같은 두 에이전트가 독립 검토했다. 다음 지적을 반영했으며, 이는 실제 서버 실행 검증이 아닌 설계 검토다.

| 지적 | 반영 |
|---|---|
| 부분 인벤토리 조회를 먼저 적용하면 늦은 변경 응답이 장비를 되돌릴 수 있음 | 독립 조회는 같은 revision만 사용하고 최신 revision은 전체 재조회. 오래된 전체 스냅샷과 다른 계정/세션 응답도 폐기 |
| 분해 ConfirmedCount의 양수·중복·overflow 검사 누락 | 양수/상한/중복/모드/원자적 산술 검증을 서버 절차에 추가 |
| 응답 오류도 커밋 이후 발생할 수 있어 타임아웃만 결과 미확정으로 보면 안 됨 | 실패 분기와 별개로 Rejected/Unknown을 정의하고 RequestId 복구 조회 추가 |
| 카테고리 일부 퀘스트 응답을 전체 상태로 교체할 수 있음 | 첫 계약은 전체 카테고리 조회, UI에서 필터 |
| 큰 스킬 조각 수량을 legacy 압축 필드에 항상 투영할 수 없음 | 새 K에서 상세 DTO 필수, 표현 불가 legacy 전체 목록 생략, 절삭/로그인 실패 금지 |
| 직업 해금과 선택을 순차 RPC로 분리하면 현 원자성이 깨짐 | 신규 OnSelectJob에 해금·선택·장비 해제를 한 번에 정의 |

검토 범위: 사용자 1~5 요구, 현재 코드와의 데이터 대응, 요청/응답 완결성, 오류·멱등성·적용 순서, 콘텐츠 누락과 수정 범위. 실제 서버 함수명·배포 상태·HTTP 400 전달 형태·공유 K·Android 실행은 여전히 미검증이다.

반영 후 두 에이전트가 각자 지적한 항목을 재검토했고 모두 해결되었음을 확인했다. 해당 재검토 범위에서 남은 지적은 없었다. 본문 코드 근거 36개 링크도 로컬 파일 존재를 확인했다.
