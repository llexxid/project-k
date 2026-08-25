# -*- coding: utf-8 -*-
"""IGNIS 캐릭터 스펙 — 팔레트 + 3종 프롬프트.

Astra 와 동일한 파이프라인(마스터 1장 → 도감 편집 + 컷씬 편집)을 쓴다.
픽셀 마감 구현은 Astra 쪽 노드 패키지를 그대로 재사용하고, 팔레트만 캐릭터별로 갈아 끼운다.

사용자 지정 디자인 (2026-08-21):
  붉은 장발 / 여성형 / 불꽃으로 이루어진 노출도 높은 무희복 / 연한 갈색 피부 /
  홍안 / 달궈진 쇠의 롱소드(룬 문자 각인)
  → 이전 로스터의 "남성 마왕 이그니스" 설정을 대체한다.
"""

# 32색. Astra 의 인디고/실버 팔레트를 쓰면 캐릭터가 죽으므로 캐릭터 전용으로 새로 짠다.
#   ember 4 + flame ramp 10 + hair 6 + skin 6 + steel 5 + white-hot 1 = 32
IGNIS_PALETTE_32 = [
    # ember / 근접 흑색
    "#0B0709", "#1A0D10", "#2B1216", "#3D1A1C",
    # 불꽃 램프 (어두운 적 → 백열)
    "#5E1F1E", "#7E2A1E", "#9E3A1C", "#C04E17",
    "#DC6A16", "#F08A1C", "#FBA82C", "#FFC64A",
    "#FFE07A", "#FFF3C0",
    # 머리카락 (짙은 크림슨 → 밝은 주홍)
    "#4A0F16", "#6E1620", "#8F1F26", "#B62B2C",
    "#D94435", "#F26A45",
    # 피부 (연한 갈색)
    "#5C3226", "#7E4632", "#A05F41", "#C07E56",
    "#DCA077", "#F0C39A",
    # 검신·손잡이 강철
    "#3A3A44", "#5B5C68", "#86889A", "#B9BCC9", "#E6E9F2",
    # 백열 코어 (룬 글로우 겸용)
    "#FFFFFF",
]


# 도트 마감에서 정리할 색 — 피부 램프 + 광택이 튀는 백열색.
# HD 의 광택은 그대로 두고 도트에서만 반점/얼룩을 흡수시킨다.
# 불꽃·머리카락·강철은 넣지 않는다. 넣으면 룬 글리프 같은 1px 디테일이 날아간다.
# unify(톤 통일) 대상 — 피부 램프 전체 + 백열색.
IGNIS_SKIN = ["#5C3226", "#7E4632", "#A05F41", "#C07E56", "#DCA077", "#F0C39A",
              "#FFF3C0", "#FFFFFF"]

# despeckle(고립 픽셀 제거) 대상 — 밝은 쪽만. 광택이 만드는 반점이 여기서 생긴다.
# 어두운 음영 단계까지 넣으면 의도된 음영 경계 픽셀까지 흡수돼 셰이딩이 뭉갠다.
IGNIS_GLOSS = ["#A05F41", "#C07E56", "#DCA077", "#F0C39A", "#FFF3C0", "#FFFFFF"]


_STYLE = """RENDERING STYLE - match this exactly, it is as important as the design:
Clean Japanese anime key-art rendering with soft cel shading, smooth even lighting and large flat readable color areas. A luminous, bright overall value key - she must read as clearly lit even against a dark backdrop. Delicate refined anime facial features with a soft jawline, a small nose and large clear expressive eyes. Crisp confident line work, restrained specular highlights, matte evenly-lit skin, minimal texture noise.
Her complexion is EVEN and calm: the cheeks are the same light warm brown as the rest of her face, with
at most a barely perceptible hint of warmth. No pink or red blush patch on the cheeks, no flushed nose
bridge, no rosy cheek circles, no heated glow on the face.
This must NOT look like a dark painterly semi-realistic illustration: no heavy chiaroscuro, no dramatic rim-light-on-black, no glossy or oiled skin, no photorealistic muscle striation, no gritty texture overlay, no washed-out silhouette lost in shadow."""

_WEAPON = """WEAPON - a single mythic LONGSWORD, drawn with strict structural accuracy:
A classic European cruciform LONGSWORD - a true two-hand war sword, not an arming sword, not a short
sword and not a broadsword. One long straight double-edged blade, a straight bar crossguard, a long
leather-wrapped grip sized for two hands, and a heavy rounded pommel. The blade is about three quarters
of the total length and the hilt the remaining quarter.
SCALE: the blade alone is LONGER than her arm measured from shoulder to fingertips, and the grip alone
is long enough to take two of her hands side by side. Held point-down at her hip with a relaxed arm the
point reaches the ground. Draw it long and slender - err on the side of too long rather than too short,
and never let it read as a stubby, wide, dagger-like or hand-and-a-half blade.
The BLADE is slender and flat with a shallow fuller running down its centre.
BLADE PROFILE - follow this literally: the blade holds its full width for about SEVEN EIGHTHS of its
length, its two edges running as straight, very nearly parallel lines the whole way down. Only over the
FINAL EIGHTH, close to the tip, do the edges converge into a short, crisp point. Do not start the taper
near the crossguard, do not let the blade narrow gradually along its whole length, and do not draw a
long slow spike - it is a parallel-sided war blade with a short point, not a needle.
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
dead straight end to end."""

_LOCK = """Preserve exactly: her mature adult anime face, fully visible glowing crimson-red irises, extremely long red hair falling past her waist that shades from deep crimson roots through bright scarlet to glowing ember-orange tips and lifts at the ends as if caught in heat, her light warm brown skin, and her flame-woven dancer's regalia - a fitted sleeveless bodice of layered fire-silk, a low wrapped hip sash, and long flowing skirt panels split at the sides, fastened with thin blackened-iron rings at the hip and upper arm. The fire-silk reads as solid woven fabric with clean readable shapes; the flame lives only along the hems and trailing edges, never as an all-over blaze that swallows her silhouette. No gold, no crown, no visor, no armor, no cape, no wings, no horns."""

MASTER = """Create the definitive master reference image for IGNIS, a clearly adult flame goddess for a premium anime fantasy mobile game.

""" + _STYLE + """

One single adult woman with a visibly mature refined anime face, tall elegant proportions, long legs, an athletic dancer's build and a fierce confident presence. Fully visible glowing crimson-red irises, a bold half-smile.

""" + _LOCK + """

""" + _WEAPON + """

She holds the longsword with her RIGHT HAND ONLY, in a relaxed natural standing grip at about hip height, her arm held slightly out from her body so the weapon sits OUTBOARD of her - entirely off to her right, clear of her torso, hip, hair and skirt. The BLADE POINTS DOWN toward the ground, running straight down and very slightly forward on that same outboard side, the point finishing in open space just above the floor beside her. The complete sword, pommel to point, is visible against the background along its whole length with nothing overlapping it. Her left hand is free and rests naturally. Exactly one sword. No other props.

Full-body 3/4 front view, relaxed ready stance, both hands fully visible, feet fully visible, hair, flame drapes and the entire sword completely inside the frame, generous safe margin around the silhouette. Simple dark ember-red studio background, flat and unlit, with a restrained flat ember halo behind her. The background must stay clearly darker and simpler than she is, and must not bleed into her silhouette.

Premium Japanese anime game character key art, clean shape language, strong readable silhouette, controlled detail density suitable for later conversion into high-resolution pixel art.

Render her as a fully clothed adult heroine in stage regalia. Avoid heavy red cheek blush, a flushed face, a curved or bent blade, an interrupted blade, a distorted hilt junction, a spear, a polearm, a scimitar, a katana, chibi proportions, a youthful child-like face, gold decoration, crown, blindfold, visor, armor, cape, wings, horns, a second weapon, extra character, duplicate body, extra limbs, malformed hands, extra fingers, cropped feet, cropped hair, text, logo, watermark or UI frame."""

CODEX = """Using the supplied IGNIS master image as the exact identity, face, body proportion, hairstyle, costume, color palette and prop blueprint, create IGNIS'S FINAL CODEX PORTRAIT. Do not redesign her.

""" + _LOCK + """

""" + _STYLE + """

""" + _WEAPON + """

Vertical 2:3 full-body composition. Elegant contrapposto and slight 3/4 turn toward the camera, weight on one leg, the longsword held in her RIGHT HAND ONLY at about hip height with the BLADE POINTING DOWN toward the ground.

THE SWORD IS PLANTED, THE HAND RESTS ON THE HILT. This is the classic standing portrait pose of a
warrior at ease with a great sword: the point is set on the ground a little forward of her right foot,
the blade stands almost upright and tilts only slightly so the hilt leans in toward her hip, and the
ground takes the sword's weight. Her right arm hangs nearly straight down to it, the elbow softly
unlocked, the shoulder relaxed and level.

HAND POSITION: her right hand sits HIGH on the hilt, at the top of the leather grip directly under the
pommel, so the pommel shows just above the heel of her palm and the grip disappears downward into her
hand. Her hand is at about hip-to-waist height.

HOW THE HAND IS DRAWN - this is the part that keeps going wrong, so follow it literally:
Draw the fingers wrapping AROUND the grip. The grip is a cylinder that passes BEHIND her four fingers
and IN FRONT OF her palm, so the near side of the grip is hidden by the fingers and the far side is
hidden by the palm. Never draw a closed fist first and then add a hilt sticking out above and below it.
The four fingers are four separate, clearly drawn digits with a readable line of knuckles across the
back of the hand and visible creases between them - not a fused mitten, not a smooth lump, not a blur.
Her thumb is clearly separate and relaxed, lying DOWN along the grip toward the crossguard rather than
clamped across her fingers - a relaxed handshake grip, gripping mainly with the middle, ring and little
fingers while the index finger and thumb stay loose.
Her wrist is straight and continues the line of the grip: forearm, wrist and hilt read as one soft
line, with no bend, kink or broken angle at the wrist. The whole hand is rotated to follow the angle of
the sword - the palm plane lies along the grip, not across it.
Exactly five digits, correct proportions, no finger passing through the metal.

The sword sits OUTBOARD of her body, off to her right in open space, well clear of her torso, hip,
skirt and streaming hair.

The long straight blade runs down through open space on her right to the point set on the floor. No part of the sword is hidden behind her or behind her hair at any point - the pommel, the grip, the crossguard, the full blade and the point are all continuously visible as one straight unbroken line silhouetted against the darker background. Keep the head, hair, hands, the entire sword from pommel to point, every flame drape and both feet completely inside the frame. Make her face, costume construction and silhouette readable on a mobile-game codex screen.

Use a restrained volcanic sanctuary background with stepped deep-ember and charcoal shapes, a thin dark-iron circular diagram and sparse floating embers, all kept flat, simple and clearly darker than she is. The background must not merge with her hair, her flames or the sword.

Premium anime fantasy mobile-game codex key art intended for high-resolution pixel-art conversion. Clean silhouette, large readable shapes, controlled highlights and limited visual noise.

Strictly preserve the master identity and costume. Avoid heavy red cheek blush, a flushed face, a curved or bent blade, a blade hidden behind her body or hair, a distorted hilt junction, an asymmetric crossguard, a pencil grip, a pinch grip, fingertips only, an open palm, splayed fingers, a mitten hand, fused or blurred fingers, a shapeless lump for a hand, a clenched fist with the hilt drawn through it, a hand floating off the hilt, a grip passing through the fingers, six fingers, a bent or broken wrist, a sword that looks weightless or floating, a short or stubby blade, a spear, a polearm, a scimitar, a katana, a younger face, different hairstyle, shortened hair, changed costume construction, a second weapon, extra props, malformed hands, cropped body, text, logo, watermark, card border or UI."""

ULTIMATE = """Using the supplied IGNIS reference image as the exact identity - face, mature adult proportions, hairstyle and hair color order, costume construction and color palette - create IGNIS'S ULTIMATE-SKILL CUTSCENE CUT. Do not redesign her.

""" + _LOCK + """

""" + _STYLE + """

""" + _WEAPON + """

COMPOSITION - match this camera and pose exactly:
A BACK-VIEW cutscene cut, upper body and hips, seen from behind and slightly to her left so the
camera is largely looking at her BACK and the back of her streaming hair. She faces toward the RIGHT
of the frame; her head turns into sharp profile toward that same direction, chin lifted, a fierce
commanding expression - the moment she orders the advance.

She holds the longsword with her RIGHT HAND ONLY and LEVELS IT FORWARD, aiming the point in exactly
the direction she is facing.

ARM AND SWORD ARE ONE STRAIGHT LINE. Lay a single ruler from her elbow through her forearm, through her
fist, through the grip and crossguard, and all the way down the blade to the point - every one of those
parts sits exactly on that one line. Her wrist is straight and in line with the forearm: it is NOT bent,
cocked, dropped or angled, and the sword does NOT sit at an angle to her arm. Tilt the whole arm to
whatever angle the composition needs, but the sword always follows the arm exactly.
This must read as a deliberate aim - arm and weapon together form one long arrow pointing at the target,
the way a commander points a blade to order the charge. It must not look like she is merely carrying the
sword or holding it out sideways.

SHOW THE WHOLE SWORD AT ITS FULL LENGTH. The sword is presented BROADSIDE to the camera: we look at
the flat of the blade across its entire length, so the weapon reads at its true size. It is not
foreshortened, not pointing toward or away from the viewer, and not seen edge-on as a thin line. The
blade is the second subject of this shot after her face - give it room and let it dominate the upper
half of the composition.

The sword is held OUT AND AWAY from her body on her right side, roughly at shoulder height, so the whole
weapon - pommel, grip, crossguard and the entire blade out to the point - is silhouetted against the
open green with nothing overlapping it anywhere along its length. It must not cross in front of her
torso, must not tuck in toward her spine, and must not vanish behind her body or her hair at any point,
and her own arm must not cover any part of the blade. Her hand is closed firmly around the grip with
every finger correct, the crossguard just in front of her fist. Her left arm is free.

Frame the shot so the ENTIRE longsword is shown at its full length, pommel to point, spanning at least
three quarters of the frame - the blade alone is longer than her whole torso and clearly outreaches her
extended arm several times over. Leave a clear margin of empty green beyond the point and beyond the
pommel so neither end is cut off or crowded against an edge. The sword must read unmistakably as a long two-hand
war sword: a long slender blade plus a long grip, never a short or stubby one. She may sit lower, smaller
and further back in the frame to make room for the full length of the weapon.

Her red hair and the flame drapes of her costume stream in one continuous direction behind her.
KEEP THE HAIR RESTRAINED. Show it as a small number of long, thick, clearly separated ribbons with
plenty of open green between them - not a huge billowing cloud, not a dense curtain, not a mass that
fills the frame. It stays behind her and close to her back rather than spreading wide, takes up clearly
less room than in a typical windswept shot, and must never compete with the sword or crowd her face.
Airy and weightless, but sparse and readable.

CRITICAL - the character and nothing else:
The entire background is one flat, uniform, pure chroma-key green (#00FF00) filling every pixel that is not Ignis herself or her sword. Absolutely nothing else in the frame: no sky, no ground, no scenery, no rocks, no smoke clouds, no scattered particles, no magic circles, no explosion, no floating props, no cast shadow, no background glow, no gradient, no vignette, no frame, no text. Do not use any green anywhere on her body, hair, skin, costume, grip or blade.

Keep her head, her extended hand and the entire sword from point to pommel completely inside the frame. Clean crisp silhouette edges against the green so she can be cut out as a game cutscene overlay.

Premium Japanese anime mobile-game key art intended for high-resolution pixel-art conversion. Avoid heavy red cheek blush, a flushed face, a curved or bent blade, a blade that disappears behind her body or hair, a sword angled inward across her torso, a distorted hilt junction, an asymmetric crossguard, a foreshortened blade, a blade pointing at the viewer, a blade seen edge-on, a blade partly covered by her arm, a blade angled away from the forearm, a bent or cocked wrist, a short or stubby blade, an arming sword, an oversized cloud of hair, hair filling the frame, hair covering the sword, a cropped point, a cropped pommel, a spear, a polearm, a scimitar, a katana, identity drift, a younger face, changed costume, a second weapon, a bent or warped blade, extra limbs, fused fingers, broken wrists or duplicated bodies."""

# 승인된 마스터에서 무기만 갈아 끼울 때 쓴다.
# 마스터를 t2i 로 다시 뽑으면 의상·신발·배경이 같이 흔들린다 (실측: 롱소드 전환 1차에서
# 보디스가 크로스 스트랩으로 바뀌고 맨발이 하이힐이 됐다). 무기만 바꾸는 요구에는
# 반드시 참조 편집을 쓴다.
MASTER_FROM_REF = """Reproduce the supplied IGNIS reference image EXACTLY as it is, with one single change: replace her weapon.

EVERYTHING except the weapon must stay identical to the reference - the same woman, the same face, the same expression and eye colour, the same hairstyle and hair length and hair colour order, the same body proportions, the same standing pose and the same angle of every limb, the same bare feet with their ankle rings, the same background, the same lighting and the same colour palette.

Her costume in particular must be copied EXACTLY as constructed in the reference: the fitted sleeveless bodice of layered fire-silk covering her torso, the low wrapped hip sash, the long flowing skirt panels split at the sides, and the thin blackened-iron rings at the hip and upper arm. Do not restyle it, do not turn the bodice into thin crossed straps or a bandeau, do not change how much fabric there is, do not add or remove any strap, panel or ring, and do not put shoes, heels, sandals or boots on her - she is barefoot exactly as in the reference.

""" + _WEAPON + """

THE ONE CHANGE: she now holds a LONGSWORD instead of the polearm in the reference. She holds it with her RIGHT HAND ONLY, in the same relaxed natural standing grip at about hip height, her arm held slightly out from her body so the weapon sits OUTBOARD of her - entirely off to her right, clear of her torso, hip, hair and skirt. The BLADE POINTS DOWN toward the ground, running straight down and very slightly forward on that same outboard side, the point finishing in open space just above the floor beside her. The complete sword, pommel to point, is visible against the background along its whole length with nothing overlapping it. Her left hand stays free and rests naturally exactly as in the reference. Exactly one sword. No other props.

Keep the head, hair, hands, the entire sword from pommel to point, every flame drape and both bare feet completely inside the frame.

Avoid heavy red cheek blush, a flushed face, a curved or bent blade, an interrupted blade, a distorted hilt junction, a polearm, a scimitar, a katana, a restyled costume, thin crossed chest straps, a bandeau top, shoes, heels, boots, a changed hairstyle, a younger face, a second weapon, extra props, malformed hands, cropped body, text, logo, watermark or UI frame."""


CODEX_POMMEL = """Using the supplied IGNIS master image as the exact identity, face, body proportion, hairstyle, costume, color palette and prop blueprint, create IGNIS'S FINAL CODEX PORTRAIT. Do not redesign her.

""" + _LOCK + """

""" + _STYLE + """

""" + _WEAPON + """

Vertical 2:3 full-body composition. Elegant contrapposto and slight 3/4 turn toward the camera, weight on one leg, the longsword held in her RIGHT HAND ONLY at about hip height with the BLADE POINTING DOWN toward the ground.

SHE RESTS HER HAND ON TOP OF THE POMMEL - she does NOT wrap her fist around the grip.
The longsword is planted point-down on the ground just in front of her right foot and stands straight
upright, the whole hilt rising to about her hip. Her right arm reaches down to it almost straight, the
elbow softly unlocked, the shoulder relaxed, and she leans a little of her weight onto the sword.

HOW THE HAND IS DRAWN - follow this literally, it is the part that keeps going wrong:
Her right hand is laid OVER THE TOP of the round disc pommel like a hand resting on the knob of a cane.
The heel of her palm caps the pommel from above. Her fingers drape DOWN over the far side of the pommel
and curl loosely under its rim; her thumb comes down over the near side. The back of her hand faces up
and toward the camera, showing a soft, relaxed line of knuckles and clearly separated fingers.
Her hand does NOT close around the leather grip at all: the entire grip below the pommel is left bare,
visible and unobstructed all the way down to the crossguard. Nothing is clenched. This must not read as
a fist, and it must not read as gripping a hammer, a pipe or a bar.
Her wrist is relaxed and drops naturally from the forearm. Exactly five digits, correct proportions.

The sword sits OUTBOARD of her body, off to her right in open space, well clear of her torso, hip,
skirt and streaming hair.

The long straight blade runs down through open space on her right to the point set on the floor. No part of the sword is hidden behind her or behind her hair at any point - the pommel, the grip, the crossguard, the full blade and the point are all continuously visible as one straight unbroken line silhouetted against the darker background. Keep the head, hair, hands, the entire sword from pommel to point, every flame drape and both feet completely inside the frame. Make her face, costume construction and silhouette readable on a mobile-game codex screen.

Use a restrained volcanic sanctuary background with stepped deep-ember and charcoal shapes, a thin dark-iron circular diagram and sparse floating embers, all kept flat, simple and clearly darker than she is. The background must not merge with her hair, her flames or the sword.

Premium anime fantasy mobile-game codex key art intended for high-resolution pixel-art conversion. Clean silhouette, large readable shapes, controlled highlights and limited visual noise.

Strictly preserve the master identity and costume. Avoid a hammer grip, a tight vertical fist, knuckles squared to the camera, a locked straight wrist, a rigid straight line from elbow to blade, heavy red cheek blush, a flushed face, a curved or bent blade, a blade hidden behind her body or hair, a distorted hilt junction, an asymmetric crossguard, a pencil grip, a pinch grip, fingertips only, an open palm, splayed fingers, a mitten hand, fused or blurred fingers, a shapeless lump for a hand, a clenched fist with the hilt drawn through it, a hand floating off the hilt, a grip passing through the fingers, six fingers, a bent or broken wrist, a sword that looks weightless or floating, a short or stubby blade, a spear, a polearm, a scimitar, a katana, a younger face, different hairstyle, shortened hair, changed costume construction, a second weapon, extra props, malformed hands, cropped body, text, logo, watermark, card border or UI."""


CODEX_LEAN = """Using the supplied IGNIS master image as the exact identity, face, body proportion, hairstyle, costume, color palette and prop blueprint, create IGNIS'S FINAL CODEX PORTRAIT. Do not redesign her.

""" + _LOCK + """

""" + _STYLE + """

""" + _WEAPON + """

Vertical 2:3 full-body composition. Elegant contrapposto and slight 3/4 turn toward the camera, weight on one leg, the longsword held in her RIGHT HAND ONLY at about hip height with the BLADE POINTING DOWN toward the ground.

THE SWORD LEANS, THE WRIST TURNS - this is a relaxed diagonal rest, never a stiff vertical hold.
The longsword is planted point-down on the ground WELL OUT to her right and forward of her, so the whole
weapon leans across at a clear diagonal, roughly thirty degrees off vertical, with the hilt rising back
toward her body at about waist height. Because the blade leans and her arm does not, her forearm and the
grip meet at a distinct angle - that angle is the whole point of the pose.

HOW THE HAND IS DRAWN - follow this literally, it is the part that keeps going wrong:
Her right elbow is clearly BENT and held a little away from her ribs, her forearm angling down and out
to meet the leaning hilt. Her wrist is turned so the palm follows the diagonal of the grip while the
forearm keeps its own direction - a visible, natural break at the wrist, never a straight rigid line
from elbow to blade.
Draw the fingers wrapping AROUND the grip: the grip is a cylinder passing BEHIND her four fingers and
IN FRONT OF her palm. Never draw a closed fist first and then add a hilt above and below it. The four
fingers are separate, clearly drawn digits with a readable knuckle line and creases between them - not
a fused mitten, not a smooth lump. Her thumb is separate and relaxed, lying along the grip rather than
clamped across her fingers, and she grips mainly with the middle, ring and little fingers while the
index and thumb stay loose.
This must NOT look like a hammer grip: not a tight vertical fist, not knuckles squared to the camera,
not a locked straight wrist. Exactly five digits, correct proportions, no finger passing through metal.

The sword sits OUTBOARD of her body, off to her right in open space, well clear of her torso, hip,
skirt and streaming hair.

The long straight blade runs down through open space on her right to the point set on the floor. No part of the sword is hidden behind her or behind her hair at any point - the pommel, the grip, the crossguard, the full blade and the point are all continuously visible as one straight unbroken line silhouetted against the darker background. Keep the head, hair, hands, the entire sword from pommel to point, every flame drape and both feet completely inside the frame. Make her face, costume construction and silhouette readable on a mobile-game codex screen.

Use a restrained volcanic sanctuary background with stepped deep-ember and charcoal shapes, a thin dark-iron circular diagram and sparse floating embers, all kept flat, simple and clearly darker than she is. The background must not merge with her hair, her flames or the sword.

Premium anime fantasy mobile-game codex key art intended for high-resolution pixel-art conversion. Clean silhouette, large readable shapes, controlled highlights and limited visual noise.

Strictly preserve the master identity and costume. Avoid a hammer grip, a tight vertical fist, knuckles squared to the camera, a locked straight wrist, a rigid straight line from elbow to blade, heavy red cheek blush, a flushed face, a curved or bent blade, a blade hidden behind her body or hair, a distorted hilt junction, an asymmetric crossguard, a pencil grip, a pinch grip, fingertips only, an open palm, splayed fingers, a mitten hand, fused or blurred fingers, a shapeless lump for a hand, a clenched fist with the hilt drawn through it, a hand floating off the hilt, a grip passing through the fingers, six fingers, a bent or broken wrist, a sword that looks weightless or floating, a short or stubby blade, a spear, a polearm, a scimitar, a katana, a younger face, different hairstyle, shortened hair, changed costume construction, a second weapon, extra props, malformed hands, cropped body, text, logo, watermark, card border or UI."""


CODEX_HANG = """Using the supplied IGNIS master image as the exact identity, face, body proportion, hairstyle, costume, color palette and prop blueprint, create IGNIS'S FINAL CODEX PORTRAIT. Do not redesign her.

""" + _LOCK + """

""" + _STYLE + """

""" + _WEAPON + """

Vertical 2:3 full-body composition. Elegant contrapposto and slight 3/4 turn toward the camera, weight on one leg, the longsword held in her RIGHT HAND ONLY at about hip height with the BLADE POINTING DOWN toward the ground.

SHE SIMPLY LETS THE SWORD HANG FROM HER HAND, exactly the way she holds it in her ultimate cut -
same hand, same closed grip - only now the arm is relaxed and the weapon hangs down at her side.

ARM AND SWORD ARE ONE STRAIGHT LINE. Her right arm hangs down along her right side, held a little away
from her body, the shoulder dropped and the elbow softly unlocked. Lay a single ruler from her elbow
down through her forearm, through her fist, through the grip and the crossguard, and on down the blade
to the point: every one of those parts sits exactly on that one line. Her wrist is STRAIGHT and
continues the forearm - it is not bent, cocked, dropped or angled to keep the blade vertical, and the
sword never sits at an angle to her arm. The whole limb plus weapon reads as one long relaxed line
falling from her shoulder to the floor.
Because her arm hangs naturally rather than straight down, that line leans a few degrees, so the blade
points down and slightly forward and the point comes to rest just at the ground a little in front of
her right foot. Let the line lean with the arm - never twist the wrist to force the blade upright.

HOW THE HAND IS DRAWN - copy the hand from the ultimate cut:
Her hand is closed around the leather grip with every finger correct, the pommel showing just above her
fist and the crossguard just below it. Draw the fingers wrapping AROUND the grip: the grip is a cylinder
passing BEHIND her four fingers and IN FRONT OF her palm. Never draw a closed fist first and then add a
hilt above and below it. The four fingers are separate, clearly drawn digits with a readable knuckle
line and creases between them - not a fused mitten, not a smooth lump, not a blur. Her thumb is separate
and relaxed, lying along the grip rather than clamped across her fingers.
Show the hand from its side, the way it reads in the ultimate cut, so the wrap around the grip is fully
legible - not from straight above or straight behind where the fingers collapse into a shape.
The grip is relaxed, not clenched: she is carrying the sword, not squeezing it.
Exactly five digits, correct proportions, no finger passing through the metal.

The sword sits OUTBOARD of her body, off to her right in open space, well clear of her torso, hip,
skirt and streaming hair.

The long straight blade runs down through open space on her right to the point set on the floor. No part of the sword is hidden behind her or behind her hair at any point - the pommel, the grip, the crossguard, the full blade and the point are all continuously visible as one straight unbroken line silhouetted against the darker background. Keep the head, hair, hands, the entire sword from pommel to point, every flame drape and both feet completely inside the frame. Make her face, costume construction and silhouette readable on a mobile-game codex screen.

Use a restrained volcanic sanctuary background with stepped deep-ember and charcoal shapes, a thin dark-iron circular diagram and sparse floating embers, all kept flat, simple and clearly darker than she is. The background must not merge with her hair, her flames or the sword.

Premium anime fantasy mobile-game codex key art intended for high-resolution pixel-art conversion. Clean silhouette, large readable shapes, controlled highlights and limited visual noise.

Strictly preserve the master identity and costume. Avoid a hammer grip, a tight vertical fist, knuckles squared to the camera, a locked straight wrist, a rigid straight line from elbow to blade, heavy red cheek blush, a flushed face, a curved or bent blade, a blade hidden behind her body or hair, a distorted hilt junction, an asymmetric crossguard, a pencil grip, a pinch grip, fingertips only, an open palm, splayed fingers, a mitten hand, fused or blurred fingers, a shapeless lump for a hand, a clenched fist with the hilt drawn through it, a hand floating off the hilt, a grip passing through the fingers, six fingers, a bent or broken wrist, a sword that looks weightless or floating, a short or stubby blade, a spear, a polearm, a scimitar, a katana, a younger face, different hairstyle, shortened hair, changed costume construction, a second weapon, extra props, malformed hands, cropped body, text, logo, watermark, card border or UI."""

# 확정된 도감에서 손만 고칠 때. 다른 것은 아무것도 건드리지 않는다.
CODEX_HANDFIX = """Reproduce the supplied image EXACTLY as it is. This is a targeted retouch, not a redraw: change ONE thing only - the way her right hand grips the sword.

EVERYTHING ELSE IS UNCHANGED AND MUST BE COPIED PRECISELY: the same woman, the same face, expression and eye colour, the same hairstyle and every strand of hair, the same costume with every drape, strap and iron ring, the same bare feet and ankle rings, the same standing pose and the same angle of every limb, the same position of her arm and shoulder, the same background, the same lighting and the same colour palette.
THE SWORD DOES NOT MOVE. Its position, its angle, its length, its blade, its glowing runes, its crossguard and its pommel all stay exactly where and as they are. Only the fingers and hand around the grip change.

THE FIX - how her hand must grip the leather grip:
We are looking at her from the front, so we see the BACK of her right hand. Her hand closes around the grip the way a hand closes around a vertical bar held at the hip.
Her four fingers wrap from the far side of the grip AROUND to the near side, and their curled middle segments cross IN FRONT OF the grip, toward the camera - so along the length of her fist the grip is HIDDEN BEHIND HER FINGERS. The fingertips tuck back in toward her palm. The four fingers are four separate, clearly drawn digits stacked in a row, with visible creases between them and a readable curved row of knuckles across the back of her hand.
Her thumb comes over the top of the grip and lies down across the side of her index finger, angled along the grip - relaxed, not clamped.
Above her fist a short stub of bare grip and the whole pommel are visible; below her fist the grip continues down to the crossguard. Between those two, the grip is behind her fingers and not drawn as a bare cylinder passing through an empty ring of fingers.
The grip is relaxed, not clenched white-knuckled - she is carrying the sword, not squeezing it.
Her wrist stays exactly as it is: straight, continuing the line of her forearm down into the sword.

It must NOT look like a fist drawn first with a hilt added above and below it, and it must NOT look like a hand gripping a hammer. Do not open the hand, do not rest it on the pommel, do not add a second hand.

Exactly five digits, correct human proportions, no extra or missing fingers, no finger passing through the metal, no fused mitten shape, no smooth featureless lump.

Match the existing rendering exactly - the same clean anime cel shading, the same line weight, the same skin tone and shadow colours - so the retouched hand is indistinguishable in style from the rest of the image.

Avoid changing the pose, the sword, the costume, the hair, the face, the background or the palette; avoid a hammer grip, an open palm, a pencil grip, fingertips only, splayed fingers, six fingers, a bent wrist, text, logo or watermark."""

# 확정 도감을 참조해 팔과 검의 배치만 고친다 (손목 꺾임 제거).
CODEX_ARMLINE = """Reproduce the supplied image, keeping her identity and everything around her unchanged, and fix ONE thing: the placement of her right arm and her sword, so that her wrist is no longer bent.

UNCHANGED AND COPIED PRECISELY: the same woman, the same face, expression and eye colour, the same hairstyle and hair colour order, the same flame-woven costume with every drape, strap and iron ring, the same bare feet and ankle rings, the same left arm, the same background, the same lighting and the same colour palette. The sword itself is the same sword - same length, same slender straight blade, same glowing runes, same crossguard, same disc pommel. Only where the arm and the sword sit in the picture changes.

THE FIX - ONE UNBROKEN VERTICAL LINE FROM SHOULDER TO POINT:
Her right arm hangs straight DOWN along her side, relaxed and close to her body: the upper arm falls vertically beside her ribs, the elbow is only softly bent, and the forearm continues straight down. Her hand comes to rest at about hip height, just outside her right thigh - she is NOT reaching out sideways for the sword.
The sword hangs DIRECTLY BELOW her fist. The pommel sits just above her hand, the grip runs vertically down out of her fist, and the blade continues straight down close alongside her right leg to the floor beside her right foot.
Lay a single ruler from her shoulder down through her upper arm, her forearm, her wrist, her fist, the grip, the crossguard and the blade to the point: every one of those sits on that one near-vertical line. HER WRIST IS PERFECTLY STRAIGHT - it is not bent, cocked, tilted or deviated, and the grip is never at an angle to her forearm. This straight wrist is the entire purpose of the change.
The line may lean a few degrees outward as an arm naturally hangs, but arm and sword lean TOGETHER by the same amount. Never tilt one without the other.

BRING THE SWORD IN CLOSE. It hangs just outside her right hip and thigh, near her leg, not out in open space away from her body. It may overlap the edge of her skirt drape. It only must not disappear behind her body and reappear on the other side - the whole sword stays readable from pommel to point.

THE HAND, seen from the front so we see the BACK of it:
Her four fingers wrap from the far side of the grip around to the near side, their curled middle segments crossing IN FRONT of the grip toward the camera, so along her fist the grip is hidden behind her fingers. The fingertips tuck back toward her palm. The four fingers are separate digits stacked in a row with creases between them and a readable curved row of knuckles across the back of the hand. Her thumb comes over the top of the grip and lies across the side of her index finger. Above her fist a short stub of bare grip and the whole pommel show; below her fist the grip runs down to the crossguard. Relaxed, not clenched.

Match the existing rendering exactly - the same clean anime cel shading, the same line weight, the same skin tone and shadow colours.

Exactly five digits, correct human proportions. Avoid a bent, cocked or broken wrist, an arm reaching out sideways, a sword floating away from her body, a grip at an angle to the forearm, a hammer grip, an open palm, splayed fingers, six fingers, a curved or bent blade, a changed face, hairstyle, costume, background or palette, text, logo or watermark."""


# 마스크 인페인팅 전용: 아래팔만 다시 그려 손목을 편다. 손과 검은 건드리지 않는다.
CODEX_WRISTFIX = """Redraw only the masked area of this illustration. Everything outside the mask is already correct and must be matched seamlessly at the mask border.

WHAT IS WRONG: her right wrist is broken. Her forearm currently comes down at a shallow diagonal from the upper right while the sword's grip hangs vertically, so the hand is cocked sharply against the forearm. A real wrist does not bend like that.

THE FIX: redraw her right forearm so that it descends STEEPLY and arrives at the grip IN LINE with it. Lay a ruler along the sword's grip and continue that same line upward through her wrist and forearm - the forearm must follow that line. Her elbow moves down and inward to make this possible, and the forearm reads shorter and more foreshortened, which is correct for an arm hanging beside the body. Her upper arm still comes from her shoulder and the join at the elbow stays natural.
HER WRIST IS STRAIGHT: forearm, wrist and grip form one continuous line with no bend, no cock, no kink and no sharp angle. This is the entire purpose of the edit.

KEEP HER HAND AS IT IS. The grip of the sword, the way her fingers wrap around it, the pommel and the position of her fist all stay exactly where they are - only the forearm above the hand is re-drawn, plus whatever hair the change reveals or covers. Do not move the sword. Do not move her fist. Do not reshape the fingers.

Where the mask crosses her hair, continue the existing strands so the repaint is invisible.

Match the surrounding art exactly: the same clean anime cel shading, the same line weight, the same skin tone, the same shadow and highlight colours, the same level of detail. The retouched forearm must be indistinguishable in style from the rest of the painting.

Avoid a bent, cocked or broken wrist, a rubbery or boneless forearm, a doubled or missing elbow, a second arm, changed fingers, a moved sword, a visible seam at the mask edge, text, logo or watermark."""

# 확정 도감에서 파지 위치와 검 비례만 손본다. 팔·손목·손 모양은 절대 건드리지 않는다.
CODEX_GRIPHILT = """Reproduce the supplied image, keeping everything about it, and make only these adjustments to the sword and where her hand sits on it.

DO NOT TOUCH THE ARM. Her right arm, her wrist and her hand keep exactly the shape, angle and position they already have. The forearm descends steeply and runs in one straight line into the grip, and THE WRIST STAYS PERFECTLY STRAIGHT - no bend, no cock, no kink. Her fingers keep the same wrap around the grip, the same separated digits, the same knuckle line, the same thumb. Her fist stays exactly where it is on the canvas. This is already correct and must not be redrawn differently.

CHANGE 1 - SHE GRIPS NEARER THE HILT. At the moment she looks like she is holding the sword by the very end of the grip. Slide the SWORD upward through her closed hand so that her fist now sits LOW on the grip, close above the crossguard, with only a short gap between the bottom of her fist and the guard. Above her fist a longer run of bare leather grip and the whole disc pommel are now clearly visible. The hand does not move - the sword moves through it.

CHANGE 2 - THE SWORD IS SLIGHTLY LONGER. Lengthen the whole weapon a little, so that even after sliding up through her hand the point still reaches the ground beside her foot. It should read as a long two-hand war sword.

CHANGE 3 - THE BLADE TAPERS LATE. The blade holds its full width for about seven eighths of its length, its two edges running as straight, very nearly parallel lines all the way down, and converges into a short crisp point only over the final eighth near the tip. It must not narrow gradually from the crossguard, and it must not read as a long slow spike.

THE BLADE IS DEAD STRAIGHT. Lay a ruler along it: both edges are straight lines, the fuller runs straight down the centre, and the point sits exactly on the blade axis. No bow, no bend, no warp, no twist, no ripple, no S-curve, no widening or narrowing anywhere except the final taper. The crossguard stays one straight bar, perpendicular to the blade, both arms the same length at the same angle. The join where blade, guard and grip meet stays clean - no bulge, no kink, no smear.

EVERYTHING ELSE IS UNCHANGED: the same woman, face, expression, eye colour, hairstyle, costume with every drape and iron ring, bare feet and ankle rings, her left arm, the standing pose, the background, the lighting, the palette, and the sword's own design - the same dark steel glowing like heated iron, the same glowing rune glyphs, the same crossguard and disc pommel.

Match the existing rendering exactly - the same clean anime cel shading, line weight, skin tone and shadow colours.

Avoid a bent, cocked or broken wrist, a redrawn or reshaped hand, a hand still at the very end of the grip, a curved, bowed, warped or wavy blade, a blade that tapers from the guard, a needle-thin point, an asymmetric crossguard, a distorted hilt junction, a changed face, hairstyle, costume, background or palette, text, logo or watermark."""


# 마스크 인페인팅 전용: 손만 다시 그려 v19 의 자연스러운 손으로 되돌린다.
# v20 에서 손이 작아지고 납작해졌다 — 손등이 밋밋한 살덩이가 되고 손가락 마디의 부피가
# 사라졌다. 위치(크로스가드 바로 위)와 검은 그대로 두고 손의 '구조'만 복원한다.
CODEX_HANDROLLBACK = """Redraw only the masked area of this illustration - her right hand on the sword grip. Everything outside the mask is already correct and must be matched seamlessly at the mask border.

WHAT IS WRONG: the hand has gone flat and small. The back of the hand reads as a plain slab of skin with no structure, the fingers have lost their volume, and the whole hand looks undersized next to the grip.

THE FIX - give the hand back its weight and structure:
The hand is a solid three-dimensional mass, drawn slightly larger so it sits convincingly around the grip - about as deep as the grip is wide, not a thin flap laid against it.
We see it from the outside, so the BACK OF THE HAND faces us: a broad plane running from the wrist up to the knuckles, with a clearly drawn curved row of KNUCKLES across the top of the fist and the soft tendon ridges running back toward the wrist.
The four fingers curl around the near face of the grip as four SEPARATE, ROUNDED segments stacked in a row - each finger has its own rounded volume and a visible crease shadow between it and the next. They are not flat sausages, not a fused mitten, and not a smooth blur.
The thumb comes over the top of the grip and lies down along the near side as a distinct digit with its own volume, joined at a full, rounded base-of-thumb muscle so the hand does not pinch in at the wrist.
The wrist flows smoothly into the hand with no narrowing or kink.
The grip is relaxed, not clenched white-knuckled.

DO NOT MOVE ANYTHING. The hand stays exactly where it is on the grip - low, just above the crossguard, with the bare grip and pommel showing above it. The sword does not move: same grip, same crossguard, same blade, same runes. Her forearm keeps its angle and the wrist stays straight, in line with the grip.

Match the surrounding art exactly: the same clean anime cel shading, the same line weight, the same skin tone, the same shadow and highlight colours. The repainted hand must be indistinguishable in style from the rest of the painting, with no seam at the mask edge.

Exactly five digits, correct human proportions. Avoid a flat or slab-like back of the hand, a shrunken hand, fused or blurred fingers, a mitten, a pinched wrist, splayed fingers, six fingers, a moved sword, a bent wrist, a visible mask seam, text, logo or watermark."""


# 마스크 인페인팅 전용: 손을 레퍼런스(칼을 옆구리에 늘어뜨린 인체 드로잉)의 파지와 맞춘다.
CODEX_HANDREF = """Redraw only the masked area - her right hand closed on the sword grip. Everything outside the mask is correct and must be matched seamlessly at the mask border.

Draw the hand exactly the way a life-drawing reference shows a hand carrying a sword at the hip, seen from outside the body:

THE BACK OF THE HAND FACES US and is tipped slightly toward the viewer, so we read the full width of the hand from the wrist to the knuckles. Across the top of the fist runs a clear, slightly arched ROW OF FOUR KNUCKLES, the index knuckle highest and largest, stepping down toward the little finger. Soft tendon ridges run back from the knuckles toward the wrist.
THE FOUR FINGERS wrap around the far side of the grip and curl back toward the palm, and we see their middle segments stacked in a neat descending row on the near face of the grip. Each finger is a separate rounded digit with a crease shadow between it and the next; the index finger sits highest and the little finger lowest, and each is slightly shorter as it goes down.
THE THUMB comes across the TOP of the fist and lies over the side of the index finger, its tip pointing down along the grip. There is a small visible hollow between the thumb and the index knuckle, and the base of the thumb is a full rounded muscle so the hand does not pinch in at the wrist.
The fist is closed but RELAXED - she is carrying the weight, not squeezing. The hand has real depth, about as thick as the grip is wide.

DO NOT MOVE ANYTHING ELSE. The hand stays exactly where it is on the grip - low, just above the crossguard, with bare grip and pommel showing above it. The sword does not move: same grip, crossguard, blade and runes. The forearm keeps its angle and the wrist stays straight, in line with the grip.

Match the surrounding art exactly: same clean anime cel shading, line weight, skin tone, shadow and highlight colours, with no seam at the mask edge.

Exactly five digits, correct human proportions. Avoid a flat slab back of the hand, a shrunken hand, fused or blurred fingers, a mitten, a pinched wrist, a hidden thumb, splayed fingers, six fingers, a moved sword, a bent wrist, a visible mask seam, text, logo or watermark."""


# 레퍼런스 인체 드로잉의 자세를 그대로 옮긴 대안 도감.
CODEX_REFPOSE = """Using the supplied IGNIS image as the exact identity - same face, same hairstyle and hair colour order, same flame-woven costume with every drape and iron ring, same bare feet and ankle rings, same sword, same palette, same background, same rendering style - redraw her in a NEW STANDING POSE. Do not redesign anything; only the pose changes.

THE POSE, taken from a life-drawing reference:
She stands turned roughly three-quarters AWAY from the camera, so we see her back and the long line of her spine, her far shoulder receding. Her head turns back over her near shoulder toward the viewer in a clean three-quarter view, chin slightly lowered, her gaze level and composed with a faint confident half-smile.
Her weight is on the near leg, which is straight and carries the body. The far leg is extended out and back, the knee soft and the foot resting lightly on the ground, so the legs make a long open stride rather than a symmetric stance. The hips tilt with the weight and the shoulders counter-rotate, giving a relaxed S-curve through the torso.
Her RIGHT ARM hangs straight down along her side, the elbow only softly bent, the shoulder dropped. Her right hand is closed around the sword grip at about hip height, low on the grip just above the crossguard, with bare grip and pommel showing above her fist. The forearm, wrist and grip run in ONE STRAIGHT LINE - the wrist is not bent or cocked.
The sword hangs down and slightly FORWARD from her hand, close beside her leg, its point resting near the ground in front of her feet. Blade, crossguard and grip stay on one straight axis.
Her LEFT ARM hangs relaxed and free on the far side.

THE HAND: we see the back of it, with a clear arched row of four knuckles, four separate rounded fingers wrapping the grip in a descending row with creases between them, and the thumb laid across the top over the index finger. Relaxed, not clenched. Exactly five digits.

THE BLADE IS DEAD STRAIGHT: both edges are straight lines, the blade holds its full width for about seven eighths of its length and converges to a short crisp point only over the final eighth. No bow, bend, warp, twist or ripple. The crossguard is one straight symmetric bar perpendicular to the blade.

Keep her head, hair, both hands, the entire sword from pommel to point, every flame drape and both feet completely inside the frame, with a comfortable margin. Vertical 2:3 full-body composition.

Avoid identity drift, a changed face, hairstyle, costume, palette or background, a front-facing symmetric stance, a bent or cocked wrist, a hammer grip, a flat or mitten hand, six fingers, a curved or bent blade, malformed hands or feet, cropped limbs, text, logo or watermark."""


# 옷과 머리카락이 같은 붉은 필라멘트 질감이라 서로 녹아든다. 옷만 '천'으로 다시 읽히게 한다.
CODEX_CLOTH = """Reproduce the supplied image exactly, changing only ONE thing: how her costume fabric is rendered, so that the cloth separates clearly from her hair.

THE PROBLEM: her flame-woven regalia and her red hair are drawn with the same fibrous, strand-by-strand red texture, so where they overlap the eye cannot tell cloth from hair and the whole silhouette turns into one red mass.

THE FIX - MAKE THE COSTUME READ AS WOVEN CLOTH:
Render the bodice, the hip sash and the long skirt panels as real fabric: broad, smooth, continuous surfaces of woven silk with clean flat colour areas, crisp folds and a clear outer edge. Each panel is one piece of cloth with a defined hem you can follow from top to bottom, catching the light along the ridge of a fold and turning to shadow in the trough. Its surface is smooth and slightly matte, like heavy dyed silk - not made of separate threads.
Remove the strand-by-strand filament texture from the fabric. No hair-like fibres, no braided or woven rope look, no thousands of thin overlapping ribbons inside the cloth. Fold lines are few, long and confident, not a mesh of small strokes.
Give the cloth a slightly DEEPER, more saturated crimson than the hair and hold it in larger flat areas, so it separates by both value and shape. Keep the small licks of live flame only along the hems and trailing edges, as a thin bright border on the cloth, never spread across the whole panel.
Where a skirt panel passes in front of or behind her hair, draw a clean unbroken silhouette edge with a clear tonal step, so the boundary between cloth and hair is instantly readable.

HER HAIR IS UNCHANGED and keeps its strand texture: long flowing locks of separate red-to-ember strands, exactly the same shape, length, parting and colour order as in the reference.

EVERYTHING ELSE IS UNCHANGED: the same woman, face, expression, eye colour, hairstyle and hair silhouette, the same pose and the same angle of every limb, the same hand on the sword, the same sword with its blade, runes, crossguard and pommel, the same bare feet and ankle rings, the same background, lighting and palette, the same costume CONSTRUCTION - the same bodice, the same low hip sash, the same skirt panels split at the sides, the same blackened-iron rings. Only the surface rendering of the cloth changes; do not restyle, redesign, add or remove any garment piece, and do not change how much of her is covered.

Match the existing rendering: the same clean anime cel shading, line weight, skin tone and shadow colours.

Avoid a changed pose, a changed hand or wrist, a moved or reshaped sword, a changed hairstyle, a redesigned or restyled costume, more or less coverage, a fibrous hair-like fabric, a braided or rope-like fabric, cloth that dissolves into the hair, text, logo or watermark."""


# 대안 자세 도감의 손을 레퍼런스 인체 드로잉의 파지로 맞춘다 (마스크 인페인팅 전용).
CODEX_HANDREF2 = """Redraw only the masked area - her right hand closed on the sword grip. Everything outside the mask is correct and must be matched seamlessly at the mask border.

WHAT IS WRONG NOW: the hand shows only the back of the hand and a row of finger segments. THE THUMB IS MISSING - it is nowhere to be seen - so the grip does not read as a real hand closing around a cylinder.

DRAW IT LIKE THE LIFE-DRAWING REFERENCE:
We look at the hand from its THUMB SIDE, slightly from behind, as it hangs at her hip.
THE THUMB IS THE MOST VISIBLE PART. It comes up and over the near face of the grip and lies diagonally ACROSS the middle joint of the index finger, its rounded tip pointing down-forward along the grip. Its nail catches a small highlight. Behind it, the base-of-thumb muscle is a full rounded mass that fills the web between the thumb and the index finger, so the hand reads thick and solid where it meets the wrist.
THE FOUR FINGERS wrap around the FAR side of the grip and curl back toward the palm, so from this angle we see only their middle joints stepping down the near face in a short, tightly tucked row beneath the thumb - the index highest, the little finger lowest, each a little shorter. They are compact and close together with crisp crease shadows between them, not long or splayed.
THE FIST IS COMPACT - a closed, economical grip, not a big swollen mass. It should look like a hand that has carried this sword all day.
The knuckle line runs along the back edge of the fist, away from the viewer, so only a hint of it shows.
The wrist is STRAIGHT, continuing the forearm straight into the grip with no bend or kink.

DO NOT MOVE ANYTHING ELSE. The hand stays where it is on the grip - low, just above the crossguard, with bare grip and the disc pommel showing above it. The sword does not move: same grip, crossguard, blade, runes and pommel. The forearm keeps its exact angle and position.

Match the surrounding art exactly: same clean anime cel shading, line weight, skin tone, shadow and highlight colours, no seam at the mask edge.

Exactly five digits, correct human proportions. Avoid a hidden or missing thumb, a thumb fused to the fingers, a swollen or oversized fist, splayed or long fingers, a flat slab back of the hand, a mitten, a pinched wrist, six fingers, a moved sword, a bent wrist, a visible mask seam, text, logo or watermark."""


_HAIR_FIX = """HER HAIR MUST READ AS FLOWING FIRE, NOT AS TANGLED HAIR.
Right now it is a dense, frizzy, knotted mass of many small overlapping curls that reads as messy hair.
Redraw it as a slow tongue of flame given the shape of hair:
Group it into a SMALL NUMBER OF BROAD, SMOOTH LOCKS - long sweeping ribbons of hair, each one wide and
confident, each following one long unbroken S-curve from the scalp to its tip. Think of the way a flame
leans and streams in a draft: one dominant direction, gentle parallel curves, generous open space
between the locks.
Every lock flows in the SAME overall direction, so the whole mass reads as one current. Where locks
overlap they lie cleanly over one another with a crisp edge and a clear tonal step, never merging into
an undifferentiated red field.
The tips are the flame part: each lock tapers and its last stretch lifts and curls upward into a soft
licking point, brightening from scarlet to glowing ember-orange as it rises, with a few fine sparks
leaving the tips.
Remove the frizz: no dense mesh of tiny curls, no knots, no crinkled or matted texture, no thousands of
thin strands crossing each other, no wiry flyaways. Interior detail is a few long parallel highlight
lines following each lock, not a scribble.
Keep the same silhouette, the same length, the same parting and the same colour order - deep crimson at
the roots through bright scarlet to glowing ember-orange at the tips."""


# 확정 도감의 머리카락만 흐르는 불길로 다시 그린다.
CODEX_HAIR = """Reproduce the supplied image exactly, changing only ONE thing: how her hair is drawn.

""" + _HAIR_FIX + """

EVERYTHING ELSE IS UNCHANGED: the same woman, face, expression and eye colour, the same pose and the same angle of every limb, the same hand closed on the sword grip with the same fingers and thumb, the same sword with its blade, runes, crossguard and pommel in the same position and at the same angle, the same fabric costume with every panel, fold, hem and iron ring, the same bare feet and ankle rings, the same background, lighting and palette.

The costume stays SMOOTH WOVEN CLOTH and must remain clearly distinct from the hair - broad flat silk panels with few long folds and a thin flame hem. Do not give the fabric a hair-like or fibrous texture, and do not let hair and cloth blend together.

Match the existing rendering: the same clean anime cel shading, line weight, skin tone and shadow colours.

Avoid a changed pose, a changed hand, wrist or finger count, a moved or re-angled sword, a changed costume, a changed face or hairstyle silhouette, frizzy or matted hair, text, logo or watermark."""


# 컷씬의 머리카락만 흐르는 불길로 다시 그린다.
ULTIMATE_HAIR = """Reproduce the supplied image exactly, changing only ONE thing: how her hair is drawn.

""" + _HAIR_FIX + """

Because she is moving, the whole hair mass streams behind her in one continuous direction - a small number of long, broad, clearly separated ribbons with plenty of open background between them. Keep it restrained: it must not become a large billowing cloud, must not spread wide, and must not crowd her face or the sword.

EVERYTHING ELSE IS UNCHANGED: the same woman, face, expression and eye colour, the same pose and the same angle of every limb, the same hand closed on the sword grip, the same sword with its blade, runes, crossguard and pommel in the same position and at the same angle, the same costume, the same bare feet and ankle rings.

THE BACKGROUND STAYS EXACTLY AS IT IS: one flat, uniform, pure chroma-key green (#00FF00) filling every pixel that is not Ignis herself or her sword. Nothing else in the frame - no scenery, no smoke, no particles, no glow, no gradient, no shadow. Do not use any green anywhere on her body, hair, skin, costume or sword.

Match the existing rendering: the same clean anime cel shading, line weight, skin tone and shadow colours.

Avoid a changed pose, a changed hand, wrist or finger count, a moved or re-angled sword, a changed costume, frizzy or matted hair, an oversized cloud of hair, a background that is not flat green, text, logo or watermark."""


# 마스크 인페인팅 전용: 크로스가드의 유령 손 제거 + 검을 팔 축에 맞춰 살짝 우측으로.
CODEX_SWORDFIX = """Redraw only the masked area of this illustration. Everything outside the mask is correct and must be matched seamlessly at the mask border.

FIX 1 - DELETE THE SECOND HAND. There is a duplicate hand gripping the blade just below the crossguard. It should not exist. She holds the sword with ONE HAND ONLY, and that hand is ABOVE the crossguard, on the leather grip, outside this mask. Inside the mask, below the crossguard, there is nothing but the flat of the blade against the background - no hand, no fingers, no thumb, no knuckles, no wrist, no skin of any kind. Draw the crossguard as a clean, unobstructed bar with nothing wrapped around it, and let the blade run down from it completely bare.

FIX 2 - TILT THE SWORD SLIGHTLY TO THE RIGHT. At the moment the blade hangs a little too upright, so it does not line up with her hand and forearm and there is a visible kink at the crossguard. Rotate the whole sword a few degrees CLOCKWISE - the point swings to the viewer's right and the blade leans a little further out from her leg. It is a SMALL adjustment, not a dramatic new angle.
After the change, forearm, fist, grip, crossguard and blade must all sit on ONE STRAIGHT AXIS: lay a ruler along her forearm and continue it downward - the crossguard sits square across that line and the blade runs straight down it to the point. No kink, no angle change where the blade leaves the guard.
The point still comes down to the ground.

THE BLADE ITSELF IS UNCHANGED IN DESIGN: the same long slender heated-steel blade glowing cherry red near the guard and brightening toward the edges and point, the same glowing rune glyphs down the fuller, the same straight symmetric crossguard with its flared ends and stud detail. It is DEAD STRAIGHT - both edges straight and very nearly parallel, holding full width for about seven eighths of its length and converging to a short crisp point only at the end. No bow, bend, warp, twist or ripple.

EVERYTHING ELSE INSIDE THE MASK IS REDRAWN EXACTLY AS IT IS: the same skirt fabric with the same folds and flame hem, the same dark floor with its glowing lava cracks, the same background. Do not change her leg, her foot, her skirt or the ground.

Match the surrounding art exactly: the same clean anime cel shading, line weight and colours, with no seam at the mask edge.

Avoid a second hand, stray fingers, any skin below the crossguard, a bent or curved blade, a kink at the crossguard, a large angle change, a moved hand or forearm, a changed leg, foot, skirt or floor, a visible mask seam, text, logo or watermark."""


# 스탠딩 전용: 얼굴 피부 매끈하게 + 머리카락을 미니멀한 아래로 흐르는 불길로.
CODEX_FACEHAIR = """Reproduce the supplied image, changing only TWO things: her face skin and her hair. Everything else must be copied exactly.

FIX 1 - SMOOTH HER FACE.
Her face is currently broken up by hard crease lines that read as cracks: a harsh vertical line down the bridge of her nose, stiff lines around the eyes, and a hard-edged shadow slab across one cheek with a visible seam.
Redraw the face as clean anime cel shading: the skin is ONE smooth, even, matte field of light warm brown, broken only by two or three soft, simply-shaped shadows - under the fringe, along one side of the nose, and under the jaw - each with a soft clean edge. No crack lines, no crease lines, no wrinkles, no hard seams, no blotches, no gritty texture, no harsh ridge drawn down the nose. The nose is suggested by a small soft shadow and a tiny highlight, not by a drawn line.
Delicate refined anime features: a soft jawline, a small nose, and large clear expressive eyes. BOTH EYES MATCH - the same shape, the same size, the same fully visible glowing crimson-red iris with the same highlight, clean even eyelash lines, no smearing or distortion on either eye. A faint confident half-smile. Even complexion with no blush patch.

FIX 2 - THE HAIR: MINIMAL, SOFT, FLOWING DOWNWARD LIKE A SLOW FLAME.
Right now it reads as a repeating, choppy wave pattern - many ripples of the same size and the same frequency stacked over each other, like corrugated water. That repetition is the problem.
Redraw it as a SMALL NUMBER of long, broad, smooth locks falling DOWNWARD and slightly back. Each lock is one single long gentle curve from the scalp to its tip - not a series of waves, not a zigzag, not a ripple. Give the locks DIFFERENT widths and DIFFERENT lengths so no two read the same: a few wide dominant locks carry the shape, a few narrower ones sit between them. Leave generous open space between locks so the mass stays airy and light.
Inside each lock, draw only a few long clean highlight lines that follow its curve - not a dense scribble of strands.
The tips taper and lift very slightly, brightening from scarlet to glowing ember-orange, like the calm tip of a flame. A few small sparks may leave the tips.
Keep it RESTRAINED and minimal - fewer, larger, calmer shapes. Keep the same overall silhouette, the same length, the same parting and the same colour order: deep crimson roots through bright scarlet to ember-orange tips.
Avoid a repeating wave pattern, corrugated ripples, frizz, knots, tangles, matted clumps or a dense mesh of thin strands.

EVERYTHING ELSE IS UNCHANGED: the same pose and the same angle of every limb, the same single right hand closed on the sword grip - SHE HAS ONLY ONE HAND ON THE SWORD and there must be NO second hand or stray fingers anywhere near the crossguard - the same sword at the same angle with the same blade, runes, crossguard and pommel, the same smooth woven crimson cloth costume with every panel, fold, hem and iron ring, the same bare feet and ankle rings, the same background, lighting and palette.

The costume stays SMOOTH WOVEN CLOTH, clearly distinct from the hair.

Match the existing rendering: the same clean anime cel shading, line weight, skin tone and shadow colours.

Avoid a changed pose, a changed or duplicated hand, a second hand at the crossguard, six fingers, a moved or re-angled sword, a changed costume, a changed hairstyle silhouette, a cracked or creased face, mismatched eyes, text, logo or watermark."""
