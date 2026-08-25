# 신(God) 캐릭터 아트 생성 파이프라인

이 폴더는 **이미지 생성 전용 세션**의 작업 공간이다.
인게임 적용과 그 밖의 개발은 다른 세션에서 한다 — 여기서는 PNG 를 만들어 `output/` 에 놓는 데까지만 한다.

---

## 1. 지금까지 만든 것

| 캐릭터 | 상태 | 폴더 |
|---|---|---|
| Astra (심판의 여신) | 완료 — 도감 + 컷씬 | `astra/` |
| Ignis (화염의 여신) | 완료 — 도감 + 컷씬 | `ignis/` |
| 나머지 6신 | 미착수 | — |

남은 신: Ferrum, Gaien, Hora, Lumen, Nox, Silphir.
**이름만 남아 있고 디자인은 확정되지 않았다.** 예전 로스터 문서에 있던 설정
(예: "Ignis = 남성 마왕")은 **폐기됐다** — Ignis 는 사용자 지시로 여성형으로 다시 정의됐다.
새 신을 만들 때는 사용자가 그 자리에서 주는 디자인 지시를 유일한 기준으로 삼는다.

## 2. 실행 환경 (실측)

| 항목 | 값 |
|---|---|
| MCP 서버 | `https://cloud.comfy.org` (production, OAuth 인증됨) |
| 실행 형태 | **Comfy Cloud** — 로컬 ComfyUI 가 아니다 |
| 생성 노드 | `OpenAIGPTImageNodeV2` (`gpt-image-2`, quality `high`) |
| 픽셀 체인 노드 | `Change Channel Count`, `ImageScale`, `ImageQuantize` |
| Unity 화면 | **1080×1920 세로** (`Assets/UGUI/Scripts/Core/UguiTheme.cs`) |

노드 이름·입력 포트·위젯 순서는 **반드시 `get_node` 실측값**을 쓴다. 기억이나 추측 금지.

**Cloud 라서 불가능한 것**: `custom_nodes` 설치, 서버 재시작, `/object_info` 직접 조회,
input/output 폴더 접근. 커스텀 마감은 그래서 **로컬 파이썬**으로 돌린다.

## 3. 구조 — 캐릭터 1명당 워크플로 1개

```
[10] MASTER (t2i, 1024x1536)              ← 게임에 안 쓴다. 얼굴/체형/복장을 고정하는 내부 기준
      |
      +-- [20] CODEX (master 참조 편집, 1024x1536)
      |     +-- [21] Save  <God>/01_<God>_Codex_Raw
      |     +-- [23~28] ChangeChannelCount(RGB) -> ImageScale(area 256x384)
      |                 -> ImageQuantize(32,none) -> ImageScale(nearest 1024x1536) -> Save
      |
      +-- [30]/[40] ULTIMATE 2시드 (master 참조 편집, 1152x2048, 크로마 그린 배경)
            +-- Save  <God>/02_<God>_Ultimate_Raw_A / _B
            +-- (로컬) 3패스 그린키 -> 32색 스냅 -> 알파 이진화 -> 정수배 x4
```

도감과 컷씬은 **각각 따로 t2i 를 돌리지 않는다.** 반드시 같은 마스터 1장을 참조 편집한다.
이게 캐릭터 동일성을 유지하는 유일한 장치다.

### 컷씬 2시드를 뽑는 이유
프레이밍은 시드 운이다. Astra 때 한 시드는 뻗은 손이 우측 프레임에 33px 걸려 잘렸다.
두 장 뽑고 **가장자리 접촉 픽셀 수를 재서** 고른다 — 눈으로 고르지 말 것.

## 4. 해상도

| 결과물 | 논리 | 배율 | 최종 | 모드 |
|---|---|---|---|---|
| 도감 | 256×384 | ×4 | 1024×1536 | RGB |
| 컷씬 | 288×512 | ×4 | 1152×2048 | RGBA (투명) |

## 5. 픽셀 마감이 두 갈래인 이유

요구 사양은 **축소 → 고정 팔레트 → 경계 정리 → 최근접 정수배 확대**다.
코어 `ImageQuantize` 는 **적응형**이라 이미지마다 색이 달라져 고정 팔레트를 만족 못 한다.

1. **그래프 안 (클라우드)** — 도감용. 코어 노드만 써서 추가 설치 없이 돌아간다.
2. **로컬 팔레트 고정** — 최종 채택본. `astra/custom_nodes/ComfyUI-AstraTools/astra_nodes.py`
   의 `finalize()` 가 32색으로 정확히 스냅한다. 이 파일은 ComfyUI 노드이면서 동시에
   **독립 모듈**이라 로컬 마감과 (로컬 ComfyUI 사용 시) 그래프 내 마감이 같은 코드를 쓴다.

컷씬은 항상 로컬 마감이다 — `ImageQuantize` 가 RGB 만 받아 그래프 안에서 알파를 보존할 수 없다.

## 6. 하드-원 규칙 (전부 실제로 터져서 얻은 것)

**`ImageQuantize` 는 RGBA 입력에서 터진다.**
`gpt-image-2` 는 `background: opaque` 여도 4채널을 내보낸다.
→ 양자화 직전에 `Change Channel Count(kind=RGB)` 를 넣는다.

**`background: transparent` 는 거부된다.**
카탈로그엔 있지만 라이브 검증기가 `'transparent' is not a valid value` 로 반려한다.
→ 평면 **크로마 그린**으로 뽑고 로컬에서 키잉한다. 신들 팔레트에 녹색이 없어 충돌이 없다.

**그린키는 3패스여야 한다.**
① 테두리 flood fill ② 순수 그린만 노리는 엄격한 전역 테스트(팔·머리카락이 둘러싼 **닫힌 구멍**용)
③ 배경에 닿은 옅은 그린 1~2겹 벗기기(가는 머리카락의 형광 테두리 제거).
①만 하면 반드시 구멍이 남는다.

**OpenAI 안전 필터: 부정문이 역효과다.**
"revealing / high exposure / bare midriff / opaque coverage of intimate areas" 같은 표현은
**부정으로 쓰더라도** `safety_violations=[sexual]` 을 유발한다.
→ 의상은 **패션 용어로 긍정 서술**한다: "fitted sleeveless bodice", "low wrapped hip sash",
"skirt panels split at the sides", "dancer's regalia". 실루엣은 그대로 유지된다.

**유료 노드는 저비용으로 먼저 검증한다.**
작업이 실패하면 **부분 출력도 회수할 수 없고**(`get_output` 이 `job.failed` 를 준다)
이미 나간 API 호출은 과금된다. 새 프롬프트는 `input_overrides` 로 `model.quality: "low"` 를
걸어 4노드 전체를 한 번 돌려 안전·연결을 확인한 뒤 `high` 로 올린다.
노드 레벨 버그는 `EmptyImage` 로 유료 노드 없이 재현한다.

**크레딧이 떨어지면 노드 단위로 죽는다.**
`Payment Required: Please add credits to your account to use this node.` 가 뜨면
그 작업은 통째로 실패하고 **앞서 성공한 노드의 출력도 회수되지 않는다.**
잔액이 빠듯하면 워크플로의 유료 노드 수를 줄여서(예: 컷씬 시드 2개 → 1개) 돌린다.
잔액 충전은 사용자만 할 수 있다 — cloud.comfy.org → settings → workspace.

**GCS 서명 URL 은 파라미터 순서가 서명에 포함된다.**
재구성할 때 순서를 바꾸면 403. 원 순서
`X-Goog-Algorithm, Credential, Date, Expires, Signature, SignedHeaders, response-content-disposition`
를 그대로 지키고 `response-content-disposition` 도 빼지 않는다.
`/api/s/<id>?raw=1` 짧은 링크가 나오면 그걸 쓰는 게 제일 안전하다.

**긴 프롬프트는 인라인하지 말고 파일로 올린다.**
`save_workflow(workflow_path=..., client_os=...)` → PUT 업로드 → `run_saved_workflow(workflow_id=...)`.
`workflow_path` 는 **save(graph) 포맷만** 받는다. API 포맷은 인라인 `workflow_json` 전용.

## 7. 아트 디렉션 락

**렌더링 스타일 (`_STYLE`) — 전 캐릭터 공통, 반드시 세 프롬프트 모두에 넣는다.**
부드러운 셀 셰이딩, 균일한 조명, **밝은 명도**, 매트 피부, 섬세한 애니 얼굴, 또렷한 선.
금지: 강한 명암대비, 어둠 위 림라이트, 광택/기름진 피부, 사실적 근육 묘사, 거친 텍스처.
→ 이걸 빼면 캐릭터마다 화풍이 갈린다. Ignis 1차가 어두운 유화풍으로 나와 재작업했다.

**배경은 인물보다 확실히 어둡고 단순하게.** 실루엣에 배경이 번지면 안 된다.

**무기는 구조 정확도를 따로 명시한다.**
자루는 3차원에서 하나의 곧은 직선, 두께 균일, 휘거나 뒤틀리지 않게.
원근은 가까운 쪽이 크고 먼 쪽이 작게, 그래도 직선 유지.
자루가 몸 뒤로 지나가면 "어깨 뒤로 물러난다"고 깊이를 명시한다.
(검날이 미묘하게 휘어 나온 전례가 있다.)

**컷씬 구도는 로스터 공통이다** — 3/4 백뷰 반신, 머리는 옆얼굴, 먼 쪽 팔을 뻗음,
머리·의상이 한 방향으로 흩날림, 캐릭터 단독, 투명 배경, prop·이펙트·배경 전부 없음.
캐릭터마다 다른 건 무기와 팔레트뿐이다.

**팔레트는 캐릭터마다 32색을 새로 짠다.** 다른 신의 팔레트를 재사용하면 색이 죽는다.
구성 예(Ignis): ember 4 + 불꽃 램프 10 + 머리 6 + 피부 6 + 금속 5 + 백열 1.

## 8. 폴더/파일 표준 — 전 캐릭터 공통

`python standardize.py` 로 검사, `--apply` 로 정리한다. 새 신도 같은 규칙을 따른다.

```
AI/comfyui/
  README.md                  이 문서. 파이프라인 단일 출처.
  standardize.py             폴더 표준 검사/정리

  _shared/                   공용 코드. 신을 추가해도 여기만 재사용한다.
    pixel_finalize.py        축소 -> 팔레트 스냅 -> despeckle -> unify -> 알파 이진화 -> 정수배
    chroma_key.py            3패스 크로마 그린 키잉
    workflow_json.py         API 포맷 -> ComfyUI save(graph) 포맷 변환
    god_finalize.py          마감 CLI 본체 (standing / cutscene)
    comfyui_node/            로컬 ComfyUI 노드 래퍼. Cloud 에서는 못 쓴다.

  <god>/                     폴더명은 소문자 (astra, ignis)
    <god>_spec.py            팔레트 + SKIN/GLOSS + 프롬프트. 캐릭터 전용 값의 단일 출처.
    build_*.py               워크플로 빌더
    finalize.py              _shared/god_finalize.main 을 부르는 열 줄짜리 껍데기
    workflows/               생성된 워크플로 JSON (중간 산출물)
    output/                  납품물만. 정확히 6개.
      <God>_00_Master.png            내부 기준. 게임에 쓰지 않는다.
      <God>_01_Standing_HD.png       스탠딩 HD            RGB   1024x1536
      <God>_01_Standing_Pixel.png    스탠딩 도트          RGB   1024x1536
      <God>_02_Cutscene_HD.png       컷씬 HD (크로마 그린)  RGB   1152x2048
      <God>_02_Cutscene_Cutout.png   컷씬 풀해상도 투명     RGBA  1152x2048
      <God>_02_Cutscene_Pixel.png    컷씬 도트            RGBA  1152x2048
    output/폐기된_아트/<사유>/        폐기본. RAW 는 절대 지우지 않는다.
```

**이름 규칙**
- 확장자는 항상 `.png`. 도트본은 논리해상도 x4 정수배.
- 신 이름은 첫 글자만 대문자. 번호 `00`=마스터 / `01`=스탠딩 / `02`=컷씬 고정.
- 접미사는 `HD` / `Pixel` / `Cutout` 셋만. `Raw` / `Final` / `_A` `_B` 같은 옛 표기는 쓰지 않는다.
- 합성 리뷰 시트는 **프로젝트에 저장하지 않는다.** 세션 스크래치패드에서만 만든다.

**공용/전용 경계 — 신을 추가할 때 지킬 것**
- 캐릭터마다 다른 것은 **팔레트 32색, SKIN/GLOSS 색 목록, 프롬프트** 셋뿐이다. 전부 `<god>_spec.py` 에 둔다.
- 마감 로직·키잉·워크플로 변환은 `_shared` 에만 있다. 신 폴더에 복사하지 않는다.
- 다른 신의 폴더에서 임포트하지 않는다. (이그니스가 아스트라에서 임포트하던 구조를 2026-08-25 에 정리했다.)

## 9. 도트 마감 (_shared/god_finalize.py)

축소(BOX) → 고정 팔레트 스냅 → **고립 픽셀 제거** → **톤 통일** → 알파 이진화 → 정수배 NEAREST.

두 정리 단계는 피부에만 건다. HD 원본의 광택은 그대로 두고 도트에서만 처리하는 것이 목적이다.
- `despeckle` (대상 `<GOD>_GLOSS`) : 같은 색 이웃이 1개 이하인 고립 픽셀을 주변 다수 색으로 흡수.
  광택이 만든 1~2px 반점 제거. **밝은 쪽 색만** 넣는다 — 어두운 음영까지 넣으면 음영 경계가 뭉갠다.
- `unify` (대상 `<GOD>_SKIN`) : 피부색 무리 안에서만 도는 3x3 최빈값 필터(3회).
  3~5px 덩어리로 섞인 톤을 하나로 모은다.

대상 색은 **피부 램프 + 백열색으로 한정**한다. 전 색에 걸면 검신의 룬 글리프 같은 1px 디테일이 날아간다.
마스크가 있으면 **마스크 밖 이웃은 투표에서 뺀다** — 넣으면 실루엣 가장자리 피부가 배경색을 집어와 검은 점이 박힌다.

  python finalize.py standing  <God>_01_Standing_HD.png  <God>_01_Standing_Pixel.png
  python finalize.py cutscene  <God>_02_Cutscene_HD.png  <God>_02_Cutscene_Pixel.png

## 10. 납품 전 검수 (건너뛰지 않는다)

`finalize.py` 자동 검증: 크기 = 논리 x 배율 / 팔레트 외 색 0 / 컷씬 알파 0·255 만 / 정수배 블록 균일.

**육안 확대 검수 — 아래는 매번 확대해서 확인한다.**
- **손과 손가락 개수.** 검을 쥔 손 외에 손이 하나 더 생긴 전례가 있다 (크로스가드 아래). 축소본으로는 안 보인다.
- 얼굴: 좌우 눈 일치, 피부에 균열·이음매 없음
- 무기: 직진성(가드~검끝 직선 오버레이), 가드 대칭, 접합부 왜곡 없음
- 컷씬: 네 변 접촉 픽셀 수 (컷아웃이므로 접촉 = 잘림)
- 배경 잔여물 없음, 의상과 머리카락이 서로 녹지 않았는지

## 11. 새 신 만드는 절차

1. `<god>/` 폴더를 만들고 `ignis/` 의 `*_spec.py` / `build_workflow.py` / `finalize.py` 를 복사
2. `<god>_spec.py` 에 팔레트 32색과 `_STYLE` / `_LOCK` / `_WEAPON` / MASTER / CODEX / ULTIMATE 작성
   — `_STYLE` 은 그대로 복사, `_LOCK` 과 팔레트만 새로 씀
3. `python build_workflow.py` → API/UI JSON + 프롬프트 문서 생성
4. `save_workflow(workflow_path=...UI.json)` → PUT 업로드 → `run_saved_workflow`
   (첫 실행은 `input_overrides` 로 quality `low` — 안전 필터 확인)
5. 통과하면 override 없이 `high` 로 재실행
6. 다운로드 → 컷씬 2시드의 **가장자리 접촉 픽셀 수 비교** → 채택
7. `python finalize.py codex ...` / `python finalize.py cutscene ...`
8. 검증 출력에서 크기·팔레트 외 색 0·알파 이진·정수배 블록 균일 확인
9. 미채택본 `archive/` 로 이동, 리뷰 시트 만들어 사용자에게 보여줌

## 12. 참고

`finalize.py` 가 매번 찍는다. 하나라도 FAIL 이면 넘어가지 않는다.

- 크기 = 논리 × 배율
- **팔레트 외 색 0개**
- 컷씬: 알파가 0/255 만 (반투명 테두리 없음)
- 정수배 블록이 완전 균일

육안 체크: 성인 얼굴 / 눈 색 보임 / 머리 색 순서 / 복장 3컷 동일 / 금색 없음 /
손·팔 정상 / 무기 직선 / 이펙트가 얼굴 안 가림 / 배경 잔여물 없음.
