using System;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 생성기가 쓰는 공용 에셋 로더.
    /// UI 스프라이트는 Layer Lab "GUI Pro - Minimal Game (Dark)" 에셋에서 이름으로 로드한다.
    /// (Layer Lab 스프라이트는 PPU=100 + 9-slice border 가 이미 세팅돼 있어 재임포트가 필요 없다.)
    /// 폰트는 한글 지원을 위해 Galmuri11 SDF 를 유지한다(LL 폰트는 라틴 전용).
    /// </summary>
    internal static class UguiGenAssets
    {
        // 프로젝트에 이미 존재하는 에셋 GUID
        private const string GuidFontGalmuri = "e8c81bad0478536459fd7c980f22b8c0";     // Galmuri11 SDF (TMP, 한글)
        private const string GuidSfxPanelOpen = "903de372b5fa6404abdb6413592bf28f";
        private const string GuidSfxPanelClose = "9d24e3663f195324abd2c16503f04bda";
        private const string GuidSfxButtonClick = "1830b8c536e5ead4db1d11e5f9c8d36e";

        // Layer Lab 에셋 루트 (스프라이트 이름 검색 범위)
        private const string LLRoot = "Assets/ExternalAssets/Layer Lab";

        internal static TMP_FontAsset Font => LoadFontByGuid(GuidFontGalmuri);

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
                if (obj is TMP_FontAsset font) return font;
            Debug.LogWarning($"[UguiGen] {path} 에서 TMP_FontAsset을 찾지 못했습니다.");
            return null;
        }

        internal static AudioClip SfxPanelOpen => LoadByGuid<AudioClip>(GuidSfxPanelOpen);
        internal static AudioClip SfxPanelClose => LoadByGuid<AudioClip>(GuidSfxPanelClose);
        internal static AudioClip SfxButtonClick => LoadByGuid<AudioClip>(GuidSfxButtonClick);

        // ── 프레임/패널 (Layer Lab Shared, 흰색 '_Bg'=속 채운 9-slice 마스터 → 코드에서 틴트) ──
        // ('_Border' 계열은 속이 빈 테두리 오버레이라 배경 채움엔 부적합 → '_Bg'를 사용한다.)
        internal static Sprite KitWindow => LL("BasicFrame_Rectangle_01~04_White_Bg"); // 시트/모달 패널(속 채움)
        internal static Sprite KitWindowBorder => LL("BasicFrame_Rectangle_01~04_White_Border1"); // 패널 테두리 오버레이
        internal static Sprite KitTitleBar => LL("BasicFrame_Rectangle_01~04_White_Bg"); // 헤더 스트립(속 채움, 슬레이트 틴트)
        internal static Sprite KitCard => LL("ListFrame_06_White_Bg");     // 카드/섹션 배경(속 채움)
        internal static Sprite KitSlot => LL("ItemFrame_02_White_Bg");     // 아이템 슬롯 배경(속 채움)
        internal static Sprite KitEllipse => LL("BasicFrame_Circle_H68_White_Bg"); // 원형 배경(속 채움)
        internal static Sprite KitFrameBorder => LL("BasicFrame_Rectangle_01~04_White_Border1"); // 셀/카드 등급·상태 테두리(속 빈 라운드 보더)
        internal static Sprite TitleBanner => LL("Title_01_NoDeco_Navy"); // 패널 헤더 리본 배너(가로 9-slice, 접힌 끝)
        internal static Sprite BadgeCrimped => LL("Badge_Crimped_01_White_Bg"); // 레벨/랭크 훈장 배지(8각 별)

        // ── 버튼 (Layer Lab Button_01: 흰색 Bg를 accent로 틴트, 눌림은 스케일 애니메이션) ──
        internal static Sprite KitBtnBlue => LL("Button_01_White_Bg");
        internal static Sprite KitBtnGreen => LL("Button_01_White_Bg");
        internal static Sprite KitBtnGrey => LL("Button_01_White_Bg");
        internal static Sprite KitBtnBorder => LL("Button_01_White_InnerBorder1"); // 정품 이너 림(광택/입체) — LL Button_01 베이스와 동일
        // 눌림/비활성은 UIButtonPress 스케일 + ColorBlock disabledColor로 처리(SpriteSwap 미사용).

        // ── 토글/스위치 (Layer Lab Dark 테마, 이미 채색됨 → 틴트 없이 사용) ──
        internal static Sprite KitToggleOn => LL("Toggle_Check_01_On");
        internal static Sprite KitToggleOff => LL("Toggle_Check_01_Off");

        // ── 게이지/슬라이더 ──
        internal static Sprite KitBarTrack => LL("Slider_01_White_Bg");    // 트랙 (틴트 어둡게)
        internal static Sprite KitBarHandle => LL("Slider_Hande_01_Handle");
        // 게이지 채움은 흰색 라운드(roundedRect)를 색으로 틴트 → 전용 채움 스프라이트 없음.

        // ── 아이콘 (Layer Lab PictoIcon/Weapon — 풀컬러, 틴트 없이 사용) ──
        internal static Sprite IconX => LL("exit_1");            // 닫기
        internal static Sprite IconCheck => LL("check");         // 체크
        internal static Sprite IconArrowLeft => LL("arrow_back"); // 뒤로
        internal static Sprite IconSwords => LL("hammer_1");     // 육성
        internal static Sprite IconHelmet => LL("headgear");     // 왕국군
        internal static Sprite IconStar => LL("star_1");         // 별/등급
        internal static Sprite IconBag => LL("bag_1");           // 인벤토리
        internal static Sprite IconEnvelope => LL("mail_unread_1"); // 우편
        internal static Sprite IconRepeat => LL("refresh");      // 루프

        internal static Sprite IconUser => LL("headgear");
        internal static Sprite IconMenu => LL("menu_1");
        internal static Sprite IconMinus => LL("minus");
        internal static Sprite IconPlus => LL("plus");
        internal static Sprite IconSetting => LL("setting_1");
        internal static Sprite IconLock => LL("lock");
        internal static Sprite IconWrench => LL("hammer_2");
        internal static Sprite IconWarning => LL("info");

        // 메뉴 역할 구분 아이콘
        internal static Sprite IconUserRole => LL("headgear");   // 종합(캐릭터)
        internal static Sprite IconSword => LL("FA_WP_Main_Sword_001_Silver"); // 장비
        internal static Sprite IconBook => LL("book");           // 스킬
        internal static Sprite IconWand => LL("wand_star");      // 마법탑 스킬
        internal static Sprite IconChest => LL("chest");         // 장비 뽑기/보상
        internal static Sprite IconGem => LL("gem_4");           // 재료·기타·고대주화
        internal static Sprite IconCoin => LL("coin_2");         // 골드

        private static T LoadByGuid<T>(string guid) where T : UnityEngine.Object
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[UguiGen] GUID {guid} 에셋을 찾을 수 없습니다 ({typeof(T).Name}).");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // 타이틀 배경/로고 — Layer Lab 데모 풍경 배경 사용(전용 아트 준비 시 교체).
        internal static Sprite TitleBg => LL("Background_04");
        internal static Sprite TitleLogo => null;

        /// <summary>구 호출부 호환 — 이름으로 Layer Lab 스프라이트 검색.</summary>
        internal static Sprite FindIconSprite(string name) => LL(name);

        /// <summary>
        /// Layer Lab 폴더에서 정확한 파일명으로 스프라이트를 로드한다.
        /// LL 스프라이트는 이미 Sprite 타입으로 올바르게 임포트돼 있으므로 재임포트하지 않는다
        /// (재임포트하면 Point 필터가 강제돼 부드러운 UI 아트가 계단현상을 일으킴).
        /// </summary>
        internal static Sprite LL(string exactName)
        {
            var guids = AssetDatabase.FindAssets($"{exactName} t:Texture2D", new[] { LLRoot });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (!System.IO.Path.GetFileNameWithoutExtension(path)
                        .Equals(exactName, StringComparison.OrdinalIgnoreCase)) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }
            Debug.LogWarning($"[UguiGen] Layer Lab 스프라이트 '{exactName}' 를 찾을 수 없습니다.");
            return null;
        }
    }
}
