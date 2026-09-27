# 사이트 호스팅과 공개 운영안

기준일 2026-09-27. Ludos Interactive 회사 사이트이며, 왕국군 키우기는 개발 작품 중 하나로 독립된 소개 페이지를 둔다. 구현·실행 방법은 [Website/README](../../Website/README.md)에 통합했다.

## 1. 선택: Cloudflare Pages Free + 제공 하위 주소

처음에는 **Cloudflare Pages Free의 `프로젝트명.pages.dev` 주소**로 시작한다. 별도 도메인·서버·DB를 구매하지 않고 정적 HTML·CSS·이미지를 제공한다. 사이트 제작 비용과 호스팅 비용을 분리하면 이번 구현에는 유료 서비스가 필요하지 않다. 수익이 발생했다고 곧바로 유료 플랜으로 전환해야 하는 구조도 아니다. 실제 사용량·기능·계약 조건이 달라질 때 다시 판단한다.

Pages 공식 문서는 정적 자산 요청을 무료·무제한으로 안내한다. 무료 빌드 월500회, 사이트20,000파일, 파일당25MiB 제한이 있고 이번 작은 정적 사이트는 그 안에서 운영하도록 구성했다. Functions나 유료 Workers는 사용하지 않는다. 요금제와 가용성이 영구히 보장된다는 의미는 아니다. [정적 요청 과금](https://developers.cloudflare.com/pages/functions/pricing/), [플랫폼 제한](https://developers.cloudflare.com/pages/platform/limits/)

| 후보 | 현재 상황에서의 판단 |
| --- | --- |
| **Cloudflare Pages Free** | 회사·작품·공개 문서용 정적 호스팅의 우선안. 제공 주소·HTTPS로 시작하고 커스텀 도메인을 나중에 연결 가능 |
| Firebase Hosting Spark | 적절한 대안이나 저장/전송 무료 할당량을 관리해야 한다. App Hosting과 다른 상품이며 할당량을 넘을 때 동작을 확인해야 함 |
| Vercel Hobby | 개인 비상업적 이용에 한정되므로 회사의 상업용 게임 사이트 출발점으로 선택하지 않음 |
| GitHub Pages | 온라인 사업·전자상거래 등의 무료 호스팅 용도 제한이 있어 이번 상업용 사이트의 우선안에서 제외. 모든 회사 소개 페이지가 금지된다고 단정하지 않음 |

비교 출처: [Firebase Hosting 할당량](https://firebase.google.com/docs/hosting/usage-quotas-pricing), [Vercel Hobby](https://vercel.com/docs/plans/hobby), [GitHub Pages 제한](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits). 요금·상품 조건은 실제 가입 시 다시 확인한다.

### 왜 처음에는 Direct Upload인가

현재 게임 저장소 전체를 외부 빌드 서비스에 연결할 필요가 없다. 회사가 소유한 Cloudflare 계정에서 **검증된 `Website/release/`만** 업로드하면 된다. 작은 사이트는 브라우저 업로드로 시작하고 필요할 때 Wrangler/CI 자동화로 확장할 수 있다. Direct Upload 프로젝트는 같은 프로젝트에서 Git integration으로 전환할 수 없고 그 방식은 새 프로젝트가 필요하다. 이 제한을 알고 선택한다. [공식 업로드 안내](https://developers.cloudflare.com/pages/get-started/direct-upload/)

### 도메인

`pages.dev`는 호스팅사의 무료 하위 주소이지 소유권을 갖는 독립 도메인이 아니다. 이미 지원 메일에 사용하는 `ludosinteractive.com`의 소유·DNS 권한이 확인되면 **`www.ludosinteractive.com`**을 연결하는 편이 회사 브랜드에 적합하다. 기존 도메인의 갱신비는 남지만 별도 도메인 구입은 필요하지 않을 수 있다. 먼저 무료 주소로 공개하고 나중에 이전해도 고정 경로는 유지한다.

서브도메인 연결은 CNAME 방식으로 할 수 있으며 기존 메일용 MX·SPF·DKIM·DMARC를 바꾸지 않는다. 루트 도메인 연결은 Cloudflare zone/네임서버 조건이 다르므로 DNS 담당자가 별도로 처리한다. 이번 작업에서는 DNS·서버·메일 설정을 변경하지 않는다. [공식 커스텀 도메인 안내](https://developers.cloudflare.com/pages/configuration/custom-domains/)

## 2. 콘솔에 제공할 주소

실제 호스팅 주소는 **아직 생성하지 않았다**. 아래는 확정된 경로이며, 배포 후 검증한 HTTPS 주소를 붙인다.

| 용도 | 경로 |
| --- | --- |
| 회사 홈페이지·개발자 웹사이트 | `/` |
| 왕국군 키우기 작품 페이지 | `/games/kingdom-idle/` |
| 개인정보처리방침 | `/privacy/` |
| 외부 계정 삭제 요청 안내 | `/delete-account/` |
| 게임 내 서비스 이용약관 | `/terms/` |
| 장비·마탑 확률 정보 | `/probabilities/` |
| 고객지원 | `/support/` |

개인정보 링크는 로그인·지역 제한 없이 열리는 HTML 전문이어야 한다. 홈페이지 첫 화면, PDF, 편집 링크, 비공개 문서, 이메일 주소로 대신하지 않는다. 계정 삭제 페이지는 게임·개발자·실제 삭제 요청 방법과 보존 예외를 안내하며, 안내 링크만 있다고 삭제 기능이 구현되는 것은 아니다. [사용자 데이터 정책](https://support.google.com/googleplay/android-developer/answer/10144311?hl=ko), [계정 삭제 웹 리소스](https://support.google.com/googleplay/android-developer/answer/13327111?hl=ko)

AdMob 등 광고사가 정해지면 스토어 개발자 웹사이트와 `app-ads.txt` 탐색 규칙을 확인한다. **실제 판매자 값이 없으므로 파일을 생성하지 않았다.** 호스트 루트와 www/하위 도메인 처리 규칙을 적용하고 광고사 검증까지 확인한다. [AdMob app-ads.txt](https://support.google.com/admob/answer/9363762?hl=en)

## 3. 공개 전에 필요한 순서

1. [자료 요청표](REQUIRED_INPUTS.md)의 사업자 서류와 서버 담당자 답변을 받는다.
2. [법무 검토](LEGAL_REVIEW_KO.md)의 미확정 사실·아동 절차·삭제 실행·동의 흐름을 해결하고 공개 문서에 반영한다.
3. 최종 AAB 기준 인증·수집·광고·구매·확률과 문서/콘솔 답변을 대조한다. 지금 UI-only 데모와 향후 수익화 버전을 혼동하지 않는다.
4. 사이트의 회사 정보·공개 주소·시행일과 검증 상태를 갱신하고 공개 빌드를 만든다.
5. Cloudflare 계정 로그인 후 배포할 실제 파일과 주소를 확인하여 게시한다.
6. HTTPS 직접 접근, 모바일, 메일 수신, 실제 삭제 요청 처리와 앱 링크를 확인한 뒤 사용자가 콘솔에 입력한다.

문서 검토 담당과 서버 실행 담당을 정하고, 개인정보·SDK·상품 변경 시 코드와 문서를 같은 변경으로 검토한다. 전담 법조인 고용을 전제로 하지 않되 이번 아동·국외이전·무작위 유료상품 범위는 출시 전 한 번의 한정된 외부 법률 검토를 권고한다. 공개 파일·시행일·변경 이유는 Git과 배포 이력으로 보존한다.
