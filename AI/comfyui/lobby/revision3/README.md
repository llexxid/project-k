# 로비 3차 수정 — 마탑과 도적 정리

- 독립 마탑을 왼쪽 하나로 줄이고 오른쪽 마탑·마법 광선·끝점 광원을 제거했다.
- 남은 마탑은 기존 중심(화면 폭 약 51.7%)에서 약 49%로 이동했다. 원통형 석탑/성가퀴/청록 마법 창문의 개념만 참고하고 배경과 같은 붓질, 석재색, 햇빛, 원거리 안개로 다시 그렸다. 인게임 픽셀 원화를 합성하지 않았다.
- 오른쪽 도적은 로비 프리팹과 배포 아틀라스에서 제외했다. 왼쪽 도적과 세 영웅, 한영 제목/UI, 저사양 모드, 음악은 유지했다.
- 마법 입자도 하나로 줄여 새로운 탑 위치 (-9, 284)에서 용 방향 (82, 336)으로 이동한다. 좌표는 2048×2048 아트 월드의 중심 기준이다.

원본: `source/Background_SingleTower.png`. 게임용 배경: `Assets/UGUI/Art/Lobby/Lobby_Background.png`.
제거한 도적 원본은 보존한다. 아틀라스는 실제 프리팹 의존성만 수집하므로 사용하지 않는 도적 이미지는 게임 빌드에 포함되지 않는다.

## 재현 / 최적화

프로젝트 루트에서 `python AI/comfyui/lobby/build_assets.py` 실행. 현재 디스패처는 3차 레시피를 선택한다.
기존 2차 영웅/UI 에셋 위에 1536² 배경을 내보내고 도적을 레이어 목록에서 제외한다.
Unity `TitleLobbyBuilder.Rebuild()` 후 `OptAtlases.CreateInBuildAtlases()`를 실행한다. 로비 폴더 전체 대신 실제 UI 의존성에 있는 이미지만 아틀라스에 넣는다.
Atlas_UI / Bilinear / ASTC 6×6 / max 2048 / mipmap OFF / readable OFF.
실제 로비 의존성 PNG 11개, 합계 4,770,539 bytes. UI 아틀라스는 2048² 두 장, ASTC 블록 데이터 합계 3,742,848 bytes(기존 UI 포함)다.
Unity 의존성 검사에서 오른쪽 도적이 아틀라스에 없고, 이동 레이어 5개/마법 입자 1개임을 확인했다.

검증 자료: `Recordings/LobbyRevision3/Editor` 및 `Recordings/LobbyRevision3/Device`.

## 생성 비용 / 지시

이번 수정은 내장 ImageGen 편집 1회. 도구에서 금액을 제공하지 않는다. 추가 Comfy 유료 호출은 없다.
편집 대상과 참조 역할, 최종 지시는 [prompts.md](prompts.md)에 기록했다.

## 최종 검증

Unity 6000.3.21f1의 실제 title 씬에서 6개 viewport 검사와 240프레임 렌더를 통과했다.
연결된 Samsung SM-N986N / Android 13 / Vulkan 기기에 ARM64 IL2CPP QA 빌드를 설치했다.
실제 터치 19/19, 기기 720×1280·1440×3088·1536×2048·1440×1600 검사 통과.
30초씩 측정: 일반 60.08 FPS (P99 16.70ms), 저사양 60.08 FPS (P99 16.68ms). managed allocation: 일반 0 bytes / 저사양 0 bytes. 저사양 로비 애니메이션 갱신 0.00µs/frame.
도적은 BanditLeft 한 명, 영웅/검을 포함한 이동 레이어 5개, 마법 입자 1개임을 실제 기기에서도 확인했다.
QA 로그의 Unity/네이티브 오류·예외 0건. 기기 화면 크기·밀도 복구: True.
이번 작업에서는 로비를 검사했으며 계정 연동 전투는 실행하지 않았다.
최종 캡처: Recordings/LobbyRevision3/Device/final-ko.png, final-en.png. 동영상: final-lobby-motion.mp4.
상세: validation-summary.json, final-checks.json, final-resolution-checks.json, Editor/asset-audit.txt.

최종 QA APK는 오류 0건으로 빌드하고 다시 설치하여 터치 19/19 및 로비 레이어 구성을 재검증했다. 상세 빌드와 설치 해시는 Device/build-verified.json, 재검사 로그는 logcat-rebuilt.txt에 기록했다.

QA 빌드에서 EDM이 생성 Maven 파일을 연속 import하는 동안 Gradle 템플릿을 다시 캐시해 Windows 1224 오류가 발생했다. QA 빌드 전 의존성 검사를 완료하고, 그 구간에만 import 후 캐시 핸들을 해제한다. 일반 에디터 import와 게임 런타임에는 이 처리가 실행되지 않는다. API 근거: [Unity ReleaseCachedFileHandles](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.ReleaseCachedFileHandles.html), [EDM 1.2.182 resolver](https://github.com/googlesamples/unity-jar-resolver/blob/v1.2.182/source/AndroidResolver/src/PlayServicesResolver.cs).
