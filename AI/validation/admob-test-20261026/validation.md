# AdMob 단독 테스트 검증

- 작업: 2026-10-26~27 KST, Unity 6000.3.21f1 / Google Mobile Ads 11.6.0.
- 범위: 별도 AdMobTest 씬, 광고 보기 버튼 1개, 완료 팝업, 공통 RewardedAdService.
- Google Android 보상형 테스트 ID만 테스트 씬에 고정했다. 로그인·계정·게임 재화는 호출하지 않는다.

## Editor 결과

- 실제 Play Mode에서 frameCount 584 → 1188 → 3590 증가를 확인했다.
- 실제 Button.onClick 경로로 SDK 초기화 → 테스트 광고 로드 → SDK 모의 광고 표시를 확인했다.
- SDK countdown 보상 이벤트 후 Close Ad를 누르면 완료 팝업이 나타나고 요청 상태와 버튼이 복구된다.
- 처리 중 버튼은 비활성화되고, 다른 서비스 요청은 Busy를 반환한다.
- 빈 광고 ID는 Failed를 한 번 반환하며 서비스를 잠그지 않는다.
- SDK 모의 광고의 닫기 이벤트를 countdown 완료 전에 직접 호출하는 취소 시뮬레이션에서 완료 팝업이 나타나지 않았고 IsBusy가 해제됐다. 이 검증은 실기기 조작이 아니다.
- 취소 후 새 광고 요청과 정상 완료를 다시 확인했다.
- 처음에는 공용 생성기의 오래된 폰트 GUID가 LiberationSans를 가리켜 한글이 사각형으로 보였다. 기존 Galmuri11 SDF 에셋의 명시 경로로 연결해 재확인했다. 폰트나 아트를 새로 만들지 않았다.

캡처: `editor-ad.png`(SDK 모의 광고), `editor-complete.png`(폰트 수정 전), `editor-button.png`(최종 버튼), `editor-complete-fixed.png`(최종 완료 팝업). 540×960 Editor 캡처이며 실기기 화면은 아니다. UI는 텍스트 3개와 단순 Image/Button만 추가하며, 상태 변경 때만 텍스트를 갱신한다. 별도 성능 프로파일링은 하지 않았다.

## 실제 보상 연결 경계

- Completed는 광고 시청 완료이며 실제 보상 저장 성공을 의미하지 않는다.
- 오프라인 기본 보상은 이미 지급되므로 원래 정산의 고유 ID·확정 금액을 캡처하고 추가분만 지급해야 한다. 광고 복귀를 새 오프라인 정산으로 처리하지 않는 연결도 필요하다.
- 일일 광고는 보상·횟수·날짜 귀속 정책 확정 후 고정 claim ID를 사용해야 한다.
- 두 경로는 계정 세대 확인과 LocalProgression.Execute 안의 지급·claim 기록을 함께 적용해야 한다.
- Google 광고의 보상 → 닫힘 순서를 사용한다. Unity Ads 미디에이션을 추가할 때 순서를 재검증한다.
- 프로세스 종료 후 보상 복구, UMP 동의 흐름, 실제 지급 정책은 이번 테스트에 포함하지 않았다.

## Android

연결 기기 조회 결과 0대이므로 실기기 광고 노출·네트워크 장애·앱 전환 복귀는 미검증이다. APK 빌드 결과는 아래에 기록한다.

- IL2CPP / ARM64, 0.16.3 b64, `.adtest` 패키지 빌드 성공. 오류 0, 경고 89(기존 API 폐기 경고와 빌드 도구 경고 등).
- 실제 APK 파일은 96,197,494 bytes이다. `android-build.txt`의 Bytes는 Unity BuildReport 전체 크기이며 APK 파일 크기와 다르다.
- 결과: `Recordings/AdMobTest/Android/AdMob 광고 테스트_0.16.3_b64.apk`.
- 첫 빌드는 약 20분 41초였으며 `packagingOptions` → `packaging` 자동 수정 확인 창 대기와 최초 IL2CPP 컴파일을 포함한다. AdMob 전처리기의 추가 Resolve가 구문을 재생성한 것이며 Unity의 Yes로 진행해 성공했다. 재빌드 때 같은 창이 뜨면 Yes로 진행한다.
- 빌드 후 게임의 패키지·표시 이름·ARMv7/ARM64·서명·EDM 자동 Resolve 설정은 복구됐다. 빌드가 생성한 URP 파생값, 성능 테스트 파일, SDK AndroidManifest, 테스트 패키지 의존성 기록도 원복했다. 버전 코드 64와 기존 Google EDM 선택을 반영한 UNITY_EDM_DISABLED만 유지한다.
- 테스트 씬이 직렬화된 에셋으로 완성된 뒤 일회성 UI 생성 코드를 제거했다. Editor 코드는 씬 열기·APK 빌드 메뉴와 저장하지 않은 씬 보호만 남긴다.
