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
    /// 살아있는 연출 — **탑 본체는 절대 움직이지 않는다.** 건물이 흔들리거나 숨쉬면 UI 가 아니라
    /// 버그처럼 보인다. 계속 움직이는 것은 부유 수정 하나뿐이고, 탑은 창문 밝기만 변한다.
    ///  - 유휴(탑): 창문 점등 알파가 아주 천천히 맥동 (LitIdle)
    ///  - 유휴(수정): 상하 부유 + 광원 미세 맥동 (CrystalBob)
    ///  - 탭: 창문 점등 플래시 → 마탑 팝업 열기 (탑 트랜스폼은 건드리지 않는다)
    ///  - 스킬 발동: 수정만 번쩍인다 (CrystalFlash)
    /// </summary>
    [DefaultExecutionOrder(-936)]
    public sealed class MageTowerEnvController : MonoBehaviour
    {
        public static MageTowerEnvController Instance { get; private set; }

        // 창문 유휴 점등 — 탑에서 유일하게 변하는 값(알파). 트랜스폼은 건드리지 않는다.
        private const float LitIdleMin = 0.10f;
        private const float LitIdleMax = 0.42f;
        private const float LitIdlePeriod = 4.2f;

        // 부유 수정 — 탑과 독립된 오브젝트라 이것만 계속 움직인다
        private const float CrystalBobAmp = 9f;      // 상하 진폭(px)
        private const float CrystalBobPeriod = 2.8f; // 부유 주기(초)
        private const float CrystalIdleGlow = 0.28f; // 평시 광원 알파

        private MageTowerEnvView _view;
        private Coroutine _litCo;
        private Coroutine _litIdleCo;
        private Coroutine _crystalBobCo;
        private Coroutine _crystalFlashCo;
        private Vector2 _crystalBasePos;
        private bool _subscribedCast;
        private MageTowerManager _castMgr;

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
            if (_subscribedCast && _castMgr != null)
            {
                _castMgr.OnCastingChanged -= OnSkillCastingChanged;
                _subscribedCast = false;
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            EnsureBuilt();
            EnsureCastSubscription();
            UpdateVisibility();
        }

        /// <summary>
        /// 마탑 스킬 발동 구독. MageTowerManager(실행순서 -950)가 이 컨트롤러(-936)보다
        /// 늦게 초기화될 수 있어 붙을 때까지 매 프레임 재시도한다 (MageTowerHudController 와 동일 관례).
        /// </summary>
        private void EnsureCastSubscription()
        {
            if (_subscribedCast) return;
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;
            _castMgr = mgr;
            _castMgr.OnCastingChanged += OnSkillCastingChanged;
            _subscribedCast = true;
        }

        /// <summary>자동·수동 가리지 않고 마탑 스킬이 나갈 때마다 수정이 짧게 번쩍인다.</summary>
        private void OnSkillCastingChanged(int slotIndex, bool casting)
        {
            if (!casting) return;                       // 시전 '시작' 순간만
            if (_view == null || !_view.gameObject.activeInHierarchy) return;
            if (_crystalFlashCo != null) StopCoroutine(_crystalFlashCo);
            _crystalFlashCo = StartCoroutine(CrystalFlash());
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

            if (_view.button != null)
                _view.button.onClick.AddListener(OnTowerTapped);

            // 탑의 유휴 연출은 창문 밝기뿐이다 — 트랜스폼은 절대 건드리지 않는다
            StartLitIdle();

            // 부유 수정: 상하로 천천히 떠다닌다. 화면에서 계속 움직이는 유일한 요소.
            if (_view.crystalRoot != null)
            {
                _crystalBasePos = _view.crystalRoot.anchoredPosition;
                StartCrystalBob();
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
                // 두 코루틴 모두 이 컨트롤러(영속 UGUI_UIRoot)가 들고 있어 비활성화로 죽지는 않지만,
                // 숨김 중엔 멈춰 두므로 재표시 때 다시 돌린다.
                if (show)
                {
                    StartLitIdle();
                    StartCrystalBob();
                }
                else
                {
                    StopLitIdle();
                    StopCrystalBob();
                }
            }
        }

        // ===== 창문 유휴 점등 (탑에서 움직이는 것은 이 알파 하나뿐) =====

        private void StartLitIdle()
        {
            if (_view == null || _view.litGroup == null) return;
            if (_litIdleCo != null) StopCoroutine(_litIdleCo);
            _litIdleCo = StartCoroutine(LitIdle());
        }

        private void StopLitIdle()
        {
            if (_litIdleCo != null) { StopCoroutine(_litIdleCo); _litIdleCo = null; }
        }

        /// <summary>창문에서 새어 나오는 빛이 아주 천천히 밝아졌다 어두워진다. 위치·크기는 불변.</summary>
        private IEnumerator LitIdle()
        {
            float e = 0f;
            while (true)
            {
                e += Time.unscaledDeltaTime;
                var g = _view != null ? _view.litGroup : null;
                if (g == null) yield break;

                // 탭 플래시 중에는 알파를 넘긴다 (두 코루틴이 같은 값을 두고 싸우지 않게)
                if (_litCo == null)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(e / LitIdlePeriod * 2f * Mathf.PI);
                    g.alpha = Mathf.Lerp(LitIdleMin, LitIdleMax, k);
                }
                yield return null;
            }
        }

        // ===== 부유 수정 =====

        private void StartCrystalBob()
        {
            if (_view == null || _view.crystalRoot == null) return;
            if (_crystalBobCo != null) StopCoroutine(_crystalBobCo);
            _crystalBobCo = StartCoroutine(CrystalBob());
        }

        private void StopCrystalBob()
        {
            if (_crystalBobCo != null) { StopCoroutine(_crystalBobCo); _crystalBobCo = null; }
            if (_view != null && _view.crystalRoot != null)
                _view.crystalRoot.anchoredPosition = _crystalBasePos;
        }

        /// <summary>수정이 상하로 천천히 떠다닌다. 광원 알파도 같은 위상으로 아주 약하게 맥동.</summary>
        private IEnumerator CrystalBob()
        {
            float e = 0f;
            while (true)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Sin(e / CrystalBobPeriod * 2f * Mathf.PI);
                var rt = _view != null ? _view.crystalRoot : null;
                if (rt == null) yield break;
                rt.anchoredPosition = _crystalBasePos + new Vector2(0f, k * CrystalBobAmp);

                // 섬광 재생 중에는 알파를 건드리지 않는다 (두 코루틴이 같은 값을 두고 싸우지 않게)
                if (_crystalFlashCo == null && _view.crystalGlowGroup != null)
                    _view.crystalGlowGroup.alpha = CrystalIdleGlow + 0.06f * k;

                yield return null;
            }
        }

        /// <summary>스킬 발동 섬광 — 즉시 밝아졌다(0.06s) 빠르게 식는다(0.34s).</summary>
        private IEnumerator CrystalFlash()
        {
            var g = _view != null ? _view.crystalGlowGroup : null;
            var body = _view != null ? _view.crystalRoot : null;
            if (g == null) { _crystalFlashCo = null; yield break; }

            float e = 0f;
            const float up = 0.06f;
            while (e < up)
            {
                e += Time.unscaledDeltaTime;
                if (g == null) { _crystalFlashCo = null; yield break; }
                g.alpha = Mathf.Lerp(CrystalIdleGlow, 1f, Mathf.Clamp01(e / up));
                if (body != null) body.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, Mathf.Clamp01(e / up));
                yield return null;
            }

            e = 0f;
            const float down = 0.34f;
            while (e < down)
            {
                e += Time.unscaledDeltaTime;
                if (g == null) { _crystalFlashCo = null; yield break; }
                float t = Mathf.Clamp01(e / down);
                g.alpha = Mathf.Lerp(1f, CrystalIdleGlow, t);
                if (body != null) body.localScale = Vector3.one * Mathf.Lerp(1.18f, 1f, t);
                yield return null;
            }

            if (g != null) g.alpha = CrystalIdleGlow;
            if (body != null) body.localScale = Vector3.one;
            _crystalFlashCo = null;
        }

        // ===== 탭: 점등 + 팝업 =====
        private void OnTowerTapped()
        {
            if (_view != null)
            {
                // 탭 반응도 **알파만**이다. 예전의 UITween.Punch 는 탑을 움찔거리게 만들어
                // "건물이 튄다"는 인상을 줬다 — 반응은 수정과 창문 빛이 대신한다.
                if (_litCo != null) StopCoroutine(_litCo);
                _litCo = StartCoroutine(LitFlash());

                if (_crystalFlashCo != null) StopCoroutine(_crystalFlashCo);
                _crystalFlashCo = StartCoroutine(CrystalFlash());
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
            // 유휴 맥동으로 되돌린다 (0 으로 떨구면 창문이 완전히 꺼져 죽은 건물처럼 보인다)
            g.alpha = LitIdleMin;
            _litCo = null;
        }

        // 화면 흔들림(CameraShaker) 동기화는 제거했다 — 탑 트랜스폼을 흔들면 "건물이 움직인다".
        // 전투 피드백으로 되살리고 싶다면 흔드는 대상을 root 가 아니라 창문 알파/수정으로 두어야 한다.
    }
}
