using KingdomIdle.Balance;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>상점 표시와 연동 준비 중인 상품 안내.</summary>
    public static class ShopPopupController
    {
        static ShopPopupView _view;
        static int _tab;
        static long _revision = -1, _account = -1;
        static NumberStyle _numberStyle;
        static string _day;
        public static bool IsOpen => _view != null && _view.gameObject.activeSelf;
#if UNITY_EDITOR
        internal static bool HasPendingAdForTesting => false;
#endif

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
                    row.action.onClick.AddListener(() => Activate(id));
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

        internal static void Activate(string id) => Preview(id);

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
            string day = ShopAdRewardService.Today;
            if (_account == LocalProgression.AccountGeneration && _revision == state.Revision &&
                _numberStyle == NumberNotation.Style && _day == day) return;
            _revision = state.Revision; _numberStyle = NumberNotation.Style;
            _account = LocalProgression.AccountGeneration;
            _day = day;
            Set(_view.wallet, "보유 고대주화  " + NumberNotation.Format(LocalProgression.Balance(eCurrency.AncientCoin)));
            Set(_view.income, "골드 기준 수입  " + NumberNotation.Format((long)decimal.Floor(ShopCatalog.GoldPerMinute(state))) + " / 분 · 루비 효과 포함");
            foreach (var row in _view.offers)
            {
                var offer = ShopCatalog.Find(row.offerId);
                if (offer == null) continue;
                if (offer.GoldMinutes > 0)
                    Set(row.amount, "골드 " + NumberNotation.Format(ShopCatalog.PreviewGold(offer, state)) + "개");
                if (!ShopAdRewardService.TryGetCurrency(offer.Id, out var currency)) continue;
                bool claimed = ShopAdRewardService.IsClaimed(offer.Id, day);
                row.action.interactable = true;
                Set(row.price, "준비 중");
                Set(row.amount, "보유 " + NumberNotation.Format(LocalProgression.Balance(currency)) + "장 · 오늘 " + (claimed ? "1/1" : "0/1"));
            }
        }

        static void Set(TMPro.TMP_Text label, string text) { if (label != null && label.text != text) label.text = text; }
    }
}
