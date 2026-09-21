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

`bootstrap/GameManager → Direction.GameDirectManager → FeatureGuidePlayer → UguiFeatureGuideSurface → FeatureGuideView`로 실행한다. Manager는 MonoBehaviour이며 큐·저장·계정 수명을 소유한다. Player와 UGUI 연결부는 일반 C# 클래스이고, 실제 프리팹 참조·레이아웃·입력은 MonoBehaviour View가 담당한다.

- `GameDirectSequenceSO` 두 개는 `Assets/_Project/Data/Direction/`에 있다. `first_start_menus`는 육성·왕국군·던전·뽑기 네 단계, `first_reincarnation_guide`는 상단 환생 설명 한 단계다. Inspector에서 문구·대상·카드 방향을 편집하며, 실행 시 복사하므로 원본 에셋에 진행 상태를 쓰지 않는다.
- `GameDirectManager.RequestPlay(id)`는 실제 계정의 진행을 저장하는 명시적 요청이다. 자동 트리거는 아직 없다. 동일 ID가 재생 중이거나 대기 중이면 거절하고, 서로 다른 ID는 FIFO로 한 번에 하나씩 실행한다. 완료·건너뛴 ID도 거절하며 `LastError`로 원인을 확인한다.
- `RequestPlay(id, preview: true)`는 읽기·쓰기 없는 미리보기다. 완료한 안내도 다시 볼 수 있다. `CancelCurrent()`는 완료나 건너뛰기를 기록하지 않고 현재 실행만 취소한다. 씬/팝업 때문에 가려진 안내는 입력을 풀고 같은 단계에서 대기한다.
- 진행 저장은 기존 계정 스냅샷의 `Modules["game-direct:" + id]`를 사용한다. 확인 단계는 배열 위치 대신 안정적인 단계 ID로 저장한다. 저장 성공 후에만 다음 단계로 진행하며, 실패하면 안내를 닫고 다음 요청에서 미확인 단계부터 다시 시작한다. 환생으로 초기화하지 않는다.
- `MainScreenController.Bind/Dispose`가 `FeatureGuideTargetRegistry`에 현재 다섯 버튼을 등록·해제한다. 원본 버튼을 복제하거나 이동하지 않는다. 카드·테두리는 SafeArea와 실제 RectTransform을 따라가고, 투명 입력 막은 밝은 구멍의 실제 버튼 클릭도 차단한다.
- 자동 전투는 계속된다. 다음/확인만 단계를 완료하고, 건너뛰기와 Android 뒤로가기는 별도의 건너뜀 상태를 저장한다. 로딩·팝업·화면 교체는 시스템 중단이며 사용자 건너뛰기와 다르다. 계정 변경은 실행과 큐를 취소하고 이전 계정의 늦은 응답을 거절한다.
- 오버레이는 `Prefabs/Overlays/Overlay_FeatureGuide.prefab`과 `UIViewCatalog.overlayFeatureGuide`로 연결한다. 공용 폰트와 버튼 스프라이트를 재사용하고 새 텍스처·폰트·머티리얼 에셋은 생성하지 않는다. 저사양 모드에서는 UITween의 반복 장식을 줄이며 본문과 터치는 유지한다.

### 직접 실행하고 데이터 추가하기

1. bootstrap부터 Play한 뒤 로그인하여 메인 화면으로 이동한다. 패널·로딩·팝업이 닫혀 있어야 안내가 보인다.
2. `KingdomIdle/Direction/Preview/First start menus` 또는 `Reincarnation`을 실행한다. `Cancel current`로 현재 미리보기를 중단할 수 있다.
3. 새 안내는 `Create → KingdomIdle → Direction → Sequence`로 만들고, 고유 sequence ID와 겹치지 않는 단계 ID를 설정한다. bootstrap의 GameDirectManager `sequences` 목록에 등록한 뒤 `RequestPlay`로 요청한다.
4. `KingdomIdle/Direction/Prepare guide foundation`은 최초 프리팹·데이터와 누락된 연결을 만든다. 이미 있는 프리팹·문구를 덮어쓰지 않으므로 외형 수정은 실제 프리팹에서 수행한다. 전체 UI Generate All은 필요하지 않다.

Android의 기존 `LOBBY_DEVICE_QA` 진단 빌드에서는 로컬 `lobby-command.json` 명령의 `action="guide-preview"`, `value="first_start_menus"` 또는 `"first_reincarnation_guide"`로 미리본다. `guide-cancel`은 중단이다. 일반 테스트 빌드에는 이 명령 수신기나 진단 계정 강제 지정이 들어가지 않는다.

`KingdomIdle/Direction/Validate isolated guide acceptance`는 저장된 단일 씬의 편집 모드에서 실행한다. 별도 빈 PlayMode 씬·격리 계정과 실제 UI 프리팹으로 입력·FIFO·저장·취소·재개·세 비율 배치를 검사하고 원래 편집 씬으로 복귀한다. 결과는 `AI/validation/game-direct-20260921/acceptance.json`에 기록한다. 격리 프리팹 캡처는 실제 전투나 Android 기기 검증을 대체하지 않는다.

0.15.0의 181개 검사 항목, 세 비율 캡처, Editor 실제 전투 확인과 Android 검증 범위는 [기능 안내 검증 기록](../../AI/validation/game-direct-20260921/README.md)에 정리했다.

후속 4단계에서는 신규 계정 판정과 메인 화면 준비 시점에서 최초 안내를 요청한다. 기존 계정에 저장 키가 없다고 신규 계정으로 판정하지 않는다. 5단계에서는 환생 소개 스테이지 조건을 확정하고 진입/로그인 복구 시 요청한다. 현재 환생은 스테이지 외 대기시간·보스 처치·전투 상태 조건이 있으므로 `ReincarnationService` 판정을 공유하고 기능 소개와 즉시 실행 가능 문구를 구분한다.

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
