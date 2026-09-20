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

        public static void Dismantle(EquipmentEconomy.DismantlePlan plan, Action refresh)
        {
            if (plan.Count == 0) { UIManager.Instance?.ShowToast("분해할 장비가 없습니다. 잠금·장착·강화 장비는 일괄 분해에서 제외됩니다."); return; }
            var view = Create("장비 분해", $"장비 {NumberNotation.Exact(plan.Count)}개\n강화석 +{NumberNotation.Exact(plan.Stones)}\n\n분해한 장비는 되돌릴 수 없습니다.\n강화에 사용한 강화석은 80% 반환됩니다.", "분해하기");
            if (view == null) return;
            foreach (var toggle in view.rarityToggles) toggle.gameObject.SetActive(false);
            view.panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 620);
            view.confirm.onClick.AddListener(() => {
                bool success = EquipmentManager.Instance != null && EquipmentManager.Instance.Dismantle(plan);
                view.Close();
                UIManager.Instance?.ShowToast(success ? $"강화석 {NumberNotation.Format(plan.Stones)}개 획득" : "장비 상태가 바뀌었습니다. 목록을 확인해 주세요.");
                refresh?.Invoke();
            });
        }

        public static void AutoDismantle(Action refresh)
        {
            var view = Create("습득 시 자동 분해", "앞으로 얻는 장비에 적용합니다.\n기존 장비와 잠금·장착·강화 장비는 보존합니다.\n\n일반 1 · 레어 4 · 에픽 12 강화석", "설정 저장");
            if (view == null) return;
            int mask = LocalProgression.State.AutoDismantleMask;
            for (int i = 0; i < view.rarityToggles.Length; i++) view.rarityToggles[i].SetIsOnWithoutNotify((mask & (1 << i)) != 0);
            view.confirm.onClick.AddListener(() => {
                int next = 0;
                for (int i = 0; i < view.rarityToggles.Length; i++) if (view.rarityToggles[i].isOn) next |= 1 << i;
                bool saved = LocalProgression.Execute("equipment-auto-dismantle", s => { s.AutoDismantleMask = next; return true; });
                if (!saved) { UIManager.Instance?.ShowToast("설정을 저장하지 못했습니다."); return; }
                view.Close(); refresh?.Invoke();
            });
        }

        private void Close() { gameObject.SetActive(false); Destroy(gameObject); }
    }
}
