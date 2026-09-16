using System;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    public static class MageUiPreparation
    {
        [MenuItem("KingdomIdle/Mage Tower/Prepare UI prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var path = "Assets/UGUI/Art/Gacha/GachaBronzeButton.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = new Vector4(38, 12, 38, 12);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            F.Init(); F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            var prefabs = new[] {
                MageTowerDetailPopupPrefabGens.GenerateMageTowerDetailPopup(),
                PopupGens.GenerateMageSkillCell(),
                ItemGens.GenerateGachaPullButton(),
                OverlayGens.GenerateGachaResult(),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Panels/GachaTabContent.prefab")
            };
            foreach (var prefab in prefabs)
            {
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try { UguiPolishPass.ApplyTo(root); PrefabUtility.SaveAsPrefabAsset(root, prefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
