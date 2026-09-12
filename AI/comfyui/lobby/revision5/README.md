# 로비 5차 수정

## 아트

마탑 수정은 인게임 픽셀 원본을 붙이지 않고 배경과 같은 그림체로 작게 다시 그렸다. 탑의 중심축에 맞췄으며 후속 지시에 따라 레이저·매직 볼트와 마탑 공격 오브젝트를 모두 제거했다. 수정은 은은하게 푸르게 빛나고 먹구름 외곽의 약한 푸른빛과 연관성을 암시한다.
왼쪽 용의 앞다리는 작은 관절과 발톱으로 다시 정리했다. 오른쪽 용 바로 위에는 작고 진한 보라색 먹구름을 배치했다.

최종 배경: `Assets/UGUI/Art/Lobby/Lobby_Background.png`.
원본과 확대 편집 결과는 `source/`, 실제 생성 지시는 [prompts.md](prompts.md)와 후속 편집 [idle-prompt.md](idle-prompt.md), 최종 여백 보정 [fit-prompt.md](fit-prompt.md)에 있다.
`python AI/comfyui/lobby/build_assets.py`로 5차 배경을 다시 내보낸다. 확대 편집 결과를 원래 좌표에 재조합하고 가장자리 8px만 혼합한다.

## 연출 / UI

- 먹구름 안에서 약 8.6초마다 짧은 빛 두 번. 번개가 구름 밖이나 지면으로 내려오지 않는다.
- `LobbyStormFlash`의 작은 UGUI 메시로 구현한다. 추가 텍스처, 매 프레임 생성 이미지, 별도 Update/코루틴은 없다.
- 수정의 작은 푸른 후광만 약 4.65초 주기로 부드럽게 맥동한다. 수정의 위치와 크기는 고정한다.
- 로비 우측 하단 연출 버튼과 히트 영역을 제거했다. 인게임 설정창의 저사양 모드로만 사용자가 연출을 변경한다.
- 언어 버튼과 선택 팝업은 SafeArea 왼쪽 위에 배치한다. 현재 언어 표시와 팝업 미세 애니메이션을 유지한다.
- 저사양 모드에서 기존 로비 코루틴, 구름 메시, 수정 후광, 불빛과 입자를 중지/비활성화한다. 입력 프레임 목표와 게임 기능은 유지한다.

## 최적화

게임용 배경은 1536×1536 RGB, PNG 2,650,158 bytes. 기존 Atlas_UI를 사용하며 Bilinear / ASTC 6×6 / max 2048 / mipmap OFF / readable OFF 설정을 유지한다.
최종 로비 의존성 PNG 9개, 합계 4,694,190 bytes. 기존 UI 포함 Atlas_UI 2048² 두 장, ASTC 블록 데이터 합계 3,742,848 bytes로 페이지 수는 증가하지 않았다.

## 생성 비용

이번 수정의 내장 ImageGen 편집은 5회다. 첫 두 결과의 작은 연결점 오류를 확대 편집으로 보정했고, 4번째 호출은 사용자의 후속 지시에 따라 레이저를 없애고 푸른 수정/구름 윤곽으로 바꿨다. 5번째 호출은 세로 화면에서 오른쪽 용·구름이 잘리지 않도록 둘만 함께 왼쪽으로 옮겼다.
내장 도구는 과금액을 반환하지 않아 금액은 확인할 수 없다. 현재 세션에는 ComfyUI 호출 및 get_usage_report 도구가 노출되지 않았으며, 이번 수정에서 추가 Comfy 유료 호출은 0회다.

## Unity 검증

실제 title 씬/배포 프리팹에서 6개 viewport와 가상 SafeArea, 240프레임 렌더를 검증했다. 300회 연출 갱신의 managed allocation은 0 bytes다.
처음 검사에서 새 번개 메시의 CanvasRenderer 누락을 수정했다. 긴 화면에서 오른쪽 용/구름이 잘리는 문제는 원화를 재배치해 해결했고, 가로 fallback의 언어 버튼/제목 겹침은 레이아웃을 보정한 후 재검사했다. 실기기에서 발견한 태블릿/폴더블의 제목과 먹구름 겹침도 전체 화면 아트 좌표와 SafeArea의 실제 간격으로 제목의 최대 높이를 제한해 수정했다.
최종 기록: `Recordings/LobbyRevision5/Editor/validation.txt`, `asset-audit.txt`, `Lobby-Motion.mp4`.

## Android 빌드 환경 보정

Windows에서 EDM의 임포트 이후 ARM64 QA용 Gradle ABI 제외 항목을 쓰는 단계가 1224 오류를 일으켰다. QA 빌드에서는 단일 프로세스 임포트와 import 전후 캐시 핸들 해제를 사용하고, ARM64-only 빌드에 필요한 armeabi-v7a 제외 한 줄을 의존성 임포트 전에 준비한다. 전체 EDM resolver를 계속 실행해 모든 의존성과 최종 템플릿을 검증한다. 템플릿 바이트와 임포트 모드는 finally에서 복구한다.
실패한 빌드의 예전 성공 보고서와 APK를 재사용하지 않도록 시작 시 이전 build.txt를 지우고, 기기 설치 스크립트에서 실패 파일·APK의 소스 변경 시각을 검사한다.
API 근거: [Unity refreshImportMode](https://docs.unity3d.com/cn/6000.0/ScriptReference/EditorSettings-refreshImportMode.html), [Unity ReleaseCachedFileHandles](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.ReleaseCachedFileHandles.html). EDM의 재임포트 동작은 [공식 1.2.182 소스](https://github.com/googlesamples/unity-jar-resolver/blob/v1.2.182/source/AndroidResolver/src/GradleTemplateResolver.cs)를 확인했다.

## 최종 Android 검증

Samsung SM-N986N / Android 13 / Vulkan에서 최종 ARM64 IL2CPP QA APK를 설치했다. 최종 터치 검사 24/24 통과, 앞선 두 회차 48개 항목도 통과했다. 720×1280, 1440×3088, 1536×2048, 1440×1600 화면 크기/밀도 조합에서 안전 영역과 팝업, 실제 터치·뒤로 가기·설정 저장을 확인했다. 동일 기기의 표시 크기를 바꾼 검사이며 별도 기기 네 대를 검사한 것은 아니다.
최종 빌드와 실기기 Unity/네이티브 로그 오류 0건. 컴파일·기존 앱 아이콘 경고는 Device/build-messages.txt에 기록한다. APK 실제 크기는 100,461,169 bytes다.
30초씩 측정: 일반 60.08 FPS (P99 16.69ms), 저사양 60.08 FPS (P99 16.67ms). 두 모드 모두 관리 메모리 할당 0 bytes. 로비 연출 갱신 비용은 일반 24.90μs/frame → 저사양 0μs/frame. 두 모드가 이미 60 FPS 상한을 유지하므로 FPS 상승은 관찰되지 않았다.
최종 실기기 20초 영상에서 구름 내부의 짧은 빛과 조용한 간격이 반복되고 수정의 푸른빛이 천천히 변하는 것을 확인했다. 저사양 모드에서는 구름과 수정 후광이 꺼지고 중립 자세를 유지한다. 앱 재시작 후 설정 유지와 다시 켰을 때 연출 복귀도 통과했다.
기기의 원래 해상도/밀도를 복구했고 임시 프로젝트 빌드 설정도 복구했다. 실제 계정 로그인과 계정 연동 전투는 실행하지 않았다.

결과: `Recordings/LobbyRevision5/Device/final-ko.png`, `final-en.png`, `final-lobby-motion.mp4`, `validation-summary.json`. 상세 체크는 `final-checks.json`, `final-resolution-checks.json`, `video-inspection.json`에 있다.
