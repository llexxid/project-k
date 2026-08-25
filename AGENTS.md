# AGENTS.md — Project-K core rules

Rulebook for AI agents on this project. Complements `CLAUDE.md`. Follow exactly.

## Art assets — 조달 순서

1. **기성 키트 먼저.** 프로젝트에 이미 있는 것: `Assets/ExternalAssets/PixelArtGUI2`
   (도트 GUI: 패널/버튼/프레임/32·48px 아이콘 — 이 프로젝트 톤에 가장 잘 맞는다),
   `TinyRPG`, `5000FantasyIcons`, `Medieval Tools & Weapons Package`, Layer Lab Minimal Game Dark.
   **명시 경로로 로드한다** — 같은 파일명이 여러 해상도에 있어 이름 검색은 8px 아이콘을 집어온다.
2. **기존 아트에서 결정론적으로 파생.** 초상화·버스트는 실제 스프라이트 시트를 잘라
   정수배 NEAREST 확대 + 알파 이진화. 확산 모델은 14~29색 시트를 재현하지 못한다.
3. **그래도 없으면 생성.** 신 캐릭터 아트, 마탑 프롭처럼 원본이 아예 없는 것만.

생성 파이프라인의 실행 규칙·환경 제약·하드원 함정은 **`AI/comfyui/README.md`** 에 있다.
생성 작업 전에 그 문서를 먼저 읽는다.

## 생성 시 지출 가시성

- 지출은 허용되지만 **얼마나 썼는지 보고**한다 (`get_usage_report`).
- 유료 노드가 실패하면 부분 출력도 회수되지 않고 이미 나간 호출은 과금된다.
  새 프롬프트는 저비용 설정으로 한 번 검증한 뒤 프로덕션으로 올린다.

## 검증은 인게임에서

에셋을 만든 뒤에는 실제 화면에 올려 캡처해 **맥락 속에서** 판단한다.
격리된 스프라이트만 보고 판정하지 않는다.

## Recurring product rules (standing requirements)
> Meta-rule: whenever the user states a requirement that should apply to future work too
> (not just the current task), record it in this section immediately.
- **UI elements ship with subtle "alive" micro-animation by default** — breathing scale, light
  flicker/glow pulses, sway — synchronized where it makes sense (e.g. shake with CameraShaker).
  Use lightweight unscaled-time coroutines (UITween 계열); restart them on re-activation
  (UGUI coroutines die permanently when the GameObject is deactivated).
- **Concept consistency:** UI = rustic dark wood + bronze trim (`UguiTheme` Rustic* tokens,
  Layer Lab Minimal Game Dark assets). 신 캐릭터 아트 = `AI/comfyui/README.md` §7 아트 디렉션 락, VFX = flat 4–6 colors, no dark outline (§6 of the spec).
- **Anything shown next to the in-game sprites must match THEIR dot resolution** — party-HUD
  portraits, skill icons, etc. The real job sprites measure **14–29 unique colors**; the card
  illustration spec (thousands of colors) is for key art only. Follow **§8** of the style spec:
  48px canvas, ≤16 colors (≤6 for VFX icons), generated as chunky pixel art (not smoothed then
  downscaled), post-processed with mode-tile downscale + median-cut quantize.
- **Skill icons depict the SKILL EFFECT, not the caster.** A bust crop of the character
  illustration is not a skill icon.
- **ComfyUI is for STATIC images.** Animation comes from asset reuse / procedural authoring
  (masking, sweeps, palette pulses, Unity Animation) — never per-frame diffusion.
- **Decoration must never hurt readability or usability.** If ornamentation competes with
  text legibility, tap targets, or information hierarchy, cut the ornamentation.
- **Portraits/busts come from the ACTUAL in-game sprite sheets**, not from generation. Crop the
  upper body, integer-upscale (NEAREST), binarize alpha. Diffusion cannot match a 14-color sheet;
  the sheet already is the answer. (세션 스크래치패드의 `bust_from_sprite.py` 방식)
- **Every art task carries an optimization pass.** New sprites must land in a Sprite Atlas
  (`KingdomIdle/Optimize/2) Create In-Build Sprite Atlases`). 도트 아트 = `Atlas_UIPixel`
  (Point + ASTC_4x4); 스무스 UI = `Atlas_UI` (Bilinear + ASTC_6x6). **ASTC 6x6 이상은 픽셀 아트를
  뭉갠다 — 도트는 4x4 가 상한.** mipmap off, readable off, POT/max 2048.
- **FLAT art is authored in code, not generated.** When the brief says flat / no depth / "UI 처럼"
  (mage tower, panels, chips), draw it procedurally: flat color fields + one uniform dark outline,
  separation by **outline, never by shading**, then integer NEAREST upscale from a logical grid.
  Diffusion always sneaks in gradients and bevels, and flat art has no shading to fake — so code
  wins on accuracy AND costs nothing to iterate. (세션 스크래치패드의 `flat_tower.py` 방식)
- **신 8종의 디자인은 확정본이 아니다.** 예전 로스터 설정(예: Ignis = 남성 마왕)은 폐기됐다.
  새 신을 만들 때는 사용자가 그때 주는 디자인 지시만을 기준으로 삼는다.
- **Review a character ROSTER as a row, never one at a time.** Eight characters that each look fine
  alone can be interchangeable the moment they sit in a banner/collection grid — which is how the
  player actually sees them. Gate on: no two confusable in one second at 96px, no shared silhouette,
  no shared hair-colour family, no shared trim metal, no repeated accent hue, no shared framing
  furniture (halo discs, backdrops), at most one symmetric front-on pose.
- **Per-character uniqueness is enforced by PARAMETERS, not by the shared prompt.** Any clause baked
  into the shared template ("standing front-facing", "prop held vertically", "gold filigree") becomes
  a sameness engine that overrides every per-character instruction. Silhouette, pose, expression,
  accent hue and trim metal must each be a per-character field.
- **More ornament ≠ more premium at mobile pixel sizes.** Perceived rarity comes from a bolder
  silhouette, a stronger accent and a better pose. Detail thinner than a 3×3 pixel cluster is
  deleted, not shrunk — express the idea as a notch in the outline instead. Budget detail
  60/30/10 top-weighted (head / torso+prop / below the waist); icons crop the bust, so
  detail below the waist is pure cost.
- **Wrong camera angle is fixed by REFERENCE-GUIDED REGENERATION, not by cropping or redrawing.**
  When existing art is drawn from the wrong angle (e.g. a tower drawn from slightly above so you see
  into its top), cropping cannot fix it — an elliptical element has no horizontal cut line that leaves
  a clean front view — and redrawing from primitives destroys the art. The working method: upload the
  existing art (`upload_file`), wire `LoadImage → GeminiNanoBanana2V2(model.images.image_1) → SaveImage`
  via `submit_workflow`, and prompt "keep EVERYTHING identical — style, palette, detail level, outline
  weight, pixel block size — change exactly ONE thing: the camera angle", spelling out what must and
  must not be visible. Run 2–3 seeds and pick. Note `medias[].value` will NOT take a local or
  Comfy-uploaded file (auth-gated); the LoadImage + submit_workflow path is the one that works.
- **"Reduce the detail" does NOT mean redraw at lower fidelity.** Mode filters and hard quantization
  mush the dots and read as damage, not as simplification. If asked to simplify existing pixel art,
  change composition/framing/angle, or remove specific named elements — never smooth the pixels.
- **Chroma-key in three passes.** Border flood-fill alone leaves background inside *enclosed*
  regions (a chain loop, a halo ring). Follow it with a strict global chroma test (kills pure
  green, spares desaturated costume greens), then peel 1–2 px of loose-green pixels that touch
  the background to de-fringe thin anti-aliased lines.

## Communication
- Simple, formal, low-token. Keep vital details; omit filler.
