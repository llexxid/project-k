using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Imports recorded logical frames without touching purchased source sheets.</summary>
public static class RefinedMageSpritePreparation
{
    public const string Folder = "Assets/_Project/Art/VFX/MageTower/Refined/";
    [Serializable] sealed class Sheet
    {
        public string name;
        public int width, height, count, columns;
        public float[] pivot;
    }
    public static void Prepare()
    {
        var sheets = JsonConvert.DeserializeObject<Sheet[]>(File.ReadAllText(Folder + "sheets.json"));
        var factories = new SpriteDataProviderFactories(); factories.Init();
        foreach (var sheet in sheets)
        {
            string path = Folder + sheet.name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var old = provider.GetSpriteRects();
            int rows = (sheet.count + sheet.columns - 1) / sheet.columns;
            var frames = Enumerable.Range(0, sheet.count).Select(i => new SpriteRect
            {
                name = sheet.name + "_" + i,
                rect = new Rect(i % sheet.columns * sheet.width, (rows - 1 - i / sheet.columns) * sheet.height, sheet.width, sheet.height),
                pivot = new Vector2(sheet.pivot[0], sheet.pivot[1]), alignment = SpriteAlignment.Custom,
                spriteID = i < old.Length ? old[i].spriteID : GUID.Generate()
            }).ToArray();
            provider.SetSpriteRects(frames);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(frames.Select(f => new SpriteNameFileIdPair(f.name, f.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
        }
    }
}
