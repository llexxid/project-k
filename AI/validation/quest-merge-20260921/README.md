# 퀘스트 브랜치 develop 병합 검증 — 2026-09-21

기준: feat/quest `7914dde64` + develop `b2a030dd9`. Unity 6000.3.21f1.

## 반영한 구조

- 카드·패널 배치는 feat/quest를 유지한다. 기존 GUID manifest로 외부 원본 스프라이트 참조만 프로젝트 작업본에 연결했다.
- UI는 `QuestManager.GetSnapshot`과 `QuestsChanged`를 사용한다. `BalanceQuestPanel`과 `GuideGoalView`의 입력은 `TryClaim` 또는 공통 `QuestNavigation`으로 전달된다.
- `TryClaim`은 표시 당시 계정 세대·퀘스트 ID·기간을 검증하고, 재화와 수령 기록을 하나의 `LocalProgression.Execute`에서 저장한다. 성공 알림은 입력을 받은 UI 한 곳에서만 표시한다.
- `LocalProgression`은 직접 작성한 `DeepClone`으로 거래 복제본을 만들고, 검증·파일 교체 성공 뒤 상태와 알림을 공개한다. 완료 ID 집합과 보류 보상의 내부 항목도 복제한다.
- 스킬 횟수는 develop의 기존 메모리 집계와 0.75초 자동 저장을 유지한다. 별도 표시 타이머나 UI 변경 번호는 추가하지 않았다. 자동 저장의 알림은 성공 시 기존 `Changed`로 전달한다.
- 계정 전환은 `_opening` 보호 안에서 이전 전투 시간 → 남은 스킬 집계 저장 → 새 계정 퀘스트·마법 이관 및 저장 → 계정 세대 증가·알림 순서다.
- 기간 경계의 첫 스킬 사용은 기존 거래 경로에서 이전 기간 전투 시간과 함께 처리한다. 정상 시전은 계속 묶음 저장한다.
- 경계 저장 실패 시 `RecordSkillCast()`가 false를 반환하고 `MageTowerManager`가 시전·쿨다운 시작 전에 중단한다. 집계되지 않은 효과만 실행되는 일을 막으며, 별도 재시도 큐 없이 다음 시도에서 복구한다.
- 장비 복원은 `ImportLegacy`로 초과 수량을 보존하며 획득 퀘스트에 세지 않는다. 실제 `Grant` 지급은 기존대로 집계한다.
- 동적 골드는 확장 스테이지 카탈로그를 사용한다. 신규 보류 보상은 수령 시 계산, 이관한 기존 고정 골드는 보존한다.

- 목적지 패널이 중첩된 뒤 복귀하면 Gacha/KingdomArmy/Development/Inventory 정적 컨트롤러 연결을 복구한다. 같은 인스턴스의 일반 복귀는 재구성하지 않는다. Gacha 콘텐츠 캐시는 다른 패널에 재사용하지 않는다.

## 검증 결과

- `core-regression-final.json`: 퀘스트 79, 기간·계정 33, UI snapshot 31, 경제 102, 복제·자동 저장 19 — 총 264개 통과. 이전 261개 통과 기록인 `core-regression.json`도 보존한다.
- `playmode-tabs.json`: 실제 프리팹을 사용한 격리 PlayMode 탭·수령·완료 카드·재접속 48개 통과.
- `pointer-input.json`: 540×960 GameView에서 실제 GraphicRaycaster와 EventSystem을 통한 탭·스크롤·수령·닫기·메뉴 복귀 검사 52개 통과. 소환 목표 2, 장비, 전직, 육성, 인벤토리의 중첩 이동 후 기존 패널 조작을 포함한다.
- `prefab-references.json`: UI 직렬화 참조 400개 검사, 끊긴 참조·missing script·ExternalAssets 의존성 없음. 행 높이 192 유지.
- `catalog-and-scene.json`: bootstrap/구형 가이드/UI root 참조 160개 검사. Excel과 배포 JSON의 guide/quests/achievements/rewards 일치.
- `captures.json` 및 PNG: 360×800, 540×960, 900×1200의 실제 프리팹 PreviewScene 캡처. 동일 PC의 비율 변경이며 실제 기기나 전투 배경 캡처가 아니다.
- 최종 회귀 검사와 PlayMode 검사 합계 364개 통과. 직렬화 참조 검사는 별도 수치다.
- 최종 캐시 snapshot 조회 10,000회: 0.0712ms, 관리 힙 할당 0 bytes. 격리 경제 테스트 평균 내구 저장: 7.599ms, 1,660-byte 샘플. PC 측정이며 실계정/Android 성능 상한을 의미하지 않는다.

## 발견·수정·재검증

- 첫 컴파일에서 확장 Stage enum namespace 누락을 수정했다.
- `core-regression-initial.json`은 실패한 초기 검사 기록이다. EditMode의 DontDestroyOnLoad, 구형 스키마 fixture, 일일 퀘스트 해금 누락을 보완한 후 전부 통과했다. 실제 저장 검증 조건은 완화하지 않았다.
- `pointer-input-initial.json`은 develop의 LayerOverlays 이동 전 탐색 경로를 사용한 도구 실패다. 실제 패널이 위치한 LayerOverlays를 찾도록 수정했다.
- `pointer-input-batch-landscape.json`은 기본 BatchMode 화면 640×480에서 세로 패널이 잘려 발생한 입력 검사 실패다. 임시 540×960 GameView로 바꾸고 전체 52개 검사를 통과했다. 게임의 화면 정책은 변경하지 않았다.
- 입력 검사 도구는 BatchMode에서 지원되지 않는 WaitForEndOfFrame 및 GameView 촬영을 건너뛰고 실제 EventSystem raycast로 검사한다. 화면 이미지는 별도 PreviewScene 캡처로 남긴다.
- 과거 검증 디렉터리는 덮어쓰지 않도록 두 캡처 도구에 출력 폴더 인자를 추가했다.
- 최종 저장 검토에서 자정 경계 거래 실패 후 집계 없이 시전되는 경로를 발견했다. 집계 결과를 호출부에 반환하도록 수정했고, 실패 시 상태·전투 시간 버퍼 보존 및 복구 후 1회 집계 검사 3개를 추가해 통과했다. 시전/쿨다운 시작 전 반환 순서는 호출부 코드에서도 확인했다.
- UI 비교 기준은 `../quest-cards-20260920/after-*.png`의 병합 전 최종 UI다. 이번 `after-*.png`와 비교했고, 프리팹 구조 자체도 HEAD와의 GUID 치환 비교로 보존을 확인했다. 해당 과거 폴더의 `before-*`는 이전 UI 개발의 원본이며 이번 병합 전 상태와 구분한다.

## 제한

- Android `adb devices -l`에 연결 기기가 없어 실제 기기의 전투·안전 영역·터치·저장 지연은 검증하지 못했다.
- 0.75초는 저장 시작 간격이며, 디스크 지연·실패를 포함한 표시 지연의 상한 보장은 아니다.
- 병합 후 정상적인 컴파일과 격리 테스트를 검증한다. 외부 에셋 원본의 대량 삭제는 develop에서 들어온 기존 변경이며 이 작업에서 되돌리거나 추가 삭제하지 않았다.
- 10개 충돌을 해결해 Git unmerged 항목과 충돌 마커가 0개다. 관련 구현·검증 기록을 스테이징했고 HEAD 및 MERGE_HEAD는 변경하지 않았다. 머지 커밋과 push는 수행하지 않았다.
- 약 5.5만 개의 전체 staged 변경은 develop 병합에서 들어온 대량 에셋 이동·삭제 등을 포함하므로 그대로 남는다. 충돌 해결은 이 변경을 취소하거나 머지 커밋을 대신하지 않는다. 세부 상태는 `git-resolution.json`에 기록했다.

코드의 실행 순서와 각 파일의 책임, 후속 확장 지점 및 이해 확인 질문은 [코드 설명](code-walkthrough.md)에 기록했다.
