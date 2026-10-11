# 행위별 API 호출을 기준으로 한 클라이언트 서버 연동 계획

- 개정일: 2026-10-11 (KST)
- 백업 후 정정: 사용자 확인에 따라 로그인 서버 함수명을 OnLogIn으로 반영했다. 정정 전 통합 계획은 Git 이력에 보존한다.
- 상태: 사용자 검토용 실행계획. 이번 작업은 이 문서만 수정하며, 게임 코드와 DTO 소스 수정은 실행 승인 후 시작한다.
- 기준: 현재 작업 트리의 Server/DTO 및 콘텐츠 코드, 사용자의 최신 지시.
- 서버 구현 소스와 PlayFab 등록 목록은 확인하지 못했다. 이 문서의 ‘기존 API’는 DTO/호출 코드가 있다는 뜻이며 서버 배포·구현 완료 판정이 아니다.
- 이전 설계는 Git 이력에 보존한다. 이 문서의 현재 개정이 실행 기준이다.

## 1. 확정할 방향

RequestId, StateRevision, DefinitionVersion은 추가하지 않는다. 이를 대신하는 공통 요청 식별자·상태 순번·정의 버전도 만들지 않는다. 이전 계획의 공통 변경 영수증, 변경 이력 조회 API, 버전 비교를 통한 상태 적용 절차를 제거한다.

핵심 흐름은 다음과 같다.

~~~text
콘텐츠 행위 발생
  → NetworkManager의 해당 API 호출
  → PlayFab CloudScript
  → 서버가 조건 확인·비용 계산·결과 저장
  → 해당 ResponseDTO 반환
  → 클라이언트가 서버 확정값을 기존 모델에 반영
  → 기존 UI·이벤트·연출 갱신
~~~

1. ProtocolVersion은 서버/클라이언트가 공유하는 상수다. 정확히 일치해야 하며, 서버 응답값으로 클라이언트 상수를 덮어쓰지 않는다.
2. NetworkManager는 공통 호출·요청 헤더·응답 해석·오류 분리를 담당한다. 콘텐츠의 비용·확률·보상 확정은 서버가 담당한다.
3. 로그인 때 필요한 사용자 데이터를 로딩하고, 이후에는 행위별 응답으로 해당 콘텐츠와 함께 바뀐 재화·퀘스트 상태를 갱신한다.
4. 클라이언트에서 결과를 먼저 저장한 뒤 서버에 통보하는 방식으로 만들지 않는다. 전투 시뮬레이션·애니메이션은 유지하되 영구 재화·소유·강화·보상은 서버 응답으로 확정한다.
5. 기존 UI와 콘텐츠 규칙을 보존하는 DTO를 설계한다. 서버 연동을 위해서는 승인 후 호출부·응답 반영부·비동기 완료 처리를 수정해야 한다. C#을 전혀 수정하지 않은 채 로컬 저장 구조를 서버 구조로 바꾸는 것은 목표로 삼지 않는다.
6. 사냥 DTO에 이미 있는 RewardSequence/NextRewardSequence는 기존 사냥 배치 계약으로 유지한다. 다른 API의 공통 필드로 확대하지 않는다. 장비 InstanceId·퀘스트 PeriodKey·던전 BattleId는 각각 소유 장비·퀘스트 기간·실제 입장 회차를 구별하는 콘텐츠 데이터다.

## 2. 행위 → API → 서버 응답

요청은 가입/로그인 외에는 ProtocolVersion + SessionID를 공통 포함한다. 성공 응답은 기존 ApiResponseDTO를 상속하고 기능별 결과를 직접 담는다. 아래 신규 이름은 제안이며 실제 서버 등록명은 구현 단계에서 맞춘다.

| 발생 행위 | NetworkManager에서 호출할 API | 요청 핵심 | 서버가 처리하고 응답할 데이터 |
|---|---|---|---|
| 인증 성공 후 가입 | 기존 OnSignUpInitUser | InitUserRequestDTO | 사용자 생성·세션 발급, UserInitResponseDTO |
| 인증 성공 후 로그인 | OnLogIn | AuthRequestDTO | 세션 발급, 동일한 UserInitResponseDTO |
| 누적 사냥 결과 전송 | 기존 OnHuntReward | Stage, Loots, 기존 RewardSequence, 던전이면 BattleId | 확정 Level/Exp/KillScore, 재화 잔액, 실제 드롭/장비 상태, 퀘스트 상태, NextRewardSequence |
| 메인 스테이지 클리어 | 기존 OnStageClear | ClearedStage | 다음 스테이지·최고/최초 클리어 기록, 실제 최초 보상, 재화·장비·스킬·퀘스트 |
| 공격력/체력 골드 강화 | 기존 OnEnChantATK / OnEnChantHP | Count | 실제 강화 횟수, 최종 강화 레벨, 재화 잔액, 퀘스트 상태 |
| 장비 강화 | 기존 OnEnchantEquipment 확장 | InstanceId | 해당 장비 최종 강화 상태, 강화석 잔액·실제 지출, 퀘스트 상태 |
| 장비 뽑기 | 기존 OnGachaEquipmentClassFragment 확장 | Count: 1 또는 10 | 순서대로 뽑힌 보상, 보관/자동분해 결과, 천장, 장비·재화·퀘스트 |
| 스킬 뽑기 | 기존 OnGachaSkillArcaneKnowledge 확장 | Count: 1 또는 10 | 순서대로 뽑힌 보상, 중복 조각, 지식, 스킬·재화·퀘스트 |
| 스킬 강화/각성 | 기존 OnEnChantSkill / OnAwakeningSkill 확장 | 기존 SkillCode의 대상 ID | 변경 스킬, 재화 잔액, 퀘스트 상태 |
| 퀘스트 조건 달성 | 신규 OnClearQuest | QuestId, PeriodKey | 서버가 조건 확인 후 Claimable 상태 반환 |
| 업적 조건 달성 | 신규 OnClearAchievement | QuestId, PeriodKey | 같은 퀘스트 처리기로 조건 확인 후 Claimable 상태 반환 |
| 퀘스트/업적 보상 받기 | 신규 OnClaimQuestReward | QuestId, PeriodKey | 실제 지급 보상, 재화·장비·스킬·퀘스트 상태 |
| 던전 입장 | 신규 OnEnterDungeon | DungeonId | 입장 검증·티켓 차감, BattleId, 던전/티켓 상태 |
| 던전 클리어 | 신규 OnCompleteDungeon | BattleId, DungeonId | 완료 검증·최초 기록·실제 추가 보상, 결과창용 총보상, 던전·재화·퀘스트 상태 |
| 장비/스킬 장착·잠금·분해·전직 | 5~6절의 개별 API | 선택한 콘텐츠와 행위 인자 | 서버가 해당 행위를 처리한 뒤 변경된 콘텐츠와 재화를 반환 |

API 응답의 재화·레벨·인벤토리 상태는 최종값이다. GrantedRewards 같은 표시용 보상 목록을 받은 뒤 잔액에 다시 더하지 않는다. 한 행위가 여러 콘텐츠를 바꾸면 그 응답에 해당 데이터를 함께 담고 모두 검증한 뒤 한 번에 반영한다. 모든 응답에 전체 계정 데이터를 넣거나 매 행위 후 전체 재조회하지 않는다.

## 3. NetworkManager와 공통 RPC

### 3.1 공통 처리

호출 형태는 Call<TRequest, TResponse>(functionName, request, onSuccess, onFailure)로 통일한다. 기존 콘텐츠별 공개 메서드는 유지하거나 목적에 맞게 추가하고 공통 호출을 사용한다.

- ProtocolVersion: 공유 상수를 모든 요청에 넣고, 정상/오류 응답에서도 일치 여부를 검사한다. 불일치 시 게임 데이터 적용·메인 진입·후속 변경 호출을 중단하고 업데이트/지원되지 않는 클라이언트 안내로 연결한다. 서버에서도 업무 처리 전에 검사하고, 업데이트 불일치 또는 변조 의심 클라이언트를 판별하는 기준으로 사용한다.
- ServerVersion: 현재 ApiResponseDTO의 서버 릴리스 정보로 유지한다. 플레이어 상태 순번으로 사용하지 않는다.
- SessionID: 가입/로그인 응답에서 받은 서버 세션을 이후 요청에 사용한다. PlayFabId 및 PlayFab SessionTicket과 구분한다.
- 계정은 PlayFab 호출 컨텍스트에서 식별하고 SessionID를 검증한다. 클라이언트가 임의 계정 ID를 지정하지 않는다.
- 빈 목록과 필드 누락을 구별한다. 캐릭터 Index 0, 스킬 ID 0은 유효하므로 필수 필드 누락을 기본값 0으로 성공 처리하지 않는다.
- long/ulong 식별자와 재화는 64비트 정밀도를 보존한다. 특히 ItemCode/SkillCode를 중간에 double로 바꾸지 않으며 실제 SDK 경로로 왕복 검증한다.

### 3.2 두 종류의 실패

| 구분 | 발생 위치 | NetworkManager 처리 |
|---|---|---|
| PlayFab 통신/호출 실패 | SDK error callback의 PlayFabError | 통신·인증·호출 오류를 전달하고 성공 상태를 적용하지 않음 |
| PlayFab 호출 성공 후 응답 실패 | SDK success callback | ExecuteFunctionResult.Error, FunctionResult의 ErrorResponseDTO, 프로토콜 불일치·필수 데이터 오류를 검사하고 실패 사유 전달 |
| 성공 | SDK success callback에서 모든 검사 통과 | 기능별 TResponse로 해석해 콘텐츠 완료 콜백에 전달 |

두 번째 분기 안에서는 함수 실행 오류, 업무 오류 코드, 버전 불일치, 잘못된 응답을 구별해 알맞게 안내한다. ErrorCode가 있는 응답을 성공 DTO로 역직렬화해 기본값 상태를 적용하지 않는다.

업무 오류의 제안 계약은 FunctionResult에 기존 ErrorResponseDTO(ProtocolVersion, ServerVersion, ErrorCode, Message)를 반환하는 것이다. 현재 ErrorResponseDTO 주석은 HTTP 400을 가리키므로 실제 서버 반환 방식과 SDK에서 관측되는 위치를 확인해야 한다. HTTP 400 본문이 FunctionResult에 항상 들어온다고 가정하지 않는다. 이 확인 전에도 SDK error callback과 success callback 내부 오류의 분리는 구현할 수 있다.

### 3.3 대기·적용·재접속

- 재화/진행 상태를 바꾸는 API와 관련 상태 조회는 클라이언트에서 순서대로 호출·반영한다. 요청 대기 중 같은 버튼을 다시 누르면 추가 요청을 만들지 않는다. 이는 클라이언트의 대기 처리이며 새 전송 필드를 요구하지 않는다.
- 서버는 같은 플레이어의 한 행위에 해당하는 비용·보상·진행 기록을 함께 저장한다. 입력 조건 실패 시 해당 행위의 일부만 적용하지 않는다.
- 전투는 서버 응답을 기다리는 동안 다음 사냥 결과를 별도로 누적한다. 무제한 요청 중첩을 허용하지 않으며 장비 보관함 포화로 전투를 막지 않는다.
- 로그아웃/다른 계정 로그인 후 도착한 콜백은 호출 당시의 로컬 계정·세션과 비교해 폐기한다. 관련 조회를 변경 호출 사이에 병렬 실행해 오래된 값이 새 결과를 덮지 않게 한다.
- 가챠·강화 등 소비 요청은 응답을 잃었다고 자동으로 다시 보내지 않는다. 서버 현재 데이터를 다시 불러온 뒤 이용을 재개한다. 현재 상태 재조회는 실패한 호출의 과거 결과를 증명하거나 뽑기 연출을 정확히 복원하는 수단은 아니다.
- 임의의 소비 요청에 대한 정확히 한 번 실행/결과 재생을 이 계획이 보장한다고 설명하지 않는다. 퀘스트 수령 여부·던전 완료 기록·기존 사냥 순번은 각 콘텐츠의 저장 규칙으로 처리한다.

## 4. 인증 → 가입/로그인 → 데이터 로딩 → 메인

~~~text
PlayFab 인증 성공
  → 신규 계정: OnSignUpInitUser / 기존 계정: OnLogIn
  → UserInitResponseDTO 수신 및 검사
  → 필요한 서버 콘텐츠 정의 로딩
  → 사용자·캐릭터 3명·재화·장비·스킬·직업·퀘스트·던전 상태 적용
  → 데이터 준비 완료
  → 메인 진입
~~~

로그인 서버 함수명은 사용자 확인에 따라 OnLogIn으로 확정되었다. 클라이언트의 기존 로그인 메서드·호출부와 FunctionName을 OnLogIn으로 맞추고, AuthRequestDTO를 보내 UserInitResponseDTO를 받는다.

현재 Authentication은 RPC 완료를 기다리지 않고 별도의 PlayFab UserReadOnlyData를 해석해 메인으로 이어질 수 있다. TitleScreenController도 PlayFabId 존재만으로 준비 여부를 판단한다. 가입/로그인 응답 적용과 필수 데이터 로딩이 끝난 뒤에만 준비 완료를 알리는 구조로 바꾼다.

UserInitResponseDTO에 Inventory: List<ItemCode>는 이미 있다. 다만 현행 장비 기능을 보존하기에는 부족하므로 다음 상세 데이터를 포함하도록 확장한다.

| 초기 응답 데이터 | 필요한 내용 |
|---|---|
| 기존 사용자 정보 | SessionID, Level, Exp, KillScore, CurrentStage, EnchantHPCount, EnchantATKCount |
| 캐릭터 | Index 0/1/2가 각각 한 번씩 존재하는 CharacterDatas, 이름·직업·스탯 |
| 재화 | CurrencyQueryDTO 확장: 기존 재화와 EquipmentStone 등 클라이언트가 사용하는 잔액. 누락을 임의 0으로 처리하지 않음 |
| 장비 | EquipmentInventoryDTO: 주 보관함·추가 보관·초과 수량·장착·잠금·강화 비용 이력 |
| 마탑 | MageTowerStateDTO: 보유 스킬·강화·각성·조각·실제 지출·개화·5슬롯 |
| 직업 | 캐릭터별 선택 직업과 해금 트리. 장비의 CharacterIndex와 일치해야 함 |
| 뽑기 | GachaStateDTO: 장비 천장 진행값 |
| 퀘스트/업적 | QuestStateDTO: 전체 카테고리와 미수령 완료 항목·기간·초기화 시각 |
| 스테이지/던전 | 최고·최초 클리어 기록, DungeonStateDTO의 티켓·진행 기록·현재 입장 정보 |
| 기존 사냥 계약 | NextRewardSequence: 최초 사냥 배치에 사용할 값 |
| 시간 | ServerTimeUtc: 기간제 콘텐츠 표시/초기화의 기준 |

OnGetUserStateRequestDTO(공통 헤더만) / OnGetUserStateResponseDTO를 추가해 재접속·결과 미확인 상황에서 같은 데이터 집합을 조회한다. 이 조회는 세션을 새로 발급하지 않는다. 실제 로그인과 달리 재조회가 세션 필드를 덮거나 캐릭터를 중복 생성하지 않게 한다.

현재 UserManager/User/장비/마탑은 서버 자료를 로컬 JSON에 한 번 가져오는 경로가 있다. 로그인마다 서버 데이터로 갱신하고, 로컬 JSON이 소유·재화 상태를 다시 덮지 않게 한다. 기존 로컬 데이터의 서버 이관은 별도 정책이며 임의 업로드·병합하지 않는다. 같은 재화를 바꾸는 미전환 경로가 남아 있는 상태에서는 해당 재화의 서버 모드를 출시하지 않는다.

## 5. 장비: 현행 모델을 담는 계약

### 5.1 인벤토리 DTO

압축 ItemCode는 장비 종류/강화/수량을 표현하지만 동일 종류의 개별 소유 장비를 구별하지 못한다. Equality도 종류 기준이다. 클라이언트가 이미 사용하는 EquipmentSave의 개별 ID와 상태를 그대로 전송할 수 있어야 한다.

~~~text
EquipmentInstanceDTO
  InstanceId: string
  DefinitionId: int
  EnhancementLevel: int
  CharacterIndex: int?             // 0~2 또는 미장착
  IsLocked: bool
  ExpiresUtc: long
  EnhancementStonesSpent: long

EquipmentReserveStackDTO
  DefinitionId: int
  EnhancementLevel: int
  Count: int

EquipmentInventoryDTO
  Items: EquipmentInstanceDTO[]
  PendingItems: EquipmentInstanceDTO[]
  ReserveStacks: EquipmentReserveStackDTO[]
  Capacity: int
  PendingCapacity: int
  AutoDismantleRarityMask: int
~~~

EquipmentSave의 Id/Code/Level/Player/Locked/ExpiresUtc/EnhancementStonesSpent에 손실 없이 매핑한다. 배열 순서는 기존 ‘최근 획득’ 정렬을 보존한다. 장착 정보의 기준은 InstanceId + CharacterIndex이며 기존 CharacterEquipmentDatas와 함께 제공한다면 반드시 일치시킨다.

새 공통 ProtocolVersion에 해당하는 클라이언트는 상세 인벤토리를 사용한다. 기존 압축 Inventory를 유지하더라도 상세 장비와 합쳐 중복 생성하지 않는다. 기존 저장분의 개별 ID와 강화 지출을 서버가 보관하지 않았다면 복구 가능한 근거와 이관 규칙을 먼저 정하며, 과거 지출을 현재 비용으로 임의 복원하지 않는다.

### 5.2 행위별 RequestDTO / ResponseDTO

아래 각 함수 이름에 RequestDTO/ResponseDTO를 붙여 계약을 정의한다. 성공 응답에는 표의 결과와 영향을 받은 재화·퀘스트를 담는다.

| API | 요청 | 응답과 서버 처리 |
|---|---|---|
| OnGetEquipmentInventory | 공통 헤더 | EquipmentInventoryDTO. 메뉴 최초 표시/명시적 새로고침에 사용 |
| OnEnchantEquipment | InstanceId | 주 보관함의 소유 개체 확인 → 강화석 비용·장비 정의별 상한 확인 → 강화. 장비 목록 최종 상태, Currency, 실제 소비량 |
| OnEquipEquipment | InstanceId, CharacterIndex | 주 보관함의 미장착 개체·직업 적합성 확인 → 해당 캐릭터의 기존 장비 해제와 새 장착을 한 번에 저장. 장비 최종 상태. 다른 캐릭터에게 장착 중인 장비를 강제로 이전하지 않음 |
| OnUnequipEquipment | CharacterIndex, InstanceId | 현재 장착 대상 확인 후 해제. 장비 최종 상태 |
| OnSetEquipmentLock | InstanceId, IsLocked | 대상 잠금 변경. 장비 최종 상태 |
| OnDismantleEquipment | Mode, Items[], ReserveStacks[] | 확정 목록 검증 → 모두 분해 → 강화석 지급. 장비·Currency·지급 강화석 |
| OnSetAutoDismantle | RarityMask | 이후 획득에 적용할 설정 저장. 기존 소유 장비를 즉시 분해하지 않음 |
| OnClaimPendingEquipment | InstanceId | 주 보관함으로 이동. 장비 최종 상태 |
| OnClaimReserveEquipment | DefinitionId, EnhancementLevel | 해당 스택에서 1개를 주 보관함 개체로 변환. 장비 최종 상태 |

분해 요청의 Items[]는 확인창을 열 때 선택한 InstanceId, DefinitionId, EnhancementLevel, EnhancementStonesSpent, 보관 위치를 담는다. ReserveStacks[]는 DefinitionId, EnhancementLevel, Count를 담는다. 서버는 표시 당시 선택한 대상만 분해하고, 이후 추가로 획득한 장비를 포함하지 않는다.

같은 대상의 잠금/장착/강화/보관 위치가 달라졌거나 삭제되었다면 전체 요청을 거절하고 새 목록을 보여준다. 초과 스택이 늘었다면 확인한 Count만 소비한다. 양수 수량·중복 ID/스택 키·상한·산술 overflow를 검사한다. 이는 삭제 대상 확인에 필요한 값이며 모든 요청에 공통 비교 필드를 추가하는 설계가 아니다.

### 5.3 서버가 보존할 장비 규칙

- 주 보관 300, 추가 보관 100 및 초과 수량 보존. 포화 때문에 사냥·던전·재화가 충분한 뽑기를 실패시키지 않는다. 현재 추가 보관은 무기한이며 ExpiresUtc=0을 사용하고 서버가 임의 만료·삭제하지 않는다.
- 강화는 강화석을 소비하며 확정 성공이다. 장비 정의의 maxEnhancementLevel을 적용하고, 현재 정의는 일반 +5/희귀 +10/영웅 +15다. 기본 분해량은 일반 1/희귀 4/영웅 12, 강화 비용은 2 × 기본 분해량 × (1 + floor(현재 강화/3)).
- 분해 보상은 기본 분해량 + floor(실제 사용 강화석 × 0.8). 단일 분해는 강화품도 가능하고 일괄 분해는 +0만 대상으로 하며 잠금/장착품은 보호한다.
- 자동 분해는 설정에 해당하는 신규 +0·미잠금·미장착 장비에 적용한다. 소유 내역과 실제 보관/전환 결과를 응답한다.
- 스택은 개별 잠금·장착·지출을 담을 수 없다. 일반 신규 보상을 넘어 기존 특수 상태의 개체까지 수량으로 뭉개지 않는다.
- 활은 신규 드롭/뽑기에서 제외하며 기존 소유분은 보존한다.
- TrySynthesize는 현재 비활성 경로이므로 합성 API를 추가하지 않는다.

## 6. 캐릭터 강화·마탑·직업·가챠

### 6.1 캐릭터 강화

StatEnhanceManager의 AttackLevel/HealthLevel은 계정 공통이며 캐릭터 3명에게 함께 적용된다. 현재 함수별 OnEnChantATK/OnEnChantHP를 유지하고 개별 캐릭터 Index를 추가하지 않는다.

OnEnchantRequestDTO.Count는 현 UI의 1/10/MAX를 보존한다. MAX는 기존 클라이언트 의미인 -1로 계약을 명시하고 서버가 현재 잔액으로 가능한 횟수를 계산한다. 양수 요청은 상한까지 남은 횟수로 제한하되 그 비용이 부족하면 실패한다. 현재 상한 300과 BalanceMath의 비용식을 서버로 옮긴다.

OnEnchantResponseDTO의 CurrentLevel/CurrentGold는 절대값으로 적용하며 AppliedCount와 영향을 받은 재화/퀘스트 상태를 보완한다. 서버 성공 시 기존 OnEnhanceCompleted/OnEnhanced로 UI를 갱신하고 3명의 스탯을 적용한다. 응답 전 bool 성공을 반환해 완료 연출·가이드 성공을 확정하지 않는다.

### 6.2 마탑 스킬

~~~text
MageSkillDTO
  SkillId: int
  EnhancementLevel: int
  AwakeningLevel: int
  Fragments: int
  KnowledgeSpent: long
  BloomEnabled: bool

MageTowerStateDTO
  Skills: MageSkillDTO[]
  EquippedSkillIds: int[5]          // 빈 슬롯 -1
~~~

기존 SkillCode만으로는 실제 지식 지출·개화 설정·슬롯을 보존할 수 없고 16비트 수량은 큰 조각 수량을 담지 못한다. 상세 DTO를 기존 MageSave/슬롯에 매핑한다. 구 압축 목록으로 재변환하며 조각을 잘라내지 않는다.

| API | 요청 | 응답/서버 처리 |
|---|---|---|
| 기존 OnEnChantSkill | 기존 SkillCode로 대상 식별 | 현재 서버 레벨에서 강화 비용 계산, MageTowerStateDTO·Currency·퀘스트 |
| 기존 OnAwakeningSkill | 기존 SkillCode로 대상 식별 | 서버 보유 조각 차감·각성, MageTowerStateDTO·Currency·퀘스트 |
| 신규 OnSetSkillLoadout | EquippedSkillIds[5] | 보유/중복/비활성 ID 검사 후 5슬롯을 함께 변경, MageTowerStateDTO |
| 신규 OnSetSkillBloom | SkillId, Enabled | 각성 조건 검사 후 개화 설정, MageTowerStateDTO |
| 신규 OnResetSkillEnhancement | SkillId | 실제 지출의 80% 지식 환급, 강화/지출 초기화, MageTowerStateDTO·Currency·환급량 |

강화 상한 100, 각성 상한 10, 중복 획득 조각 30, 각성 비용은 다음 각성 단계의 수량, 각성 10에서 개화를 허용하는 현행 규칙을 보존한다. 초기화는 각성·조각·장착·개화를 지우지 않는다. 이미 진행 중인 시전/쿨다운은 서버 응답 적용으로 초기화하지 않는다.

활성 로스터는 현재 클라이언트를 따른다. ID 6/8은 재사용/신규 지급하지 않고, ID 8→3 이관 시 높은 투자 상태·환급·원본 내역을 보존한다. 폐기된 신 스킬 시스템은 대상이 아니다.

### 6.3 직업

기존 OnGetJob/OnChangeJob 요청의 Index 누락 및 잘못된 요청 DTO 사용을 고친다. 현 ChangeJob.TryChangeJob은 미해금 직업의 해금·재화 차감·선택·비호환 장비 해제를 한 번에 수행한다.

이 버튼에는 신규 OnSelectJobRequestDTO(Index, JobCode) / OnSelectJobResponseDTO를 제안한다. 서버가 선행 직업·비용·사용 가능 여부를 확인해 필요한 해금과 선택을 함께 저장하고 CharacterJobs, JobTrees, Equipment, Currency, Quests를 반환한다. 기존 두 API는 서버 등록 현황에 맞춰 유지할 수 있지만 한 번의 전직을 두 번의 독립 소비 RPC로 나누지 않는다.

기본/상위 전직 비용 40/120, 기존 웨이브 경계에서 실제 전직을 적용하는 시점, 궁수/정예 궁수 비활성 정책을 보존한다.

### 6.4 장비/스킬 뽑기

서버가 비용 차감·난수 추첨·장비 천장·중복 변환·보관/자동 분해·퀘스트 증가를 함께 처리한다. 클라이언트는 뽑을 보상을 결정해서 보내지 않는다.

~~~text
GachaRewardDTO
  Order: int
  Kind: Equipment / Skill / Currency
  DefinitionId: int?               // 장비/스킬 ID
  CurrencyType: eCurrency?
  Amount: long
  IsDuplicate: bool
  GrantedFragments: int
  InstanceId: string?              // 실제 개별 장비가 만들어진 경우
  Disposition: Inventory / Pending / Reserve / AutoDismantled / None
  GrantedEquipmentStone: long

GachaStateDTO
  EquipmentPity: int
~~~

기존 각 가챠 ResponseDTO에 Rewards[], Currency, GachaState, 해당 Equipment/MageTowerState, Quests를 보완한다. Rewards는 연출용 결과이며 최종 상태의 재화/소유를 다시 가산하지 않는다. 기존 GachaList/Cnt만으로 결과 순서·중복·최종 잔액을 추정하지 않는다.

현행 규칙은 1/10회, 회당 고대주화 50이다. 장비는 일반 70%/희귀 20%/영웅 5%/직업조각 2개 5%, 장비 천장 40번째 영웅 보장과 영웅 획득 시 초기화를 보존한다. 스킬은 총 50%를 활성 8종에 균등 분배하며 나머지는 지식 10/20/50개가 각각 30%/15%/5%다. 스킬에 희귀도를 새로 추가하지 않는다.

## 7. 사냥·메인 클리어·던전

### 7.1 사냥

StageManager에는 3초 주기 SendHuntResultLoop가 정의되어 있지만 시작 호출이 검색되지 않는다. 또한 현재 LocalProgression.IsLocalAuthority는 항상 true이고 서버 요청 직후 집계 버퍼를 지우며, UserManager.SetHuntResult는 응답을 적용하지 않는다.

승인 후 다음 흐름을 연결한다.

1. 로그인 데이터 적용 후 집계를 시작하고 로그아웃/종료 때 정리한다.
2. Stage별 결과를 분리하고 전송할 배치를 복사해 보관한다. 다음 처치는 별도 버퍼에 쌓는다.
3. 기존 OnRewardRequestDTO의 Stage/Loots/RewardSequence를 모두 채운다. 던전 배치에는 입장 응답의 BattleId도 전달하고 회차별로 집계한다. short Count 범위와 서버의 배치 총량 제한에 맞게 누적을 분할하며 잘라내거나 overflow시키지 않는다.
4. 서버가 스테이지·처치 정보·기존 사냥 순번 및 던전 회차를 검사하고 경험치·골드·드롭·퀘스트를 계산한다. 클라이언트가 계산한 보상량을 신뢰하지 않는다.
5. 정상 응답의 확정값과 NextRewardSequence를 반영한 뒤 그 배치만 제거한다. 기존 서버 중복 배치 처리가 어떤 응답을 반환하는지 공유 계약 단계에서 확인한다.
6. 클리어 직전 해당 스테이지의 남은 사냥 배치를 먼저 처리해 클리어 보상과 순서가 뒤섞이지 않게 한다. 던전 실패·중도 종료·다음 입장 전에도 이전 회차의 남은 배치를 먼저 처리한다.

현재 서버 CommonDatas의 주석은 배치 총처치 150 이상 거절과 수락 간격 2초 이하 거절을 나타낸다. 실제 서버 적용 여부를 확인해 총량·간격 규칙을 맞춘다. 마지막 배치도 직전 전송 직후 즉시 보내지 않고 허용 간격 이후 처리한 다음 클리어/종료 요청을 보낸다. 제한 때문에 유효한 처치·보상을 버리지 않는다. 정상 전투 속도를 감당하지 못하는 제한이면 서버 검증 기준을 먼저 조정하고 통합한다.

OnHuntResponseDTO는 기존 Level/Exp/KillScore/Gold/AncientCoin/NextRewardSequence를 유지하면서 실제 필드 드롭 목록, 영향을 받은 장비 상태, 퀘스트 상태를 보완한다. Currency 전체를 함께 사용한다면 기존 Gold/AncientCoin과 반드시 일치시키고 한 경로로만 적용한다.

전투 시간/스킬 시전 횟수 목표에는 같은 사냥 배치에 Activity(전투 시간, 스킬별 시전 수)를 추가하는 안을 사용한다. 기존 사냥 순번이 이 배치 전체에 적용되며 별도의 활동 ID/순번 시스템을 만들지 않는다. 처치 없이 활동만 있는 배치를 허용하고, 서버는 경과시간·장착·쿨다운과 유효 기간으로 검증한다. 기간 경계를 넘는 자료는 기간별로 분리하고 클라이언트의 임의 진행도·완료 bool을 그대로 승인하지 않는다. 정확한 시간 구간 필드와 검증 허용 오차는 서버 계약 단계에서 확정한다.

### 7.2 메인 클리어

OnStageClear는 ClearedStage와 FunctionParameter를 빠짐없이 전송하도록 수정한다. 현재 응답의 Stage만으로는 최초 보상/해금/최고 기록을 보존할 수 없으므로 실제 지급 보상, 갱신된 진행 기록, 관련 재화·장비·마탑·퀘스트 상태를 보완한다.

서버는 최초 지급 여부를 저장한다. 다음 스테이지는 현행 3장 이후 반복 지역과 증가하는 번호/난도를 보존하고 기존 안정적인 Stage ID를 사용한다. 서버 DTO에 열거된 1~2장 값만을 전체 지원 범위로 가정하지 않는다.

### 7.3 던전 입장·클리어·종료

던전 클리어만 서버에 알리면 현재 입장 티켓 차감/최초 기록과 연결되지 않는다. 입장과 종료 API를 함께 설계한다.

~~~text
DungeonStateDTO
  GoldTickets: int
  RubyTickets: int
  TicketResetUtc: long
  GoldDungeonClear: int
  RubyDungeonClear: int
  ActiveBattleId: string?          // 실제 던전 입장 회차
  ActiveDungeonId: long?

OnEnterDungeonRequestDTO: DungeonId
OnEnterDungeonResponseDTO: BattleId, DungeonState, Quests

OnCompleteDungeonRequestDTO: BattleId, DungeonId
OnCompleteDungeonResponseDTO:
  DungeonState, IsFirstClear, GrantedRewards, TotalRunRewards,
  Currency, Quests, 필요시 Equipment/MageTowerState

OnExitDungeonRequestDTO: BattleId, Reason(Failed/Abandoned)
OnExitDungeonResponseDTO: DungeonState, Quests
~~~

요청/응답의 공통 부모 필드는 3절과 같다. 난도는 현행 StageDefinition의 DungeonId로 식별한다. BattleId는 현재 클라이언트의 StageSession.RunId에 대응하는 실제 입장 회차이며 서버가 발급해 저장한다.

서버는 입장 가능 난도·티켓·진행 중 회차를 확인하고 입장 시 티켓을 차감한다. 같은 회차 완료/종료 기록으로 중복 지급을 막으며 클리어·실패·중도 종료 전에 해당 BattleId의 마지막 사냥 배치를 확인한다. 미정산 상태에서 다음 입장 회차로 넘기지 않는다. 실패/중도 종료에도 종료 전 정상 획득한 처치 보상은 보존하고, 클리어 보상은 지급하지 않는다. 티켓은 현 입장 차감 정책을 유지한다.

골드 던전은 현재 처치 때 골드를 지급하고 클리어에서는 LastDungeonGold를 결과창에 표시한다. OnCompleteDungeon에서 같은 골드를 다시 지급하지 않는다. 루비 던전의 종료 루비·최초 추가 보상은 서버가 클리어 때 지급한다. 응답은 이번 호출이 새로 지급한 GrantedRewards와 이미 받은 금액을 포함하는 결과창용 TotalRunRewards를 구별한다.

## 8. 퀘스트·업적: 완료와 수령을 구별

현재 Guide/Daily/Weekly/Achievement는 같은 QuestEconomy/QuestContracts 구조를 사용한다. 목표 달성은 Claimable로 바뀌는 것이고, 실제 보상은 보상 받기를 눌렀을 때 지급한다. 사용자 표현의 ‘클리어 API’를 추가하면서 이 동작을 자동 수령으로 변경하지 않는다.

| API 및 DTO | 요청 | 응답/처리 |
|---|---|---|
| OnGetQuestStateRequestDTO / ResponseDTO | 공통 헤더 | 모든 카테고리의 QuestStateDTO |
| OnClearQuestRequestDTO / ResponseDTO | QuestId, PeriodKey | 서버 누적값·현재 상태로 조건 확인, 전체 QuestStateDTO |
| OnClearAchievementRequestDTO / ResponseDTO | QuestId, PeriodKey | 같은 검증 처리기에서 Achievement 카테고리 확인, 전체 QuestStateDTO |
| OnClaimQuestRewardRequestDTO / ResponseDTO | QuestId, PeriodKey | 조건·미수령·기한 확인, GrantedRewards + 최종 Currency/Quests와 필요한 Equipment/MageTowerState |

강화·뽑기·사냥·던전 API 자체가 해당 퀘스트의 진행값도 갱신한다. 클리어 API는 이를 다시 증가시키지 않고 완료 상태를 확인·확정한다. 이미 완료된 퀘스트의 클리어 재호출은 보상을 추가 지급하지 않는다. 업적 수령도 같은 OnClaimQuestReward를 사용한다.

~~~text
QuestRewardDTO
  Kind, CurrencyType/DefinitionId
  Amount: decimal
  IsDynamicGold: bool              // 기존 '2분 골드' 등의 표시와 고정 금액 구별

QuestRowDTO
  QuestId: long
  PeriodKey: string
  Category: Guide / Daily / Weekly / Achievement
  Title, Description
  PresentationType
  ObjectiveType, TargetId, RequiredCount, Progress
  State: Locked / InProgress / Claimable / Claimed
  IsPending: bool
  ExpiresUtc: long
  Rewards: QuestRewardDTO[]

QuestStateDTO
  ServerTimeUtc: long
  DailyPeriodKey, DailyResetUtc
  WeeklyPeriodKey, WeeklyResetUtc
  Rows: QuestRowDTO[]               // 미수령 보관 항목을 포함한 전체 카테고리
~~~

기존 QuestRowSnapshot/QuestBoardSnapshot에 변환해 UI를 유지한다. 고정 골드로 이관된 미수령 항목은 IsDynamicGold=false와 소수 정밀도를 보존한 Amount로 표시하고, 서버가 보관하는 원래 고정 금액·소수 잔여 처리도 유지한다. 실제 지급한 GrantedRewards는 동적 표시가 아닌 확정 수량이다. 로컬 AccountGeneration은 이전 계정 콜백 차단용으로만 사용한다. 일부 카테고리 응답을 전체 목록으로 덮어쓰는 문제가 없도록 첫 계약은 전체 목록 반환으로 통일한다.

서버 구현에는 다음 현행 규칙을 옮긴다.

- KST 일일 자정/월요일 주간 초기화, 메인 1-11 이후 일일·주간 해금.
- 가이드 순서·현재값 목표·누적 목표·완료 상태 고정·업적 최고 기록.
- 완료 미수령 항목은 해당 기간 종료 후 7일까지 유지하며 당시 제목·설명·보상 정의를 직접 보존한다.
- 일반 동적 골드 보상은 수령 시점의 서버 상태로 계산한다. 기존 고정 골드로 저장된 항목만 고정값을 유지한다.
- 인증 계정 + QuestId + PeriodKey의 수령 여부를 저장해 같은 퀘스트 보상을 두 번 지급하지 않는다.
- 서버가 직접 관찰하는 행동은 해당 API에서 진행도를 계산하고, 전투 시간/시전 횟수는 7.1의 검증된 활동 자료로 누적한다.
- 수령 결과로 다음 가이드/업적/전체 완료 목표가 달라지면 같은 응답에 최종 상태를 반환한다.

## 9. 서버에서 받아야 할 정의와 미지원 콘텐츠

### 9.1 정의 데이터와 사용자 데이터

사용자 소유·진행 상태는 로그인과 행위별 응답으로 받는다. 장비 능력치·강화 비용·직업 조건·스킬 정의·퀘스트 설명/보상표·가챠 표시 확률 같은 정의 데이터도 서버가 제공한 현재 자료를 사용한다.

신규 OnGetContentDefinitionsRequestDTO는 공통 헤더만 갖고, OnGetContentDefinitionsResponseDTO는 장비/마탑/직업/퀘스트/가챠의 정의를 담는다. 기존 JSON 로더가 요구하는 필드·안정적인 ID를 유지해 기존 표시 모델로 변환한다. 첫 구현에서는 필요한 전체 정의를 로그인 로딩 단계에서 내려받는다. 버전·해시·NotModified·차등 갱신 필드를 추가하지 않는다.

이미지/프리팹 등 에셋은 기존 로컬/CDN 로딩 경로를 재사용한다. 서버 정의에는 기존 클라이언트 에셋을 찾는 안정적인 키를 둔다. CDN/자동화 시스템 개편까지 이번 범위에 자동 포함하지 않는다. 정의 로딩 실패 시 로컬 기본 데이터로 임의 보상 지급을 시작하지 않는다.

### 9.2 현재 DTO와 부족한 기능

| 기능 | 현재 확인 상태 | 계획 |
|---|---|---|
| 로그인·사냥·메인 클리어·골드 강화 | 기본 DTO 있음, 호출/응답 반영 누락 | 2~4·6.1·7절 연결 |
| 장비 인벤토리 | 압축 목록 있음 | 개별 소유·장착·잠금·보관·비용 이력 확장 |
| 장비 강화 | API 있음, 개별 대상/강화석 부족 | 기존 API 확장 |
| 장비 장착·해제·잠금·분해·자동분해·보관 회수 | 대응 DTO 없음 | 5절 신규 API |
| 스킬 강화·각성 | 기본 API 있음 | 상세 상태·재화·현행 로스터 지원 |
| 스킬 장착·개화·강화 초기화 | 대응 DTO 없음 | 6.2절 신규 API |
| 직업 | 해금/변경 기본 API 있음 | Index 보완, 한 번의 전직 처리에 맞춘 계약 |
| 장비/스킬 가챠 | 기본 API 있음 | 서버 추첨·비용·천장·순서·중복·최종 상태 확장 |
| 가이드/일일/주간/업적 | 대응 DTO 없음 | 8절 신규 API |
| 던전 입장·종료·클리어 | 대응 DTO 없음 | 7.3절 신규 API |
| 필드 드롭·최초 장비/스킬 지급 | 사냥/클리어 DTO에 부족 | 기존 보상 API에 실제 결과 포함 |
| 루비 영구 성장/초기화·환생·오프라인 보상 | 전용 DTO 없음 | 아래 후속 계약 제안, 코드 수정 범위에는 자동 포함하지 않음 |
| 튜토리얼 실습 재화/완료 처리 | 대응 DTO 없음 | 서버 가챠·강화 연결의 선행 의존성으로 따로 명시 |

추가 콘텐츠의 계약 방향도 기록하되 해당 콘텐츠 코드는 별도 범위 승인 전 수정하지 않는다.

| 후속 DTO/API 제안 | 요청 | 응답과 필요한 서버 로직 |
|---|---|---|
| OnEnhanceRubyGrowth | 성장 종류, 횟수 | 서버 비용·상한 계산, 최종 성장/재화 |
| OnResetRubyGrowth | 성장 종류 | 현행 웨이브 경계 예약·실제 지출 기반 환급·일일 제한 상태 |
| OnRequestReincarnation | 현재 진행 단계에 대한 실행 요청 | 조건 확인·경계 적용 대기 상태. 확정 후 골드/골드 강화/메인 진행 초기화와 보존 상태 |
| OnGetOfflineReward / OnClaimOfflineReward | 공통 헤더 | 서버 미수령 시간 구간·속도 근거로 계산한 골드/경험치. 수령 시 기준 시각 갱신 |
| OnClaimGuidePracticeCoins | 현 가이드 단계·행동 | 현재 단계와 일회 지급 여부를 검사해 실습 재화와 최종 진행 상태 반환 |

각 후속 API의 RequestDTO/ResponseDTO는 위 내용을 기능별로 담는 형태다. 범용 변경 봉투나 복구 API를 덧붙이지 않는다. 튜토리얼에서 제공하는 실습 고대주화 50과 실제 가챠/강화 성공 처리를 서버와 연결하지 않으면 신규 가입 진행이 막히므로 통합 전에 범위를 확정해야 한다.

공유 재화를 사용하는 경로는 하나의 확정 주체를 가져야 한다. 예를 들어 오프라인 보상·환생이 로컬 골드를 바꾸는 상태에서 서버 골드 스냅샷으로 덮는 혼합 운영을 출시 완료로 간주하지 않는다. 범위 밖 기능을 몰래 수정하거나 비활성화하지 않고, 필요한 연결을 승인받아 전환한 뒤 통합을 완료한다.

## 10. 승인 후 실행 순서와 검증

### 단계 1 — 공유 DTO와 실제 함수 계약 확정

- 현재 변경된 RequestDTO/ResponseDTO/QueryDTO를 기준으로 작업하며 사용자 변경을 보존한다.
- ProtocolVersion 상수의 실제 값·공유 위치, 서버 등록 FunctionName, 정상/오류 JSON 샘플을 확인한다.
- 기존 ApiVersions 미정의, namespace 차이, List using, Unity C# 9에 맞지 않는 파일 범위 namespace 등 컴파일 연결 문제를 대상 서버 코드 안에서 정리한다.
- 재화 enum의 Ruby/EquipmentStone, 스킬 로스터, 확장 스테이지 ID를 현행 클라이언트와 맞춘다. 서버 DTO 상한 9를 근거로 클라이언트의 장비 정의별 +5/+10/+15 및 스킬 +100 규칙을 변경하지 않는다.
- 상세 장비/마탑/퀘스트/던전/가챠 DTO와 필요한 정의 로딩 DTO를 추가한다. 구 압축 데이터와 새 상세 데이터의 적용 기준을 하나로 정한다.

완료 기준: 행위별 요청/응답 샘플과 실제 등록명이 정해지고, 필수 데이터가 기존 클라이언트 모델에 손실 없이 대응한다.

### 단계 2 — NetworkManager 공통화와 로그인 진입

- 요청마다 헤더 설정, 실제 FunctionParameter 전달, 두 종류의 오류 분리, 기능별 성공 콜백을 구현한다.
- 가입/로그인 → 데이터/정의 로딩 → 모델 적용 → 준비 완료 → 메인 진입을 연결한다.
- 상태 적용 경로와 순차 호출/중복 클릭 대기 처리를 마련하고 재접속 조회를 연결한다.

완료 기준: 인증만 성공한 상태에서는 메인에 진입하지 않으며, 재로그인하면 캐릭터 3명과 서버 사용자 상태가 정확히 복원된다.

### 단계 3 — 기존 API에 해당하는 행위 연결

- 사냥 집계와 메인 클리어, 공격력/체력 강화.
- 장비 강화, 스킬 강화/각성, 장비/스킬 가챠, 직업 선택.
- 각 기능의 로컬 확정 지점을 서버 성공 응답 반영으로 전환하고 기존 UI·비용 표시·완료 이벤트를 연결한다.
- 현행 동기 bool 반환을 즉시 성공으로 오해하는 호출부를 필요한 범위에서 비동기 완료에 연결한다.

완료 기준: 한 번의 행위가 해당 API를 호출하고, 서버 오류에서는 비용/보상을 클라이언트가 임의 확정하지 않으며, 성공 후 재로그인해도 상태가 같다.

### 단계 4 — 신규 API 기능 연결

- 상세 인벤토리 조회/장착/잠금/분해/초과 회수.
- 마탑 슬롯/개화/강화 초기화.
- 퀘스트·업적 완료/수령, 던전 입장/클리어/종료.
- 서버 구현은 이 문서의 콘텐츠별 규칙을 이관하고, 같은 행위의 비용·소유·진행 변경을 함께 저장한다.
- 서버 저장소가 이 작업 공간에 없으므로 이 문서 전달과 서버 실제 구현 완료를 구별한다. 필요한 서버 API가 준비되기 전에는 임의 성공 처리로 연결 완료를 표시하지 않는다.

완료 기준: 기존 클라이언트에서 제공하던 대상 기능과 보상 규칙이 서버 데이터를 사용해 동작한다.

### 단계 5 — 공용 재화 의존성 확인 및 통합 검증

- 9.2절의 범위 밖 의존성을 실제 사용 경로별로 확인해 필요한 추가 수정 범위를 제시한다.
- 서버 응답 예제로 DTO 검증 후 지정 Unity Editor에서 import·컴파일·참조·실행을 검증한다.
- 큰 변경 묶음 완료 시 연결 Android 기기에서 실제 로그인부터 전투·메뉴·재접속까지 확인한다. 현재 문서 작업에서는 Unity/Android 실행 검증을 수행하지 않는다.

주요 검증 시나리오:

1. ProtocolVersion 불일치, PlayFab 실패, FunctionResult 업무 오류, ExecuteFunctionResult.Error, 필수 필드 누락이 서로 올바르게 분기되는지.
2. 가입/로그인 중복 진입 방지, 캐릭터 0/1/2 검증, 데이터 로딩 실패, 로그아웃 후 늦은 응답 폐기.
3. 같은 종류 장비 여러 개·잠금/장착·강화 지출·장비별 상한·주 보관함만 강화/장착 가능·추가 보관 무기한 유지·보관 포화/초과 수량 보존.
4. 사냥 전송 중 추가 처치, 스테이지 이동, short 및 서버 총량 범위 분할, 최소 전송 간격과 마지막 배치, 기존 사냥 순번 재전송 처리, 드롭/퀘스트·재화 이중 가산 방지.
5. 캐릭터 강화 1/10/MAX, 상한 근처, 잔액 부족, 3명 반영, 중복 클릭.
6. 가챠 1/10회·천장·스킬 균등·중복 조각·자동 분해, 소비 요청 응답 유실 후 자동 재소비 없이 재조회.
7. 분해 확인 후 잠금/강화/소유 변화, 음수/중복 수량 거절, 확인 이후 신규 획득분 보존.
8. 스킬 ID 0, 65,535를 넘는 조각, 슬롯 교환, 개화/초기화, 비활성 로스터 이관, 전직 후 장비 해제.
9. 퀘스트/업적 완료와 수령 구분, 중복 수령, KST 기간 경계, 7일 미수령 보관, 수령 시 동적 골드.
10. 던전 티켓·입장 회차별 사냥·실패/중도 종료 전 정산·최초 보상·동일 회차 완료 중복, 골드 던전 보상 이중 지급 방지.
11. 서버 확정 이후 로컬 JSON이 상태를 되돌리지 않는지와 공용 재화의 미전환 지급 경로.
12. 실제 PlayFab 경로에서 64비트 정밀도, 인벤토리/정의의 최대 응답 크기, 사냥 주기와 순차 호출의 지연. 한도가 부족하면 해당 데이터 조회만 분리하며 새로운 버전 필드를 추가하지 않는다.

승인 대상 코드 범위는 Server/DTO 및 관련 서버 enum/상수, NetworkManager, 인증·초기 진입 연결부, 사용자 상태 매핑, 사용자가 언급한 장비·마탑·직업·가챠·퀘스트/업적·사냥·골드 강화·던전의 API 호출/반영부다. 공용 파일은 해당 경로만 수정하며 전투 연출·무관한 콘텐츠·외부 에셋·CDN·자동화·프로젝트 전역 설정은 수정 범위에 포함하지 않는다. 실제로 필요한 최소 UI 호출부 변경은 서버 완료 대기/오류 안내/기존 갱신 이벤트 연결에 한정한다.

## 11. 코드 근거

아래 링크는 현재 동작의 출처다. 기존 문서의 과거 설명보다 실제 구현을 우선했다.

- [RequestDTO](../../Assets/_Project/Scripts/Server/DTO/RequestDTO.cs), [ResponseDTO](../../Assets/_Project/Scripts/Server/DTO/ResponseDTO.cs), [QueryDTO](../../Assets/_Project/Scripts/Server/DTO/QueryDTO.cs), [ProtocolDTO](../../Assets/_Project/Scripts/Server/DTO/ProtocolDTO.cs), [ErrorResponseDTO](../../Assets/_Project/Scripts/Server/DTO/ErrorResponseDTO.cs)
- [NetworkManager](../../Assets/_Project/Scripts/Core/Manager/NetworkManager.cs), [Authentication](../../Assets/_Project/Scripts/Server/Auth/Authentication.cs), [UserManager](../../Assets/_Project/Scripts/Core/Manager/UserManager.cs), [TitleScreenController](../../Assets/UGUI/Scripts/Views/TitleScreenController.cs)
- [ProgressionState](../../Assets/_Project/Scripts/Core/Balance/ProgressionState.cs), [LocalProgression](../../Assets/_Project/Scripts/Core/Balance/LocalProgression.cs), [Wallet](../../Assets/_Project/Scripts/Wallet/Wallet.cs)
- [EquipmentManager](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentManager.cs), [EquipmentEconomy](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentEconomy.cs), [EquipmentInstance](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentInstance.cs), [EquipmentToolbarView](../../Assets/UGUI/Scripts/Views/EquipmentToolbarView.cs), [EquipmentEconomyAcceptance](../../Assets/_Project/Scripts/Core/Balance/EquipmentEconomyAcceptance.cs)
- [MageTowerManager](../../Assets/MageTower/Scripts/MageTowerManager.cs), [MageSkillStateSnapshot](../../Assets/_Project/Scripts/MageTower/MageSkillStateSnapshot.cs), [MageSkillRules](../../Assets/_Project/Scripts/MageTower/MageSkillRules.cs), [MageCatalogMigration](../../Assets/_Project/Scripts/MageTower/MageCatalogMigration.cs)
- [ChangeJob](../../Assets/_Project/Scripts/Player/Job/Script/ChangeJob.cs), [KingdomArmyManager](../../Assets/_Project/Scripts/Core/KingdomArmyManager.cs), [GachaManager](../../Assets/Gacha/Scripts/GachaManager.cs), [BalanceMath](../../Assets/_Project/Scripts/Core/Balance/BalanceMath.cs)
- [QuestCatalog](../../Assets/_Project/Scripts/Quest/QuestCatalog.cs), [QuestContracts](../../Assets/_Project/Scripts/Quest/QuestContracts.cs), [QuestEconomy](../../Assets/_Project/Scripts/Core/Balance/QuestEconomy.cs), [QuestPeriod](../../Assets/_Project/Scripts/Core/Balance/QuestPeriod.cs), [QuestProgressEvaluator](../../Assets/_Project/Scripts/Quest/QuestProgressEvaluator.cs)
- [BattleEconomy](../../Assets/_Project/Scripts/Core/Balance/BattleEconomy.cs), [RubyProgression](../../Assets/_Project/Scripts/Core/Balance/RubyProgression.cs), [ReincarnationService](../../Assets/_Project/Scripts/Reincarnation/ReincarnationService.cs), [OfflineRewardManager](../../Assets/_Project/Scripts/OfflineReward/OfflineRewardManager.cs), [OfflineRewardCalculator](../../Assets/_Project/Scripts/OfflineReward/OfflineRewardCalculator.cs), [GameDirectInteraction](../../Assets/_Project/Scripts/Direction/GameDirectInteraction.cs)

- [StageManager](../../Assets/_Project/Scripts/Core/Manager/StageManager.cs), [StatEnhanceManager](../../Assets/_Project/Scripts/Player/PlayerStatus/StatEnhanceManager.cs), [StageDefinition](../../Assets/_Project/Scripts/Core/Stage/StageDefinition.cs)

## 12. 독립 검토 기록

이전 개정은 장비 계약과 콘텐츠 누락을 별도 에이전트 2명이 검토했다. 이전 개정의 공통 ID/버전/영수증 관련 제안은 최신 사용자 지시로 철회했다.

이번 개정도 장비/스킬/직업/가챠 담당과 콘텐츠 연동 담당의 두 에이전트가 읽기 전용으로 독립 검토했다. 행위별 호출이 중심이고 제외한 공통 필드를 다른 이름으로 재도입하지 않았음을 확인했으며 다음 6건을 보완했다.

| 검토 지적 | 반영 내용 |
|---|---|
| 장비 상한의 단일 +15 표기가 실제 정의와 다름 | 정의별 상한, 현재 일반/희귀/영웅 +5/+10/+15 명시 |
| 강화·장착 대상의 위치/기존 장착 검사가 부족 | 주 보관함만 사용, 새 장착 대상은 미장착 상태 검사 |
| 추가 보관 만료 정책 누락 | 무기한 보관, ExpiresUtc=0, 임의 만료·삭제 금지 |
| 퀘스트 표시 정보가 상세 응답에 빠짐 | IsDynamicGold, decimal Amount, PresentationType 및 고정 골드 보존 |
| 사냥 마지막 배치가 서버 간격/총량에 걸릴 수 있음 | 실제 제한 확인, 허용 총량·간격에 맞춰 전송 후 클리어/종료 |
| 던전 사냥과 입장 회차/중도 종료 연결이 부족 | BattleId로 배치를 구별하고 완료·실패·중도 종료·다음 입장 전 정산 |

- 반영 후 두 에이전트가 각자 지적한 항목을 다시 확인했으며 해당 범위에 남은 지적은 없다.
- 코드 근거 링크 39개의 파일 존재와 문서 차이의 공백 오류를 확인했다. 게임 코드/DTO/에셋은 이 작업에서 수정하지 않았다.
- 로그인 함수명 OnLogIn은 사용자 확인을 반영했다. 그 외 서버 등록명·실제 배포 상태·HTTP 오류 전달 방식·공유 상수 값·기기 실행은 코드/문서 검토로 확인한 사실이 아니다.
