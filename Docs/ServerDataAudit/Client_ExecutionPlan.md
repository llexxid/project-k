# Client_ExecutionPlan — 클라이언트 서버 연동 실행계획

- 작성일: 2026-10-11 (KST)
- 목적: Unity 클라이언트에서 수정할 코드, 작업 순서, 서버 의존성, 검증 기준을 정리한다.
- 상태: 실행계획 문서. 이번 문서 분리 작업에서는 게임 코드·DTO·에셋을 수정하지 않는다.
- 서버 요청사항: [Server_ExectionPlan.md](Server_ExectionPlan.md)
- 전체 흐름 보관본: [통합 계획 백업](CLIENT_SERVER_PROTOCOL_PLAN_Backup_2026-10-11.md)
- 출처: 통합 계획 커밋 131f8aaff. 통합 원본과 백업은 이전 검토 기록을 포함한 보관 자료이며, 이후 담당별 작업은 이 문서와 서버 계획을 기준으로 진행한다.

## 1. 클라이언트의 역할과 수정 범위

클라이언트는 콘텐츠 행위를 NetworkManager의 해당 API로 요청하고, 성공 응답의 확정값을 기존 모델에 적용한 후 UI·완료 이벤트·연출을 갱신한다.

~~~text
행위 발생
  → 요청 DTO 작성
  → NetworkManager 공통 RPC
  → 응답 검사
  → 콘텐츠 모델에 서버 확정값 적용
  → 기존 UI와 이벤트 갱신
~~~

서버의 비용 차감·추첨·보상 지급·저장 로직을 클라이언트에서 다시 실행하지 않는다. 전투 시뮬레이션과 표시용 비용/확률은 유지하되 영구 재화·소유·강화 결과는 응답으로 확정한다.

수정 대상은 NetworkManager, 클라이언트에 포함된 공유 DTO/관련 enum·상수, 인증·초기 진입, 사용자 상태 매핑, 장비·마탑·직업·가챠·퀘스트/업적·사냥·골드 강화·던전의 호출/반영부다. 공용 파일은 해당 경로만 수정한다. UI 호출부는 비동기 완료·대기/오류 안내·기존 갱신 이벤트 연결에 필요한 범위로 제한한다.

RequestId, StateRevision, DefinitionVersion 및 이를 대신하는 공통 ID/버전/영수증 체계는 추가하지 않는다. ProtocolVersion은 서버와 같은 상수로 검사한다. 기존 RewardSequence/NextRewardSequence는 사냥 계약에서만 사용한다.

## 2. 실행 순서와 서버 의존성

C 번호는 클라이언트 작업, S 번호는 서버 계획의 대응 항목이다. 작업 분류이며 전송 필드가 아니다.

| 순서 | 클라이언트 작업 | 필요한 서버 산출물 | 완료 기준 |
|---|---|---|---|
| 1 | C01 공유 DTO·상수·호환성 | S01 계약, S02~S08 응답 명세/샘플 | 필수 데이터 매핑과 직렬화 검증 |
| 2 | C02 NetworkManager 공통 RPC | S01 정상/업무 오류/함수 오류 샘플 | 두 종류의 실패와 성공 콜백 분리 |
| 3 | C03 로그인, C04 상태·정의 적용 | S02 초기화/조회/정의 API | 모든 필수 로딩 후 메인 진입 |
| 4 | C05 사냥·던전, C06 캐릭터 강화 | S06/S07, S04 | 행위 호출과 서버 결과 적용 |
| 5 | C07 장비, C08 마탑·직업·가챠 | S03/S04/S05 | 현행 기능을 서버 상태로 재현 |
| 6 | C09 퀘스트·업적 | S08, 다른 행동 API의 퀘스트 결과 | 완료/수령 및 기간 상태 보존 |
| 7 | C10 통합 검증 | S10 등록 함수·시험 계정·실제 응답 | Unity/Android 실연동 확인 |

DTO·RPC·변환 계층은 합의된 예제 응답으로 먼저 준비할 수 있다. 서버 함수가 아직 없으면 해당 기능은 ‘샘플 검증 완료/실연동 대기’로 기록한다. 임의 성공 응답이나 로컬 보상 지급으로 실제 연동이 끝난 것처럼 처리하지 않는다.

## 3. C01 — 공유 DTO와 컴파일 연결 정리

주요 대상: [DTO 폴더](../../Assets/_Project/Scripts/Server/DTO/), [서버 enum](../../Assets/_Project/Scripts/Server/Enums/CommonDatas.cs).

- 현재 작업 트리의 새 DTO와 사용자 변경을 기준으로 기존 호출부의 타입/namespace를 맞춘다.
- ApiVersions의 공유 상수 정의, List using, Unity C# 9와 호환되지 않는 파일 범위 namespace 등 연결 문제를 정리한다. 엔진 버전을 올려 해결하지 않는다.
- 요청은 가입/로그인에 ProtocolVersion, 나머지에 ProtocolVersion + SessionID를 설정한다.
- S02~S08의 상세 장비·마탑·퀘스트·던전·가챠 DTO를 클라이언트 수신 타입에 반영한다. 공통 부모와 실제 JSON 필드는 서버 명세와 동일하게 맞춘다.
- 클라이언트 재화 Ruby/EquipmentStone, 활성 스킬 ID, 확장 스테이지 ID를 서버 enum/표현과 대응시킨다. 기존 기능·상한을 오래된 서버 상수에 맞춰 축소하지 않는다.
- 필수 필드 누락과 0/빈 배열을 구별한다. 캐릭터 Index 0과 스킬 ID 0은 정상값이다.
- long/ulong/decimal을 중간에 double로 바꾸지 않는다. 실제 PlayFab 직렬화 경로로 큰 값과 소수 금액을 검증한다.

완료 기준: 서버 예제 JSON으로 초기 상태와 각 행위 응답을 손실 없이 읽을 수 있다. DTO 상세 명세의 기준은 서버 계획이며 두 문서에 서로 다른 계약을 만들지 않는다.

## 4. C02 — NetworkManager 공통 RPC

주요 대상: [NetworkManager.cs](../../Assets/_Project/Scripts/Core/Manager/NetworkManager.cs).

1. Call<TRequest,TResponse>(functionName, request, onSuccess, onFailure) 형태로 공통 호출을 만든다.
2. 콘텐츠별 공개 메서드는 기능별 DTO를 구성한 뒤 공통 호출을 사용한다.
3. SDK error callback의 PlayFabError와, success callback 안의 ExecuteFunctionResult.Error/ErrorResponseDTO를 구분한다.
4. success callback에서도 ProtocolVersion·필수 데이터·응답 타입을 검사하고 유효한 TResponse만 전달한다.
5. ErrorCode가 있는 응답을 성공 타입으로 해석해 기본값 상태를 적용하지 않는다.
6. 재화/진행 변경 요청과 관련 조회를 순서대로 호출·반영한다. 같은 버튼의 중복 클릭은 대기 상태로 막고 계정 전환 후 늦은 콜백은 폐기한다.
7. 소비 요청은 응답 유실 시 자동 재전송하지 않는다. 현재 사용자 상태 조회로 이용을 재개하며 과거 뽑기 결과를 정확히 복원했다고 간주하지 않는다.

현재 코드에서 함께 수정할 누락:

| 메서드 | 클라이언트 수정 |
|---|---|
| OnSignUpInitUser | InitUserRequestDTO를 FunctionParameter로 전달 |
| OnLogIn | 기존 로그인 메서드·호출부를 OnLogIn으로 정리하고 FunctionName="OnLogIn"에 AuthRequestDTO 전달 |
| OnHuntReward | Stage·Loots·기존 RewardSequence, 던전 BattleId 연결 |
| OnStageClear | ClearedStage와 FunctionParameter 전달 |
| OnGetJob / OnChangeJob | Index 전달, 변경 API에 올바른 요청 DTO 사용 |
| OnSetJobTree | OnSetJobTreeRequestDTO와 SessionID 사용 |
| 전체 RPC | ProtocolVersion 설정 및 공통 응답/오류 처리 |

OnEnChantHP/OnEnChantATK/OnEnChantSkill 등 기존 등록 문자열은 대소문자를 임의 변경하지 않는다. 로그인 서버 함수명은 사용자 확인에 따라 OnLogIn으로 확정되었으며, 클라이언트 메서드·호출부와 FunctionName을 이 이름에 맞춘다.

완료 기준: 호출부에 반복 JSON 해석이 남지 않고, PlayFab 실패와 응답 오류가 구별되어 전달된다.

## 5. C03 — 인증 완료 후 초기 데이터 로딩

주요 대상: [Authentication.cs](../../Assets/_Project/Scripts/Server/Auth/Authentication.cs), [TitleScreenController.cs](../../Assets/UGUI/Scripts/Views/TitleScreenController.cs), [UserManager.cs](../../Assets/_Project/Scripts/Core/Manager/UserManager.cs).

- 인증 성공 → 가입(OnSignUpInitUser) 또는 로그인(OnLogIn) RPC 완료 → 필수 데이터/정의 로딩 → 상태 적용 → 준비 완료 → 메인 진입 순서로 연결한다.
- 현재 RPC 완료 전에 별도의 UserReadOnlyData로 사용자를 만들거나 메인으로 넘어갈 수 있는 흐름을 교체한다.
- PlayFabId 존재만으로 게임 데이터 준비 완료를 판단하지 않는다.
- 삭제된 초기화 응답 타입 참조를 UserInitResponseDTO로 정리한다.
- SessionID를 PlayFabId/SessionTicket과 구별해 저장하고, 이후 API의 서버 세션으로 사용한다.
- CharacterDatas의 Index 0/1/2와 중복·누락을 검사한 뒤 캐릭터 3명을 생성한다.
- 장비·마탑·직업·재화·천장·퀘스트·스테이지/던전을 적용한 뒤 진입을 허용한다. 새 SessionID의 최초 RewardSequence는 클라이언트에서 생성하며 로그인 응답에 시작 순번을 요구하지 않는다.
- OnGetUserState 재조회는 상태를 갱신하는 용도로 사용하고, 로그인처럼 세션을 재발급하거나 캐릭터를 중복 생성하지 않는다.

완료 기준: 인증만 성공한 상태에서는 메인에 진입하지 않으며, 로딩 실패를 성공 상태로 넘기지 않는다.

## 6. C04 — 서버 데이터의 기존 모델 적용

주요 대상: [LocalProgression.cs](../../Assets/_Project/Scripts/Core/Balance/LocalProgression.cs), [ProgressionState.cs](../../Assets/_Project/Scripts/Core/Balance/ProgressionState.cs), [User.cs](../../Assets/_Project/Scripts/User/User.cs), [Wallet.cs](../../Assets/_Project/Scripts/Wallet/Wallet.cs), 각 콘텐츠 로더.

- 현재 로컬 JSON을 확정 주체로 사용하는 대상 경로에 서버 상태 적용 경로를 만든다. 단순히 IsLocalAuthority만 전역으로 뒤집지 않는다.
- 서버 자료를 최초 한 번만 가져오는 import 경로를 정리하고 로그인/재조회마다 서버 상태를 갱신한다.
- 장비·재화·퀘스트 등 한 응답에 함께 온 데이터를 먼저 검증하고 한 번에 반영한 뒤 기존 Changed/완료 이벤트를 보낸다.
- 재화·레벨·소유 상태는 최종값으로 적용한다. 결과창용 GrantedRewards를 잔액이나 인벤토리에 다시 더하지 않는다.
- 전환 대상 상태를 로컬 JSON이 뒤늦게 덮지 않게 한다. 로컬 설정과 범위 밖 콘텐츠의 데이터를 일괄 삭제하지 않는다.
- 서버로 기존 로컬 진행을 업로드하거나 병합하는 것은 별도 이관 정책 없이 수행하지 않는다.
- OnGetContentDefinitions 응답을 기존 장비/마탑/직업/퀘스트/가챠 로더 형식으로 변환한다. 안정적인 콘텐츠 ID와 기존 에셋 키를 유지한다.
- 정의는 로그인 로딩 단계에 필요한 전체 자료를 받아 적용한다. 정의 버전·해시·차등 갱신을 추가하지 않는다.
- 이미지/프리팹은 기존 로컬/CDN 경로를 유지한다. 정의 로딩 실패를 로컬 보상 지급으로 우회하지 않는다.

완료 기준: 서버 상태가 기존 UI·계산 모델에 연결되며 계정 전환/재로그인/재조회 때 상태가 되돌아가거나 이중 지급되지 않는다.

## 7. C05 — 사냥·메인 클리어·던전 호출

주요 대상: [StageManager.cs](../../Assets/_Project/Scripts/Core/Manager/StageManager.cs), [BattleEconomy.cs](../../Assets/_Project/Scripts/Core/Balance/BattleEconomy.cs), [UserManager.cs](../../Assets/_Project/Scripts/Core/Manager/UserManager.cs), 던전 UI 호출부.

### RewardSequence 생성과 갱신

- 새 서버 SessionID를 받은 뒤 최초 사냥 요청 전에 1,000~50,000 양 끝을 포함하는 정수 하나를 생성해 long으로 보관한다. 매 3초마다 새 난수를 생성하지 않는다.
- 이 값은 NetworkManager의 해당 세션 상태로 관리한다. 같은 SessionID에서는 스테이지/던전 전환이나 화면 재진입으로 초기화하지 않는다. 새 SessionID가 발급되면 새 시작값을 생성한다.
- 요청 DTO의 RewardSequence에 저장값을 넣고, 전송한 순번과 배치 내용을 응답 처리까지 보존한다. 전송 시점에는 순번을 증가시키지 않는다.
- 정상 OnHuntResponseDTO.NextRewardSequence가 전송값 + 1인지 확인한 뒤 그대로 다음 요청값으로 저장한다. 응답값에 다시 +1하지 않는다.
- 사냥 요청은 한 번에 하나만 처리한다. 3초가 지나도 응답 대기 중이면 처치만 별도로 누적하고 다음 요청을 겹쳐 보내지 않는다.
- 실패/응답 유실에서는 순번을 임의 증가하거나 새 난수로 초기화하지 않는다. 수락 여부가 불명확한 배치를 새 순번으로 다시 지급 요청하지 않는다. 동일 세션의 순번/배치 처리 여부를 확인하는 복구 계약은 서버 S06에서 별도로 확정한다.

| 배치 | 보내는 RewardSequence | 정상 응답 NextRewardSequence | 클라이언트에 저장할 다음 값 |
|---|---|---|---|
| 최초 | 12000 | 12001 | 12001 |
| 다음 | 12001 | 12002 | 12002 |
| 그다음 | 12002 | 12003 | 12003 |

3초마다 1씩 증가하면 하루 28,800, 30일 864,000 증가한다. long.MaxValue는 9,223,372,036,854,775,807이므로 이 시작 범위로 장기간 사용하기에 충분하다. 50,000은 최초 생성 상한일 뿐 이후 순번의 상한이 아니며, 값이 커졌다고 순환시키지 않는다.

### 사냥과 메인 클리어

- 정의만 있고 시작 호출이 검색되지 않은 3초 사냥 집계 루프를 초기화/종료 수명에 연결한다.
- 현재 Stage별로 처치를 모으고 전송본과 다음 집계 버퍼를 분리한다. 서버 성공 전에 전송본을 지우지 않는다.
- short Count와 서버의 실제 배치 총량·간격 제한에 맞춰 나눈다. 유효한 처치를 잘라내지 않는다.
- 응답의 절대값과 NextRewardSequence를 적용하고 성공한 배치만 제거한다. UserManager.SetHuntResult의 무시 경로를 교체한다.
- 전투 시간/스킬 시전 수가 필요한 경우 S06의 Activity 계약에 맞춰 수집한다. 기존 사냥 배치 계약을 사용하고 별도 활동 ID/순번 체계를 만들지 않는다.
- 메인 클리어 전에 마지막 사냥 배치를 허용 간격에 맞춰 처리한 뒤 OnStageClear를 호출한다.
- 로컬 BattleEconomy에서 대상 재화/드롭/최초 보상을 동시에 확정하지 않게 하고, 응답의 실제 보상과 최종 상태로 표시를 갱신한다.

### 던전

- 입장 전에 OnEnterDungeon을 호출하고 승인된 BattleId/티켓/던전 상태로 진입한다.
- 사냥 배치를 Stage와 BattleId별로 구별해 다른 입장 회차의 결과가 섞이지 않게 한다.
- 클리어·실패·중도 종료·다음 입장 전에 해당 회차의 마지막 사냥 배치를 처리한다.
- 클리어는 OnCompleteDungeon, 실패/중도 종료는 OnExitDungeon으로 연결한다.
- 결과창에서는 TotalRunRewards를 표시하고, 상태에는 응답의 Currency와 실제 지급 결과를 적용한다. 골드 던전의 처치 보상을 클리어 때 다시 가산하지 않는다.
- 기존 전투/복귀 연출은 보존하면서 비동기 완료와 결과창 진입 시점을 연결한다.

완료 기준: 전송 중 추가 처치, 스테이지 전환, 던전 중도 종료 및 재접속에서도 보상과 회차가 섞이지 않는다.

## 8. C06 — 캐릭터 골드 강화

주요 대상: [StatEnhanceManager.cs](../../Assets/_Project/Scripts/Player/PlayerStatus/StatEnhanceManager.cs)와 기존 강화 버튼 호출부.

- 공격력/체력 버튼을 각각 OnEnChantATK/OnEnChantHP로 연결한다.
- 기존 1/10/MAX 입력을 Count로 전달하고 MAX=-1 계약을 사용한다. 개별 캐릭터 Index를 추가하지 않는다.
- 요청 중 IsEnhancing/Pending 상태를 유지하고 전송 성공만으로 강화 성공을 반환하지 않는다.
- 응답의 AppliedCount/CurrentLevel/재화·퀘스트 상태를 적용한 후 기존 OnEnhanceCompleted/OnEnhanced를 호출한다.
- 계정 공통 AttackLevel/HealthLevel을 3명 모두에게 반영한다.
- 표시 비용은 서버 정의에 맞추고 실제 비용 차감은 응답으로 확정한다.

완료 기준: 1/10/MAX·상한 근처·잔액 부족·중복 클릭·서버 오류에서 기존 UI와 세 캐릭터의 스탯이 일치한다.

## 9. C07 — 장비 인벤토리와 장비 행위

주요 대상: [EquipmentManager.cs](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentManager.cs), [EquipmentEconomy.cs](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentEconomy.cs), [EquipmentInstance.cs](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentInstance.cs), 장비 UI.

- EquipmentInventoryDTO를 EquipmentSave/추가 보관/LegacyEquipmentStack에 매핑한다.
- InstanceId, 장비 종류, 강화, 장착 캐릭터, 잠금, 지출 이력, 보관 순서를 보존한다.
- 기존 압축 Inventory와 상세 응답을 함께 더해 중복 개체를 만들지 않는다.
- 강화/장착/해제/잠금/분해/자동 분해 설정/추가 보관 및 초과 수량 회수를 S03의 개별 API에 연결한다.
- 요청 대상을 같은 종류 코드가 아닌 개체 ID로 전달한다. 현행 주 보관함 대상 제한과 장비 정의별 상한을 UI에도 반영한다.
- 분해 확인창을 연 순간의 선택 목록과 수량을 보존해 전송한다. 이후 얻은 장비를 확인 목록에 자동 추가하지 않는다.
- 서버 응답으로 인벤토리·강화석을 갱신한 뒤 기존 장착/해제/강화/드롭 이벤트를 전달한다.
- 300/100 보관과 초과 수량, 무기한 추가 보관, 자동 분해 결과를 기존 표시로 연결한다.
- 비활성 TrySynthesize와 활 신규 지급 경로를 복원하지 않는다.

완료 기준: 같은 종류의 장비 여러 개를 구별하고, 잠금·장착 보호·분해·초과 보관·최근 획득 정렬을 유지한다.

## 10. C08 — 마탑·직업·장비/스킬 가챠

주요 대상: [MageTowerManager.cs](../../Assets/MageTower/Scripts/MageTowerManager.cs), [ChangeJob.cs](../../Assets/_Project/Scripts/Player/Job/Script/ChangeJob.cs), [GachaManager.cs](../../Assets/Gacha/Scripts/GachaManager.cs), 관련 UI 완료 처리.

### 마탑

- MageTowerStateDTO를 기존 MageSave와 5슬롯에 매핑하고 강화/각성/조각/실제 지출/개화 설정을 보존한다.
- 강화·각성·슬롯 변경·개화·강화 초기화를 각각 S04 API에 연결한다.
- 상세 조각 수량을 압축 SkillCode로 재변환하며 잘라내지 않는다.
- 응답으로 슬롯/개화를 반영할 때 진행 중인 시전과 쿨다운을 초기화하지 않는다.
- 비활성 ID 6/8과 폐기된 신 스킬의 진입점을 복원하지 않는다. 서버 이관 결과는 손실 없이 표시한다.

### 직업

- 캐릭터 Index와 JobCode를 보내고 전직 버튼은 한 번의 OnSelectJob 호출로 연결한다.
- 응답의 선택 직업·해금 트리·장비 해제·재화·퀘스트를 함께 적용한다.
- 기존 주 전투 웨이브 경계의 실제 적용 시점과 궁수/정예 궁수 비활성 표시를 유지한다.

### 가챠

- 장비/스킬 각각의 기존 함수에 Count=1/10을 전달하고 로컬 추첨·차감·지급 경로를 교체한다.
- Rewards 순서대로 기존 결과 UI에 연결하며 중복 스킬 조각·재화 보상·자동 분해/보관 결과를 구별한다.
- 천장 및 최종 Equipment/MageTowerState/Currency/Quests를 적용한 뒤 성공 연출을 시작한다.
- 장비 보관함 포화를 뽑기 실패 이유로 처리하지 않는다.
- 가이드 실습 재화와 성공 이벤트의 서버 의존성이 남아 있으면 해당 상태를 통합 미완료로 기록한다.

완료 기준: 로컬 성공 bool과 전송 접수를 혼동하지 않고 서버 성공 후에만 기존 완료 흐름이 실행된다.

## 11. C09 — 퀘스트·업적 완료와 수령

주요 대상: [QuestManager.cs](../../Assets/_Project/Scripts/Quest/QuestManager.cs), [QuestContracts.cs](../../Assets/_Project/Scripts/Quest/QuestContracts.cs), [QuestCatalog.cs](../../Assets/_Project/Scripts/Quest/QuestCatalog.cs), [QuestEconomy.cs](../../Assets/_Project/Scripts/Core/Balance/QuestEconomy.cs), 기존 퀘스트/가이드 UI.

- 서버의 전체 QuestStateDTO를 기존 QuestRowSnapshot/QuestBoardSnapshot으로 변환한다.
- Title/Description/PresentationType/Progress/State/기간/만료/보상 표시를 보존한다.
- IsDynamicGold와 decimal Amount로 동적 골드와 과거 고정 골드를 구별해 표시한다.
- 조건 달성은 OnClearQuest/OnClearAchievement, 보상 받기는 OnClaimQuestReward로 연결한다.
- 다른 행동 응답에 들어온 퀘스트 진행을 반영한 뒤 클리어 호출로 같은 진행량을 추가하지 않는다.
- API 성공 전에 로컬 퀘스트 보상을 지급하거나 수령 플래그를 확정하지 않는다.
- 전체 카테고리 응답을 적용한 뒤 UI에서 필터한다. 일부 목록으로 전체 퀘스트를 덮지 않는다.
- 서버 시간 기준 초기화/미수령 보관 상태를 표시하고 계정 전환 뒤 오래된 클릭/응답은 폐기한다.
- 기존 로컬 보드 갱신용 값은 내부 UI 용도로만 유지하며 서버 상태 순번으로 전송/비교하지 않는다.

완료 기준: 달성 상태와 보상 수령이 구별되고, 다음 가이드·일일/주간·업적·미수령 항목이 일관되게 갱신된다.

## 12. 범위 밖 의존성과 변경 경계

다음 기능의 코드는 이번 대상에 자동 편입하지 않는다. 다만 같은 재화를 로컬에서 바꾸므로 서버 연동 완료 전 연결 여부를 정해야 한다. 서버 측 후속 요구는 S09에 있다.

| 의존성 | 클라이언트 확인 사항 | 현재 범위 |
|---|---|---|
| 루비 영구 성장/초기화 | 서버 재화/성장 응답을 로컬 로직이 덮는지 | 읽기·의존성 확인, 별도 범위 승인 후 수정 |
| 환생 | 골드/강화/메인 진행 초기화와 서버 상태 충돌 | 동일 |
| 오프라인 보상 | 로컬 지급과 서버 잔액 사이 중복/소실 | 동일 |
| 튜토리얼 실습 | 고대주화 50 지급과 가챠/강화 실제 성공 연결 | 신규 가입 통합의 선행 의존성, 별도 범위 확정 |

범위 밖 콘텐츠를 임의 비활성화하거나 미전환 로컬 지급을 남긴 채 서버 모드 출시 완료로 간주하지 않는다. CDN·자동화·외부 에셋·프로젝트 전역 설정 변경은 포함하지 않는다.

## 13. C10 — 클라이언트 검증과 전달

| 검증 단계 | 확인 항목 |
|---|---|
| DTO/응답 샘플 | 필수값 누락, 0 ID, 64비트/decimal, 상세 장비/조각/퀘스트 표시 보존 |
| RPC | PlayFab 실패, 함수 실행 오류, 업무 오류, 프로토콜 불일치, 중복 클릭·늦은 응답 |
| 로그인/재조회 | 초기 로딩 완료 후 진입, 캐릭터 3명, 재접속 복원, 로컬 JSON 덮어쓰기 방지 |
| 사냥/던전 | 최초 순번 범위·서버 응답값 저장·중복 전송 대기·같은 세션 유지/새 세션 초기화·실패 시 임의 증가 금지·집계 보존·서버 총량/간격·마지막 정산·회차·골드 이중 가산 방지 |
| 콘텐츠 | 강화 1/10/MAX, 장비 정의별 상한, 분해 확인목록, 초과 보관, 스킬 슬롯/개화, 가챠 천장 |
| 퀘스트 | 완료/수령 분리, KST 경계, 7일 이월, 동적/고정 골드 |
| Unity | ProjectVersion.txt의 Editor로 import·컴파일·참조·실행 |
| Android | 큰 변경 묶음 후 실제 로그인부터 전투·메뉴·재접속 검증 |
| 실제 통신 | 등록 함수/오류 전달 위치/최대 응답 크기/사냥 주기와 순차 호출 지연 |

클라이언트 완료 보고에는 수정 파일, 서버 의존성 충족 여부, 검증 결과, 미검증 범위를 구별해 남긴다. 샘플 응답 검증과 실제 서버 통신 검증을 구분한다. 이번 문서 분리 작업은 실행 검증을 수행한 것이 아니다.
