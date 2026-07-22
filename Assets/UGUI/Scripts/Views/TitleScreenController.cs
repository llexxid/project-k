using System.Collections;
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
        private Coroutine _pressHintBlink;
        private bool _requestedScene;

        public void Bind(TitleScreenView view, UIManager host)
        {
            _view = view;
            _host = host;
            _requestedScene = false;

            if (_view.btnLogin != null && _view.popupLogin != null)
                _view.btnLogin.onClick.AddListener(ShowLoginPopup);

            if (_view.btnLoginGuest != null)
            {
                _view.btnLoginGuest.onClick.AddListener(() =>
                {
                    if (NetworkManager.Instance != null)
                        NetworkManager.Instance.AuthenticateTest();
                    else
                        _host.ShowToast("네트워크가 초기화되지 않았습니다.");
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
                        _host.ShowToast("네트워크가 초기화되지 않았습니다.");
                    HideLoginPopup();
                });
            }

            if (_view.btnLoginApple != null)
                _view.btnLoginApple.onClick.AddListener(() => _host.ShowToast("Apple 로그인은 준비 중입니다."));

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
                    if (!IsAuthenticatedSession())
                    {
                        ShowLoginPopup();
                        return;
                    }

                    LoadMainOnce();
                });
            }

            if (_view.pressHint != null)
                _pressHintBlink = _host.RunCoroutine(BlinkPressHint());

            HideLoginPopup();
        }

        public void Dispose()
        {
            if (_pressHintBlink != null && _host != null)
            {
                _host.StopRunningCoroutine(_pressHintBlink);
                _pressHintBlink = null;
            }

            _view = null;
            _host = null;
        }

        public bool HandleBack()
        {
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
            _view.popupLogin.SetActive(true);
            _view.popupLogin.transform.SetAsLastSibling();
        }

        private void HideLoginPopup()
        {
            if (_view == null || _view.popupLogin == null) return;
            _view.popupLogin.SetActive(false);
        }

        private void LoadMainOnce()
        {
            if (_requestedScene) return;

            _requestedScene = true;
            if (LoadManager.Instance != null)
                LoadManager.Instance.LoadAsyncScene(eSceneType.main);
        }

        /// <summary>PlayFab 인증(세션 발급) 완료 여부.</summary>
        private static bool IsAuthenticatedSession()
        {
            var net = NetworkManager.Instance;
            if (net == null) return false;
            string sid = net.GetSessionID();
            return !string.IsNullOrEmpty(sid);
        }

        private IEnumerator BlinkPressHint()
        {
            while (_view != null && _view.pressHint != null)
            {
                float t = Time.unscaledTime * 2.2f;
                float a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t));
                var c = _view.pressHint.color;
                _view.pressHint.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
        }
    }
}
