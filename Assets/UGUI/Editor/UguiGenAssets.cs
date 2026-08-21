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
        internal static Sprite TitleBanner => LL("Title_01_NoDeco_LightBrown"); // 패널 헤더 리본 배너(러스틱 브라운, 가로 9-slice)
        internal static Sprite TitleBannerDeco => LL("Title_01_Deco_LightBrown"); // 타이틀/특별 헤더(장식 리본)
        internal static Sprite BadgeCrimped => LL("Badge_Crimped_01_White_Bg"); // 레벨/랭크 훈장 배지(8각 별)
        internal static Sprite PanelGradient => LL("Popup_Box_02_White_Gradient"); // 패널 세로 그라디언트 오버레이
        internal static Sprite HeaderDeco => LL("Popup_Box_02_White_Deco"); // ◇—◇ 헤더 플로리시(장식 디바이더)
        internal static Sprite PopupPattern => LL("Popup_Box_05_White_Pattern"); // 78x18 타일 텍스처(웜 틴트 → 우드그레인 질감)

        // ── PixelArtGUI2 (진짜 픽셀아트 UI 키트) ────────────────────────────────
        // Layer Lab 은 플랫 벡터 키트라 도트 게임 톤과 겉돈다. 하단바처럼 "도트 느낌"이 필요한
        // 곳은 이 키트를 쓴다. 전부 작은 원본(8~48px) + 9-slice 라 확대해도 픽셀이 살아 있고,
        // 회색/흰색 마스터라 러스틱 팔레트로 틴트하면 그대로 중세 금속·목재가 된다.
        // ⚠ 이름 검색(LL) 금지 — Icons/{8,32,...} 에 동명 파일이 있어 해상도가 임의로 잡힌다. 경로 고정.
        private const string PixRoot = "Assets/ExternalAssets/PixelArtGUI2/Textures";
        private static Sprite Pix(string rel) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{PixRoot}/{rel}");

        internal static Sprite PixBarMetal => Pix("Panels/TitleBarMetal.png");        // 16x16 리벳 금속판 (하단바 배경)
        internal static Sprite PixBarMetal2 => Pix("Panels/TitleBarMetal02.png");     // 16x16 변형
        internal static Sprite PixCornersGold => Pix("Panels/CornersGold.png");       // 48x48 금색 코너 장식 4모서리
        internal static Sprite PixSeparator => Pix("Panels/Separator.png");           // 4x4 구분선
        internal static Sprite PixPanel => Pix("Panels/UniversalPanel1.png");         // 8x8 흰 마스터 패널
        internal static Sprite PixTabUp => Pix("Panels/TagUp.png");                   // 8x8 위로 향한 탭 실루엣
        internal static Sprite PixBtn => Pix("Buttons/Grey.png");                     // 16x16 베벨 버튼(기본)
        internal static Sprite PixBtnDown => Pix("Buttons/GreyDown.png");             // 16x16 눌림/선택 상태
        internal static Sprite PixSkillSlot => Pix("Panels/SkillSlot.png");           // 32x32 슬롯 다이아

        // 왕국군 스킬 슬롯용 픽셀 아이콘 (32px 원본, 파티 HUD 46px 슬롯에 적합)
        internal static Sprite PixIconSword => Pix("Icons/32/Sword01.png");
        internal static Sprite PixIconBow => Pix("Icons/32/Bow01.png");
        internal static Sprite PixIconWand => Pix("Icons/32/Wand01.png");
        internal static Sprite PixIconShield => Pix("Icons/32/Shield01.png");   // 기사의 오라 = 심플한 방패 하나
        internal static Sprite PixIconPotion => Pix("Icons/32/Potion01.png");   // 자가 회복(강철의지)
        internal static Sprite PixIconArrows => Pix("Icons/32/Bow02.png");      // 집중사격
        internal static Sprite PixIconStar => Pix("Icons/32/StarBlue.png");     // 에너지 파동
        internal static Sprite PixIconBoots => Pix("Icons/32/Boots.png");       // 이동속도 계열

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
        internal static Sprite IconBag => LL("UI_Common_Bag_01_Brown");   // 인벤토리(러스틱 가죽 가방)
        internal static Sprite IconEnvelope => LL("UI_Common_Inbox_01");  // 우편(풀컬러)
        internal static Sprite IconRepeat => LL("refresh");      // 루프

        internal static Sprite IconUser => LL("headgear");
        internal static Sprite IconMenu => LL("menu_1");
        internal static Sprite IconMinus => LL("minus");
        internal static Sprite IconPlus => LL("plus");
        internal static Sprite IconSetting => LL("UI_System_Setting_01");  // 설정 기어(풀컬러)
        internal static Sprite IconLock => LL("lock");
        internal static Sprite IconWrench => LL("UI_System_Setting_01");   // 설정(메뉴) — 기어 통일
        internal static Sprite IconWarning => LL("UI_Common_Notice_01_Red"); // 공지(풀컬러 확성기)

        // 메뉴 역할 구분 아이콘
        internal static Sprite IconUserRole => LL("headgear");   // 종합(캐릭터)
        internal static Sprite IconSword => LL("FA_WP_Main_Sword_001_Silver"); // 장비
        internal static Sprite IconBook => LL("book");           // 스킬
        internal static Sprite IconWand => LL("wand_star");      // 마법탑 스킬
        internal static Sprite IconChest => LL("chest");         // 장비 뽑기/보상
        internal static Sprite IconGem => LL("Economy_Gem_02_Red");   // 재료·기타·고대주화(풀컬러 러스틱)
        internal static Sprite IconCoin => LL("Economy_Coin_02_Gold"); // 골드(풀컬러 러스틱)
        internal static Sprite IconAncientCoin => LL("Economy_Coin_02_Bronze"); // 고대주화(청동)
        internal static Sprite IconArcane => LL("Economy_Gem_01_Purple");        // 비전지식
        internal static Sprite IconFragment => LL("Item_Scroll_01_Red");         // 전직 파편

        internal static Sprite IconStageMap => LL("UI_Play_Map_01_Brown"); // 스테이지 지도 마커(러스틱)
        internal static Sprite IconCrown => LL("Economy_Crown_01_Gold");    // 왕관(타이틀 엠블럼/랭크)
        internal static Sprite IconTrophy => LL("UI_Rewards_Trophy_01_Gold"); // 트로피(랭킹)
        internal static Sprite IconPower => LL("Stat_Power_01");            // 전투력(CP)

        // ── 스탯 칩 아이콘 (LL 풀컬러 픽토, 틴트 없이 사용) ──
        internal static Sprite IconStatAtk => LL("Stat_Attack_01");      // 공격력(검)
        internal static Sprite IconStatHp => LL("Economy_Heart_02_Red");  // 체력(하트)
        internal static Sprite IconStatMove => LL("Stat_MoveSpeed_01");   // 이동속도(부츠)
        internal static Sprite IconStatDef => LL("Gear_Shield_03_Blue");  // 방어(방패)

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
