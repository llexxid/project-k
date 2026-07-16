using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// UGUI 프리팹/공용 에셋 카탈로그. 에디터 생성기가 채우고 UIManager가 참조한다.
    /// (Resources/Addressables 대신 하드 참조 — 동기 접근 + 빌드 포함 보장 + 에디트 타임 검증)
    /// </summary>
    [CreateAssetMenu(fileName = "UIViewCatalog", menuName = "KingdomIdle/UGUI/UI View Catalog")]
    public sealed class UIViewCatalog : ScriptableObject
    {
        [Header("Fonts")]
        public TMP_FontAsset defaultFont;
        public Material damageTextMaterial;

        [Header("Shared sprites")]
        public Sprite roundedRect;   // 흰색 9-slice 라운드 사각형 (틴트용)
        public Sprite circle;        // 흰색 원형 (틴트용)

        [Header("SFX")]
        public AudioClip panelOpenSfx;
        public AudioClip panelCloseSfx;
        public AudioClip buttonClickSfx;

        [Header("Screens")]
        public GameObject screenTitle;
        public GameObject screenMain;

        [Header("Panels (bottom sheets)")]
        public GameObject panelPlaceholder;
        public GameObject panelGuide;
        public GameObject panelGacha;
        public GameObject panelKingdomArmy;
        public GameObject panelDevelopment;
        public GameObject panelInventory;

        [Header("Popups")]
        public GameObject popupGachaResult;
        // 마법탑 장착/상세 팝업은 원본(UITK)과 동일하게 100% 코드 생성 — 프리팹 없음

        [Header("Overlays")]
        public GameObject overlayLoading;
        public GameObject overlayToast;
        public GameObject overlaySettings;

        [Header("HUDs")]
        public GameObject hudParty;
        public GameObject hudMageTower;

        [Header("Item prefabs (dynamic list contents)")]
        public GameObject itemNavTabButton;   // 탭/네비 버튼 공용
        public GameObject itemGachaCard;      // 가챠 미리보기/결과 카드 공용
        public GameObject itemCurrencyLine;   // 재화 드롭다운 한 줄
        public GameObject itemDamageText;     // 데미지 텍스트 (아웃라인 머티리얼)
        // 그 외 동적 행(가이드/장비/스킬/전직/인벤토리 등)은 원본과 동일하게
        // UguiRuntimeFactory 로 코드 생성한다.
    }
}
