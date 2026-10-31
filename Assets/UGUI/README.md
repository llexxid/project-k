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

### 광고 단독 테스트

`KingdomIdle → Ads → Open Test Scene`으로 `Scenes/AdMobTest.unity`를 열고 Play한 뒤 `광고 보기`를 누른다. 초기화·로드·재생을 자동 처리하며 SDK 보상 콜백을 받은 후 광고를 닫으면 완료 팝업을 표시한다. 중도 종료는 완료 처리하지 않고 실패 시 같은 팝업으로 재시도를 안내한다. Editor는 SDK 모의 광고이고, Android는 Google의 고정 테스트 광고 단위를 사용한다. 로그인·실제 재화 지급은 없다.

`KingdomIdle → Ads → Build Android Test APK`는 이 씬만 `.adtest` 패키지로 빌드한다. 결과는 `Recordings/AdMobTest/Android`, 버전과 빌드 번호는 APK 이름에 포함된다. 기존 게임 앱과 별도로 설치할 수 있으며 Build Settings의 게임 씬 목록은 바꾸지 않는다.

빌드 중 `mainTemplate.gradle needs to be updated` 창이 나오면 `Yes`로 진행한다. Unity 6의 `packaging` 문법으로 갱신하는 안내이며 외부 의존성 관리자가 이전 문법을 다시 쓸 때 나타날 수 있다.

공통 호출은 `KingdomIdle.Ads.RewardedAdService.Show(adUnitId, (result, error) => ...)`다. `Completed`만 보상 자격으로 인정하고, 보상 저장 성공은 호출자가 확인한다. `Skipped`, `Failed`, `Busy`를 구별하며 요청당 한 번만 완료를 반환한다. 광고 준비는 60초 후 실패로 해제되고, `IsBusy`/`IsShowing`으로 진행 상태를 확인한다. 현재 Google 광고만 검증 대상으로 삼으며 미디에이션 추가 시 보상·닫힘 콜백 순서를 재검증한다.

### 게임 내 오프라인 광고 보상

접속 시 `OfflineRewardManager.Claim()`이 기본 골드·경험치를 먼저 저장한다. 선택창의 `보상 수령`은 추가 지급 없이 결과를 표시하고, `광고보고 보상 2배`는 `RewardedAdService.Show()`의 `Completed` 후 `TryGrantAdBonus(snapshot)`으로 최초 정산 금액을 한 번 더 저장한다. 현재 Google Android 보상형 테스트 ID `ca-app-pub-3940256099942544/5224354917`만 사용한다.

정산 ID·확정 금액·계정 세대는 읽기 전용 스냅샷으로 전달한다. 광고 전후와 저장 안에서 계정을 확인하고, `LocalProgression.Execute` 거래에서 추가 보상과 중복 방지 기록을 함께 저장한다. 경험치는 기존 레벨 상승 처리를 사용하며 처치·퀘스트 횟수는 추가하지 않는다. 광고로 일시정지된 사실을 유지하므로 SDK가 먼저 종료되어도 복귀를 새 오프라인 정산으로 처리하지 않는다.

선택창은 배경·뒤로가기로 닫히지 않는다. 광고 준비·시청·저장 중에는 두 버튼을 잠근다. 완료 후 저장 실패는 실행 중 기억하며 `보상 수령`으로 지급만 재시도한다. 다른 정산이 도착해도 기존 선택을 보존한다. 강제 화면 정리는 결과 팝업도 닫는다. `RewardPopupView.ShowOfflineRewards()`는 `LayerPopups`에 골드·경험치 합계를 표시하고, 광고일 때만 `보상 2배 획득`을 보여준다. 기존 우편·출석 결과 표시는 유지한다.

`OfflineRewardPopupPrefabGen.UpgradeAdRewardPrefabs()`는 기존 테마·폰트를 유지하며 실제 프리팹의 버튼과 참조를 갱신한다. 핵심 검사는 `OfflineRewardAdAcceptance.Run()`, UI 검사와 캡처는 [2026-10-28 검증 기록](../../AI/validation/offline-ad-20261028/README.md)에 있다. 강제 종료 후 미지급 광고 보너스 복구, 운영 광고·미디에이션·서버 보상 검증·일일 광고 보상은 포함하지 않는다.

결과 확인 버튼은 기존 계층의 `Popup_RewardInfo/Panel/BtnOfflineConfirm`에서 편집한다. 기본 크기는 Canvas 기준 120×100이며 LayoutElement의 Min/Preferred Width·Height를 함께 바꾸고 Flexible Width·Height는 0으로 둔다. Panel은 Control Child Size Width 켜기, Child Force Expand Width 끄기, Upper Center 정렬을 사용한다. Header·ContentScroll·RewardScroll은 Flexible Width=1로 전체 폭을 유지한다. 확인 버튼만 오프라인 결과에서 활성화하며 별도 버튼 행은 사용하지 않는다.

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

`guide_mage_tower`는 **실제 마탑 탭 → 스킬 목록 → 장착 버튼 설명 → 해제 버튼 설명**의 네 단계다. `RequestPlay("guide_mage_tower")`로 요청하거나 Unity 상단 `GameTest → Guide Test/실제 계정 저장/5 마탑 스킬` 메뉴에서 실행한다. 장착·해제 실습은 요구하지 않으며 보유 스킬이 없어도 설명 확인으로 완료한다. `MageTower` 문맥은 기존 스킬 팝업을 정상 진행 화면으로 인정하고 미확인 단계 재개 시 복원한다. 마지막 확인 또는 건너뛰기 후에는 팝업을 닫는다. 진행 기록만 저장하며 스킬 소유·장착·재화는 변경하지 않는다. 에셋 누락 복구는 `KingdomIdle/Direction/Prepare mage tower guide`로 가능하다.

안내 진행은 `Modules["game-direct:" + sequenceId]`, 실습 성공은 `Modules["game-direct-action:" + sequenceId + ":" + stepId]`에 분리 저장한다. 강화·뽑기의 `Execute` 거래 초안에 성공 영수증을 함께 넣으므로 소비 직후 종료되어도 재소비하지 않는다. 안내 JSON 저장은 성공 영수증을 덮지 않는다. 주화 지급은 `Claims`의 안내 ID·Action별 키와 50개 지급을 같은 거래에 저장한다. 이미 지급한 주화는 중단 시 회수하지 않으며 추가 지급도 없다. 저장 실패는 초안을 확정하지 않고 안내 입력을 해제한다. 계정 전환은 이전 요청·대기열·지연 콜백을 무효화한다. 환생으로 초기화하지 않는다.

`FeatureGuideInputGate`는 실제 최상단 레이캐스트 대상이 허용 버튼인지 검사한다. 밝은 구멍 안이라고 모든 터치를 통과시키지 않는다. 설명 카드와 지정된 읽기 스크롤 외 조작은 막고, 버튼 복제·부모 이동·경제 처리 대체는 하지 않는다. 필요한 패널·결과 팝업은 안내 흐름에 포함하며 무관한 모달·로딩에서는 숨기고 입력 차단을 해제한다. 돌아오면 미확인 단계의 화면을 복원한다. 자동 전투 시간 배율은 변경하지 않는다. 뒤로가기는 건너뛰기이며 정상 완료 후 메인 화면으로 돌아간다.

### 직접 실행과 검증

bootstrap부터 Play하고 메인 화면에 진입한다. Unity 상단 `GameTest → Guide Test/실제 계정 저장` 메뉴에서 다섯 개를 각각 또는 순차 요청한다. 편집 모드에서는 메뉴가 비활성화된다. 실제 계정 결과가 저장되므로 완료 후 다시 실행되지 않는 것이 정상이다. 환생 설명은 별도 미리보기 메뉴로 확인한다. 에셋 연결 복구는 편집 모드 `KingdomIdle/Direction/Prepare interactive guides`를 사용하며 기존 SO의 수동 편집은 덮어쓰지 않는다.

`KingdomIdle/Direction/Validate isolated guide acceptance`는 빈 PlayMode 씬·격리 계정·실제 프리팹·EventSystem 입력으로 검사하고 원래 편집 씬으로 돌아간다. 기존 안내 회귀, 다섯 안내 FIFO, 실제 강화·확률 뽑기, 중복 소비 방지, 지급·저장 실패와 재개를 검사한다. 장비 48개의 스크롤·강조 범위와 마탑 열기·재개도 검사하며 기록은 `AI/validation/game-direct-mage-20260921/`에 남긴다. 이전 네 실습 검증은 `AI/validation/game-direct-interactive-20260921/`에 보존한다. 화면 비율 캡처는 Editor 시뮬레이션이며 Android 실기기 결과가 아니다. 과거 0.15.0 검증 기록은 `AI/validation/game-direct-20260921/`에 보존한다.

자동 발생은 아직 연결하지 않는다. 다음 단계에서 신규 계정·스테이지 클리어 등의 외부 트리거가 해당 SO를 `RequestPlay`하도록 연결한다. 트리거는 게임 조건을 판단하고, 중복·완료 억제와 재개는 Manager에 맡긴다. 기존 계정에 안내 키가 없다는 이유만으로 신규 계정으로 판단하지 않는다.

`GameTest` 순차 실행은 현재 계정에서 미완료인 안내만 요청한다. 완료·건너뛰기 항목은 ID와 함께 `안내 생략` 일반 로그를 남기며 기록을 초기화하지 않는다. 패널 복원 직후 대상이 화면·마스크 밖에 있으면 `FeatureGuideView.TryRefreshLayout`은 false를 반환하고 호출자가 입력·참조를 정리한다. Player가 같은 미확인 단계를 다시 표시하므로 일시적인 레이아웃 미준비를 완료나 오류로 취급하지 않는다. 이 경계와 실제 `GameTest` 재개 경로의 회귀 결과는 `AI/validation/game-direct-visibility-20260921/`에 기록한다.

반복 실습은 PlayMode에서 Unity 상단 `GameTest → Guide Test/실제 계정 저장/다섯 안내 테스트 기록 초기화 (재지급 허용)` 메뉴를 실행한 뒤 개별 또는 순차 실행 메뉴를 사용한다. 초기화 메뉴는 실행 중인 안내와 대기열을 정리하고, 육성·왕국군·던전·뽑기·마탑의 **진행·실습 성공·체험 주화 지급 기록**을 한 번의 저장 거래로 지운다. 다음 실행에서는 강화·뽑기를 다시 수행하며 뽑기 주화 50개씩도 다시 지급한다. 현재 보유 재화·강화·장비·스킬은 유지되므로 반복 테스트 보상은 누적될 수 있다. 환생 안내와 다른 안내·퀘스트 기록은 유지한다. 이 초기화 경로는 `UNITY_EDITOR`에만 포함되며 계정 변경이나 저장 실패 시 초기화를 확정하지 않는다. 검증 기록은 `AI/validation/game-direct-reset-20260921/`에 남긴다.

## 전투 HUD

- `CompactHudBuilder.Apply`: `Screen_Main`, `Panel_Guide`, `Item_GuideStepRow`를 갱신한다.
- 메인 상단 두 줄: 재화 바 아래 `WaveHudView`의 스테이지 선택·처치 진행도, 그 아래 `GuideGoalView`의 가이드 한 줄. 클리어 스테이지에서는 선택 버튼과 `최고 스테이지 이동`만 표시한다. 기존 목표 이동·보상 수령 기능은 유지한다.
- `StageSelectPopupView/Controller`는 `Popup_StageSelect`를 카탈로그에서 열고 `StageManager.TryMoveToMainStage`로 이동한다. `CycleFrontierStage`는 이번 환생의 최고 도전 가능 위치이며 영구 클리어 기록과 분리한다. 구 저장은 현재 위치·이번 환생의 보스 클리어로 확인되는 위치만 복원한다.
- 던전은 메인 스테이지·가이드를 접고 상단에 목표·남은 시간을 표시한다. 하단 `메인 스테이지 복귀` → `Popup_DungeonReturnConfirm`의 확인에서만 기존 복귀 경로를 호출한다. 던전 결과 팝업과 자동 복귀는 기존 경로를 사용한다.
- 마탑 수동 스킬은 상단 스테이지 위치와 분리하여 우하단 왕국군 HUD 위에 유지한다. `StageBadgeAnchor`의 상단 모드는 위치를 움직이지 않고 기존 메뉴 숨김만 처리하며, 하단 던전 복귀 행은 왕국군 HUD 위에 정렬한다.
- 햄버거: 퀘스트/가이드, 가방, 설정, 반복 사냥 종료. 보스 자동 도전은 스테이지 인디케이터 옆에서 조작한다.
- `Panel_Guide`는 가이드·일일·주간·업적 네 탭을 제공한다. 상단 현재 퀘스트 카드는 가이드 탭에서만 표시하며 HUD와 같은 `QuestManager` 데이터를 쓴다.
- 하단 네 탭은 육성·왕국군·던전·뽑기. 시트가 열리면 HUD 목표 카드는 숨긴다.

`UguiPolishPass → UguiTypeNavPass`는 기존 스타일 보정 도구다. 스테이지 HUD의 부분 적용은 `StageHudPreparation.Apply`를 사용한다. `Screen_Main`과 두 신규 팝업, 카탈로그 참조만 저장하며 공용 스킨이나 다른 패널은 재생성하지 않는다.
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
- `StageHudValidation.Run()`: 명시적으로 연 `balance-qa-stage-hud-*` 계정의 Editor Play에서 스테이지 선택·최고 이동·던전 복귀·환생 기록과 네 화면 크기를 검사한다. 일반 계정에서는 실행을 거절한다. 결과와 캡처는 `Recordings/StageHud20261018/`, 이번 적용 검증 기록은 `AI/validation/stage-hud-20261018/report.json`에 보관한다.
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

`MageUiPreparation`은 스킬 셀, 개화 선택이 있는 상세 창, 목재·청동 뽑기 버튼과 결과 창을 준비한다. 현재 로스터는 ID 6·8을 제외한 8종이며 제거된 ID를 재사용하지 않는다. 스킬 등급은 없으며 청동 테두리와 문구로 장착, 보라색과 아이콘 변형으로 개화를 표시한다. 스킬 셀은 재사용한다. `MageSkillPresentation`은 등록 SO와 저장 상태를 읽는다. `GachaButtonFlare`는 unscaled-time 가장자리 청색 연출을 재사용하며, 저사양에서도 재화 차감 시점·취소 동작은 동일하다. 창을 닫으면 차감 전 연출을 취소하고 연속 터치에는 한 번만 차감한다.

마탑 Android 검증 도우미는 `AI/qa/mage/`, 상세 근거는 [통합 보고서](../../Docs/ArtPreparation/MAGE_INTEGRATION_VALIDATION.md)에 있다.

`MageManualCastHud`는 자동 시전을 끄면 장착 슬롯을 우측 스테이지 표시 위로 순서대로 펼치고 다시 켜면 대응하는 퇴장 애니메이션으로 접는다. 준비 상태는 점등하고 쿨다운은 시계 방향 음영과 남은 초로 표시한다. 탭은 자동 조준 시전, 드래그는 전투 지점 지정이다. `MagicAimGraphic`은 실제 원형 판정 반경을 쿼터뷰 타원으로 투영하고 전장 도트 크기에 맞춰 표시한다. 빈 슬롯은 숨기며 UI 위 드롭, 화면 밖, 전투 전환, 포커스 상실은 시전 없이 취소한다. 얼음 송곳과 유성우는 탭으로만 시전한다.

장비와 가방은 `VirtualEquipmentGrid`로 화면에 보이는 카드만 만든다. 보관함 초과 보상은 기존 수량형 저장 구조에 영구 보관하여 전투·던전·뽑기를 막지 않는다. `EquipmentData.DisplayName`은 화면용 이름이며 기존 저장 키와 아이템 코드는 유지한다. 상세 스탯은 실제 `PlayerStatus` 계산을 사용하고 변경된 계산식만 다시 그린다.

현재 검증 진입점은 `PlayabilityRevisionPreparation.Validate`, Unity 실행 검사 `KingdomIdle.UGUI.Editor.PlayabilityLiveValidation.Run`, Android `AI/qa/mage/playability_checks.py`다. 증거는 `Recordings/PlayabilityRevision`과 [플레이 개선 검증 기록](../../Docs/ArtPreparation/PLAYABILITY_20260918.md)에 있다.

0.11.1 후속 검증은 `PlayabilityRevisionPreparation.ValidateSessionRoutes`와 `AI/qa/mage/player_*.py`를 사용한다. 가이드 이동은 대상 탭을 한 번 지정하고, 스테이지 배지는 반복 사냥에서 다음 구간 도전을 제공한다. 프로필은 실제 진행 수치를 읽으며 던전 팝업은 짧은 웨이브 전환 동안 입장 요청을 유지하고 닫기 시 취소한다. [30분 × 3회 실플레이 기록](../../Docs/ArtPreparation/PLAYER_SIMULATION_20260918.md)에 수정과 성능 측정이 있다.

퀘스트 병합(2026-09-21): UI는 `QuestManager`의 snapshot/event를 읽고 수령은 `TryClaim`으로 요청한다. `LocalProgression`의 0.75초 스킬 자동 저장 성공 뒤 기존 변경 알림으로 갱신하며 별도 표시 타이머는 두지 않는다. 이동은 목표 ID까지 전달해 소환·장비·전직 탭을 선택한다. 프리팹 배치는 퀘스트 카드 작업본을 유지하고 외부 에셋 참조는 프로젝트 내 작업본으로 연결한다.

## 상점

햄버거 메뉴의 `상점`에서 목재·청동 테마 팝업을 연다. 추천·주화 / 골드·매일 광고 세 탭과 11개 상품을 표시한다. `ShopCatalog`가 안정 상품 ID·예정 가격·수량을 관리하며 `ShopPopupController`는 현재 고대주화 잔액과 기존 `QuestEconomy` 기준의 골드 환산을 읽는다. 구매·교환·광고 제거 구매는 준비 중 토스트만 표시한다. 매일 광고 두 상품은 기존 `RewardedAdService`의 Google 테스트 광고를 재생한다.

`ShopAdRewardService.Begin()`은 계정 세대·광고 시작일·지급 수량을 고정한다. `Completed`만 지급 자격으로 인정하며 `LocalProgression.Execute()` 안에서 `RewardGrants.TryApply()`와 `shop-ad:{상품 ID}:{한국 날짜}` 수령 기록을 함께 저장한다. 장비·마탑 뽑기권을 각각 2장씩 하루 한 번 지급한다. 자정 이후 완료·저장 재시도도 시작일 보상이며 새날 수령 기회와 구별한다. 기존 기간의 마지막 승인 시각을 사용해 시계 역행으로 기간이 재개되지 않게 한다. 서버 시각·서버 보상 검증은 아직 연결하지 않았다.

시청 중에는 두 광고 버튼이 잠긴다. 중단·실패는 횟수를 소비하지 않는다. 완료 후 저장 실패는 같은 실행 중 기억하고 `보상 다시 받기`로 광고 없이 재시도한다. 팝업을 닫아도 이 권리는 유지되지만 앱 강제 종료 후 미지급 보너스 복구는 지원하지 않는다. 계정 전환은 이전 요청을 거절한다. 성공 결과는 기존 `RewardPopupView.ShowRewards()`로 표시한다.

뽑기권은 로컬 지갑의 `EquipmentTicket=8`, `MageTicket=9`에 보존하며 기존 재화 ID는 바꾸지 않는다. 두 항목의 보유 수량과 오늘 수령 여부는 상점에 표시한다. **뽑기권 사용·자동 소비·고대주화 대체는 미구현**이며 기존 장비·마탑 뽑기는 그대로다. 결과 아이콘은 기존 상자·지팡이를 재사용한다.

`ShopPopupPrefabGen.Rebuild`는 상점 프리팹과 기존 메인의 상점 버튼·카탈로그 참조를 갱신한다. 상점 안내 문구는 실제 프리팹과 생성 코드에 함께 반영한다. 오프라인 보상 상한은 8시간이며 상점의 일일 광고 수령 기록과 오프라인 광고 정산은 독립적이다.

`ShopPopupPrefabGen.Validate`는 상품·프리팹 참조를 검사하고 `ValidatePlayMode`는 격리 계정에서 메뉴·탭·구매/교환 미리보기·닫기·재사용을 확인한다. 광고 지급은 `ShopAdRewardAcceptance.Run()`과 [상점 광고 검증](../../AI/validation/shop-ad-20261030/README.md)을 사용한다. 과거 미리보기만 제공하던 통합 기록은 [상점 develop 통합](../../Docs/QA/SHOP_DEVELOP_MERGE_20261028.md)에 보존한다.

Editor에서 bootstrap부터 Play·로그인한 뒤 `GameTest → Shop Ad Test → 오늘 광고 수령 초기화 (재지급 허용)`를 실행하면 현재 계정의 오늘 장비·마탑 광고 수령 기록만 삭제·저장한다. 상점이 열려 있으면 버튼도 즉시 갱신한다. 보유 뽑기권·과거 날짜·오프라인 보상 기록은 유지되며, 재시청하면 뽑기권이 누적되므로 테스트 계정에서 사용한다. 광고 준비·시청 중이거나 완료 보상의 저장이 대기 중이면 초기화를 거절한다. APK에는 이 메뉴가 포함되지 않는다. 초기화 저장 검사는 `ShopAdRewardAcceptance.RunReset()`이다.

## CheckIn·우편함 구현

메인 메뉴의 **출석 / 우편함**에서 진입한다. `GameManager`가 분리된 `CheckInService`·`MailboxService`를 소유하며, 두 기능 모두 기존 `LocalProgression.Execute`로 재화와 수령 기록을 함께 저장한다.

- **출석:** 한국시간 매월 1일 초기화, 해당 월 누적 접속일 최대 28일. 수령하지 않아도 진도가 증가하며 활성 칸을 누르면 표시된 미수령 보상을 일괄 수령한다. 구월 미수령 권리는 월 전환을 확인한 접속 시 우편으로 이월하며 발급부터 30일간 유지한다.
- **우편:** 개별·모두 받기는 대상 전체 성공 또는 전체 취소. 수령 후 원래 행·만료일을 유지하고 `수령한 우편 삭제`로 별도 제거한다. 만료 우편은 수령 여부와 무관하게 정리하며 조회에서도 제외한다.
- **표시:** 출석은 `월간 출석` 제목 아래 `MM월 출석부`·`누적 n / 28일`, 7열×4행, `다음 보상까지 HH : MM : SS`를 표시한다. 우편은 헤더·보관 목록·하단 버튼을 구분하고 전체/미수령/수령 완료 수와 `모두 받기 (n)`를 표시한다. 활성 출석 칸 이외의 슬롯과 우편 첨부는 공통 재화 설명 팝업을 연다. 28일 완료 후 카운트다운은 다음 달 1일까지 표시한다.

보상표는 [CheckInRewards.asset](../_Project/ScriptableObjects/CheckInRewards.asset)에서 편집한다. 현재 수치는 **시안 검증용**이다. 이미 열린 슬롯은 저장한 보상 명세를 유지한다. 일반 우편은 고유 ID·내용·생성/만료 UTC·재화 목록을 지정해 `GameManager.Instance.Mailbox.TryAddMail(mail)`로 발급한다. 삭제·만료 후에도 같은 ID는 재발급하지 않는다. 환영·점검 우편 자동 지급과 서버·광고 연동은 포함하지 않는다.

`UIViewCatalog`에 두 패널·공통 슬롯·우편 행·설명 팝업·보상표를 연결한다. 기존 메뉴·패널 스택·뒤로가기·재화 HUD 갱신을 재사용한다. 로컬 시계와 로컬 저장 기반이므로 시계·파일 위변조 방지는 보장하지 않는다.

보상표 오류는 신규 출석 개방을 막고 화면에 안내하지만 현재 월에 이미 확정된 보상의 수령은 허용한다. 우편 시각은 UTC 초로 전달하며 한국시간으로 표시할 수 있는 범위를 검사한다. 재발급 방지용 `mail-issued:`는 유지하고, 새 `mail-claimed:` 기록은 만들지 않는다. 기존 저장 기록은 보존한다.

현재 배치는 전용 프리팹의 `RectTransform`·기본 Layout 컴포넌트에서 편집한다. 출석 `Sheet`는 Vertical Layout Group과 Content Size Fitter로 헤더·본문·하단 높이를 합산하며 `Body/Rewards`는 **7열 / Cell Size 128×160 / Spacing 8×8**이다. 우편 첨부는 120×160, 팝업 첨부는 128×160 셀을 **가로 한 줄**로 배치하고 넘치면 가로 스크롤한다. 팝업은 폭 720이며 본문이 없으면 높이 318, 있으면 본문 스크롤 영역을 포함해 458로 기본 Layout이 조절한다. 런타임 크기 계산은 없으며 슬롯 내부는 `Item_RewardSlot`에서 편집한다. 출석 정책은 28일을 유지하며 31칸은 레이아웃 미리보기만 지원한다.

수령한 우편은 회색 배경과 어두워진 첨부로 구분하며 아이템 설명 클릭은 유지한다. 기한은 남은 일·시간·분으로 표시하고, 0.5초 확인 중 표시 분이 바뀔 때만 텍스트를 갱신한다. `1분 미만 남음`과 `만료`도 구분한다. 우편 첨부에서 시작한 세로 드래그는 바깥 우편 목록으로 전달한다.

에디터 도구:

- `KingdomIdle/UGUI/Connect CheckIn and Mailbox`: 기존 전용 프리팹 5개와 보상표를 확인해 카탈로그에 연결한다. 프리팹·보상표를 재생성하지 않는다.
- Unity 상단 `GameTest/CheckIn Test/1일 진행`: 다음 출석 슬롯 하나를 미수령 상태로 연다. `GameTest/CheckIn Test/출석 보상판 초기화`: 이번 달 진행·수령 기록을 0일로 되돌리고 보유 재화·기존 우편은 유지한다.
- Unity 상단 `GameTest/Mailbox Test`의 `골드 우편 보내기`, `보석(비전지식) 우편 보내기`, `고대주화 우편 보내기`, `전직 파편 우편 보내기`, `4종 전부 우편 보내기`: 골드 10,000·비전지식 100·고대주화 100·전직 파편 10 중 해당 재화 또는 4종을 한 통에 첨부한다. 발급부터 30일간 유지하며 실제 재화는 우편함에서 수령한다.

수동 테스트는 Editor Play에서 로그인·서비스 준비 후 실행하며 편집 모드에서는 메뉴가 비활성화된다. 씬에서 `GameTest` 컴포넌트를 선택할 필요는 없다. `Assets/_Project/Scripts/Test/GameTest.cs`의 `UNITY_EDITOR` 영역에만 포함되며 **현재 계정에 저장**된다. 출석 초기화 후 다시 받은 보상은 누적될 수 있다. 기기 시계는 변경하지 않는다.

테스트 검증 기록과 캡처는 Git 제외 경로 `PersonalDocs/Validation/checkin-mailbox-*`에 로컬 보관한다. APK·실기기 검증은 최종 요청으로 제외했다. 스크립트별 책임·메서드 호출·거래 경계·다이어그램과 Inspector 편집 위치는 [PersonalDocs 상세 설명](../../PersonalDocs/CheckIn_우편함_구현구조와_호출흐름.md)에 있다. `PersonalDocs`는 Git 제외 대상인 로컬 개인 문서 폴더다.
