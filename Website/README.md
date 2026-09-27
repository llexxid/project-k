# Ludos Interactive 웹사이트

회사를 소개하는 메인·스튜디오, 독립된 왕국군 키우기 작품 페이지, 고객지원 및 정책 전문으로 구성한 정적 사이트다. 게임 서버와 별도이며 외부 서비스에 게시하지 않았다.

## 로컬 실행

프로젝트 루트의 PowerShell에서 `./Website/Start-Preview.ps1`을 실행한 뒤 `http://127.0.0.1:8765/`를 연다. 종료는 Ctrl+C. 스크립트 실행이 정책상 제한되면 Node.js로 `node Website/scripts/dev.mjs`를 직접 실행한다. 시스템 보안 설정을 바꿀 필요는 없다.

Node.js 22 이상을 사용한다. npm 설치·DB·API 키가 필요하지 않다. 기존 Codex 번들 Node도 실행 스크립트가 찾는다. `file://`로 열면 루트 기준 링크가 동작하지 않으므로 반드시 로컬 HTTP로 확인한다. 개발 서버는 이 컴퓨터의 127.0.0.1에만 연결한다.

소스 수정 후 자동으로 HTML을 다시 생성한다. 브라우저에서는 새로고침한다. 개발 서버 자체(`scripts/dev.mjs`)를 바꾼 경우 서버를 다시 시작한다. 빌드는 별도 짧은 프로세스에서 실행해 Windows에서 소스 파일을 계속 점유하지 않도록 했다.

## 수정 위치

| 대상 | 파일 |
| --- | --- |
| 회사 정보·공개 기준일·배포 주소·확인 상태 | `content/site.json` |
| 페이지 구성·본문·문서 변환 | `scripts/build.mjs` |
| 디자인·반응형·인쇄 | `src/styles.css` |
| 모바일 메뉴·스크린샷 확대 | `src/site.js` |
| 개인정보·약관·삭제·확률의 단일 원문 | `../Docs/Publishing/*.md`의 PUBLIC-CONTENT 블록 |
| 웹 이미지 원본 선정·변환 내역 | `ARTWORK.md`, `content/asset-manifest.json` |
| Cloudflare 보안 응답 헤더 | `public/_headers` |

사이트는 원문을 빌드할 때 HTML로 변환한다. `dist/`와 `release/`는 생성물 전용이며 빌드 시 다시 만들어진다. 여기에 직접 수정하거나 개인정보·서류를 저장하지 않는다. 외부 폰트·분석·광고 SDK, 회원가입, 입력폼, 브라우저 저장소 사용은 없다. 호스팅 제공자의 접속 처리와 지원 메일 운영에 관한 검토까지 면제되는 것은 아니다.

## 빌드와 검증

- `node Website/scripts/build.mjs`: 로컬 검토본을 `dist/`에 생성. 정책에 검토 상태를 표시하고 검색 제외를 요청한다.
- `node Website/scripts/check.mjs`: 페이지·링크·이미지·내부 문서 유출 여부·확률 합계·배포 차단을 검사한다.
- `node Website/scripts/build.mjs --production`: 실제 공개 자료가 확인된 경우만 `release/`를 생성한다. 미확정이면 오류로 종료한다.

검토본을 공개 호스팅에 올리지 않는다. `noindex`는 접근 통제가 아니다. 공개 빌드의 확인 플래그는 체크리스트를 우회하기 위한 스위치가 아니라 담당자가 실제 검증 후 기록하는 값이다. 개인정보 문서의 미확정 표를 실제 내용으로 해결하고, 최종 앱·삭제 실행·아동 절차까지 확인해야 한다. 자동 검사는 법률 적합성이나 심사 통과를 판정하지 않는다.

## 배포 인계

첫 호스팅은 **Cloudflare Pages Free / Direct Upload**를 선택했다. 공개할 폴더는 확인을 통과한 **`Website/release/` 하나**다. 로컬 검토본 `dist/`, 게임 저장소, 내부 Markdown, 서류, 기기 캡처 원본을 업로드하지 않는다. 아직 외부 프로젝트·URL·DNS를 생성하지 않았다.

가입·로그인은 사용자가 직접 진행한다. Direct Upload는 게임 저장소 접근권한 없이 완성 파일만 올릴 수 있다. 나중에 Wrangler 기반 자동 배포는 가능하지만 같은 프로젝트를 Cloudflare Git integration 방식으로 바꾸려면 새 프로젝트가 필요하다. [공식 Direct Upload 안내](https://developers.cloudflare.com/pages/get-started/direct-upload/)

공개 준비 후 계정 소유자와 배포 대상을 확인하고 업로드한다. HTTPS에서 전체 페이지·보안 헤더·404·모바일·메일 수신·삭제 실행을 다시 확인한 뒤 실제 URL을 앱과 콘솔에 반영한다. 기존 공개본은 교체 전에 Git 태그와 배포 버전으로 보존하고, 정책 시행일별 이전 HTML도 다음 개정 시 보관한다. 아직 시행된 정책이 없으므로 가짜 이전 버전을 만들지 않았다.

호스팅 선택 근거와 콘솔 링크 표: [운영안](../Docs/Publishing/WEBSITE_PLAN.md). 필요한 서류·정보: [제공 자료](../Docs/Publishing/REQUIRED_INPUTS.md). 법률·정책 검토: [검토 문서](../Docs/Publishing/LEGAL_REVIEW_KO.md). 프로젝트 행동 규칙: [AGENTS.md](../AGENTS.md).
