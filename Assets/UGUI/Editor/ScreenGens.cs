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

            // 배경 이미지 (stretch-to-fill). 전용 타이틀 아트가 없으면 단색 다크 배경.
            var bg = rootRt.gameObject.AddComponent<Image>();
            var titleBg = UguiGenAssets.TitleBg;
            bg.sprite = titleBg;
            bg.color = titleBg != null ? Color.white : new Color(0.09f, 0.10f, 0.14f, 1f);
            bg.raycastTarget = false;

            var dark = F.Box(rootRt, "Dark", new Color(0f, 0f, 0f, 0.25f), rounded: false);
            F.Stretch(dark.rectTransform);

            // 아무 곳이나 탭 캐처
            view.bgClickCatcher = F.InvisibleCatcher(rootRt, "BgClickCatcher");

            // 타이틀 로고 — 제목은 배경 이미지(타이틀배경3.jpg)에 이미 포함되어 있으므로
            //   별도 로고/텍스트를 그리지 않는다. (전용 로고 스프라이트가 생기면 여기에 Image 추가)
            var logoSprite = UguiGenAssets.TitleLogo;
            if (logoSprite != null)
            {
                var logo = F.IconImage(rootRt, "TitleLogo", logoSprite, 0f, 0f);
                F.AnchorTopStretch(logo.rectTransform, 120f, 520f);
                logo.preserveAspect = true;
            }

            // ── 게임 타이틀 "왕국군 키우기" (러스틱 엠블럼 + 골드 텍스트 + 은은한 부유 애니메이션) ──
            var titleGroup = F.Container(rootRt, "GameTitle");
            titleGroup.anchorMin = new Vector2(0.5f, 1f);
            titleGroup.anchorMax = new Vector2(0.5f, 1f);
            titleGroup.pivot = new Vector2(0.5f, 1f);
            titleGroup.anchoredPosition = new Vector2(0f, -300f);
            titleGroup.sizeDelta = new Vector2(900f, 320f);
            F.VLayout(titleGroup.gameObject, 6f, null, TextAnchor.UpperCenter, childControlHeight: true, expandWidth: false);

            // 왕관 엠블럼
            var crown = F.IconImage(titleGroup, "CrownEmblem", UguiGenAssets.IconCrown, 132f, 132f);
            F.Preferred(crown, width: 132f, height: 132f);

            // 타이틀 텍스트 (골드, 두꺼운 다크 아웃라인 + 드롭섀도우)
            var titleText = F.Text(titleGroup, "TitleText", "왕국군 키우기", 96f, UguiTheme.AccentGoldStrong,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(titleText, width: 900f, height: 130f);
            titleText.characterSpacing = 4f;
            var titleShadow = titleText.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            titleShadow.effectDistance = new Vector2(0f, -6f);

            // 데코 디바이더 라인 (중앙 다이아)
            F.DecoDivider(titleGroup, height: 30f, gemColor: UguiTheme.AccentGoldStrong);

            // 은은한 부유 + 호흡 애니메이션
            titleGroup.gameObject.AddComponent<UITitleFloat>();

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
                F.Catalog != null ? F.Catalog.kitWindow : null, F.FrameGold, 24f, raycast: true,
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
            // 러스틱 상단 바 (따뜻한 다크 우드)
            var hud = F.Box(rootRt, "HudTop", UguiTheme.RusticBar, rounded: false);
            F.AnchorTopStretch(hud.rectTransform, 0f, UguiTheme.HudTopHeight);
            F.HLayout(hud.gameObject, 0f, new RectOffset(22, 22, 0, 0), TextAnchor.MiddleLeft);

            // 러스틱 드레싱: 우드그레인 + 광 시엔 + 하단(안쪽) 청동 트림
            DressRusticBar(hud, trimAtBottom: true);

            // ── 좌측: 프로필 + 닉네임 ──
            var leftWrap = F.Container(hud.transform, "LeftWrap");
            F.HLayout(leftWrap.gameObject, 18f, null, TextAnchor.MiddleLeft);

            // 프로필: 청동 링 + 어두운 소켓 + 아이콘 (아바타 프레임 느낌)
            var profile = F.CircleBox(leftWrap, "BtnProfileBlank", UguiTheme.Bronze, raycast: true);
            F.Preferred(profile, width: 96f, height: 96f);
            var profileBtn = profile.gameObject.AddComponent<Button>();
            profileBtn.targetGraphic = profile;
            profileBtn.transition = Selectable.Transition.ColorTint;
            profileBtn.colors = UguiTheme.MakeColorBlock();
            profile.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.btnProfile = profileBtn;
            var profileSocket = F.CircleBox(profile.transform, "Socket", new Color(0.16f, 0.13f, 0.10f, 1f), raycast: false);
            F.AnchorCenter(profileSocket.rectTransform, 80f, 80f);
            var profileIcon = F.IconImage(profileSocket.transform, "Icon", UguiGenAssets.IconUser, 56f, 56f);
            F.AnchorCenter(profileIcon.rectTransform, 56f, 56f);

            // 레벨 훈장 배지 (LL Badge_Crimped, 8각 별 — 초상화 우하단에 겹침)
            var badge = F.Container(profile.transform, "LevelBadge");
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = UguiGenAssets.BadgeCrimped;
            badgeImg.color = new Color(0.95f, 0.72f, 0.24f, 1f);   // 금색 훈장
            badgeImg.raycastTarget = false;
            badgeImg.preserveAspect = true;
            badge.anchorMin = new Vector2(1f, 0f); badge.anchorMax = new Vector2(1f, 0f); badge.pivot = new Vector2(1f, 0f);
            badge.anchoredPosition = new Vector2(8f, -6f);
            badge.sizeDelta = new Vector2(54f, 54f);
            var badgeShadow = badge.gameObject.AddComponent<Shadow>();
            badgeShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            badgeShadow.effectDistance = new Vector2(0f, -2f);
            var lvlLbl = F.Text(badge, "Lvl", "1", 28f, new Color(0.16f, 0.11f, 0.02f, 1f), TextAlignmentOptions.Center, bold: true);
            var lvlRt = lvlLbl.rectTransform;
            F.Stretch(lvlRt);
            lvlRt.offsetMin = new Vector2(0f, 2f); lvlRt.offsetMax = new Vector2(0f, 0f);
            view.lblProfileLevel = lvlLbl;

            var nick = F.Text(leftWrap, "LblNickname", "닉네임", 34f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(nick, width: 240f, height: 44f);
            view.lblNickname = nick;

            // ── 중앙 스페이서 ──
            var spacer = F.Container(hud.transform, "Spacer");
            F.Flexible(spacer.gameObject.AddComponent<LayoutElement>(), flexWidth: 1f);

            // ── 우측: 재화 칩 2종(아이콘+값) + 햄버거 ──
            var rightWrap = F.Container(hud.transform, "RightWrap");
            F.HLayout(rightWrap.gameObject, 12f, null, TextAnchor.MiddleRight);

            // 골드 칩 (클릭 → 재화 드롭다운)
            var goldChip = MakeCurrencyChip(rightWrap, "BtnCurrency",
                F.Catalog != null ? F.Catalog.iconCoin : null, out var goldValue);
            view.btnCurrency = F.ButtonOn(goldChip);
            view.lblGold = goldValue;

            // 고대주화 칩 (청동 고대주화 아이콘)
            var coinChip = MakeCurrencyChip(rightWrap, "AncientCoinChip",
                UguiGenAssets.IconAncientCoin, out var coinValue);
            view.lblAncientCoin = coinValue;
            view.btnAncientCoin = F.ButtonOn(coinChip);

            var hamburger = F.Box(rightWrap, "BtnHamburgerRight", UguiTheme.RusticSurface, rounded: true, raycast: true);
            F.Preferred(hamburger, width: 92f, height: 92f);
            view.btnHamburger = F.ButtonOn(hamburger);
            view.btnHamburgerRect = hamburger.rectTransform;
            var hamburgerIcon = F.IconImage(hamburger.transform, "Icon", UguiGenAssets.IconMenu, 48f, 48f);
            F.AnchorCenter(hamburgerIcon.rectTransform, 48f, 48f);
        }

        /// <summary>
        /// 바 공통 러스틱 드레싱: 우드그레인 타일 + 세로 광 시엔 + 안쪽 모서리 청동 트림.
        /// 전부 raycast 없는 ignoreLayout 오버레이 — 콘텐츠(칩/탭)보다 먼저 추가해 뒤에 깔린다.
        /// 어두운 틴트/불투명 청동만 사용 (Linear 색공간에서 흰 저알파 오버레이 금지).
        /// </summary>
        private static void DressRusticBar(Image bar, bool trimAtBottom)
        {
            if (UguiGenAssets.PopupPattern != null)
            {
                var wood = F.Container(bar.transform, "WoodGrain");
                var woodImg = wood.gameObject.AddComponent<Image>();
                woodImg.sprite = UguiGenAssets.PopupPattern;
                woodImg.type = Image.Type.Tiled;
                woodImg.color = new Color(0.10f, 0.065f, 0.04f, 0.55f);   // 어두운 그레인 라인
                woodImg.raycastTarget = false;
                F.Stretch(wood);
                wood.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            if (UguiGenAssets.PanelGradient != null)
            {
                var sheen = F.Container(bar.transform, "Sheen");
                var sheenImg = sheen.gameObject.AddComponent<Image>();
                sheenImg.sprite = UguiGenAssets.PanelGradient;
                sheenImg.type = Image.Type.Sliced;
                sheenImg.color = new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.16f);
                sheenImg.raycastTarget = false;
                F.Stretch(sheen);
                sheen.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            // 안쪽 모서리 트림: 근검정 그림자 위에 불투명 청동 라인 (입체 단차)
            AddTrimLine(bar.transform, "TrimShadow", new Color(0f, 0f, 0f, 0.55f), trimAtBottom, 0f, 3f);
            AddTrimLine(bar.transform, "TrimBronze", UguiTheme.BronzeLight, trimAtBottom, 3f, 4f);
        }

        /// <summary>
        /// 하단 탭바 = 중세 금속판. Layer Lab(플랫 벡터) 대신 **PixelArtGUI2(진짜 도트 키트)** 를 쓴다.
        /// 원본이 8~48px 이라 확대해도 도트가 살아 있고, 회색 마스터라 러스틱 팔레트로 틴트하면
        /// 그대로 중세 금속·목재가 된다. 전부 ignoreLayout + raycast off 라 탭 레이아웃과 충돌하지 않는다.
        /// </summary>
        private static void DressPixelBottomBar(Image bar)
        {
            // ① 배경 자체를 리벳 금속판으로 (여태 스프라이트 없는 민 색면이었다)
            if (UguiGenAssets.PixBarMetal != null)
            {
                bar.sprite = UguiGenAssets.PixBarMetal;
                bar.type = Image.Type.Sliced;
                bar.pixelsPerUnitMultiplier = 0.25f;   // 16px 원본을 크게 → 리벳이 굵은 도트로 읽힌다
                // 바 = '움푹 들어간 어두운 판', 탭 = '위로 솟은 밝은 판'.
                // 둘을 같은 밝기(0.42대)로 두니 장식이 전부 같은 갈색 노이즈로 뭉개졌다 —
                // 도트 UI가 풍성해 보이는 건 장식의 개수가 아니라 명도 대비다.
                bar.color = new Color(0.24f, 0.18f, 0.13f, 1f);
            }

            // ② 목재 결 — 금속판 위에 얇게 덧대 재질을 섞는다
            if (UguiGenAssets.PopupPattern != null)
            {
                var wood = F.Container(bar.transform, "WoodGrain");
                var img = wood.gameObject.AddComponent<Image>();
                img.sprite = UguiGenAssets.PopupPattern;
                img.type = Image.Type.Tiled;
                img.color = new Color(0.08f, 0.05f, 0.03f, 0.42f);
                img.raycastTarget = false;
                F.Stretch(wood);
                wood.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            // ③ 상단 캐노피 = 3단 금속 레일 (하이라이트/본체/그늘). 도트 UI의 입체감은
            //    그라데이션이 아니라 '한 줄씩 끊어지는 명도 계단'에서 나온다.
            AddTrimLine(bar.transform, "TrimShadow", new Color(0f, 0f, 0f, 0.7f), false, 0f, 3f);
            AddTrimLine(bar.transform, "TrimGoldHi", new Color(0.98f, 0.86f, 0.52f, 1f), false, 3f, 3f);
            AddTrimLine(bar.transform, "TrimGold", new Color(0.80f, 0.61f, 0.24f, 1f), false, 6f, 6f);
            AddTrimLine(bar.transform, "TrimGoldLo", new Color(0.34f, 0.23f, 0.09f, 1f), false, 12f, 4f);

            // ④ 리벳 열 — 금속판 느낌을 확정짓는 요소. 바 상단을 따라 일정 간격으로 박는다.
            if (UguiGenAssets.PixSeparator != null)
            {
                const int rivets = 9;
                for (int i = 0; i < rivets; i++)
                {
                    var rv = F.Container(bar.transform, $"Rivet{i}");
                    var img = rv.gameObject.AddComponent<Image>();
                    img.sprite = UguiGenAssets.PixSeparator;
                    img.color = new Color(0.78f, 0.62f, 0.30f, 0.95f);
                    img.raycastTarget = false;
                    rv.anchorMin = rv.anchorMax = new Vector2((i + 0.5f) / rivets, 1f);
                    rv.pivot = new Vector2(0.5f, 1f);
                    rv.anchoredPosition = new Vector2(0f, -18f);
                    rv.sizeDelta = new Vector2(10f, 10f);
                    rv.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                }
            }

            // ⑤ 탭 사이 기둥 — 판이 하나로 쭉 이어지면 아무리 장식을 얹어도 밋밋하다.
            //    탭 3개 경계에 어두운 목재 기둥 + 금색 캡을 세워 '칸이 나뉜 목공' 으로 읽히게 한다.
            //    HLayout 자식이 아니라 ignoreLayout 오버레이라 탭 폭에 영향을 주지 않는다.
            for (int i = 1; i <= 2; i++)
            {
                float fx = i / 3f;
                var post = F.Box(bar.transform, $"Post{i}", new Color(0.16f, 0.11f, 0.07f, 1f), rounded: false);
                var prt = post.rectTransform;
                prt.anchorMin = new Vector2(fx, 0f);
                prt.anchorMax = new Vector2(fx, 1f);
                prt.pivot = new Vector2(0.5f, 0.5f);
                prt.anchoredPosition = Vector2.zero;
                prt.sizeDelta = new Vector2(14f, -14f);
                post.raycastTarget = false;
                post.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

                // 기둥 왼쪽 하이라이트 한 줄 (도트 셰이딩)
                var hi = F.Box(post.transform, "Hi", new Color(0.46f, 0.34f, 0.22f, 1f), rounded: false);
                var hrt = hi.rectTransform;
                hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(0f, 1f);
                hrt.pivot = new Vector2(0f, 0.5f);
                hrt.anchoredPosition = Vector2.zero;
                hrt.sizeDelta = new Vector2(4f, 0f);
                hi.raycastTarget = false;

                // 위·아래 금색 캡
                foreach (var top in new[] { true, false })
                {
                    var cap = F.Box(post.transform, top ? "CapTop" : "CapBottom",
                        new Color(0.82f, 0.64f, 0.28f, 1f), rounded: false);
                    var crt = cap.rectTransform;
                    float ay = top ? 1f : 0f;
                    crt.anchorMin = new Vector2(0.5f, ay);
                    crt.anchorMax = new Vector2(0.5f, ay);
                    crt.pivot = new Vector2(0.5f, ay);
                    crt.anchoredPosition = new Vector2(0f, top ? 0f : 0f);
                    crt.sizeDelta = new Vector2(22f, 9f);
                    cap.raycastTarget = false;
                }
            }

            // ⑥ 네 모서리 금색 장식 — 원본(48x48)이 이미 4모서리 한 장이므로 늘이지 말고
            //    모서리마다 정사각으로 조각내 배치한다 (세로로 늘리면 금색 막대로 보인다).
            if (UguiGenAssets.PixCornersGold != null)
            {
                var corners = new[]
                {
                    (new Vector2(0f, 1f), new Vector2(0f, 0f)),   // 좌상
                    (new Vector2(1f, 1f), new Vector2(1f, 0f)),   // 우상
                    (new Vector2(0f, 0f), new Vector2(0f, 1f)),   // 좌하
                    (new Vector2(1f, 0f), new Vector2(1f, 1f)),   // 우하
                };
                for (int i = 0; i < corners.Length; i++)
                {
                    var (anchor, flip) = corners[i];
                    var c = F.Container(bar.transform, $"Corner{i}");
                    var img = c.gameObject.AddComponent<Image>();
                    img.sprite = UguiGenAssets.PixCornersGold;
                    img.type = Image.Type.Simple;
                    img.raycastTarget = false;
                    c.anchorMin = c.anchorMax = anchor;
                    c.pivot = anchor;
                    c.anchoredPosition = Vector2.zero;
                    c.sizeDelta = new Vector2(44f, 44f);
                    // 원본은 좌상 기준 장식 — 나머지 모서리는 스케일 반전으로 재사용
                    c.localScale = new Vector3(flip.x > 0 ? -1f : 1f, flip.y > 0 ? -1f : 1f, 1f);
                    c.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                }
            }
        }

        private static void AddTrimLine(Transform parent, string name, Color color, bool atBottom, float inset, float height)
        {
            var line = F.Box(parent, name, color, rounded: false);
            var rt = line.rectTransform;
            float ay = atBottom ? 0f : 1f;
            rt.anchorMin = new Vector2(0f, ay);
            rt.anchorMax = new Vector2(1f, ay);
            rt.pivot = new Vector2(0.5f, ay);
            rt.anchoredPosition = new Vector2(0f, atBottom ? inset : -inset);
            rt.sizeDelta = new Vector2(0f, height);
            line.raycastTarget = false;
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        /// <summary>재화 칩(데모식): 어두운 알약 + 좌측 오버행 아이콘 소켓(청동 링) + 우측 값. 반환: 칩 배경 Image, out 값 라벨.</summary>
        private static Image MakeCurrencyChip(Transform parent, string name, Sprite icon, out TextMeshProUGUI valueLabel)
        {
            var chip = F.Box(parent, name, UguiTheme.RusticSurfaceDark, rounded: true, raycast: true);
            F.Preferred(chip, width: 220f, height: 84f);
            // 왼쪽 여백을 크게 잡아 오버행 아이콘 자리 확보, 값은 우측 정렬
            F.HLayout(chip.gameObject, 0f, new RectOffset(78, 22, 0, 0), TextAnchor.MiddleRight);
            // 알약 구분감 — 청동 얇은 테두리
            var chipFrame = F.Frame(chip.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.6f));
            chipFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            valueLabel = F.Text(chip.transform, "Value", "0", UguiTheme.FontCurrencyValue, UguiTheme.TextPrimary,
                TextAlignmentOptions.Right, bold: true);
            F.Flexible(valueLabel, flexWidth: 1f);

            // 좌측 오버행 아이콘 소켓 (청동 링 + 풀컬러 아이콘) — 레이아웃 무시, 왼쪽 가장자리에 걸침
            var ring = F.CircleBox(chip.transform, "IconRing", UguiTheme.Bronze, raycast: false);
            var rrt = ring.rectTransform;
            rrt.anchorMin = new Vector2(0f, 0.5f); rrt.anchorMax = new Vector2(0f, 0.5f); rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(4f, 0f); rrt.sizeDelta = new Vector2(78f, 78f);
            ring.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var socket = F.CircleBox(ring.transform, "Socket", UguiTheme.RusticSurfaceDark, raycast: false);
            F.AnchorCenter(socket.rectTransform, 66f, 66f);
            var ic = F.IconImage(socket.transform, "Icon", icon, 58f, 58f);
            F.AnchorCenter(ic.rectTransform, 58f, 58f);
            return chip;
        }

        private static void BuildStageArea(RectTransform rootRt, MainScreenView view)
        {
            var waveGo = F.Container(rootRt, "StageArea");
            F.AnchorTopStretch(waveGo, UguiTheme.StageAreaTop, 110f);
            var waveView = waveGo.gameObject.AddComponent<WaveHudView>();
            view.waveHud = waveView;

            var col = F.VLayout(waveGo.gameObject, 8f, null, TextAnchor.UpperCenter, childControlHeight: true, expandWidth: false);
            col.childForceExpandWidth = false;

            // ── 스테이지 행 (러스틱 프레임 플라크 + 지도 마커) ──
            var rowBg = F.Box(waveGo, "StageRow", UguiTheme.RusticPanelDeep, rounded: true);
            F.HLayout(rowBg.gameObject, 12f, new RectOffset(20, 26, 8, 8), TextAnchor.MiddleCenter);
            var rowLe = rowBg.gameObject.AddComponent<LayoutElement>();
            rowLe.preferredWidth = 720f; rowLe.preferredHeight = 78f;
            var row = rowBg.rectTransform;
            // 청동 프레임 테두리(플라크 느낌)
            var stageFrame = F.Frame(rowBg.transform, "Frame", UguiTheme.Bronze);
            stageFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // 스테이지 지도 마커 아이콘
            var stageIcon = F.IconImage(row, "StageIcon", UguiGenAssets.IconStageMap, 48f, 48f);
            F.Preferred(stageIcon, width: 48f, height: 48f);

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
            // [마탑 환경 연출 예약] 바 왼쪽 ~220px 는 별도 작업에서 마법사 탑 환경 스프라이트가
            // 절대 배치(ignoreLayout 또는 형제 오버레이)로 겹칠 자리다. 탭 3개는 HLayout의
            // flexible 자식이라 형제 오버레이와 간섭하지 않는다 — 왼쪽에 고정 폭 레이아웃 요소를
            // 추가하지 말 것(오버레이가 탭을 밀어내는 대신 '위에 겹치는' 구조를 유지한다).
            // 러스틱 하단 탭 바 (더 어두운 다크 우드)
            var bar = F.Box(rootRt, "BottomBar", UguiTheme.RusticBarDeep, rounded: false);
            F.AnchorBottomStretch(bar.rectTransform, 0f, UguiTheme.BottomBarHeight);
            F.HLayout(bar.gameObject, 12f, new RectOffset(16, 16, 12, 16), TextAnchor.MiddleCenter, expandWidth: true);
            view.bottomBar = bar.rectTransform;

            // 픽셀아트 중세 드레싱 (하단바 전용 — 상단바보다 훨씬 풍성하게)
            DressPixelBottomBar(bar);

            view.tabDevelopment = MakeTabButton(bar.transform, "BtnDevelopment",
                F.Catalog != null ? F.Catalog.iconSwords : null, "육성");
            view.tabKingdomArmy = MakeTabButton(bar.transform, "BtnKingdomArmy",
                F.Catalog != null ? F.Catalog.iconHelmet : null, "왕국군");
            view.tabGacha = MakeTabButton(bar.transform, "BtnGacha",
                F.Catalog != null ? F.Catalog.iconChest : null, "뽑기");
        }

        private static MainTabButtonView MakeTabButton(Transform parent, string name, Sprite icon, string label)
        {
            // 탭 배경 — 픽셀 베벨 버튼(PixelArtGUI2). 스프라이트가 중간 회색이라 틴트가 곱해지므로
            // 어두운 러스틱 색을 그대로 쓰면 탭이 새까맣게 죽는다 → 밝은 웜브라운으로 시작.
            var bg = F.Box(parent, name, new Color(0.52f, 0.40f, 0.28f, 1f), rounded: true, raycast: true);
            F.Flexible(bg, flexWidth: 1f);
            F.Preferred(bg, height: 150f);
            if (F.Catalog != null && F.Catalog.kitBtnGrey != null)
            {
                bg.sprite = F.Catalog.kitBtnGrey;   // Button_01_White_Bg
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 1f;
            }

            // 픽셀아트 베벨 버튼으로 교체 — 도트 게임 톤에 맞고, 눌림 상태 스프라이트가 따로 있다.
            if (UguiGenAssets.PixBtn != null)
            {
                bg.sprite = UguiGenAssets.PixBtn;
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 0.25f;   // 16px 원본 → 굵은 베벨
            }

            // 입체감: 아래 드롭 섀도우(검정, Linear 안전)
            var tabShadow = bg.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            tabShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            tabShadow.effectDistance = new Vector2(0f, -4f);
            tabShadow.useGraphicAlpha = true;

            var tab = bg.gameObject.AddComponent<MainTabButtonView>();
            tab.background = bg;
            tab.bgNormalSprite = UguiGenAssets.PixBtn;
            tab.bgSelectedSprite = UguiGenAssets.PixBtnDown;   // 선택 시 베벨이 눌린 스프라이트로 교체

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

            // 아이콘 + 뒤에 어두운 금속 소켓 — 아이콘이 판 위에 '박힌' 느낌을 준다
            var iconWrap = F.Container(inner, "IconWrap");
            F.Preferred(iconWrap.gameObject.AddComponent<LayoutElement>(), height: 76f);
            if (UguiGenAssets.PixPanel != null)
            {
                // 아이콘 뒤 얕은 소켓 — 너무 진하면 탭 전체가 검게 죽는다
                var socket = F.Box(iconWrap, "Socket", new Color(0.20f, 0.15f, 0.10f, 0.45f));
                socket.sprite = UguiGenAssets.PixPanel;
                socket.type = Image.Type.Sliced;
                socket.pixelsPerUnitMultiplier = 0.2f;
                socket.raycastTarget = false;
                F.AnchorCenter(socket.rectTransform, 74f, 74f);
                socket.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            var iconImg = F.IconImage(iconWrap, "Icon", icon, 60f, 60f);
            F.AnchorCenter(iconImg.rectTransform, 60f, 60f);
            // 아이콘 마스터는 흰색이라 그대로 두면 순백으로 붕 뜬다. SetSelected 가 런타임에 다시
            // 칠하지만, 프리팹 초기값도 비선택 톤으로 맞춰 첫 프레임 번쩍임을 없앤다.
            iconImg.color = new Color(0.80f, 0.70f, 0.55f, 1f);
            tab.icon = iconImg;

            // 탭 네 귀퉁이 금속 스터드 — 판이 '박혀 있는' 느낌. 4개짜리 작은 도트라 가독성을 해치지 않는다.
            var studs = new[]
            {
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f),
            };
            for (int i = 0; i < studs.Length; i++)
            {
                var stud = F.Box(bg.transform, $"Stud{i}", new Color(0.74f, 0.57f, 0.26f, 0.9f), rounded: false);
                var srt = stud.rectTransform;
                srt.anchorMin = srt.anchorMax = studs[i];
                srt.pivot = studs[i];
                srt.anchoredPosition = new Vector2(studs[i].x > 0 ? -10f : 10f, studs[i].y > 0 ? -10f : 10f);
                srt.sizeDelta = new Vector2(8f, 8f);
                stud.raycastTarget = false;
                stud.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

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
            F.VLayout(currencies.gameObject, 6f, new RectOffset(14, 14, 14, 14));
            var curFitter = currencies.gameObject.AddComponent<ContentSizeFitter>();
            curFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var curGroup = currencies.gameObject.AddComponent<CanvasGroup>();
            AddDropdownFrame(currencies);

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
            F.VLayout(hamburger.gameObject, 10f, new RectOffset(10, 10, 10, 10), TextAnchor.UpperCenter);
            var hamFitter = hamburger.gameObject.AddComponent<ContentSizeFitter>();
            hamFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hamGroup = hamburger.gameObject.AddComponent<CanvasGroup>();
            AddDropdownFrame(hamburger);

            view.popupHamburger = hamburger.gameObject;
            view.popupHamburgerRect = hamRt;
            view.popupHamburgerGroup = hamGroup;

            view.btnMenuInventory = MakeHamburgerItem(hamburger.transform, "BtnMenuInventory", null,
                F.Catalog != null ? F.Catalog.iconBag : null);
            // 신 스킬 도감 — HUD 모서리 버튼에서 이사 옴 (원형 버튼 리워크로 자리 없음)
            view.btnMenuDivineCollection = MakeHamburgerItem(hamburger.transform, "BtnMenuDivineCollection", null,
                F.Catalog != null ? F.Catalog.iconBook : null);
            view.btnMenuSettings = MakeHamburgerItem(hamburger.transform, "BtnMenuSettings", null, UguiGenAssets.IconWrench);
            view.btnMenuNotice = MakeHamburgerItem(hamburger.transform, "BtnMenuNotice", null, UguiGenAssets.IconWarning);
            view.btnMenuMail = MakeHamburgerItem(hamburger.transform, "BtnMenuMail", null,
                F.Catalog != null ? F.Catalog.iconEnvelope : null);

            hamburger.gameObject.SetActive(false);
        }

        /// <summary>드롭다운에 청동 프레임 테두리(패널과 동일 러스틱 룩) 오버레이.</summary>
        private static void AddDropdownFrame(Image box)
        {
            var ol = box.gameObject.AddComponent<UnityEngine.UI.Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
            ol.effectDistance = new Vector2(3f, 3f);
            ol.useGraphicAlpha = true;
            var frame = F.Frame(box.transform, "Frame", UguiTheme.Bronze);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        private static Button MakeHamburgerItem(Transform parent, string name, string text, Sprite icon)
        {
            var bg = F.Box(parent, name, UguiTheme.RusticSurface, rounded: true, raycast: true);
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
