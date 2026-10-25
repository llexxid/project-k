# 공개 문서 사이트 구성·비용·운영안

2026-09-25. 루도스(Ludos) / 왕국군 키우기 / 대한민국 우선 출시.

## 1. 추천 구성

**무료 정적 호스팅 + 기존 도메인의 help.ludosinteractive.com 같은 하위 도메인**을 추천한다. 회사 홍보용 대형 홈페이지, 회원 가입, 데이터베이스, 별도 게임 서버 설치는 필요하지 않다. 공개 문서와 문의 방법을 읽는 작은 안내 사이트면 충분하다.

현재 이메일 주소만 확인했으며 도메인 소유권·DNS 관리 권한·갱신 비용은 확인하지 않았다. 기존 도메인을 사용할 수 있다면 새 도메인 구매비가 추가되지 않지만 기존 도메인 연간 갱신비는 계속 발생한다. 전혀 도메인을 구매하지 않으려면 호스팅사가 제공하는 `프로젝트명.pages.dev` 또는 `프로젝트명.web.app` 주소로 시작할 수 있다. 이것은 소유하는 무료 독립 도메인이 아니라 제공자의 하위 주소다.

| 선택 | 비용·범위: 조사일 기준 | 판단 |
|---|---|---|
| **Cloudflare Pages Free + 기존 하위 도메인** | 정적 사이트 무료 요금제. 월 500회 빌드, 사이트당 20,000개 파일, 파일당 25 MiB 제한. 정적 요청과 대역폭은 무료 안내 범위 | 이번 문서 사이트의 우선안. 공개 페이지·HTTPS·루트 파일을 함께 관리하기 좋음 |
| **Firebase Hosting Spark + web.app** | Hosting 저장 10 GB, 전송 360 MB/일의 무료 범위. 자체 도메인·SSL 지원 | Google 계정 중심 대안. 무료 한도를 넘으면 서비스 제한이 생길 수 있어 사용량 확인 필요. App Hosting 상품과 구별 |
| GitHub Pages | 무료 제공 범위가 있으나 온라인 사업·전자상거래 등을 무료 호스팅하는 용도 제한을 명시 | 회사 안내 페이지 전부가 금지된다고 단정하지 않지만 상업적 용도 경계 검토가 필요하므로 이번 첫 선택에서는 제외 |

출처: [Cloudflare 제한](https://developers.cloudflare.com/pages/platform/limits/), [정적 요청 과금 설명](https://developers.cloudflare.com/pages/functions/pricing/), [Firebase 가격](https://firebase.google.com/pricing), [Firebase 할당량](https://firebase.google.com/docs/hosting/usage-quotas-pricing), [GitHub Pages 제한](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits).

무료 요금제가 영구히 유지되거나 무제한 가용성을 보장하는 것은 아니다. 유료 Functions·Worker·데이터베이스·유료 요금제 전환 없이 정적 파일만 제공하는 범위로 시작한다. 운영자가 파일을 내려받아 다른 호스팅으로 옮길 수 있게 원본을 보관한다.

## 2. 콘솔에 넣는 링크

개인정보처리방침 URL은 로그인 없이 읽을 수 있는 공개 HTML 본문 주소다. 홈페이지 첫 화면·편집 링크·비공개 문서·이메일 주소·PDF로 대신하지 않는다. Google은 공개 접근 가능하고 지역 제한이 없으며 이용자가 편집할 수 없는 개인정보 페이지를 요구한다. 계정 삭제 URL도 게임과 개발자를 식별하고 삭제 요청 경로를 안내하는 전용 페이지가 적합하다. [Google 사용자 데이터 정책](https://support.google.com/googleplay/android-developer/answer/10144311?hl=ko), [계정 삭제 웹 리소스](https://support.google.com/googleplay/android-developer/answer/13327111?hl=ko)

아래 주소는 **설계 예시이며 아직 존재하거나 작동하는 주소가 아니다.** 실제 게시·확인 후 입력한다. 로컬 시안의 `#privacy` 같은 이동 주소는 운영용 URL의 대체물이 아니다.

| 용도 | 제안 URL | 본문 원본 |
|---|---|---|
| 스토어 개발자 웹사이트·지원 | https://help.ludosinteractive.com/ | 게임 안내와 문의 |
| 개인정보처리방침 | https://help.ludosinteractive.com/privacy/ | [개인정보처리방침](PRIVACY_POLICY_KO.md) |
| 외부 계정 삭제 요청 | https://help.ludosinteractive.com/delete-account/ | [삭제 안내](ACCOUNT_DELETION_KO.md) |
| 게임 내 이용약관 | https://help.ludosinteractive.com/terms/ | [이용약관](TERMS_OF_SERVICE_KO.md) |
| 게임 내 확률 정보·홈페이지 공개 | https://help.ludosinteractive.com/probabilities/ | [확률 작성표](PROBABILITY_DISCLOSURE.md) |
| 문의·사업자 정보 | https://help.ludosinteractive.com/support/ | 확정 사업자 표시사항·지원 방법 |
| 광고사가 요구하는 경우 | https://help.ludosinteractive.com/app-ads.txt | 해당 광고 계정의 실제 판매자 값 |

약관과 확률표는 첨부 이미지의 독립된 체크 항목은 아니지만 실제 게임 출시 문서로 함께 준비한다. 개인정보 URL 하나에 약관·삭제·확률표를 모두 붙여 구분 없이 안내하지 않는다.

광고사는 미정이다. **AdMob을 선택한다면** 스토어 개발자 웹사이트 호스트를 기준으로 app-ads.txt를 찾는 규칙을 적용한다. 첫 단계 하위 도메인 help.example.com은 지원하지만 www·m은 별도 취급하고 더 깊은 하위 도메인은 일부를 제외하므로 주소를 임의로 바꾸지 않는다. 앱별 경로 아래가 아니라 확인된 호스트 루트에 실제 파일을 제공한다. 가짜 판매자 ID나 빈 파일로 준비 완료 처리하지 않는다. [AdMob 호스트 탐색 규칙](https://support.google.com/admob/answer/9363762?hl=en), [앱 확인 안내](https://support.google.com/admob/answer/14538460?hl=en)

## 3. 사이트 초안

로컬 시안: [안내센터 시안](site-preview/dist/index.html).

- 차분한 목재·청동 계열 머리글과 밝은 문서 영역. 작은 모바일에서도 본문이 먼저 읽히도록 구성.
- 시작 화면에서 개인정보·약관·계정 삭제·확률 정보·문의에 바로 접근.
- 검토 중인 시안임을 모든 화면에 표시. 아직 정해지지 않은 상품 확률·삭제 기간·사업자 정보는 실제 운영 정보처럼 표시하지 않음.
- 정책 페이지에는 적용일·현재 문서·이전 버전 영역을 두는 구조. 실제 적용일은 게시 확정 때 기입.
- 외부 글꼴·분석·광고·쿠키 배너·방문자 입력 폼 없이 시작. 쿠키 배너가 필요 없다는 사실과 개인정보 처리가 전혀 없다는 주장은 구별. 호스팅 접속 로그·지원 이메일 수신도 검토 대상.
- 삭제는 확정된 지원 절차를 안내하는 페이지로 시작할 수 있음. 이메일 방식도 실제 계정 확인·삭제 수행이 가능하고 Google 요건을 충족해야 함. 시안에는 전송 폼이나 삭제 완료 메시지를 만들지 않음.

시안의 개인정보·약관은 읽는 구조를 보여 주는 요약이다. 법률 문서 전문은 위 Markdown 파일에서 검토하며, 공개 버전에는 확정된 전문을 각 고정 경로에 제공한다. 시안의 noindex는 검색 제외 요청일 뿐 접근 보안이 아니므로 미확정 문서를 외부에 배포하지 않는다.

## 4. 연락처·사업자 표시

지원 이메일: kingdomidle@ludosinteractive.com. 개인정보 문의도 같은 창구로 받는 안이며 실제 책임자·담당자 배정이 필요하다. 문의 시간을 임의로 정하거나 24시간 대응을 약속하지 않는다.

개인 휴대전화는 초안·저장소에 기재하지 않았다. 앱별 지원 전화번호가 선택 입력이어도 한국 개발자의 별도 공개 연락처 의무까지 없어지는 것은 아니다. 실제 연락·인증이 가능한 업무용 번호를 마련하는 편을 권고한다. 법적 사업자 표시사항은 등록·판매 구조를 확인한 뒤 반영한다. [한국 개발자 연락처](https://support.google.com/googleplay/android-developer/answer/3255733?hl=ko)

사용자 제공 사업자명은 루도스(Ludos), 기존 콘솔 조회 표시명은 Ludos Interactive 및 Personal account다. 법적 사업자·계정 유형·결제 프로필의 정합성은 계정 소유자가 확인한다. 개인정보 방침에 대표자의 개인 번호를 무조건 복사해서 공개하지 않는다.

## 5. 게시 담당자에게 넘길 순서

1. 정책 미확정 값·사업자 표시·삭제 운영·상품 및 확률 정보를 확정하고 [법무 검토](LEGAL_REVIEW_KO.md)의 공개 기준을 확인한다.
2. 회사가 관리하는 호스팅 계정과 복구 수단을 준비한다. 기존 도메인 사용 가능 여부와 갱신 담당자를 확인한다.
3. Pages에서 해당 하위 도메인을 먼저 연결하고 DNS 담당자가 필요한 CNAME을 설정한다. 하위 도메인은 DNS 전체 네임서버를 옮기지 않는 방식도 가능하다. 메일의 MX·SPF·DKIM·DMARC와 다른 서비스 레코드를 변경하지 않는다. [공식 연결 절차](https://developers.cloudflare.com/pages/configuration/custom-domains/)
4. 확정 HTML·이미지·필요한 광고 파일만 배포한다. 저장소 전체, 인증정보, 서버 설정, 내부 검토 문서는 공개 출력에 포함하지 않는다.
5. HTTPS, 각 페이지 직접 접근, 휴대폰 가독성, 이메일 수신, 삭제 요청 실제 처리, 과거 버전 링크를 확인한다. 로그인·지역 제한·접근 차단을 두지 않는다.
6. 앱과 콘솔에서 같은 URL을 사용한다. 확률표는 앱의 구매 전 화면에서도 충분히 확인할 수 있게 연결한다.
7. 이후 정책 변경은 동일 URL의 현재 문서를 교체하고 이전 공개본·시행일·변경 근거를 보존한다.

이번 작업은 로컬 파일과 시안 작성까지다. 외부 게시, 호스팅 가입·유료 전환, DNS·콘솔·게임 서버 변경은 수행하지 않았다.
