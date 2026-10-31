# 보상 확인 버튼 크기 수정

## 0.17.2 — 기존 계층으로 복귀 (현재)

- 사용자 요청에 따라 OfflineActions 오브젝트와 런타임의 부모 활성화 코드를 제거했다. 확인 버튼은 Panel 마지막 직접 자식이며 120×100을 유지한다. 기존 Inspector 저장 상태는 `f269a3755`에 보존했다.
- Panel의 Child Force Expand Width만 끄고 Upper Center 정렬을 사용한다. Header와 ContentScroll의 기존 LayoutElement Flexible Width를 1로 변경했다. RewardScroll의 Flexible Width=1은 유지한다. 새 오브젝트·컴포넌트·Graphic·텍스처는 추가하지 않았다.
- Unity 6000.3.21f1 컴파일 통과. 실제 프리팹 계층은 Sheen/Header/RewardScroll/ContentScroll/BtnOfflineConfirm이다. 생성 코드에도 같은 설정을 반영했다.
- [27개 표시·닫기 검사 통과](checks-flat.json): 일반/2배 결과 크기·텍스트·중앙 정렬·닫기, 공통 재화 결과, 우편 본문, 우편 슬롯에서 아이템 상세 열기, 상세만 X로 닫기, 우편 뒤로가기, 독립 재화 설명. [현재 검사 스크립트](check-layout.cs)는 표시용 데이터만 사용하고 보상을 지급하지 않는다.
- 실제 로그인→main에서 프레임 1122→5901 진행을 확인하고, 화면 확인 중 전투 시간을 일시정지한 뒤 복원했다. 확인 버튼 120×100, 제목 텍스트 영역 512, 보상 영역 672 논리 픽셀을 확인했다.
- 1080×1920 캡처: [일반 오프라인 결과](flat-normal.png), [우편 표시](flat-mail.png), [중첩 아이템 상세](flat-item.png). 우편·아이템은 표시 전용 데이터다. 종료 후 팝업·시간 배율·background 설정을 복원했다.
- 이번 변경은 UI 표시 검증이며 광고 지급 검사를 다시 수행하거나 Android APK를 재빌드하지 않았다. 아래 0.17.1 기록과 `checks.json`은 이전 방식의 검증 출처로 보존한다.

## 0.17.1 — 이전 버튼 행 방식

- Unity 6000.3.21f1 / 게임 0.17.1. 사용자 Inspector 변경은 `36a1411cb`에 먼저 보존했다. 기존 EDM 설정 변경은 수정하지 않았다.
- 원인 재현: 공용 Panel의 Child Force Expand Width를 끄자 Header 폭이 672→0이 되었다. 버튼은 Flexible Width=1 때문에 여전히 672로 늘어났다. 버튼 Label 자체의 여백은 0이므로 폰트나 여백 문제는 아니었다.
- 공용 Panel을 유지하고 OfflineActions 행에서 확인 버튼을 120×100, 가운데 정렬, Flexible Width/Height=0으로 제한했다. 실제 프리팹·생성 코드·오프라인 결과 진입점에 반영했다.
- 실제 로그인→main에서 정상 수령, 확인 클릭 닫기를 검증했다. 프레임 389→4465 증가를 확인했다. 광고 결과 캡처는 같은 정산을 이용한 **표시 전용 미리보기**이며 추가 보상이나 광고 재검증은 수행하지 않았다.
- [일반 결과](normal-result.png), [2배 표시 미리보기](doubled-preview.png): 1080×1920, 버튼과 Label 모두 120×100, 글자 overflow 없음. [변경 전 화면](../offline-ad-20261028/doubled-result.png)은 540×960 캡처이므로 화면 픽셀 크기 직접 비교와 구별한다.
- 일반 팝업의 기본 버튼 활성 상태가 이전 생성 코드로 저장된 것을 검사에서 발견했다. 최신 생성 코드를 다시 적용하여 버튼과 행을 모두 기본 비활성화한 뒤 [11개 표시·닫기 검사](checks.json)를 통과했다. [검사 스크립트](check-layout.cs)는 재화 지급 없이 실행한다.
- 새 텍스처·Graphic은 없으며 레이아웃 행 객체 1개만 추가했다. 이번 작은 UI 수정은 Editor 검증만 했고 Android 재빌드·기기 검증은 하지 않았다. 기존 b65 APK에는 이 수정이 없다. 120×100은 Canvas 논리 크기이며 실제 픽셀 크기는 CanvasScaler에 따라 달라진다.
- 컴파일 실패는 없다. 콘솔에서는 CLI의 명령 호환성 탐색 오류(`script recompile`, `editor status`)와 EDM의 `GvhProjectSettings.xml` 파일 잠금(Windows 1224) 오류를 확인했다. UI 검사 실패와 구분했으며, 별도 의존성 관리자 설정은 이번 UI 수정에서 변경하지 않았다.
