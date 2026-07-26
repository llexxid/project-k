using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// GameUI.uss에서 추출한 디자인 토큰.
    /// 에디터 생성기(프리팹 구성)와 런타임 상태 변경(탭 선택, 등급 색상)이 공용으로 사용한다.
    /// 모든 px 값은 기준 해상도 1080x1920 기준.
    /// </summary>
    public static class UguiTheme
    {
        // ── 기준 해상도 ──
        public const float RefWidth = 1080f;
        public const float RefHeight = 1920f;
        public const float MatchWidthOrHeight = 0.5f;

        // ── 레이아웃 상수 (USS px) ──
        public const float BottomBarHeight = 190f;
        public const float HudTopHeight = 170f;
        public const float PanelSheetHeightPct = 0.60f;
        public const float GuideSheetHeightPct = 0.72f;
        public const float PanelSheetMinHeight = 380f;
        public const float PanelCornerRadiusPx = 22f;
        public const float PanelPadding = 20f;
        public const float PanelCloseBtnSize = 72f;
        public const float StageAreaTop = 180f;
        public const float DropdownTop = 175f;
        public const float DropdownWidth = 420f;
        public const float HamburgerDropdownWidth = 90f;
        public const float PartyHudBottom = 202f;
        public const float PartyHudHeight = 150f;
        public const float MageTowerHudTop = 300f;
        public const float MageTowerHudWidth = 176f;
        public const float MageTowerSlotSize = 134f;

        // ── 폰트 크기 (USS px) ──
        public const float FontTitleBig = 72f;
        public const float FontPressHint = 30f;
        public const float FontLoginPopupTitle = 40f;
        public const float FontStageLabel = 36f;
        public const float FontDeathTitle = 42f;
        public const float FontPanelTitle = 34f;
        public const float FontTabIcon = 56f;
        public const float FontTabLabel = 26f;
        public const float FontCurrencyValue = 28f;
        public const float FontCurrencyName = 24f;
        public const float FontSectionTitle = 26f;
        public const float FontBody = 24f;
        public const float FontSmall = 20f;
        public const float FontBadge = 16f;
        public const float FontDamageText = 30f;

        // ── 공통 색상 ──
        public static readonly Color PanelSheetBg = Rgba(10, 10, 15, 1f);
        public static readonly Color BottomBarBg = Rgba(8, 10, 16, 0.92f);
        public static readonly Color ModalBg = Rgba(35, 30, 45, 0.96f);
        public static readonly Color LoginBoxBg = Rgba(28, 32, 48, 0.98f);
        public static readonly Color GachaResultBg = Rgba(30, 30, 50, 0.95f);
        public static readonly Color TextPrimary = Rgba(255, 255, 255, 0.95f);
        public static readonly Color TextSecondary = Rgba(255, 255, 255, 0.70f);
        public static readonly Color TextTertiary = Rgba(255, 255, 255, 0.50f);
        public static readonly Color AccentGold = Rgba(255, 235, 180, 1f);
        public static readonly Color AccentGoldStrong = Rgba(255, 220, 100, 1f);
        public static readonly Color AccentBlue = Rgba(60, 120, 220, 0.85f);
        public static readonly Color AccentBlueSoft = Rgba(110, 180, 255, 0.22f);
        public static readonly Color LoginBtnBg = Rgba(104, 72, 38, 0.98f);   // 러스틱 청동-브라운(골드 텍스트와 대비)
        public static readonly Color LoginBtnBorder = Rgba(170, 130, 70, 0.80f);
        public static readonly Color SuccessGreen = Rgba(60, 180, 80, 1f);
        public static readonly Color SuccessGreenBright = Rgba(100, 210, 130, 1f);
        public static readonly Color DangerRed = Rgba(180, 60, 60, 1f);
        public static readonly Color DangerRedBright = Rgba(220, 70, 70, 1f);
        public static readonly Color WarnRed = Rgba(255, 80, 80, 1f);
        public static readonly Color PurpleDeep = Rgba(100, 60, 180, 1f);
        public static readonly Color PurpleBright = Rgba(180, 80, 255, 1f);
        public static readonly Color DimLight = Rgba(0, 0, 0, 0.35f);
        public static readonly Color DimMedium = Rgba(0, 0, 0, 0.65f);
        public static readonly Color DimHeavy = Rgba(0, 0, 0, 0.70f);
        public static readonly Color SurfaceFaint = Rgba(255, 255, 255, 0.06f);
        public static readonly Color SurfaceLight = Rgba(255, 255, 255, 0.08f);
        public static readonly Color SurfaceMid = Rgba(255, 255, 255, 0.12f);
        public static readonly Color HpGreen = Rgba(120, 255, 120, 0.85f);
        public static readonly Color TimerAmber = Rgba(255, 200, 60, 1f);
        public static readonly Color ToastBg = Rgba(0, 0, 0, 0.70f);
        public static readonly Color HudTopBg = Rgba(0, 0, 0, 0.35f);

        // ── 러스틱 판타지 테마 (따뜻한 나무/가죽/청동) — 평평한 검정 배경 대신 리치한 웜톤 ──
        public static readonly Color RusticPanel = Rgba(38, 29, 21, 0.98f);       // 패널 본문 (다크 우드/가죽)
        public static readonly Color RusticPanelDeep = Rgba(28, 21, 15, 0.99f);   // 더 깊은 본문/팝업
        public static readonly Color RusticBar = Rgba(32, 24, 17, 0.97f);         // 상/하단 바
        public static readonly Color RusticBarDeep = Rgba(24, 18, 12, 0.98f);     // 하단 바(더 어둡게)
        public static readonly Color RusticSurface = Rgba(56, 43, 30, 1f);        // 칩/버튼/탭 표면
        public static readonly Color RusticSurfaceDark = Rgba(22, 16, 11, 0.96f); // 어두운 칩(재화 등)
        public static readonly Color Bronze = Rgba(150, 110, 60, 1f);             // 청동 프레임/테두리
        public static readonly Color BronzeLight = Rgba(196, 154, 92, 1f);        // 밝은 청동
        public static readonly Color Parchment = Rgba(242, 230, 208, 0.97f);      // 양피지 텍스트
        public static readonly Color DropdownBg = Rgba(38, 29, 21, 0.98f);
        public static readonly Color GuideHintBlue = Rgba(180, 200, 255, 0.75f);
        public static readonly Color EnhanceOrange = Rgba(220, 160, 40, 0.80f);
        public static readonly Color DisabledGrey = Rgba(100, 100, 100, 0.50f);

        // ── 버튼 컬러 언어 (데모: 빨강=주/소모, 골드=확정/장착, 다크=취소/파괴) ──
        public static readonly Color BtnSpend   = Rgba(170, 58, 50, 1f);   // 주 행동/소모(뽑기·강화·구매) — 러스틱 크림슨
        public static readonly Color BtnConfirm = Rgba(178, 132, 60, 1f);  // 확정/장착/전직 — 청동-골드
        public static readonly Color BtnCancel  = Rgba(54, 43, 34, 1f);    // 취소/해제/닫기 — 다크 우드

        // ── 탭 선택 상태 (tab-btn-selected) ──
        public static readonly Color TabSelectedBg = AccentBlueSoft;
        public static readonly Color TabSelectedIcon = Rgba(255, 220, 150, 1f);
        public static readonly Color TabSelectedLabel = Rgba(255, 230, 180, 1f);
        public static readonly Color TabIndicator = Rgba(255, 205, 120, 0.95f);
        public static readonly Color TabNormalBg = SurfaceFaint;
        public static readonly Color TabNormalText = Rgba(255, 255, 255, 0.88f);

        // ── 등급 색상 (gacha-rarity-* / inv-rarity-*) ──
        public static readonly Color RarityNormal = Rgba(180, 180, 180, 1f);
        public static readonly Color RarityRare = Rgba(60, 140, 255, 1f);
        public static readonly Color RarityEpic = Rgba(180, 80, 255, 1f);
        public static readonly Color RarityClassFragment = Rgba(255, 200, 80, 1f);
        public static readonly Color RarityArcane = Rgba(100, 220, 180, 1f);
        public static readonly Color RaritySkill = Rgba(100, 220, 180, 1f);

        public static Color RarityColor(eEquipmentRarity rarity)
        {
            switch (rarity)
            {
                case eEquipmentRarity.Rare: return RarityRare;
                case eEquipmentRarity.Epic: return RarityEpic;
                default: return RarityNormal;
            }
        }

        /// <summary>표시 대상 재화의 강조 색 (파편/비전지식 카드 프레임 등).</summary>
        public static Color CurrencyAccentColor(eCurrency currency)
        {
            switch (currency)
            {
                case eCurrency.ClassFragment: return RarityClassFragment;
                case eCurrency.ArcaneKnowledge: return RarityArcane;
                default: return AccentGold;
            }
        }

        /// <summary>
        /// USS :active(scale 0.94~0.9, 밝기 하락) 대응 UGUI ColorBlock.
        /// pressed는 .mobile-tap-pressed(opacity 0.85)와 동일 감쇠.
        /// </summary>
        public static ColorBlock MakeColorBlock(float pressedMul = 0.80f, float highlightMul = 1.10f)
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(highlightMul, highlightMul, highlightMul, 1f);
            cb.pressedColor = new Color(pressedMul, pressedMul, pressedMul, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.60f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            return cb;
        }

        private static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }
    }
}
