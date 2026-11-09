using System;
using KingdomIdle.Balance;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class EquipmentActionDialog : MonoBehaviour
    {
        public RectTransform panel;
        public TMP_Text title, description;
        public Button backdrop, cancel, confirm;
        public Button recommendation;
        public Toggle[] rarityToggles;
        private static EquipmentActionDialog _open;

        private static EquipmentActionDialog Create(string heading, string body, string action)
        {
            var ui = UIManager.Instance;
            if (ui?.Catalog?.popupEquipmentAction == null) return null;
            if (_open != null) _open.Close();
            var view = Instantiate(ui.Catalog.popupEquipmentAction, ui.LayerOverlays, false).GetComponent<EquipmentActionDialog>();
            _open = view;
            view.transform.SetAsLastSibling();
            view.title.text = heading; view.description.text = body;
            view.confirm.GetComponentInChildren<TMP_Text>().text = action;
            view.backdrop.onClick.AddListener(view.Close); view.cancel.onClick.AddListener(view.Close);
            ModalBackHandler.Bind(view.gameObject, view.Close);
            UITween.PopIn(view.panel);
            return view;
        }

        public static void Dismantle(EquipmentEconomy.DismantlePlan plan, Action refresh, string scope = null)
        {
            if (plan.Count == 0) { UIManager.Instance?.ShowToast("분해할 장비가 없습니다. 잠금·장착·강화 장비는 일괄 분해에서 제외됩니다."); return; }
            string summary = $"장비 {NumberNotation.Exact(plan.Count)}개 · 강화석 +{NumberNotation.Exact(plan.Stones)} (예상)";
            string detail = scope == null ? "강화에 사용한 강화석은 80% 반환됩니다." : $"{scope}\n잠금·장착·강화 장비는 제외됩니다.";
            var view = Create("장비 분해", $"{summary}\n{detail}\n\n분해한 장비는 되돌릴 수 없습니다.", "분해하기");
            if (view == null) return;
            if (view.recommendation != null) view.recommendation.transform.parent.gameObject.SetActive(false);
            foreach (var toggle in view.rarityToggles) toggle.gameObject.SetActive(false);
            view.panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 620);
            view.confirm.onClick.AddListener(() => {
                // 서버(OnDismantleEquipment)가 보호·수량을 검사하고 환급액을 확정한다. 완료 토스트·목록 갱신은 패널의 완료 이벤트가 한다.
                var result = EquipmentManager.Instance != null ? EquipmentManager.Instance.DismantleEx(plan) : EquipmentManager.EnhancementResult.NetworkNotReady;
                view.Close();
                if (result == EquipmentManager.EnhancementResult.Pending) UIManager.Instance?.ShowToast($"장비 {NumberNotation.Exact(plan.Count)}개 분해 요청 중");
                else { UIManager.Instance?.ShowToast(EquipmentManager.Describe(result)); refresh?.Invoke(); }
            });
        }

        public static void Settings()
        {
            var view = Create("장비 설정", "추천 기준은 누르면 이 기기의 현재 계정에 저장됩니다.\n현재 무기는 공격력만 올립니다.\n\n자동 분해할 등급을 선택하세요. 앞으로 획득할 장비에 적용됩니다.\n일반 1 · 레어 4 · 에픽 12 강화석", "자동 분해 저장");
            if (view == null) return;
            view.cancel.GetComponentInChildren<TMP_Text>().text = "닫기";
            if (view.recommendation != null)
            {
                view.panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1056);
                view.UpdateRecommendationLabel();
                view.recommendation.onClick.AddListener(view.ChangeRecommendation);
            }
            int mask = LocalProgression.State.AutoDismantleMask;
            for (int i = 0; i < view.rarityToggles.Length; i++) view.rarityToggles[i].SetIsOnWithoutNotify((mask & (1 << i)) != 0);
            view.confirm.onClick.AddListener(() => {
                int next = 0;
                for (int i = 0; i < view.rarityToggles.Length; i++) if (view.rarityToggles[i].isOn) next |= 1 << i;
                if (next == LocalProgression.State.AutoDismantleMask) { view.Close(); return; }
                // 서버(OnSetAutoDismantle)에 저장한다. 이후 서버가 지급하는 장비에만 적용된다. 목록 갱신은 패널의 완료 이벤트가 한다.
                var result = EquipmentManager.Instance != null ? EquipmentManager.Instance.SetAutoDismantleEx(next) : EquipmentManager.EnhancementResult.NetworkNotReady;
                if (result != EquipmentManager.EnhancementResult.Pending) { UIManager.Instance?.ShowToast(EquipmentManager.Describe(result)); return; }
                view.Close();
            });
        }

        private void UpdateRecommendationLabel()
        {
            string criterion = LocalProgression.State.EquipmentRecommendation switch
            {
                EquipmentRecommendationCriterion.Attack => "공격력",
                EquipmentRecommendationCriterion.Health => "최대 체력",
                _ => "전투력"
            };
            recommendation.GetComponentInChildren<TMP_Text>().text = $"추천 기준: {criterion}";
        }

        private void ChangeRecommendation()
        {
            bool saved = LocalProgression.Execute("equipment-recommendation", state =>
            {
                state.EquipmentRecommendation = state.EquipmentRecommendation switch
                {
                    EquipmentRecommendationCriterion.Power => EquipmentRecommendationCriterion.Attack,
                    EquipmentRecommendationCriterion.Attack => EquipmentRecommendationCriterion.Health,
                    _ => EquipmentRecommendationCriterion.Power
                };
                return true;
            });
            if (saved) UpdateRecommendationLabel();
            else UIManager.Instance?.ShowToast("추천 기준을 저장하지 못했습니다.");
        }

        private void Close() { gameObject.SetActive(false); Destroy(gameObject); }
    }
}
