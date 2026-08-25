# Astra 프로덕션 프롬프트

- 생성 모델: `OpenAIGPTImageNodeV2` / `gpt-image-2` / quality `high`
- 마스터 1024x1536 → 도감 1024x1536(edit), 컷씬 1152x2048(edit)
- Unity 기준 해상도 1080x1920 세로 (`UguiTheme.RefWidth/RefHeight`) → 컷씬 9:16
- 마스터는 게임에 직접 쓰지 않는 **내부 기준 이미지**다. 두 결과물의 참조로만 쓴다.

## 1. MASTER

```text
Create the definitive master reference image for ASTRA, a clearly adult celestial goddess for a premium anime fantasy mobile game.

One single adult woman with a visibly mature refined anime face, tall elegant proportions, long legs, graceful silhouette, calm and unreachable divine presence. Fully visible metallic silver irises, cool confident expression and a restrained faint smile.

Her extremely long Milky-Way hair reaches below her calves. It must still read as real layered flowing hair, with near-black indigo roots, deep violet and cold blue middle sections, lavender highlights and silver-white tips. Crisp stars and small nebula shapes appear inside the hair without turning it into smoke.

LOCKED COSTUME:
A liquid-silver floor-length asymmetric goddess dress with one bare shoulder, a deep but non-explicit neckline, open side-waist sections connected by polished silver bands, detached long silver sleeves, silver upper-arm bands and one silver band around the left thigh. Exactly one high slit over the LEFT thigh. Near-black indigo inner lining. Tasteful revealing adult fantasy design with opaque coverage of intimate areas.

Exactly three identical polished-silver six-point astral star sigils, same size and same construction, floating in a shallow arc behind her shoulders. No other floating props.

Full-body 3/4 front view, relaxed neutral pose, both hands fully visible, feet fully visible, hair and dress completely inside the frame, generous safe margin around the silhouette. Simple dark indigo cosmic studio background with a restrained flat silver halo.

Premium Japanese anime game character key art, clean shape language, strong readable silhouette, controlled detail density suitable for later conversion into high-resolution pixel art.

Do not create a child, teenager, youthful baby face, chibi proportions, school uniform, explicit nudity, transparent coverage, gold decoration, crown, blindfold, visor, armor, cape, weapon, extra character, duplicate body, extra limbs, malformed hands, extra fingers, cropped feet, cropped hair, text, logo, watermark or UI frame.
```

## 2. CODEX (도감)

```text
Using the supplied ASTRA master image as the exact identity, face, body proportion, hairstyle, costume, color palette and prop blueprint, create ASTRA'S FINAL CODEX PORTRAIT. Do not redesign her.

Preserve her mature face, visible metallic silver eyes, exact Milky-Way hair color order, exact liquid-silver asymmetric dress, exactly one high slit over the LEFT thigh, detached sleeves, silver arm bands, left thigh band and indigo inner lining. Preserve exactly three identical silver six-point astral star sigils. Do not add a weapon, crown, cape, armor, visor, blindfold or gold.

Vertical 2:3 full-body composition. Elegant contrapposto and slight 3/4 turn toward the camera. One hand lightly gathers the outer edge of her galaxy hair while the other rests open near her hip. Keep the head, hair, hands, entire dress hem and both feet completely inside the frame. Make her face, costume construction and silhouette readable on a mobile-game codex screen.

Use a restrained cosmic sanctuary background with stepped deep-indigo and violet shapes, a thin silver circular astral diagram and sparse stars. The background must not merge with her hair or dress.

Premium anime fantasy mobile-game codex key art intended for high-resolution pixel-art conversion. Clean silhouette, large readable shapes, controlled highlights and limited visual noise.

Strictly preserve the master identity and costume. Avoid a younger face, different hairstyle, shortened hair, altered neckline, second slit, changed asymmetry, extra props, altered star-sigil count, malformed hands, cropped body, text, logo, watermark, card border or UI.
```

## 3. ULTIMATE (궁극기 컷씬)

```text
Using the supplied ASTRA reference image as the exact identity - face, mature adult proportions, hairstyle and hair color order, costume construction and color palette - create ASTRA'S ULTIMATE-SKILL CUTSCENE CUT. Do not redesign her.

Preserve exactly: her mature adult anime face, fully visible metallic silver irises, extremely long Milky-Way hair with near-black indigo roots, deep violet and cold blue mid-lengths and silver-white tips, the liquid-silver asymmetric dress with one bare shoulder, open side waist connected by polished silver bands, detached long silver sleeves, silver upper-arm bands, and the near-black indigo inner lining. No gold, no crown, no visor, no blindfold, no weapon, no armor, no cape.

COMPOSITION - match this camera and pose exactly:
Upper-body half-body shot seen from slightly behind and to the side, a dynamic three-quarter back view. Her torso turns away from the camera while her head turns into near-profile to look off toward the horizon, chin slightly lifted, calm serene expression with a faint restrained smile. Her far arm sweeps out and forward, fully extended, the hand open with fingers naturally spread and relaxed. Her enormous galaxy hair streams in one continuous direction across the frame in a strong wind, long flowing strands separating into ribbons. The whole image feels airy, weightless and windswept.

CRITICAL - the character and nothing else:
The entire background is one flat, uniform, pure chroma-key green (#00FF00) filling every pixel that is not Astra herself. Absolutely nothing else in the frame: no sky, no clouds, no ground, no scenery, no birds, no flowers, no petals, no particles, no energy effects, no magic circles, no galaxy, no floating star sigils, no props, no cast shadow, no glow, no gradient, no vignette, no frame, no text. Do not use any green anywhere on her body, hair, skin or costume.

Keep her head, her extended hand and all fingers completely inside the frame. Clean crisp silhouette edges against the green so she can be cut out as a game cutscene overlay.

Premium Japanese anime mobile-game key art intended for high-resolution pixel-art conversion. Avoid identity drift, a younger face, changed costume, a second slit, extra limbs, fused fingers, broken wrists or duplicated bodies.
```
