using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Screen_Title / Screen_Main 프리팹 생성기.</summary>
    internal static class ScreenGens
    {
        // ═══════════════════════════════════════════
        //  Screen_Title (Screen_Title.uxml + .title-*/.login-* USS 대응)
        // ═══════════════════════════════════════════
        internal static GameObject GenerateTitle()
        {
            var rootRt = F.Root("Screen_Title");
            var view = rootRt.gameObject.AddComponent<TitleScreenView>();

            // 배경 이미지 (stretch-to-fill) + 25% 어둡게
            var bg = rootRt.gameObject.AddComponent<Image>();
            bg.sprite = UguiGenAssets.TitleBg;
            bg.color = Color.white;
            bg.raycastTarget = false;

            var dark = F.Box(rootRt, "Dark", new Color(0f, 0f, 0f, 0.25f), rounded: false);
            F.Stretch(dark.rectTransform);

            // 아무 곳이나 탭 캐처
            view.bgClickCatcher = F.InvisibleCatcher(rootRt, "BgClickCatcher");

            // 타이틀 로고 — 원본 타이틀1.jpg가 프로젝트에 없어 텍스트 타이틀로 대체.
            //   (팀이 로고 스프라이트를 준비하면 이 Image의 sprite만 교체하면 됨)
            var logoSprite = UguiGenAssets.TitleLogo;
            if (logoSprite != null)
            {
                var logo = F.IconImage(rootRt, "TitleLogo", logoSprite, 0f, 0f);
                F.AnchorTopStretch(logo.rectTransform, 120f, 520f);
                logo.preserveAspect = true;
            }
            else
            {
                // 텍스트 타이틀 (흰 박스 방지). 골드 색 + 큰 픽셀 폰트.
                var titleCol = F.Container(rootRt, "TitleText");
                F.AnchorTopStretch(titleCol.rectTransform, 260f, 260f);
                F.VLayout(titleCol.gameObject, 6f, null, TextAnchor.UpperCenter, expandWidth: true);

                var lineMain = F.Text(titleCol.transform, "TitleMain", "왕국 키우기", 96f, UguiTheme.AccentGoldStrong,
                    TextAlignmentOptions.Center, bold: true);
                F.Preferred(lineMain, height: 130f);
                lineMain.characterSpacing = 4f;

                var lineSub = F.Text(titleCol.transform, "TitleSub", "KINGDOM  IDLE", 40f,
                    new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, bold: true);
                F.Preferred(lineSub, height: 56f);
                lineSub.characterSpacing = 12f;
            }

            // 로그인 버튼 480×120 (화면 중심에서 약간 아래 = top 52%) — 픽셀 키트 버튼
            var loginImg = F.Box(rootRt, "BtnLogin", UguiTheme.LoginBtnBg, rounded: true, raycast: true);
            F.AnchorCenter(loginImg.rectTransform, 480f, 120f, 0f, -(0.52f - 0.5f) * UguiTheme.RefHeight);
            view.btnLogin = F.ButtonOn(loginImg);
            var loginLbl = F.Text(loginImg.transform, "Label", "로그인", 34f, UguiTheme.AccentGold, TextAlignmentOptions.Center, bold: true);
            loginLbl.characterSpacing = 8f;
            F.Stretch(loginLbl.rectTransform);

            // PRESS ANYWHERE TO CONTINUE (bottom 120)
            var hint = F.Text(rootRt, "LblPressHint", "PRESS ANYWHERE TO CONTINUE", 30f,
                new Color(1f, 1f, 1f, 0.90f), TextAlignmentOptions.Center);
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 120f);
            hintRt.sizeDelta = new Vector2(0f, 44f);
            view.pressHint = hint;

            // ── 로그인 팝업 ──
            var popup = F.Container(rootRt, "PopupLogin");
            F.Stretch(popup);
            view.popupLogin = popup.gameObject;

            var dim = F.Box(popup, "PopupLoginDim", new Color(0f, 0f, 0f, 0.65f), rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.transition = Selectable.Transition.None;
            view.popupLoginDim = dimBtn;

            var box = F.PixelPanel(popup, "PopupLoginBox",
                F.Catalog != null ? F.Catalog.kitWindow : null, Color.white, 0.5f, raycast: true,
                baseColor: F.PanelBaseDarker);
            var boxRt = box.rectTransform;
            F.AnchorCenter(boxRt, 820f, 0f);
            var boxFitter = box.gameObject.AddComponent<ContentSizeFitter>();
            boxFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            F.VLayout(box.gameObject, 16f, new RectOffset(40, 36, 40, 40));
            view.popupLoginBox = boxRt;

            var popupTitle = F.Text(box.transform, "Title", "계정 로그인", 40f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(popupTitle, height: 52f);

            var subtitle = F.Text(box.transform, "Subtitle", "계정을 선택하여 게임을 시작하세요.", 24f,
                new Color(1f, 1f, 1f, 0.85f));
            F.Preferred(subtitle, height: 34f);

            var divider = F.Box(box.transform, "Divider", new Color(1f, 1f, 1f, 0.10f), rounded: false);
            F.Preferred(divider, height: 2f);

            view.btnLoginGuest = MakeProviderButton(box.transform, "BtnLoginGuest", "게스트로 시작",
                new Color(70f / 255f, 130f / 255f, 90f / 255f, 1f), UguiTheme.TextPrimary,
                new Color(96f / 255f, 170f / 255f, 120f / 255f, 1f));
            view.btnLoginGoogle = MakeProviderButton(box.transform, "BtnLoginGoogle", "Google 계정으로 로그인",
                new Color(0.96f, 0.96f, 0.96f, 1f), new Color(0.16f, 0.16f, 0.16f, 1f),
                new Color(0.85f, 0.85f, 0.85f, 1f));
            view.btnLoginApple = MakeProviderButton(box.transform, "BtnLoginApple", "Apple 계정으로 로그인",
                new Color(0.06f, 0.06f, 0.06f, 1f), UguiTheme.TextPrimary,
                new Color(0.25f, 0.25f, 0.25f, 1f));

            var terms = F.Text(box.transform, "Terms",
                "로그인 시 이용약관 및 개인정보 처리방침에 동의한 것으로 간주됩니다.", 20f,
                new Color(180f / 255f, 190f / 255f, 210f / 255f, 0.85f), TextAlignmentOptions.Center, wrap: true);
            F.Preferred(terms, height: 56f);

            popup.gameObject.SetActive(false);

            return PrefabGenUtil.SavePrefab(rootRt.gameObject, $"{PrefabGenUtil.PrefabRoot}/Screens/Screen_Title.prefab");
        }

        private static Button MakeProviderButton(Transform parent, string name, string label, Color bg, Color textColor, Color iconColor)
        {
            var img = F.Box(parent, name, bg, rounded: true, raycast: true);
            F.Preferred(img, height: 110f);
            F.HLayout(img.gameObject, 16f, new RectOffset(20, 20, 0, 0), TextAnchor.MiddleLeft);
            var btn = F.ButtonOn(img);

            var icon = F.Box(img.transform, "Icon", iconColor);
            F.Preferred(icon, width: 64f, height: 64f);

            var lbl = F.Text(img.transform, "Label", label, 30f, textColor, TextAlignmentOptions.Left, bold: true);
            F.Flexible(lbl, flexWidth: 1f);

            return btn;
        }

        // ═══════════════════════════════════════════
        //  Screen_Main (Screen_Main.uxml + .hud-*/.stage-*/.bottom-bar USS 대응)
        // ═══════════════════════════════════════════
        internal static GameObject GenerateMain()
        {
            var rootRt = F.Root("Screen_Main");
            var view = rootRt.gameObject.AddComponent<MainScreenView>();

            BuildTopHud(rootRt, view);
            BuildStageArea(rootRt, view);
            BuildDeathPopup(rootRt, view);
            BuildBottomBar(rootRt, view);
            BuildDropdowns(rootRt, view);

            // 드롭다운 바깥 탭 캐처 (기본 비활성; 드롭다운들보다 형제 순서상 먼저)
            var catcher = F.InvisibleCatcher(rootRt, "OutsideCatcher");
            catcher.gameObject.SetActive(false);
            view.outsideCatcher = catcher;

            // 드롭다운들이 캐처보다 위에 오도록 마지막으로 이동
            view.popupCurrencies.transform.SetAsLastSibling();
            view.popupHamburger.transform.SetAsLastSibling();

            return PrefabGenUtil.SavePrefab(rootRt.gameObject, $"{PrefabGenUtil.PrefabRoot}/Screens/Screen_Main.prefab");
        }

        private static void BuildTopHud(RectTransform rootRt, MainScreenView view)
        {
            // 불투명 어두운 상단 바 (게임 배경 위에서 확실히 보이도록)
            var hud = F.Box(rootRt, "HudTop", new Color(0.06f, 0.07f, 0.11f, 0.94f), rounded: false);
            F.AnchorTopStretch(hud.rectTransform, 0f, UguiTheme.HudTopHeight);
            F.HLayout(hud.gameObject, 0f, new RectOffset(22, 22, 0, 0), TextAnchor.MiddleLeft);

            // 하단 골드 구분선
            var underline = F.Box(hud.transform, "Underline", new Color(1f, 220f / 255f, 120f / 255f, 0.35f), rounded: false);
            var ulRt = underline.rectTransform;
            ulRt.anchorMin = new Vector2(0f, 0f);
            ulRt.anchorMax = new Vector2(1f, 0f);
            ulRt.pivot = new Vector2(0.5f, 0f);
            ulRt.anchoredPosition = Vector2.zero;
            ulRt.sizeDelta = new Vector2(0f, 3f);
            underline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            underline.raycastTarget = false;

            // ── 좌측: 프로필 + 닉네임 ──
            var leftWrap = F.Container(hud.transform, "LeftWrap");
            F.HLayout(leftWrap.gameObject, 18f, null, TextAnchor.MiddleLeft);

            var profile = F.CircleBox(leftWrap, "BtnProfileBlank", UguiTheme.SurfaceMid, raycast: true);
            F.Preferred(profile, width: 92f, height: 92f);
            var profileBtn = profile.gameObject.AddComponent<Button>();
            profileBtn.targetGraphic = profile;
            profileBtn.transition = Selectable.Transition.ColorTint;
            profileBtn.colors = UguiTheme.MakeColorBlock();
            profile.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.btnProfile = profileBtn;
            var profileIcon = F.IconImage(profile.transform, "Icon", UguiGenAssets.IconUser, 56f, 56f);
            F.AnchorCenter(profileIcon.rectTransform, 56f, 56f);

            var nick = F.Text(leftWrap, "LblNickname", "닉네임", 34f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(nick, width: 240f, height: 44f);
            view.lblNickname = nick;

            // ── 중앙 스페이서 ──
            var spacer = F.Container(hud.transform, "Spacer");
            F.Flexible(spacer.gameObject.AddComponent<LayoutElement>(), flexWidth: 1f);

            // ── 우측: 재화 버튼 + 햄버거 ──
            var rightWrap = F.Container(hud.transform, "RightWrap");
            F.HLayout(rightWrap.gameObject, 14f, null, TextAnchor.MiddleRight);

            var currencyBtnImg = F.Box(rightWrap, "BtnCurrency", UguiTheme.SurfaceMid, rounded: true, raycast: true);
            F.Preferred(currencyBtnImg, height: 100f);
            F.HLayout(currencyBtnImg.gameObject, 10f, new RectOffset(16, 16, 0, 0), TextAnchor.MiddleLeft);
            view.btnCurrency = F.ButtonOn(currencyBtnImg);

            var goldName = F.Text(currencyBtnImg.transform, "GoldName", "골드", UguiTheme.FontCurrencyName,
                new Color(1f, 1f, 1f, 0.85f));
            F.Preferred(goldName, width: 58f, height: 34f);
            var goldValue = F.Text(currencyBtnImg.transform, "LblGold", "0", UguiTheme.FontCurrencyValue,
                UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(goldValue, width: 140f, height: 36f);
            view.lblGold = goldValue;

            var coinName = F.Text(currencyBtnImg.transform, "CoinName", "고대주화", UguiTheme.FontCurrencyName,
                new Color(1f, 1f, 1f, 0.85f));
            F.Preferred(coinName, width: 110f, height: 34f);
            var coinValue = F.Text(currencyBtnImg.transform, "LblAncientCoin", "0", UguiTheme.FontCurrencyValue,
                UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(coinValue, width: 120f, height: 36f);
            view.lblAncientCoin = coinValue;

            var hamburger = F.Box(rightWrap, "BtnHamburgerRight", UguiTheme.SurfaceMid, rounded: true, raycast: true);
            F.Preferred(hamburger, width: 90f, height: 90f);
            view.btnHamburger = F.ButtonOn(hamburger);
            view.btnHamburgerRect = hamburger.rectTransform;
            var hamburgerIcon = F.IconImage(hamburger.transform, "Icon", UguiGenAssets.IconMinus, 44f, 44f);
            F.AnchorCenter(hamburgerIcon.rectTransform, 44f, 44f);
        }

        private static void BuildStageArea(RectTransform rootRt, MainScreenView view)
        {
            var waveGo = F.Container(rootRt, "StageArea");
            F.AnchorTopStretch(waveGo, UguiTheme.StageAreaTop, 110f);
            var waveView = waveGo.gameObject.AddComponent<WaveHudView>();
            view.waveHud = waveView;

            var col = F.VLayout(waveGo.gameObject, 8f, null, TextAnchor.UpperCenter, childControlHeight: true, expandWidth: false);
            col.childForceExpandWidth = false;

            // ── 스테이지 행 ──
            var row = F.Container(waveGo, "StageRow");
            F.HLayout(row.gameObject, 16f, null, TextAnchor.MiddleCenter);
            F.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 60f);

            var loopImg = F.Box(row, "BtnLoopIcon", new Color(1f, 200f / 255f, 60f / 255f, 1f), rounded: true, raycast: true);
            F.Preferred(loopImg, width: 48f, height: 48f);
            waveView.btnLoopIcon = F.ButtonOn(loopImg);
            if (F.Catalog != null && F.Catalog.iconRepeat != null)
            {
                var loopIcon = F.IconImage(loopImg.transform, "Icon", F.Catalog.iconRepeat, 30f, 30f);
                F.AnchorCenter(loopIcon.rectTransform, 30f, 30f);
            }
            loopImg.gameObject.SetActive(false);

            var stageLbl = F.Text(row, "LblStage", "스테이지 1-1", UguiTheme.FontStageLabel, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(stageLbl, width: 320f, height: 52f);
            waveView.lblStage = stageLbl;

            var bossGroup = F.Container(row, "BossChallenge");
            F.HLayout(bossGroup.gameObject, 8f, null, TextAnchor.MiddleCenter);
            waveView.bossChallengeRoot = bossGroup.gameObject;

            var bossLbl = F.Text(bossGroup, "LblBossChain", "보스 자동 도전", 18f, new Color(1f, 1f, 1f, 0.85f));
            F.Preferred(bossLbl, width: 130f, height: 34f);

            var toggle = F.SimpleToggle(bossGroup, "TglBossChain", 34f);
            F.Preferred((RectTransform)toggle.transform, width: 54f, height: 34f);
            waveView.tglBossChain = toggle;

            // ── 보스 타이머 바 (300×8) ──
            var bossTimerFill = F.HFillBar(waveGo, "BossTimerBar", new Color(1f, 1f, 1f, 0.15f),
                new Color(1f, 80f / 255f, 80f / 255f, 0.90f), out var bossTrack);
            F.Preferred(bossTrack, width: 300f, height: 8f);
            waveView.bossTimerBar = bossTrack.gameObject;
            waveView.bossTimerFill = bossTimerFill;
            bossTrack.gameObject.SetActive(false);
        }

        private static void BuildDeathPopup(RectTransform rootRt, MainScreenView view)
        {
            var popup = F.Container(rootRt, "DeathPopup");
            F.Stretch(popup);
            F.VLayout(popup.gameObject, 20f, null, TextAnchor.MiddleCenter, expandWidth: false);
            view.waveHud.deathPopup = popup.gameObject;

            var title = F.Text(popup, "Title", "전원 사망", UguiTheme.FontDeathTitle,
                new Color(1f, 80f / 255f, 80f / 255f, 0.95f), TextAlignmentOptions.Center, bold: true);
            F.Preferred(title, width: 600f, height: 60f);

            var msg = F.Text(popup, "LblDeathMsg", "같은 웨이브를 도전하시겠습니까?", 28f,
                UguiTheme.TextPrimary, TextAlignmentOptions.Center);
            F.Preferred(msg, width: 700f, height: 44f);
            view.waveHud.lblDeathMsg = msg;

            var btnRow = F.Container(popup, "Buttons");
            F.HLayout(btnRow.gameObject, 24f, null, TextAnchor.MiddleCenter);
            F.Preferred(btnRow.gameObject.AddComponent<LayoutElement>(), width: 400f, height: 64f);

            view.waveHud.btnDeathYes = F.TextButton(btnRow, "BtnDeathYes", "예", 26f,
                new Color(60f / 255f, 180f / 255f, 80f / 255f, 0.80f), out _);
            F.Preferred((RectTransform)view.waveHud.btnDeathYes.transform, width: 180f, height: 64f);

            view.waveHud.btnDeathNo = F.TextButton(btnRow, "BtnDeathNo", "아니요", 26f,
                new Color(180f / 255f, 60f / 255f, 60f / 255f, 0.80f), out _);
            F.Preferred((RectTransform)view.waveHud.btnDeathNo.transform, width: 180f, height: 64f);

            var deathFill = F.HFillBar(popup, "DeathTimerBar", new Color(1f, 1f, 1f, 0.15f),
                new Color(1f, 200f / 255f, 60f / 255f, 1f), out var deathTrack);
            F.Preferred(deathTrack, width: 340f, height: 10f);
            view.waveHud.deathTimerFill = deathFill;

            popup.gameObject.SetActive(false);
        }

        private static void BuildBottomBar(RectTransform rootRt, MainScreenView view)
        {
            // 불투명 어두운 하단 탭 바
            var bar = F.Box(rootRt, "BottomBar", new Color(0.04f, 0.05f, 0.08f, 0.97f), rounded: false);
            F.AnchorBottomStretch(bar.rectTransform, 0f, UguiTheme.BottomBarHeight);
            F.HLayout(bar.gameObject, 12f, new RectOffset(16, 16, 12, 16), TextAnchor.MiddleCenter, expandWidth: true);
            view.bottomBar = bar.rectTransform;

            // 상단 골드 구분선
            var topline = F.Box(bar.transform, "Topline", new Color(1f, 220f / 255f, 120f / 255f, 0.35f), rounded: false);
            var tlRt = topline.rectTransform;
            tlRt.anchorMin = new Vector2(0f, 1f);
            tlRt.anchorMax = new Vector2(1f, 1f);
            tlRt.pivot = new Vector2(0.5f, 1f);
            tlRt.anchoredPosition = Vector2.zero;
            tlRt.sizeDelta = new Vector2(0f, 3f);
            topline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            topline.raycastTarget = false;

            view.tabDevelopment = MakeTabButton(bar.transform, "BtnDevelopment",
                F.Catalog != null ? F.Catalog.iconSwords : null, "육성");
            view.tabKingdomArmy = MakeTabButton(bar.transform, "BtnKingdomArmy",
                F.Catalog != null ? F.Catalog.iconHelmet : null, "왕국군");
            view.tabGacha = MakeTabButton(bar.transform, "BtnGacha",
                F.Catalog != null ? F.Catalog.iconStar : null, "뽑기");
        }

        private static MainTabButtonView MakeTabButton(Transform parent, string name, Sprite icon, string label)
        {
            // 탭 배경 — 은은한 반투명 (픽셀 버튼 스킨 미적용; 선택 시 SetSelected가 색을 강조)
            var bg = F.Box(parent, name, new Color(1f, 1f, 1f, 0.05f), rounded: true, raycast: true);
            F.Flexible(bg, flexWidth: 1f);
            F.Preferred(bg, height: 150f);

            var tab = bg.gameObject.AddComponent<MainTabButtonView>();
            tab.background = bg;

            // 픽셀 버튼 스킨을 적용하지 않는 순수 버튼 (배경 색상은 SetSelected가 직접 제어)
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            bg.gameObject.AddComponent<PlayClickSfxOnClick>();
            tab.button = btn;

            var inner = F.Container(bg.transform, "Inner");
            F.Stretch(inner);
            F.VLayout(inner.gameObject, 6f, new RectOffset(0, 0, 18, 12), TextAnchor.MiddleCenter, expandWidth: true);

            // 픽셀 키트 아이콘 (⚔♞✦ 글리프는 Galmuri11에 없어 스프라이트 사용)
            var iconWrap = F.Container(inner, "IconWrap");
            F.Preferred(iconWrap.gameObject.AddComponent<LayoutElement>(), height: 72f);
            var iconImg = F.IconImage(iconWrap, "Icon", icon, 64f, 64f);
            F.AnchorCenter(iconImg.rectTransform, 64f, 64f);
            tab.icon = iconImg;

            var nameLbl = F.Text(inner, "Label", label, UguiTheme.FontTabLabel, new Color(1f, 1f, 1f, 0.85f),
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(nameLbl, height: 34f);
            tab.label = nameLbl;

            // 상단 인디케이터 (선택 시 표시)
            var indicator = F.Box(bg.transform, "Indicator", new Color(1f, 205f / 255f, 120f / 255f, 0f));
            var indRt = indicator.rectTransform;
            indRt.anchorMin = new Vector2(0.28f, 1f);
            indRt.anchorMax = new Vector2(0.72f, 1f);
            indRt.pivot = new Vector2(0.5f, 1f);
            indRt.anchoredPosition = new Vector2(0f, -6f);
            indRt.sizeDelta = new Vector2(0f, 4f);
            var indLe = indicator.gameObject.AddComponent<LayoutElement>();
            indLe.ignoreLayout = true;
            tab.indicator = indicator;

            return tab;
        }

        private static void BuildDropdowns(RectTransform rootRt, MainScreenView view)
        {
            // ── 재화 상세 드롭다운 (top 175, right 0, width 420) ──
            var currencies = F.Box(rootRt, "PopupCurrencies", UguiTheme.DropdownBg, rounded: true, raycast: true);
            var curRt = currencies.rectTransform;
            curRt.anchorMin = new Vector2(1f, 1f);
            curRt.anchorMax = new Vector2(1f, 1f);
            curRt.pivot = new Vector2(1f, 1f);
            curRt.anchoredPosition = new Vector2(0f, -UguiTheme.DropdownTop);
            curRt.sizeDelta = new Vector2(UguiTheme.DropdownWidth, 100f);
            F.VLayout(currencies.gameObject, 6f, new RectOffset(12, 12, 12, 12));
            var curFitter = currencies.gameObject.AddComponent<ContentSizeFitter>();
            curFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var curGroup = currencies.gameObject.AddComponent<CanvasGroup>();

            view.popupCurrencies = currencies.gameObject;
            view.popupCurrenciesRect = curRt;
            view.popupCurrenciesGroup = curGroup;
            view.popupCurrenciesContent = curRt;
            currencies.gameObject.SetActive(false);

            // ── 햄버거 드롭다운 (top 175, right 0, width 90) ──
            var hamburger = F.Box(rootRt, "PopupHamburger", UguiTheme.DropdownBg, rounded: true, raycast: true);
            var hamRt = hamburger.rectTransform;
            hamRt.anchorMin = new Vector2(1f, 1f);
            hamRt.anchorMax = new Vector2(1f, 1f);
            hamRt.pivot = new Vector2(1f, 1f);
            hamRt.anchoredPosition = new Vector2(-8f, -UguiTheme.DropdownTop);
            hamRt.sizeDelta = new Vector2(UguiTheme.HamburgerDropdownWidth + 16f, 100f);
            F.VLayout(hamburger.gameObject, 10f, new RectOffset(8, 8, 8, 8), TextAnchor.UpperCenter);
            var hamFitter = hamburger.gameObject.AddComponent<ContentSizeFitter>();
            hamFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hamGroup = hamburger.gameObject.AddComponent<CanvasGroup>();

            view.popupHamburger = hamburger.gameObject;
            view.popupHamburgerRect = hamRt;
            view.popupHamburgerGroup = hamGroup;

            view.btnMenuInventory = MakeHamburgerItem(hamburger.transform, "BtnMenuInventory", null,
                F.Catalog != null ? F.Catalog.iconBag : null);
            view.btnMenuSettings = MakeHamburgerItem(hamburger.transform, "BtnMenuSettings", null, UguiGenAssets.IconWrench);
            view.btnMenuNotice = MakeHamburgerItem(hamburger.transform, "BtnMenuNotice", null, UguiGenAssets.IconWarning);
            view.btnMenuMail = MakeHamburgerItem(hamburger.transform, "BtnMenuMail", null,
                F.Catalog != null ? F.Catalog.iconEnvelope : null);

            hamburger.gameObject.SetActive(false);
        }

        private static Button MakeHamburgerItem(Transform parent, string name, string text, Sprite icon)
        {
            var bg = F.Box(parent, name, UguiTheme.SurfaceLight, rounded: true, raycast: true);
            F.Preferred(bg, width: 90f, height: 90f);
            var btn = F.ButtonOn(bg);

            if (icon != null)
            {
                var img = F.IconImage(bg.transform, "Icon", icon, 48f, 48f);
                F.AnchorCenter(img.rectTransform, 48f, 48f);
            }
            else if (!string.IsNullOrEmpty(text))
            {
                var lbl = F.Text(bg.transform, "Label", text, 40f, UguiTheme.TextPrimary, TextAlignmentOptions.Center, bold: true);
                F.Stretch(lbl.rectTransform);
            }

            return btn;
        }
    }
}
