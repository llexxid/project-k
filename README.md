# 왕국군 키우기 · Project-K

Unity 모바일 방치 RPG. 엔진 버전은 `ProjectSettings/ProjectVersion.txt`, 패키지는 `Packages/manifest.json`을 기준으로 한다.

현재 게임 버전은 **0.11.0**, Unity는 **6000.3.21f1**이다. Unity Hub에서 지정 버전으로 연다. 게임 버전은 Player Settings의 Version 한 곳에서 관리하며 로비와 설정이 이를 표시한다. 기능 개정은 minor, 수정은 patch를 증가시키며 첫 정식 출시는 1.0.0으로 올린다. Android Version Code는 배포마다 단조 증가시킨다. 빌드 전 엔진 핀과 버전 형식을 자동 검사한다.

`Assets/ExternalAssets/`는 로컬 원본 보관소이며 Git에서 제외한다. 게임이 참조하는 작업본은 `_Project/Art`와 `UGUI/Art`에서 버전 관리한다. 아트 제작을 다시 수행하려면 구매·다운로드한 원본 팩을 ExternalAssets에 별도 설치한다.

- [작업 지침](AGENTS.md): AI·개발 공통 행동 규칙.
- [UGUI 구조](Assets/UGUI/README.md): 화면·프리팹·데이터 연결 및 검증 진입점.
- [게임용 아트](Assets/_Project/Art/README.md): 캐릭터·몬스터·애니메이션·환경 프리팹 위치와 준비 상태.
- [에셋 준비 카탈로그](Docs/ArtPreparation/README.md): 신규 몬스터, 환경 90개, 출처와 검증 기록.
- [아트 실행 참고](AI/comfyui/README.md): ComfyUI 공정, 출력·출처 관리, 기존 제작 기록.
- [보관 아트](Assets/_Project/Art/Archive/Divine/README.md): 폐기된 신 스킬의 재활용 이미지·VFX.
- [마탑 스킬 모듈](Assets/MageTower/README.md): 등급 없는 10종, 중복 파편, 각성 10 개화와 개발용 카탈로그.

자체 코드·에셋은 `Assets/_Project/`, UI는 `Assets/UGUI/`에 있다. Unity Hub에서 이 폴더를 열고 `ProjectSettings/EditorBuildSettings.asset`의 활성 씬 순서로 실행한다.

팀: 이의찬(PM·기획·개발), 박준기(개발 리드), 유형진(개발·QA).
과거 작업 내역은 Git 기록을 참조한다.
