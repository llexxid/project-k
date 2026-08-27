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
    ///
    /// AUTO 시전 (마탑 스킬 자동 발동) — 좌측 수동 슬롯 열 제거 후 이 오브젝트가 단일 소유자다.
    ///  - 기본 ON. 길게 누르기(0.5s)로 토글, PlayerPrefs("magetower.auto")에 영속.
    ///  - OFF 동안 수정이 잿빛으로 소등된다: 광원/섬광 정지 + 스프라이트 교체.
    ///    부유(hovering)는 소등 중에도 유지 — 잿빛이어도 떠 있는 마법 물체로 남긴다.
    ///  - 수동 개별 시전 경로는 없다 — 스킬은 AUTO로만 나간다 (장착/강화는 마탑 메뉴).
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

        // AUTO 시전 영속 키 — 1=ON (기본). 이 컨트롤러가 MageTowerManager.SetAutoEnabled의 단일 호출자다.
        private const string PrefKeyAuto = "magetower.auto";
        private static readonly Color CrystalOffTint = new Color(0.42f, 0.42f, 0.46f, 1f); // 잿빛 폴백 (off 스프라이트 없을 때)

        private MageTowerEnvView _view;
        private Coroutine _litCo;
        private Coroutine _litIdleCo;
        private Coroutine _crystalBobCo;
        private Coroutine _crystalFlashCo;
        private Coroutine _crystalDimCo;
        private Vector2 _crystalBasePos;
        private bool _subscribedCast;
        private MageTowerManager _castMgr;
        private bool _autoOn = true;
        private Sprite _crystalOnSprite;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _autoOn = PlayerPrefs.GetInt(PrefKeyAuto, 1) == 1;
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

            // 저장된 AUTO 상태를 매니저에 반영 (매니저 기본값은 false — UI가 소유자)
            _castMgr.SetAutoEnabled(_autoOn);
        }

        /// <summary>마탑 스킬이 나갈 때마다 수정이 짧게 번쩍인다 (AUTO 소등 중엔 침묵).</summary>
        private void OnSkillCastingChanged(int slotIndex, bool casting)
        {
            if (!casting) return;                       // 시전 '시작' 순간만
            if (!_autoOn) return;                       // 소등된 수정은 반응하지 않는다
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

            // 탭 판정을 실루엣 픽셀로 제한 — 히트 rect 전체(탑 좌우의 투명 여백 포함)가
            // 잡히면 빈 땅을 눌러도 팝업이 열리고, 길게 누르면 AUTO 까지 꺼진다.
            // 텍스처가 Read/Write 가 아니면(임포트 설정 유실) 종전 rect 판정으로 조용히 폴백.
            if (_view.towerImage != null && _view.towerImage.sprite != null &&
                _view.towerImage.sprite.texture != null && _view.towerImage.sprite.texture.isReadable)
            {
                _view.towerImage.alphaHitTestMinimumThreshold = 0.1f;
            }

            // 탭=팝업 / 길게(0.5s)=AUTO 토글. Button.onClick에는 달지 않는다(이중 발화 방지) —
            // Button은 눌림 틴트 + PlayClickSfxOnClick만 담당한다 (신성 스킬 버튼과 동일 관례).
            if (_view.longPress != null)
            {
                _view.longPress.Tapped += OnTowerTapped;
                _view.longPress.LongPressed += OnLongPressToggleAuto;
                _view.longPress.allowLongPressWhenDisabled = true;
            }
            else if (_view.button != null)
            {
                // 구버전 프리팹(장압 컴포넌트 없음) 안전망 — 최소한 탭 진입은 살린다
                Debug.LogWarning("[MageTowerEnv] UILongPressButton 이 프리팹에 없습니다. HUD를 재생성하세요.");
                _view.button.onClick.AddListener(OnTowerTapped);
            }

            // 탑의 유휴 연출은 창문 밝기뿐이다 — 트랜스폼은 절대 건드리지 않는다
            StartLitIdle();

            // 부유 수정: AUTO ON일 때만 떠다니며 빛난다. OFF면 잿빛으로 소등 상태 시작.
            if (_view.crystalRoot != null)
            {
                _crystalBasePos = _view.crystalRoot.anchoredPosition;
                if (_view.crystalImage != null)
                    _crystalOnSprite = _view.crystalImage.sprite;
                ApplyCrystalAutoVisual(animate: false);
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
                    ApplyCrystalAutoVisual(animate: false);   // AUTO ON일 때만 부유/발광 재개
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

        /// <summary>
        /// 수정이 상하로 천천히 떠다닌다. 소등(AUTO OFF) 중에도 부유는 유지한다 —
        /// 잿빛이어도 '떠 있는 마법 물체'라는 정체성은 남긴다. 광원 맥동만 점등 상태 전용.
        /// </summary>
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

                // 광원 맥동은 점등 상태에서만. 섬광/소등 페이드 재생 중에는 알파를 건드리지 않는다
                // (코루틴들이 같은 값을 두고 싸우지 않게).
                if (_autoOn && _crystalFlashCo == null && _crystalDimCo == null && _view.crystalGlowGroup != null)
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

                // 소등된 수정은 탭에도 빛나지 않는다 (창문 점등만 반응)
                if (_autoOn)
                {
                    if (_crystalFlashCo != null) StopCoroutine(_crystalFlashCo);
                    _crystalFlashCo = StartCoroutine(CrystalFlash());
                }
            }

            MageTowerPopupController.Show();
        }

        // ===== 길게 누르기: AUTO 시전 토글 =====
        private void OnLongPressToggleAuto()
        {
            _autoOn = !_autoOn;
            PlayerPrefs.SetInt(PrefKeyAuto, _autoOn ? 1 : 0);

            var mgr = MageTowerManager.Instance;
            if (mgr != null) mgr.SetAutoEnabled(_autoOn);

            ApplyCrystalAutoVisual(animate: true);

            var ui = UIManager.Instance;
            if (ui != null) ui.ShowToast(_autoOn ? "마탑 자동 시전 ON" : "마탑 자동 시전 OFF");
        }

        /// <summary>
        /// AUTO 상태를 수정에 반영한다.
        /// ON  = 푸른 수정 + 부유 + 은은한 발광 (animate 시 재점등 섬광).
        /// OFF = 잿빛 수정 + 정지 + 무광 (animate 시 광원이 사그라드는 소등 연출).
        /// </summary>
        private void ApplyCrystalAutoVisual(bool animate)
        {
            if (_view == null || _view.crystalRoot == null) return;

            // 진행 중이던 소등/섬광 연출 정리 — 두 코루틴이 같은 알파를 두고 싸우지 않게
            if (_crystalDimCo != null) { StopCoroutine(_crystalDimCo); _crystalDimCo = null; }
            if (_crystalFlashCo != null) { StopCoroutine(_crystalFlashCo); _crystalFlashCo = null; }

            if (_autoOn)
            {
                // 재점등: 원래 스프라이트/색 복구 → 부유 재개 → (토글 직후엔) 확 밝아지는 섬광
                if (_view.crystalImage != null)
                {
                    if (_crystalOnSprite != null) _view.crystalImage.sprite = _crystalOnSprite;
                    _view.crystalImage.color = Color.white;
                }
                _view.crystalRoot.localScale = Vector3.one;
                if (_view.crystalGlowGroup != null) _view.crystalGlowGroup.alpha = CrystalIdleGlow;
                StartCrystalBob();
                if (animate)
                    _crystalFlashCo = StartCoroutine(CrystalFlash());
            }
            else
            {
                // 소등 상태에서도 부유는 유지 — 광원만 꺼진 잿빛 수정이 그대로 떠다닌다.
                _view.crystalRoot.localScale = Vector3.one;
                StartCrystalBob();
                if (animate)
                {
                    _crystalDimCo = StartCoroutine(CrystalDim());
                }
                else
                {
                    if (_view.crystalGlowGroup != null) _view.crystalGlowGroup.alpha = 0f;
                    ApplyCrystalOffLook();
                }
            }
        }

        /// <summary>잿빛 수정 룩 — 전용 스프라이트가 있으면 교체, 없으면 회색 틴트 폴백.</summary>
        private void ApplyCrystalOffLook()
        {
            if (_view == null || _view.crystalImage == null) return;
            if (_view.crystalOffSprite != null)
            {
                _view.crystalImage.sprite = _view.crystalOffSprite;
                _view.crystalImage.color = Color.white;
            }
            else
            {
                _view.crystalImage.color = CrystalOffTint;
            }
        }

        /// <summary>소등 연출 — 광원이 0.35s에 걸쳐 사그라든 뒤 수정이 잿빛으로 식는다.</summary>
        private IEnumerator CrystalDim()
        {
            var g = _view != null ? _view.crystalGlowGroup : null;
            float from = g != null ? g.alpha : 0f;
            float e = 0f;
            const float dur = 0.35f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                if (g == null) break;
                g.alpha = Mathf.Lerp(from, 0f, Mathf.Clamp01(e / dur));
                yield return null;
            }
            if (g != null) g.alpha = 0f;
            ApplyCrystalOffLook();
            _crystalDimCo = null;
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
