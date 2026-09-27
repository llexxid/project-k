# 상점 · 재접속 보상 검증 (2026-09-27)

## 변경과 구현 범위

게임 버전은 PlayerSettings 기준 **0.15.0**이다. 햄버거 메뉴의 상점은 추천, 주화/골드, 매일 광고 탭을 제공한다. 안정 상품 ID와 예정 구성은 `ShopCatalog` 한 곳에서 관리하고, 모든 구매·교환·광고 클릭은 준비 중 토스트만 표시한다. 가격은 개발 예정가로 표시한다. 결제 SDK, 광고 SDK, 영수증 검증, 유료 지급, 광고 제거 소유권, 일일 광고 횟수 차감은 아직 연결하지 않았다.

광고 제거 예정 구성은 3,900원 무기한이며 보상형 광고를 포함해 영상 없이 보상을 받되 일일 횟수는 유지한다. 일일 장비·마탑 광고는 각각 하루 1회, 각 뽑기권 2장이다. 상세 가격·패키지·골드 환산과 연동 계약은 기존 기획데이터와 상세/운영 기획서에 통합했다.

오프라인 계산의 실제 상한을 8시간에서 **6시간(21,600초)**으로 줄였다. 기본 보상은 기존 원자 정산으로 먼저 지급되고, 새 광고 2배 버튼은 추가 지급 없이 토스트만 표시한다. 추후 같은 복귀 건의 기본 지급과 추가 지급을 구분하여 정확히 한 번만 지급해야 한다.

## Unity 검증

- 고정 Editor **6000.3.21f1**으로 import·C# 컴파일·프리팹 생성 완료.
- `ShopPopupPrefabGen.Validate`: 핵심 동작 **15개 통과**, 관련 프리팹 참조 **2,818개 / missing 0**.
- `ShopPopupPrefabGen.ValidatePlayMode`: 실제 프리팹·컨트롤러의 동작 **11개 통과**. 세 탭, 구매·광고 버튼의 전체 계정 무변경, 닫기, 재사용, Android 뒤로가기 공용 경로, 바깥 터치, 기본 보상 확인과 2배 버튼을 검사했다.
- 6시간 보상 상한, 음수 경과 시간, 같은 시각 중복 수령, 시계 역행, 저장 실패 시 지급/워터마크 보존, 재시도 시 단 한 번 지급을 검사했다.
- `FoundationPlayValidation.RunShopReview`: 실제 bootstrap/main Play Mode에서 격리된 임시 로컬 상태를 사용했다. 인증 공급자·실제 계정·서버 거래는 호출하지 않았다.
- **1080×1920, 720×1600, 1200×1600**의 Editor 렌더로 팝업과 글자·버튼 배치를 검수했다. 이는 세 대의 실제 기기 검증을 의미하지 않는다. 상점은 고정 머리말·안내와 스크롤 목록을 구분하며 긴 목록 하단 상품은 스크롤해서 확인한다.

원본 실행 로그는 Git 제외 경로 `Recordings/ShopRevision/validation.txt`, `playmode.txt`, `EditorScenes/report.json`, `unity-*.log`에 있다. 공개 가능한 최신 게임 화면 세 장만 이 폴더에서 관리하고 웹용 이미지는 그 원본에서 파생한다.

기존 기획 DOCX 3종에도 게임 내부 변경만 통합했다. 상세 기획서 46쪽, 개정 기획서 6쪽, 사업소개서 4쪽의 **총 56쪽**을 Word PDF로 렌더한 뒤 PNG로 전 페이지 검수했다. 증거는 `Recordings/PublishingShop20260927/Planning/final-verification.json`에 있다. 일회성 문서 갱신 스크립트는 같은 기록 폴더로 이동했고 운영 스크립트로 남기지 않았다.

## 발견 문제와 수정

1. 새 Play Mode 검사 메뉴가 미저장 씬을 바꿀 수 있어, 열려 있는 씬이 dirty이면 검사 시작 전에 중단하도록 보호했다.
2. 기존 RenderTexture 캡처 함수가 Overlay UI를 카메라 Canvas로 임시 전환하면서 월드 스프라이트를 UI 앞에 합성했다. 캡처 중에만 UI 최상위 정렬을 적용하고 원래 Canvas·카메라 상태를 finally에서 복원하도록 고쳤다. 이는 캡처 경로 수정이며 게임 프리팹의 렌더 순서를 바꾸지 않는다.
3. 빠른 batch Editor tick 수만 기다린 장비 화면에 진입 애니메이션 중간 프레임이 남아, 실제 unscaled 시간으로 애니메이션 완료를 기다린 후 다시 촬영했다.

## Android 확인 범위

Galaxy Note20 Ultra(SM-N986N) 한 대에서 USB 연결을 확인했다. 기존 이 프로젝트 앱의 내부/외부 데이터를 PC의 Git 제외 백업에 보존한 뒤 같은 패키지를 업데이트했다. 다른 앱·개인 파일·Google 계정 선택은 변경하지 않았다.

진단 APK **0.15.0 b64**는 IL2CPP/ARM64로 빌드·설치했다. 실제 APK Manifest의 minSdk는 25, targetSdk/compileSdk는 **36**이었다. 타이틀과 실제 Google 계정 선택 화면까지 확인했다. 임의 계정 선택이나 공유 게스트로 로그인하지 않았다.

최종 수동 테스트 APK **0.15.0 b65**를 같은 패키지에 업데이트 설치하고 타이틀의 v0.15.0 표시와 로그인 전 진입을 확인했다. 현재 앱 프로세스에 한정한 `Unity:E`/`AndroidRuntime:E` 로그는 0바이트였다. APK 파일은 `Recordings/ShopRevision/Manual/상점 플레이 테스트_0.15.0_b65.apk`이며 98,961,728바이트, SHA-256 `31ef91e12fbcdc0260ab46e6da0107a75cf75a5389510e418faad99780078a79`다. 실제 Manifest는 versionCode 65, minSdk 25, targetSdk/compileSdk 36, ARM64다. 빌드 시간 12분 13초, 오류 0, 경고 89건이다. 경고 원문은 같은 폴더의 `build-messages.txt`에 보존했다.

이 APK는 `BuildForManualTesting`의 **진단 코드·강제 계정이 없는 수동 테스트 빌드**다. Development Build 표시가 남는 테스트 패키지이며 Play 제출용 정식 서명 AAB가 아니다. 최종 설치본에서 Google 계정 선택 이후는 확인하지 않았으며, 진단 b64에서 확인한 계정 선택 화면까지의 경로와 구별한다. 타이틀 캡처는 `Recordings/ShopRevision/Manual/title-final.png`에 보존했다. 한 대의 휴대폰에서 이 프로젝트 앱 패키지 한 개를 업데이트했으며 다른 앱을 제거하지 않았다.

## 빌드 부산물 정리

빌드가 바꾼 GeneratedLocalRepo의 import meta, URP prefilter/runtime 목록, AndroidResolverDependencies, PerformanceTestRunInfo/Settings는 기존 내용으로 복구했다. PlayerSettings에는 의도한 버전 0.15.0과 versionCode 65만 남긴다.

자동 승인 검토가 빌드 부산물 삭제 명령을 **blocked by policy**로 거절했다. 해당 명령은 실행되지 않았으므로 아래 경로는 그대로 남아 있다. 우회하거나 다시 삭제하지 않았다.

- `Recordings/ShopRevision/Diagnostic/상점 보상 검증 0.15.0 (b64)_BurstDebugInformation_DoNotShip/`
- `Recordings/ShopRevision/Manual/상점 플레이 테스트 0.15.0 (b65)_BurstDebugInformation_DoNotShip/`
- `Recordings/ShopRevision/Diagnostic/상점 보상 검증_0.15.0_b64.apk`
- `Assets/AddressableAssetsData/link.xml` (미추적 생성 파일)
- `Assets/AddressableAssetsData/link.xml.meta` (미추적 생성 파일)

원본 저장 데이터 백업·최종 b65 APK·검증 로그는 보존한다. 생성 link.xml 두 파일은 이번 기능 소스가 아니므로 커밋 대상에서 제외한다.

## 성능·아트·미검증

새 상점은 기존 UI 테마·아이콘·공유 폰트를 재사용하며 새 텍스처나 생성 아트를 추가하지 않았다. 추가 한글 글리프를 기존 Galmuri11 SDF에 bake했고 최종 폰트는 912개 문자, 아틀라스 1장을 유지한다. 상점은 프리팹 인스턴스를 재사용하고 상태 revision 또는 숫자 표기 설정이 바뀔 때만 텍스트를 갱신하며 닫을 때 이벤트를 해제한다.

실제 기기의 로그인 완료 후 전투·상점 조작, 노치/안전 영역, 절전/복귀, 터치 스크롤 및 FPS·메모리 전후 비교는 아직 확인하지 못했다. Editor 렌더와 정상 Android 빌드가 이를 대체하지 않는다. 로그인 완료 후 같은 최신 앱으로 후속 검증해야 한다. 실제 광고/IAP·서버 지급·환불·구매 복원·동시 기기 거래 검증은 연동 구현 후 별도 수행해야 한다.
