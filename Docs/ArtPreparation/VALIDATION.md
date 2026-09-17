# 검증 기록

검증 날짜: 2026-09-15. Unity 6000.3.21f1, 현재 Android 빌드 타깃.

## 무결성

- 주요 번들 원본 및 `.meta` SHA-256 변경 0건.
- 원본 138개 전부 작업본과 바이트 일치. 신규 복사 119개, 기존/번들 내 중복 재사용 19개.
- 기존 아트 파일 628개와 장비 폴더 이동 시 GUID 유지. 기존 Addressables 이름/등록은 그대로 유지.
- 이동·복사 대상 텍스처 159개의 **개별 스프라이트 참조 6,331건**을 Unity에 로드해 로컬 fileID까지 검사: 누락 0건. 신규 시트 100개의 Point/32 PPU/mipmap OFF/readable OFF 검사: 실패 0건.
- 작업 전 존재한 파일 유실 0건. 코드와 씬 변경 0건.
- 기존 픽셀, PPU, 슬라이싱을 유지한 작업본은 가져오기 압축 설정만 조정. UI용 EliteArcher와 애니메이션용 EliteArcher의 서로 다른 서브에셋은 보존.
- 빈 폴더 189개와 수정 과정에 남았던 Knight `Attack2` 클립 5개는 참조가 없음을 검사한 후 `.utmp/asset-prep/cleanup-backup`으로 이동. 원복 대응은 [cleanup.json](Manifests/cleanup.json)에 기록.

## 프리팹·애니메이션

- 37개 몬스터: 모든 기본 상태의 Animator 파라미터, SpriteRenderer, 클립, `MonsterAnimationSO` 대응 확인.
- Unity PlayableGraph로 Idle → Walk → Attack → Hurt → Dead를 실행해 전환 확인. Dead 이후 Hurt를 요청해도 사망 상태 유지. 실패 0건.
- 컨트롤러 상태 클립과 행동 enum의 길이 데이터가 일치. 사망/피격 enum 순서 교정 완료.
- Knight 128×64, EliteArcher 64×32 프레임 규격 재검수. 잘못된 이전 Knight 잔여 클립 정리.
- 공급자 JSON의 시간 유지. 정지 마지막 키로 1/100초가 덧붙던 519개 클립은 원본 길이에 맞게 보정. 단발 효과의 마지막 숨김 키는 유지.
- 기존 Archer의 유실된 차지샷 클립 참조를 같은 원본 시트의 8프레임으로 복구.
- 단발 효과, 반복 투사체, 함정의 표시 종료 규칙은 별도 소유자/풀 반환 기능과 구별하여 문서화.

## 환경·가독성

- 90개 프리팹 각각 Grid, 3 Tilemap, 3 Chunk renderer. Collider, 커스텀 스크립트, 상시 Animator 0개.
- 총 177,886개 배치 타일이 공유 Tile 에셋을 참조(초기 175,498개에서 3스테이지 수목 증가). 모든 Tile sprite 유효, colliderType=None.
- 90개 전체의 장식 사각 경계가 중앙 비우기 영역과 겹치지 않는지 수치 검사: 통과.
- 대표 15개 구도를 360×800(긴 폰), 540×960(16:9), 720×960(태블릿)으로 Unity PreviewScene에서 렌더링: 45장. 실제 여러 기기에서 실행한 결과는 아님.
- 최초 풀길은 주변 바닥과 구분이 약해 바깥을 잎이 있는 타일로 교체. 중앙은 단순한 밝은 바닥으로 유지. 수정 후 재촬영.
- 초기 Lancer 참조 이미지에서 현재 병과와 다른 16 PPU 기본 스프라이트를 사용한 크기 오류를 발견했습니다. 공개 미리보기는 현재 Spearman·Knight·Mage 병과와 실제 몬스터 배율로 교체했습니다. 이전 오류 이미지는 `Previews/History/Stage03_before_obsolete_lancer_scale.png`에 이력용으로만 보관합니다.

## 메모리·렌더링

- 준비 아틀라스 51개, 각 최대 2048, 모든 측정 페이지 POT, Point, 회전/타이트 패킹 OFF, 여백 4, mipmap/readable OFF, Android/iPhone ASTC 4×4.
- 준비 아틀라스 전체의 ASTC 텍스처 페이로드 합계 **약 39.66 MiB**: 가로×세로와 ASTC 4×4 블록 크기로 계산한 값. 실제 기기 메모리 측정값이 아님.
- 같은 준비 페이지의 Unity Editor `Profiler.GetRuntimeMemorySizeLong` 합계 **약 66.20 MiB**. 에디터의 보관 비용이 포함되며 모바일 사용량으로 환산하지 않음.
- 드래곤 색상 9종은 별도 아틀라스로 분리. 선택한 콘텐츠만 로드/포함하도록 준비본의 빌드 자동 포함 OFF.
- 배경은 프리팹당 Renderer 3개. Tilemap chunk 수·실제 draw call은 화면 범위와 통합 환경에 따라 달라지므로 3 draw calls라고 주장하지 않음.
- 새로운 저사양 모드나 카메라/LOD 기능은 도입하지 않음. 현재 프리셋의 장식 Tilemap 분리로 후속 제어 가능.
- 기존 캐릭터·장비 아틀라스는 이동 후 다시 패킹. 장비 313 sprites, 캐릭터 602 sprites 확인. 장비 작업본 재압축 경고 해결 후 깨끗한 재패킹 확인.

## 실행 검사와 미검증 범위

기존 bootstrap → title → 개발용 게스트 → main 흐름을 실행했습니다. 메인 화면 진입까지 확인했습니다. [메인 캡처](Previews/existing-main-regression.png)는 테스트 서버 문제로 전투가 시작되기 전 상태입니다.

발견된 기존 실행 문제:

1. `SFXManager._musicId`의 `Scripts.Core.eSFXType` 직렬화 오류.
2. `CloudScriptAzureFunctionsExecutionTimeLimitExceeded`.
3. `Progression migration incomplete; battle start withheld.`

신규/이동 에셋의 GUID 누락은 없습니다. 전체 검사에서 기존 누락 참조 8개 중 궁수 1개를 복구했습니다. 나머지 7개는 작업 전부터 바이트가 같았던 비활성 Sandbox 씬 2개와 구형 `BuildScriptVirtualMode.asset`에 남아 있습니다. 해당 씬/빌더를 삭제하거나 새 스크립트로 대체하지 않았습니다.

연결된 Android 기기 SM-N986N을 확인하고 현재 프로젝트로 Development APK 빌드를 시도했습니다. 기존 사용자 서명 설정의 **Can not sign the application**에서 실패했습니다. 빌드 결과 Failed, 오류 1, 출력 0 bytes. 서명 설정을 우회하거나 변경하지 않았으며 기기에 새 APK를 설치하지 않았습니다. 기기에서 신규 에셋·전투·성능·터치/복귀를 검증했다고 간주하지 않습니다.

빌드/실행이 자동으로 갱신한 URP 프리필터, 전역 렌더 설정, PlayerSettings 배칭 항목, TMP 동적 폰트 데이터는 작업 전 내용으로 복원했습니다. 세부 전역 설정과 씬의 영구 변경은 없습니다.

## 재시작 조사

작업 중 재시작은 Windows 이벤트의 `MoUsoCoreWorker.exe` / `TrustedInstaller.exe`가 요청한 계획된 Windows Update 재시작이었습니다. 당일 비정상 전원 종료 기록은 발견되지 않았습니다. 자동 재시작 보호 시간을 사용자가 선택한 **06:00–24:00**으로 저장하고 확인했습니다. 자세한 시스템 로그와 기존 값은 기기별 정보이므로 로컬 `.utmp`에만 보관합니다.

에셋 생성은 10개 환경·6개 아틀라스 등의 작은 묶음으로 저장하면서 진행했습니다. 외부 생성 모델/API는 사용하지 않았고 해당 생성 비용은 발생하지 않았습니다.

## 후속 검수: 몬스터 크기·3스테이지 숲

- 실제 왕국군 병과를 기준으로 신규 37종과 기존 Bandit 4종을 검사했습니다. 소형 13, 중형 10, 대형 6, 보스 12종입니다. 몸체와 무기·이펙트를 구분한 프레임별 측정 근거는 [크기 문서](MONSTER_SIZES.md)에 있습니다.
- Unity에서 실제 프리팹의 스프라이트 키 1,446개를 샘플링해 크기·피벗 유지와 참조를 확인했습니다. 충돌체·머티리얼·초기 Sprite 검사 포함 오류 0건입니다. 새 37종은 몸체에 맞춘 트리거와 발 원점을 저장했습니다. 기존 Bandit은 기존 피벗 구조를 유지합니다.
- 초기 프리팹이 희게 뜨던 공용 피격 머티리얼의 기본값 0.446을 확인했습니다. 동일 셰이더의 작업본을 Art/Materials/Monsters에 만들고 기본값 0으로 저장했습니다. 41종이 한 머티리얼을 공유하며 원래 머티리얼과 TinyRPG는 유지했습니다. 새 텍스처·상시 Animator·런타임 스크립트는 추가하지 않았습니다.
- 크기 보정 전 48장, 후 정지·공격·사망 및 왕국군 130장을 공통 카메라로 촬영했습니다. 환경은 30구도 × 3화면 비율로 90장 재촬영했습니다. 해당 해상도를 실제 기기 3대 검증으로 간주하지 않습니다.
- 3스테이지 18종의 Scenery 배치를 2,033개에서 4,421개로 늘렸습니다(약 2.17배). 3개 TilemapRenderer와 동일 Forest 텍스처를 재사용합니다. 타일 수와 겹치는 수관 증가로 메쉬·오버드로 비용은 늘어날 수 있으며 기기 GPU 비용은 아직 측정하지 못했습니다.
- 18개 프리팹의 35,903개 직렬화 타일과 수관 이동 행렬을 계획과 대조했습니다. 장식 전체 사각 경계의 중앙 침범 0건입니다. 360×800 긴 폰부터 720×960 태블릿 비율까지 중앙 인물과 통로가 유지됩니다.
- 이번 후속 작업 전후 스냅샷 비교: 몬스터 41개 + 3스테이지 18개 프리팹 변경. 나머지 72개 환경·모든 스크립트·씬·프로젝트 설정·TinyRPG·왕국군 프리팹은 바이트 동일합니다. 기존 MonoBehaviour 데이터와 프리팹 GUID를 유지했습니다. 외부 원본 변경 0건입니다.
- 편집기 검수 종료 시 Play/Compile OFF, 활성 씬 Dirty=false. Android 재검증은 기존 서명 문제로 수행하지 못했습니다. 기능 연결과 실제 HP바·전투·기기 성능 검증은 후속 통합 범위입니다.

[후속 무결성 결과](Manifests/size-forest-verification.json) · [크기 비교](Previews/monster-size-comparison.png) · [숲 비교](Previews/stage2-stage3-comparison.png)
