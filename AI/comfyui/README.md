# Project-K — ComfyUI (Comfy Cloud) Workflows

This folder holds the AI image-generation workflows for the game, plus their manifests and test
reports. **Generation runs on Comfy Cloud** (remote GPUs) through the `comfy-cloud` MCP bridge — there
is no local ComfyUI, so nothing here loads your GPU.

```
AI/comfyui/
├── README.md                                  ← you are here
├── workflows/
│   ├── pixel_art_single_asset_v1.0.0.api.json       ← the workflow (API format, source of truth)
│   └── pixel_art_single_asset_v1.0.0.manifest.json  ← models, automation slots, locked parts
└── tests/
    ├── pixel_art_single_asset_v1.0.0.validation.md  ← real test result (VERIFIED)
    └── outputs/pixel_art_single_asset_v1.0.0/       ← the test PNGs (raw + 64×64 preview)
```

## What `pixel_art_single_asset_v1.0.0` does
Generates **one** static pixel-art asset (or one animation frame) per run — a character, enemy, icon,
effect, or environment concept — at 1024×1024, using **SDXL base + the Pixel Art XL LoRA**. It is the
stable foundation the rest of the art pipeline builds on.

### Why one asset per run
Diffusion is unreliable at drawing a clean multi-frame sheet in a single image (duplicated/registered
frames drift). We generate frames **individually**, approve them by hand, then assemble spritesheets
deterministically in a later step. Do **not** prompt this workflow for a grid/contact-sheet.

## Where images go
- Comfy Cloud saves the raw image under its output dir with prefix `game_pixel_art/raw/…`.
- We download it into `AI/comfyui/tests/outputs/<workflow>/` (a **staging** area outside `Assets/`).
- Only **after human review** does an approved sprite move into `Assets/Generated/ComfyUI/…` for Unity.
  Never write an unreviewed generation straight into `Assets/`.

## The 64×64 preview
A nearest-neighbour downscale of the raw image, produced locally for pixel-level review. It is **not**
an approved final sprite — it does not guarantee clean pixel clusters, palette compliance, or alpha.
The **raw 1024×1024 image is the source of truth.**

## Safe vs locked

| ✅ Safe to change (per run) | 🔒 Do NOT change without re-validation |
|---|---|
| Positive prompt (node 6) | Base checkpoint `sd_xl_base_1.0.safetensors` (node 4) |
| Negative prompt (node 7) | Style LoRA `sdxl-pixel_art_xl.safetensors` (node 10) |
| Seed (node 3) | Sampler / scheduler `dpmpp_2m` / `karras` (node 3) |
| Steps / cfg (node 3) | VAE source (checkpoint VAE, node 4 → node 8) |
| Source width/height (node 5, SDXL-native ~1MP) | Node wiring / links |
| LoRA strength (node 10) | Node class types |
| Raw filename prefix (node 9) | |

Changing the model or a locked node can silently break compatibility — re-run the validation in
`tests/…validation.md` afterward (dry-run → estimate → real run → verify outputs).

## How to use it (human)
- **Open/edit the canvas:** it's saved in your Comfy Cloud workspace as `pixel_art_single_asset_v1.0.0`.
  Open it in the Comfy canvas to tweak visually.
- **Change subject** (e.g. character → icon): edit the **positive prompt** — swap the subject clause and
  keep the pixel-art style clauses (`pixel art, … dark outline, hard-edged color clusters, …`). For a
  small item icon, also drop the source size toward a square SDXL-native size and describe "centered
  single item icon on flat background".
- **New seed / several candidates:** change the seed (node 3), or run a few seeds and pick the best.

## How Claude Code uses it (automation, later)
The API JSON is the reproducible artifact. Two ways to run it on Comfy Cloud via MCP:
- `run_saved_workflow` with the saved name `pixel_art_single_asset_v1.0.0` + `input_overrides`, or
- `submit_workflow` with the JSON from `workflows/…api.json` + edited inputs.

**Automation-editable node IDs / inputs** (also in the manifest's `automation_slots`):

| purpose | node id | input |
|---|---|---|
| positive prompt | `6` | `text` |
| negative prompt | `7` | `text` |
| seed | `3` | `seed` |
| steps / cfg | `3` | `steps` / `cfg` |
| source width / height | `5` | `width` / `height` |
| batch size | `5` | `batch_size` |
| LoRA strength | `10` | `strength_model` / `strength_clip` |
| raw filename prefix | `9` | `filename_prefix` |

Do **not** let automation casually change node 4 (`ckpt_name`), node 10 (`lora_name`), the sampler
architecture, or the wiring — those are the locked identity of the workflow.

## Current known limitations
- Background renders near-white, not a clean key-color — **background removal/alpha is a later stage**.
- Directional pose control (strict "facing right") is not reliable yet — a v1.1 prompt/ControlNet task.
- Model SHA-256 not recorded (models live on Comfy Cloud, not locally).
- This is text-to-image only; reference/pose control (ControlNet, IP-Adapter) is deferred to a future
  `pixel_art_controlled_frame` workflow once the primary is stable.

## Reproducibility
Everything needed to reproduce a generation is captured: workflow (`api.json`), model files + strengths
(manifest), seed and all sampler settings (manifest `test` + API JSON). If the model or workflow
changes, treat the output as a **new** generation and bump the version — don't silently overwrite.
