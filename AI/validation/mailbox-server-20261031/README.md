# 서버 우편함 0.19.0 검증

작업·검증: 2026-10-31~2026-11-01. Unity **6000.3.21f1**, Windows Editor. 실제 서버 저장소는 수정하지 않았다. 최신 사용자 지시 **“빌드는 하지마”**에 따라 진행 중이던 Android 빌드는 종료됐으며 추가 빌드는 하지 않았다. APK는 생성되지 않았다. Android 설치·터치·실제 로그인 검증도 제외했다.

## 적용 구조

`MailboxPanelController → MailboxService → NetworkManager.Call → OnGetMailList / OnClaimMailReward`로 연결한다. 열 때 50통씩 최대 100통을 조회하고 닫으면 메모리 목록을 비운다. 개별/전체 수령은 서버 응답의 Currency·Equipment를 UserManager의 기존 반영 함수로 저장한다. 확정된 우편은 즉시 제거하고 0번부터 다시 조회한다. 수령 요청은 자동 재전송하지 않는다.

기존 우편함·행·보상 슬롯·팝업·출석 프리팹의 파일과 GUID는 변경하지 않았다. VirtualizedMailList를 실행 중 추가하고 기존 Content의 Layout/Fitter만 비활성화한다. 삭제 버튼은 보존·비활성화하고 수령 시 자동 삭제 문구를 표시한다. 장비는 기존 데이터베이스의 이름·아이콘·희귀도를 사용하며 획득 결과의 자동 분해·대기·예비 보관을 구별한다.

로컬 MailMessage·Claims·출석 기록·보상표는 보존한다. 기존 로컬 우편 지급은 Editor 전용 LocalMailboxService로 이동하고 격리 계정만 허용한다. 출석 자동 기록·로컬 지급·실계정 테스트 지급 메뉴는 중지했다. 재접속 보상 수령 경로는 유지했다.

## Editor 검증

- `MailboxAcceptance.Run`: 35개 검사 통과. 0/1/50/51/100통, 두 번째 페이지 실패, 중복/개수 변경의 한 번 재조회, 모든 ID의 long 유지, 개별/50+50 수령, 두 번째 묶음 실패, 클릭 이후 도착한 우편 제외, 중복 클릭 차단, 닫기/재진입/계정 변경, 응답 유실, 저장 실패, 복구 실패/재시도, 만료·이미 수령한 우편. 마지막 검토에서 서버 저장소 오류도 Business로 포장된다는 점을 발견하여 StorageFailure/InvalidStoredData 역시 플레이어 상태를 재조회하도록 보완하고 재검증했다.
- `MailboxAcceptance.RunLocal`: 8개 검사 통과. 실제 계정 접근 거절, 격리 생성·수령·삭제·중복 방지, 강제 저장 실패 시 미지급, 종료 후 원래 계정 키와 직렬화된 상태 동일.
- `MailboxUiAcceptance.Open(100)` 및 `VerifyRecycling`: 9개 검사 통과. 101개 스크롤 위치에서 100통 전체를 재연결, 행 인스턴스 동일, 통신 증가 없음, 첨부 개수와 클릭 대상 일치, 가로 위치 초기화, 비활성 삭제 버튼 보존, 실제 결과의 자동 분해 표시.
- 실제 5개 프리팹의 Missing Script 0개. 출석 28칸 보존·비활성화와 준비 중 표시 확인. 기존 재접속 보상 팝업에서 골드 +100/경험치 +200 및 확인 버튼 유지 확인.
- Unity import·컴파일 후 위 검사를 실행했다. 검증 미리보기는 로그인 전 bootstrap에서 실제 프리팹과 가짜 응답을 사용했으며 운영 계정이나 서버를 조작하지 않았다.

| Editor GameView | 논리 viewport 높이 | 재사용 행 수 | 행 높이 / 간격 |
| --- | ---: | ---: | --- |
| 1080×1920 / 360×640 | 1152 | 6 | 346 / 14 |
| 720×1600 | 1632 | 7 | 346 / 14 |
| 1200×1600 | 672 | 4 | 346 / 14 |

100통의 Content 높이는 35,994이다. 재조회 뒤 99통에서는 35,634였다. 초기 조회는 2회, 스크롤 중 추가 조회 0회다. 변경 전 코드는 목록의 모든 우편마다 행을 생성했고, 변경 후에는 위 풀 수만 사용한다. 이 비교는 코드와 현재 오브젝트 수를 근거로 하며 FPS·GPU·기기 메모리 개선율을 측정한 것은 아니다. 슬롯은 풀 행마다 최대 10개를 재사용하고 새 텍스처·아틀라스·폰트는 추가하지 않았다.

해상도별 캡처는 한 Editor의 GameView 크기를 바꾼 결과로, 여러 실제 기기 검증이 아니다. 기존 레이아웃·가로 첨부·스크롤 마스크·팝업 표시를 확인했다. 변경 전 실행 캡처는 남기지 않았으므로 프리팹 파일 무변경과 변경 후 캡처를 근거로 비교한다. 안전 영역·모바일 뒤로가기·실제 터치·전투 중 표시 성능은 미검증이다.

## 실행 자료

- 자동 검사 결과: `acceptance.json`, `ui-recycling.json`, `prefabs.json`.
- 로컬 캡처: [우편함](../../../PersonalDocs/Validation/mailbox-server-20261031/mailbox-phone.png), [좁은 화면](../../../PersonalDocs/Validation/mailbox-server-20261031/mailbox-360x640.png), [긴 화면](../../../PersonalDocs/Validation/mailbox-server-20261031/mailbox-720x1600.png), [태블릿 비율](../../../PersonalDocs/Validation/mailbox-server-20261031/mailbox-1200x1600.png), [보상 결과](../../../PersonalDocs/Validation/mailbox-server-20261031/reward-popup-portrait.png). 원본 캡처는 개인 검증 폴더에 보존하고 앱 에셋·Git에는 포함하지 않는다.
- 시도한 빌드는 `Recordings/MailboxServer0190/build.txt`와 `build-messages.txt`에 남아 있다. 결과는 Failed, `TaskCanceledException`이며 진행 중 GvhProjectSettings.xml의 Windows 파일 잠금 오류(1224)도 기록됐다. 재빌드·빌드 환경 수정은 하지 않았다. 버전 0.19.0, 빌드 시도에서 예약된 Android 번호 66을 유지한다. 임시 패키지명·앱 이름·서명·기기별 설정·자동 생성 변경은 복원했다. 종료 후 `BuildPipeline.isBuildingPlayer=false`를 확인했다.

## 서버 통합에서 남은 확인

실제 개발 우편으로 조회·수령하는 통합 검증은 실행하지 않았다. 공유된 서버에는 우편 발송 API가 없어 서버 담당자가 개발 계정의 우편을 준비해야 한다. 테스트용 가짜 응답 통과를 실제 배포 서버 검증으로 간주하지 않는다.

서버는 우편함·재화·장비 등 여러 문서를 원자적으로 저장한다고 보장하지 않는다. 클라이언트의 재조회는 저장된 서버 상태를 다시 맞추는 동작이며 서버의 부분 저장에 따른 누락·중복을 복구하는 거래 보장은 아니다. 수령 ID와 결과를 영속적으로 확정하는 서버 처리가 필요한 한계다. 페이지에 snapshot revision도 없으므로 중복 ID·개수 변경은 탐지하지만 개수가 같은 동시 변경을 모두 탐지할 수는 없다.

월별 출석 DTO, 미수령 이월, 출석 +N 표시, 우편함 100통 포화 시 이월 정책은 이번 범위에 포함하지 않았다. 출석 준비 상태를 해제할 때 새 서버 계약에 맞춰 구현한다.

## 0.19.1 — 닫기 직후 재진입 회귀 수정 (2026-11-01)

- `MailboxPanelController.Awake`에서 기존 `BottomSheetView.OnClosed`에 컨트롤러 비활성화를 연결했다. 스택에서 빠지는 즉시 기존 OnDisable이 구독·서비스 연결을 정리하고, 퇴장 중 Update의 재연결도 중단한다. 애니메이션·프리팹·서버 계약은 변경하지 않았다.
- Unity 6000.3.21f1 bootstrap Play에서 실제 우편함 프리팹과 격리 응답으로 `MailboxUiAcceptance.VerifyCloseReopen()`을 실행했다. 수정 전에는 `closing panel invalidated the reopened mailbox`로 실패했고, 수정 후에는 이전 패널 지연 종료/재진입·가림 후 복귀·재진입 수령 3개 검사가 통과했다(적용 1회, 목록 요청 4회). 기존 `MailboxAcceptance.Run()` 35개 검사도 통과했다.
- Unity import·컴파일 성공, Panel_Mailbox의 Missing Script 0개와 필수 참조를 확인했다. Play 프레임은 141에서 1892로 진행했다. 검사 오브젝트를 정리하고 원래의 bootstrap 편집 상태로 복귀했다.
- 표시·레이아웃·텍스처·행 풀 변경은 없어 캡처 및 성능 측정을 반복하지 않았다. Android 빌드·설치·실기기와 실제 서버 수령은 검증하지 않았다. bundleVersion은 0.19.1, 배포하지 않았으므로 Android versionCode는 66을 유지한다.
- 기존 GvhProjectSettings.xml 파일 잠금 오류(1224)와 CLI status의 구버전 Pipeline 명령 호환 오류(`editor status`)는 별도 환경 문제로 남아 있다. 우편함 컴파일·회귀 검사는 정상 완료했으며 해당 설정 파일은 수정·커밋하지 않았다.

## 0.19.2 — 미수령 장비 표시와 실제 저장 경로 검증 (2026-11-01)

- `RewardDisplayItem.From(MailRewardDTO)`에서 `Disposition.None`을 명시한다. 미수령 아이콘·우편 상세의 장비 설명에 `장비 보관함에 보관했습니다.`가 붙던 문제를 한 곳에서 수정했다. 수령 결과는 기존 `Granted` 경로의 서버 disposition을 유지한다.
- `MailboxAcceptance.RunServerApply()`를 추가했다. 테스트에서 같은 제품 함수를 호출할 수 있도록 `MailboxService.Apply`의 접근성만 internal로 바꾸었다. 별도의 보상 적용 구현은 추가하지 않았다.
- Unity 6000.3.21f1 Edit에서 134개 검사 통과. 일반·대기·예비 보관·자동 분해 각각에 성공/실제 파일 쓰기 실패/응답 유실을 적용한 12개 조합이다. 수령 DTO를 JSON 왕복 후 `MailboxService.ClaimAll → ValidateClaim → Apply → UserManager.ApplyCurrency/ApplyEquipment → LocalProgression.Execute`로 적용하고 계정 파일을 다시 열어 저장값이 같은지 비교했다.
- 8종 재화 절대값, 장비 종류·보관 위치·수량, 기존 ID·잠금·장착·강화석 지출, 자동 분해 강화석, 동일 스냅샷 재적용 시 중복 없음, 원래 계정·상태 복원을 확인했다. 저장 실패/응답 유실 시 메모리·파일 불변, 성공 보상 미표시, 복구 중 수령 차단, 복구 실패 후 재시도와 수령 요청 1회 유지도 확인했다.
- 복구 콜백은 보류/실패/성공을 주입하며, 복구 성공 시 테스트 응답을 제품의 `MailboxService.Apply`로 저장한다. 실제 HTTP·PlayFab, `UserManager.RefreshFromServer`의 전체 OnGetUserState 적용, Edit에서 존재하지 않는 런타임 매니저의 장비/퀘스트 갱신은 이 검사의 범위가 아니다. 서버 부분 저장의 거래 보장을 입증하는 검사도 아니다.
- 미수령 표시 검사는 수정 전 `unclaimed preview has no storage result`로 실패했고 수정 후 통과했다. bootstrap Play의 실제 프리팹에서 장비 아이콘을 눌러 `미수령`, 수령 요청 0회, 설명 `나무로 만든 훈련용 대검이다.`만 표시되는 것을 확인했다. 1080×1920 Game View 원본 캡처는 `PersonalDocs/Validation/mailbox-preview-20261101/details.png`(이전)와 `details-fixed.png`(수정 후)에 보존했다. 타이틀 위 격리 우편 미리보기이며 실제 서버 계정·Android 캡처와 구별한다.
- 기존 `MailboxAcceptance.Run()` 35개, `RunLocal()` 8개, Play의 `VerifyCloseReopen()` 3개도 통과했다. 레이아웃·에셋·풀은 변경하지 않았고 새 테스트는 Editor 전용이다. 렌더링 비용 측정·다른 화면 비율·Android 빌드 및 실기기 검증은 반복하지 않았다. 버전 0.19.2, 배포하지 않아 Android versionCode 66 유지.
- 최종 Unity import·컴파일 성공, Panel_Mailbox 누락 스크립트 0개·content/closeButton/claimAll 참조 정상. 원래 bootstrap 편집 상태로 복귀했고 씬 미저장 변경은 없다. 기존 GvhProjectSettings.xml 변경은 보존하고 커밋에서 제외했다.
