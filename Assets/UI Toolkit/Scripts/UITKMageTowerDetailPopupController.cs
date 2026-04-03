using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.MageTower;
using Scripts.Core;
using Scripts.Wallets;

namespace KingdomIdle.UIToolkit
{
    // 마탑 스킬 상세 팝업 (강화/각성/초기화)
    public static class UITKMageTowerDetailPopupController
    {
        private static VisualElement _overlay;
        private static VisualElement _panel;
        private static int _skillId;

        // UI refs
        private static Label _titleLabel;
        private static VisualElement _iconVe;
        private static Label _lblBaseDmg;
        private static Label _lblBaseCd;
        private static Label _lblEffDmg;
        private static Label _lblEffCd;

        // enhance
        private static Label _lblEnhLevel;
        private static Label _lblEnhCost;
        private static Button _btnEnhance;

        // awaken
        private static Label _lblAwkLevel;
        private static Label _lblAwkCost;
        private static Button _btnAwaken;

        // reset
        private static Label _lblResetRefund;
        private static Button _btnReset;

        public static bool IsOpen => _overlay != null && !_overlay.ClassListContains("hidden");

        public static void Show(int skillId)
        {
            _skillId = skillId;
            EnsureBuilt();
            RefreshContent();
            _overlay.RemoveFromClassList("hidden");
            _overlay.BringToFront();
        }

        public static void Hide()
        {
            if (_overlay == null) return;
            _overlay.AddToClassList("hidden");
            if (UITKMageTowerHudController.Instance != null)
                UITKMageTowerHudController.Instance.RefreshSlots();
        }

        private static void EnsureBuilt()
        {
            if (_overlay != null) return;

            var mgr = UITKUIManager.Instance;
            if (mgr == null) return;
            var uiDoc = mgr.GetComponent<UIDocument>();
            if (uiDoc == null) return;
            var root = uiDoc.rootVisualElement;
            var overlays = root?.Q<VisualElement>("Layer_Overlays");
            if (overlays == null) return;

            _overlay = new VisualElement();
            _overlay.AddToClassList("mt-detail-overlay");
            _overlay.pickingMode = PickingMode.Position;

            _panel = new VisualElement();
            _panel.AddToClassList("mt-detail-panel");
            _panel.pickingMode = PickingMode.Position;
            _panel.RegisterCallback<PointerDownEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);

            // header
            var header = new VisualElement();
            header.AddToClassList("mt-detail-header");

            _titleLabel = new Label("스킬");
            _titleLabel.AddToClassList("mt-detail-title");

            var closeBtn = new Button(Hide);
            closeBtn.text = "✕";
            closeBtn.AddToClassList("mt-detail-close");

            header.Add(_titleLabel);
            header.Add(closeBtn);
            _panel.Add(header);

            // icon + stats row
            var iconRow = new VisualElement();
            iconRow.AddToClassList("mt-detail-icon-row");

            _iconVe = new VisualElement();
            _iconVe.AddToClassList("mt-detail-icon");

            var statsCol = new VisualElement();
            statsCol.AddToClassList("mt-detail-stats");

            _lblBaseDmg = new Label();
            _lblBaseDmg.AddToClassList("mt-detail-stat-label");
            _lblBaseCd = new Label();
            _lblBaseCd.AddToClassList("mt-detail-stat-label");
            _lblEffDmg = new Label();
            _lblEffDmg.AddToClassList("mt-detail-stat-value");
            _lblEffCd = new Label();
            _lblEffCd.AddToClassList("mt-detail-stat-value");

            statsCol.Add(_lblBaseDmg);
            statsCol.Add(_lblEffDmg);
            statsCol.Add(_lblBaseCd);
            statsCol.Add(_lblEffCd);

            iconRow.Add(_iconVe);
            iconRow.Add(statsCol);
            _panel.Add(iconRow);

            // enhance section
            var enhSection = new VisualElement();
            enhSection.AddToClassList("mt-detail-section");

            var enhTitle = new Label("강화");
            enhTitle.AddToClassList("mt-detail-section-title");

            _lblEnhLevel = new Label();
            _lblEnhLevel.AddToClassList("mt-detail-section-info");
            _lblEnhCost = new Label();
            _lblEnhCost.AddToClassList("mt-detail-section-info");

            _btnEnhance = new Button(OnEnhanceClicked);
            _btnEnhance.text = "강화하기";
            _btnEnhance.AddToClassList("mt-detail-btn");
            _btnEnhance.AddToClassList("mt-detail-btn-enhance");

            enhSection.Add(enhTitle);
            enhSection.Add(_lblEnhLevel);
            enhSection.Add(_lblEnhCost);
            enhSection.Add(_btnEnhance);
            _panel.Add(enhSection);

            // awaken section
            var awkSection = new VisualElement();
            awkSection.AddToClassList("mt-detail-section");

            var awkTitle = new Label("각성");
            awkTitle.AddToClassList("mt-detail-section-title");

            _lblAwkLevel = new Label();
            _lblAwkLevel.AddToClassList("mt-detail-section-info");
            _lblAwkCost = new Label();
            _lblAwkCost.AddToClassList("mt-detail-section-info");

            _btnAwaken = new Button(OnAwakenClicked);
            _btnAwaken.text = "각성하기";
            _btnAwaken.AddToClassList("mt-detail-btn");
            _btnAwaken.AddToClassList("mt-detail-btn-awaken");

            awkSection.Add(awkTitle);
            awkSection.Add(_lblAwkLevel);
            awkSection.Add(_lblAwkCost);
            awkSection.Add(_btnAwaken);
            _panel.Add(awkSection);

            // reset row
            var resetRow = new VisualElement();
            resetRow.AddToClassList("mt-detail-btn-row");

            _lblResetRefund = new Label();
            _lblResetRefund.AddToClassList("mt-detail-section-info");

            _btnReset = new Button(OnResetClicked);
            _btnReset.text = "초기화";
            _btnReset.AddToClassList("mt-detail-btn");
            _btnReset.AddToClassList("mt-detail-btn-reset");

            var resetSection = new VisualElement();
            resetSection.AddToClassList("mt-detail-section");
            resetSection.Add(_lblResetRefund);
            resetSection.Add(_btnReset);
            _panel.Add(resetSection);

            _overlay.Add(_panel);

            // close on backdrop
            _overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                var target = evt.target as VisualElement;
                if (target == _overlay)
                    Hide();
            }, TrickleDown.TrickleDown);

            _overlay.AddToClassList("hidden");
            overlays.Add(_overlay);
        }

        private static void RefreshContent()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            var so = mgr.GetSkillById(_skillId);
            if (so == null) { Hide(); return; }

            _titleLabel.text = so.nameKor;

            if (so.icon != null)
                _iconVe.style.backgroundImage = new StyleBackground(so.icon);
            else
                _iconVe.style.backgroundImage = StyleKeyword.None;

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
            _btnEnhance.SetEnabled(mgr.CanEnhance(_skillId));
            _btnEnhance.text = eLv >= so.maxEnhanceLevel ? "최대 레벨" : "강화하기";

            // awaken
            _lblAwkLevel.text = $"각성 레벨: {aLv} / {so.maxAwakeningLevel}";
            int awkCost = mgr.GetAwakeningCost(_skillId);
            int frags = mgr.GetFragments(_skillId);
            _lblAwkCost.text = $"비용: 파편 {awkCost}개 (보유: {frags})";
            _btnAwaken.SetEnabled(mgr.CanAwaken(_skillId));
            _btnAwaken.text = aLv >= so.maxAwakeningLevel ? "최대 각성" : "각성하기";

            // reset
            int refund = mgr.GetResetRefund(_skillId);
            _lblResetRefund.text = $"초기화 시 AK {refund} 반환 (80%)";
            _btnReset.SetEnabled(mgr.CanReset(_skillId));
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
