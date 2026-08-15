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

## Communication
- Simple, formal, low-token. Keep vital details; omit filler.
