# 퀘스트 4탭 검증 — 2026-09-18

Unity 6000.3.21f1 / Pipeline 0.7.0-exp.1. 기존 Editor에 Unity CLI로 연결했다. `Panel_Guide.prefab`만 Editor API로 저장했으며 전체 UI 생성과 YAML 직접 수정은 하지 않았다.

## 확인한 결과

- C# import·컴파일 성공, 컴파일 오류 0.
- `QuestUiAcceptance` 로드 실패 원인은 33자리 `.meta` GUID였다. 해당 GUID를 참조하는 다른 에셋이 없음을 확인한 뒤 32자리로 바로잡았으며, Unity에서 타입 로드와 30개 검사를 확인했다.
- 기존 인수 검사: 퀘스트 73 / 시간 24 / UI 계약 30 / 경제 93, 합계 220개 통과. Excel과 런타임 카탈로그 일치 및 기존 직렬화 참조 160개 확인. 상세는 `core-regression.json`.
- 탭 PlayMode 검사: 39개 통과. 실제 카탈로그·프리팹의 Button.onClick, GameObject 활성/비활성화를 사용했다. 네 범주 필터, 선택 테두리, 카드 표시, 스크롤 초기화, 같은 탭 재선택, 반복 Bind, 복귀, 업적 다음 단계, 오래된 계정 버튼 거절, 빈 목록을 확인했다. 상세는 `playmode-tabs.json`.
- 패널 스택·입력 검사: 22개 통과. 실제 UIManager의 PushPanel/PopPanel/RequestBack, GraphicRaycaster의 최상단 대상에 전달하는 합성 포인터 입력, ScrollRect 드래그, 닫기·복귀·재열기, 일일·주간 보관분, 저장 실패 후 같은 token 재수령을 확인했다. 상세는 `playmode-interaction.json`. 물리 마우스·휴대폰 입력이나 원본 전투 씬 검사는 아니다.
- 최종 통과 항목은 220+39+22=281개다. 수정한 Panel_Guide 참조 221개에서 missing script·끊긴 참조는 0개였다(`prefab-references.json`).
- 탭 프리팹 적용을 두 번 실행해 파일 내용이 동일함을 확인했다.
- 실제 사용자 계정을 열지 않는 격리 검사를 사용했다. 새 탭 선택과 같은 탭 재클릭은 저장 revision을 증가시키지 않는다.

## 화면 기록의 범위

`QuestTabCapture.cs`는 실제 UI 프리팹·글꼴·스프라이트를 PreviewScene에 복제하고, 현재 카탈로그의 설명과 고정된 예시 진행도를 표시한다. 사용자 세이브를 읽거나 변경하지 않는다. **이 PNG는 정적 레이아웃 비교이며, 전투 중 화면·실제 게임 입력·기기 검증으로 간주하지 않는다.**

| 비율 | 변경 전 | 변경 후 |
|---|---|---|
| 540×960, 9:16 | `before-all-540x960.png` | `after-{guide,daily,weekly,achievement}-540x960.png` |
| 360×800, 9:20 | `before-all-360x800.png` | `after-{guide,daily,weekly,achievement}-360x800.png` |
| 900×1200, 3:4 | `before-all-900x1200.png` | `after-{guide,daily,weekly,achievement}-900x1200.png` |

선택 테두리·네 문구·동일 폭을 확인했고 가이드 이외 탭에서 상단 카드 공간이 접힌다. 공용 탭 높이 144를 유지하므로 360px 폭 기준 터치 높이는 48px이다. 기존 제목 `퀘스트 / 가이드`는 이번 범위에서 유지했다.

`live-achievement.png`, `live-daily.png`, `live-weekly.png`는 실제 UIManager와 바인딩을 실행한 275×488 GameView 캡처다. 합성 입력 검사 중 `ScreenCapture`로 기록했다. 전투 배경이 없는 격리 QA 계정 화면이므로 실전 전투 맥락 검증은 남아 있다. 업적 캡처는 드래그 이후의 스크롤 위치다.

## 비용과 한계

- 새 텍스처·폰트·머티리얼 에셋은 추가하지 않았다. 공용 탭 프리팹 네 개와 하나의 목록을 사용한다.
- 캡처 예시에서 이전 목록 27행이 선택에 따라 1 / 8 / 7 / 11행으로 줄었다. 실제 행 수는 수령·보관 상태에 따라 달라진다. 이는 구조상 객체 수 비교이며 FPS·GPU 메모리 측정값이 아니다.
- 관련 없는 범주의 변경은 현재 목록에 반영하지 않고 해당 탭을 열 때 조회한다. 같은 탭 클릭과 반복 Bind의 객체 재사용은 PlayMode 검사로 확인했다.
- 기존 경제 검사의 저장 시간은 `core-regression.json`에 포함했다. 탭 추가 전후 성능 벤치마크나 실기기 프레임 측정은 수행하지 않았다.
- `adb devices` 결과 연결 기기 0개. Android 실제 터치·백그라운드 복귀·노치 안전 영역 검증은 미완료다. 세 비율 이미지를 여러 실제 기기 검사로 계산하지 않는다.

## 도구 진단

초기 CLI eval에서 메인 스레드 5초 타임아웃이 한 차례 발생했고 이후 정상 실행됐다. 설치된 CLI가 `--caller` / `--skill` 옵션을 거절하여 실제 지원 옵션으로 실행했다. 이를 테스트 실패나 성공 결과와 혼동하지 않았다. 원본 Console은 지우지 않았다.

추가 입력 검사는 처음에 5개 항목 후 첫 탭에서 raycast 결과를 찾지 못해 실패했다. 실패 결과·소스는 `playmode-interaction-initial-failure.json` / `QuestTabInteraction-initial.cs`에 보존했다. GameView를 열어 프레임을 진행시키고 렌더 완료를 기다리는 검사로 보완한 뒤, 입력 21개와 뒤로가기까지 포함한 최종 22개 검사를 통과했다. 이 문제 때문에 제품 코드나 프리팹 입력 설정을 바꾸지는 않았다.

검사 중 임시로 켠 `Application.runInBackground`는 기존 false로 되돌렸다. QA 계정·오브젝트를 정리한 뒤 원래 `Assets/_Project/Scenes/buildScenes/bootstrap.unity`를 다시 열었다. 종료 시 편집 모드·컴파일 중 아님·씬 dirty=false를 확인했다. 커밋·push·PR은 수행하지 않았다.

Unity 직렬화가 생성한 빈 `m_Name: ` 두 줄은 줄 끝 공백을 포함한다. C# diff 검사는 통과했으며 이 공백을 없애려고 프리팹 YAML을 직접 편집하지 않았다.
