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
        // 파티 HUD 15% 확대(초상화 78→90, 스킬 슬롯 40→46) 반영 명목 높이.
        // 실측 블록 높이는 90 + 패딩 24 = 114px 이지만, 위 요소(신 스킬 버튼) 배치는
        // 여유를 포함한 이 예약 밴드를 기준으로 계산한다.
        public const float PartyHudHeight = 172f;
        public const float MageTowerHudTop = 300f;
        public const float MageTowerHudWidth = 134f;   // 슬롯(112) + 좌우 패딩 11
        // 슬롯 열 좌측 x — 화면 좌단에 붙인다(1080 기준 10px = 0.93%).
        // **마탑 중심에서 파생시키지 않는다.** 예전엔 MageTowerEnvCenterX 로부터 계산해 열을
        // 탑 아래 중앙에 맞췄는데, 그러면 탑을 옮길 때마다 열이 딸려 오고 열 폭을 바꾸면
        // 절반만큼 밀린다. 두 요소는 독립적으로 배치되어야 한다.
        public const float MageTowerHudLeft = 10f;
        // 마탑 환경 오브젝트 — 파티 HUD 밴드(202~316) 위로 솟게 한다.
        // 원본 200x402(석조 망루 정면도 — 기존 아트를 레퍼런스로 각도만 정면 재생성).
        // 폭 300 → 표시 높이 603, 발치 y=70, 꼭대기 y≈673.
        public const float MageTowerEnvWidth = 300f;
        public const float MageTowerEnvBottom = 70f;
        // 탑 **전체가 화면 안에** 들어와야 한다(이전엔 좌측 1/3 이 잘려 나갔다).
        // 폭 300, 중심 x=170 → 좌우 경계 20~320 으로 완전히 노출된다.
        // 스프라이트 안에서 탑이 정확히 중앙(99.5/100)이라 이 중심이 곧 탑의 중심이고,
        // 같은 축에 앵커된 수정도 자동으로 탑 중앙에 온다.
        public const float MageTowerEnvCenterX = 170f;
        public const float MageTowerCrystalSize = 132f;  // 수정 표시 크기
        // 탑 꼭대기 위로 띄우는 간격(수정 중심 기준). 수정이 커진 만큼 같이 올려야
        // 아랫부분이 총안에 파묻히지 않는다 — 반지름 66 + 여유 29.
        public const float MageTowerCrystalRise = 95f;

        // 슬롯 5개 + Auto = 6칸 세로 열. 134 면 열 높이가 881px 이라 좌측을 과점유하고
        // 확대된 마탑 지붕(꼭대기 y≈780)과 간격이 빠듯해진다 → 112 로 낮춰 열 높이 749, 여유 90px 확보.
        public const float MageTowerSlotSize = 112f;

        // ── 신성 스킬(궁극기) HUD — 하단 중앙 원형 버튼.
        //    가이드 퀘스트 창(임시 숨김)이 떠 있던 자리 = 파티 HUD 바로 위를 쓴다 ──
        public const float DivineHudDiameter = 176f;  // 원형 버튼 지름 (마탑 슬롯 134 대비 대형)
        public const float DivineHudMargin = 24f;     // 파티 HUD 예약 밴드 위 여백
        // 버튼 하단 y = 파티 HUD 바닥(202) + 파티 밴드(172) + 여백(24) = 398
        public const float DivineHudBottom = PartyHudBottom + PartyHudHeight + DivineHudMargin;
        public const float DivineHudGlowPad = 26f;    // 준비 완료 후광이 버튼 밖으로 번지는 여유
        // 컨셉 링 아트 캔버스. 링 몸체 안반경 82 = 등급 링(164) 바깥과 정합, 바깥 장식이 ±16px 돌출한다.
        public const float DivineRingCanvas = 208f;
        public const float DivineHudAutoRingPad = 12f; // AUTO 회전 링(틱)이 버튼 밖으로 나가는 반지름 여유

        // ── 신성 스킬 컷인 오버레이 ──
        // 컷씬 아트의 논리 해상도는 288x512 (AI/comfyui README §4). 홀더를 그 **정수배(x2)** 로 잡아야
        // Point 필터에서 픽셀이 균일한 2x2 블록으로 떨어진다 — 620x860 이던 시절엔 1.68배로 깔려
        // 픽셀 행이 들쭉날쭉했다. 세로 1024 는 y=180 기준 -332..+692 로, 이름 플레이트 윗변(-350)과
        // 화면 위끝(+960) 어디에도 닿지 않는다.
        public const float DivineCutInIllustWidth = 576f;    // 288 x2
        public const float DivineCutInIllustHeight = 1024f;  // 512 x2
        public const float DivineCutInIllustY = 180f;    // 화면 중앙 기준 일러스트 y 오프셋
        public const float DivineCutInSlideX = 420f;     // 일러스트가 옆에서 밀려 들어오는 시작 오프셋
        public const float DivineCutInPlateWidth = 900f;
        public const float DivineCutInPlateHeight = 260f;
        public const float DivineCutInPlateY = -480f;    // 화면 중앙 기준 이름 플레이트 y 오프셋

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
        public const float FontDivineCooldown = 48f;   // 궁극기 버튼 남은 초
        public const float FontDivineEmpty = 26f;      // 궁극기 버튼 미장착/이름 대체 표기
        public const float FontCutInGrade = 28f;       // 컷인 등급 리본
        public const float FontCutInName = 34f;        // 컷인 카드(초월자) 이름
        public const float FontCutInSkill = 62f;       // 컷인 스킬 이름

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
