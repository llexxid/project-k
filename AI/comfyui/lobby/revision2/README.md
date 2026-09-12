# 로비 2차 수정 — 2026-09-12

## 반영 내용

- 기사: 방패 제거, 대검과 팔을 독립 레이어로 분리. 궁수: 양쪽 활대와 하나의 연결된 시위를 갖춘 활로 수정.
- 캐릭터: 발 디딤, 망토/머리카락, 팔 준비 동작을 가벼운 메시 변형으로 표현. 대검은 팔 관절을 중심으로 움직인다.
- 도적: 기존 방패 도적 + 실제 Bandit King의 대머리·왕관·복면·몽둥이 특징을 반영한 수풀 속 보스.
- 원경: 파괴되고 불타는 왕성, 용 실루엣 두 마리, 청색 마법을 쏘는 돌 마탑 두 개. 실제 화면에서 제목에 가려진 첫 시안은 교체했다.
- 제목: 한영 로고를 유지하고 밝기를 6% 낮췄다. 공성 장면을 가리지 않도록 화면 비율에 따른 크기도 조정했다.
- UI: 인게임 Layer Lab 패널과 청동 테두리. 지구 아이콘 → 현재 언어 표시 및 한국어/English 선택 팝업. 재생 아이콘=연출 켬, 정지 아이콘=연출 끔.
- BGM: Lobby 폴더의 Moonwell Drift 1/2, InGame 폴더의 Quest to the Skyforge 1/2 및 Skybound Relic 1/2 순환 재생. 곡/화면 전환 1.2초 페이드, 장면의 이전 TITLE.wav 자동 재생 제거.

## 저사양 모드

공통 키 `settings_lowSpec`, 로비 연출 버튼과 인게임 설정이 즉시 같은 값을 사용한다.
이전 `title_ambientMotion` 설정은 최초 실행 시 이관한다.

- 로비 반복 코루틴/관절 메시 갱신/마법 투사체/발광 장식 정지, 중립 이미지 표시.
- 데미지 숫자의 개수·값·강조색·표시 시간을 유지한다. 확대·좌우 흩뿌림 대신 고정 크기, 15Hz 단순 상승, 짧은 3단계 페이드를 사용한다. 연속 타격 숫자가 같은 자리에 완전히 포개지지 않게 상승은 유지한다.
- 전투 데미지 UInt64 포맷은 캐시된 문자 버퍼를 사용한다. 콤마 구분과 전체 값은 유지한다.
- UI 공통 반복 호흡/회전과 마탑 유휴 발광/수정 부유 정지. 설정 해제/재활성화 시 재개한다.
- 버튼 누름, 팝업 등장, 마탑 시전 피드백, 입력/전투/자동 시전 기능은 유지한다.
- 목표 60 FPS를 유지한다. 기존 별도 **절전 모드**의 30 FPS 옵션과 구분된다.

## 최적화 / 재현

- 배포 PNG 12개 총 4,755,833 bytes. 원본과 생성 중간 결과는 Assets 밖에 있어 빌드에 포함되지 않는다.
- 배경 1536², 로고 최대 1024, 캐릭터 알파 영역으로 크롭. 전체 `Atlas_UI`, Bilinear / Android ASTC 6×6 / max 2048 / mipmap·readable OFF.
- BGM 6곡은 44.1kHz stereo Vorbis quality 0.62, Streaming, preload OFF. 압축된 MP3 원본을 PCM 전체 상주 방식으로 재생하지 않는다.
- 코드로 만든 재생/정지 아이콘은 64². 지구 아이콘은 Layer Lab의 명시 경로 `Shared/Sprite_Common/~Demo/Demo_Icon/Icon_Setting_Language.png` 재사용.

1. `python AI/comfyui/lobby/build_assets.py`
2. Unity MCP로 `KingdomIdle.UGUI.Editor.LobbyRevisionBuilder.Apply()` — 로비/설정 프리팹, 음악 연결, 아틀라스, 글꼴 사전 등록.
3. Play Mode 검증: `TitleLobbyValidation.Run()`.
4. 실기기 검증 APK: `TitleLobbyDeviceBuild.Build()`.

검증 산출물은 `Recordings/LobbyRevision2/Editor`, `Recordings/LobbyRevision2/Device`에 저장한다.
실기기 계측은 일반 빌드에 없는 `LOBBY_DEVICE_QA && DEVELOPMENT_BUILD` 전용 코드다.
데미지 부하 검사는 실제 Main 화면/데미지 프리팹을 사용하되 계정·보상·전투 시뮬레이션 서비스를 시작하지 않는 재현 가능한 표시 부하이다.

최종 측정치·검사 범위·수정 사항: [실기기 검증 보고서](device-validation.md).

## 생성 이력과 비용

원화 수정: 내장 ImageGen. Bandit King 초안과 기사 관절 레이어 분리: Comfy Cloud.
생성 지시와 선택 근거는 [prompts.md](prompts.md), 실제 Comfy API 그래프/작업 ID는 이 폴더의 `*.api.json`, `*.job.json`에 있다.

Comfy 작업 4건 / 과금 이벤트 5건: **85.22 표시 크레딧**. `get_usage_report`의 시간 단위 누계가 $45.489101 → $45.892959로 증가했다(관측 차액 **$0.403858**).
이는 워크스페이스 집계 차액이며 개별 작업별 청구서는 제공되지 않는다. 상세: [spend.json](spend.json).
내장 ImageGen 5회는 도구가 별도 금액을 제공하지 않는다.

첫 기사 분리 호출은 배경 출력만 저장하여 알파 출력을 연결한 호출을 추가했다. 2K 후보는 팔 중복 때문에 채택하지 않았다.
깨끗한 1K 분리본을 최종 게임 크기로 축소해 사용한다. 평가한 유료 후보도 비용에 포함했다.

검수 중 수정: 언어 팝업의 닫기 배경은 공용 버튼 스킨을 적용하지 않는 반투명 이미지로 만들었다. 데미지는 완전 정지 시 연속 타격이 겹치는 문제 때문에 15Hz 단순 상승을 유지한다. 팝업 선택 후 비활성화되는 버튼의 눌림 크기도 즉시 원복한다.
