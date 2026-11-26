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
        public Toggle masterAuto, autoLockFirst;
        private static EquipmentActionDialog _open;

        private static EquipmentActionDialog Create(string heading, string body, string action)
        {
            var ui = UIManager.Instance;
            if (ui?.Catalog?.popupEquipmentAction == null) return null;
            if (_open != null) _open.Close();
            var go = Instantiate(ui.Catalog.popupEquipmentAction, ui.LayerOverlays, false);
            var view = go.GetComponent<EquipmentActionDialog>();
            if (view == null) { Destroy(go); return null; }
            _open = view;
            view.transform.SetAsLastSibling();
            view.title.text = heading; view.description.text = body;
            view.confirm.GetComponentInChildren<TMP_Text>().text = action;
            view.backdrop.onClick.AddListener(view.Close); view.cancel.onClick.AddListener(view.Close);
            ModalBackHandler.Bind(view.gameObject, view.Close);
            UITween.PopIn(view.panel);
            return view;
        }

        public static bool ShowConfirmation(string heading, string body, string action, Action confirmed)
        {
            var view = Create(heading, body, action);
            if (view == null) return false;
            if (view.recommendation != null) view.recommendation.transform.parent.gameObject.SetActive(false);
            foreach (var toggle in view.rarityToggles) toggle.gameObject.SetActive(false);
            if (view.autoLockFirst != null) view.autoLockFirst.gameObject.SetActive(false);
            if (view.masterAuto != null) view.masterAuto.gameObject.SetActive(false);
            view.panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 620);
            long generation = LocalProgression.AccountGeneration;
            LocalProgression.AccountChanged += view.Close;
            view.confirm.onClick.AddListener(() =>
            {
                if (!view.gameObject.activeSelf) return;
                view.Close();
                if (generation == LocalProgression.AccountGeneration) confirmed?.Invoke();
            });
            return true;
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
            if (view.autoLockFirst != null) view.autoLockFirst.gameObject.SetActive(false);
            if (view.masterAuto != null) view.masterAuto.gameObject.SetActive(false);
            view.panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 620);
            long account = LocalProgression.AccountGeneration;
            LocalProgression.AccountChanged += view.Close;
            view.confirm.onClick.AddListener(() => {
                if (!view.gameObject.activeSelf || LocalProgression.AccountGeneration != account) return;
                // 서버(OnDismantleEquipment)가 보호·수량을 검사하고 환급액을 확정한다. 완료 토스트·목록 갱신은 패널의 완료 이벤트가 한다.
                var result = EquipmentManager.Instance != null ? EquipmentManager.Instance.DismantleEx(plan) : EquipmentManager.EnhancementResult.NetworkNotReady;
                view.Close();
                if (result == EquipmentManager.EnhancementResult.Pending) UIManager.Instance?.ShowToast($"장비 {NumberNotation.Exact(plan.Count)}개 분해 요청 중");
                else { UIManager.Instance?.ShowToast(EquipmentManager.Describe(result)); refresh?.Invoke(); }
            });
        }

        public static void BulkDismantle(Action refresh)
        {
            var view = Create("일괄 분해", "분해할 장비 등급을 선택하세요.", "대상 확인");
            if (view == null) return;
            view.recommendation.transform.parent.gameObject.SetActive(false);
            view.autoLockFirst.gameObject.SetActive(false);
            view.masterAuto.gameObject.SetActive(false);
            view.panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 980);
            EquipmentEconomy.DismantlePlan plan = null;
            void Preview()
            {
                int mask = view.SelectedMask();
                plan = EquipmentEconomy.Preview(code => (mask & (1 << (int)ItemCode.DecodeRarity(code))) != 0);
                view.description.text = $"선택한 장비 {NumberNotation.Format(plan.Count)}개 · 예상 강화석 {NumberNotation.Format(plan.Stones)}개\n잠금·장착·강화 장비는 보호됩니다.";
                view.confirm.interactable = plan.Count > 0;
            }
            foreach (var toggle in view.rarityToggles)
            {
                toggle.SetIsOnWithoutNotify(false);
                toggle.onValueChanged.AddListener(_ => Preview());
            }
            Preview();
            long generation = LocalProgression.AccountGeneration;
            LocalProgression.AccountChanged += view.Close;
            view.confirm.onClick.AddListener(() =>
            {
                view.Close();
                if (generation == LocalProgression.AccountGeneration) Dismantle(plan, refresh, "선택한 등급");
            });
        }

        private int SelectedMask()
        {
            int mask = 0;
            for (int i = 0; i < rarityToggles.Length; i++) if (rarityToggles[i].isOn) mask |= 1 << i;
            return mask;
        }

        public static void Settings()
        {
            var view = Create("장비 자동분해", "선택한 등급의 새 장비를 강화석으로 바꿉니다.", "설정 저장");
            if (view == null) return;
            view.cancel.GetComponentInChildren<TMP_Text>().text = "닫기";
            bool canSetAutoLock = EquipmentManager.IsInstanceInventory && view.autoLockFirst != null;
            if (view.autoLockFirst != null)
            {
                view.autoLockFirst.gameObject.SetActive(canSetAutoLock);
                view.autoLockFirst.SetIsOnWithoutNotify(EquipmentManager.AutoLockFirstEnhancement);
            }
            if (canSetAutoLock) view.confirm.GetComponentInChildren<TMP_Text>().text = "설정 저장";
            if (view.recommendation != null)
            {
                view.panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, canSetAutoLock ? 1280 : 1172);
                view.UpdateRecommendationLabel();
                view.recommendation.onClick.AddListener(view.ChangeRecommendation);
            }
            int mask = LocalProgression.State.AutoDismantleMask;
            int lastMask = mask == 0 ? LocalProgression.State.LastAutoDismantleMask : mask;
            view.masterAuto.SetIsOnWithoutNotify(mask != 0);
            for (int i = 0; i < view.rarityToggles.Length; i++)
            {
                view.rarityToggles[i].SetIsOnWithoutNotify((lastMask & (1 << i)) != 0);
                view.rarityToggles[i].interactable = mask != 0;
            }
            view.masterAuto.onValueChanged.AddListener(on =>
            {
                foreach (var toggle in view.rarityToggles) toggle.interactable = on;
            });
            LocalProgression.AccountChanged += view.Close;
            view.confirm.onClick.AddListener(() => {
                int selected = view.SelectedMask();
                if (selected == 0 && view.masterAuto.isOn) { UIManager.Instance?.ShowToast("자동분해할 등급을 선택하세요."); return; }
                int next = view.masterAuto.isOn ? selected : 0;
                if (selected != 0 && selected != LocalProgression.State.LastAutoDismantleMask)
                    LocalProgression.Execute("equipment-auto-preference", s => { s.LastAutoDismantleMask = selected; return true; });
                bool? autoLock = canSetAutoLock ? view.autoLockFirst.isOn : null;
                if (next == LocalProgression.State.AutoDismantleMask &&
                    (!autoLock.HasValue || autoLock.Value == EquipmentManager.AutoLockFirstEnhancement)) { view.Close(); return; }
                var result = EquipmentManager.Instance != null ? EquipmentManager.Instance.SetAutoDismantleEx(next, autoLock) : EquipmentManager.EnhancementResult.NetworkNotReady;
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

        private void OnDisable() => LocalProgression.AccountChanged -= Close;
        private void Close()
        {
            if (_open == this) _open = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
