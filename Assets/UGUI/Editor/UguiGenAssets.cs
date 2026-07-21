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
        // "Galmuri11 SDF"(e8c81bad…) 사용 — 소스 폰트 참조는 복구 완료(동적 한글 정상),
        // "SDF 1"은 메인 오브젝트가 아틀라스(Texture2D)라 타입 로드가 실패하는 이력이 있음.
        private const string GuidFontGalmuri = "e8c81bad0478536459fd7c980f22b8c0";     // Galmuri11 SDF (TMP)
        private const string GuidSfxPanelOpen = "903de372b5fa6404abdb6413592bf28f";
        private const string GuidSfxPanelClose = "9d24e3663f195324abd2c16503f04bda";
        private const string GuidSfxButtonClick = "1830b8c536e5ead4db1d11e5f9c8d36e";
        private const string GuidTitleBg = "f31f25eafade2c34dbc355e36f693c1d";        // 타이틀배경3.jpg
        private const string GuidTitleLogo = "b9c40fb974e04b14999f727ed745c1f6";      // 타이틀1.jpg

        internal static TMP_FontAsset Font => LoadFontByGuid(GuidFontGalmuri);

        /// <summary>
        /// TMP 폰트 로드. 에셋 파일의 메인 오브젝트가 아틀라스(Texture2D)로 잡혀 있으면
        /// 타입 지정 로드가 null을 반환하므로, 실패 시 전체 오브젝트에서 폰트를 찾는다.
        /// </summary>
        private static TMP_FontAsset LoadFontByGuid(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[UguiGen] 폰트 GUID {guid} 에셋을 찾을 수 없습니다.");
                return null;
            }

            var direct = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (direct != null) return direct;

            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj is TMP_FontAsset font) return font;
            }

            Debug.LogWarning($"[UguiGen] {path} 에서 TMP_FontAsset을 찾지 못했습니다.");
            return null;
        }
        internal static AudioClip SfxPanelOpen => LoadByGuid<AudioClip>(GuidSfxPanelOpen);
        internal static AudioClip SfxPanelClose => LoadByGuid<AudioClip>(GuidSfxPanelClose);
        internal static AudioClip SfxButtonClick => LoadByGuid<AudioClip>(GuidSfxButtonClick);
        internal static Sprite TitleBg => SpriteByGuid(GuidTitleBg);
        internal static Sprite TitleLogo => SpriteByGuid(GuidTitleLogo);

        internal static Sprite IconUser => FindIconSprite("UserBlueDark");
        internal static Sprite IconMinus => FindIconSprite("MinusBlueDark");
        internal static Sprite IconWrench => FindIconSprite("WrenchBlueDark");
        internal static Sprite IconWarning => FindIconSprite("WarningBlueDark");

        // ── 픽셀 아트 키트 (Assets/UI Toolkit/Art/Textures — Sprite 타입 + 9-slice border 설정 완료) ──
        internal static Sprite KitWindow => FindIconSprite("Window");
        internal static Sprite KitTitleBar => FindIconSprite("TitleBarMetal");
        internal static Sprite KitCard => FindIconSprite("UniversalPanel2");
        internal static Sprite KitSlot => FindIconSprite("SkillSlot");
        internal static Sprite KitEllipse => FindIconSprite("Ellipse64");
        internal static Sprite KitBtnBlue => FindIconSprite("Blue");
        internal static Sprite KitBtnBlueDown => FindIconSprite("BlueDown");
        internal static Sprite KitBtnGreen => FindIconSprite("Green");
        internal static Sprite KitBtnGreenDown => FindIconSprite("GreenDown");
        internal static Sprite KitBtnGrey => FindIconSprite("Grey");
        internal static Sprite KitBtnGreyDown => FindIconSprite("GreyDown");
        internal static Sprite KitBtnInactive => FindIconSprite("Inactive");
        internal static Sprite KitToggleOn => FindIconSprite("ToggleOn");
        internal static Sprite KitToggleOff => FindIconSprite("ToggleOff");
        internal static Sprite KitBarTrack => FindIconSprite("ScrollBarBg");
        internal static Sprite KitFillBlue => FindIconSprite("FillBlue");
        internal static Sprite KitFillGreen => FindIconSprite("FillGreen");
        internal static Sprite KitFillRed => FindIconSprite("FillRed");
        internal static Sprite KitFillYellow => FindIconSprite("FillYellow");
        internal static Sprite KitBarHandle => FindIconSprite("BubbleHandle");

        // 글리프 대체용 아이콘 (Galmuri11에 없는 ✕✓←⚔♞✦📦✉🔁 대응)
        internal static Sprite IconX => FindIconSprite("X");
        internal static Sprite IconCheck => FindIconSprite("CheckGreen");
        internal static Sprite IconArrowLeft => FindIconSprite("Arrow01Left");
        internal static Sprite IconSwords => FindIconSprite("Hammer");
        internal static Sprite IconHelmet => FindIconSprite("Helmet");
        internal static Sprite IconStar => FindIconSprite("Chest01");
        internal static Sprite IconBag => FindIconSprite("Bag");
        internal static Sprite IconEnvelope => FindIconSprite("Envelope");
        internal static Sprite IconRepeat => FindIconSprite("Repeat");

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

        /// <summary>
        /// 텍스처를 Sprite로 로드. Sprite 타입 + Point 필터(픽셀 아트 선명도) + border 유지를 보장한다.
        /// 이미 올바르게 임포트돼 있으면 재임포트하지 않는다.
        /// </summary>
        internal static Sprite EnsureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);

            bool needsReimport = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                needsReimport = true;
            }

            // 픽셀 아트 선명도 — Point 필터 + 압축 없음 (블러/뭉개짐 방지)
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                needsReimport = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                needsReimport = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                needsReimport = true;
            }

            if (needsReimport)
                importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
