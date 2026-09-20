# develop 병합 검증

2026-09-17. `develop`의 `5ef77f1f4`에 `feature/asset-Integration`의 `994fd1422`를 병합했다.

## 충돌 해결 기준

총 3,362개 파일의 충돌을 각각 다음 기준으로 해결했다.

| 범위 | 파일 수 | 처리 |
|---|---:|---|
| ExternalAssets 메타파일 | 3,234 | Git 인덱스에서만 제거하고 로컬 원본 보존 |
| 작업본 에셋·폰트·스테이지 생성기 | 104 | 현재 프리팹·애니메이션과 연결된 GUID, 슬라이싱, 카탈로그 경로 유지 |
| 이동 후 남은 폴더·가이드 이미지 메타파일 | 24 | 대상 파일과 참조가 없는 것을 확인하고 제거 |

충돌한 develop 쪽 메타파일 GUID를 참조하는 게임 파일은 없었다. 준비된 스프라이트의 프레임 ID를 유지해 애니메이션 연결을 보존했다. 폰트는 글리프와 아틀라스를 한 쌍으로 유지하고 develop에만 있던 `›` 문자를 기존 글리프 준비 과정에 추가했다.

`Stage_Catalog.xlsx → StageDataGenerator → Resources/StageDatabaseSO.asset` 경로를 유지했다. 병합으로 돌아온 `Stage_Revised.xlsx`는 카탈로그 개정 전 파일과 Git blob이 동일한 것을 확인하고 제거했다. 팀원이 추가한 `Docs/ServerDataAudit/overview.md`와 충돌 없는 변경은 보존했다.

## 원본 보관소 복구

Git 추적 해제를 포함한 병합 과정에서 ExternalAssets의 원본 파일들도 작업 디렉터리에서 사라진 상태였다. 병합 전 develop의 Git 객체에서 누락된 파일만 원래 경로에 복원했다. 이미 존재하던 원본 메타파일은 검증 전 바이트를 백업해 보존하며, `.gitignore`와 추적 해제 상태를 유지한다. 원본을 게임 작업본으로 덮어쓰지 않는다.

47,021개 파일과 LFS 원본 패키지 2개를 복구했다. 이 중 옛 `MonsterAndTilsets` 경로의 308개는 현재 `MainAssetBundles & Tilesets`에 있는 원본과 내용이 같고 메타파일 GUID 102개도 겹쳤다. JSON 구조와 텍스트 줄바꿈 차이를 구별해 내용을 대조하고, **이번에 복구한 옛 경로 사본만** `ExternalAssets/RecoveredGitArchives/MonsterAndTilsets-develop-5ef77f1f4.zip`에 보관했다. ZIP의 모든 항목을 develop Git blob과 대조했다. 기존 최신 경로의 원본은 변경하지 않았으며, 과거 메타파일도 ZIP 안에 그대로 보존했다.

## 검증

- 고정 Editor: Unity `6000.3.21f1`.
- Unity에서 카탈로그를 다시 생성했을 때 StageDatabaseSO의 GUID와 직렬화 데이터가 동일했다.
- 밸런스 검사 96개 통과. 스테이지 43개, 장비 18개, 직렬화 참조 14,710개에서 누락 0건.
- 마탑 스킬 10종, 기본·개화 아이콘 각 10개, 프리팹 19개와 컴포넌트 116개의 연결 검사 통과.
- Git 충돌 마커 0개, 게임 Assets 내 중복 GUID 0개. AI 출처 보관본의 메타파일은 Unity 에셋으로 계산하지 않았다.
- 원본 복구 후 전체 Assets GUID 29,378개 대조, 중복 0개.
- 원본·복구 파일 47,142개의 내용을 해시로 대조해 변경·누락 0건. 기존 원본 메타파일 3,665개를 포함한다. LFS 패키지 2개, 총 286,467,972바이트도 기록된 SHA-256과 일치했다.
- 설정 검사 70개 통과. 밸런스와 합계 166개다.
- 실행 모드 전용 전투 검사를 임시 Editor 검증에서 호출한 첫 시도는 `DontDestroyOnLoad` 제한으로 중단됐다. 임시 검사 진입점을 수정했으며 이 시도를 전투 검사 통과로 집계하지 않았다.

폰트 글리프 결과를 저장할 때 Windows `File.Replace`가 기존 파일 제거 오류로 실패했다. `MageSkillAssetPreparation.WriteAtomically`에 기존 Android 빌드 도구와 같은 Windows `MoveFileExW` 대체 경로를 적용했다. 원본 파일을 잘라 쓰지 않고 새 디렉터리 항목으로 교체하며, 교체 실패는 오류로 전달하고 임시 파일을 정리한다. `.meta`와 GUID는 변경하지 않는다.

## 최종 Android 확인

- 앱: **develop 병합 테스트 0.10.1 (b13)**. IL2CPP/ARM64 일반 플레이 빌드이며 진단 계정 강제 지정 코드를 포함하지 않는다.
- 빌드 성공: 오류 0, 경고 22. 기존 미사용 코드·API 경고와 압축 앱 아이콘 경고가 포함된다. APK는 80,769,824바이트이며 전체 심볼 산출물 크기와 구별했다.
- APK SHA-256: `3fe8c8fdcb84f07e3e5dc6159469e7a7ae3633194e50640a6ec77e51d91350e0`.
- 기존 데이터 백업 후 같은 패키지로 업데이트했다. 진행 데이터·PlayerPrefs 49개 파일의 설치 전후 내용이 일치했고, 이 프로젝트의 설치 앱은 한 개다.
- 실제 게스트 로그인, 방치 보상에서 복귀, 3스테이지 보스와 반복 전투, 왕국군 전직 트리, 마탑 편성·라이트닝의 `개화: 천벌` 설명, 설정 `v0.10.1`, Android 뒤로가기와 백그라운드 복귀를 확인했다. 실행 로그의 Unity 오류·치명적 예외·ANR·거래 거절 검색 결과는 0건이다.
- 일반 전투 25.01초간 SurfaceFlinger에서 화면에 표시된 프레임 1,563개를 수집했다. 평균 간격 16.66ms, 최대 25.22ms, 50ms 초과 0건이다. Unity 메인 스레드 프로파일러 측정값은 아니다. 전투 후 메모리 표본은 TOTAL PSS 490,576KB였다.
- 실제 기기 한 대(SM-N986N)에서 확인했다. 기존 1080×2316/DPI 450과 USB 화면 유지 값 7을 보존했다. 이번 검사는 병합 회귀 점검이며 여러 기기나 마탑 10종 전체의 전투 연출을 다시 검수한 결과가 아니다.
- 최종 앱을 타이틀 화면에 남겼다. 임시 설치용 APK 사본, 임시 Editor 검사 코드와 빌드 전용 설정 변경은 정리했다. 배포 빌드 번호 13과 실제 폰트 결과는 유지한다. 최종 Assets GUID 29,379개에서 중복 0건이다.

원시 근거는 로컬 `Recordings/MergeDevelop20260917`의 `editor-validation.json`, `Device/build.json`, `Device/install-verification.json`, `Device/manual-smoke.json`, `Device/battle-frame-sample.json`, `Device/manual-logcat-b13.txt`와 전후 PNG 캡처다. 저장 데이터 백업은 `Device/PreinstallBackup/b13`에 보관하며 Git에 넣지 않는다. 원본 복구·해시 대조·충돌별 판단 자료는 `.utmp/merge-develop-20260917`에 보관한다.
