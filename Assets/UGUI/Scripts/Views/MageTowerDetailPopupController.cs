using UnityEngine;
using KingdomIdle.MageTower;
using Scripts.Core;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 마탑 스킬 상세 팝업 — 강화/각성/초기화 (프리팹 기반).
    /// 프리팹 Panel_MageTowerDetail(=MageTowerDetailPopupView)을 1회 인스턴스화해 캐시하고,
    /// View 참조로 값만 세팅한다. 코드로 UI 구조를 생성하지 않는다(런타임 코드빌드 제거 완료).
    /// 고정 구조라 반복 셀은 없다.
    /// </summary>
    public static class MageTowerDetailPopupController
    {
        private static MageTowerDetailPopupView _view;
        private static int _skillId;

        public static bool IsOpen => _view != null && _view.gameObject.activeSelf;

        public static void Show(int skillId)
        {
            _skillId = skillId;
            if (!EnsureBuilt()) return;

            RefreshContent();
            _view.gameObject.SetActive(true);
            _view.transform.SetAsLastSibling();
        }

        public static void Hide()
        {
            if (_view == null) return;
            _view.gameObject.SetActive(false);
            // (좌측 스킬 슬롯 HUD 제거됨 — 강화/각성 결과는 별도 HUD 갱신이 필요 없다)
        }

        private static bool EnsureBuilt()
        {
            if (_view != null) return true;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerOverlays == null || mgr.Catalog == null || mgr.Catalog.popupMageTowerDetail == null)
            {
                Debug.LogWarning("[MageTowerDetailPopup] 카탈로그의 popupMageTowerDetail 프리팹이 없습니다.");
                return false;
            }

            var go = Object.Instantiate(mgr.Catalog.popupMageTowerDetail, mgr.LayerOverlays, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            _view = go.GetComponent<MageTowerDetailPopupView>();
            if (_view == null)
            {
                Debug.LogError("[MageTowerDetailPopup] MageTowerDetailPopupView 컴포넌트가 없습니다.");
                Object.Destroy(go);
                return false;
            }

            if (_view.backdropButton != null) _view.backdropButton.onClick.AddListener(Hide);
            if (_view.closeButton != null) _view.closeButton.onClick.AddListener(Hide);
            if (_view.btnEnhance != null) _view.btnEnhance.onClick.AddListener(OnEnhanceClicked);
            if (_view.btnAwaken != null) _view.btnAwaken.onClick.AddListener(OnAwakenClicked);
            if (_view.btnReset != null) _view.btnReset.onClick.AddListener(OnResetClicked);

            _view.gameObject.SetActive(false);
            return true;
        }

        private static void RefreshContent()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null || _view == null) return;

            var so = mgr.GetSkillById(_skillId);
            if (so == null) { Hide(); return; }

            if (_view.titleLabel != null) _view.titleLabel.text = so.nameKor;

            if (_view.icon != null)
            {
                if (so.icon != null)
                {
                    _view.icon.sprite = so.icon;
                    _view.icon.enabled = true;
                }
                else
                {
                    _view.icon.sprite = null;
                    _view.icon.enabled = false;
                }
            }

            int eLv = mgr.GetEnhanceLevel(_skillId);
            int aLv = mgr.GetAwakeningLevel(_skillId);
            float effDmg = mgr.GetEffectiveDamage(_skillId);
            float effCd = mgr.GetEffectiveCooldown(_skillId);

            if (_view.lblBaseDmg != null) _view.lblBaseDmg.text = $"기본 데미지: {so.BaseDamage:F0}";
            if (_view.lblBaseCd != null) _view.lblBaseCd.text = $"기본 쿨타임: {so.baseCooldown:F1}s";
            if (_view.lblEffDmg != null) _view.lblEffDmg.text = $"최종 데미지: {effDmg:F0}";
            if (_view.lblEffCd != null) _view.lblEffCd.text = $"최종 쿨타임: {effCd:F1}s";

            // enhance
            if (_view.lblEnhLevel != null) _view.lblEnhLevel.text = $"강화 레벨: {eLv} / {so.maxEnhanceLevel}";
            int enhCost = mgr.GetEnhanceCost(_skillId);
            EconomyBridge.TryGetAmount(eCurrency.ArcaneKnowledge, out long ak);
            if (_view.lblEnhCost != null) _view.lblEnhCost.text = $"비용: {enhCost} AK (보유: {ak})";
            if (_view.btnEnhance != null) _view.btnEnhance.interactable = mgr.CanEnhance(_skillId);
            if (_view.btnEnhanceLabel != null) _view.btnEnhanceLabel.text = eLv >= so.maxEnhanceLevel ? "최대 레벨" : "강화하기";

            // awaken
            if (_view.lblAwkLevel != null) _view.lblAwkLevel.text = $"각성 레벨: {aLv} / {so.maxAwakeningLevel}";
            int awkCost = mgr.GetAwakeningCost(_skillId);
            int frags = mgr.GetFragments(_skillId);
            if (_view.lblAwkCost != null) _view.lblAwkCost.text = $"비용: 파편 {awkCost}개 (보유: {frags})";
            if (_view.btnAwaken != null) _view.btnAwaken.interactable = mgr.CanAwaken(_skillId);
            if (_view.btnAwakenLabel != null) _view.btnAwakenLabel.text = aLv >= so.maxAwakeningLevel ? "최대 각성" : "각성하기";

            // reset
            int refund = mgr.GetResetRefund(_skillId);
            if (_view.lblResetRefund != null) _view.lblResetRefund.text = $"초기화 시 AK {refund} 반환 (80%)";
            if (_view.btnReset != null) _view.btnReset.interactable = mgr.CanReset(_skillId);
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
