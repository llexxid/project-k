using UnityEngine;
using Scripts.Core;
using Scripts.Core.Manager;

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
        private bool _requestedScene;

        public void Bind(TitleScreenView view, UIManager host)
        {
            _view = view;
            _host = host;
            _requestedScene = false;
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
                // Guest accounts use a persistent, device-local ID instead of a shared diagnostic account.
                _view.btnLoginGuest.gameObject.SetActive(true);
                _view.btnLoginGuest.onClick.AddListener(() =>
                {
                    if (NetworkManager.Instance != null)
                        NetworkManager.Instance.AuthenticateTest();
                    else
                        _host.ShowToast(Localize("네트워크가 초기화되지 않았습니다.", "The network is not ready yet."));
                    HideLoginPopup();
                });
            }

            if (_view.btnLoginGoogle != null)
            {
                _view.btnLoginGoogle.onClick.AddListener(() =>
                {
                    if (NetworkManager.Instance != null)
                        NetworkManager.Instance.Authenticate(Scripts.Server.Auth.eAuthType.GoogleWebLogin);
                    else
                        _host.ShowToast(Localize("네트워크가 초기화되지 않았습니다.", "The network is not ready yet."));
                    HideLoginPopup();
                });
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
        }

        public void Dispose()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.LoginStateChanged -= HandleLoginStateChanged;
            _view = null;
            _host = null;
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
            if (_view == null || _view.popupLogin == null) return;
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
            if (_requestedScene || !IsAuthenticatedSession()) return;

            _requestedScene = true;
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
                _host.ShowToast(net.LastFailure?.ErrorMessage ?? Localize("로그인에 실패했습니다.", "Sign-in failed."));
            else if (net.LoginState == GameLoginState.Loading)
                _host.ShowToast(Localize("서버 데이터를 확인하는 중입니다.", "Checking server data."));
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
