using KingdomIdle.Balance;
using KingdomIdle.Ads;
using KingdomIdle.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>상점 표시와 일일 광고 입력. 구매·교환은 아직 미리보기다.</summary>
    public static class ShopPopupController
    {
        static ShopPopupView _view;
        static int _tab;
        static long _revision = -1, _account = -1;
        static NumberStyle _numberStyle;
        static ShopAdRewardService.Claim _claim;
        static bool _busy, _adBusy;
        static string _day;
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

        internal static void Activate(string id)
        {
            if (ShopCatalog.Find(id)?.Payment != ShopPayment.RewardedAd) { Preview(id); return; }
            if (!IsOpen || _busy || RewardedAdService.IsBusy) return;
            if (_claim != null && !ShopAdRewardService.IsCurrentAccount(_claim)) _claim = null;
            if (_claim != null && _claim.Earned)
            {
                if (_claim.OfferId == id)
                {
                    _busy = true; Invalidate();
                    HandleReward(_claim, ShopAdRewardService.TryGrant(_claim));
                }
                return;
            }
            var status = ShopAdRewardService.Begin(id, out var claim);
            if (status != RewardStatus.Success)
            {
                RewardPresentation.Report(new RewardResult(status));
                Invalidate(); return;
            }
            _claim = claim; _busy = true; Invalidate();
            // shortcut: keep Google's test unit until production ads and consent are configured.
            RewardedAdService.Show("ca-app-pub-3940256099942544/5224354917", (result, error) =>
            {
                if (!ReferenceEquals(_claim, claim) || !_busy) return;
                if (result == RewardedAdResult.Completed)
                    HandleReward(claim, ShopAdRewardService.FinishAd(claim, result));
                else
                {
                    _busy = false; _claim = null; Invalidate();
                    if (IsOpen && result != RewardedAdResult.Skipped)
                        UIManager.Instance?.ShowToast("광고를 표시하지 못했습니다. 잠시 후 다시 시도해 주세요.");
                }
            });
        }

        static void HandleReward(ShopAdRewardService.Claim claim, RewardResult result)
        {
            _busy = false;
            // shortcut: unsaved earned rewards survive popup closure, not process termination.
            if (result.Succeeded || result.Status == RewardStatus.Stale || result.Status == RewardStatus.NothingAvailable)
                _claim = null;
            Invalidate();
            if (!IsOpen || !ShopAdRewardService.IsCurrentAccount(claim)) return;
            if (result.Succeeded) RewardPopupView.ShowRewards(UIManager.Instance.LayerPopups, result.Rewards);
            else if (_claim != null && _claim.Earned)
                UIManager.Instance.ShowToast("보상을 저장하지 못했습니다. 보상 다시 받기를 눌러 주세요.");
            else RewardPresentation.Report(result);
        }

        static void Invalidate() { _revision = -1; Refresh(); }

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
            if (!_busy && _claim != null && !ShopAdRewardService.IsCurrentAccount(_claim))
            { _claim = null; _revision = -1; }
            var state = LocalProgression.State;
            string day = ShopAdRewardService.Today;
            bool adBusy = RewardedAdService.IsBusy;
            if (_account == LocalProgression.AccountGeneration && _revision == state.Revision &&
                _numberStyle == NumberNotation.Style && _day == day && _adBusy == adBusy) return;
            _revision = state.Revision; _numberStyle = NumberNotation.Style;
            _account = LocalProgression.AccountGeneration;
            _day = day; _adBusy = adBusy;
            Set(_view.wallet, "보유 고대주화  " + NumberNotation.Format(LocalProgression.Balance(eCurrency.AncientCoin)));
            Set(_view.income, "골드 기준 수입  " + NumberNotation.Format((long)decimal.Floor(ShopCatalog.GoldPerMinute(state))) + " / 분 · 루비 효과 포함");
            foreach (var row in _view.offers)
            {
                var offer = ShopCatalog.Find(row.offerId);
                if (offer == null) continue;
                if (offer.GoldMinutes > 0)
                    Set(row.amount, "골드 " + NumberNotation.Format(ShopCatalog.PreviewGold(offer, state)) + "개");
                if (!ShopAdRewardService.TryGetCurrency(offer.Id, out var currency)) continue;
                bool retry = _claim != null && _claim.Earned && _claim.OfferId == offer.Id;
                bool claimed = ShopAdRewardService.IsClaimed(offer.Id, day);
                bool blocked = _busy || adBusy || (_claim != null && _claim.Earned && !retry);
                row.action.interactable = !blocked && (retry || !claimed);
                Set(row.price, _busy && _claim?.OfferId == offer.Id ? "처리 중…" : retry ? "보상 다시 받기" : claimed ? "오늘 수령 완료" : "광고 보고 받기");
                Set(row.amount, "보유 " + NumberNotation.Format(LocalProgression.Balance(currency)) + "장 · 오늘 " + (claimed ? "1/1" : "0/1"));
            }
        }

        static void Set(TMPro.TMP_Text label, string text) { if (label != null && label.text != text) label.text = text; }
    }
}
