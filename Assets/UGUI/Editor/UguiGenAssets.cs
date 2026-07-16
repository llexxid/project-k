using UnityEditor;
using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 생성기가 쓰는 공용 에셋 로더. GUID/이름 기반으로 폰트·SFX·스프라이트를 찾는다.
    /// 텍스처가 Sprite 타입이 아니면 임포터 설정을 고쳐서 다시 임포트한다.
    /// </summary>
    internal static class UguiGenAssets
    {
        // 프로젝트에 이미 존재하는 에셋 GUID (탐색으로 확정)
        private const string GuidFontGalmuri = "e8c81bad0478536459fd7c980f22b8c0";     // Galmuri11 SDF (TMP)
        private const string GuidSfxPanelOpen = "903de372b5fa6404abdb6413592bf28f";
        private const string GuidSfxPanelClose = "9d24e3663f195324abd2c16503f04bda";
        private const string GuidSfxButtonClick = "1830b8c536e5ead4db1d11e5f9c8d36e";
        private const string GuidTitleBg = "f31f25eafade2c34dbc355e36f693c1d";        // 타이틀배경3.jpg
        private const string GuidTitleLogo = "b9c40fb974e04b14999f727ed745c1f6";      // 타이틀1.jpg

        internal static TMP_FontAsset Font => LoadByGuid<TMP_FontAsset>(GuidFontGalmuri);
        internal static AudioClip SfxPanelOpen => LoadByGuid<AudioClip>(GuidSfxPanelOpen);
        internal static AudioClip SfxPanelClose => LoadByGuid<AudioClip>(GuidSfxPanelClose);
        internal static AudioClip SfxButtonClick => LoadByGuid<AudioClip>(GuidSfxButtonClick);
        internal static Sprite TitleBg => SpriteByGuid(GuidTitleBg);
        internal static Sprite TitleLogo => SpriteByGuid(GuidTitleLogo);

        internal static Sprite IconUser => FindIconSprite("UserBlueDark");
        internal static Sprite IconMinus => FindIconSprite("MinusBlueDark");
        internal static Sprite IconWrench => FindIconSprite("WrenchBlueDark");
        internal static Sprite IconWarning => FindIconSprite("WarningBlueDark");

        private static T LoadByGuid<T>(string guid) where T : Object
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[UguiGen] GUID {guid} 에셋을 찾을 수 없습니다 ({typeof(T).Name}).");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static Sprite SpriteByGuid(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            return EnsureSprite(path);
        }

        /// <summary>이름으로 아이콘 스프라이트 검색 (Assets/UI Toolkit/Art/Textures 우선).</summary>
        internal static Sprite FindIconSprite(string name)
        {
            var guids = AssetDatabase.FindAssets($"{name} t:Texture2D");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(file, name, System.StringComparison.OrdinalIgnoreCase)) continue;

                var sprite = EnsureSprite(path);
                if (sprite != null) return sprite;
            }
            Debug.LogWarning($"[UguiGen] 아이콘 '{name}' 스프라이트를 찾을 수 없습니다.");
            return null;
        }

        /// <summary>텍스처를 Sprite로 로드. 임포트 타입이 Sprite가 아니면 고쳐서 재임포트.</summary>
        internal static Sprite EnsureSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
