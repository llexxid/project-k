# 로비 2차 리워크 검증 — 2026-09-12

## 최종 상태

방패 없는 대검 기사, 정상적인 활, Bandit King, 불타는 왕성·용·마탑 공성 배경, 한영 제목, 캐릭터의 발·팔·망토 동작, 언어 선택 팝업, 연출 아이콘, 공통 저사양 모드와 Suno BGM을 적용했다.

- 초기 언어는 기기 언어에 따라 한국어/영어로 정하고, 연출은 기본 켬이다. 언어 선택은 로비의 제목과 문구를 변경한다. 최종 QA 기기는 한국어/연출 켬으로 복구했다.
- 로비 연출 끔 = 설정의 저사양 모드 켬. 같은 저장값을 즉시 사용한다.
- 저사양: 반복 장식 정지, 고정 크기 데미지 + 15Hz 단순 상승/3단계 페이드. 숫자 값·개수·강조색·명목 표시 시간 유지. 버튼/팝업 피드백과 전투·입력 로직은 유지한다.
- 기본 60 FPS 유지. 기존 별도 절전 모드는 30 FPS이며 최종 기기에서도 확인했다.

## 환경 및 범위

Unity 6000.3.21f1, ARM64 IL2CPP Development APK. Samsung SM-N986N, Android 13, Adreno 650 Vulkan, 기본 검사 1080×2316 / 450dpi.
기존 앱을 덮어쓰지 않는 `com.isolatedyouth.idlekingdomrpg.lobbyqa`에 설치했다. 프로덕션 계정 데이터는 변경하지 않았다.

로비는 실제 bootstrap/title 씬과 UIManager/TitleScreenController에서 검사했다. 데미지는 실제 Main 화면·DamageTextManager·텍스트 프리팹에 초당 약 100회(35개/0.35초) 표시를 발생시키는 재현 가능한 부하이다. **로그인한 계정의 서버 연동 전투를 실행한 결과는 아니다.** 메인 화면 캡처의 검은 전장·초기값은 이 표시 검사 구성에 따른다.

## 실기기 성능

각 모드 25초씩 일반→저사양→저사양→일반 ABBA 측정. Android `Process.getElapsedCpuTime` 증가량/벽시계 시간으로 프로세스 전체 CPU를 측정했다. 100%는 CPU 코어 하나를 계속 사용한 양이다. 프레임 대기 시간이 섞인 Unity Main Thread 시간으로 CPU 절약을 판단하지 않았다.

| 구간 | 일반 CPU | 저사양 CPU | CPU 감소 | FPS 일반 / 저사양 |
|---|---:|---:|---:|---:|
| 로비 | 64.12% | 55.29% | 13.8% | 60.10 / 60.10 |
| 데미지 표시 부하 | 82.70% | 68.18% | 17.6% | 60.13 / 60.13 |

- 로비 애니메이션 표식: 28.97 → 0.00 µs/frame.
- 데미지 연출 표식: 207.82 → 129.81 µs/frame, 37.5% 감소.
- 120초 일반 로비 연속 실행: 60.06 FPS, P99 16.72ms, 50ms 초과 0회, 샘플 내 managed allocation 0 bytes.
- 제한: 단일 기기·Development 빌드·연결/충전 상태의 상대 비교다. 모든 기기에서 같은 감소율을 보장하지 않는다. FPS 상한은 유지하므로 주된 이득은 CPU 여유다.

원자료: `lobby-normal-a.json` 등 8개 비교 샘플, `*-paired-summary.json`, `final-soak.json` (Recordings/LobbyRevision2/Device). 중간 정지형 데미지 측정은 Preliminary 폴더로 분리했다.

## 기능·반응형 검사

최종 Android 빌드 오류 0건, 기존 코드 경고 20건. QA 프로세스 로그 4,000줄에서 Unity/네이티브 오류·예외 0건. 필요한 한영 글꼴 누락 0건.

- 실기기 직접 입력 19/19 통과: 로그인/바깥 탭/Android Back, 언어 선택·현재 언어, 버튼 크기 복원, 배경 가림 회귀, 연출 정지/재개, 반복 조작, 인게임 설정 동기화, 완료 버튼.
- 화면 4개: 720×1280, 1440×3088, 1536×2048, 1440×1600. 실제 `Screen.safeArea` 안의 제목·터치 영역·언어 팝업·설정 패널, 계정 버튼/Back 확인. 기기 크기/밀도는 검사 전 값으로 복구했다.
- Unity 별도 6개 viewport: 1080×1920, 1080×2400(상하 안전 영역), 720×1280, 1536×2048, 1600×2560, 1920×1080. 캐릭터와 시작 문구의 간격, 포인터 가림, 인증 화면 진입, 300 pose 갱신 managed allocation 0. 실제 렌더 240프레임 저장.
- UInt64 최댓값 `18,446,744,073,709,551,615`와 0은 두 모드 모두 동일하게 표시된다.
- 언어/저사양 설정은 프로세스 재시작 후 유지. Android 백그라운드 복귀 후 음악/동작 정상 재개.

## 음악·용량

Lobby의 Moonwell Drift 1/2, InGame의 Quest to the Skyforge 1/2 및 Skybound Relic 1/2를 모두 순환 검사했다. 13/13 음악·수치·상태 복구 검사 통과. 실제 AudioSource 진행 시간, Streaming 로드 방식, 출력 믹스 RMS 신호를 확인했다. 곡의 끝부분으로 이동해 다음 곡과 전체 순환도 확인했다. 물리 스피커 청취 판정은 포함하지 않는다.

6곡: 44.1kHz stereo Vorbis 0.62, Streaming, preload OFF. 새 플레이리스트가 구성된 경우 이전 BGM Addressable은 중복 선로딩하지 않는다.

- 배포 로비 PNG 12개: 4,755,833 bytes. 생성 중간본은 Assets 밖에 보관.
- 실제 기기 UI atlas: 2048² ASTC 6×6 두 장, readable OFF. GPU 블록 데이터 합계 3,742,848 bytes(기존 UI 포함). Mipmap OFF, Bilinear.
- 최종 APK: 100,446,385 bytes (95.79 MiB). 개발용 심볼 등을 포함한 BuildReport.totalSize와 APK 파일 크기는 다르다.
- SHA-256: `82799f524c342705b354171de09140ea650bd2408ab1662cdbbdb184005538fb`

## 검수 중 수정한 문제

1. 작은 언어 팝업의 선택 행을 132 UI 단위, 하단 아이콘 144, 설정 행 136으로 확장했다.
2. 공용 버튼 스킨이 언어 팝업 닫기 배경을 갈색으로 덮던 문제를 제거했다. 배경은 18% 검은 반투명막이다.
3. 언어 선택 직후 버튼이 비활성화되면 눌린 크기가 남던 문제를 복원 처리했다.
4. 완전히 정적인 저사양 데미지가 반복 타격과 겹치는 현상을 줄이기 위해 15Hz 짧은 상승을 유지했다.
5. 원경이 제목에 가리는 초기 구도를 교체하고, 기사 팔 중복 후보와 떠 있는 보스 초안을 제외했다.

## 확인 자료

- 최종 한국어/영어: `final-ko.png`, `final-en.png`
- 언어/인게임 설정: `final-language-popup.png`, `final-ingame-settings.png`
- 실제 기기 동영상: `final-lobby-motion.mp4` (10초, 720×1544)
- 상세 입력/해상도/상태: `final-checks.json`, `final-resolution-checks.json`, `final-ready.json`
- 빌드: `build.txt`, `build-messages.txt`; 런타임: `logcat-final.txt`

## 생성 비용

Comfy 4작업/5과금 이벤트: **85.22 표시 크레딧**. `get_usage_report` 워크스페이스 시간 누계의 관측 증가액은 **$0.403858**이며 작업별 청구 금액은 제공되지 않았다. 내장 ImageGen 5회는 금액이 노출되지 않는다. 상세는 `spend.json`과 `prompts.md`.
