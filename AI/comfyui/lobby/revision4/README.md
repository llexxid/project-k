# 로비 4차 아트 수정

- 마탑 위에 인게임 수정의 형태를 참고한 푸른 부유 수정을 그렸다. 픽셀 원화를 붙이지 않고 배경 그림체로 표현했다.
- 연속 레이저를 제거하고, 수정에서 용 쪽으로 날아가는 별개의 푸른 매직 볼트 두 덩어리로 바꿨다.
- 왼쪽 용에 앞다리와 발톱을 추가했다.
- 오른쪽 용 위에만 작고 국소적인 먹구름을 추가했다. 실제 번개·섬광·비는 없다.
- 배경 배치와 화면 비율은 유지했다. 기존 움직이는 볼트의 발사 좌표만 새 수정 위치 `(-26, 317)`로 맞췄다.

최종 게임 파일: `Assets/UGUI/Art/Lobby/Lobby_Background.png`.
편집 원본: `source/Background_CrystalBolts.png`.
재현: 프로젝트 루트에서 `python AI/comfyui/lobby/build_assets.py`.

1536×1536 RGB, mipmap OFF, readable OFF, Bilinear, 기존 Atlas_UI / ASTC 6×6 / max 2048을 유지한다.
새 텍스처·오브젝트·애니메이션 루프는 추가하지 않았다.
사용자 요청에 따라 Android 재빌드·실기기 테스트·여러 비율의 회귀 테스트·성능 재측정은 실행하지 않는다.

내장 ImageGen 편집 1회. 비용은 도구에서 제공하지 않는다. 이번 수정의 Comfy 호출과 추가 Comfy 과금은 0이다.
정확한 편집 지시는 [prompts.md](prompts.md)에 저장했다.

## 최소 확인 결과

기존 Screen_Title 프리팹을 Unity 에디터에서 720×1280으로 한 번 렌더해 아트 배치를 확인했다. Play Mode·자동 조작·성능 테스트는 실행하지 않았다.
배경 1536×1536, 2,657,489 bytes(이전보다 68,162 bytes 감소). 기존 Atlas_UI에 정상 연결되며 2048² ASTC 6×6 두 페이지 유지. 이동 레이어 5개, 마법 오브젝트 1개 유지.
미리보기: `Recordings/LobbyRevision4/lobby-art-preview.png`. 확인 기록: `Recordings/LobbyRevision4/art-audit.txt`.
기기의 설치 앱은 3차 APK 상태이며, 이 4차 아트는 다음 빌드부터 반영된다.
