using KingdomIdle.Balance;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Preview-only presentation. An authoritative commerce adapter will own future checkout and grants.</summary>
    public static class ShopPopupController
    {
        static ShopPopupView _view;
        static int _tab;
        static long _revision = -1;
        static NumberStyle _numberStyle;
        public static bool IsOpen => _view != null && _view.gameObject.activeSelf;

        public static void Show()
        {
            var host = UIManager.Instance;
            if (host == null || host.Catalog == null || host.Catalog.popupShop == null) return;
            if (_view == null)
            {
                var instance = Object.Instantiate(host.Catalog.popupShop, host.LayerPopups, false);
                var rect = (RectTransform)instance.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                _view = instance.GetComponent<ShopPopupView>();
                _view.backdrop.onClick.AddListener(Hide); _view.close.onClick.AddListener(Hide);
                ModalBackHandler.Bind(instance, Hide);
                for (int i = 0; i < _view.tabs.Length; i++) { int index = i; _view.tabs[i].onClick.AddListener(() => SelectTab(index)); }
                foreach (var row in _view.offers)
                {
                    string id = row.offerId;
                    row.action.onClick.AddListener(() => Preview(id));
                }
            }
            _view.gameObject.SetActive(true); _view.transform.SetAsLastSibling();
            _revision = -1;
            SelectTab(_tab); Refresh();
            LocalProgression.Changed -= Refresh; LocalProgression.Changed += Refresh;
            host.FrameTick -= Refresh; host.FrameTick += Refresh;
            UITween.PopIn(_view.panel);
        }

        public static void Hide()
        {
            LocalProgression.Changed -= Refresh;
            if (UIManager.Instance != null) UIManager.Instance.FrameTick -= Refresh;
            if (_view != null) _view.gameObject.SetActive(false);
        }

        public static void Preview(string id) => UIManager.Instance?.ShowToast(ShopCatalog.PreviewMessage(id));

        static void SelectTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, _view.pages.Length - 1);
            for (int i = 0; i < _view.pages.Length; i++)
            {
                bool active = i == _tab; _view.pages[i].SetActive(active);
                _view.tabs[i].GetComponent<Image>().color = active ? UguiTheme.Bronze : UguiTheme.RusticSurfaceDark;
            }
            _view.scroll.StopMovement(); Canvas.ForceUpdateCanvases(); _view.scroll.verticalNormalizedPosition = 1;
        }

        static void Refresh()
        {
            if (!IsOpen || !LocalProgression.IsReady) return;
            var state = LocalProgression.State;
            if (_revision == state.Revision && _numberStyle == NumberNotation.Style) return;
            _revision = state.Revision; _numberStyle = NumberNotation.Style;
            Set(_view.wallet, "보유 고대주화  " + NumberNotation.Format(LocalProgression.Balance(eCurrency.AncientCoin)));
            Set(_view.income, "골드 기준 수입  " + NumberNotation.Format((long)decimal.Floor(ShopCatalog.GoldPerMinute(state))) + " / 분 · 루비 효과 포함");
            foreach (var row in _view.offers)
            {
                var offer = ShopCatalog.Find(row.offerId);
                if (offer == null || offer.GoldMinutes <= 0) continue;
                Set(row.amount, "골드 " + NumberNotation.Format(ShopCatalog.PreviewGold(offer, state)) + "개");
            }
        }

        static void Set(TMPro.TMP_Text label, string text) { if (label != null && label.text != text) label.text = text; }
    }
}
