using System.Collections.Generic;
using UnityEngine;
using KingdomIdle.Divine;
using KingdomIdle.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 신성 스킬(궁극기) HUD 컨트롤러 (MageTowerHudController와 동일 관례).
    /// 하단 중앙(파티 HUD 위) 원형 대형 버튼 1개 —
    ///  - 탭: 수동 시전 / 길게(0.5초): 자동 시전 토글(골드 회전 링 + AUTO 필 + 토스트)
    ///  - 준비 완료: PopIn + 후광 맥동·호흡 / 시전 중: 눌림 스케일 + 아이콘 어두운 틴트
    ///  - 발동(시전 종료): 눌림 해제 + 확장 플래시 / 쿨다운: 방사형 스윕 + 남은 초
    ///
    /// 표시 규칙
    ///  - 시스템 미해금(3-10 클리어 전): 위젯 전체를 숨긴다. 아직 존재하지 않는 기능을
    ///    죽은 버튼으로 남겨 두면 화면만 어지럽고 플레이어에게 아무것도 알려 주지 못한다.
    ///  - 해금 O / 카드 미장착: 흐린 빈 디스크 + "미장착" (버튼 비활성 = ColorBlock.disabledColor).
    ///    기능은 이미 존재하므로 "장착하러 가라"는 신호가 되어야 한다.
    /// </summary>
    [DefaultExecutionOrder(-934)]
    public sealed class DivineSkillHudController : MonoBehaviour
    {
        public static DivineSkillHudController Instance { get; private set; }

        private static readonly Color GradeBorderEmpty = UguiTheme.DisabledGrey;
        private static readonly Color IconCastingTint = new Color(0.55f, 0.55f, 0.62f, 1f);
        private static readonly Color DiscNormal = new Color(0.11f, 0.09f, 0.07f, 1f);
        private static readonly Color DiscEmpty = new Color(0.11f, 0.09f, 0.07f, 0.60f);   // 미장착 = 흐린 디스크

        /// <summary>CanCast()는 대상 판정에 물리 질의가 들어간다 — 매 프레임이 아니라 주기적으로만 본다.</summary>
        private const float ReadyCheckInterval = 0.2f;

        /// <summary>AUTO 링 회전 속도 (도/초). 느긋하게 도는 장식 — 존재감만 주면 된다.</summary>
        private const float AutoRingDegPerSec = 30f;

        private DivineSkillHudView _view;
        private readonly List<CanvasGroup> _pulseTargets = new();
        private bool _subscribed;
        private bool _pulsing;
        private bool _wasReady;
        private bool _autoShown;
        private float _nextReadyCheck;
        private bool _managerMissingLogged;

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
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            var mgr = DivineSkillManager.Instance;
            if (mgr == null) return;

            mgr.OnCooldownTick += OnCooldownTick;
            mgr.OnCastStateChanged += OnCastStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var mgr = DivineSkillManager.Instance;
            if (mgr != null)
            {
                mgr.OnCooldownTick -= OnCooldownTick;
                mgr.OnCastStateChanged -= OnCastStateChanged;
            }
            _subscribed = false;
        }

        private void Update()
        {
            // DivineSkillManager가 UI보다 늦게 초기화될 수 있어 구독을 재시도한다
            if (!_subscribed) Subscribe();

            EnsureHudBuilt();
            UpdateVisibility();

            if (Time.unscaledTime >= _nextReadyCheck)
            {
                _nextReadyCheck = Time.unscaledTime + ReadyCheckInterval;
                UpdateReadyState();
            }
        }

        private void EnsureHudBuilt()
        {
            if (_view != null) return;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerScreens == null || mgr.Catalog == null || mgr.Catalog.hudDivineSkill == null)
                return;

            // 게임플레이 매니저 부재를 조용히 숨기지 않는다 — bootstrap 씬에 DivineSkillManager 가
            // 설치되지 않으면 기능 전체가 죽은 코드가 되므로 1회 명시적으로 알린다.
            if (DivineSkillManager.Instance == null && !_managerMissingLogged)
            {
                _managerMissingLogged = true;
                Debug.LogError("[DivineHud] DivineSkillManager 가 씬에 없습니다. " +
                               "KingdomIdle/Divine/Install Manager Into Bootstrap 을 실행했는지 확인하세요.");
            }

            // HUD는 스트레치하지 않는다 — 프리팹의 앵커(하단 중앙)가 그대로 살아야 한다
            var go = Instantiate(mgr.Catalog.hudDivineSkill, mgr.LayerScreens, false);
            _view = go.GetComponent<DivineSkillHudView>();
            if (_view == null)
            {
                Debug.LogError("[DivineHud] DivineSkillHudView 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }

            go.transform.SetAsLastSibling();

            if (_view.longPress != null)
            {
                // 탭=수동 시전, 길게=자동 토글. Button.onClick에는 달지 않는다(이중 발화 방지) —
                // Button은 눌림 틴트와 PlayClickSfxOnClick(SFX)만 담당한다.
                _view.longPress.Tapped += OnDivineBtnClicked;
                _view.longPress.LongPressed += OnLongPressToggleAuto;
                // 시전 불가(대상 없음 등)로 버튼이 비활성이어도 AUTO 토글만은 가능해야 한다
                _view.longPress.allowLongPressWhenDisabled = true;
            }
            else if (_view.button != null)
            {
                // 구버전 프리팹(장압 컴포넌트 없음) 안전망 — 최소한 탭 시전은 살린다
                Debug.LogWarning("[DivineHud] UILongPressButton 이 프리팹에 없습니다. HUD를 재생성하세요.");
                _view.button.onClick.AddListener(OnDivineBtnClicked);
            }

            _pulseTargets.Clear();
            if (_view.readyGlowGroup != null) _pulseTargets.Add(_view.readyGlowGroup);

            Refresh();
        }

        private void UpdateVisibility()
        {
            if (_view == null) return;

            var ui = UIManager.Instance;
            var mgr = DivineSkillManager.Instance;

            // 메인 화면을 벗어나면 도감 팝업도 함께 닫는다
            // (UIManager.ReplaceScreen은 이 팝업을 모른다 — 화면 전환 정리를 HUD가 대신한다)
            if ((ui == null || ui.ActiveScreenId != UIScreenId.Main) && DivineCollectionPopupController.IsOpen)
                DivineCollectionPopupController.Hide();

            // 메인 화면 + 시스템 해금 상태에서만 존재한다 (전체를 덮는 시트가 열려 있으면 숨김)
            bool show = ui != null
                        && ui.ActiveScreenId == UIScreenId.Main
                        && !ui.HasBlockingPanel
                        && mgr != null
                        && mgr.IsSystemUnlocked;

            if (_view.gameObject.activeSelf == show) return;

            _view.gameObject.SetActive(show);
            // 펀치/눌림 트윈 도중 비활성화되면 코루틴이 끊겨 localScale 이 커진 채 얼어붙는다.
            // 표시 상태가 바뀔 때마다 무조건 원복해 잔여 스케일이 남지 않게 한다.
            _view.transform.localScale = Vector3.one;
            if (_view.frame != null) _view.frame.rectTransform.localScale = Vector3.one;
            // 성공 플래시(0.35s) 도중 숨겨지면 링이 확대·반투명 상태로 얼어붙는다 — 같은 계열 버그
            if (_view.castFlash != null)
            {
                _view.castFlash.gameObject.SetActive(false);
                _view.castFlash.rectTransform.localScale = Vector3.one;
            }
            if (show) Refresh();
            else SetPulsing(false);
        }

        // ===== 장착 카드 반영 =====
        /// <summary>장착/보유 상태가 바뀔 때 브릿지가 호출한다.</summary>
        public void Refresh()
        {
            if (_view == null) return;

            _lastShownTenths = -1; // 다음 쿨다운 틱에서 강제로 다시 그린다

            var mgr = DivineSkillManager.Instance;
            var card = mgr != null ? mgr.EquippedCard : null;
            bool equipped = card != null;

            // 아이콘 스프라이트가 실제로 있을 때만 아이콘을 켠다 — 없으면 흰 박스가 남는다
            bool hasIcon = equipped && card.icon != null && _view.icon != null;
            if (_view.icon != null)
            {
                if (hasIcon) _view.icon.sprite = card.icon;
                _view.icon.gameObject.SetActive(hasIcon);
                // 숨김 중 시전 상태가 바뀌었을 수 있으므로 틴트를 현재 상태로 맞춘다
                _view.icon.color = mgr != null && mgr.IsCasting ? IconCastingTint : Color.white;
            }

            if (_view.emptyLabel != null)
            {
                _view.emptyLabel.gameObject.SetActive(!hasIcon);
                if (!hasIcon)
                {
                    // 아트가 아직 없으면 카드 이름으로 대체해 빈 원을 남기지 않는다
                    _view.emptyLabel.text = equipped ? card.DisplayName : "미장착";
                    _view.emptyLabel.color = equipped ? UguiTheme.TextPrimary : UguiTheme.TextTertiary;
                }
            }

            if (_view.gradeBorder != null)
                _view.gradeBorder.color = equipped ? DivineSkillSO.GetGradeColor(card.grade) : GradeBorderEmpty;

            // 미장착 = 흐린 빈 디스크 (버튼 링은 disabledColor, 디스크는 직접 알파를 낮춘다)
            if (_view.disc != null)
                _view.disc.color = equipped ? DiscNormal : DiscEmpty;

            if (_view.button != null)
                _view.button.interactable = equipped;

            ApplyAutoVisual(force: true);   // 재표시 시 회전 코루틴이 죽어 있으므로 무조건 다시 적용
            OnCooldownTick();
            UpdateReadyState();
        }

        // ===== 쿨다운 =====
        /// <summary>마지막으로 표시한 0.1초 단위 값. 표시값이 바뀔 때만 UI 를 다시 쓴다.</summary>
        private int _lastShownTenths = -1;

        private void OnCooldownTick()
        {
            if (_view == null) return;

            var mgr = DivineSkillManager.Instance;
            if (mgr == null) return;

            bool cooling = mgr.IsOnCooldown && mgr.EquippedCard != null;

            // OnCooldownTick 은 매 프레임 발화하지만 F1 표시값은 0.1초에 한 번만 변한다.
            // 같은 표시값이면 fillAmount/text 를 건드리지 않아 캔버스 리빌드를 60→10회/초로 줄인다.
            int tenths = cooling ? Mathf.CeilToInt(mgr.CooldownRemaining * 10f) : -1;
            if (tenths == _lastShownTenths) return;
            _lastShownTenths = tenths;

            if (_view.cooldownFill != null)
            {
                if (_view.cooldownFill.gameObject.activeSelf != cooling)
                    _view.cooldownFill.gameObject.SetActive(cooling);
                if (cooling)
                    _view.cooldownFill.fillAmount = Mathf.Clamp01(mgr.CooldownRatio);
            }

            if (_view.cooldownText != null)
            {
                if (_view.cooldownText.gameObject.activeSelf != cooling)
                    _view.cooldownText.gameObject.SetActive(cooling);
                if (cooling)
                    _view.cooldownText.text = (tenths * 0.1f).ToString("F1");
            }
        }

        // ===== 시전 가능 후광 + 주기 폴링 =====
        private void UpdateReadyState()
        {
            if (_view == null || !_view.gameObject.activeInHierarchy)
            {
                SetPulsing(false);
                return;
            }

            var mgr = DivineSkillManager.Instance;
            bool ready = mgr != null && !DivinePresentation.CutInPlaying && mgr.CanCast();

            // 준비 완료로 '전환'되는 순간에만 PopIn — 준비됐다는 사실을 한 번 크게 알린다
            if (ready && !_wasReady)
                UITween.PopIn((RectTransform)_view.transform, 0.22f, 0.9f);
            _wasReady = ready;

            SetPulsing(ready);

            // 같은 0.2초 폴링에서 자동 시전 상태(외부 변경 포함)도 함께 반영한다
            ApplyAutoVisual();

            // 대상이 없어 시전 불가일 때 탭이 소리 없이 무시되지 않도록 버튼 활성 상태도 갱신한다.
            // 쿨다운 중에는 방사형 마스크가 이미 상태를 전달하므로 눌리는 모양을 유지한다
            // (겸사겸사 길게 눌러 자동 토글도 쿨다운 중에 가능해야 한다).
            if (_view.button != null)
                _view.button.interactable =
                    mgr != null && mgr.EquippedCard != null && (ready || mgr.IsOnCooldown);
        }

        private void SetPulsing(bool on)
        {
            if (_view == null) return;
            if (_pulsing == on) return;
            _pulsing = on;

            if (_view.readyGlow != null)
            {
                var glowRt = _view.readyGlow.rectTransform;
                if (_view.readyGlow.gameObject.activeSelf != on)
                    _view.readyGlow.gameObject.SetActive(on);
                if (on) UITween.BreathScale(glowRt, 0.06f, 2.4f);   // 은은한 호흡 — 알파 맥동과 겹쳐 살아있는 느낌
                else UITween.StopBreathScale(glowRt);
            }

            if (_view.pulse == null) return;
            if (on) _view.pulse.Begin(_pulseTargets);
            else _view.pulse.Stop();
        }

        // ===== 자동 시전 표시 (골드 회전 링 + AUTO 필) =====
        /// <summary>
        /// force=true: 상태 캐시를 무시하고 다시 적용 (재표시 직후 — 회전 코루틴이 죽어 있을 수 있다).
        /// 링 회전은 링 GameObject가 켜져 있을 때만 도는 코루틴이라 숨김 중 비용이 없다.
        /// </summary>
        private void ApplyAutoVisual(bool force = false)
        {
            if (_view == null) return;

            var mgr = DivineSkillManager.Instance;
            bool on = mgr != null && mgr.IsAutoEnabled && mgr.EquippedCard != null;
            if (!force && on == _autoShown) return;
            _autoShown = on;

            if (_view.autoPill != null && _view.autoPill.activeSelf != on)
                _view.autoPill.SetActive(on);

            if (_view.autoRing != null)
            {
                if (_view.autoRing.gameObject.activeSelf != on)
                    _view.autoRing.gameObject.SetActive(on);
                if (on) UITween.RotateLoop(_view.autoRing, AutoRingDegPerSec);   // 재호출 안전(기존 루프 교체)
                else UITween.StopRotateLoop(_view.autoRing);
            }
        }

        // ===== 시전 중 연출: 눌림 → 발동 플래시 =====
        private void OnCastStateChanged(bool casting)
        {
            if (_view == null) return;

            // 시전 중 아이콘을 살짝 어둡게 — "지금 발동 절차가 진행 중"을 전달
            if (_view.icon != null)
                _view.icon.color = casting ? IconCastingTint : Color.white;

            var frameRt = _view.frame != null ? _view.frame.rectTransform : null;
            if (casting)
            {
                SetPulsing(false);
                // 눌림(0.92) — 컷인이 끝나 실제 발동하는 순간(casting=false)에 해제+플래시
                if (frameRt != null && frameRt.gameObject.activeInHierarchy)
                    UITween.ScaleTo(frameRt, 0.92f, 0.06f);
            }
            else
            {
                if (frameRt != null)
                {
                    if (frameRt.gameObject.activeInHierarchy) UITween.ScaleTo(frameRt, 1f, 0.10f);
                    else frameRt.localScale = Vector3.one;   // 숨김 중 종료 — 트윈 대신 즉시 원복
                }
                // 성공 플래시는 "실제로 발동한" 종료에서만 — 컷인 취소(스테이지 전환·대상 소멸)는
                // 쿨타임이 시작되지 않으므로 IsOnCooldown 으로 발동/취소를 구분한다
                var mgrNow = DivineSkillManager.Instance;
                bool actuallyFired = mgrNow != null && mgrNow.IsOnCooldown;
                if (actuallyFired && _view.castFlash != null && _view.gameObject.activeInHierarchy)
                    UITween.FlashRing(_view.castFlash, 0.35f);
                UpdateReadyState();
            }
        }

        // ===== 탭 → 수동 시전 =====
        private void OnDivineBtnClicked()
        {
            var mgr = DivineSkillManager.Instance;
            if (mgr == null) return;
            if (!mgr.IsSystemUnlocked) return;
            if (mgr.EquippedCard == null) return;
            if (mgr.IsCasting || mgr.IsOnCooldown) return;
            if (DivinePresentation.CutInPlaying) return;

            // 컷인이 등록돼 있으면 컷인 → 컷인 종료 시점에 실제 발동. 없으면 즉시 발동.
            mgr.CastManual();
        }

        // ===== 길게 누르기 → 자동 시전 토글 =====
        private void OnLongPressToggleAuto()
        {
            var mgr = DivineSkillManager.Instance;
            if (mgr == null || !mgr.IsSystemUnlocked || mgr.EquippedCard == null) return;

            bool on = !mgr.IsAutoEnabled;
            mgr.SetAutoEnabled(on);
            ApplyAutoVisual(force: true);

            // 햅틱 느낌의 펀치 + 토스트로 토글 확정을 알린다
            if (_view != null && _view.gameObject.activeInHierarchy)
                UITween.Punch((RectTransform)_view.transform, 0.2f, 0.1f);
            var ui = UIManager.Instance;
            if (ui != null) ui.ShowToast(on ? "자동 시전 ON" : "자동 시전 OFF");
        }
    }
}
