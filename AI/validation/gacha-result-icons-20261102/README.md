# 뽑기 결과 재화 아이콘 수정 — 0.20.1

결과창에 전달되는 서버 재화 보상에는 아이콘이 없어 전직 파편·비전 지식이 대체 글자로 표시됐다. `GachaResultPopupController.AddResultCard`에서 아이콘이 없는 재화만 기존 `RewardPresentation.Icon`으로 보충한다. 미리보기와 같은 카탈로그의 이미지를 사용하고, 이미 지정된 장비·자동 분해 보상 아이콘은 유지한다.

Unity 6000.3.21f1에서 import·컴파일 오류 0건을 확인했다. `GachaFullScreenValidation.CheckResultCurrencyIcons()`는 실제 `GachaManager.BuildEntries`에 서버 DTO 형태의 표시용 데이터를 전달하고 기존 결과창 프리팹을 연다. 전직 파편 2+2개와 비전 지식 10+20+50개가 두 카드로 합산되며, 공통 아이콘 참조 일치·아이콘 활성·대체 글자 비활성·수량 x4/x80을 검사했다. `result-icons.json`에 결과를 기록했다.

`before-user-report.png`는 사용자가 제공한 수정 전 화면이고, `after-unity.png`는 타이틀 초기화 후 실제 결과 팝업에 표시용 데이터를 넣은 1080×1920 Unity 캡처다. 실제 뽑기 거래나 재화 지급·차감은 수행하지 않았다. 새 이미지·프리팹·레이아웃을 추가하지 않으며, 기존 카탈로그 아이콘을 팝업 생성 시에만 조회한다. 두 카드가 표시된 팝업의 활성 Graphic 33개·LayoutGroup 6개를 기록했고, 프레임 시간·GC 비교는 이번 작은 표시 수정에서 반복하지 않았다.

요청에 따라 Android 빌드·기기 검증은 하지 않았다. 버전은 수정 기준으로 0.20.1, Android versionCode는 67을 유지한다.
