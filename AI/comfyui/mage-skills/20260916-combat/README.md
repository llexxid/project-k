# Combat revision art and sound provenance

## Adopted production assets
- Starfall (`ArcaneVolley`, stable ID 3): `Starfall-v1` base and `StarfallBloom-v1` variant. Actual Comfy Cloud MCP catalogue and node schemas were inspected before execution. Nano Banana 2 / Gemini 3.1 Flash Image, 1K, MINIMAL. Full prompts, reference inputs, API JSON, editable UI graph, overrides, cloud graph saves, output and job responses remain in each version folder.
- Base image job: `db8fb556-5052-45a0-b150-db56c9a4c15a`; bloom job: `ebc39a81-1168-4969-ab09-0d24ed3a293d`. Seeds 91631/91632 were submitted where supported; hosted model determinism is not guaranteed.
- `finish_icons.py`: owned generated source -> exact 48 px nearest finishing, restrained palette (base 11 / bloom 13 opaque colours). Bloom retains the falling-star composition with 41 changed pixels. The Comfy quantized alternative flattened useful detail and was rejected; intermediates remain for comparison.
- `prepare_derivatives.py`: existing owned Lightning 1 animation recoloured violet, original cloud silhouette mapped to neutral gray, interior lightning branches from the same bolt; six-frame rotating opaque rock plus animated existing flame for meteor; square airborne void animation; projectile from the approved falling-star icon; goblin bomb from its own throw sheet. No ExternalAssets originals were changed. Sources, hashes, exact crops, palette changes and processing versions are in `derivatives-v1.json`.
- `finish_audio_and_button.py`: restrained bronze button border, retained original wood interior; six short mono 22,050 Hz combat WAVs with explicit onset, gain and 12 ms edge fades. Source masters and previous button are preserved. This script contains only repeatable asset finishing, no source-code migrations.
- `finish_venom.py`: the first device comparison left the green field difficult to distinguish from grass. The owned Poison Effect 05 working sheets now use four muted sage colours with opaque interior pixels, preserving the original animation frames. `venom-finish-v1.json` and the prior sheets retain the exact before/after recipe. Iteration6 confirmed the field remains behind bodies and is more visible on stage 3 grass.

## Sound generation
Comfy Cloud ElevenLabsTextToSoundEffects / eleven_sfx_v2 job `a33470d4-e941-4624-910c-a97f29f67e5f` produced dry sword and heavy impact cues. The original sword master had four clipped samples; final gain/trim yields no clipped samples. `Audio-v1/final-mix.json` records all six final clips (0.30–0.46 s), including owned thrust/throw/magic/whip sources. Runtime caps six pooled voices, at most two royal-guard / three monster voices, and rate-limits simultaneous cues. Acoustic listening on different speakers/headphones remains outside the automated signal checks.

## Why this process
The user requested effect identity and cleaner combat readability. Comfy generation was used for the missing starfall icon identity and short impact sound sources. Existing pixel animations gave stronger frame-to-frame control and matching pixel density for meteor, lightning, cloud and portal; generating independent animation frames would introduce flicker. Code-authored flat button borders preserve the established UI. No GPT Image call or other unexecuted model is claimed.

Reference research: [Pixel Skies](https://www.mattwalkden.com/pixel-skies/) for layered pixel storm readability; [Unity pooling](https://learn.unity.com/tutorial/use-object-pooling-to-boost-performance-of-c-scripts-in-unity) and [Unity garbage-collection guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/performance-garbage-collection-best-practices.html). No website artwork was copied. An ArtStation page required login and was not used as a viewed reference.

## Actual cost
`usage-before.json`, `usage-final.json` and `cost-summary.json` contain invoice-backed Comfy usage reports. The workspace spending delta in the measured completed-hour window is **USD 0.1766056004566625**. This is a workspace aggregate, not a per-job invoice; delayed billing can alter later reports. It includes Gemini image/text inputs and outputs, GPU work, and ElevenLabs sound effects. Failed calls/intermediates are preserved; estimates are not presented as actual charges.

## Integration and verification
Stable skill IDs/GUIDs retained. Existing SO/prefab generation remains in MageSkillAssetPreparation. Instant strikes render above monsters; persistent fields render behind. Frame captures, device results and remaining limits are documented in `Docs/ArtPreparation/COMBAT_REVISION_VALIDATION.md` after final Android verification. Large local recordings and isolated test-account backups are not committed.

Single-use upload authorization URLs are redacted in the two versioned `upload-response.json` records. The submitted workflows, input asset names, prompts and output provenance are unchanged; complete responses remain in the ignored local task record.
