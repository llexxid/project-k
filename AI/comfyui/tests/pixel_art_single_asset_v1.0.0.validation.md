# Validation Report — pixel_art_single_asset_v1.0.0

**Final status: ✅ VERIFIED** (real generation completed; outputs verified programmatically)

## When
- Date: 2026-08-09
- Run by: Claude Code via the `comfy-cloud` MCP bridge

## Environment tested
- **Mode:** Comfy **Cloud** (hosted) — NOT a local ComfyUI install. There is no local `/object_info`, no local model directory, and no VRAM figure to report; all compute ran on Comfy Cloud's remote GPUs.
- **Server:** `https://cloud.comfy.org` — MCP server `comfy-cloud` v0.37.0, environment `production`, auth OAuth (authenticated).
- **Orchestrator host:** Windows 11 (Claude Code). The local GTX 1650 (4 GB) was **not** used.

> Note on the source prompt: the pasted brief assumed a *local* ComfyUI (installation path, `/object_info`, VRAM, `comfy-cli`). Those phases are not applicable to Comfy Cloud and were intentionally replaced with the Cloud MCP equivalents (catalog search, live node schemas via `get_node`, `dry_run` validation, `estimate_credits`, `submit_workflow`).

## Model stack selected
| Role | File | Why |
|---|---|---|
| Checkpoint | `sd_xl_base_1.0.safetensors` (SDXL 1.0) | Only clean SDXL base in the catalog; the canonical base Pixel Art XL was trained on. Preferred over `realvisxlV50_v50Bakedvae` (realism bias fights the pixel look). Bundles its own VAE. |
| Style LoRA | `sdxl-pixel_art_xl.safetensors` (Pixel Art XL) | The most proven pixel-art LoRA in the catalog; the only strong pixel LoRA that **supports negative prompts** (needed for the long negative list). |

Both filenames were confirmed present in the live `CheckpointLoaderSimple` / `LoraLoader` option lists (via `get_node`) — not guessed.

## Required custom nodes
None. All seven nodes are ComfyUI **core** (`pack: core`).

## Validation stages
1. **Node schema check** — `get_node` for all 7 classes; confirmed exact input names, and that `dpmpp_2m` / `karras` and both model files exist in the live option lists.
2. **Graph validation** — `submit_workflow` with `dry_run: true` → `status: "validated"`, **0 warnings** (node existence, link integrity, required inputs all pass).
3. **Cost estimate** — `estimate_credits` → **0 paid API credits** (no partner/API nodes; OSS model = GPU-time only).
4. **Execution** — `submit_workflow` (real) → `prompt_id: bbedef07-3ee0-4259-905f-6e7db6c7c6e6` → `wait_for_job` → **`job_status: completed`** (terminal success confirmed, not merely queued).
5. **Output retrieval + verification** — `get_output` → downloaded PNG → PIL confirms dimensions.

## Test performed
- **Prompt (positive):** `pixel art, single full-body fantasy goblin miner enemy, facing right, … large round mining helmet, small pickaxe …, dark outline, three-value shading, flat solid magenta background, one character only` (full text in the API JSON, node 6).
- **Negative:** photograph, photorealistic, 3d render, painterly, …, multiple characters, extra limbs, cropped head/feet, semi-transparent edges, blurry, lowres (full text in node 7).
- **Settings:** seed `1942837601`, 30 steps, cfg 7, `dpmpp_2m` / `karras`, denoise 1.0, **1024×1024**, batch 1, LoRA strength 1.0 / 1.0.

## Outputs (verified)
| File | Verified |
|---|---|
| `tests/outputs/pixel_art_single_asset_v1.0.0/raw_pixel_art_single_asset_00001.png` | **1024×1024 RGB** (PIL), 752 KB, non-empty, decodes |
| `tests/outputs/pixel_art_single_asset_v1.0.0/preview_pixel_art_single_asset_64x64_00001.png` | **64×64 RGB** (PIL), nearest downscale |
| `tests/outputs/pixel_art_single_asset_v1.0.0/preview_64x64_view512.png` | 512×512 nearest view of the preview (for human eyes only) |

Outputs were staged **outside** `Assets/` (per the rule against writing unreviewed images into Unity). No approved Unity asset was touched. No package, node, or model was installed.

## Result quality (honest)
- Full-body goblin miner, clear silhouette, dark outline, hard-edged clusters, helmet + pick — a usable v1.0.0 sprite.
- **Background came out near-white**, not the prompted magenta key-color (SDXL pixel models commonly ignore bg color). Background removal / alpha is a separate later stage.
- Pose reads front-facing rather than strictly right-facing — a v1.1 tuning/ControlNet concern.

## Changes made after failures
None — the first `dry_run` passed and the first real execution succeeded. No OOM (cloud compute).

## Verdict
**VERIFIED** — every acceptance criterion that applies to a Cloud setup passed: valid graph, all node classes and model references exist and are selectable, `dry_run` clean, real generation reached `completed`, raw (1024×1024) and 64×64 preview both saved and dimension-verified, nothing installed, no Unity asset overwritten.
