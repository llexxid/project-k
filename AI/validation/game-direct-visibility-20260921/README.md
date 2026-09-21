# 안내 최초 표시 중 NullReferenceException 회귀

Unity 6000.3.21f1 / 0.17.1 / Android versionCode 60 유지.

## 재현 및 원인

사용자 콘솔의 `FeatureGuideView.Show:73` 예외를 실제 안내 프리팹으로 재현했다. 대상이 화면 또는 조상 마스크 밖이면 기존 `RefreshLayout()`이 `Hide()`를 호출해 `_clipMasks`를 비웠다. `Show()`가 그대로 계속되어 `_clipMasks.Length`를 읽으며 예외가 발생했다. 재현 당시 실패 기록은 [before-fix.json](before-fix.json)에 보존한다.

## 수정

- `TryRefreshLayout()`은 표시 가능 여부를 반환하며 참조를 해제하지 않는다. `Show()`는 false일 때 숨김·입력 정리 후 즉시 반환한다. `LateUpdate()`도 같은 반환값으로 숨김을 결정한다.
- 최초 표시 대기는 완료나 건너뛰기를 기록하지 않는다. 기존 Player가 다음 프레임에 같은 미확인 단계를 다시 시도한다.
- GameTest에서 이미 완료·건너뛰기 상태인 안내는 경고 대신 ID를 포함한 일반 생략 로그를 남긴다. 저장 이력은 초기화하지 않는다.

## 최종 검증

[acceptance.json](acceptance.json): 전체 PlayMode **340개 검사 통과**.

- 실제 프리팹 최초 표시 시 화면 밖 대상과 마스크 밖 대상을 각각 재현했다. 예외·입력 차단 잔류 없이 대기하며 대상 복귀 후 같은 View로 표시된다. 표시 중 대상 이탈도 정리된다.
- 격리 계정에 다른 네 안내의 완료 기록과 육성의 미확인 강화 단계만 준비했다. 실제 `GameTest.TestFirstStartGuide()` 호출로 완료 네 개를 생략하고 육성만 재개하는 것을 확인했다. 취소 후 미완료 유지와 입력 복구도 통과했다.
- 기존 다섯 안내 FIFO, 강화·뽑기 저장 거래, 중단·계정 전환·지급 실패·중복 소비 방지·환생 설명 회귀와 세 화면 비율 검사가 통과했다.
- 컴파일 오류 없음. 종료 후 bootstrap 편집 씬으로 복원했다.

격리 계정·실제 UI 프리팹·합성 EventSystem 입력으로 검사했다. 사용자 계정의 진행·재화는 수정하지 않았다. 실전 전투·Android 기기 검증, 커밋·푸시·APK 빌드·기기 설치는 하지 않았다.
