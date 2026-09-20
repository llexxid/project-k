# Spell animation revision — 2026-09-20

Three requested changes: restore the original non-bloom Lightning, remove FireTornado's flat-looking crown, and animate a substantially slower/heavier meteor. Purchased sources and earlier iterations remain intact.

## Sources and process

- Lightning: `Assets/_Project/Tests/Fixtures/MageTowerLegacy/Lightning/ThunderEffects.png`. The runtime copy `Refined/LightningOriginal.png` has exactly the same five 70×149 frames. Historical `884652ec9` starts the next strike on the 2/12-second hit event, with radius-0.8 random scatter after the initial contact. World size is the original 64 PPU × 1.5 scale. Exact historical pixels retain their existing intermediate colors; no new quantization is imposed on this user-requested restoration. Bloom assets are untouched.
- Fire: purchased working copy `Assets/_Project/Art/VFX/PixelArtRPGVFX/Textures/Fire/FireTornado.png`. Inspection found 4–6 pixels of top padding, so the original flat cap, rather than texture clipping, caused the cut-off appearance. Only the cap is sculpted; two animated flame tongues and 16 pixels of canvas headroom are added. Twelve 64×80 frames at 24 fps preserve the original six body poses and 0.5-second loop. Six colors, binary alpha, no occupied frame border.
- Meteor plume: previous Comfy master `../revision1/meteor-zimage-original.png`, retained as the coherent long flame silhouette.
- Meteor surface: new `surface-master.png`, Z-Image Turbo BF16 + hard-edge pixel LoRA 0.7, Qwen 3 4B CLIP, ae VAE, AuraFlow shift 3, 1024² latent, seed 2026092053, 8 Euler/simple steps, CFG 1. Full prompt and actual settings are in `surface-submitted-request.json`; no overrides were omitted.

`surface-api.json` is the executable API graph, `surface-ui.json` the editable graph returned from Cloud storage. Cloud workflow ID: `b7a2c42b-6d7b-4526-be2d-76fc288e7e7c`; job ID: `da94555b-34c9-4e3a-8cf6-bf8133678d87`. The service returned a storage ID but no graph URL. Model filenames and node schemas are saved in `master-nodes.json`; the service did not expose immutable node/plugin commit versions. A fixed sampler seed was supported; identical output across future GPU/runtime revisions is not guaranteed.

Live catalog discovery included displacement/warp, noise, pixel LoRA and model/template options. The generated material is finished using deterministic Pillow/NumPy spherical projection and palette/mask animation (`finalize.py`) so its cracks rotate coherently around the limb. Baking these transformations locally provides exact binary alpha, frame pivots and palette control without independently generating or upscaling each frame. Per-frame diffusion is not used.

## Iterations and adopted settings

1. Rejected `prototype-v1.py` / `prototype-v1-contact.png`: 160×192 canvas, procedural broad plume and dark material quantization. The plume became a solid blob and the core was too dark/noisy at battlefield size. Kept as provenance; do not execute it over the adopted output.
2. Adopted `finalize.py`: reuse the earlier master flame silhouette, advect along/across its axis, classify the new basalt texture into six colors after a median pass, and project onto a two-axis rotating body. Canvas grows to 192×208 to give sparks and the plume adequate transparent margins. Core radii 25×28 logical pixels; yaw 0.72 radians/sec, roll −0.21 radians/sec; irregular hot rim, 14 drifting embers and three rotating chips. All layers are baked into one sprite renderer.

Output: 48 frames at 20 fps, 1536×1248 sheet, Point / FullRect / 32 PPU / ASTC 4×4, no mipmaps or readable CPU copy. Animation is non-looping. Flight changes from 0.8 to 2.4 seconds, progress `0.38t + 0.62t²`. Head pivot and mirrored approach preserve the same landing point. Existing crater and two residual damage ticks remain unchanged; no cast circle is introduced.

The logical sheet has 1,916,928 texels (7.31 MiB raw RGBA; approximately 1.83 MiB ASTC 4×4 before atlas packing). Atlas packing and actual device memory/performance are assessed separately. Increased frame count adds texture memory; it does not add runtime particle objects or extra meteor draw layers.

## Usage evidence

`billing-job.json` confirms **2.786269 GPU seconds** on RTX Pro 6000 for this run. `surface-estimate.json` reports zero API-node credits, which excludes GPU execution and must not be reported as a free run. `usage-before.json` and `usage-after.json` are invoice-backed reports; the first after-query still ended at 08:00 UTC, before this 08:17 UTC job. A later report must be checked before stating the actual dollar increase. Comfy does not expose per-job invoiced dollars.

`usage-final.json`, queried after 09:00 UTC, includes the 08:00–09:00 bucket: **$0.003608218355** for GPU Hours Product. The workspace total increased from $38.275012597626656 to $38.278620815981654, with other product totals unchanged. `cost-summary.json` records this actual invoice-backed aggregate increase (about **$0.00361**), rather than presenting the preflight estimate or a fabricated per-job dollar quote.

Editor and physical-device results are recorded under `Docs/ArtPreparation/SPELL_ANIMATION_20260920.md`. Full local captures, temporary signed output URL and original device save backups stay under ignored `Recordings/SpellAnimationRevision/`.
