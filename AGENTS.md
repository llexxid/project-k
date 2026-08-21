# AGENTS.md — Project-K core rules

Rulebook for AI agents on this project. Complements `CLAUDE.md`. Follow exactly.

## Art assets — ComfyUI first
1. **Generate all art via ComfyUI** (comfy-cloud MCP): icons, sprites, spritesheets, VFX, UI, portraits.
2. **Exception (allowed):** first make a temporary base yourself (procedural draw, recolor, keyed frames,
   clean silhouette) when that produces a better or more on-style result, then feed it to ComfyUI
   (img2img / style-reference) for the final art. Prefer this over raw text-to-image when structure matters.
3. **Match project style.** Feed real project reference assets (existing sprites, `FireTornado.png`,
   hit effects) to ComfyUI as img2img / style reference. Do not ship generic SDXL output that looks
   off-style. Palette-snap to the project fire/UI ramps where relevant (see `AI/comfyui/style_spec_fire.md`).
4. **Iterate + verify every time.** Generate → view the result → judge direction. If it is off-style or
   off-direction, immediately change the ComfyUI workflow (prompt, denoise, LoRA, ControlNet/IP-Adapter,
   model, resolution) before generating more. Never keep generating in a wrong direction.
5. **Research when unsure.** Check the web and the Comfy catalog (`search_models`/`search_templates`/
   `search_nodes`) for the best model/tool/settings for the task.
6. **Batch candidates** with `submit_batch`; OSS SDXL runs are ~0 paid credits (GPU-time only).

## Division of labor
- **ComfyUI** = the art (diffusion).
- **Procedural (PIL/numpy)** = only what diffusion can't control: alpha keying, palette-snap, and
  animation timing / impact (타격감). Tools live in `AI/tools/`.
- **Deterministic recolor** is fine for exact variants of existing art (e.g. alt-palette class).

## Files & hygiene
- Stage generated art under `AI/` (outside `Assets/`) until reviewed; approved art → `Assets/Generated/ComfyUI/`.
- Delete rejected candidates to save space. Keep source stills used by a final asset.

## Unity / Git
- See `CLAUDE.md`. Editing a `.cs` file ≠ changing Unity state; use Unity MCP for scene/prefab/SO work.
- No destructive git without approval.

## Use ALL tools — paid included
- Do **not** restrict to free OSS models. Use partner/API models (Nano Banana, Seedream, GPT-Image,
  Ideogram, etc.) **when they help**, and use ControlNet / IP-Adapter / upscalers / video / interpolation.
- Note: OSS graphs (SDXL, Pixel Art XL, IP-Adapter) estimate as "0 paid credits" but still consume your
  Comfy Cloud **subscription GPU compute** — "0 credits" ≠ free. Partner nodes cost metered credits.
- Spend is allowed. Just **report how heavy the run was** (rough count of generations / partner calls /
  compute) so cost stays visible. Prefer `submit_batch` and multi-agent Workflows to go wide.

## Reference-guided generation (this is the real "customize the workflow")
- Feed the project's ACTUAL art into ComfyUI — never rely on text prompts alone.
  - **IP-Adapter** (style): `IPAdapterModelLoader` (`ip-adapter_sdxl_vit-h.safetensors`) +
    `CLIPVisionLoader` (`CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors`) + `IPAdapterAdvanced`
    (`weight_type:"style transfer"`, weight ~0.6–0.7; higher bleeds the ref background). Pack:
    `comfyui_ipadapter_plus`. Fire refs: `FireTornado.png`, `Hit Effect 01`.
  - **ControlNet** (structure) and **img2img** (from an authored base) to hold composition.
- Try multiple models/LoRAs; fan out candidates; adversarially judge which best matches the game.

## Animation / motion (learned the hard way)
- **ComfyUI cannot create motion** — it styles frames. **Author the motion**, then style each frame.
- Method: author motion frames → pack into ONE grid image → single img2img+IPAdapter style pass →
  slice back → key to alpha → palette-snap → assemble. Frames stay consistent (shared seed/structure).
- **Explosions must be real motion, not scale-in-place.** A blob that grows then shrinks is wrong.
  Model it **ballistically**: fire gushes UP forcefully → gravity arcs it → it FALLS and SPREADS
  outward into a wide ground blaze → burns out (up → arc → spread → die). Reference the feel, don't copy.

## Verify in-engine
- After producing an asset, use **Unity MCP** (`Unity_SceneView_Capture*` / camera capture) to place it in
  the real scene and screenshot it — judge style **in context**, not just as an isolated sprite.

## Recurring product rules (standing requirements)
> Meta-rule: whenever the user states a requirement that should apply to future work too
> (not just the current task), record it in this section immediately.
- **UI elements ship with subtle "alive" micro-animation by default** — breathing scale, light
  flicker/glow pulses, sway — synchronized where it makes sense (e.g. shake with CameraShaker).
  Use lightweight unscaled-time coroutines (UITween 계열); restart them on re-activation
  (UGUI coroutines die permanently when the GameObject is deactivated).
- **Concept consistency:** UI = rustic dark wood + bronze trim (`UguiTheme` Rustic* tokens,
  Layer Lab Minimal Game Dark assets). Character/card art = `AI/comfyui/style_spec_character.md`
  (locked reference set), VFX = flat 4–6 colors, no dark outline (§6 of the spec).
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
- **Existing assets FIRST — generation is the last resort.** Before any Comfy call, search the
  project's shipped kits. Known kits: `Assets/ExternalAssets/PixelArtGUI2` (도트 GUI: 패널/버튼/
  프레임/32·48px 아이콘 — 이 프로젝트 톤에 가장 잘 맞는다), `TinyRPG`, `5000FantasyIcons`,
  `Medieval Tools & Weapons Package`, Layer Lab Minimal Game Dark. Generate only what genuinely
  does not exist, and say so.
- **Portraits/busts come from the ACTUAL in-game sprite sheets**, not from generation. Crop the
  upper body, integer-upscale (NEAREST), binarize alpha. Diffusion cannot match a 14-color sheet;
  the sheet already is the answer. (`AI/tools/bust_from_sprite.py` 계열)
- **Every art task carries an optimization pass.** New sprites must land in a Sprite Atlas
  (`KingdomIdle/Optimize/2) Create In-Build Sprite Atlases`). 도트 아트 = `Atlas_UIPixel`
  (Point + ASTC_4x4); 스무스 UI = `Atlas_UI` (Bilinear + ASTC_6x6). **ASTC 6x6 이상은 픽셀 아트를
  뭉갠다 — 도트는 4x4 가 상한.** mipmap off, readable off, POT/max 2048.
- **Load shipped-kit sprites by EXPLICIT PATH, never by name search.** These kits ship the same
  filename at many resolutions (`Icons/8/`, `Icons/32/`, …); `FindAssets("shield")` picks an
  arbitrary one and the UI silently gets an 8px icon.
- **FLAT art is authored in code, not generated.** When the brief says flat / no depth / "UI 처럼"
  (mage tower, panels, chips), draw it procedurally: flat color fields + one uniform dark outline,
  separation by **outline, never by shading**, then integer NEAREST upscale from a logical grid.
  Diffusion always sneaks in gradients and bevels, and flat art has no shading to fake — so code
  wins on accuracy AND costs nothing to iterate. (`AI/tools/flat_tower.py` 계열)
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
- **Comfy partner slugs drift — verify before batching.** `get_prompting_guide(model: "partner")`
  lists the live registry. Nano Banana 2 is `vertexai/nano-banana-2` (the old `GeminiNanoBanana2V2`
  now bounces). A wrong slug inside `submit_batch` surfaces only as `validation.schema`, with no
  hint which item is bad — test one item via `partner_generate` first, which names the real error.
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
