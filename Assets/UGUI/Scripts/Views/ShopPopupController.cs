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
        static UIManager _host;
        static int _tab;
        static long _revision = -1, _account = -1;
        static NumberStyle _numberStyle;
        static ShopAdRewardService.Claim _claim;
        static bool _busy, _adBusy;
        static string _day;
        static int _adRevision = -1;
        public static bool IsOpen => _view != null && _view.isActiveAndEnabled;

        // 광고 보상은 서버가 지급한다(2026-11-01). 결과는 ShopAdRewardService.ClaimCompleted로 온다.
        static ShopPopupController() => ShopAdRewardService.ClaimCompleted += OnClaimCompleted;

        public static void Populate(ShopPopupView view)
        {
            if (view == null) return;
            Detach(_view);
            _view = view;
            _host = UIManager.Instance;
            view.OnClosed = () => Detach(view);
            if (!view.Bound)
            {
                for (int i = 0; i < view.tabs.Length; i++)
                {
                    int index = i;
                    view.tabs[i].onClick.AddListener(() => { if (_view == view) SelectTab(index); });
                }
                foreach (var row in view.offers)
                {
                    string id = row.offerId;
                    row.action.onClick.AddListener(() => { if (_view == view) Activate(id); });
                }
                view.Bound = true;
                SelectTab(_tab);
            }
            else _tab = view.SelectedTab;
            _revision = -1;
            Refresh();
            LocalProgression.Changed += Refresh;
            if (_host != null) _host.FrameTick += Refresh;
        }

        internal static void Restore(ShopPopupView view)
        {
            if (_view != view) Populate(view);
        }

        public static void Hide()
        {
            var host = UIManager.Instance;
            if (_view != null && host != null && host.IsTopPanel(_view)) host.PopPanel();
        }

        internal static void Detach(ShopPopupView view)
        {
            if (_view != view) return;
            LocalProgression.Changed -= Refresh;
            if (_host != null) _host.FrameTick -= Refresh;
            _view = null;
            _host = null;
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
            string adUnitId = claim.OfferId == "rewarded.daily_equipment"
                ? RewardedAdService.ShopEquipmentAdUnitId : RewardedAdService.ShopMageAdUnitId;
            RewardedAdService.Show(adUnitId, (result, error) =>
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

        // 진행 중인 클레임의 서버 결과만 반영한다.
        static void OnClaimCompleted(ShopAdRewardService.Claim claim, RewardResult result, string message)
        {
            if (!ReferenceEquals(_claim, claim)) return;
            HandleReward(claim, result, message);
        }

        static void HandleReward(ShopAdRewardService.Claim claim, RewardResult result, string message = null)
        {
            // 서버에 요청을 보냈다. "처리 중…"을 유지하고 ClaimCompleted를 기다린다.
            if (result.Status == RewardStatus.Pending) { Invalidate(); return; }
            _busy = false;
            // shortcut: unsaved earned rewards survive panel closure, not process termination.
            // 서버가 거절(InvalidReward)했으면 다시 보내도 같으므로 클레임을 버린다. 저장·전송 실패만 "보상 다시 받기"로 남긴다.
            if (result.Succeeded || result.Status == RewardStatus.Stale || result.Status == RewardStatus.NothingAvailable ||
                result.Status == RewardStatus.InvalidReward)
                _claim = null;
            Invalidate();
            var host = UIManager.Instance;
            if (host == null || !ShopAdRewardService.IsCurrentAccount(claim)) return;
            // 지급 결과는 상점을 닫았어도 보여 준다(요청 중 닫을 수 있다).
            if (result.Succeeded) { RewardPopupView.ShowRewards(host.LayerPopups, result.Rewards); return; }
            if (!IsOpen) return;
            bool retry = _claim != null && _claim.Earned;
            if (message != null) host.ShowToast(retry ? message + " 보상 다시 받기를 눌러 주세요." : message);
            else if (retry) host.ShowToast("보상을 저장하지 못했습니다. 보상 다시 받기를 눌러 주세요.");
            else RewardPresentation.Report(result);
        }

        static void Invalidate() { _revision = -1; Refresh(); }

        static void SelectTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, _view.pages.Length - 1);
            _view.SelectedTab = _tab;
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
                _numberStyle == NumberNotation.Style && _day == day && _adBusy == adBusy && _adRevision == ShopAdRewardService.Revision) return;
            _revision = state.Revision; _numberStyle = NumberNotation.Style;
            _account = LocalProgression.AccountGeneration;
            _day = day; _adBusy = adBusy; _adRevision = ShopAdRewardService.Revision;
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
