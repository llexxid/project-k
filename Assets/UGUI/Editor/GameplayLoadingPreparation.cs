using System;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace KingdomIdle.UGUI.Editor
{
    public static class GameplayLoadingPreparation
    {
        public const string PrefabPath = "Assets/UGUI/Prefabs/Overlays/Overlay_GameplayLoading.prefab";
        private const string ArtPath = "Assets/UGUI/Art/Loading";

        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before preparing loading assets.");
            F.Init();
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            if (catalog == null) throw new InvalidOperationException("UI catalog is missing.");
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = $"{ArtPath}/loading_{i:00}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Loading frame is missing: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var root = F.Root("Overlay_GameplayLoading");
            var view = root.gameObject.AddComponent<GameplayLoadingOverlayView>();
            var dim = F.Box(root, "Dim", new Color(0f, 0f, 0f, .3f), rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            var image = F.Box(root, "Indicator", Color.white, rounded: false, raycast: false);
            image.sprite = frames[0];
            image.type = UnityEngine.UI.Image.Type.Simple;
            F.AnchorCenter(image.rectTransform, 256f, 256f);
            view.indicator = image;
            view.frames = frames;
            catalog.overlayGameplayLoading = PrefabGenUtil.SavePrefab(root.gameObject, PrefabPath);
            EditorUtility.SetDirty(catalog);

            const string atlasPath = "Assets/_Project/Art/Atlases/Atlas_UI.spriteatlasv2";
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null) throw new InvalidOperationException("Atlas_UI is missing.");
            var art = AssetDatabase.LoadAssetAtPath<DefaultAsset>(ArtPath);
            bool exists = Array.Exists(SpriteAtlasExtensions.GetPackables(atlas), item => item == art);
            if (!exists)
            {
                var asset = SpriteAtlasAsset.Load(atlasPath);
                asset.Add(new UnityEngine.Object[] { art });
                SpriteAtlasAsset.Save(asset, atlasPath);
                AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
