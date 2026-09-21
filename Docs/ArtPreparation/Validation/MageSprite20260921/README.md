# 2026-09-21 빠른 코드·마탑 스프라이트 검토

요청 범위: 코드 및 고정 버전 Unity Editor 검토. 연결 기기, APK 빌드/설치, Play Mode 전투 테스트는 실행하지 않았다. 퀘스트 가독성·스테이지 정보·튜토리얼 파일, 씬, 프리팹, 공용 UI 아틀라스 구성은 수정하지 않았다.

## 조치

- `MageTowerManager.AutoCastAll`: 실제 시전을 허용하지 않는 전투 정지 상태에서는 대상 탐색도 즉시 건너뛴다. 기존 `TryCast`의 허용 조건과 같으며 전투 중 타깃 선택·타이밍·쿨다운은 그대로다.
- `MageManualSkillButton`: 진행 중인 시전 때문에 재탭이 거절되면 "스킬을 시전 중입니다."로 안내한다. 이전에는 쿨다운이 먼저 끝난 경우 "시전할 대상이 없습니다."가 표시됐다.
- 실제 마탑 로스터와 `CombatStatusArt`의 스프라이트 의존성을 검사하고, 표시용 스프라이트의 미사용 자동 물리 윤곽 생성만 끈다. 런타임 코드에 스프라이트 물리 윤곽을 읽는 호출이 없고, 검사 대상 프리팹에 PolygonCollider2D가 없음을 확인했다.
- 버전은 수정 릴리스 `0.14.1`. 배포 빌드는 없으므로 Android versionCode는 유지한다.

## 검증 기준 및 결과

세부 결과는 [audit.json](audit.json)에 기록한다. `MageSpriteAudit.Optimize`는 Unity 6000.3.21f1에서 에셋을 재임포트한 후, 각 스프라이트의 GUID/fileID, 이름, rect, pivot, border, PPU, 렌더링 vertices/triangles/UV가 전후 동일한지 대조한다. 불일치하면 해당 임포트 설정을 복구하고 실패한다. 이미지 픽셀, 해상도, 색상, 압축, 애니메이션과 피해 판정은 변경하지 않는다.

최종 실행 통과: 128개 의존성, 43개 텍스처 검사. 42개 텍스처에서 자동 물리 윤곽 정점 13,714개를 제거했고 렌더링 데이터는 모두 동일했다. 나머지 1개는 이미 자동 윤곽 생성이 꺼져 있었다. 컴파일 오류 및 검사 오류 0건, 아틀라스 재패킹 완료, Unity 종료 코드 0. Git 비교에서도 42개 `.png.meta`의 의미 있는 변경은 `spriteGenerateFallbackPhysicsShape: 1 → 0`뿐이며 PNG 바이트 변경은 없다.

43개 사용 텍스처는 기존부터 아틀라스에 포함되어 있으며 Point 필터, mipmap OFF, readable OFF를 사용한다. 마탑 VFX 및 픽셀 UI 아틀라스는 include-in-build, Android ASTC 4×4, 최대 2048 설정을 사용한다. 원본 텍스처의 Uncompressed 설정만 보고 중복 압축하지 않았다. 런타임 GPU 메모리·FPS 절약량은 측정하지 않았으며, 물리 윤곽 정점 감소량을 FPS 개선 수치로 해석하지 않는다.

최초 검사에서는 일회성 애니메이션의 마지막 `null` 키를 누락 참조로 잘못 분류해 최적화 전에 중단됐다. `MageSkillAssetPreparation`이 의도적으로 삽입하는 종료 키임을 원본 클립과 생성 코드에서 확인하고 검사만 보정했다. 클립은 변경하지 않았다. 재실행 결과가 `audit.json`이다.

로그: 로컬 `Logs/mage-sprite-review-20260921.log`, `Logs/mage-sprite-review-20260921-r2.log`. 로그 및 Unity 캐시는 커밋하지 않는다.

## 발견했지만 변경하지 않은 항목

`Assets/_Project/Scripts/Core/Manager/SFXManager.cs`의 `LoadClipAsync`는 Addressables 요청 핸들을 `_Handles`에 넣은 뒤 기다린다. 요청 실패 시 catch에서 이 핸들을 제거하지 않아, 다음 요청이 "이미 로딩 중" 분기로 빠지고 재시도하지 못할 수 있다. 동시 요청·취소·핸들 소유권을 함께 검증해야 하므로 이번 빠른 수정 범위에서 제외했다. 코드 경로로 확인한 가능성이며 실제 로딩 실패 재현은 하지 않았다.

이 검토는 전체 게임의 무결성을 보증하는 심층 감사가 아니다. 제외된 팀원 작업 영역, 실제 전투 연출·터치·Android 압축 결과와 성능은 이번에 검증하지 않았다.
