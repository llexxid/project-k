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

`KingdomIdle → Ads → Build Android Test APK`는 이 씬만 `.adtest` 패키지로 빌드한다. 기본 결과 경로는 저장소 밖 `../QA/AdMobTest/Android`이며, 출력 기준은 `LOBBY_QA_OUTPUT`으로 변경한다. 버전과 빌드 번호는 APK 이름에 포함된다. 기존 게임 앱과 별도로 설치할 수 있으며 Build Settings의 게임 씬 목록은 바꾸지 않는다.

빌드 중 `mainTemplate.gradle needs to be updated` 창이 나오면 `Yes`로 진행한다. Unity 6의 `packaging` 문법으로 갱신하는 안내이며 외부 의존성 관리자가 이전 문법을 다시 쓸 때 나타날 수 있다.

공통 호출은 `KingdomIdle.Ads.RewardedAdService.Show(adUnitId, (result, error) => ...)`다. `Completed`만 보상 자격으로 인정하고, 보상 저장 성공은 호출자가 확인한다. `Skipped`, `Failed`, `Busy`를 구별하며 요청당 한 번만 완료를 반환한다. 광고 준비는 60초 후 실패로 해제되고, `IsBusy`/`IsShowing`으로 진행 상태를 확인한다. 현재 Google 광고만 검증 대상으로 삼으며 미디에이션 추가 시 보상·닫힘 콜백 순서를 재검증한다.

### 게임 내 오프라인 광고 보상

접속 시 `OfflineRewardManager.Claim()`이 기본 골드·경험치를 먼저 저장한다. 선택창의 `보상 수령`은 추가 지급 없이 결과를 표시하고, `광고보고 보상 2배`는 `RewardedAdService.Show()`의 `Completed` 후 `TryGrantAdBonus(snapshot)`으로 최초 정산 금액을 한 번 더 저장한다. 현재 Google Android 보상형 테스트 ID `ca-app-pub-3940256099942544/5224354917`만 사용한다.

정산 ID·확정 금액·계정 세대는 읽기 전용 스냅샷으로 전달한다. 광고 전후와 저장 안에서 계정을 확인하고, `LocalProgression.Execute` 거래에서 추가 보상과 중복 방지 기록을 함께 저장한다. 경험치는 기존 레벨 상승 처리를 사용하며 처치·퀘스트 횟수는 추가하지 않는다. 광고로 일시정지된 사실을 유지하므로 SDK가 먼저 종료되어도 복귀를 새 오프라인 정산으로 처리하지 않는다.

선택창은 배경·뒤로가기로 닫히지 않는다. 광고 준비·시청·저장 중에는 두 버튼을 잠근다. 완료 후 저장 실패는 실행 중 기억하며 `보상 수령`으로 지급만 재시도한다. 다른 정산이 도착해도 기존 선택을 보존한다. 강제 화면 정리는 결과 팝업도 닫는다. `RewardPopupView.ShowOfflineRewards()`는 `LayerPopups`에 골드·경험치 합계를 표시하고, 광고일 때만 `보상 2배 획득`을 보여준다. 기존 우편·출석 결과 표시는 유지한다.

`OfflineRewardPopupPrefabGen.UpgradeAdRewardPrefabs()`는 기존 테마·폰트를 유지하며 실제 프리팹의 버튼과 참조를 갱신한다. 핵심 검사는 `OfflineRewardAdAcceptance.Run()`이다. 강제 종료 후 미지급 광고 보너스 복구, 운영 광고·미디에이션·서버 보상 검증·일일 광고 보상은 포함하지 않는다.

결과 확인 버튼은 기존 계층의 `Popup_RewardInfo/Panel/BtnOfflineConfirm`에서 편집한다. 기본 크기는 Canvas 기준 120×100이며 LayoutElement의 Min/Preferred Width·Height를 함께 바꾸고 Flexible Width·Height는 0으로 둔다. Panel은 Control Child Size Width 켜기, Child Force Expand Width 끄기, Upper Center 정렬을 사용한다. Header·ContentScroll·RewardScroll은 Flexible Width=1로 전체 폭을 유지한다. 확인 버튼만 오프라인 결과에서 활성화하며 별도 버튼 행은 사용하지 않는다.

`bootstrap → UGUI_UIRoot(DontDestroyOnLoad) → SafeArea → 화면/패널/팝업/오버레이`.
`LoadManager → SceneRoutingBridge → UIManager.ReplaceScreen`, 탐색은 `PushPanel`을 사용한다.
화면 치수는 1080px 기준이며 실제 CanvasScaler·SafeArea는 루트 프리팹에서 확인한다.

프리팹을 직접 수정하거나 대상 프리팹만 마이그레이션한다. **Generate All은 수동 편집을 덮어쓰는 초기 뼈대 생성기**다.
새 위젯은 View 참조 → Items 프리팹 → UIViewCatalog 참조 → 컨트롤러 데이터 바인딩 순서로 추가한다.
목록 생성은 패널을 열 때 수행한다. 큰 목록은 가상화·풀링을 검토한다.

## 공통 연출과 기능 안내

### 전체 화면 뽑기 (0.20.0)

`UIViewCatalog.panelGacha → Panel_Gacha`는 기존 `SafeArea/LayerPanels` 안에서 하단 메뉴까지 채운다. 루트와 `Sheet`는 Stretch·Offset 0이고 `SheetSizeFitter`와 `SheetClip`은 없다. `BottomSheetView.fullScreen`일 때 UIManager의 강제 크기·위치 변경을 건너뛰며 unscaled 0.15초 페이드만 사용한다. `PartyHudSuppressor`는 퇴장이 끝날 때까지 왕국군·수동 시전 HUD를 가리고 입력 통과를 막는다.

제목·탭·상세 버튼·천장·재화·뽑기 버튼은 고정 높이, 중앙 아트만 남는 높이를 사용한다. `GachaPanelView.authoredContent`가 프리팹에 저장된 본문을 연결한다. `Popup_GachaRewards`는 열 때만 기존 보상 카드를 구성하며 `ModalBackHandler`로 닫힌다. 팝업은 패널의 자식이므로 가림·종료 수명을 함께 따른다. 장비 천장은 서버 최종 누적값을 `N / 40회`와 게이지로 표시하며 마탑에서는 숨긴다.

`UIViewCatalog.gachaRatesUrl`은 공개 확률표 주소다. 현재 탭에 따라 `#equipment` 또는 `#mage-tower`를 외부 브라우저로 연다. 사이트 소스는 게임 저장소 밖 `바탕화면/Ludos-Interactive/gacha-rates`에서 관리한다. 해당 폴더의 `generate_rates.py`는 서버 소스와 클라이언트 카탈로그를 대조하고 페이지와 검증 기록을 만든다.

미리보기와 결과창은 아이콘이 없는 재화 보상에 `RewardPresentation.Icon`의 공통 이미지를 사용한다. `GachaFullScreenValidation.CheckResultCurrencyIcons()`는 Play Mode에서 서버 DTO 변환·전직 파편/비전 지식 아이콘·합산 수량을 표시용 데이터로 검사한다([0.20.1 검증 기록](../../AI/validation/gacha-result-icons-20261102/README.md)).

기존 프리팹은 `Panel_Gacha_Old`에 원 GUID와 구조를 유지한다. `GachaFullScreenPreparation.Prepare()`는 최초 한 번만 Old로 이동하고 이후 새 패널·보상 팝업만 재생성한다. 전체 생성기와 polish는 Old를 덮어쓰지 않는다. 정상 로그인 후 `GachaFullScreenValidation.Run()`으로 비율·안전 영역 모의·페이드·팝업·천장 표시·HUD 복원을 검사한다. 캡처와 Editor 성능 표본은 `Recordings/Gacha20261101`, 결과 요약은 `AI/validation/gacha-fullscreen-20261101`에 보관한다.

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

`KingdomIdle/Direction/Validate isolated guide acceptance`는 빈 PlayMode 씬·격리 계정·실제 프리팹·EventSystem 입력으로 검사하고 원래 편집 씬으로 돌아간다. 기존 안내 회귀, 다섯 안내 FIFO, 실제 강화·확률 뽑기, 중복 소비 방지, 지급·저장 실패와 재개를 검사한다. 장비 48개의 스크롤·강조 범위와 마탑 열기·재개도 검사한다. 화면 비율 캡처는 Editor 시뮬레이션이며 Android 실기기 결과가 아니다.

자동 발생은 아직 연결하지 않는다. 다음 단계에서 신규 계정·스테이지 클리어 등의 외부 트리거가 해당 SO를 `RequestPlay`하도록 연결한다. 트리거는 게임 조건을 판단하고, 중복·완료 억제와 재개는 Manager에 맡긴다. 기존 계정에 안내 키가 없다는 이유만으로 신규 계정으로 판단하지 않는다.

`GameTest` 순차 실행은 현재 계정에서 미완료인 안내만 요청한다. 완료·건너뛰기 항목은 ID와 함께 `안내 생략` 일반 로그를 남기며 기록을 초기화하지 않는다. 패널 복원 직후 대상이 화면·마스크 밖에 있으면 `FeatureGuideView.TryRefreshLayout`은 false를 반환하고 호출자가 입력·참조를 정리한다. Player가 같은 미확인 단계를 다시 표시하므로 일시적인 레이아웃 미준비를 완료나 오류로 취급하지 않는다.

반복 실습은 PlayMode에서 Unity 상단 `GameTest → Guide Test/실제 계정 저장/다섯 안내 테스트 기록 초기화 (재지급 허용)` 메뉴를 실행한 뒤 개별 또는 순차 실행 메뉴를 사용한다. 초기화 메뉴는 실행 중인 안내와 대기열을 정리하고, 육성·왕국군·던전·뽑기·마탑의 **진행·실습 성공·체험 주화 지급 기록**을 한 번의 저장 거래로 지운다. 다음 실행에서는 강화·뽑기를 다시 수행하며 뽑기 주화 50개씩도 다시 지급한다. 현재 보유 재화·강화·장비·스킬은 유지되므로 반복 테스트 보상은 누적될 수 있다. 환생 안내와 다른 안내·퀘스트 기록은 유지한다. 이 초기화 경로는 `UNITY_EDITOR`에만 포함되며 계정 변경이나 저장 실패 시 초기화를 확정하지 않는다.

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
- `StageHudValidation.Run()`: 명시적으로 연 `balance-qa-stage-hud-*` 계정의 Editor Play에서 스테이지 선택·최고 이동·던전 복귀·환생 기록과 네 화면 크기를 검사한다. 일반 계정에서는 실행을 거절한다.
- `KingdomIdle/UGUI/Run client regression checks`: 기존 클라이언트 회귀 검사.
- `KingdomIdle/UGUI/Apply compact battle HUD`: 이번 HUD 프리팹 적용.
- `KingdomIdle/UGUI/Apply quest category tabs` / `CompactHudBuilder.ApplyQuestTabs`: 퀘스트 패널의 탭 컨테이너·직렬화 참조만 적용.
- `KingdomIdle/UGUI/Apply quest reward cards` / `QuestCardPrefabBuilder.Apply`: 기존 행 프리팹의 GUID와 참조를 보존하며 보상·진행바·행동 버튼을 연결한다. 기존 전체 생성기도 같은 업그레이드를 사용한다.
- `KingdomIdle.UGUI.Editor.QuestTabAcceptance.Run()`: 빈 검사 씬의 PlayMode에서 실제 프리팹 복제본·격리 계정으로 탭 선택, 수령 완료 유지, 재활성화, 중복 수령 방지, 기간 초기화를 검사한다. 운영 계정이 열린 씬에서는 실행을 거절한다.
- `TitleLobbyDeviceBuild.Build`: `.lobbyqa` ARM64 Development 진단 APK. `BuildForManualTesting`은 같은 패키지를 사용하면서 자동 진단·테스트 계정 주입을 제외한다. `LOBBY_QA_OUTPUT`으로 출력 위치, `DEVICE_BUILD_PURPOSE`로 용도를 지정한다. 앱·APK 이름에는 용도와 버전·빌드 번호를 넣으며 빌드마다 versionCode가 증가한다. 성공한 APK 경로와 메타데이터는 출력 폴더의 `build.json`에서 읽는다. 기기 정리·최종 설치 기준은 [프로젝트 지침](../../AGENTS.md)을 따른다.
- `BalanceEditorValidation.BuildAndroidForManualTesting`: 밸런스·저장 데이터 이관·직렬화 참조 검사를 실행하고 글리프를 준비한 뒤 직접 플레이용 APK를 만든다.
- `CompactHudBuilder.ApplyAndBuild`: HUD 적용 후 위 Android 빌드.
- `BattleHudDeviceProbe`: QA 빌드 전용 HUD 상태·레이아웃·진행 fixture. 일반 배포에는 포함되지 않는다.

Android 검사 도우미와 실행 결과는 저장소 밖 QA 보관 폴더에서 관리한다.

## 마탑·뽑기

`MageUiPreparation`은 스킬 셀, 개화 선택이 있는 상세 창, 목재·청동 뽑기 버튼과 결과 창을 준비한다. 현재 로스터는 ID 6·8을 제외한 8종이며 제거된 ID를 재사용하지 않는다. 스킬 등급은 없으며 청동 테두리와 문구로 장착, 보라색과 아이콘 변형으로 개화를 표시한다. 스킬 셀은 재사용한다. `MageSkillPresentation`은 등록 SO와 저장 상태를 읽는다. `GachaButtonFlare`는 unscaled-time 가장자리 청색 연출을 재사용하며, 저사양에서도 재화 차감 시점·취소 동작은 동일하다. 창을 닫으면 차감 전 연출을 취소하고 연속 터치에는 한 번만 차감한다.

마탑 Android 검증 도우미와 상세 기록은 저장소 밖 QA 보관 폴더에서 관리한다.

`MageManualCastHud`는 자동 시전을 끄면 장착 슬롯을 우측 스테이지 표시 위로 순서대로 펼치고 다시 켜면 대응하는 퇴장 애니메이션으로 접는다. 준비 상태는 점등하고 쿨다운은 시계 방향 음영과 남은 초로 표시한다. 탭은 자동 조준 시전, 드래그는 전투 지점 지정이다. `MagicAimGraphic`은 실제 원형 판정 반경을 쿼터뷰 타원으로 투영하고 전장 도트 크기에 맞춰 표시한다. 빈 슬롯은 숨기며 UI 위 드롭, 화면 밖, 전투 전환, 포커스 상실은 시전 없이 취소한다. 얼음 송곳과 유성우는 탭으로만 시전한다.

장비와 가방은 `VirtualEquipmentGrid`로 화면에 보이는 카드만 만든다. 보관함 초과 보상은 기존 수량형 저장 구조에 영구 보관하여 전투·던전·뽑기를 막지 않는다. `EquipmentData.DisplayName`은 화면용 이름이며 기존 저장 키와 아이템 코드는 유지한다. 상세 스탯은 실제 `PlayerStatus` 계산을 사용하고 변경된 계산식만 다시 그린다.

`InventoryPanelController`의 재료·기타 탭은 기존 `EquipCellView`에 `RewardPresentation`의 아이콘·이름과 지갑 보유 수량을 연결한다. 강화석은 재료, 장비·마탑 뽑기권은 기타에 표시하며 수량이 0이면 슬롯을 숨기고 모두 없으면 빈 상태 안내를 표시한다. 재화 카드는 공통 청동색을 사용하고 장착·스탯·희귀도 동작은 제공하지 않는다. 클릭 설명은 `RewardPopupView.ShowItem()`을 재사용하며 잔액 변경 시 표시를 갱신한다. 전체·장비 탭의 구성과 장비 필터는 유지하고, 인벤토리 전용 수량 저장이나 뽑기권 사용 경로는 추가하지 않는다.

현재 검증 진입점은 `PlayabilityRevisionPreparation.Validate`, Unity 실행 검사 `KingdomIdle.UGUI.Editor.PlayabilityLiveValidation.Run`, Android `AI/qa/mage/playability_checks.py`다. 증거는 `Recordings/PlayabilityRevision`과 [플레이 개선 검증 기록](../../Docs/ArtPreparation/PLAYABILITY_20260918.md)에 있다.

세션 이동 경로는 `PlayabilityRevisionPreparation.ValidateSessionRoutes`로 검사한다. 가이드 이동은 대상 탭을 한 번 지정하고, 스테이지 배지는 반복 사냥에서 다음 구간 도전을 제공한다. 프로필은 실제 진행 수치를 읽으며 던전 팝업은 짧은 웨이브 전환 동안 입장 요청을 유지하고 닫기 시 취소한다.

퀘스트 병합(2026-09-21): UI는 `QuestManager`의 snapshot/event를 읽고 수령은 `TryClaim`으로 요청한다. `LocalProgression`의 0.75초 스킬 자동 저장 성공 뒤 기존 변경 알림으로 갱신하며 별도 표시 타이머는 두지 않는다. 이동은 목표 ID까지 전달해 소환·장비·전직 탭을 선택한다. 프리팹 배치는 퀘스트 카드 작업본을 유지하고 외부 에셋 참조는 프로젝트 내 작업본으로 연결한다.

## 상점

햄버거 메뉴의 `상점`에서 목재·청동 테마 팝업을 연다. 추천·주화 / 골드·매일 광고 세 탭과 11개 상품을 표시한다. `ShopCatalog`가 안정 상품 ID·예정 가격·수량을 관리하며 `ShopPopupController`는 현재 고대주화 잔액과 기존 `QuestEconomy` 기준의 골드 환산을 읽는다. 구매·교환·광고 제거 구매는 준비 중 토스트만 표시한다. 매일 광고 두 상품은 기존 `RewardedAdService`의 Google 테스트 광고를 재생한다.

`ShopAdRewardService.Begin()`은 계정 세대·광고 시작일·지급 수량을 고정한다. `Completed`만 지급 자격으로 인정하며 `LocalProgression.Execute()` 안에서 `RewardGrants.TryApply()`와 `shop-ad:{상품 ID}:{한국 날짜}` 수령 기록을 함께 저장한다. 장비·마탑 뽑기권을 각각 2장씩 하루 한 번 지급한다. 자정 이후 완료·저장 재시도도 시작일 보상이며 새날 수령 기회와 구별한다. 기존 기간의 마지막 승인 시각을 사용해 시계 역행으로 기간이 재개되지 않게 한다. 서버 시각·서버 보상 검증은 아직 연결하지 않았다.

시청 중에는 두 광고 버튼이 잠긴다. 중단·실패는 횟수를 소비하지 않는다. 완료 후 저장 실패는 같은 실행 중 기억하고 `보상 다시 받기`로 광고 없이 재시도한다. 팝업을 닫아도 이 권리는 유지되지만 앱 강제 종료 후 미지급 보너스 복구는 지원하지 않는다. 계정 전환은 이전 요청을 거절한다. 성공 결과는 기존 `RewardPopupView.ShowRewards()`로 표시한다.

뽑기권은 로컬 지갑의 `EquipmentTicket=8`, `MageTicket=9`에 보존하며 기존 재화 ID는 바꾸지 않는다. 두 항목의 보유 수량과 오늘 수령 여부는 상점에 표시한다. **뽑기권 사용·자동 소비·고대주화 대체는 미구현**이며 기존 장비·마탑 뽑기는 그대로다. 결과 아이콘은 기존 상자·지팡이를 재사용한다.

`ShopPopupPrefabGen.Rebuild`는 상점 프리팹과 기존 메인의 상점 버튼·카탈로그 참조를 갱신한다. 상점 안내 문구는 실제 프리팹과 생성 코드에 함께 반영한다. 오프라인 보상 상한은 8시간이며 상점의 일일 광고 수령 기록과 오프라인 광고 정산은 독립적이다.

`ShopPopupPrefabGen.Validate`는 상품·프리팹 참조를 검사하고 `ValidatePlayMode`는 격리 계정에서 메뉴·탭·구매/교환 미리보기·닫기·재사용을 확인한다. 광고 지급 검사는 `ShopAdRewardAcceptance.Run()`을 사용한다.

Editor에서 bootstrap부터 Play·로그인한 뒤 `GameTest → Shop Ad Test → 오늘 광고 수령 초기화 (재지급 허용)`를 실행하면 현재 계정의 오늘 장비·마탑 광고 수령 기록만 삭제·저장한다. 상점이 열려 있으면 버튼도 즉시 갱신한다. 보유 뽑기권·과거 날짜·오프라인 보상 기록은 유지되며, 재시청하면 뽑기권이 누적되므로 테스트 계정에서 사용한다. 광고 준비·시청 중이거나 완료 보상의 저장이 대기 중이면 초기화를 거절한다. APK에는 이 메뉴가 포함되지 않는다. 초기화 저장 검사는 `ShopAdRewardAcceptance.RunReset()`이다.

## 서버 우편함·공통 보상 표시

0.19.4에서는 머지 중 교체됐던 우편함의 가상 스크롤·연속 수령·검증·복구를 기존 공통 표시 경로 위에 복원했다. 메인 메뉴의 우편함은 `OnGetMailList`·`OnClaimMailReward`를 사용한다. `GameManager.Mailbox`가 서버 요청과 메모리 목록을 소유하고 `MailboxPanelController`는 화면 상태와 버튼을 연결한다. 서버 저장소·DTO 계약은 변경하지 않는다.

- 열 때 50통씩 최대 두 페이지(총 100통)를 받고, 전체 조회 완료 후 수령을 허용한다. 닫으면 목록을 비운다. 페이지 간 중복·개수 불일치 시 0부터 한 번만 재조회한다.
- 스택 종료 훅에서 컨트롤러를 즉시 비활성화해 구독과 연결을 정리한다. 퇴장 애니메이션 뒤의 파괴가 새 우편함을 닫지 않으며, 다른 패널에 가렸다 복귀할 때는 기존 OnEnable/OnDisable로 재연결한다.
- 개별 수령은 long MailId를 사용한다. 모두 받기는 클릭 시점의 ID를 최대 50개씩 순차 처리한다. 중간 실패 시 성공분은 유지하며 결과를 표시하고 목록을 다시 조회한다.
- 재화·장비는 응답의 절대값을 기존 UserManager 경로로 반영한다. 응답 유실·저장 실패는 수령을 재전송하지 않고 플레이어 상태를 재조회한다. 화면 종료 후에도 같은 계정의 수령 결과는 반영하며 다른 계정의 응답은 무시한다.
- 수령 성공한 우편은 즉시 제거한다. 기존 삭제 버튼은 비활성 상태로 보존하고 `수령 시 자동 삭제`를 안내한다. 조회 오류에서는 하단 버튼이 `다시 불러오기`가 된다.

운영 목록은 `MailboxService.Messages`의 `MailDTO`만 사용하고 ID는 long을 유지한다. Open/Close/RequestRefresh/TryClaim/TryClaimAll을 통해 조작하며 Changed와 ClaimCompleted로 UI를 갱신한다. GameManager의 우편 주기 조회는 없고, 열린 Controller가 남은 시간과 서비스 Tick(만료·실패 재시도)을 확인한다. Empty/Loading/Ready/Claiming/Recovering/RecoveryRequired 상태를 구별한다. 조회 실패는 창이 열린 동안 30초 후 자동 재시도하며 수동 재시도는 즉시 실행한다. 복구 실패에는 자동 수령 재전송이 없고, 플레이어와 우편 목록을 모두 확인하기 전에는 수령을 차단한다.

`VirtualizedMailList`는 기존 ScrollRect·Item_MailRow를 유지하면서 보이는 행 + 2행을 재사용한다. Content의 자동 Layout/Fitter는 실행 중 해제하고, 전체 높이와 행 위치만 직접 관리한다. 행 내부의 기존 배치는 유지한다. 우편당 보상 슬롯은 서버 한도인 10개까지 미리 준비하여 스크롤 중 생성·삭제하지 않는다. 행 재연결 시 보상·클릭 대상·만료 표시·가로 스크롤 위치를 갱신한다.

우편 첨부는 기존 가로 목록이며 재화와 장비를 모두 표시한다. `RewardGrants.FromServer → RewardSlotView → RewardPopupView`를 출석과 함께 사용한다. RewardGrant의 ServerReward·EquipmentCode·Disposition은 저장하지 않는 표시 전용 정보이며 로컬 지급에 사용할 수 없다. 장비 아이콘·이름은 기존 EquipmentDatabase에서 찾으며 희귀도는 장비에만 적용한다. 획득 결과는 서버의 자동 분해·대기·예비 보관 결과를 표시하고 종류·강화·만료·보관 위치가 같은 장비만 합산한다. 자동 분해는 실제 받은 강화석 수량과 자동 분해 문구로 표시한다. 공통 팝업의 기존 재접속 보상 경로는 유지한다.

`MailText`의 작은 문구표에서 TitleKey/BodyKey를 변환한다. 미등록 키는 `시스템 우편` / `첨부 보상을 확인해 주세요.`로 표시하고 키를 진단 기록에 남긴다. 서버 발송 경로가 추가되면 실제 키와 문구를 이 표에 등록한다.

이번 개정은 우편함으로 한정한다. 현재 머지된 출석·광고·재접속 보상 정책은 변경하지 않으며 출석 이월 알림은 우편함 인스턴스의 갱신 요청에만 연결한다.

과거 로컬 MailMessage·Claims 데이터는 저장 호환성을 위해 유지하지만 운영 우편함은 읽거나 지급하지 않는다. LocalProgression 전체와 공통 RewardGrant는 다른 기능에서도 사용하므로 유지한다. 기존 로컬 우편 생성·지급·삭제는 Editor 전용 LocalMailboxService로 분리하고 격리 테스트 계정만 허용한다.

검증 진입점:

- `GameTest/Mailbox Test/격리 로컬 지급 검증`: Play 종료 후 기존 BeginTestSession/OpenTestAccount로 저장 실패·지급·중복·삭제를 검사하고 원래 계정·상태를 복원한다.
- `GameTest/Mailbox Test/서버 응답 시나리오 검증`: 실제 계정·통신 없이 페이지/부분 실패/닫기/계정 변경/응답 유실을 검사한다.
- `GameTest/Mailbox Test/서버 응답 실제 저장 검증`: Play 종료 후 `MailboxAcceptance.RunServerApply()`로 일반·대기·예비 보관/자동 분해 × 성공/저장 실패/응답 유실을 검사한다. 응답·복구 콜백은 격리하고 제품의 우편 적용·파일 저장·재열기 경로를 실행하며 원래 계정·상태를 복원한다.
- `MailboxUiAcceptance.Open(100)` / `VerifyRecycling()`: bootstrap Play에서 실제 프리팹을 사용하는 격리 미리보기. 서버 통합 검증과 구분한다.
- `MailboxUiAcceptance.VerifyCloseReopen()`: bootstrap Play에서 이전 패널의 지연 종료·재진입, 비활성화 후 복귀, 재진입 후 수령을 검사한다. 검사 후 `Close()`로 미리보기를 정리한다.
- `MailboxUiAcceptance.VerifyAnchorAndCompatibility()`: 수령 후 스크롤 기준 유지·오류/재시도·출석 슬롯과 재접속 팝업 호환성을 확인한다.
- [0.19.4 검증 기록](../../AI/validation/mailbox-refactor-20261101/README.md)
- [0.19.0 당시 검증 기록](../../AI/validation/mailbox-server-20261031/README.md)
