# 우편함 구조 복원 검증 — 0.19.4

2026-11-01, Unity **6000.3.21f1**, Windows Editor. Android versionCode는 **66**으로 유지했다. APK·플레이어 빌드와 Android 기기 검증은 수행하지 않았다.

이번 기록은 `50b2c639e` 머지 이후 구현을 기준으로 한 새 검증이다. [0.19.0 당시 기록](../mailbox-server-20261031/README.md)은 과거 결과로 보존한다.

## 구현과 검증 범위

- `MailboxService` 인스턴스가 `MailDTO`와 long ID를 소유한다. 목록 조회·수령·복구 상태와 화면 열림을 구별하며 Controller는 이벤트로 갱신한다.
- 100통 조회는 50+50, 모두 받기도 클릭 시점 ID를 50+50으로 처리한다. 페이지 불일치는 한 번 재조회하며 전체 검증 전 수령을 막는다.
- 각 묶음의 검증·저장이 끝나야 성공으로 누적한다. 응답 유실·저장 실패에는 자동 수령 재전송 없이 플레이어 상태와 우편 목록을 확인한다. 복구 실패는 수령을 계속 차단한다.
- 기존 프리팹·버튼·공통 보상 팝업을 사용한다. 장비 강화·만료·보관 위치가 다르면 합치지 않으며 자동 분해는 실제 받은 강화석으로 표시한다.
- 과거 로컬 저장 형식은 유지한다. 로컬 지급은 Editor 전용 격리 테스트 계정에서만 가능하다. 출석·광고·재접속 보상 정책은 변경하지 않았다.

## 자동 검사 결과

**214개 검증 항목 통과.** 아래 수치는 독립된 사용자 시나리오 수가 아니라 각 실행의 assertion 수다.

| 실행 | 통과 | 결과 |
|---|---:|---|
| `MailboxAcceptance.Run()` | 51 | [scenarios.json](scenarios.json) |
| `MailboxAcceptance.RunLocal()` | 8 | [local.json](local.json) |
| `MailboxAcceptance.RunServerApply()` | 134 | [storage.json](storage.json) |
| `MailboxUiAcceptance.VerifyRecycling()` | 9 | [ui-recycling.json](ui-recycling.json) |
| `MailboxUiAcceptance.VerifyCloseReopen()` | 3 | [ui-lifecycle.json](ui-lifecycle.json) |
| `MailboxUiAcceptance.VerifyAnchorAndCompatibility()` | 9 | [ui-compatibility.json](ui-compatibility.json) |

조회 0·1·50·51·100통, 2페이지 실패, 중복 ID·개수 변경, 제한된 자동 재조회, 30초 자동/즉시 수동 재시도를 확인했다. 수령은 개별·100통, 두 번째 묶음 실패의 부분 성공, 만료·이미 수령한 우편, 중복 클릭, 고정 대상 ID를 검사했다.

닫기·재진입과 계정 변경 시 오래된 응답을 무시하고, 닫은 뒤 같은 계정의 지급·복구는 완료한다. 응답 유실·잘못된 응답·저장 실패·서버 StorageFailure의 복구, 복구 후 목록 실패에 따른 계속 차단, 수동 재시도를 확인했다. 확인되지 않은 지급을 성공 팝업으로 표시하지 않았다.

저장 검사는 Inventory/Pending/Reserve/AutoDismantled × 성공/파일 저장 실패/응답 유실의 조합이다. 가짜 서버 응답으로 제품의 `MailboxService.Apply → LocalProgression.Execute → UserManager.ApplyCurrency/ApplyEquipment` 및 파일 재열기를 실행했다. 10종 재화의 절대값, 장비 ID·잠금·장착 보존, 같은 응답 재적용 시 중복 없음, 원래 계정 복원을 검사했다. 실제 서버 HTTP 검증과는 구별한다.

## Editor·프리팹·화면

[compile.json](compile.json)의 최종 컴파일 결과는 `completed`, `failed=false`, 오류 0건이다. [prefabs.json](prefabs.json)에 기록한 우편함·행·보상 슬롯·보상 팝업·출석판 5개 프리팹의 누락 스크립트는 0개이고 검사한 필수 참조 37개가 연결됐다. 프리팹 파일과 GUID를 변경하지 않았다. Content 자동 배치 해제와 가상 목록 연결은 실행 중 수행한다.

bootstrap Play의 로그인 전 화면 위에서 실제 프리팹을 사용하는 격리 미리보기를 실행했다. 이는 로그인·전투 맥락의 실제 서버 검증을 대신하지 않는다. 스크롤 100개 위치에서 행 오브젝트 ID가 유지됐으며 이전 첨부·클릭 대상이 남지 않았다. 가로 스크롤 초기화, 수령 후 기준 우편/인접 행의 위치 유지, 목록 오류와 빈 목록 구별, 닫기/뒤로가기 연결, 출석 보상 슬롯·팝업과 재접속 보상 확인 콜백을 검사했다.

| Game View 크기 | 100통의 행 풀 | 전체 프레임 batches / vertices | 캡처·측정 |
|---|---:|---|---|
| 1080×1920 | 6 | 별도 기록 없음 | [phone.png](phone.png), [행 재사용 검사](ui-recycling.json) |
| 480×800 | 5 | 28 / 3,076 | [narrow.png](narrow.png), [narrow.json](narrow.json) |
| 1440×2960 | 7 | 31 / 3,424 | [long.png](long.png), [long.json](long.json) |
| 1200×1600 | 4 | 25 / 2,660 | [tablet.png](tablet.png), [tablet.json](tablet.json) |

행 높이 346, 간격 14, 100통 Content 높이 35,994를 유지한다. 화면 크기를 바꿔도 목록 요청은 최초 2회에 머물렀다. 행 풀은 `ceil(viewport 높이 / 360) + 2`이며 각 행의 첨부 슬롯 10개를 재사용한다. 기존 코드의 우편별 전체 행 생성이 화면에 필요한 행 수로 제한됐다. 이전 버전의 CPU·메모리 실측 비교는 하지 않았다.

네 크기에서 제목·첨부·수령 버튼·하단 버튼의 겹침 없이 기존 배치를 유지했다. JSON의 managedBytes와 batches/vertices는 타이틀·Editor 진단을 포함하는 값으로 우편함 단독 비용이나 실기기 성능 지표가 아니다. 한 Editor에서 Game View 크기를 바꾼 검사이며 여러 실제 기기 검증이 아니다. 수정 전의 같은 머지 버전 캡처는 없어 과거 기록과 구별한다.

## 작업 중 발견·조치

- 초기 컴파일에서 DTO에 없는 시각 필드와 namespace 누락을 수정한 뒤 정상 컴파일했다.
- Unity CLI의 자동 호환 명령 일부(`script recompile`, `editor status`)가 설치된 Pipeline에 없어 진단 오류가 남았다. 실제 제공된 `recompile_status`, `eval`, `console_status`로 상태를 확인했다. 명령 응답을 기다리는 중 도메인 재로드로 한 번 시간 초과되어 준비 상태 확인 후 다시 실행했다.
- 프레임 종료 전 직접 스크린샷 호출이 실패해 `capture_game_view`로 촬영했다. 이 도구가 `Assets/AI`에 저장한 캡처는 현재 기록 폴더로 옮기고 생성된 빈 폴더·meta를 정리했다. 최종 Console에는 도구 호출/촬영 실패 기록이 남지만 최종 컴파일 실패는 없다.
- Play를 종료하고 원래 bootstrap 씬(저장되지 않은 변경 없음), 1080×1920 Game View, runInBackground=false로 복원했다. 추가한 태블릿 QA 프리셋과 프리뷰 오브젝트를 제거했고 테스트 중 생성된 폰트 atlas 변경은 원본으로 복구했다.

## 미검증 범위와 서버 한계

- 실제 서버 수령·개발용 우편 발송은 하지 않았다. 제공된 로컬 서버 저장소 `d09f3f5`에서는 내부 `MailRules.Send`와 테스트용 데이터 외에 새 테스트 발송 HTTP API를 확인하지 못했다. 원격 GitHub 조회도 404여서 최신 원격 구현 여부는 확인하지 못했다.
- 서버의 여러 문서 저장에는 원자성 보장이 없다. 클라이언트 재조회는 서버가 보고하는 현재 값을 적용할 뿐, 서버 자체의 부분 저장을 복구하거나 지급의 정확히 한 번 처리를 보장하지 않는다.
- 목록 API에 스냅샷 버전이 없으므로 두 페이지 사이에서 총개수·ID 중복에 드러나지 않는 변경까지 완전히 검출할 수는 없다.
- 출석의 월 전환·이월 정책, 광고 SDK·실제 재접속 지급, 실제 터치·Android 뒤로가기·안전 영역은 이번 변경의 검증 대상이 아니다.
