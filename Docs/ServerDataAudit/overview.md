# 서버 연동 참고: 장비·환생·퀘스트

2026-09-15 현재 클라이언트 코드 기준.

## 저장 구조

로컬 진행 데이터의 공통 저장·불러오기 처리는 **Balance 폴더**에 모여 있습니다. [ProgressionState](../../Assets/_Project/Scripts/Core/Balance/ProgressionState.cs#L7)에는 현재 JSON에 저장하는 데이터가 정의되어 있고, [LocalProgression](../../Assets/_Project/Scripts/Core/Balance/LocalProgression.cs#L48)이 파일 읽기·쓰기를 담당합니다.

기능별 인자와 처리 흐름은 **EquipmentManager, ReincarnationService, QuestEconomy**에서 확인할 수 있습니다. 각 기능은 `LocalProgression.Execute()` 안에서 상태를 변경하며, 변경된 상태를 검증한 뒤 전체 JSON으로 저장합니다.

- **DB에 보관할 항목:** `ProgressionState` 참고.
- **요청 인자와 처리 로직:** 아래 기능별 메서드 참고.
- **저장 단위와 검증:** `LocalProgression.Execute()` 참고.

일반 조회는 이미 불러온 메모리 상태를 읽습니다. 아래 메서드 목록은 현재 게임 내부 호출이며, 각각 독립된 서버 API라는 뜻은 아닙니다.

## 1. 장비

| 기능 | 호출 메서드 | 인자 |
|---|---|---|
| 장비 획득 | [EquipmentManager.Grant()](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentManager.cs#L45) | `state`: ProgressionState 클래스 객체, `item`: EquipmentSave 클래스 객체, `allowPending`: bool |
| 장비 장착 | [EquipmentManager.TryEquip()](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentManager.cs#L82) | `playerIndex`: int, `item`: EquipmentInstance 클래스 객체 |
| 장비 강화 | [EquipmentManager.TryEnhanceDetailed()](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentManager.cs#L66) | `item`: EquipmentInstance 클래스 객체 |
| 장비 해제 | [EquipmentManager.Unequip()](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentManager.cs#L96) | `playerIndex`: int |
| 장비 분해 | [EquipmentManager.Dismantle()](../../Assets/_Project/Scripts/Player/Equipment/Script/EquipmentManager.cs#L109) | `item`: EquipmentInstance 클래스 객체 |

장착·강화 등에 전달하는 `EquipmentInstance`를 그대로 저장하지는 않습니다. 장비 ID로 저장용 `EquipmentSave`를 찾아 값을 변경합니다. 저장 필드는 `Id`, `Code`, `Level`, `Player`, `Locked`, `ExpiresUtc`입니다.

## 2. 환생

| 기능 | 호출 메서드 | 인자 |
|---|---|---|
| 환생 가능 여부 조회 | [ReincarnationService.GetPreview()](../../Assets/_Project/Scripts/Reincarnation/ReincarnationService.cs#L22) | 없음 |
| 현재 환생 정보 조회 | [ReincarnationService.CurrentState](../../Assets/_Project/Scripts/Reincarnation/ReincarnationService.cs#L10) — 프로퍼티 | 없음 |
| 환생 요청 | [ReincarnationService.TryReincarnate()](../../Assets/_Project/Scripts/Reincarnation/ReincarnationService.cs#L30) | 없음 |
| 환생 결과 확정 | [ReincarnationService.CommitAtBoundary()](../../Assets/_Project/Scripts/Reincarnation/ReincarnationService.cs#L35) | `s`: ProgressionState 클래스 객체, `battleId`: string |

환생 요청은 먼저 예약을 저장하고, 일반 웨이브 종료 시 실제 결과를 저장합니다. `CurrentState`는 조회용 `ReincarnationState` 구조체를 반환합니다. 기존 [ReincarnationStore](../../Assets/_Project/Scripts/Reincarnation/ReincarnationStore.cs#L115)의 PlayerPrefs 저장 코드는 남아 있지만 현재 서비스에서는 사용하지 않습니다.

## 3. 퀘스트

| 기능 | 호출 메서드 | 인자 |
|---|---|---|
| 진행량 반영 | [QuestEconomy.Count()](../../Assets/_Project/Scripts/Core/Balance/QuestEconomy.cs#L41) | `s`: ProgressionState 클래스 객체, `type`: eQuestObjectiveType enum, `target`: long, `amount`: long |
| 달성 후 보류 보상 기록 | [QuestEconomy.After()](../../Assets/_Project/Scripts/Core/Balance/QuestEconomy.cs#L60) | `s`: ProgressionState 클래스 객체 |
| 보상 수령 | [QuestEconomy.Claim()](../../Assets/_Project/Scripts/Core/Balance/QuestEconomy.cs#L113) | `id`: long, `pendingKey`: string 또는 null |
| 기존 보상 수령 진입점 | [QuestManager.ClaimQuestReward()](../../Assets/_Project/Scripts/Quest/QuestManager.cs#L73) | `id`: long — 내부에서 `QuestEconomy.Claim(id)` 호출 |

보상 수령 시 퀘스트 객체나 보상량을 전달하지 않고 ID와 선택적 보류 키를 전달합니다. 내부에서 조건·보상을 확인하고 재화 지급, 수령 기록, 보류 보상 제거를 함께 저장합니다. `After()`의 보류 보상 기록은 달성한 일일·주간 퀘스트 대상입니다.

## 연결 시 구분할 점

- `Grant()`, `Count()`, `After()`, `CommitAtBoundary()`는 전달받은 상태를 변경합니다. 실제 파일 저장은 이들을 호출하는 상위 `Execute()`에서 처리합니다.
- 함수에 전달하는 클래스 객체·구조체와 JSON에 기록되는 저장 모델은 구분해야 합니다.
- 예를 들어 장비 강화는 **재료 삭제와 강화 레벨 증가를 한 번에 저장**합니다. 서버 연동에서도 함께 처리할 범위를 각 기능 코드에서 확인하면 됩니다.

문서만 정리했으며 게임 코드·저장값은 변경하지 않았습니다. 실제 서버/API 실행 검증은 포함하지 않습니다.
