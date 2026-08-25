# Ignis 프로덕션 프롬프트

- 생성 모델: `OpenAIGPTImageNodeV2` / `gpt-image-2` / quality `high`
- 마스터 1024x1536 → 도감 1024x1536(edit), 컷씬 1152x2048(edit, 크로마 그린)
- 디자인: 붉은 장발 / 여성형 / 불꽃 무희복 / 연한 갈색 피부 / 홍안 / 화염 롱소드

## 1. MASTER

```text
Create the definitive master reference image for IGNIS, a clearly adult flame goddess for a premium anime fantasy mobile game.

RENDERING STYLE - match this exactly, it is as important as the design:
Clean Japanese anime key-art rendering with soft cel shading, smooth even lighting and large flat readable color areas. A luminous, bright overall value key - she must read as clearly lit even against a dark backdrop. Delicate refined anime facial features with a soft jawline, a small nose and large clear expressive eyes. Crisp confident line work, restrained specular highlights, matte evenly-lit skin, minimal texture noise.
Her complexion is EVEN and calm: the cheeks are the same light warm brown as the rest of her face, with
at most a barely perceptible hint of warmth. No pink or red blush patch on the cheeks, no flushed nose
bridge, no rosy cheek circles, no heated glow on the face.
This must NOT look like a dark painterly semi-realistic illustration: no heavy chiaroscuro, no dramatic rim-light-on-black, no glossy or oiled skin, no photorealistic muscle striation, no gritty texture overlay, no washed-out silhouette lost in shadow.

One single adult woman with a visibly mature refined anime face, tall elegant proportions, long legs, an athletic dancer's build and a fierce confident presence. Fully visible glowing crimson-red irises, a bold half-smile.

Preserve exactly: her mature adult anime face, fully visible glowing crimson-red irises, extremely long red hair falling past her waist that shades from deep crimson roots through bright scarlet to glowing ember-orange tips and lifts at the ends as if caught in heat, her light warm brown skin, and her flame-woven dancer's regalia - a fitted sleeveless bodice of layered fire-silk, a low wrapped hip sash, and long flowing skirt panels split at the sides, fastened with thin blackened-iron rings at the hip and upper arm. The fire-silk reads as solid woven fabric with clean readable shapes; the flame lives only along the hems and trailing edges, never as an all-over blaze that swallows her silhouette. No gold, no crown, no visor, no armor, no cape, no wings, no horns.

WEAPON - a single mythic LONGSWORD, drawn with strict structural accuracy:
A classic European cruciform longsword: one long straight double-edged blade, a straight bar crossguard,
a long leather-wrapped two-hand grip and a heavy rounded pommel. The blade is about three quarters of
the total length and the hilt the remaining quarter. It is a large weapon - held point-down at her hip
with a relaxed arm, the point very nearly reaches the ground.
The BLADE is slender and flat with a shallow fuller running down its centre. Its two edges run straight
and very nearly parallel for most of the blade, converging evenly to a point only over the last stretch.
The blade is NOT on fire. It is HEATED METAL: dark steel that glows from within like iron fresh from
the forge - deep cherry red near the crossguard, brightening to orange and pale yellow-white along the
edges and toward the point, with faint heat shimmer. No licking flames, no fire wreath around the blade.
The hilt is dark iron-bound: a plain straight crossguard with slightly flared ends, a dark leather grip
bound with fine iron wire, and a simple disc pommel. Small sparse glowing rune glyphs are etched along
the fuller and around the crossguard - an ancient mystical alphabet, faint and evenly spaced.
GEOMETRY - the single most important constraint:
The blade is DEAD STRAIGHT. Its two edges are straight lines and the point sits exactly on the blade
axis, centred, not swept to one side. It is NOT curved, bowed, scimitar-shaped, sabre-shaped,
sickle-shaped, wavy, flame-bladed or leaf-bellied, and it must not bend, bow, warp, twist, kink, ripple,
taper unevenly or change width abruptly anywhere along its length.
BLADE, GRIP AND POMMEL SHARE ONE AXIS. Draw the whole sword as a single straight ruler line first, then
hang the crossguard across that line at a right angle. The blade runs straight out of the guard along
that same line and never sits at an angle to the grip.
The CROSSGUARD is one straight bar, perpendicular to the blade and symmetric - both arms the same length
at the same angle. The junction where blade, guard and grip meet is clean and undistorted: no bulge, no
kink, no swelling, no sudden change of direction, no smeared or melted join.
THE ENTIRE WEAPON STAYS OUTSIDE HER SILHOUETTE. From the pommel to the point, every part of the sword is
seen against the open background - it never disappears behind her body, her arm, her hip, her hair or
her flame drapes and then reappear on the other side. There is no hidden segment anywhere. Keep the
whole sword clear of her outline so it reads as one unbroken straight line from end to end. This is
mandatory: an interrupted or bent blade is the failure that keeps happening and it must not happen.
Under foreshortening the near end is larger and the far end smaller, but the sword still reads as
dead straight end to end.

She holds the longsword with her RIGHT HAND ONLY, in a relaxed natural standing grip at about hip height, her arm held slightly out from her body so the weapon sits OUTBOARD of her - entirely off to her right, clear of her torso, hip, hair and skirt. The BLADE POINTS DOWN toward the ground, running straight down and very slightly forward on that same outboard side, the point finishing in open space just above the floor beside her. The complete sword, pommel to point, is visible against the background along its whole length with nothing overlapping it. Her left hand is free and rests naturally. Exactly one sword. No other props.

Full-body 3/4 front view, relaxed ready stance, both hands fully visible, feet fully visible, hair, flame drapes and the entire sword completely inside the frame, generous safe margin around the silhouette. Simple dark ember-red studio background, flat and unlit, with a restrained flat ember halo behind her. The background must stay clearly darker and simpler than she is, and must not bleed into her silhouette.

Premium Japanese anime game character key art, clean shape language, strong readable silhouette, controlled detail density suitable for later conversion into high-resolution pixel art.

Render her as a fully clothed adult heroine in stage regalia. Avoid heavy red cheek blush, a flushed face, a curved or bent blade, an interrupted blade, a distorted hilt junction, a spear, a polearm, a scimitar, a katana, chibi proportions, a youthful child-like face, gold decoration, crown, blindfold, visor, armor, cape, wings, horns, a second weapon, extra character, duplicate body, extra limbs, malformed hands, extra fingers, cropped feet, cropped hair, text, logo, watermark or UI frame.
```

## 2. CODEX (도감)

```text
Using the supplied IGNIS master image as the exact identity, face, body proportion, hairstyle, costume, color palette and prop blueprint, create IGNIS'S FINAL CODEX PORTRAIT. Do not redesign her.

Preserve exactly: her mature adult anime face, fully visible glowing crimson-red irises, extremely long red hair falling past her waist that shades from deep crimson roots through bright scarlet to glowing ember-orange tips and lifts at the ends as if caught in heat, her light warm brown skin, and her flame-woven dancer's regalia - a fitted sleeveless bodice of layered fire-silk, a low wrapped hip sash, and long flowing skirt panels split at the sides, fastened with thin blackened-iron rings at the hip and upper arm. The fire-silk reads as solid woven fabric with clean readable shapes; the flame lives only along the hems and trailing edges, never as an all-over blaze that swallows her silhouette. No gold, no crown, no visor, no armor, no cape, no wings, no horns.

RENDERING STYLE - match this exactly, it is as important as the design:
Clean Japanese anime key-art rendering with soft cel shading, smooth even lighting and large flat readable color areas. A luminous, bright overall value key - she must read as clearly lit even against a dark backdrop. Delicate refined anime facial features with a soft jawline, a small nose and large clear expressive eyes. Crisp confident line work, restrained specular highlights, matte evenly-lit skin, minimal texture noise.
Her complexion is EVEN and calm: the cheeks are the same light warm brown as the rest of her face, with
at most a barely perceptible hint of warmth. No pink or red blush patch on the cheeks, no flushed nose
bridge, no rosy cheek circles, no heated glow on the face.
This must NOT look like a dark painterly semi-realistic illustration: no heavy chiaroscuro, no dramatic rim-light-on-black, no glossy or oiled skin, no photorealistic muscle striation, no gritty texture overlay, no washed-out silhouette lost in shadow.

WEAPON - a single mythic LONGSWORD, drawn with strict structural accuracy:
A classic European cruciform longsword: one long straight double-edged blade, a straight bar crossguard,
a long leather-wrapped two-hand grip and a heavy rounded pommel. The blade is about three quarters of
the total length and the hilt the remaining quarter. It is a large weapon - held point-down at her hip
with a relaxed arm, the point very nearly reaches the ground.
The BLADE is slender and flat with a shallow fuller running down its centre. Its two edges run straight
and very nearly parallel for most of the blade, converging evenly to a point only over the last stretch.
The blade is NOT on fire. It is HEATED METAL: dark steel that glows from within like iron fresh from
the forge - deep cherry red near the crossguard, brightening to orange and pale yellow-white along the
edges and toward the point, with faint heat shimmer. No licking flames, no fire wreath around the blade.
The hilt is dark iron-bound: a plain straight crossguard with slightly flared ends, a dark leather grip
bound with fine iron wire, and a simple disc pommel. Small sparse glowing rune glyphs are etched along
the fuller and around the crossguard - an ancient mystical alphabet, faint and evenly spaced.
GEOMETRY - the single most important constraint:
The blade is DEAD STRAIGHT. Its two edges are straight lines and the point sits exactly on the blade
axis, centred, not swept to one side. It is NOT curved, bowed, scimitar-shaped, sabre-shaped,
sickle-shaped, wavy, flame-bladed or leaf-bellied, and it must not bend, bow, warp, twist, kink, ripple,
taper unevenly or change width abruptly anywhere along its length.
BLADE, GRIP AND POMMEL SHARE ONE AXIS. Draw the whole sword as a single straight ruler line first, then
hang the crossguard across that line at a right angle. The blade runs straight out of the guard along
that same line and never sits at an angle to the grip.
The CROSSGUARD is one straight bar, perpendicular to the blade and symmetric - both arms the same length
at the same angle. The junction where blade, guard and grip meet is clean and undistorted: no bulge, no
kink, no swelling, no sudden change of direction, no smeared or melted join.
THE ENTIRE WEAPON STAYS OUTSIDE HER SILHOUETTE. From the pommel to the point, every part of the sword is
seen against the open background - it never disappears behind her body, her arm, her hip, her hair or
her flame drapes and then reappear on the other side. There is no hidden segment anywhere. Keep the
whole sword clear of her outline so it reads as one unbroken straight line from end to end. This is
mandatory: an interrupted or bent blade is the failure that keeps happening and it must not happen.
Under foreshortening the near end is larger and the far end smaller, but the sword still reads as
dead straight end to end.

Vertical 2:3 full-body composition. Elegant contrapposto and slight 3/4 turn toward the camera, weight on one leg, the longsword held in her RIGHT HAND ONLY at about hip height with the BLADE POINTING DOWN toward the ground. Her right arm is held a little away from her side so the whole sword sits OUTBOARD of her body: the long straight blade runs down and very slightly forward through open space on her right, well clear of her torso, hip, skirt and streaming hair, the point finishing just above the floor beside her. No part of the sword is hidden behind her or behind her hair at any point - the pommel, the grip, the crossguard, the full blade and the point are all continuously visible as one straight unbroken line silhouetted against the darker background. Keep the head, hair, hands, the entire sword from pommel to point, every flame drape and both feet completely inside the frame. Make her face, costume construction and silhouette readable on a mobile-game codex screen.

Use a restrained volcanic sanctuary background with stepped deep-ember and charcoal shapes, a thin dark-iron circular diagram and sparse floating embers, all kept flat, simple and clearly darker than she is. The background must not merge with her hair, her flames or the sword.

Premium anime fantasy mobile-game codex key art intended for high-resolution pixel-art conversion. Clean silhouette, large readable shapes, controlled highlights and limited visual noise.

Strictly preserve the master identity and costume. Avoid heavy red cheek blush, a flushed face, a curved or bent blade, a blade hidden behind her body or hair, a distorted hilt junction, an asymmetric crossguard, a spear, a polearm, a scimitar, a katana, a younger face, different hairstyle, shortened hair, changed costume construction, a second weapon, extra props, malformed hands, cropped body, text, logo, watermark, card border or UI.
```

## 3. ULTIMATE (궁극기 컷씬)

```text
Using the supplied IGNIS reference image as the exact identity - face, mature adult proportions, hairstyle and hair color order, costume construction and color palette - create IGNIS'S ULTIMATE-SKILL CUTSCENE CUT. Do not redesign her.

Preserve exactly: her mature adult anime face, fully visible glowing crimson-red irises, extremely long red hair falling past her waist that shades from deep crimson roots through bright scarlet to glowing ember-orange tips and lifts at the ends as if caught in heat, her light warm brown skin, and her flame-woven dancer's regalia - a fitted sleeveless bodice of layered fire-silk, a low wrapped hip sash, and long flowing skirt panels split at the sides, fastened with thin blackened-iron rings at the hip and upper arm. The fire-silk reads as solid woven fabric with clean readable shapes; the flame lives only along the hems and trailing edges, never as an all-over blaze that swallows her silhouette. No gold, no crown, no visor, no armor, no cape, no wings, no horns.

RENDERING STYLE - match this exactly, it is as important as the design:
Clean Japanese anime key-art rendering with soft cel shading, smooth even lighting and large flat readable color areas. A luminous, bright overall value key - she must read as clearly lit even against a dark backdrop. Delicate refined anime facial features with a soft jawline, a small nose and large clear expressive eyes. Crisp confident line work, restrained specular highlights, matte evenly-lit skin, minimal texture noise.
Her complexion is EVEN and calm: the cheeks are the same light warm brown as the rest of her face, with
at most a barely perceptible hint of warmth. No pink or red blush patch on the cheeks, no flushed nose
bridge, no rosy cheek circles, no heated glow on the face.
This must NOT look like a dark painterly semi-realistic illustration: no heavy chiaroscuro, no dramatic rim-light-on-black, no glossy or oiled skin, no photorealistic muscle striation, no gritty texture overlay, no washed-out silhouette lost in shadow.

WEAPON - a single mythic LONGSWORD, drawn with strict structural accuracy:
A classic European cruciform longsword: one long straight double-edged blade, a straight bar crossguard,
a long leather-wrapped two-hand grip and a heavy rounded pommel. The blade is about three quarters of
the total length and the hilt the remaining quarter. It is a large weapon - held point-down at her hip
with a relaxed arm, the point very nearly reaches the ground.
The BLADE is slender and flat with a shallow fuller running down its centre. Its two edges run straight
and very nearly parallel for most of the blade, converging evenly to a point only over the last stretch.
The blade is NOT on fire. It is HEATED METAL: dark steel that glows from within like iron fresh from
the forge - deep cherry red near the crossguard, brightening to orange and pale yellow-white along the
edges and toward the point, with faint heat shimmer. No licking flames, no fire wreath around the blade.
The hilt is dark iron-bound: a plain straight crossguard with slightly flared ends, a dark leather grip
bound with fine iron wire, and a simple disc pommel. Small sparse glowing rune glyphs are etched along
the fuller and around the crossguard - an ancient mystical alphabet, faint and evenly spaced.
GEOMETRY - the single most important constraint:
The blade is DEAD STRAIGHT. Its two edges are straight lines and the point sits exactly on the blade
axis, centred, not swept to one side. It is NOT curved, bowed, scimitar-shaped, sabre-shaped,
sickle-shaped, wavy, flame-bladed or leaf-bellied, and it must not bend, bow, warp, twist, kink, ripple,
taper unevenly or change width abruptly anywhere along its length.
BLADE, GRIP AND POMMEL SHARE ONE AXIS. Draw the whole sword as a single straight ruler line first, then
hang the crossguard across that line at a right angle. The blade runs straight out of the guard along
that same line and never sits at an angle to the grip.
The CROSSGUARD is one straight bar, perpendicular to the blade and symmetric - both arms the same length
at the same angle. The junction where blade, guard and grip meet is clean and undistorted: no bulge, no
kink, no swelling, no sudden change of direction, no smeared or melted join.
THE ENTIRE WEAPON STAYS OUTSIDE HER SILHOUETTE. From the pommel to the point, every part of the sword is
seen against the open background - it never disappears behind her body, her arm, her hip, her hair or
her flame drapes and then reappear on the other side. There is no hidden segment anywhere. Keep the
whole sword clear of her outline so it reads as one unbroken straight line from end to end. This is
mandatory: an interrupted or bent blade is the failure that keeps happening and it must not happen.
Under foreshortening the near end is larger and the far end smaller, but the sword still reads as
dead straight end to end.

COMPOSITION - match this camera and pose exactly:
A BACK-VIEW cutscene cut, upper body and hips, seen from behind and slightly to her left so the
camera is largely looking at her BACK and the back of her streaming hair. She faces toward the RIGHT
of the frame; her head turns into sharp profile toward that same direction, chin lifted, a fierce
commanding expression - the moment she orders the advance.

She holds the longsword with her RIGHT HAND ONLY and LEVELS IT FORWARD, aiming the point in exactly
the direction she is facing. Her right arm extends forward and the sword lies along the SAME straight
line as her forearm, so forearm, grip and blade read as one continuous line reaching out to the point.

The sword is held OUT AND AWAY from her body on her right side, roughly at shoulder height, so the whole
weapon - pommel, grip, crossguard and the entire blade out to the point - is silhouetted against the
open green with nothing overlapping it anywhere along its length. It must not cross in front of her
torso, must not tuck in toward her spine, and must not vanish behind her body or her hair at any point.
The blade reaches far forward, the point out near the front of the frame. Her hand is closed firmly
around the grip with every finger correct, the crossguard just in front of her fist. Her left arm is free.

Frame her so the entire sword fits inside the frame with a comfortable margin while she still fills the
composition - do not zoom in so far that the point or the pommel is cut off.

Her enormous red hair and the flame drapes of her costume stream in one continuous direction behind
her, long strands separating into ribbons. Airy, weightless, windswept.

CRITICAL - the character and nothing else:
The entire background is one flat, uniform, pure chroma-key green (#00FF00) filling every pixel that is not Ignis herself or her sword. Absolutely nothing else in the frame: no sky, no ground, no scenery, no rocks, no smoke clouds, no scattered particles, no magic circles, no explosion, no floating props, no cast shadow, no background glow, no gradient, no vignette, no frame, no text. Do not use any green anywhere on her body, hair, skin, costume, grip or blade.

Keep her head, her extended hand and the entire sword from point to pommel completely inside the frame. Clean crisp silhouette edges against the green so she can be cut out as a game cutscene overlay.

Premium Japanese anime mobile-game key art intended for high-resolution pixel-art conversion. Avoid heavy red cheek blush, a flushed face, a curved or bent blade, a blade that disappears behind her body or hair, a sword angled inward across her torso, a distorted hilt junction, an asymmetric crossguard, a spear, a polearm, a scimitar, a katana, identity drift, a younger face, changed costume, a second weapon, a bent or warped blade, extra limbs, fused fingers, broken wrists or duplicated bodies.
```
