using UnityEditor;
using UnityEngine;

namespace KingdomIdle.Divine.EditorTools
{
    /// <summary>
    /// `Assets/Generated/ComfyUI/**` 아래로 들어오는 생성 아트의 임포트 설정을 강제한다.
    ///
    /// Why: 유니티 기본 임포트는 textureType Default + Bilinear + mipmap ON + nPOTScale 1 이라
    /// 픽셀 아트가 흐려지고 POT 크기로 리샘플되어 픽셀 그리드가 깨진다.
    /// (ExternalAssets/CombatRPG 1,600여 장이 실제로 이 상태다 — 같은 실수를 반복하지 않기 위한 장치)
    ///
    /// 규칙은 Assets/_Project/Art 의 프로젝트 관례를 따른다.
    /// </summary>
    public sealed class GeneratedArtPostprocessor : AssetPostprocessor
    {
        private const string TargetRoot = "Assets/Generated/ComfyUI/";

        /// <summary>UI/카드용 아트는 PPU 100 이 아니라 프로젝트 관례(32)를 쓰되, UI 스프라이트는 크기가 그대로 쓰이므로 무관.</summary>
        private const float PixelsPerUnit = 32f;

        private void OnPreprocessTexture()
        {
            if (assetPath == null || !assetPath.StartsWith(TargetRoot)) return;

            var importer = (TextureImporter)assetImporter;

            // 이미 한 번 강제된 에셋은 사용자가 인스펙터에서 바꾼 값을 존중한다
            if (!string.IsNullOrEmpty(importer.userData) && importer.userData.Contains("gen-art-v1"))
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.userData = "gen-art-v1";
        }
    }
}
