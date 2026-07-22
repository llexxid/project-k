# UGUI 인게임 UI 구조 안내 (팀 개발자용)

UI Toolkit → UGUI(+TextMeshPro) 전면 이식본입니다. 이 문서만 보면 **어디를 어떻게 고치는지** 알 수 있습니다.

---

## 1. 폴더 구조

```
Assets/UGUI/
├── Prefabs/            ← 화면/패널/팝업/HUD/아이템 프리팹 (여기서 인스펙터로 편집)
│   ├── UGUI_UIRoot.prefab       루트 캔버스 + 매니저들 (bootstrap 씬에 배치됨)
│   ├── Screens/                 Screen_Title, Screen_Main
│   ├── Panels/                  Panel_Guide/Gacha/KingdomArmy/Development/Inventory/Placeholder
│   ├── Popups/                  Popup_GachaResult
│   ├── Overlays/                Overlay_Loading/Toast/Settings
│   ├── Huds/                    Hud_Party, Hud_MageTower
│   └── Items/                   반복 위젯 (탭버튼/카드/뽑기버튼/알약/액션버튼 등)
├── Scripts/
│   ├── Core/            UIManager, UIViewCatalog, UguiTheme, UguiRuntimeFactory, UguiPixelSkin ...
│   ├── Views/           각 화면/패널의 View(직렬화 참조) + Controller(로직)
│   │   └── Items/       아이템 프리팹의 View 컴포넌트
│   ├── Hud/             파티/마탑 HUD, 데미지 텍스트
│   └── Bridges/         씬 라우팅·로딩·데미지텍스트·마탑 브릿지
├── Editor/             프리팹 생성기 (아래 "재생성" 참고)
├── Sprites/            절차 생성 스프라이트(RoundedRect 등)
├── Fonts/              데미지 텍스트용 TMP 머티리얼
└── UIViewCatalog.asset ★ 모든 프리팹/폰트/SFX/아이콘의 중앙 배선표
```

픽셀 아트 원본은 `Assets/UI Toolkit/Art/` (폰트 Galmuri11 SDF, 9-slice 패널/버튼/바, 아이콘 600여 개).

---

## 2. 부팅 흐름

```
bootstrap.unity
 └ UGUI_UIRoot (프리팹 인스턴스, DontDestroyOnLoad)
    ├ Canvas(Overlay, sortingOrder 10) + CanvasScaler(1080×1920, Match 0.5)
    ├ UIManager  ← UIViewCatalog.asset 참조
    │   └ 씬 전환 시 카탈로그의 Screen/Panel 프리팹을 Instantiate
    └ SafeArea / LayerScreens / LayerPanels / LayerPopups / LayerOverlays
```

- **화면 전환**: `LoadManager` 이벤트 → `SceneRoutingBridge` → `UIManager.ReplaceScreen(UIScreenId)`
- **패널 열기**: 하단 탭/햄버거 → `UIManager.PushPanel(UIPanelId)` → 카탈로그 프리팹 Instantiate → 컨트롤러 `Populate(view)`
- **API는 UITK 시절과 동일**: `UIManager.Instance.PushPanel/ShowToast/ShowGachaResultPopup ...`

---

## 3. "무엇을 고칠 때 어디를 만지나"

| 바꾸고 싶은 것 | 위치 | 방법 |
|---|---|---|
| 패널/팝업/화면 레이아웃·색·크기 | `Prefabs/**` | **인스펙터에서 직접 편집** (일반 UGUI 프리팹) |
| 반복 위젯(탭버튼/카드/뽑기버튼/알약/액션버튼) | `Prefabs/Items/**` | 프리팹 하나만 고치면 전 화면 반영 |
| 공통 색/폰트크기/치수 토큰 | `Scripts/Core/UguiTheme.cs` | 상수 수정 (생성기·런타임 공용) |
| 버튼 픽셀 스킨(Blue/Green/Grey 매핑) | `Scripts/Core/UguiPixelSkin.cs` | |
| 패널 안 **동적 콘텐츠**(전직/강화/스탯표 등) | `Scripts/Views/*Controller.cs` | 코드에서 데이터 바인딩. 위젯은 아이템 프리팹 사용 |
| 카탈로그에 프리팹 새로 연결 | `UIViewCatalog.asset` | 필드에 드래그, 또는 생성기에 추가 |

> ⚠️ **프리팹을 수정한 뒤 생성기를 다시 돌리면 덮어씁니다.** 생성기는 "초기 뼈대 자동 생성"용입니다. 팀이 프리팹을 손보기 시작하면, 구조를 바꿀 때만 생성기를 쓰고 평소엔 프리팹을 직접 편집하세요. (색/크기 조정은 프리팹에서 하는 걸 권장)

---

## 4. 프리팹 재생성 (초기 뼈대/구조 변경 시)

Unity 에디터 메뉴:
- `KingdomIdle → UGUI → Generate All (prefabs + catalog)` — 전 프리팹 + 카탈로그 재생성 + 배선 검증
- `KingdomIdle → UGUI → Validate → Check view wiring` — missing script/빈 필드 점검
- `KingdomIdle → UGUI → Bootstrap → Switch to UGUI / back to UITK` — bootstrap 씬 UI 시스템 토글

에디터를 닫고 배치로도 가능:
```
Unity.exe -batchmode -quit -projectPath <프로젝트> \
  -executeMethod KingdomIdle.UGUI.Editor.UguiGenMenu.GenerateAll -logFile gen.log
```

**렌더 미리보기(플레이 없이 UI 외형 확인)**:
```
Unity.exe -batchmode -quit -projectPath <프로젝트> \
  -executeMethod KingdomIdle.UGUI.Editor.UguiPreviewCapture.CaptureAll -logFile prev.log
# 결과 PNG: %TEMP%/ugui_preview/
```

---

## 5. 동적 콘텐츠(런타임 생성)와 프리팹의 경계

- **단순·반복 위젯 → 프리팹**: 탭/네비 버튼, 가챠 카드, 뽑기 옵션 버튼, 확률 알약, 액션 버튼, 재화 라인.
  컨트롤러가 `Instantiate` 후 `View.Set(...)`로 데이터만 넣습니다. 외형은 프리팹에서 편집.
- **복잡·가변 레이아웃 → 코드 생성**(`UguiRuntimeFactory`): 캐릭터 시트, 스탯 비교표, 전직 상세 등.
  자주 커스텀하는 부분이 생기면 아이템 프리팹으로 승격하세요 (아래 6번 패턴).

**성능 메모**
- 패널 콘텐츠는 열 때 1회 생성되고 닫으면 파괴됩니다(가벼움). 매 프레임 재생성 없음.
- 스크롤은 `ScrollRect + RectMask2D`(가벼운 마스크). 목록이 수백 개로 커지면 가상화/풀링을 고려하세요.
- 장식 이미지·텍스트는 `raycastTarget=false` 기본. 상호작용 요소만 raycast 켬.
- 데미지 텍스트는 이미 오브젝트 풀링(`DamageTextManager`, warm 24).

---

## 6. 새 위젯을 프리팹화하는 패턴 (예시)

1. `Scripts/Views/Items/XxxItemView.cs` — `[SerializeField] internal` 참조 + `Set(...)` 메서드
2. `Editor/ItemGens.cs`에 `GenerateXxx()` 추가 (뼈대 생성 + 참조 배선)
3. `UIViewCatalog.cs`에 `public GameObject itemXxx;` 필드 추가
4. `Editor/CatalogGen.cs`의 `AssignPrefabs`에 `catalog.itemXxx = Load(...)` 추가
5. `Editor/UguiGenMenu.cs`의 GenerateAll에 `ItemGens.GenerateXxx()` 추가
6. 컨트롤러에서 `Instantiate(catalog.itemXxx)` → `view.Set(...)`

가챠 뽑기 버튼(`GachaPullButtonView` + `Item_GachaPullButton`)이 이 패턴의 참고 예시입니다.

---

## 7. 자주 쓰는 진입점

- `UIManager.Instance` — 화면/패널/토스트/로딩/뒤로가기
- `UIViewCatalog` (`UIManager.Instance.Catalog`) — 프리팹/폰트/아이콘 참조
- `UguiTheme` — 색/치수/폰트크기 토큰
- `UguiRuntimeFactory` — 런타임 UI 헬퍼(Box/Label/TextButton/PixelWindow/PixelCard/스크롤 등)
- `DamageTextBridge.ShowOnTransform(...)` — 게임플레이에서 데미지 숫자
- `EconomyBridge` (Assets/Scripts/Core) — 재화 조회/증감
