using System;
using System.Collections;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

namespace KingdomIdle.Ads
{
    public enum RewardedAdResult { Completed, Skipped, Failed, Busy }

    /// <summary>One rewarded-ad request at a time; callers own eligibility and durable reward claims.</summary>
    public sealed class RewardedAdService : MonoBehaviour
    {
        private static RewardedAdService _instance;
        private RewardedAd _ad;
        private Action<RewardedAdResult, string> _completion;
        private Coroutine _timeout;
        private bool _initialized, _busy, _showing, _earned, _destroyed;
        private int _request;

        public static bool IsBusy => _instance != null && _instance._busy;
        public static bool IsShowing => _instance != null && _instance._showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        public static void Show(string adUnitId, Action<RewardedAdResult, string> onFinished)
        {
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));
            if (string.IsNullOrWhiteSpace(adUnitId))
            {
                onFinished(RewardedAdResult.Failed, "광고 단위 ID가 없습니다.");
                return;
            }
            if (IsBusy)
            {
                onFinished(RewardedAdResult.Busy, "이미 광고를 준비하거나 시청 중입니다.");
                return;
            }
            if (_instance == null)
            {
                _instance = new GameObject(nameof(RewardedAdService)).AddComponent<RewardedAdService>();
                DontDestroyOnLoad(_instance.gameObject);
            }
            _instance.Begin(adUnitId, onFinished);
        }

        private void Begin(string adUnitId, Action<RewardedAdResult, string> onFinished)
        {
            int request = ++_request;
            _completion = onFinished;
            _busy = true;
            _earned = false;
            _timeout = StartCoroutine(WaitForLoad(request));
            try
            {
                if (_initialized) Load(request, adUnitId);
                else MobileAds.Initialize(status => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (!IsCurrent(request)) return;
                    if (status == null)
                    {
                        Finish(RewardedAdResult.Failed, "광고 SDK 초기화에 실패했습니다.");
                        return;
                    }
                    _initialized = true;
                    Load(request, adUnitId);
                }));
            }
            catch (Exception error) { Finish(RewardedAdResult.Failed, error.Message); }
        }

        private void Load(int request, string adUnitId)
        {
            try
            {
                RewardedAd.Load(adUnitId, new AdRequest(), (ad, error) =>
                    MobileAdsEventExecutor.ExecuteInUpdate(() =>
                    {
                        if (!IsCurrent(request)) { ad?.Destroy(); return; }
                        if (error != null || ad == null)
                        {
                            ad?.Destroy();
                            Finish(RewardedAdResult.Failed, error?.GetMessage() ?? "광고가 반환되지 않았습니다.");
                            return;
                        }
                        _ad = ad;
                        Present(request);
                    }));
            }
            catch (Exception error) { Finish(RewardedAdResult.Failed, error.Message); }
        }

        private void Present(int request)
        {
            if (!_ad.CanShowAd())
            {
                Finish(RewardedAdResult.Failed, "광고를 다시 불러와 주세요.");
                return;
            }
            if (_timeout != null) StopCoroutine(_timeout);
            _timeout = null;
            _showing = true;
            // shortcut: Google sends reward before close; revalidate ordering when adding mediation.
            _ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (IsCurrent(request)) Finish(_earned ? RewardedAdResult.Completed : RewardedAdResult.Skipped, null);
            });
            _ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (IsCurrent(request)) Finish(RewardedAdResult.Failed, error.GetMessage());
            });
            try
            {
                _ad.Show(reward => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (IsCurrent(request)) _earned = true;
                }));
            }
            catch (Exception error) { Finish(RewardedAdResult.Failed, error.Message); }
        }

        private IEnumerator WaitForLoad(int request)
        {
            yield return new WaitForSecondsRealtime(60);
            _timeout = null;
            if (IsCurrent(request)) Finish(RewardedAdResult.Failed, "광고 준비 시간이 초과되었습니다. 다시 시도해 주세요.");
        }

        private bool IsCurrent(int request) => !_destroyed && _busy && _request == request;

        private void Finish(RewardedAdResult result, string error)
        {
            if (!_busy) return;
            var completion = _completion;
            _completion = null;
            _busy = _showing = false;
            if (_timeout != null) StopCoroutine(_timeout);
            _timeout = null;
            var ad = _ad;
            _ad = null;
            ad?.Destroy();
            Debug.Log($"[RewardedAd] {result}: {error}");
            completion?.Invoke(result, error);
        }

        private void OnDestroy()
        {
            _destroyed = true;
            _completion = null;
            _ad?.Destroy();
            if (_instance == this) _instance = null;
        }
    }
}
