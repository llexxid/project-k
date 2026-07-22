using System.IO;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 프리팹 저장/카탈로그/공용 스프라이트 생성 유틸.
    /// SaveAsPrefabAsset은 같은 경로에 덮어쓰면 GUID가 유지되므로 재실행에 안전(멱등)하다.
    /// </summary>
    internal static class PrefabGenUtil
    {
        internal const string PrefabRoot = "Assets/UGUI/Prefabs";
        internal const string SpriteRoot = "Assets/UGUI/Sprites";
        internal const string CatalogPath = "Assets/UGUI/UIViewCatalog.asset";

        /// <summary>임시 하이어라키를 프리팹으로 저장하고 파괴한다. 반환: 저장된 프리팹 에셋.</summary>
        internal static GameObject SavePrefab(GameObject temp, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var saved = PrefabUtility.SaveAsPrefabAsset(temp, path, out bool ok);
            Object.DestroyImmediate(temp);
            if (!ok)
            {
                Debug.LogError($"[UguiGen] 프리팹 저장 실패: {path}");
                return null;
            }
            Debug.Log($"[UguiGen] 프리팹 저장: {path}");
            return saved;
        }

        internal static UIViewCatalog GetOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UIViewCatalog>();
                EnsureFolder(Path.GetDirectoryName(CatalogPath));
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                Debug.Log($"[UguiGen] 카탈로그 생성: {CatalogPath}");
            }
            return catalog;
        }

        internal static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ═══ 공용 스프라이트 (흰색 라운드 사각형 / 원형 — 틴트해서 사용) ═══

        internal static Sprite GetOrCreateRoundedRect()
        {
            // 작은 테두리(8px/32px) — 작은 요소에서도 9-slice 코너가 겹치지 않아
            // 십자/흰박스 아티팩트가 생기지 않는다. (요소 폭·높이 ≥16px에서 안전)
            return GetOrCreateGeneratedSprite(
                $"{SpriteRoot}/RoundedRect.png",
                () => MakeRoundedRectTex(32, 8),
                border: new Vector4(8, 8, 8, 8));
        }

        internal static Sprite GetOrCreateCircle()
        {
            return GetOrCreateGeneratedSprite(
                $"{SpriteRoot}/Circle.png",
                () => MakeCircleTex(128),
                border: Vector4.zero);
        }

        private static Sprite GetOrCreateGeneratedSprite(string path, System.Func<Texture2D> maker, Vector4 border)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path));

            var tex = maker();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D MakeRoundedRectTex(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;

                    // 네 모서리에서 라운드 처리
                    float cx = x < radius ? radius : (x >= size - radius ? size - 1 - radius : -1);
                    float cy = y < radius ? radius : (y >= size - radius ? size - 1 - radius : -1);
                    if (cx >= 0 && cy >= 0)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        a = Mathf.Clamp01(radius - d + 0.5f);   // 1px 안티앨리어싱
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeCircleTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f - 1f;
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
