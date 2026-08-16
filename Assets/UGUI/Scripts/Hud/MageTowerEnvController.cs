using System.Collections;
using UnityEngine;
using KingdomIdle.MageTower;
using KingdomIdle.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 마탑 환경 오브젝트 컨트롤러 — "UI 가 아니라 전장 가장자리에 서 있는 건물" 느낌.
    ///
    /// 배치: LayerScreens 의 첫 번째 자식(= 화면 프리팹 뒤)으로 넣어 하단바가 탑의 아랫부분을
    /// 자연스럽게 가리게 한다 → 탑이 바 뒤에서 삐죽 솟아오른 실루엣이 된다.
    ///
    /// 살아있는 연출
    ///  - 유휴: 아주 미세한 호흡 스케일 (UITween.BreathScale)
    ///  - 탭: 창문 점등 크로스페이드 + 펀치 → 마탑 팝업 열기
    ///  - 화면 흔들림(CameraShaker.OnShake) 동기화: 탑도 같은 리듬으로 흔들린다
    /// </summary>
    [DefaultExecutionOrder(-936)]
    public sealed class MageTowerEnvController : MonoBehaviour
    {
        public static MageTowerEnvController Instance { get; private set; }

        /// <summary>월드 흔들림(월드 유닛)을 UI px 로 환산하는 배율. 0.12 유닛 → 약 14px.</summary>
        private const float ShakeToUiScale = 120f;

        // 유휴 호흡 파라미터 — 표시/숨김 전환 시 재시작에도 쓰이므로 상수로 둔다
        private const float BreathAmplitude = 0.008f;
        private const float BreathPeriod = 3.6f;

        private MageTowerEnvView _view;
        private Coroutine _litCo;
        private Coroutine _shakeCo;
        private Vector2 _basePos;
        private bool _subscribedShake;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (_subscribedShake)
            {
                CameraShaker.OnShake -= OnWorldShake;
                _subscribedShake = false;
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            EnsureBuilt();
            UpdateVisibility();
        }

        private void EnsureBuilt()
        {
            if (_view != null) return;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerScreens == null || mgr.Catalog == null || mgr.Catalog.hudMageTowerEnv == null)
                return;

            var go = Instantiate(mgr.Catalog.hudMageTowerEnv, mgr.LayerScreens, false);
            _view = go.GetComponent<MageTowerEnvView>();
            if (_view == null)
            {
                Debug.LogError("[MageTowerEnv] MageTowerEnvView 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }

            // 화면 프리팹(하단바 포함)보다 먼저 그려져야 탑의 하단이 바 뒤로 숨는다
            go.transform.SetAsFirstSibling();

            if (_view.root != null) _basePos = _view.root.anchoredPosition;

            if (_view.button != null)
                _view.button.onClick.AddListener(OnTowerTapped);

            // 유휴 호흡 — 눈에 겨우 보이는 수준 (0.8%, 3.6초 주기)
            if (_view.root != null)
                UITween.BreathScale(_view.root, BreathAmplitude, BreathPeriod);

            if (!_subscribedShake)
            {
                CameraShaker.OnShake += OnWorldShake;
                _subscribedShake = true;
            }
        }

        private void UpdateVisibility()
        {
            if (_view == null) return;

            var ui = UIManager.Instance;
            bool show = ui != null && ui.ActiveScreenId == UIScreenId.Main;

            if (_view.gameObject.activeSelf != show)
            {
                _view.gameObject.SetActive(show);
                if (show && _view.root != null)
                {
                    // 숨김이 호흡 코루틴을 죽였으므로 재표시 때마다 다시 시작한다
                    // (BreathScale 재호출은 기존 핸들을 교체하므로 안전)
                    UITween.BreathScale(_view.root, BreathAmplitude, BreathPeriod);
                }
                else if (!show && _view.root != null)
                {
                    UITween.StopBreathScale(_view.root); // 어중간한 스케일로 얼어붙지 않게 원복
                }
            }
        }

        // ===== 탭: 점등 + 팝업 =====
        private void OnTowerTapped()
        {
            if (_view != null)
            {
                if (_litCo != null) StopCoroutine(_litCo);
                _litCo = StartCoroutine(LitFlash());

                if (_view.root != null)
                    UITween.Punch(_view.root, 0.18f, 0.03f);
            }

            MageTowerPopupController.Show();
        }

        /// <summary>창문 점등 — 빠르게 켜지고(0.12s) 잠시 유지(0.5s) 천천히 식는다(0.6s).</summary>
        private IEnumerator LitFlash()
        {
            var g = _view != null ? _view.litGroup : null;
            if (g == null) yield break;

            float from = g.alpha; // 연타 시 현재 밝기에서 이어서 켠다 (0으로 툭 떨어지는 깜빡임 방지)
            float t = 0f;
            while (t < 0.12f)
            {
                t += Time.unscaledDeltaTime;
                if (g == null) yield break;
                g.alpha = Mathf.Lerp(from, 1f, Mathf.Clamp01(t / 0.12f));
                yield return null;
            }
            g.alpha = 1f;

            yield return new WaitForSecondsRealtime(0.5f);

            t = 0f;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                if (g == null) yield break;
                g.alpha = 1f - Mathf.Clamp01(t / 0.6f);
                yield return null;
            }
            g.alpha = 0f;
            _litCo = null;
        }

        // ===== 화면 흔들림 동기화 =====
        private void OnWorldShake(float duration, float magnitude)
        {
            if (_view == null || !_view.gameObject.activeInHierarchy || _view.root == null) return;

            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(UiShake(duration, magnitude * ShakeToUiScale));
        }

        private IEnumerator UiShake(float duration, float magnitudePx)
        {
            var rt = _view.root;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime; // 월드 흔들림(CameraShaker)과 같은 시간축
                if (rt == null) yield break;

                float t = 1f - Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = _basePos + new Vector2(
                    Random.Range(-1f, 1f) * magnitudePx * t,
                    Random.Range(-1f, 1f) * magnitudePx * t * 0.6f); // 세로는 덜 흔든다 (건물 느낌)
                yield return null;
            }

            rt.anchoredPosition = _basePos;
            _shakeCo = null;
        }
    }
}
