# Server_ExectionPlan — 서버 변경 요구 및 실행계획

- 작성일: 2026-10-11 (KST)
- 목적: 현재 Unity 클라이언트 기능을 보존하는 데 필요한 서버 API, 응답 데이터, 검증·저장 처리, 구현 순서를 정리한다.
- 상태: 서버 구현 요청용 계획. 실제 서버 저장소·배포 목록은 확인하지 못했으므로 기존 API 표기는 클라이언트 DTO/호출 코드 존재를 뜻한다.
- 클라이언트 작업: [Client_ExecutionPlan.md](Client_ExecutionPlan.md)
- 전체 흐름 보관본: [통합 계획 백업](CLIENT_SERVER_PROTOCOL_PLAN_Backup_2026-10-11.md)
- 출처: 통합 계획 커밋 131f8aaff.
- 서버 소스 파일 경로는 서버 저장소 확인 후 지정한다. 아래 클라이언트 파일 링크는 동작 확인 자료이며 이 문서의 수정 대상 파일이 아니다.

## 1. 서버 담당 범위와 구현 순서

서버는 행위별 요청에서 인증·대상·조건을 확인하고 비용 계산·난수·보상·영구 상태 변경을 처리한 뒤 기능별 ResponseDTO를 반환한다. 클라이언트의 호출/로딩/UI 변경은 Client_ExecutionPlan에만 작업으로 기재한다.

~~~text
기능별 RequestDTO
  → ProtocolVersion·계정·SessionID 확인
  → 대상 콘텐츠 및 실행 조건 검사
  → 비용·결과·소유·진행 기록 함께 저장
  → 기능별 ResponseDTO 또는 ErrorResponseDTO 반환
~~~

| 순서 | 서버 작업 | 클라이언트 연결 |
|---|---|---|
| 1 | S01 공유 계약·함수명·오류 반환 확정 | C01/C02 |
| 2 | S02 초기화·상태 조회·정의 제공 | C03/C04 |
| 3 | S03 상세 장비 저장/행위, S04 캐릭터·마탑·직업 | C06/C07/C08 |
| 4 | S05 장비/스킬 가챠 | C08 |
| 5 | S06 사냥·메인 클리어, S07 던전 | C05 |
| 6 | S08 퀘스트·업적 및 다른 API의 진행 반영 | C09 및 모든 관련 행위 |
| 7 | S09 범위 밖 공용 재화 의존성 확정 | 클라이언트 12절 |
| 8 | S10 통합 검증·함수 등록·전달 | C10 |

장비/마탑/재화 저장 모델은 초기화 응답과 보상 API가 공통 사용하므로 S02 설계 때 함께 확정한다. 퀘스트 진행 기록도 각 행위 API 설계에 포함하고, S08에서 완료/수령/기간 정책과 통합한다.

## 2. S01 — 공유 DTO·함수명·오류 계약

### 공통 요청/응답

| 항목 | 서버 요구 |
|---|---|
| ProtocolVersion | 클라이언트와 공유하는 고정 상수. 정확히 일치하지 않으면 업무 처리 전 거절 |
| SessionID | 가입/로그인에서 발급, 이후 현재 서버 세션과 비교 |
| 계정 식별 | PlayFab 호출 컨텍스트에서 얻음. 요청의 임의 AccountId를 신뢰하지 않음 |
| ApiResponseDTO | 기존 ProtocolVersion, ServerVersion 유지. ServerVersion은 서버 릴리스 정보 |
| 성공 응답 | 개별 ResponseDTO에 결과와 영향을 받은 최종 상태를 직접 포함 |
| 업무 오류 | 기존 ErrorResponseDTO의 ErrorCode, Message 및 공통 응답 헤더 |
| 수치 | long/ulong/decimal의 정밀도 유지. 필수값 누락과 0/빈 목록 구분 |

RequestId, StateRevision, DefinitionVersion을 추가하지 않는다. 대체 이름의 공통 요청 ID·상태/정의 버전·공통 변경 영수증·변경 이력 복구 API도 만들지 않는다.

기존 사냥 RewardSequence/NextRewardSequence는 해당 배치 계약으로만 유지한다. InstanceId는 장비 소유 개체, PeriodKey는 퀘스트 기간, BattleId는 던전 입장 회차를 식별한다.

ProtocolVersion 불일치는 업데이트 불일치 또는 변조 의심 클라이언트 판별 기준으로 처리한다. 값과 공유 소스/배포 경로를 확정해 클라이언트 담당자에게 전달한다.

### 함수 등록과 오류 반환

- 기존 OnSignUpInitUser/OnExistUserInit 및 OnHuntReward/OnStageClear/강화/가챠/직업 함수의 실제 등록명과 요청 DTO를 확인한다.
- OnEnChantHP/OnEnChantATK/OnEnChantSkill의 대소문자를 포함해 실제 FunctionName을 제공한다. OnLogin으로 이름을 바꿀 경우 명시적으로 계약을 맞춘다.
- 기존 OnSetJobTree의 실제 생성/조회 의미도 확인한다. 클라이언트는 해당 SessionRequestDTO를 보내고 JobTrees를 받는 것으로 연결한다.
- 업무 오류는 FunctionResult에서 ErrorResponseDTO로 식별 가능하게 반환하는 계약을 제안한다.
- 현 DTO 주석의 HTTP 400 방식을 유지할지, 정상 함수 반환에 업무 오류 객체를 담을지 실제 PlayFab 경로에서 확정한다. HTTP 400 본문이 FunctionResult에 그대로 들어온다고 가정하지 않는다.
- PlayFab SDK 실패와 success callback 내부 ExecuteFunctionResult.Error/업무 오류의 실제 예제를 각각 제공한다.
- 입력 검증 실패를 일반 성공 DTO의 기본값으로 돌려주지 않는다.

같은 행위의 비용·보상·소유·진행 변경은 함께 저장한다. 실패한 행위가 일부만 적용되지 않게 한다. 같은 플레이어에 대한 동시 처리는 저장 계층에서 충돌을 막는다. 임의 소비 API에 대한 결과 재생이나 정확히 한 번 실행을 공통 ID 없이 보장한다고 설명하지 않는다. 퀘스트 수령·던전 완료·사냥 배치 중복은 해당 콘텐츠의 저장 규칙으로 다룬다.

산출물: 공유 상수, 실제 FunctionName 목록, 확정 요청/응답 DTO, 성공/업무 오류/함수 오류 JSON 샘플.

## 3. S02 — 가입·로그인·사용자 상태·콘텐츠 정의

### 초기화 및 조회 API

| API | 요청 | 서버 처리/응답 |
|---|---|---|
| OnSignUpInitUser | InitUserRequestDTO: ProtocolVersion | 사용자 초기 데이터 생성·세션 발급, UserInitResponseDTO |
| OnExistUserInit | AuthRequestDTO: ProtocolVersion | 기존 상태 조회·세션 발급, 동일 UserInitResponseDTO |
| 신규 OnGetUserState | SessionRequestDTO의 공통 필드 | 같은 사용자 데이터 집합 재조회. 새 세션을 발급하지 않음 |
| 신규 OnGetContentDefinitions | SessionRequestDTO의 공통 필드 | 클라이언트 표시/계산에 필요한 현재 콘텐츠 정의 전체 |

기존 UserInitResponseDTO의 Inventory: List<ItemCode>는 존재한다. 신규 상세 인벤토리·상태를 보완해야 하며 ‘인벤토리 API가 전혀 없다’는 전제로 재구현하지 않는다.

### 초기 응답과 서버 저장에 필요한 데이터

| 데이터 | 요구 내용 |
|---|---|
| 기존 사용자 필드 | SessionID, Level, Exp, KillScore, CurrentStage, EnchantHPCount, EnchantATKCount |
| CharacterDatas | Index 0/1/2 각 1개, 이름·직업·스탯 |
| Currency | 기존 CurrencyQueryDTO 확장. 현재 사용하는 재화 잔액과 EquipmentStone 포함 |
| Equipment | S03의 상세 인벤토리·장착·잠금·보관·강화 지출 |
| MageTowerState | S04의 스킬 상세 정보와 5슬롯 |
| CharacterJobs/JobTrees | 캐릭터별 선택 직업·해금 목록. 장착 상태와 일치 |
| GachaState | S05의 장비 천장 |
| Quests | S08의 전체 카테고리 및 미수령 항목 |
| 스테이지 진행 | 최고/최초 클리어 기록과 현재 진행 |
| DungeonState | S07의 티켓·클리어 기록·진행 중 입장 |
| NextRewardSequence | 최초/재접속 후 사냥 배치에 사용할 기존 계약 값 |
| ServerTimeUtc | 기간제 콘텐츠 초기화/표시의 기준 시각 |

Currency와 서버 enum은 클라이언트의 Gold, AncientCoin, KingdomSupply, TrainingTome, ClassFragment, ArcaneKnowledge, Ruby, EquipmentStone을 확인해 누락 없이 대응한다. 미사용 과거 재화가 남아 있다면 임의 삭제/0 처리하지 않는다.

기존 장비 개체 ID·강화 지출·마탑 지출·미수령 고정 보상을 서버에 보관하지 않았다면 이관 근거와 정책을 확정한다. 과거 지출을 현재 공식으로 임의 복원하거나 클라이언트 JSON을 무조건 신뢰해 업로드하는 계약은 만들지 않는다.

### 콘텐츠 정의 응답

장비 능력치/강화 상한·비용, 마탑 정의, 직업 조건, 퀘스트/업적 설명과 보상표, 가챠 표시 확률을 기존 JSON 로더가 읽을 수 있는 구조와 안정적인 ID로 제공한다. 기존 에셋을 찾는 키도 보존한다.

첫 계약은 필요한 전체 정의를 반환한다. 정의 버전·해시·NotModified·차등 갱신을 추가하지 않는다. 이미지/프리팹을 이 API의 데이터에 포함시키거나 CDN 시스템을 개편하는 요구가 아니다.

완료 기준: 신규/기존 계정이 같은 초기화 모델로 게임 진입에 필요한 데이터를 받고, 상태 재조회가 세션을 바꾸지 않는다.

## 4. S03 — 장비 인벤토리와 개별 장비 API

### 저장/응답 모델

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

압축 ItemCode는 같은 종류의 개별 소유 장비를 구별할 수 없다. 상세 개체 ID를 영구 보관하고 장착은 InstanceId + CharacterIndex를 기준으로 한다. 기존 CharacterEquipmentDatas도 제공한다면 같은 상태를 나타내야 한다. 배열 순서는 클라이언트의 최근 획득 정렬을 보존한다.

### API 요구

아래 이름에 RequestDTO/ResponseDTO를 붙인 개별 계약을 사용하며 공통 헤더는 S01과 같다. 장비 변경 응답은 EquipmentInventoryDTO, 영향을 받은 Currency/Quests 및 표시용 실제 소비/지급량을 반환한다.

| API | 요청 | 서버 처리 |
|---|---|---|
| OnGetEquipmentInventory | 공통 헤더 | 상세 인벤토리 조회 |
| 기존 OnEnchantEquipment 확장 | InstanceId | 주 보관함 개체·정의별 상한·강화석 확인 후 강화 |
| OnEquipEquipment | InstanceId, CharacterIndex | 주 보관함 미장착 개체·직업 적합성 확인. 해당 캐릭터의 기존 장비 해제와 새 장착을 함께 저장 |
| OnUnequipEquipment | CharacterIndex, InstanceId | 현재 장착 일치 확인 후 해제 |
| OnSetEquipmentLock | InstanceId, IsLocked | 개체 잠금 변경 |
| OnDismantleEquipment | Mode, Items[], ReserveStacks[] | 확인 대상 전체 검증 후 삭제·강화석 지급 |
| OnSetAutoDismantle | RarityMask | 이후 신규 획득에 적용할 설정 저장 |
| OnClaimPendingEquipment | InstanceId | 공간 확인 후 주 보관함으로 이동 |
| OnClaimReserveEquipment | DefinitionId, EnhancementLevel | 스택에서 1개를 개별 소유 장비로 전환 |

다른 캐릭터에게 이미 장착된 무기를 강제로 이전하지 않는다. 강화에 잠금/장착 보호를 일괄 적용하지 말고 현재 강화 가능 조건을 보존한다. 분해에서는 잠금/장착을 보호한다.

### 분해와 보관 규칙

- Items[]에는 확인한 InstanceId, DefinitionId, EnhancementLevel, EnhancementStonesSpent, 보관 위치를 받는다. ReserveStacks[]에는 종류·강화·수량을 받는다.
- 선택 개체가 삭제되거나 잠금/장착/강화/위치가 달라졌으면 전체 요청을 거절한다. 스택이 늘었다면 확인한 수량만 소비한다.
- 양수 수량, 중복 ID/스택 키, Mode별 허용 대상, 산술 overflow를 검증한다. 새로운 드롭을 이전 확인 목록에 포함하지 않는다.
- 주 보관 300, 추가 보관 100, 초과 수량 보존. 포화 때문에 사냥·던전·충분한 재화의 뽑기를 실패시키지 않는다.
- 추가 보관은 현재 무기한이며 ExpiresUtc=0이다. 표시상 만료값만으로 소유 보상을 임의 삭제하지 않는다.
- 스택에는 개별 잠금·장착·지출 이력을 담을 수 없다. 특수 상태 개체를 스택으로 뭉개지 않는다.
- 강화는 확정 성공, 장비 정의별 상한을 적용한다. 현재 일반 +5/희귀 +10/영웅 +15다.
- 기본 분해량은 일반 1/희귀 4/영웅 12. 강화 비용은 2 × 기본 분해량 × (1 + floor(현재 강화/3)).
- 분해 보상은 기본 분해량 + floor(실제 사용 강화석 × 0.8). 단일 분해는 강화품도 가능하고 일괄 분해는 +0만 대상이다.
- 자동 분해는 설정에 해당하는 신규 +0·미잠금·미장착 장비에 적용하며 기존 소유 장비를 즉시 분해하지 않는다.
- 활 신규 드롭/뽑기는 제외하고 기존 소유는 보존한다. 비활성 합성 API는 추가하지 않는다.

완료 기준: 같은 종류의 장비를 개별 조작하고, 재로그인 시 소유·장착·잠금·보관·강화 지출이 동일하게 복원된다.

## 5. S04 — 캐릭터 강화·마탑·직업

### 캐릭터 골드 강화

기존 OnEnChantATK/OnEnChantHP는 계정 공통 AttackLevel/HealthLevel을 처리한다. 개별 캐릭터 Index를 요구하지 않는다.

- OnEnchantRequestDTO.Count는 1/10/MAX 의미를 유지한다. MAX=-1이면 서버 잔액으로 가능한 횟수를 계산한다.
- 양수 요청은 상한까지 남은 횟수로 제한하며 그 비용이 부족하면 실패한다.
- 현재 상한 300과 BalanceMath의 비용식을 사용한다.
- OnEnchantResponseDTO의 CurrentLevel/CurrentGold는 최종값이다. AppliedCount와 관련 Currency/Quests를 보완한다.

### 마탑 모델과 API

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

SkillCode만으로 슬롯·개화·지출 이력과 65,535를 넘는 조각 수량을 보존할 수 없다. 상세 상태를 저장/반환하며 압축 호환 필드에 맞추느라 수량을 잘라내지 않는다.

| API | 요청 | 서버 처리/응답 |
|---|---|---|
| 기존 OnEnChantSkill | 기존 SkillCode로 대상 ID 식별 | 서버 현재 레벨/지식으로 강화, MageTowerState/Currency/Quests |
| 기존 OnAwakeningSkill | 기존 SkillCode로 대상 ID 식별 | 서버 보유 조각으로 각성, MageTowerState/Currency/Quests |
| OnSetSkillLoadout | EquippedSkillIds[5] | 보유·중복·비활성 ID 확인 후 슬롯 전체 변경, MageTowerState |
| OnSetSkillBloom | SkillId, Enabled | 각성 조건 검사, MageTowerState |
| OnResetSkillEnhancement | SkillId | 실제 지식 지출의 80% 환급, 강화/지출 초기화, MageTowerState/Currency/환급량 |

현재 강화 상한 100, 각성 상한 10, 중복 스킬 조각 30, 다음 각성 단계만큼의 조각 소비, 각성 10의 개화 조건을 보존한다. 초기화는 각성·조각·장착·개화를 지우지 않는다.

ID 6/8은 신규 지급/재사용하지 않는다. ID 8→3 이관은 높은 투자 상태·중복 투자 환급·원본 내역을 보존한다. 폐기된 신 스킬 시스템을 재도입하지 않는다.

### 직업 API

기존 OnGetJob/OnChangeJob의 Index 계약을 명확히 하고, 실제 전직 버튼에 맞는 OnSelectJobRequestDTO(Index, JobCode) / OnSelectJobResponseDTO를 추가한다.

- 미해금이면 선행 직업 확인과 직업조각 차감, 해금, 선택, 비호환 장비 해제를 한 번에 저장한다.
- 이미 해금된 직업 선택에는 해금 비용을 다시 차감하지 않는다.
- 기본/상위 해금 비용은 40/120이며 궁수/정예 궁수는 비활성이다.
- CharacterJobs, JobTrees, Equipment, Currency, Quests 및 신규 해금/실제 비용 정보를 반환한다.
- 현재 전직 가능 전투 상황과 실제 웨이브 경계 적용 의미를 클라이언트와 맞춘다. 기존 두 API를 유지하더라도 하나의 전직을 두 독립 소비로 나누지 않는다.

완료 기준: 서버 DTO에 남은 상한 9나 축소된 스킬 목록 때문에 현행 기능을 잃지 않으며, 강화·초기화·전직이 저장 후 일관되게 조회된다.

## 6. S05 — 장비/스킬 가챠

기존 OnGachaEquipmentClassFragment / OnGachaSkillArcaneKnowledge를 유지한다. 요청은 OnGachaRequestDTO의 Count=1 또는 10이다.

서버가 비용 차감·추첨·천장·중복 변환·장비 보관/자동 분해·퀘스트 증가를 함께 처리한다. 클라이언트가 원하는 보상이나 결과 난수를 제출하는 구조로 만들지 않는다.

~~~text
GachaRewardDTO
  Order: int
  Kind: Equipment / Skill / Currency
  DefinitionId: int?
  CurrencyType: eCurrency?
  Amount: long
  IsDuplicate: bool
  GrantedFragments: int
  InstanceId: string?
  Disposition: Inventory / Pending / Reserve / AutoDismantled / None
  GrantedEquipmentStone: long

GachaStateDTO
  EquipmentPity: int
~~~

각 기존 가챠 ResponseDTO에 Rewards[], Currency, GachaState, 해당 Equipment/MageTowerState, Quests를 포함한다. Rewards는 순서별 연출/실제 지급 설명이고 Currency/소유 상태는 최종값이다. 기존 GachaList/Cnt만으로 중복·순서·최종 잔액을 추정하게 하지 않는다.

현행 규칙:

- 회당 고대주화 50, 1/10회.
- 장비 일반 70%/희귀 20%/영웅 5%/직업조각 2개 5%.
- 장비 40번째 영웅 보장, 영웅 획득 시 천장 초기화.
- 스킬 총 50%를 활성 8종에 균등 분배. 나머지는 지식 10/20/50개가 각각 30%/15%/5%.
- 스킬에는 희귀도를 추가하지 않는다. 장비 포화 시에도 소유 수량을 보존한다.

완료 기준: 실제 소비·결과·보관·천장·퀘스트가 같은 저장 결과를 나타내고 재로그인해도 유지된다.

## 7. S06 — 사냥·메인 스테이지 클리어

### OnHuntReward

OnRewardRequestDTO의 기존 Stage/Loots/RewardSequence를 사용하고, 던전 배치에는 BattleId를 보완한다.

- Stage와 처치 자료, 기존 배치 순번, 던전 입장 회차를 검증한다.
- 서버 정의로 경험치·골드·드롭을 결정하고 계정 성장·재화·장비·퀘스트를 함께 저장한다.
- 응답은 기존 Level/Exp/KillScore/Gold/AncientCoin/NextRewardSequence에 실제 드롭·장비 상태·퀘스트 상태를 보완한다.
- Currency 전체도 반환한다면 기존 Gold/AncientCoin과 같은 최종값을 담는다.
- 같은 기존 배치 순번을 재전송했을 때의 중복 지급 방지와 응답 내용을 확정하고 클라이언트에 전달한다.

현재 CommonDatas 주석은 총처치 150 이상 거절과 직전 수락 후 2초 이하 거절을 나타낸다. 실제 서버 구현 여부를 확인하고 총량·간격·최종 배치 규칙을 문서화한다. 정상 전투 속도를 감당하지 못하면 서버 검증 기준을 조정하며, 유효한 누적 보상을 버리는 방식으로 맞추지 않는다.

전투 시간/스킬 시전 횟수 목표는 같은 배치의 Activity(전투 시간, 스킬별 시전 수)로 받는 안을 사용한다. 처치 없이 활동만 있는 배치도 허용한다. 경과시간·장착·쿨다운·기간을 검증하고 기간 경계의 자료를 분리한다. 정확한 시간 구간 필드와 허용 오차는 계약 단계에서 확정한다. 기존 사냥 순번을 배치 전체에 적용하며 별도 활동 ID/순번 체계를 추가하지 않는다.

### OnStageClear

- OnStageClearRequestDTO.ClearedStage를 검증해 진행·최고/최초 클리어 기록을 저장한다.
- 최초 보상은 계정의 해당 스테이지 지급 기록을 확인한다.
- 기존 응답 Stage에 실제 최초 보상과 진행 기록, 관련 Currency/Equipment/MageTowerState/Quests를 보완한다.
- 사냥 마지막 배치 처리 이후 클리어가 이어지게 계약을 맞춘다.
- 3장 이후 반복 지역과 증가하는 단계/난도를 보존한다. 서버 enum에 열거된 1~2장만 지원하는 것으로 제한하지 않는다.
- 안정적인 Stage ID와 기존 최초 장비/스킬/재화 지급 규칙을 사용한다.

완료 기준: 배치 중복·스테이지 이동·최초 보상·활동 목표가 서로 중복 지급되거나 누락되지 않는다.

## 8. S07 — 던전 입장·클리어·실패/중도 종료

~~~text
DungeonStateDTO
  GoldTickets: int
  RubyTickets: int
  TicketResetUtc: long
  GoldDungeonClear: int
  RubyDungeonClear: int
  ActiveBattleId: string?
  ActiveDungeonId: long?
~~~

| API | 요청 | 응답/서버 처리 |
|---|---|---|
| OnEnterDungeon | DungeonId | 난도·티켓·진행 회차 확인, 티켓 차감, BattleId 발급, DungeonState/Quests |
| OnCompleteDungeon | BattleId, DungeonId | 회차 완료 검증, 최초 기록/보상, DungeonState/IsFirstClear/GrantedRewards/TotalRunRewards/Currency/Quests 및 필요시 장비/스킬 |
| OnExitDungeon | BattleId, Reason(Failed/Abandoned) | 해당 입장 종료 기록, DungeonState/Quests |

각 API는 별도의 RequestDTO/ResponseDTO를 갖는다. DungeonId는 현 StageDefinition의 난도를 포함한 ID이며 BattleId는 실제 입장 회차다.

- 입장 때 티켓을 차감한다. 실패/중도 종료 시에는 클리어 보상 없이 현행 입장 차감 정책을 유지한다.
- 해당 BattleId의 남은 사냥을 정산한 뒤 클리어/종료하고 다음 회차로 이동한다.
- 종료 전 정상 획득한 처치 보상을 보존한다. 이미 종료한 회차에 새로운 처치를 넣지 않는다.
- 회차 완료/종료 기록으로 같은 완료의 중복 지급을 막는다.
- 골드 던전은 처치 때 이미 지급한 골드를 클리어에서 다시 지급하지 않는다.
- 루비 던전은 클리어 루비와 최초 추가 보상을 지급한다.
- GrantedRewards는 이번 호출의 신규 지급량, TotalRunRewards는 결과창에서 보여 줄 회차 전체 보상이다.
- 일일 티켓 초기화 시각과 난도 해금/클리어 기록을 조회에도 제공한다.

완료 기준: 입장·사냥·종료가 한 회차로 연결되고, 반복 완료·중도 종료·재접속에서 티켓과 보상이 일관된다.

## 9. S08 — 퀘스트·업적 진행·완료·수령

Guide/Daily/Weekly/Achievement는 현재 같은 처리 모델을 사용하므로 서버도 같은 저장/검증 기반을 사용한다. 조건 달성은 Claimable이며 실제 보상은 수령 API에서 지급한다.

### API와 표시 데이터

| API | 요청 | 응답/서버 처리 |
|---|---|---|
| OnGetQuestState | 공통 헤더 | 전체 카테고리 QuestStateDTO |
| OnClearQuest | QuestId, PeriodKey | 서버 진행값·조건 확인, 전체 QuestStateDTO |
| OnClearAchievement | QuestId, PeriodKey | 같은 처리기에서 업적 분류 확인, 전체 QuestStateDTO |
| OnClaimQuestReward | QuestId, PeriodKey | 미수령·조건·기한 확인, GrantedRewards와 최종 Currency/Quests 및 필요한 장비/스킬 |

~~~text
QuestRewardDTO
  Kind, CurrencyType/DefinitionId
  Amount: decimal
  IsDynamicGold: bool

QuestRowDTO
  QuestId: long
  PeriodKey: string
  Category: Guide / Daily / Weekly / Achievement
  Title, Description, PresentationType
  ObjectiveType, TargetId, RequiredCount, Progress
  State: Locked / InProgress / Claimable / Claimed
  IsPending: bool
  ExpiresUtc: long
  Rewards: QuestRewardDTO[]

QuestStateDTO
  ServerTimeUtc: long
  DailyPeriodKey, DailyResetUtc
  WeeklyPeriodKey, WeeklyResetUtc
  Rows: QuestRowDTO[]               // 전체 카테고리와 미수령 보관 항목
~~~

클라이언트의 AccountGeneration이나 UI 갱신용 Revision은 서버 계약 필드로 추가하지 않는다. 과거 고정 골드는 IsDynamicGold=false와 소수 정밀도를 보존한 Amount로 표시한다. GrantedRewards는 동적 표시가 아닌 실제 확정 수량이다.

### 서버 처리 규칙

- 사냥·강화·가챠·던전 등 서버가 처리한 행위에서 퀘스트 진행을 증가시킨다.
- 클리어 API는 그 진행값을 재증가시키지 않고 완료 상태를 확인한다. 완료 재호출로 보상을 지급하지 않는다.
- 계정 + QuestId + PeriodKey의 수령 기록으로 이중 지급을 방지한다.
- KST 자정/월요일 초기화, 메인 1-11 이후 일일·주간 해금을 보존한다.
- 가이드 순서, 현재값/누적값 목표, 달성 상태 고정, 업적 최고 기록을 보존한다.
- 완료 미수령은 기간 종료 후 7일까지 유지한다. 당시 제목·설명·표시 방식·보상 정의를 저장해 현재 정의가 바뀌어도 보존한다.
- 일반 동적 골드는 수령 시 서버 상태로 계산한다. 기존 HasFixedGold 항목만 고정값을 쓰고 원래 소수 잔여 처리도 유지한다.
- 전투 시간/시전 횟수는 S06의 검증된 Activity로 누적한다. 임의 완료 bool이나 진행 숫자를 그대로 승인하지 않는다.
- 수령 결과로 다음 가이드·업적·전체 완료 목표가 달라지면 함께 저장하고 같은 응답으로 돌려준다.
- 첫 계약은 전체 퀘스트 상태 반환으로 통일한다. 카테고리 일부만 전체 상태처럼 반환하지 않는다.

완료 기준: 완료와 수령이 분리되고 기간 경계·미수령 이월·동적/고정 골드·다음 목표가 현행 동작과 일치한다.

## 10. S09 — 후속 콘텐츠와 공용 재화 의존성

이 절은 발견한 추가 요구의 목록이며 서버/클라이언트 구현 범위에 자동 포함하지 않는다. 실제 수정 범위를 정하기 전에는 후속 항목으로 유지한다.

| 후속 API/DTO 제안 | 요청 | 서버 요구 |
|---|---|---|
| OnEnhanceRubyGrowth | 성장 종류, 횟수 | 비용/상한·최종 성장/재화 |
| OnResetRubyGrowth | 성장 종류 | 웨이브 경계 예약, 실제 지출 기반 환급, 일일 제한 |
| OnRequestReincarnation | 현재 진행 단계의 실행 요청 | 조건 검사·경계 적용 대기/확정·초기화/보존 상태 |
| OnGetOfflineReward / OnClaimOfflineReward | 공통 헤더 | 미수령 시간 구간·사냥 속도 근거로 골드/경험치 계산, 수령 기준 시각 갱신 |
| OnClaimGuidePracticeCoins | 현재 가이드 단계·행동 | 단계·일회 지급 확인, 실습 재화/최종 진행 상태 |

튜토리얼 고대주화 50과 실제 가챠/강화 성공의 단계 반영은 신규 가입 통합의 선행 의존성이다. 서버가 처리한 성공을 단계 진행과 연결하고 클라이언트의 임의 성공 선언으로 완료시키지 않는다.

환생·오프라인 보상 등 같은 재화를 바꾸는 미전환 경로가 남아 있으면 서버 최종 잔액과 충돌한다. 필요한 연결 범위를 확정한 뒤 통합 완료로 판단한다. 후속 콘텐츠를 임의 삭제하거나 비활성화하는 작업은 요구하지 않는다.

## 11. S10 — 서버 검증·배포·클라이언트 전달

| 검증 대상 | 확인 사항 |
|---|---|
| 공통 계약 | 프로토콜 불일치 사전 거절, 세션/계정 검증, 필수값, 64비트/decimal |
| 오류 반환 | PlayFab 실패, 함수 실행 오류, 업무 오류의 실제 관측 위치와 샘플 |
| 저장 | 비용·소유·보상·진행의 일괄 저장, 실패 시 부분 적용 방지, 동일 계정 동시 처리 |
| 장비 | 동일 종류 개체, 주 보관함 대상, 장착/잠금 보호, 정의별 상한, 분해 확인목록, 무기한/초과 보관 |
| 강화/마탑/직업 | 1/10/MAX, 실제 지출/환급, 큰 조각 수량, 슬롯/개화, 비활성 로스터, 전직 일괄 처리 |
| 가챠 | 결과 순서, 비용, 천장, 스킬 균등, 중복/자동 분해/보관 결과 |
| 사냥/스테이지 | 총량·간격·배치 중복·최종 배치·필드 드롭·최초 보상 |
| 던전 | 티켓·회차별 사냥·정산 후 종료·반복 완료·골드 이중 지급 방지 |
| 퀘스트 | 완료/수령 분리, 중복 수령, KST 경계, 7일 이월, 동적/고정 골드 |
| 성능/전송 | 최대 인벤토리·퀘스트·정의 응답 크기, 사냥 주기와 처리 지연 |

실제 응답 크기 제한을 넘으면 필요한 조회만 분리하는 방향을 검토한다. 새로운 공통 버전 필드를 추가하거나 데이터를 잘라내는 방식으로 해결하지 않는다.

클라이언트 담당자에게 다음 자료를 전달한다.

1. 함수별 구현/등록 여부, 실제 FunctionName, 확정 DTO와 ProtocolVersion 상수.
2. 신규/기존 로그인, 상세 인벤토리, 각 행위 성공/실패의 실제 JSON.
3. 사냥 제한·기존 순번 재전송 응답·던전 종료 전 정산 규칙.
4. 기존 저장 데이터 이관/보존 결과와 아직 지원하지 않는 항목.
5. 테스트 계정/환경의 준비 여부와 검증 결과. 문서상의 API 제안과 실제 배포 완료를 구분한다.

완료 기준: 클라이언트가 합의된 계약으로 실제 호출해 검증할 수 있고, 응답 유실 후 현재 상태 조회와 재로그인 복원이 일관되게 동작한다.

## 12. 요구사항의 클라이언트 근거

다음은 서버 규칙 이관 시 읽을 자료다. 이 문서가 해당 클라이언트 파일의 변경을 지시하는 것은 아니다.

- [RequestDTO](../../Assets/_Project/Scripts/Server/DTO/RequestDTO.cs), [ResponseDTO](../../Assets/_Project/Scripts/Server/DTO/ResponseDTO.cs), [QueryDTO](../../Assets/_Project/Scripts/Server/DTO/QueryDTO.cs), [ProtocolDTO](../../Assets/_Project/Scripts/Server/DTO/ProtocolDTO.cs), [ErrorResponseDTO](../../Assets/_Project/Scripts/Server/DTO/ErrorResponseDTO.cs)
- [EquipmentManager](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentManager.cs), [EquipmentEconomy](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentEconomy.cs)
- [StatEnhanceManager](../../Assets/_Project/Scripts/Player/PlayerStatus/StatEnhanceManager.cs), [BalanceMath](../../Assets/_Project/Scripts/Core/Balance/BalanceMath.cs)
- [MageTowerManager](../../Assets/MageTower/Scripts/MageTowerManager.cs), [MageSkillRules](../../Assets/_Project/Scripts/MageTower/MageSkillRules.cs), [MageCatalogMigration](../../Assets/_Project/Scripts/MageTower/MageCatalogMigration.cs)
- [ChangeJob](../../Assets/_Project/Scripts/Player/Job/Script/ChangeJob.cs), [GachaManager](../../Assets/Gacha/Scripts/GachaManager.cs)
- [BattleEconomy](../../Assets/_Project/Scripts/Core/Balance/BattleEconomy.cs), [StageDefinition](../../Assets/_Project/Scripts/Core/Stage/StageDefinition.cs)
- [QuestContracts](../../Assets/_Project/Scripts/Quest/QuestContracts.cs), [QuestCatalog](../../Assets/_Project/Scripts/Quest/QuestCatalog.cs), [QuestEconomy](../../Assets/_Project/Scripts/Core/Balance/QuestEconomy.cs), [QuestPeriod](../../Assets/_Project/Scripts/Core/Balance/QuestPeriod.cs)
- [기존 통합 검토와 나머지 근거](CLIENT_SERVER_PROTOCOL_PLAN_Backup_2026-10-11.md)
