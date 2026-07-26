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

        [Header("Pixel art kit — panels/frames")]
        public Sprite kitWindow;        // 메인 윈도우 프레임 (시트/모달)
        public Sprite kitTitleBar;      // 타이틀 바 (헤더 스트립)
        public Sprite kitCard;          // 카드/섹션 배경 (UniversalPanel2)
        public Sprite kitSlot;          // 아이템 슬롯 프레임 (SkillSlot)
        public Sprite kitEllipse;       // 원형 (Ellipse64)

        [Header("Pixel art kit — buttons")]
        public Sprite kitBtnBlue;
        public Sprite kitBtnBlueDown;
        public Sprite kitBtnGreen;
        public Sprite kitBtnGreenDown;
        public Sprite kitBtnGrey;
        public Sprite kitBtnGreyDown;
        public Sprite kitBtnInactive;
        public Sprite kitToggleOn;
        public Sprite kitToggleOff;

        [Header("Pixel art kit — bars")]
        public Sprite kitBarTrack;      // ScrollBarBg (트랙)
        public Sprite kitFillBlue;
        public Sprite kitFillGreen;
        public Sprite kitFillRed;
        public Sprite kitFillYellow;
        public Sprite kitBarHandle;     // BubbleHandle (슬라이더 핸들)

        [Header("Pixel art kit — icons (글리프 대체)")]
        public Sprite iconX;            // 닫기 (✕ 대체)
        public Sprite iconCheck;        // 체크 (✓ 대체)
        public Sprite iconArrowLeft;    // 뒤로 (← 대체)
        public Sprite iconSwords;       // 육성 탭 (⚔ 대체)
        public Sprite iconHelmet;       // 왕국군 탭 (♞ 대체)
        public Sprite iconStar;         // 뽑기 탭 (✦ 대체)
        public Sprite iconBag;          // 인벤토리 (📦 대체)
        public Sprite iconEnvelope;     // 우편 (✉ 대체)
        public Sprite iconRepeat;       // 루프 (🔁 대체)

        [Header("Pixel art kit — 메뉴 역할 아이콘 (탭/네비 구분용)")]
        public Sprite iconUser;         // 종합(캐릭터)
        public Sprite iconSword;        // 장비
        public Sprite iconBook;         // 스킬
        public Sprite iconWand;         // 마법탑 스킬 뽑기
        public Sprite iconChest;        // 장비 뽑기 / 보상
        public Sprite iconGem;          // 재료·기타
        public Sprite iconCoin;         // 재화

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
        public GameObject panelDungeon;

        [Header("Popups")]
        public GameObject popupGachaResult;
        public GameObject popupDungeonClear;
        // 마법탑 장착/상세 팝업은 원본(UITK)과 동일하게 100% 코드 생성 — 프리팹 없음

        [Header("Overlays")]
        public GameObject overlayLoading;
        public GameObject overlayToast;
        public GameObject overlaySettings;

        [Header("HUDs")]
        public GameObject hudParty;
        public GameObject hudMageTower;

        [Header("Item prefabs (dynamic list contents)")]
        public GameObject itemNavTabButton;    // 탭/네비 버튼 공용
        public GameObject itemGachaCard;       // 가챠 미리보기/결과 카드 공용
        public GameObject itemCurrencyLine;    // 재화 드롭다운 한 줄
        public GameObject itemDamageText;      // 데미지 텍스트 (아웃라인 머티리얼)
        public GameObject itemGachaPullButton; // 뽑기 옵션 버튼 (1회/10연)
        public GameObject itemRatePill;        // 확률 요약 알약
        public GameObject itemActionButton;    // 범용 액션 버튼 (강화/장착/전직)
        public GameObject itemEquipCell;       // 장비 그리드 셀 (왕국군/인벤토리)
        public GameObject itemJobCard;         // 전직 카드
        public GameObject itemEnhanceCard;     // 육성 강화 카드
        public GameObject itemSkillRow;        // 스킬 행
        // 그 외 복잡한 동적 행(캐릭터 시트/스탯 비교표 등)은 UguiRuntimeFactory 로 코드 생성.
        // 반복되는 단순 위젯은 위 프리팹을 인스펙터에서 편집하면 전 화면에 반영된다.
    }
}
