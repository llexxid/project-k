# CLAUDE.md — Project-K AI Development Pipeline

Guidance for Claude Code working in this repository. This is a **mature Unity project**, not a
greenfield one — prefer incremental, additive changes and never restructure broadly without asking.

## 1. What this project is
- **Engine:** Unity **6.3 LTS** (`6000.3.21f1`). Mobile idle-RPG.
- **First-party code & assets** live under `Assets/_Project/` (type-first: `Art/`, `Audio/`,
  `Prefabs/`, `Scenes/`, `ScriptableObjects/`, `Scripts/`, `Settings/`).
- **Third-party / feature modules** stay at their existing locations (PlayFab, Google Play Games,
  UniTask, Addressables, 2D Animation/Aseprite, Layer Lab UI, etc.). Do **not** relocate these.
- UI has been migrated UI-Toolkit → **UGUI** and is being retextured to Layer Lab "Minimal Game Dark".
- When moving folders, sync hardcoded paths (e.g. `ConstPath`). See project memory for details.

## 2. The pipeline (roles)
This project is being wired into an AI-assisted pipeline. Each tool has one job:
- **Claude Code** — orchestrator: reads the project, writes C#, coordinates the other tools.
- **Unity MCP** — Claude ↔ Unity Editor bridge. Uses Unity's **official** package
  `com.unity.ai.assistant` (already installed). Tools available: `ManageGameObject`, `ManageScene`,
  `ManageAsset`, `ManageScript`/`ScriptApplyEdits`/`ValidateScript`, `ManageShader`, `ManageEditor`,
  `ManageMenuItem`, `ReadConsole`, `RunCommand`, `ImportExternalModel`.
- **ComfyUI (Comfy Cloud) MCP** — Claude ↔ image-generation bridge. HTTP server at
  `https://cloud.comfy.org/mcp`, configured in `.mcp.json` (git-ignored, local machine only).
- **Git** — source of truth. Tools are never the authoritative state.

> Connection status is environment-dependent. Unity MCP and any local generation require Claude Code
> to run **as a local terminal CLI on the developer's Windows machine** (so it can reach the local
> Unity Editor). A cloud/synced Claude Code session can use Comfy Cloud but cannot reach local Unity.

## 3. Working rules
**Code**
- Match surrounding conventions (naming, comment density, idioms). Prefer existing systems over new ones.
- Make the *smallest* change that satisfies the requirement; don't touch unrelated systems.
- Consult project memory and existing code before proposing architecture.

**Unity (via Unity MCP)**
- Editing a `.cs` file is **not** the same as changing Unity project state. If the task needs scene /
  GameObject / prefab / ScriptableObject / import state, use Unity MCP, then validate.
- Read before write: inspect current state → determine scope → change → validate → report.
- Never bulk-delete assets, rewrite unrelated scenes, or change project-wide settings without confirmation.

**Assets (via ComfyUI MCP) — not yet active**
- Generation is **data-driven**, keyed off ScriptableObject data (e.g. equipment/skill/item IDs), not
  ad-hoc prompts. Store reproducible metadata (workflow, model, seed, prompt, resolution) beside outputs.
- Don't regenerate assets that already exist. Prioritize: equipment icons → skill icons → item icons →
  enemy/character portraits → backgrounds. Pixel-art **consistency** over variety.
- Planned home: workflows/prompts/rules under `AI/`; generated PNGs under `Assets/Generated/ComfyUI/…`.

**Git**
- Never do destructive git operations without explicit approval. Respect the existing `.gitignore`.
- Don't commit caches, model files, or machine-specific config (`.mcp.json` is intentionally ignored).

## 4. When something MCP-related fails
- **Unity MCP:** confirm the Unity Editor is running and the MCP server is enabled
  (Edit → Project Settings → AI → Unity MCP Server), check the Unity Console, retry only when safe.
- **ComfyUI MCP:** confirm the server shows connected/authenticated (`/mcp`), verify the workflow and
  its inputs exist; never silently substitute a different workflow or model.

## 5. First milestone
Get the two bridges working **independently** first — Claude↔Unity and Claude↔ComfyUI — before building
any end-to-end automation. Details and setup steps live in the conversation / setup notes, not here.
