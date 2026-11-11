using UnityEngine;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using KingdomIdle.UI;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Server.Auth;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 타이틀 화면 로직 (UITKUIManager.BindTitle 이식).
    /// 로그인 팝업 표시, 게스트/Google/Apple 인증, "아무 곳이나 탭" 메인 진입 게이트.
    /// </summary>
    public sealed class TitleScreenController
    {
        private TitleScreenView _view;
        private UIManager _host;
        private long _requestedMainGeneration = -1;
        private TermsConsentDialog _termsDialog;
        private bool _titlePatchPending;

        public void Bind(TitleScreenView view, UIManager host)
        {
            _view = view;
            _host = host;
            _requestedMainGeneration = -1;
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.LoginStateChanged += HandleLoginStateChanged;
            if (SFXManager.Instance != null) SFXManager.Instance.PlayBGM(eSFXType.TITLE);
            if (_view.btnSettings != null) _view.btnSettings.onClick.AddListener(() =>
            {
                HideLoginPopup(); _view.presentation?.CloseLanguagePopup(); _host.OpenSettings();
            });

            if (_view.btnLogin != null && _view.popupLogin != null)
                _view.btnLogin.onClick.AddListener(ShowLoginPopup);

            if (_view.btnLoginGuest != null)
            {
                _view.btnLoginGuest.gameObject.SetActive(true);
                _view.btnLoginGuest.onClick.AddListener(() => SignIn(eAuthType.CustomLogin));
            }

            if (_view.btnLoginGoogle != null)
            {
                _view.btnLoginGoogle.onClick.AddListener(() => SignIn(eAuthType.GoogleWebLogin));
            }

            if (_view.btnLoginApple != null)
                _view.btnLoginApple.onClick.AddListener(() => _host.ShowToast(Localize("Apple 로그인은 준비 중입니다.", "Apple sign-in is coming soon.")));

            // 팝업 바깥(딤) 탭 → 닫기. 팝업 박스는 별도 Image가 레이캐스트를 막는다.
            if (_view.popupLoginDim != null)
                _view.popupLoginDim.onClick.AddListener(HideLoginPopup);

            if (_view.bgClickCatcher != null)
            {
                _view.bgClickCatcher.onClick.AddListener(() =>
                {
                    // 팝업이 열려있으면 딤이 레이캐스트를 가로채므로 여기 도달하지 않지만, 방어적으로 무시.
                    if (_view.popupLogin != null && _view.popupLogin.activeSelf)
                        return;

                    // 미인증 상태로 메인 진입 시 익명 계정 로그인 문제가 있어
                    // 세션이 없으면 진입을 차단하고 로그인 팝업을 띄운다.
                    if (NetworkManager.Instance != null && NetworkManager.Instance.IsAuthenticating)
                    {
                        _host.ShowToast(Localize("서버 데이터를 불러오는 중입니다.", "Loading server data."));
                        return;
                    }
                    if (!IsAuthenticatedSession())
                    {
                        ShowLoginPopup();
                        return;
                    }

                    LoadMainOnce();
                });
            }

            HideLoginPopup();
            _titlePatchPending = true;
            NoticePopupController.ShowTitlePatchThen(host, () =>
            {
                if (_view != view || _host != host) return;
                _titlePatchPending = false;
                PrepareLogin();
            });
        }

        private void PrepareLogin()
        {
            if (IsAuthenticatedSession())
            {
                LoadMainOnce();
                return;
            }
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !ENVIRONMENT_SIMULATION_QA
            // 재실행 시에도 기존 인증과 서버 동의 기록 검사를 그대로 거친다.
            var network = NetworkManager.Instance;
            if (network != null && !network.IsAuthenticating && !network.IsGameDataReady &&
                !GameTermsConsent.HasActiveAgeRestriction && GameTermsConsent.VerifiedOnDevice &&
                GameTermsConsent.HasEligibleProvider(eAuthType.CustomLogin))
                SignIn(eAuthType.CustomLogin);
#endif
        }

        public void Dispose()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.LoginStateChanged -= HandleLoginStateChanged;
            if (_termsDialog != null) _termsDialog.Cancel();
            _titlePatchPending = false;
            _view = null;
            _host = null;
        }

        private void SignIn(eAuthType type)
        {
            if (_titlePatchPending) return;
            var net = NetworkManager.Instance;
            if (net == null)
            {
                _host.ShowToast(Localize("네트워크가 초기화되지 않았습니다.", "The network is not ready yet."));
                return;
            }
            if (net.IsGameDataReady) return;
            HideLoginPopup();
            net.Authenticate(type, RequestTerms);
        }

        private Task<bool> RequestTerms()
        {
            if (_view == null || !_view.isActiveAndEnabled) return Task.FromResult(false);
            _termsDialog = TermsConsentDialog.Open(_view);
            return _termsDialog.Result;
        }

        public bool HandleBack()
        {
            if (_view != null && _view.presentation != null && _view.presentation.CloseLanguagePopup()) return true;
            if (_view != null && _view.popupLogin != null && _view.popupLogin.activeSelf)
            {
                HideLoginPopup();
                return true;
            }
            return false;
        }

        private void ShowLoginPopup()
        {
            if (_view == null || _view.popupLogin == null || _titlePatchPending || IsAuthenticatedSession()) return;
            if (_view.presentation != null) _view.presentation.CloseLanguagePopup();
            _view.popupLogin.SetActive(true);
            _view.popupLogin.transform.SetAsLastSibling();
            if (_view.popupLoginBox != null) UITween.PopIn(_view.popupLoginBox, .2f, .96f);
        }

        private void HideLoginPopup()
        {
            if (_view == null || _view.popupLogin == null) return;
            _view.popupLogin.SetActive(false);
        }

        private void LoadMainOnce()
        {
            var net = NetworkManager.Instance;
            if (_view == null || _host == null || _titlePatchPending || net == null || !net.IsGameDataReady ||
                _requestedMainGeneration == net.AccountGeneration) return;

            long generation = net.AccountGeneration;
            _requestedMainGeneration = generation;
            EnterMainAfterTitleFrame(net, generation).Forget();
        }

        private async UniTask EnterMainAfterTitleFrame(NetworkManager net, long generation)
        {
            // Title Bind can run inside SceneLoadFinished before the loader releases its current request.
            await UniTask.NextFrame();
            if (_view == null || _host == null || _host.ActiveScreenId != UIScreenId.Title ||
                net == null || NetworkManager.Instance != net || !net.IsGameDataReady || net.AccountGeneration != generation) return;
            if (LoadManager.Instance != null)
                LoadManager.Instance.LoadAsyncScene(eSceneType.main);
        }

        private void HandleLoginStateChanged()
        {
            if (_view == null || _host == null || NetworkManager.Instance == null) return;
            var net = NetworkManager.Instance;
            if (_view.btnLoginGuest != null) _view.btnLoginGuest.interactable = !net.IsAuthenticating;
            if (_view.btnLoginGoogle != null) _view.btnLoginGoogle.interactable = !net.IsAuthenticating;
            if (net.LoginState == GameLoginState.Failed)
            {
                var failure = net.LastFailure;
                string kind = failure?.ErrorDetails != null && failure.ErrorDetails.TryGetValue("Kind", out var values) && values.Count > 0 ? values[0] : null;
                if (kind == "AgeRestricted")
                    _host.ShowToast(Localize("이전 연령 확인 24시간 후 다시 시도해 주세요.",
                        "Try again 24 hours after your last age check."));
                else if (kind == "ConsentDeclined")
                    ShowLoginPopup();
                else if (kind == "ConsentSaveFailed")
                    _host.ShowToast(Localize("약관 동의 기록을 저장하지 못했습니다. 연결을 확인하고 다시 로그인해 주세요.",
                        "Could not save your agreement. Check your connection and sign in again."));
                else _host.ShowToast(failure?.ErrorMessage ?? Localize("로그인에 실패했습니다.", "Sign-in failed."));
            }
            else if (net.LoginState == GameLoginState.Loading)
                _host.ShowToast(Localize("서버 데이터를 확인하는 중입니다.", "Checking server data."));
            else if (net.LoginState == GameLoginState.Ready)
                LoadMainOnce();
        }

        /// <summary>인증 및 전체 서버 상태·정의 적용 완료 여부.</summary>
        private static bool IsAuthenticatedSession()
        {
            var net = NetworkManager.Instance;
            if (net == null) return false;
            return net.IsGameDataReady;
        }

        private string Localize(string korean, string english) =>
            _view != null && _view.presentation != null ? _view.presentation.Localize(korean, english) : korean;
    }
}
