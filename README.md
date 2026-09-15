# 왕국군 키우기 · Project-K

Unity 모바일 방치 RPG. 엔진 버전은 `ProjectSettings/ProjectVersion.txt`, 패키지는 `Packages/manifest.json`을 기준으로 한다.

- [작업 지침](AGENTS.md): AI·개발 공통 행동 규칙.
- [UGUI 구조](Assets/UGUI/README.md): 화면·프리팹·데이터 연결 및 검증 진입점.
- [게임용 아트](Assets/_Project/Art/README.md): 캐릭터·몬스터·애니메이션·환경 프리팹 위치와 준비 상태.
- [에셋 준비 카탈로그](Docs/ArtPreparation/README.md): 신규 몬스터, 환경 90개, 출처와 검증 기록.
- [아트 실행 참고](AI/comfyui/README.md): ComfyUI 공정, 출력·출처 관리, 기존 제작 기록.
- [신 스킬 모듈](Assets/DivineSkill/README.md): 데이터와 전투·UI 연결.

자체 코드·에셋은 `Assets/_Project/`, UI는 `Assets/UGUI/`에 있다. Unity Hub에서 이 폴더를 열고 `ProjectSettings/EditorBuildSettings.asset`의 활성 씬 순서로 실행한다.

팀: 이의찬(PM·기획·개발), 박준기(개발 리드), 유형진(개발·QA).
과거 작업 내역은 Git 기록을 참조한다.
