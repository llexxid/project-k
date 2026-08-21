# Style Spec — Project-K 캐릭터 / 카드 일러스트 (픽셀 아트)

이 문서가 **프로젝트 아트 스타일의 단일 기준**이다. ComfyUI 로 만드는 모든 캐릭터·카드·컷인 일러스트는
여기 적힌 레퍼런스 세트와 규칙을 따른다. (VFX 는 별도 — 아래 §6, **UI 도트 자산은 §8**)

> ⚠ **적용 범위 주의 (2026-08-17 추가).** 아래 §1~§5 의 고색상(5,800~17,700색) 규격은
> **카드 키아트/컷인 일러스트 전용**이다. 인게임 왕국군 스프라이트와 나란히 놓이는 UI 자산
> (파티 HUD 초상화, 스킬 아이콘)에 이 규격을 쓰면 혼자 HD 일러스트처럼 튄다 — **§8** 을 따를 것.

작성 근거: `Assets/ExternalAssets/CombatRPG` 전 세트의 실제 PNG 를 픽셀 단위로 측정한 결과
(윤곽선 커버리지, 알파 분포, 색 수, 디더링 비율, 비율/구도)를 기반으로 한다. 마케팅 문구가 아니라 측정값이다.

---

## 1. 락(lock)된 레퍼런스 세트

> **`Assets/ExternalAssets/CombatRPG/2.08 - 550+ Animated + New Chatarcters/Epic Characters #31 - Animated/100 - Characters - 256x256 - Static`**

CombatRPG 는 **하나의 스타일이 아니다.** 측정 결과 세트마다 윤곽선 커버리지가 97%(2.07) ~ 1.5%(New styles),
소프트 알파가 5.3% ~ 0% 로 서로 다르다. 그래서 위 한 세트를 기준으로 고정한다.
**다른 세트를 레퍼런스로 섞지 말 것** — 카드 8종이 한 세트로 안 보이게 된다.

대표 레퍼런스 파일 (스타일 참조 이미지로 전달): `13.png`, `1.png`, `40.png`
(각각 오너먼트 스태프 마법사 / 마법진 마녀 / 금 갑옷 기사 — 천·금속·장식 세 가지 재질을 모두 커버)

> CombatRPG 원본 자체는 완성도가 낮고 임포트 설정도 깨져 있다(§5). **본 게임 아트는 생성해서 쓴다.**
> 레퍼런스는 "스타일의 법"으로만 쓰고, 캐릭터/의상/포즈는 절대 복사하지 않는다.

---

## 2. 측정된 스타일 법칙

| 항목 | 값 |
|---|---|
| 캔버스 | 정사각/세로 투명 캔버스. 전투 스프라이트 64/128/256px, **카드 키아트는 320px 폭 그리드** |
| 인물 비율 | 성인 영웅형 애니 비율 **6~7등신** (치비 아님). 머리 = 전신의 14~18% |
| 팔레트 | 팔레트 제한 없음. 256px 기준 고유색 5,800~17,700. 채도 높은 보석색 + 무채색 강철 + 근검정 |
| 금속 | 금/황동이 지배색. 상면에 1~2px 근백색 스페큘러, 바로 아래 하드한 어두운 단절 |
| 명암 | 재질당 5~10단계 **부드러운 램프**. 2~3톤 셀셰이딩 아님, 플랫 아님 |
| 디더링 | **없음** (2x2 체커 비율 0.01~0.36% = 통계적으로 0). 프롬프트에서 명시적으로 금지 |
| 윤곽선 | 2.08 기준 실루엣에 **1px 어두운 윤곽선**. 윤곽선은 인접 채움색보다 절대 밝지 않음 |
| 알파 | 사실상 **바이너리** (불투명 픽셀의 94.7~98.5%가 alpha≥250). 머리카락 끝·마법광에만 부분 알파 |
| 배경 | 완전 투명. 접지 그림자·비네트·프레임·텍스트·로고 없음 |
| 라이팅 | 좌상단 키라이트 고정. 유일한 경쟁 광원은 캐릭터가 든 발광 소품(지팡이 머리·오브·룬) |
| 구도 | 정면 또는 약한 3/4, 한 발 체중, 무기/지팡이는 몸 옆에 수직, 망토·드레스가 하단 좌우로 퍼짐 |

---

## 3. 생성 파이프라인 (검증된 절차)

1. **레퍼런스 스트립을 만든다** — 위 3개 파일을 NEAREST 2배 확대해 가로로 이어붙이고 어두운 무채색 배경(#1a1a22)에 얹는다.
2. **업로드** → `upload_file` → 반환된 `name` 을 `LoadImage` 에 꽂는다.
3. **Nano Banana 2 로 생성** — 노드 `GeminiNanoBanana2V2`, `model = "Nano Banana 2 (Gemini 3.1 Flash Image)"`,
   `model.images.image_1 = [LoadImage, 0]`, `model.thinking_level = "HIGH"`, `model.resolution = "1K"`,
   카드 키아트는 `model.aspect_ratio = "3:4"`.
   프롬프트는 **"레퍼런스는 스타일 전용, 캐릭터/의상/포즈는 복사 금지"** 를 첫 문단에 명시한다(§4 템플릿).
4. **후처리(결정적, PIL)** — 확산 모델은 진짜 픽셀 그리드를 만들지 못한다. 반드시 아래를 거친다:
   - BOX 다운스케일 → 목표 픽셀 캔버스 (카드 키아트 320px 폭)
   - **가장자리 flood-fill 로 배경 제거** (색 키잉 금지 — 내부의 근검정 코어 섀도가 뚫린다). 허용 오차 ~42
   - 알파 128 기준 이진화
   - 채널당 4단계 스냅 (생성 노이즈 제거, 색 수를 레퍼런스 대역인 5k~17k 로 정렬)
   - 알파 bbox 크롭 + 균등 여백 8px
5. **아이콘은 별도 생성하지 말고 일러스트에서 딴다** — 상징물(지팡이 문양 등)의 최대 연결 성분을 추출해
   정사각 캔버스에 중앙 정렬 → 128x128. 스타일이 100% 일치하고 32px 까지 읽힌다.
6. **메타데이터를 남긴다** — 결과 옆에 `<Card>.gen.json` (모델/시드/프롬프트/해상도/후처리 단계/prompt_id).
   재현 가능해야 한다.

---

## 4. 프롬프트 템플릿

**Positive**
```
Reference image = STYLE ONLY. Copy its exact art style: pixel-art rendering at the same pixel scale,
smooth multi-tone pixel shading, hue-shifted shadows, near-black core shadows, saturated gold filigree
over black, crisp 1px dark outline on the silhouette, no dithering.
DO NOT copy the reference characters, their costumes or poses.

Generate ONE new character: <SUBJECT>. Single full-body figure, standing front-facing, adult heroic anime
proportions about 6 to 7 heads tall, <HAIR>, <EXPRESSION>, <COSTUME>, <PROP held vertically beside the body>,
top-left key light plus a warm glow cast by <SELF-LIT PROP>.

Composition: exactly one character, centered, complete figure with clear empty margin above the head and
below the feet, plain flat very dark background (solid #16161e), no ground shadow, no frame, no border,
no text, no watermark, no signature, no sprite sheet, no extra characters, not chibi.
```

NB2 는 네거티브 프롬프트가 없으므로 금지 항목을 위 본문에 넣는다.

---

## 5. 임포트 규칙 (어기면 픽셀이 뭉갠다)

프로젝트 관례(`Assets/_Project/Art` 기준): `textureType = Sprite`, `filterMode = Point`,
`mipmaps off`, `nPOTScale = None`, `alphaIsTransparency = true`, `spritePixelsToUnits = 32`.

`Assets/Generated/ComfyUI/**` 는 `Assets/DivineSkill/Editor/GeneratedArtPostprocessor.cs` 가 자동으로 강제한다.

> ⚠ **CombatRPG 원본 1,600여 장은 전부 유니티 기본값**(Default 타입 / Bilinear / mipmap ON / nPOTScale=ToNearest)
> 으로 들어와 있다. 그대로 쓰면 흐려지고 230x306 → 256x256 으로 리샘플되어 픽셀 그리드가 파괴된다.
> CombatRPG 에셋을 실제로 쓸 일이 생기면 **먼저 재임포트**할 것.

---

## 6. VFX 는 다른 규격이다

캐릭터 스타일과 혼동하지 말 것. 게임 내 VFX(마탑 스킬, 히트 이펙트)의 측정된 법칙:
**플랫한 4~6색, 어두운 윤곽선 없음, 48~64px 프레임.** 캐릭터용 "1px 검정 윤곽선"을 VFX 에 적용하면 오프스타일이다.

애니메이션은 프레임별 확산 생성을 하지 않는다(프레임 일관성이 깨진다).
정적 형태(마법진·룬·링)는 **완성본 1장을 스타일링한 뒤 마스킹(각도 스윕/밝기 펄스)으로 프레임을 만든다.**

타격감(impact timing) 곡선: 발동 빠르게(50~60ms/frame) → 유지 중간(~100ms) → 소산 가장 느리게(115→220ms, 감속).

---

## 8. UI 도트 자산 (파티 HUD 초상화 · 스킬 아이콘) — 인게임 스프라이트와 같은 도트

인게임 왕국군 스프라이트 옆에 붙는 자산은 **그 스프라이트의 측정값**을 따른다. §2 의 고색상 규격 금지.

**측정된 법칙 (실제 시트 픽셀 카운트, 2026-08-17):**

| 자산 | 고유색 수 |
|---|---|
| Royal/Elite 직업 스프라이트 8종 | **14 ~ 29색** (Elite_Knight 14, Elite_Mage 29) |
| VFX FireTornado / IceSpike | **5색 / 2색** |

- **캔버스**: 초상화 = **논리 32×32 → NEAREST ×2 → 64px 에셋**, 스킬 아이콘 = 48×48.
  ⚠ **캔버스 크기만 맞추면 안 된다.** 생성물이 정밀할수록 실효 해상도가 올라가 세트 안에서
  따로 논다(실측: Elite_Knight 색 런 1.66px vs Archer 4.83px). **논리 해상도로 먼저 뭉갠 뒤
  정수배 확대**해야 블록 크기가 통일된다.
- **색 수**: 초상화 ≤16색, 스킬 아이콘 ≈10색(명암 램프용). 그라디언트·AA·디더링 전부 금지.
- **윤곽선**: 캐릭터 = 인접 채움색의 **어두운 버전**(순검정 금지) / VFX = 윤곽선 **X** (§6 과 동일).
- **명암(입체감)** — 평면적이면 저해상도라도 싸구려로 보인다. 필수 규칙:
  top-left 단일 광원 / 재질당 3톤(광·기본·그림자) / 그림자는 한색 편이, 하이라이트는 난색 편이 /
  금속 상단에 1~2px 스페큘러 / 돌출부(투구챙·턱·어깨) 아래 캐스트 섀도.
  ⚠ 명암은 **서로 다른 톤의 평면 블록**으로 만든다 — 디더링으로 흉내내지 말 것.
- **세트 통일**: 승인된 구성원 1장을 **스타일 앵커 참조 이미지**로 항상 함께 넣는다
  (초상화=Archer, 아이콘=Silphir). 시드보다 참조가 훨씬 강하게 작동한다.

**생성 절차 (검증됨):**
1. 참조 = **그 직업 자신의 스프라이트 첫 프레임을 6배 NEAREST 확대**해 어두운 배경에 얹은 이미지
   (아이덴티티 + 픽셀 스케일을 동시에 전달). VFX 아이콘은 게임 VFX 프레임을 참조로.
2. 프롬프트에 **"레퍼런스는 저해상도 픽셀 스프라이트다 / 논리 픽셀 1개 = 큰 사각 블록 /
   전체가 약 40×40 논리 픽셀 / 약 16색 / 그라디언트·AA·디더링 금지 / 고해상도 일러스트 금지"** 를 명시.
   NB2 는 네거티브가 없으므로 금지 항목을 본문에 대문자로 강조한다.
3. **후처리는 BOX 평균이 아니라 타일 최빈색(k-centroid k=1) 다운스케일** — 평면 색면 경계를
   중간색으로 뭉개지 않는다. 이어서 가장자리 flood-fill 키 → **미디언컷 양자화** → 알파 이진화.
   스크립트: 세션 스크래치의 `postprocess_dot.py`.

> ❌ **실패한 접근 (반복 금지):** 매끄러운 HD 생성물을 그 직업 스프라이트의 실측 팔레트에
> 최근접 스냅하는 방식. 남의 색분포로 밀어 넣으면 노이즈가 된다 — **도트는 후처리가 아니라
> 생성 단계에서 만들어야 한다.**

## 7. 현재 상태

| 카드 | 아이콘 | 일러스트 | 비고 |
|---|---|---|---|
| Astra (심판의 여신 아스트라) | ✅ | ✅ | 본 스펙으로 생성한 첫 카드. `Assets/Generated/ComfyUI/DivineSkill/Astra/` |
| Lumen / Gaien / Silphir / Ferrum / Hora / Ignis / Nox | ❌ | ❌ | 동일 절차로 순차 생성 예정 |
