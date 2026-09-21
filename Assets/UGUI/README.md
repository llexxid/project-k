# UGUI 구조

공통 작업·아트·기기 검증 규칙은 [AGENTS.md](../../AGENTS.md)를 따른다.

## 코드와 에셋

| 위치 | 역할 |
|---|---|
| `Prefabs/Screens`, `Panels`, `Popups`, `Overlays`, `Huds`, `Items` | 인스펙터에서 편집하는 화면과 반복 위젯 |
| `Scripts/Core/UIManager.cs` | 화면·패널 스택, 뒤로가기, 토스트 |
| `UIViewCatalog.asset` | 프리팹·폰트·SFX·아이콘 참조 |
| `Scripts/Views/*Controller.cs` | 데이터 바인딩·사용자 동작 |
| `Scripts/Hud` | 파티·목표·전투 표시 |
| `Scripts/Bridges` | 씬 로딩·전투·UGUI 연결 |
| `Scripts/Core/UguiTheme.cs` | 공용 색·기준 치수 |
| `Editor` | 프리팹 생성·부분 마이그레이션·검증 |
| `Art/Font/Galmuri11 SDF.asset` | UI 기본 폰트 |

## 실행·수정

`bootstrap → UGUI_UIRoot(DontDestroyOnLoad) → SafeArea → 화면/패널/팝업/오버레이`.
`LoadManager → SceneRoutingBridge → UIManager.ReplaceScreen`, 탐색은 `PushPanel`을 사용한다.
화면 치수는 1080px 기준이며 실제 CanvasScaler·SafeArea는 루트 프리팹에서 확인한다.

프리팹을 직접 수정하거나 대상 프리팹만 마이그레이션한다. **Generate All은 수동 편집을 덮어쓰는 초기 뼈대 생성기**다.
새 위젯은 View 참조 → Items 프리팹 → UIViewCatalog 참조 → 컨트롤러 데이터 바인딩 순서로 추가한다.
목록 생성은 패널을 열 때 수행한다. 큰 목록은 가상화·풀링을 검토한다.

## 공통 연출과 기능 안내

`bootstrap/GameManager → Direction.GameDirectManager → FeatureGuidePlayer → UguiFeatureGuideSurface → FeatureGuideView`로 실행한다. Manager는 큐·저장·계정 수명을 소유하는 MonoBehaviour이고 Player와 화면 연결부는 일반 C# 클래스다. 실제 프리팹 참조·레이아웃·입력 필터는 MonoBehaviour View가 담당한다.

`Assets/_Project/Data/Direction/`의 `guide_development`, `guide_kingdom_army`, `guide_dungeon`, `guide_gacha`, `guide_mage_tower`는 독립 실습 SO다. 육성은 공격력 1회 강화, 왕국군은 대표 캐릭터의 종합·장비·전직 정보 열람, 던전은 입장 없는 정보 열람, 뽑기는 장비·마탑 스킬 각각 1회 실습이다. 기존 `first_start_menus`와 `first_reincarnation_guide`도 설명형으로 유지한다. 이전 설명형 완료를 새 실습 완료로 이관하지 않는다.

```csharp
// SO를 직접 전달하면 카탈로그 등록 없이도 요청할 수 있다.
bool accepted = Direction.GameDirectManager.Instance.RequestPlay(sequenceSO);
// ID 호출은 bootstrap의 sequences에 SO를 등록해야 한다.
Direction.GameDirectManager.Instance.RequestPlay("guide_development");
```

반환값은 접수 여부이며 완료 여부가 아니다. 동일 ID가 실행·대기 중이거나 이미 완료·건너뛰기 상태이면 거절하며 `LastError`로 이유를 제공한다. 다른 ID는 FIFO로 진행한다. `CancelCurrent()` 또는 실습의 ‘나중에 계속’은 완료·건너뛰기를 기록하지 않는다. 설명형만 `preview:true`로 저장 없이 반복할 수 있으며 실습형 preview는 거절한다.

### 데이터와 완료 조건

`Create → KingdomIdle → Direction → Sequence`로 SO를 만든다. 계정 저장에 사용하는 sequence ID와 각 단계 ID는 출시 후 다른 의미로 재사용하지 않는다. 단계에는 제목·본문·대상·배치, `context`(필요 화면), `completion`(Confirm/Click/Action), 클릭 이후 `destination`, `allowedTargets`, `allowScroll`을 설정한다. Action은 AttackOnce/EquipmentPullOnce/SkillPullOnce 중 하나다. `grantPracticeCoins`는 두 뽑기 Action에만 설정한다. 기존 SO의 추가 필드 기본값은 설명 확인/Main으로 호환된다. 본문의 `{dailyTickets}`는 실제 던전 카탈로그 값으로 치환한다.

- Confirm: 안내 카드 확인으로 완료한다. 지정한 스크롤만 허용할 수 있다.
- Click: 기존 실제 버튼을 누르고 목적 화면이 열린 것을 확인한다.
- Action: 기존 강화·뽑기 거래가 저장된 후 완료한다. 뽑기는 결과 창의 확인 버튼도 직접 누른다. 최대 강화 상태는 설명 확인으로 대체한다.

`FeatureGuideAnchor.Bind`로 컨트롤러가 동적 UI를 등록한다. Anchor는 자기 RectTransform만 해제해 늦은 Destroy가 새 등록을 지우지 않는다. 새로운 기능 대상은 대상 ID, 컨트롤러 등록, 화면 문맥 복원을 함께 추가한다. SO에 하이어라키 경로나 임의 메서드명을 저장하지 않는다.

### 저장과 입력 수명

장비 안내의 `ArmyEquipment`는 길이가 가변적인 콘텐츠가 아니라 실제 `ScrollRect.viewport`를 등록한다. View는 활성 조상 `RectMask2D`와 강조 영역을 교차해 화면 밖 콘텐츠가 전장까지 강조되지 않게 하며, 뷰포트 안내에서는 스크롤 위치를 자동으로 이동하거나 테두리를 확대하지 않는다.

`guide_mage_tower`는 **실제 마탑 탭 → 스킬 목록 → 장착 버튼 설명 → 해제 버튼 설명**의 네 단계다. `RequestPlay("guide_mage_tower")`로 요청하거나 `GameTest → Guide Test/실제 계정 저장/5 마탑 스킬`에서 실행한다. 장착·해제 실습은 요구하지 않으며 보유 스킬이 없어도 설명 확인으로 완료한다. `MageTower` 문맥은 기존 스킬 팝업을 정상 진행 화면으로 인정하고 미확인 단계 재개 시 복원한다. 마지막 확인 또는 건너뛰기 후에는 팝업을 닫는다. 진행 기록만 저장하며 스킬 소유·장착·재화는 변경하지 않는다. 에셋 누락 복구는 `KingdomIdle/Direction/Prepare mage tower guide`로 가능하다.

안내 진행은 `Modules["game-direct:" + sequenceId]`, 실습 성공은 `Modules["game-direct-action:" + sequenceId + ":" + stepId]`에 분리 저장한다. 강화·뽑기의 `Execute` 거래 초안에 성공 영수증을 함께 넣으므로 소비 직후 종료되어도 재소비하지 않는다. 안내 JSON 저장은 성공 영수증을 덮지 않는다. 주화 지급은 `Claims`의 안내 ID·Action별 키와 50개 지급을 같은 거래에 저장한다. 이미 지급한 주화는 중단 시 회수하지 않으며 추가 지급도 없다. 저장 실패는 초안을 확정하지 않고 안내 입력을 해제한다. 계정 전환은 이전 요청·대기열·지연 콜백을 무효화한다. 환생으로 초기화하지 않는다.

`FeatureGuideInputGate`는 실제 최상단 레이캐스트 대상이 허용 버튼인지 검사한다. 밝은 구멍 안이라고 모든 터치를 통과시키지 않는다. 설명 카드와 지정된 읽기 스크롤 외 조작은 막고, 버튼 복제·부모 이동·경제 처리 대체는 하지 않는다. 필요한 패널·결과 팝업은 안내 흐름에 포함하며 무관한 모달·로딩에서는 숨기고 입력 차단을 해제한다. 돌아오면 미확인 단계의 화면을 복원한다. 자동 전투 시간 배율은 변경하지 않는다. 뒤로가기는 건너뛰기이며 정상 완료 후 메인 화면으로 돌아간다.

### 직접 실행과 검증

bootstrap부터 Play하고 메인 화면에 진입한다. `GameTest` 컴포넌트의 `Guide Test/실제 계정 저장`에서 다섯 개를 각각 또는 순차 요청한다. 실제 계정 결과가 저장되므로 완료 후 다시 실행되지 않는 것이 정상이다. 환생 설명은 별도 미리보기 메뉴로 확인한다. 에셋 연결 복구는 편집 모드 `KingdomIdle/Direction/Prepare interactive guides`를 사용하며 기존 SO의 수동 편집은 덮어쓰지 않는다.

`KingdomIdle/Direction/Validate isolated guide acceptance`는 빈 PlayMode 씬·격리 계정·실제 프리팹·EventSystem 입력으로 검사하고 원래 편집 씬으로 돌아간다. 기존 안내 회귀, 다섯 안내 FIFO, 실제 강화·확률 뽑기, 중복 소비 방지, 지급·저장 실패와 재개를 검사한다. 장비 48개의 스크롤·강조 범위와 마탑 열기·재개도 검사하며 기록은 `AI/validation/game-direct-mage-20260921/`에 남긴다. 이전 네 실습 검증은 `AI/validation/game-direct-interactive-20260921/`에 보존한다. 화면 비율 캡처는 Editor 시뮬레이션이며 Android 실기기 결과가 아니다. 과거 0.15.0 검증 기록은 `AI/validation/game-direct-20260921/`에 보존한다.

자동 발생은 아직 연결하지 않는다. 다음 단계에서 신규 계정·스테이지 클리어 등의 외부 트리거가 해당 SO를 `RequestPlay`하도록 연결한다. 트리거는 게임 조건을 판단하고, 중복·완료 억제와 재개는 Manager에 맡긴다. 기존 계정에 안내 키가 없다는 이유만으로 신규 계정으로 판단하지 않는다.

`GameTest` 순차 실행은 현재 계정에서 미완료인 안내만 요청한다. 완료·건너뛰기 항목은 ID와 함께 `안내 생략` 일반 로그를 남기며 기록을 초기화하지 않는다. 패널 복원 직후 대상이 화면·마스크 밖에 있으면 `FeatureGuideView.TryRefreshLayout`은 false를 반환하고 호출자가 입력·참조를 정리한다. Player가 같은 미확인 단계를 다시 표시하므로 일시적인 레이아웃 미준비를 완료나 오류로 취급하지 않는다. 이 경계와 실제 `GameTest` 재개 경로의 회귀 결과는 `AI/validation/game-direct-visibility-20260921/`에 기록한다.

반복 실습은 PlayMode에서 `GameTest → Guide Test/실제 계정 저장/다섯 안내 테스트 기록 초기화 (재지급 허용)`를 실행한 뒤 개별 또는 순차 실행 메뉴를 사용한다. 초기화 메뉴는 실행 중인 안내와 대기열을 정리하고, 육성·왕국군·던전·뽑기·마탑의 **진행·실습 성공·체험 주화 지급 기록**을 한 번의 저장 거래로 지운다. 다음 실행에서는 강화·뽑기를 다시 수행하며 뽑기 주화 50개씩도 다시 지급한다. 현재 보유 재화·강화·장비·스킬은 유지되므로 반복 테스트 보상은 누적될 수 있다. 환생 안내와 다른 안내·퀘스트 기록은 유지한다. 이 초기화 경로는 `UNITY_EDITOR`에만 포함되며 계정 변경이나 저장 실패 시 초기화를 확정하지 않는다. 검증 기록은 `AI/validation/game-direct-reset-20260921/`에 남긴다.

## 전투 HUD

- `CompactHudBuilder.Apply`: `Screen_Main`, `Panel_Guide`, `Item_GuideStepRow`를 갱신한다.
- 스테이지 배지: `StageBadgeAnchor`가 하단 왕국군 상태 HUD 바로 위에 정렬한다. 보스전에서는 같은 배지에 타이머가 표시된다.
- 좌측 상단: `GuideGoalView`가 실제 `QuestManager`의 현재 단계·목표·진척도를 이벤트로 갱신한다. 진행 중 목적지 이동, 완료 시 다음 단계/보상, 전체 내용은 메뉴에서 확인한다.
- 햄버거: 퀘스트/가이드, 가방, 설정, 반복 사냥 종료. 보스 자동 도전은 스테이지 인디케이터 옆에서 조작한다.
- `Panel_Guide`는 가이드·일일·주간·업적 네 탭을 제공한다. 상단 현재 퀘스트 카드는 가이드 탭에서만 표시하며 HUD와 같은 `QuestManager` 데이터를 쓴다.
- 하단 네 탭은 육성·왕국군·던전·뽑기. 시트가 열리면 HUD 목표 카드는 숨긴다.

`UguiPolishPass → UguiTypeNavPass`는 기존 스타일 보정 도구다. `CompactHudBuilder`의 기본 HUD를 재생성했다면 `PlayabilityRevisionPreparation.Prepare`로 현재 배지·팝업·전직 트리·수동 마탑 슬롯 배치를 적용한다.
일반 글자는 Galmuri11 기본 굵기·공유 머티리얼, 전투 숫자·컷인만 필요한 외곽선을 사용한다.
1080px 기준 패널 제목 40, 주요 버튼 30–34, 설명 26–28, 하단 라벨 30. 아이콘은 Layer Lab `PictoIcon/64` 명시 경로를 사용한다.

## 퀘스트 패널

`UIManager → GuidePanelController.Populate → BalanceQuestPanel.Bind`로 연결한다. `GuidePanelView`는 탭 부모·현재 가이드 카드·스크롤 목록의 직렬화 참조를 보관한다. 실제 패널 데이터는 `TutorialManager`가 아닌 `QuestManager.GetSnapshot(SelectedCategory)`에서 읽는다.

- 공용 `Items/Item_NavTabButton.prefab`을 패널마다 네 개 생성하고 `NavTabButtonView`로 선택 상태를 표시한다. 선택 탭이 바뀌면 기존 스크롤 목록 하나에 해당 범주만 바인딩한다.
- 최초 열기는 가이드 탭이다. 다른 패널에 덮였다 돌아오면 선택을 유지하고 최신 값을 다시 읽는다. 완전히 닫힌 패널은 파괴되므로 새로 열면 가이드부터 시작한다.
- 현재 계정 또는 선택 범주의 변경만 목록에 반영한다. 수령 완료 행은 가득 찬 게이지·낮춘 색상·비활성 `완료` 버튼으로 남긴다. 일일·주간 완료 행은 현재 기간에만 남으며, 이전 기간 미수령 보상은 수령 또는 만료 전까지 별도 기간 표시와 함께 보관한다.
- `GuideStepRowView.SetQuest`가 재화 아이콘(복수 보상 포함)·수량·종류와 내용·진행바를 표시한다. 우측 버튼은 진행 중 `이동`, 달성 시 `받기`, 수령 후 `완료`다. 수령은 표시 시 받은 `QuestClaimToken`을 쓰며 지급·저장 성공 후에만 완료 상태를 표시한다.
- `QuestNavigation`은 가이드 HUD와 카드의 화면 이동을 공유한다. 메뉴는 스택에 쌓아 복귀 상태를 보존하고 전투 목표는 패널을 닫는다. 마탑·환생은 기존 팝업을 사용하며 오프라인 수령/일괄 목표처럼 직접 이동할 화면이 없는 항목은 `진행 중`으로 표시한다.
- `CompactHudBuilder.ApplyQuestTabs`는 기존 `Panel_Guide`에 탭 컨테이너와 참조만 적용한다. 다른 HUD·공용 버튼 프리팹을 재생성하지 않는다. 구조와 학습용 설명은 `AI/quest-tabs-implementation-20260918.md`에 기록한다.

## 검증 진입점

- `KingdomIdle/UGUI/Validate/Check view wiring`: 직렬화 참조·missing script.
- `KingdomIdle/UGUI/Run client regression checks`: 기존 클라이언트 회귀 검사.
- `KingdomIdle/UGUI/Apply compact battle HUD`: 이번 HUD 프리팹 적용.
- `KingdomIdle/UGUI/Apply quest category tabs` / `CompactHudBuilder.ApplyQuestTabs`: 퀘스트 패널의 탭 컨테이너·직렬화 참조만 적용.
- `KingdomIdle/UGUI/Apply quest reward cards` / `QuestCardPrefabBuilder.Apply`: 기존 행 프리팹의 GUID와 참조를 보존하며 보상·진행바·행동 버튼을 연결한다. 기존 전체 생성기도 같은 업그레이드를 사용한다.
- `KingdomIdle.UGUI.Editor.QuestTabAcceptance.Run()`: 빈 검사 씬의 PlayMode에서 실제 프리팹 복제본·격리 계정으로 탭 선택, 수령 완료 유지, 재활성화, 중복 수령 방지, 기간 초기화를 검사한다. 운영 계정이 열린 씬에서는 실행을 거절한다.
- `TitleLobbyDeviceBuild.Build`: `.lobbyqa` ARM64 Development 진단 APK. `BuildForManualTesting`은 같은 패키지를 사용하면서 자동 진단·테스트 계정 주입을 제외한다. `LOBBY_QA_OUTPUT`으로 출력 위치, `DEVICE_BUILD_PURPOSE`로 용도를 지정한다. 앱·APK 이름에는 용도와 버전·빌드 번호를 넣으며 빌드마다 versionCode가 증가한다. 성공한 APK 경로와 메타데이터는 출력 폴더의 `build.json`에서 읽는다. 기기 정리·최종 설치 기준은 [프로젝트 지침](../../AGENTS.md)을 따른다.
- `BalanceEditorValidation.BuildAndroidForManualTesting`: 밸런스·저장 데이터 이관·직렬화 참조 검사를 실행하고 글리프를 준비한 뒤 직접 플레이용 APK를 만든다.
- `CompactHudBuilder.ApplyAndBuild`: HUD 적용 후 위 Android 빌드.
- `BattleHudDeviceProbe`: QA 빌드 전용 HUD 상태·레이아웃·진행 fixture. 일반 배포에는 포함되지 않는다.

Android 검사 도우미·결과는 `AI/qa/hud/`, `Recordings/HudRevision/`에 있다. `Recordings`는 Git 제외다.
퀘스트 탭 결과는 `AI/validation/quest-tabs-20260918/`에 있다. 그 안의 PNG는 실제 프리팹을 PreviewScene에서 고정 예시 데이터로 렌더한 비교 자료이며, 게임플레이 또는 Android 기기 캡처와 구별한다.

## 마탑·뽑기

`MageUiPreparation`은 10종 스킬 셀, 개화 선택이 있는 상세 창, 목재·청동 뽑기 버튼과 결과 창을 준비한다. 스킬 등급은 없으며 청동 테두리와 문구로 장착, 보라색과 아이콘 변형으로 개화를 표시한다. 스킬 셀은 재사용한다. `MageSkillPresentation`은 등록 SO와 저장 상태를 읽는다. `GachaButtonFlare`는 unscaled-time 가장자리 청색 연출을 재사용하며, 저사양에서도 결제 시점·취소 동작은 동일하다. 창을 닫으면 결제 전 연출을 취소하고 연속 터치에는 한 번만 결제한다.

마탑 Android 검증 도우미는 `AI/qa/mage/`, 상세 근거는 [통합 보고서](../../Docs/ArtPreparation/MAGE_INTEGRATION_VALIDATION.md)에 있다.

`MageManualCastHud`는 자동 시전을 끄면 장착 슬롯을 우측 스테이지 표시 위로 순서대로 펼치고 다시 켜면 대응하는 퇴장 애니메이션으로 접는다. 준비 상태는 점등하고 쿨다운은 시계 방향 음영과 남은 초로 표시한다. 탭은 자동 조준 시전, 드래그는 전투 지점 지정이다. `MagicAimGraphic`은 실제 원형 판정 반경을 쿼터뷰 타원으로 투영하고 전장 도트 크기에 맞춰 표시한다. 빈 슬롯은 숨기며 UI 위 드롭, 화면 밖, 전투 전환, 포커스 상실은 시전 없이 취소한다. 얼음 송곳과 유성우는 탭으로만 시전한다.

장비와 가방은 `VirtualEquipmentGrid`로 화면에 보이는 카드만 만든다. 보관함 초과 보상은 기존 수량형 저장 구조에 영구 보관하여 전투·던전·뽑기를 막지 않는다. `EquipmentData.DisplayName`은 화면용 이름이며 기존 저장 키와 아이템 코드는 유지한다. 상세 스탯은 실제 `PlayerStatus` 계산을 사용하고 변경된 계산식만 다시 그린다.

현재 검증 진입점은 `PlayabilityRevisionPreparation.Validate`, Unity 실행 검사 `KingdomIdle.UGUI.Editor.PlayabilityLiveValidation.Run`, Android `AI/qa/mage/playability_checks.py`다. 증거는 `Recordings/PlayabilityRevision`과 [플레이 개선 검증 기록](../../Docs/ArtPreparation/PLAYABILITY_20260918.md)에 있다.

0.11.1 후속 검증은 `PlayabilityRevisionPreparation.ValidateSessionRoutes`와 `AI/qa/mage/player_*.py`를 사용한다. 가이드 이동은 대상 탭을 한 번 지정하고, 스테이지 배지는 반복 사냥에서 다음 구간 도전을 제공한다. 프로필은 실제 진행 수치를 읽으며 던전 팝업은 짧은 웨이브 전환 동안 입장 요청을 유지하고 닫기 시 취소한다. [30분 × 3회 실플레이 기록](../../Docs/ArtPreparation/PLAYER_SIMULATION_20260918.md)에 수정과 성능 측정이 있다.

퀘스트 병합(2026-09-21): UI는 `QuestManager`의 snapshot/event를 읽고 수령은 `TryClaim`으로 요청한다. `LocalProgression`의 0.75초 스킬 자동 저장 성공 뒤 기존 변경 알림으로 갱신하며 별도 표시 타이머는 두지 않는다. 이동은 목표 ID까지 전달해 소환·장비·전직 탭을 선택한다. 프리팹 배치는 퀘스트 카드 작업본을 유지하고 외부 에셋 참조는 프로젝트 내 작업본으로 연결한다.
