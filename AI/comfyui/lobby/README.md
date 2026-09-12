# 로비 리워크 — 왕국군 키우기 / Kingdom Idle

2026-09-12. 새벽 숲길을 돌파하는 1차 전직 왕국군 3인과 숨어 있는 도적.

**현재 프로젝트 적용본은 [5차 수정본](revision5/README.md)**이다. 마탑 수정과 용의 앞다리를 배경 그림체로 다시 그렸고,
공격 대신 은은하게 맥동하는 푸른 수정과 오른쪽 용 위의 작은 먹구름·구름 내부 번개를 적용했다.
언어 버튼은 왼쪽 위에 있으며, 연출 변경은 인게임 설정창의 저사양 모드에 통합했다.
[4차 수정 이력](revision4/README.md), [3차 수정 이력](revision3/README.md).
[2차 수정 이력](revision2/README.md). 아래 설명과 기존 검증 문서는 1차 제작 이력이다.
기본 `build_assets.py`는 최종 수정본 레시피로 연결된다.

## 아트와 제작 기록

- 의상 기준: 실제 `Royal Knight - Alternate 2`, `Royal Archer`, `Royal Mage`, `Bandit` 스프라이트.
- 새 로비는 키 아트이므로 부드러운 SD 일러스트로 제작했다. 전투용 도트 스프라이트·초상화는 변경하지 않았다.
- 제공된 상용 게임 이미지는 큰 타이틀과 돌격 구도의 참고로만 사용했다. 생성 입력에는 넣지 않았다.
- 원화·한영 로고: 내장 ImageGen. 실제 모델명·시드는 도구에서 제공되지 않으므로 임의로 기록하지 않는다.
- Comfy Cloud: Seedream 5.0 Pro 레이어 분리(1K 검증 → 2K 제작), Flux.1 Expand 배경 확장(15 steps 검증 → 50 steps 제작).
- 확장본의 생성 오류인 글자 모양을 ImageGen으로 제거했다. 원래 배경 중앙을 결정론적으로 복원해 레이어 좌표를 보존했다.
- `source/`에는 원본·중간 결과, `workflows/`에는 실제 API 그래프와 작업 ID가 있다. 모두 Unity 빌드 밖이다.
- `generation.json`에 생성 지시·출처·비용 집계를 기록한다.

## 배포 아트

`Assets/UGUI/Art/Lobby/`: 배경 1장, 캐릭터/검 6장, 한영 타이틀 2장, 코드로 만든 작은 UI 페이드 1장.

- 배경: 1536×1536. 원래 1024×1536 구도는 논리 2048×2048 평면의 `(512,256)`에 배치.
- 캐릭터는 알파 경계로 잘라 빈 공간을 제거했다. 레이어별 위치와 관절 중심은 `Lobby.layers.json`에 보존.
- 로고: 최대 너비 1024. 투명 알파 유지. 언어에 따라 UI와 함께 교체.
- Bilinear, mipmap/readable OFF, max 2048, Android/iOS ASTC 6×6, `Atlas_UI`.
- 신규 전투 도트 에셋이 아니다. `Atlas_UIPixel`의 Point/ASTC 4×4 규칙과 혼합하지 않는다.

## 런타임

`TitleLobbyPresentation` 하나가 정지 아트 레이어를 움직인다. 생성된 애니메이션 프레임이나 비디오를 게임에 넣지 않는다.

- 기사/궁수/마법사 호흡, 관절 기준 검 흔들림, 서로 다른 주기의 도적 떨림, 지팡이 빛, 작은 먼지 6개.
- 한 개의 unscaled-time 코루틴. 기본 30Hz, 절전/저메모리 환경 15Hz. 포커스 상실·백그라운드·비활성화 시 중지.
- 재활성화 시 코루틴 재시작. 연출 끄기 기능은 중립 자세를 복구한다.
- 정적 배경과 움직이는 캐릭터의 Canvas 리빌드 범위를 분리했다. 장식은 raycast를 받지 않는다.
- UI는 기존 SafeArea 내부, 배경과 하단 페이드는 전체 화면까지 확장한다.
- 한영 로고·안내·로그인 팝업을 함께 전환한다. 기존 인증 검사와 Google 로그인 동작을 유지한다.

## 재생성 및 검증

1. 저장소 루트에서 `python AI/comfyui/lobby/build_assets.py`.
2. Unity 메뉴 `KingdomIdle/UGUI/Rebuild illustrated lobby`.
3. Unity 메뉴 `KingdomIdle/Optimize/2) Create In-Build Sprite Atlases`.
4. 배치 검증 진입점: `KingdomIdle.UGUI.Editor.TitleLobbyValidation.Run`.

검증기는 실제 `title.unity`에서 배포 UI 루트·타이틀 프리팹·컨트롤러를 Play Mode로 실행한다.
720×1280, 1080×1920, 1080×2400, 1536×2048, 1600×2560, 1920×1080과 가상 노치/하단 안전 영역을 검사한다.
계정에 로그인하지 않고 미인증 진입 게이트, 팝업, 입력 우선순위, 언어 전환, 연출 복구와 관리 메모리 할당을 검사한다.

캡처·실행 로그: `Recordings/LobbyValidation/`. 캡처는 실제 기기 성능 측정을 대신하지 않는다.

최종 결과·측정 범위·비용: [validation.md](validation.md). 한영 비교 이미지는 `Recordings/LobbyValidation/Lobby_Comparison.jpg`, 8초 동작 미리보기는 `Lobby_Motion.mp4`에 있다.

이후 진행한 Android 실기기 검증과 수정 결과는 [device-validation.md](device-validation.md)에 정리한다.
기기 검증용 APK는 `TitleLobbyDeviceBuild.Build`로 만든다. 원래 시작 씬들을 사용하며 앱 ID에 `.lobbyqa`를 붙여 기존 게임과 데이터를 분리한다. 임시 빌드 설정은 작업 후 복구한다.
`TitleLobbyDeviceProbe`는 `LOBBY_DEVICE_QA && DEVELOPMENT_BUILD`일 때만 포함된다. 일반 빌드에서는 계측과 명령 처리가 모두 제외된다.
ADB 재검증: `device_checks.py --serial <연결기기> interactions <실행이름>` 및 `resolutions <실행이름>`. 해상도 검사는 `device-before.json`에 기록된 크기·밀도로 복구한다.
