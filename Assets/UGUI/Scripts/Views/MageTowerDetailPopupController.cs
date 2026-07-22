using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.MageTower;
using Scripts.Core;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 마탑 스킬 상세 팝업 — 강화/각성/초기화 (UITKMageTowerDetailPopupController 이식).
    /// 원본과 동일하게 100% 코드 생성, 오버레이는 만들어두고 SetActive로 토글한다.
    /// </summary>
    public static class MageTowerDetailPopupController
    {
        private static GameObject _overlayGo;
        private static int _skillId;

        // UI refs
        private static TMP_Text _titleLabel;
        private static Image _icon;
        private static TMP_Text _lblBaseDmg;
        private static TMP_Text _lblBaseCd;
        private static TMP_Text _lblEffDmg;
        private static TMP_Text _lblEffCd;

        // enhance
        private static TMP_Text _lblEnhLevel;
        private static TMP_Text _lblEnhCost;
        private static Button _btnEnhance;
        private static TMP_Text _btnEnhanceLabel;

        // awaken
        private static TMP_Text _lblAwkLevel;
        private static TMP_Text _lblAwkCost;
        private static Button _btnAwaken;
        private static TMP_Text _btnAwakenLabel;

        // reset
        private static TMP_Text _lblResetRefund;
        private static Button _btnReset;

        public static bool IsOpen => _overlayGo != null && _overlayGo.activeSelf;

        public static void Show(int skillId)
        {
            _skillId = skillId;
            EnsureBuilt();
            if (_overlayGo == null) return;

            RefreshContent();
            _overlayGo.SetActive(true);
            _overlayGo.transform.SetAsLastSibling();
        }

        public static void Hide()
        {
            if (_overlayGo == null) return;
            _overlayGo.SetActive(false);
            if (MageTowerHudController.Instance != null)
                MageTowerHudController.Instance.RefreshSlots();
        }

        private static void EnsureBuilt()
        {
            if (_overlayGo != null) return;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerOverlays == null) return;

            // ── 오버레이 딤 (mt-detail-overlay: black@65%, 바깥 탭 → 닫기) ──
            var dim = UguiRuntimeFactory.Box(mgr.LayerOverlays, "MageTowerDetailOverlay", UguiTheme.DimMedium, rounded: false, raycastTarget: true);
            UguiRuntimeFactory.Stretch(dim.rectTransform);
            _overlayGo = dim.gameObject;

            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Hide);

            // ── 패널 (mt-detail-panel: max-width 600, bg #231E2D@96%, radius 18, padding 22, gap 14) ──
            // 다른 패널과 동일한 어두운 배경 + 금색 픽셀 프레임
            var panel = UguiRuntimeFactory.PixelWindow(dim.transform, "Panel");
            var panelRt = panel.rectTransform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(600f, 0f);
            UguiRuntimeFactory.VerticalLayout(panel.gameObject, 14f, new RectOffset(22, 22, 22, 22));
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // header (title + close)
            var header = UguiRuntimeFactory.Container(panel.transform, "Header");
            UguiRuntimeFactory.HorizontalLayout(header.gameObject, 8f, null, TextAnchor.MiddleLeft);
            UguiRuntimeFactory.Preferred(header, height: 60f);

            _titleLabel = UguiRuntimeFactory.Label(header, "스킬", 32f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Flexible(_titleLabel, 1f);

            var closeBg = UguiRuntimeFactory.Box(header, "BtnClose", UguiTheme.SurfaceMid, rounded: true, raycastTarget: true);
            UguiRuntimeFactory.Preferred(closeBg, width: 60f, height: 60f);
            var closeBtn = closeBg.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.colors = UguiTheme.MakeColorBlock();
            closeBg.gameObject.AddComponent<PlayClickSfxOnClick>();
            closeBtn.onClick.AddListener(Hide);
            var closeLbl = UguiRuntimeFactory.Label(closeBg.transform, "X", 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Center, bold: true);
            UguiRuntimeFactory.Stretch(closeLbl.rectTransform);

            // icon + stats row
            var iconRow = UguiRuntimeFactory.Container(panel.transform, "IconRow");
            UguiRuntimeFactory.HorizontalLayout(iconRow.gameObject, 16f, null, TextAnchor.UpperLeft);
            UguiRuntimeFactory.Preferred(iconRow, height: 130f);

            var iconBg = UguiRuntimeFactory.Box(iconRow, "IconBg", UguiTheme.SurfaceLight, rounded: true);
            var iconLe = UguiRuntimeFactory.Preferred(iconBg, width: 90f, height: 90f);
            iconLe.minWidth = 90f;
            _icon = UguiRuntimeFactory.Box(iconBg.transform, "Icon", Color.white, rounded: false);
            _icon.enabled = false;   // 스프라이트가 붙기 전엔 비활성 (흰 박스 방지)
            _icon.preserveAspect = true;
            UguiRuntimeFactory.Stretch(_icon.rectTransform);
            _icon.rectTransform.offsetMin = new Vector2(6f, 6f);
            _icon.rectTransform.offsetMax = new Vector2(-6f, -6f);

            var statsCol = UguiRuntimeFactory.Container(iconRow, "Stats");
            UguiRuntimeFactory.VerticalLayout(statsCol.gameObject, 4f);
            UguiRuntimeFactory.Flexible(statsCol, 1f);

            _lblBaseDmg = MakeStatLabel(statsCol, new Color(1f, 1f, 1f, 0.70f));
            _lblEffDmg = MakeStatLabel(statsCol, UguiTheme.TextPrimary);
            _lblBaseCd = MakeStatLabel(statsCol, new Color(1f, 1f, 1f, 0.70f));
            _lblEffCd = MakeStatLabel(statsCol, UguiTheme.TextPrimary);

            // enhance section
            var enhSection = MakeSection(panel.transform, "강화", out var enhContent);
            _lblEnhLevel = MakeStatLabel(enhContent, new Color(1f, 1f, 1f, 0.85f));
            _lblEnhCost = MakeStatLabel(enhContent, new Color(1f, 1f, 1f, 0.85f));
            _btnEnhance = MakeActionButton(enhContent, "강화하기",
                new Color(80f / 255f, 140f / 255f, 220f / 255f, 0.70f), OnEnhanceClicked, out _btnEnhanceLabel);

            // awaken section
            var awkSection = MakeSection(panel.transform, "각성", out var awkContent);
            _lblAwkLevel = MakeStatLabel(awkContent, new Color(1f, 1f, 1f, 0.85f));
            _lblAwkCost = MakeStatLabel(awkContent, new Color(1f, 1f, 1f, 0.85f));
            _btnAwaken = MakeActionButton(awkContent, "각성하기",
                new Color(160f / 255f, 80f / 255f, 220f / 255f, 0.70f), OnAwakenClicked, out _btnAwakenLabel);

            // reset section
            var resetSection = MakeSection(panel.transform, null, out var resetContent);
            _lblResetRefund = MakeStatLabel(resetContent, new Color(1f, 1f, 1f, 0.85f));
            _btnReset = MakeActionButton(resetContent, "초기화",
                new Color(200f / 255f, 70f / 255f, 70f / 255f, 0.60f), OnResetClicked, out _);

            _overlayGo.SetActive(false);
        }

        private static TMP_Text MakeStatLabel(RectTransform parent, Color color)
        {
            var lbl = UguiRuntimeFactory.Label(parent, "", 24f, color);
            UguiRuntimeFactory.Preferred(lbl, height: 32f);
            return lbl;
        }

        /// <summary>mt-detail-section: bg black@25% radius12 padding12.</summary>
        private static Image MakeSection(Transform parent, string title, out RectTransform content)
        {
            var section = UguiRuntimeFactory.Box(parent, "Section", new Color(0f, 0f, 0f, 0.25f), rounded: true);
            UguiRuntimeFactory.VerticalLayout(section.gameObject, 6f, new RectOffset(12, 12, 12, 12));

            if (!string.IsNullOrEmpty(title))
            {
                var titleLbl = UguiRuntimeFactory.Label(section.transform, title, 26f, UguiTheme.AccentGold, TextAlignmentOptions.Left, bold: true);
                UguiRuntimeFactory.Preferred(titleLbl, height: 34f);
            }

            content = section.rectTransform;
            return section;
        }

        private static Button MakeActionButton(RectTransform parent, string label, Color bg, System.Action onClick, out TMP_Text labelText)
        {
            var btn = UguiRuntimeFactory.TextButton(parent, label, 24f, bg, onClick, out labelText);
            UguiRuntimeFactory.Preferred((RectTransform)btn.transform, height: 62f);
            return btn;
        }

        private static void RefreshContent()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            var so = mgr.GetSkillById(_skillId);
            if (so == null) { Hide(); return; }

            _titleLabel.text = so.nameKor;

            if (so.icon != null)
            {
                _icon.sprite = so.icon;
                _icon.enabled = true;
            }
            else
            {
                _icon.sprite = null;
                _icon.enabled = false;
            }

            int eLv = mgr.GetEnhanceLevel(_skillId);
            int aLv = mgr.GetAwakeningLevel(_skillId);
            float effDmg = mgr.GetEffectiveDamage(_skillId);
            float effCd = mgr.GetEffectiveCooldown(_skillId);

            _lblBaseDmg.text = $"기본 데미지: {so.BaseDamage:F0}";
            _lblBaseCd.text = $"기본 쿨타임: {so.baseCooldown:F1}s";
            _lblEffDmg.text = $"최종 데미지: {effDmg:F0}";
            _lblEffCd.text = $"최종 쿨타임: {effCd:F1}s";

            // enhance
            _lblEnhLevel.text = $"강화 레벨: {eLv} / {so.maxEnhanceLevel}";
            int enhCost = mgr.GetEnhanceCost(_skillId);
            EconomyBridge.TryGetAmount(eCurrency.ArcaneKnowledge, out long ak);
            _lblEnhCost.text = $"비용: {enhCost} AK (보유: {ak})";
            _btnEnhance.interactable = mgr.CanEnhance(_skillId);
            _btnEnhanceLabel.text = eLv >= so.maxEnhanceLevel ? "최대 레벨" : "강화하기";

            // awaken
            _lblAwkLevel.text = $"각성 레벨: {aLv} / {so.maxAwakeningLevel}";
            int awkCost = mgr.GetAwakeningCost(_skillId);
            int frags = mgr.GetFragments(_skillId);
            _lblAwkCost.text = $"비용: 파편 {awkCost}개 (보유: {frags})";
            _btnAwaken.interactable = mgr.CanAwaken(_skillId);
            _btnAwakenLabel.text = aLv >= so.maxAwakeningLevel ? "최대 각성" : "각성하기";

            // reset
            int refund = mgr.GetResetRefund(_skillId);
            _lblResetRefund.text = $"초기화 시 AK {refund} 반환 (80%)";
            _btnReset.interactable = mgr.CanReset(_skillId);
        }

        private static void OnEnhanceClicked()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;
            mgr.Enhance(_skillId);
            RefreshContent();
        }

        private static void OnAwakenClicked()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;
            mgr.Awaken(_skillId);
            RefreshContent();
        }

        private static void OnResetClicked()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;
            mgr.ResetEnhance(_skillId);
            RefreshContent();
        }
    }
}
