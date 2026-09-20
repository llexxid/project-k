# 전투 기기 검증

작업 규칙은 루트 [AGENTS.md](../../../AGENTS.md)를 따른다. 이 폴더는 USB로 연결된 Android에서 실제 입력과 진단 빌드의 런타임 상태를 대조하는 도구다.

## 실행

- 고정 Unity 버전에서 `BalanceEditorValidation.BuildAndroid`로 진단 APK를 만든다. 아트 재생성이 필요한 경우 `KingdomIdle.UGUI.Editor.CombatPolishValidation.BuildDevice`를 사용한다.
- `HUD_QA_OUTPUT`으로 결과 폴더, `HUD_QA_SERIAL`로 기기를 지정한다. `AI/qa/settings/settings_checks.py launch`로 로그인부터 진입한다.
- `final_build_smoke.py core`: 근접 위치, 저장, 도발·보호막·기절, 고블린 폭탄의 실제 명중.
- `polish_checks.py layered`: 감속/기절 동시 적용과 풀 재사용 세대 검증.
- `polish_checks.py matrix`: 일반 3·보스 3·골드 5·루비 5. 던전은 격리 계정에 입장 조건을 설정한 뒤 실제 입장/복귀 경로를 탄다. 뒤에 `main-1 gold-1`처럼 이름을 주면 선택 재실행한다.
- `HUD_QA_RECORD_COMBAT=1`은 matrix의 대표 5구간을 12초 영상으로 남긴다. 출력 폴더의 부모에 해당 APK의 `build.json`이 필요하다. 녹화 중 결과는 성능 측정으로 사용하지 않는다.
- `polish_checks.py stress`: 다섯 스킬 자동 시전, 일반/저사양 각 90초, 백그라운드 복귀.
- `aspect_checks.py`: 한 실물 기기에서 세 화면 비율을 검사한다.
- `../settings/channel_checks.py`: 일곱 음량의 실제 슬라이더 조작·저장, 표시/기기 탭 점검.
- `presentation_frames.py 30`: 진단 기능 없이 SurfaceFlinger의 실제 표시 간격을 수집한다. GPU 실행 시간 측정은 아니다.

수동 확인 앱은 `BalanceEditorValidation.BuildAndroidForManualTesting`으로 만든다. 진단 빌드와 구분하고 원래 저장 데이터·설정을 복구한 후 로그인, 전투, 메뉴를 확인한다. `fresh` 시험은 격리 계정의 저장을 이름이 명시된 백업으로 옮기므로 후속 복구가 필요하다.

2026-09-17 검증과 알려진 범위는 [폴리싱 보고서](../../../Docs/ArtPreparation/COMBAT_POLISH_20260917.md)에 있다.
