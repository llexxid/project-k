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
        public Sprite frameBorder;   // 셀/카드 등급·상태 테두리 (LL 라운드 보더, 등급색 틴트)
        public Sprite titleBanner;   // 패널/헤더용 리본 배너 (LL Title_01_NoDeco, 가로 9-slice)
        public Sprite panelGradient; // 패널 본문 세로 그라디언트 오버레이 (평평함 방지 — 상단 밝고 하단 어둡게)

        [Header("Pixel art kit — panels/frames")]
        public Sprite kitWindow;        // 메인 윈도우 프레임 (시트/모달)
        public Sprite kitTitleBar;      // 타이틀 바 (헤더 스트립)
        public Sprite kitCard;          // 카드/섹션 배경 (UniversalPanel2)
        public Sprite kitSlot;          // 아이템 슬롯 프레임 (SkillSlot)
        public Sprite kitEllipse;       // 원형 (Ellipse64)

        [Header("Pixel art kit — buttons")]
        public Sprite kitBtnBlue;
        public Sprite kitBtnGreen;
        public Sprite kitBtnGrey;
        public Sprite kitBtnBorder;   // LL Button_01 InnerBorder1 — 정품 이너 림(광택/입체용)
        public Sprite kitToggleOn;
        public Sprite kitToggleOff;

        [Header("Pixel art kit — bars")]
        public Sprite kitBarTrack;      // 슬라이더/게이지 트랙
        public Sprite kitBarHandle;     // 슬라이더 핸들

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
        public Sprite iconCoin;         // 골드
        public Sprite iconAncientCoin;  // 고대주화(청동)
        public Sprite iconArcane;       // 비전 지식(보라 젬)
        public Sprite iconFragment;     // 전직 파편(붉은 두루마리)

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
        public GameObject popupProfile;          // 프로필 팝업(더미/플레이스홀더)
        public GameObject popupMageTowerEquip;   // 마탑 스킬 장착 팝업 (프리팹화됨)
        public GameObject popupDungeonClear;
        public GameObject popupReincarnation;
        public GameObject popupDivineCollection; // 신 스킬 컬렉션북(도감) 팝업

        [Header("Overlays")]
        public GameObject overlayLoading;
        public GameObject overlayToast;
        public GameObject overlaySettings;
        public GameObject overlayDivineCutIn;    // 궁극기(신성 스킬) 컷인

        [Header("HUDs")]
        public GameObject hudParty;
        public GameObject hudMageTower;
        public GameObject hudMainActions;
        public GameObject hudDivineSkill;
        public GameObject hudMageTowerEnv;       // 마탑 환경 오브젝트 — 좌하단, 하단바 뒤        // 궁극기(신성 스킬) 버튼 — 좌하단

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
        public GameObject itemMageEquipSlot;   // 마탑 장착 슬롯 셀
        public GameObject itemMageSkillCell;   // 마탑 보유 스킬 그리드 셀
        public GameObject itemDivineCard;      // 신 스킬 컬렉션 카드 셀

        [Header("프리팹 전환 (런타임 코드빌드 → 프리팹)")]
        public GameObject popupMageTowerDetail;      // 마탑 스킬 상세 팝업
        public GameObject bodyDevelopment;           // 육성 패널 본문
        public GameObject gachaTabContent;           // 뽑기 탭 콘텐츠
        public GameObject itemGuideStepRow;          // 가이드 단계 행
        public GameObject itemGuideEmptyHint;        // 가이드 빈 힌트
        public GameObject itemInventoryListPage;     // 인벤토리 리스트 페이지
        public GameObject itemInventoryEquipDetail;  // 인벤토리 장비 상세 팝업

        [Header("왕국군 서브뷰 프리팹")]
        public GameObject panelKACharacterSheet;   // 캐릭터 시트
        public GameObject panelKAEquipment;         // 장비 뷰
        public GameObject panelKAEquipDetail;       // 장비 상세/액션
        public GameObject panelKASkill;             // 스킬 뷰
        public GameObject panelKAJobChange;         // 전직 뷰
        public GameObject panelKAJobDetail;         // 전직 상세
        public GameObject panelKAMessage;           // 안내 메시지
        public GameObject itemStatCompareRow;
        public GameObject itemStatTerm;   // 상세 스탯 방정식 탭 항       // 스탯 비교 행
        // 모든 UI가 프리팹 기반 — 위 프리팹을 인스펙터에서 편집하면 전 화면에 반영된다.
        // (런타임 코드빌드 UguiRuntimeFactory는 전면 프리팹화 완료로 제거됨. 데이터 N개는 item 프리팹 인스턴스화.)
    }
}
