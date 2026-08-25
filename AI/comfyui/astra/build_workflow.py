# -*- coding: utf-8 -*-
"""Astra 프로덕션 워크플로 빌더 — API JSON + 프롬프트 문서를 생성한다.

노드 타입/입력 이름은 전부 현재 인스턴스의 스키마(get_node)에서 확인한 실제 값이다:
  OpenAIGPTImageNodeV2 : prompt, model(dynamic combo), model.size, model.quality,
                         model.background, model.images.image_1(IMAGE), n, seed
  ImageScale           : image, upscale_method[nearest-exact|area|...], width, height, crop
  ImageQuantize        : image, colors(1..256), dither[none|floyd-steinberg|...]
  SaveImage/PreviewImage: images, filename_prefix
"""
import json
import io
import os

ROOT = os.path.dirname(os.path.abspath(__file__))

MASTER = """Create the definitive master reference image for ASTRA, a clearly adult celestial goddess for a premium anime fantasy mobile game.

One single adult woman with a visibly mature refined anime face, tall elegant proportions, long legs, graceful silhouette, calm and unreachable divine presence. Fully visible metallic silver irises, cool confident expression and a restrained faint smile.

Her extremely long Milky-Way hair reaches below her calves. It must still read as real layered flowing hair, with near-black indigo roots, deep violet and cold blue middle sections, lavender highlights and silver-white tips. Crisp stars and small nebula shapes appear inside the hair without turning it into smoke.

LOCKED COSTUME:
A liquid-silver floor-length asymmetric goddess dress with one bare shoulder, a deep but non-explicit neckline, open side-waist sections connected by polished silver bands, detached long silver sleeves, silver upper-arm bands and one silver band around the left thigh. Exactly one high slit over the LEFT thigh. Near-black indigo inner lining. Tasteful revealing adult fantasy design with opaque coverage of intimate areas.

Exactly three identical polished-silver six-point astral star sigils, same size and same construction, floating in a shallow arc behind her shoulders. No other floating props.

Full-body 3/4 front view, relaxed neutral pose, both hands fully visible, feet fully visible, hair and dress completely inside the frame, generous safe margin around the silhouette. Simple dark indigo cosmic studio background with a restrained flat silver halo.

Premium Japanese anime game character key art, clean shape language, strong readable silhouette, controlled detail density suitable for later conversion into high-resolution pixel art.

Do not create a child, teenager, youthful baby face, chibi proportions, school uniform, explicit nudity, transparent coverage, gold decoration, crown, blindfold, visor, armor, cape, weapon, extra character, duplicate body, extra limbs, malformed hands, extra fingers, cropped feet, cropped hair, text, logo, watermark or UI frame."""

CODEX = """Using the supplied ASTRA master image as the exact identity, face, body proportion, hairstyle, costume, color palette and prop blueprint, create ASTRA'S FINAL CODEX PORTRAIT. Do not redesign her.

Preserve her mature face, visible metallic silver eyes, exact Milky-Way hair color order, exact liquid-silver asymmetric dress, exactly one high slit over the LEFT thigh, detached sleeves, silver arm bands, left thigh band and indigo inner lining. Preserve exactly three identical silver six-point astral star sigils. Do not add a weapon, crown, cape, armor, visor, blindfold or gold.

Vertical 2:3 full-body composition. Elegant contrapposto and slight 3/4 turn toward the camera. One hand lightly gathers the outer edge of her galaxy hair while the other rests open near her hip. Keep the head, hair, hands, entire dress hem and both feet completely inside the frame. Make her face, costume construction and silhouette readable on a mobile-game codex screen.

Use a restrained cosmic sanctuary background with stepped deep-indigo and violet shapes, a thin silver circular astral diagram and sparse stars. The background must not merge with her hair or dress.

Premium anime fantasy mobile-game codex key art intended for high-resolution pixel-art conversion. Clean silhouette, large readable shapes, controlled highlights and limited visual noise.

Strictly preserve the master identity and costume. Avoid a younger face, different hairstyle, shortened hair, altered neckline, second slit, changed asymmetry, extra props, altered star-sigil count, malformed hands, cropped body, text, logo, watermark, card border or UI."""

ULT = """Using the supplied ASTRA reference image as the exact identity - face, mature adult proportions, hairstyle and hair color order, costume construction and color palette - create ASTRA'S ULTIMATE-SKILL CUTSCENE CUT. Do not redesign her.

Preserve exactly: her mature adult anime face, fully visible metallic silver irises, extremely long Milky-Way hair with near-black indigo roots, deep violet and cold blue mid-lengths and silver-white tips, the liquid-silver asymmetric dress with one bare shoulder, open side waist connected by polished silver bands, detached long silver sleeves, silver upper-arm bands, and the near-black indigo inner lining. No gold, no crown, no visor, no blindfold, no weapon, no armor, no cape.

COMPOSITION - match this camera and pose exactly:
Upper-body half-body shot seen from slightly behind and to the side, a dynamic three-quarter back view. Her torso turns away from the camera while her head turns into near-profile to look off toward the horizon, chin slightly lifted, calm serene expression with a faint restrained smile. Her far arm sweeps out and forward, fully extended, the hand open with fingers naturally spread and relaxed. Her enormous galaxy hair streams in one continuous direction across the frame in a strong wind, long flowing strands separating into ribbons. The whole image feels airy, weightless and windswept.

CRITICAL - the character and nothing else:
The entire background is one flat, uniform, pure chroma-key green (#00FF00) filling every pixel that is not Astra herself. Absolutely nothing else in the frame: no sky, no clouds, no ground, no scenery, no birds, no flowers, no petals, no particles, no energy effects, no magic circles, no galaxy, no floating star sigils, no props, no cast shadow, no glow, no gradient, no vignette, no frame, no text. Do not use any green anywhere on her body, hair, skin or costume.

Keep her head, her extended hand and all fingers completely inside the frame. Clean crisp silhouette edges against the green so she can be cut out as a game cutscene overlay.

Premium Japanese anime mobile-game key art intended for high-resolution pixel-art conversion. Avoid identity drift, a younger face, changed costume, a second slit, extra limbs, fused fingers, broken wrists or duplicated bodies."""


def gpt(prompt, size, images=None, seed=0, quality="high"):
    node = {
        "class_type": "OpenAIGPTImageNodeV2",
        "inputs": {
            "prompt": prompt,
            "model": "gpt-image-2",
            "model.size": size,
            "model.custom_width": 1024,
            "model.custom_height": 1024,
            "model.background": "opaque",
            "model.quality": quality,
            "n": 1,
            "seed": seed,
        },
    }
    if images:
        node["inputs"]["model.images.image_1"] = images
    return node


def pixel_chain(src, lw, lh, scale, prefix, base):
    """RGB 정규화 → 축소(area) → 32색 양자화(dither none) → 정수배 nearest 확대 → 저장 + 프리뷰.

    첫 노드가 왜 필요한가: OpenAIGPTImageNodeV2 는 background=opaque 여도 **4채널(RGBA)**
    텐서를 내보내는데, 코어 ImageQuantize 는 RGBA 입력에서 터진다
    (`torch.zeros_like` 로 4채널 대상을 만들고 3채널 양자화 결과를 써넣어
     "expanded size of the tensor (4) must match the existing size (3)").
    실측으로 확인하고 무료 프로브 워크플로로 재현·수정했다. RGB 로 낮춰서 넣으면 통과한다.
    """
    return {
        str(base + 0): {"class_type": "Change Channel Count", "inputs": {
            "image": src, "kind": "RGB"}},
        str(base + 1): {"class_type": "ImageScale", "inputs": {
            "image": [str(base + 0), 0], "upscale_method": "area",
            "width": lw, "height": lh, "crop": "disabled"}},
        str(base + 2): {"class_type": "ImageQuantize", "inputs": {
            "image": [str(base + 1), 0], "colors": 32, "dither": "none"}},
        str(base + 3): {"class_type": "ImageScale", "inputs": {
            "image": [str(base + 2), 0], "upscale_method": "nearest-exact",
            "width": lw * scale, "height": lh * scale, "crop": "disabled"}},
        str(base + 4): {"class_type": "SaveImage", "inputs": {
            "images": [str(base + 3), 0], "filename_prefix": prefix}},
        str(base + 5): {"class_type": "PreviewImage", "inputs": {
            "images": [str(base + 3), 0]}},
    }


def build(quality="high"):
    api = {}
    # --- MASTER : 내부 기준 이미지 (게임에 직접 쓰지 않는다) ---
    api["10"] = gpt(MASTER, "1024x1536", seed=20260818, quality=quality)
    api["11"] = {"class_type": "SaveImage", "inputs": {
        "images": ["10", 0], "filename_prefix": "Astra/00_Astra_Master_Raw"}}
    api["12"] = {"class_type": "PreviewImage", "inputs": {"images": ["10", 0]}}

    # --- CODEX : master 를 참조 이미지로 편집 ---
    api["20"] = gpt(CODEX, "1024x1536", images=["10", 0], seed=20260819, quality=quality)
    api["21"] = {"class_type": "SaveImage", "inputs": {
        "images": ["20", 0], "filename_prefix": "Astra/01_Astra_Codex_Raw"}}
    api["22"] = {"class_type": "PreviewImage", "inputs": {"images": ["20", 0]}}
    api.update(pixel_chain(["20", 0], 256, 384, 4,
                           "Astra/01_Astra_Codex_Pixel_Final", 23))

    # --- ULTIMATE : 같은 master 를 참조 이미지로 편집 ---
    # 스킬 컷씬은 **캐릭터 단독 + 투명 배경**으로 게임 위에 얹힌다. 그래서 이 가지는
    # 그래프 안 픽셀 체인을 쓰지 않는다:
    #  · gpt-image-2 는 `background: transparent` 를 거부한다(라이브 검증기가 값 자체를 반려).
    #    그래서 평면 크로마 그린으로 뽑고 로컬에서 키잉한다.
    #  · 코어 ImageQuantize 는 RGB 만 받으므로 그래프 안에서 알파를 보존할 수 없다.
    # 마감은 finalize_ultimate.py (3패스 그린키 → 32색 스냅 → 알파 이진화 → 정수배 확대).
    api["30"] = gpt(ULT, "1152x2048", images=["10", 0], seed=20260901, quality=quality)
    api["31"] = {"class_type": "SaveImage", "inputs": {
        "images": ["30", 0], "filename_prefix": "Astra/02_Astra_Ultimate_Raw"}}
    api["32"] = {"class_type": "PreviewImage", "inputs": {"images": ["30", 0]}}
    return api


def main():
    api = build("high")
    with io.open(os.path.join(ROOT, "Astra_Production_Workflow_API.json"),
                 "w", encoding="utf-8") as f:
        json.dump(api, f, ensure_ascii=False, indent=1)

    md = [
        "# Astra 프로덕션 프롬프트\n\n",
        "- 생성 모델: `OpenAIGPTImageNodeV2` / `gpt-image-2` / quality `high`\n",
        "- 마스터 1024x1536 → 도감 1024x1536(edit), 컷씬 1152x2048(edit)\n",
        "- Unity 기준 해상도 1080x1920 세로 (`UguiTheme.RefWidth/RefHeight`) → 컷씬 9:16\n",
        "- 마스터는 게임에 직접 쓰지 않는 **내부 기준 이미지**다. 두 결과물의 참조로만 쓴다.\n",
    ]
    for title, prompt in [("1. MASTER", MASTER),
                          ("2. CODEX (도감)", CODEX),
                          ("3. ULTIMATE (궁극기 컷씬)", ULT)]:
        md += ["\n## ", title, "\n\n```text\n", prompt, "\n```\n"]
    io.open(os.path.join(ROOT, "Astra_Prompts.md"), "w", encoding="utf-8").write("".join(md))

    print("nodes:", len(api))
    print("ids:", " ".join(sorted(api, key=int)))


if __name__ == "__main__":
    main()
