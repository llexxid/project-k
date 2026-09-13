# 전투 HUD 개편 · 2026-09-13

## 설계 참고

- [Legend of Slime](https://www.malavida.com/en/soft/legend-of-slime/android/): 전투 진행과 현재 퀘스트의 짧은 요약, 하단 성장 기능을 비교했다.
- [Slayer Legend](https://www.malavida.com/en/soft/slayer-legend/android/): 상단 스테이지 표기와 하단 성장/장비 탐색을 참고했다.
- [Blade Idle](https://www.mumuplayer.com/blog/blade-idle-beginner-guide.html): 전투 상단 상태와 별도 기능 메뉴를 비교했다. 많은 측면 아이콘은 본 프로젝트의 화면 가림을 키우므로 채택하지 않았다.
- [Idle Slayer](https://reviewsbysupersven.com/idle-slayer/): 상세 퀘스트 목록에서 현재/필요 횟수를 함께 표시하는 방식을 참고했다.

위 화면들을 비교한 설계 판단: 전투 중에는 스테이지와 현재 목표 하나만 표시하고, 전체 가이드와 저빈도 설정은 메뉴에 둔다. 원본 게임의 아트는 복사하지 않았다.

## 적용

- 상단 중앙 반투명 웨이브 배지, 보스 타이머 유지. 반복 모드 이름도 배지에 표시.
- 좌측 반투명 카드: 가이드 단계, 목표, 현재/필요 횟수, 작은 진행 막대. 카드 탭으로 목적지/상세/완료 처리를 제공.
- 햄버거 첫 항목에 퀘스트/가이드. 보스 자동 도전·반복 종료도 메뉴로 이동.
- 메뉴의 현재 퀘스트와 HUD는 같은 QuestManager 이벤트를 사용한다. 수동 확인하는 TutorialManager 도움말은 별도로 구별하며, 등록된 도움말이 없으면 다음 퀘스트들을 표시한다.
- 기존 실제 퀘스트 데이터는 스테이지 클리어·몬스터 처치 4단계다. 예시였던 골드 강화 목표를 임의로 추가하거나 실제 진행인 것처럼 표시하지 않았다.
- 신규 래스터·머티리얼 없음. 기존 RoundedRect, Galmuri11 공유 머티리얼, Atlas_UI에 이미 포함된 Layer Lab 64px book 아이콘 재사용.
- 목표는 이벤트 시 갱신하고 동일 텍스트는 다시 설정하지 않는다. 완료 안내만 작은 unscaled 호흡 연출을 사용한다.

기준 1080px 배경 면적: 기존 984×144 두 개 = 283,392px², 변경 380×72 + 540×184 = 126,720px²로 **55.3% 감소**. GPU 시간 절감률이 아닌 배경 사각형 면적 비교다.

## ComfyUI 선택 근거·비용

실제 MCP의 catalog overview, pixel 모델 검색, inpainting 템플릿, ImageQuantize 검색, LoadImage/SaveImage/ImageScale/ImageQuantize 스키마를 조회했다.
픽셀 검색은 카탈로그 2,307개 중 해당 질의 16개, 첫 페이지 8개를 검토했다. 전체 자원을 검토했다고 주장하지 않는다.
이번 요구는 배치·정보 계층·반투명 UI 수정이며 기존 에셋이 충분하다. 생성·후처리 체인은 추가 품질 이득이 없으므로 사용하지 않았다.
생성 실행 0회, **이번 작업 생성 지출 $0**. 실제 workflow/API graph는 실행하지 않았으므로 만들지 않았다.
`get_usage_report(months=1, granularity=day)` 조회 결과는 과거 한 달 누적 $44.665547이며 이번 작업 비용과 별개다.
조회 요청·응답 원본: `Recordings/HudRevision/comfy-*.json`.

## 문서 정리

AGENTS.md에 공통 지침을 통합하고 CLAUDE.md는 연결만 유지했다. 프로젝트/UGUI/ComfyUI/신 스킬 안내는 역할별 실행 참고로 축약했다.
낡은 출시 예정일·미구현/연결 예정 선언·중복 검수·캐릭터 공통 포즈 프롬프트·생성 초상화 지시를 정리했다.
과거 제작 폴더의 실제 프롬프트·버전·지출·검증 기록 및 외부 패키지 문서는 보존했다.

## 검증

Samsung SM-N986N / Android 13 / Vulkan에서 ARM64 IL2CPP Development APK로 검증했다. 기존 앱과 분리된 `.lobbyqa` 패키지를 사용했다.

- Unity 클라이언트 회귀 검사: **18/18 통과**.
- 실제 Android 입력·라이프사이클: **26/26 통과**. 0/10·7/10·10/10, 완료 시 1회 이동, 현재/상세 수치 일치, 메뉴/바깥 탭/Back, 육성창 숨김·복귀, 화면 재생성, 앱 백그라운드 복귀.
- 진행 숫자 검증은 실제 QuestManager 이벤트에 QA용 몬스터 처치 횟수를 넣고 전투 시간을 잠시 정지했다. 서버 보상·재화를 주는 fixture는 아니다. 실제 계정 진입·전투 렌더링도 별도로 수행했다.
- 720×1280/320dpi, 1440×3088/560dpi, 1536×2048/320dpi: 목표/스테이지/메뉴/상세창 안전 영역, 핵심 텍스트 잘림 없음, 메뉴 터치 높이 44dp 이상, 뒤로가기 통과.
- 동일 기기의 표시 크기·밀도를 변경한 검사이며 서로 다른 GPU·기기 3대를 검사한 것이 아니다. 원래 표시 크기/밀도를 복원했다.
- 런타임 로그에서 예외·missing reference·누락 글리프 패턴 0건.

실제 전투 20초: 평균 **60.09 FPS**, P99 **16.72ms**, 33ms 초과 0프레임, 평균 draw calls **29.87**. Unity 할당 메모리 약 **146.5MiB**, Android 프로세스 PSS 약 **466.0MiB**. 전체 게임 GC 할당은 평균 **1,153 bytes/frame**이며 HUD만의 측정치가 아니다. 이 값으로 기존 버전 대비 CPU/GPU 개선율을 주장하지 않는다.

저사양 모드의 별도 전투 20초: 평균 **60.10 FPS**, P99 **16.72ms**, 평균 draw calls **26.19**, GC **749 bytes/frame**. 전투 상황이 동일하지 않으므로 모드 간 절감률로 해석하지 않는다. 검수 후 일반 모드로 복원했다.

최종 Unity 빌드: 오류 0건, 기존 경고 12건. 아틀라스 아이콘·목재색 행을 반영한 기기 스모크 6항목 통과. 증분 APK에 누적된 빈 공간은 생성된 Gradle 프로젝트에서 `:launcher:clean :launcher:assembleDebug`로 제거했다. 압축 해제한 **240개 파일의 SHA-256이 전부 동일**하며 APK는 145,809,198 → **78,469,723 bytes**로 감소했다. 이는 패키지 공간 정리이며 게임 콘텐츠·런타임 메모리 감소는 아니다. 명령 출력과 비교 결과는 최종 폴더의 `repackage.txt`, `apk-packaging.json`에 보존했다.

자체 피드백으로 개선한 항목:
1. 메뉴 글자가 버튼 전체와 회전하던 동작 → 글자·터치 영역 고정, 드롭다운 이동/페이드만 유지.
2. 비어 있는 큰 도움말 시트 → 높이 축소, 실제 다음 가이드 목록 표시.
3. 반투명 배경의 낮은 대비 → 단계·동작 안내를 밝은 양피지/금색으로 보강.
4. 상세창의 눌러도 동작 없는 안내 버튼 → 전투 진행 상태로 표시하고 비활성화.
5. 새 참조 아이콘의 별도 텍스처 → 이미 Atlas_UI에 들어 있는 책 아이콘 재사용.
6. 다음 가이드의 기존 청색 배경 → 공용 어두운 목재색으로 통일.

주요 결과: `Recordings/HudRevision/DeviceFinal/functional-results.json`, `resolution-results.json`, `hud-performance.json`, `final-logcat.txt`. 최종 아틀라스 아이콘·행 배경 반영 APK와 스모크 검수는 `Recordings/HudRevision/Final/`에 보존한다. 정리한 `KingdomIdle-LobbyQA-clean.apk`를 재설치한 뒤에도 6/6 스모크 검사가 통과했으며(`clean-smoke.json`), 전투 시간 정지를 해제했다.
