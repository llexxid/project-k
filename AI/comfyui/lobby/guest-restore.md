# 개발용 게스트 로그인 복구 — 2026-09-12

사용자 요청에 따라 기존 공용 개발 계정의 게스트 로그인 경로를 복구했다.
TitleScreenController의 게스트 버튼은 NetworkManager.AuthenticateTest()를 호출한다.
TitleLobbyBuilder도 버튼을 활성화하고 기존 로비 패널 스타일과 한/영 라벨을 설정하므로 재생성 후에도 유지된다.
현재 로그인 팝업은 게스트와 Google 버튼을 표시한다.

Unity의 실제 title 씬에서 6개 화면 조건, 41개 검사를 통과했다. 게스트 버튼의 표시/활성 상태, 언어, 최상위 터치 대상, 팝업 안전 영역을 포함한다.
Samsung SM-N986N의 QA 앱에 새 IL2CPP APK를 설치해 한/영 버튼 검사와 실제 게스트 로그인 13개 항목을 통과했다. 기존 개발 계정으로 메인 화면까지 진입했고 해당 검증 구간의 런타임 예외는 0건이었다.
빌드는 성공했으며 오류 0건, 기존 경고 20건이다. 검증 후 기기를 한국어 로비 로그인 팝업으로 돌려놓았다.
새 이미지나 텍스처를 추가하지 않았으며 Atlas_UI는 기존 2048×2048 ASTC 6×6 두 페이지를 유지한다. 임시 빌드 설정은 복구했다.

기록: `Recordings/LobbyGuestRestore/Editor/validation.txt`, `Device/guest-checks.json`, `Device/guest-ko-popup.png`, `Device/guest-main.png`.
