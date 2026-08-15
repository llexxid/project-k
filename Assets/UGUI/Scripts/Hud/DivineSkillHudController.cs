using System.Collections.Generic;
using UnityEngine;
using KingdomIdle.Divine;
using KingdomIdle.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 신성 스킬(궁극기) HUD 컨트롤러 (MageTowerHudController와 동일 관례).
    /// 좌하단 대형 버튼 1개 — 방사형 쿨다운, 남은 초, 시전 가능 시 후광 맥동, 탭 시 수동 시전.
    ///
    /// 표시 규칙
    ///  - 시스템 미해금(3-10 클리어 전): 위젯 전체를 숨긴다. 아직 존재하지 않는 기능을
    ///    죽은 버튼으로 남겨 두면 화면만 어지럽고 플레이어에게 아무것도 알려 주지 못한다.
    ///  - 해금 O / 카드 미장착: 흐린 빈 슬롯을 남긴다(버튼 비활성 = ColorBlock.disabledColor).
    ///    기능은 이미 존재하므로 "장착하러 가라"는 신호가 되어야 한다.
    /// </summary>
    [DefaultExecutionOrder(-934)]
    public sealed class DivineSkillHudController : MonoBehaviour
    {
        public static DivineSkillHudController Instance { get; private set; }

        private static readonly Color FrameCasting = new Color(1f, 0.94f, 0.74f, 1f);
        private static readonly Color GradeBorderEmpty = UguiTheme.DisabledGrey;

        /// <summary>CanCast()는 대상 판정에 물리 질의가 들어간다 — 매 프레임이 아니라 주기적으로만 본다.</summary>
        private const float ReadyCheckInterval = 0.2f;

        private DivineSkillHudView _view;
        private readonly List<CanvasGroup> _pulseTargets = new();
        private Color _frameNormal = Color.white;
        private bool _subscribed;
        private bool _pulsing;
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

            // HUD는 스트레치하지 않는다 — 프리팹의 앵커가 그대로 살아야 한다
            var go = Instantiate(mgr.Catalog.hudDivineSkill, mgr.LayerScreens, false);
            _view = go.GetComponent<DivineSkillHudView>();
            if (_view == null)
            {
                Debug.LogError("[DivineHud] DivineSkillHudView 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }

            go.transform.SetAsLastSibling();

            if (_view.frame != null) _frameNormal = _view.frame.color;

            if (_view.button != null)
                _view.button.onClick.AddListener(OnDivineBtnClicked);

            _pulseTargets.Clear();
            if (_view.readyGlowGroup != null) _pulseTargets.Add(_view.readyGlowGroup);

            Refresh();
        }

        private void UpdateVisibility()
        {
            if (_view == null) return;

            var ui = UIManager.Instance;
            var mgr = DivineSkillManager.Instance;

            // 메인 화면 + 시스템 해금 상태에서만 존재한다 (전체를 덮는 시트가 열려 있으면 숨김)
            bool show = ui != null
                        && ui.ActiveScreenId == UIScreenId.Main
                        && !ui.HasBlockingPanel
                        && mgr != null
                        && mgr.IsSystemUnlocked;

            if (_view.gameObject.activeSelf == show) return;

            _view.gameObject.SetActive(show);
            // 펀치 트윈 도중 비활성화되면 코루틴이 끊겨 localScale 이 커진 채 얼어붙는다.
            // 표시 상태가 바뀔 때마다 무조건 원복해 잔여 스케일이 남지 않게 한다.
            _view.transform.localScale = Vector3.one;
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
            }

            if (_view.emptyLabel != null)
            {
                _view.emptyLabel.gameObject.SetActive(!hasIcon);
                if (!hasIcon)
                {
                    // 아트가 아직 없으면 카드 이름으로 대체해 빈 박스를 남기지 않는다
                    _view.emptyLabel.text = equipped ? card.DisplayName : "궁극기\n미장착";
                    _view.emptyLabel.color = equipped ? UguiTheme.TextPrimary : UguiTheme.TextTertiary;
                }
            }

            if (_view.gradeBorder != null)
                _view.gradeBorder.color = equipped ? DivineSkillSO.GetGradeColor(card.grade) : GradeBorderEmpty;

            // 미장착 = 흐린 빈 슬롯 (Button.disabledColor가 자동으로 어둡게 만든다)
            if (_view.button != null)
                _view.button.interactable = equipped;

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
            // 같은 표시값이면 fillAmount/text 를 건드리지 않아 루트 캔버스 리빌드를 60→10회/초로 줄인다.
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

        // ===== 시전 가능 후광 =====
        private void UpdateReadyState()
        {
            if (_view == null || !_view.gameObject.activeInHierarchy)
            {
                SetPulsing(false);
                return;
            }

            var mgr = DivineSkillManager.Instance;
            bool ready = mgr != null && !DivinePresentation.CutInPlaying && mgr.CanCast();
            SetPulsing(ready);

            // 대상이 없어 시전 불가일 때 탭이 소리 없이 무시되지 않도록,
            // 같은 0.2초 폴링에서 버튼 활성 상태도 함께 갱신한다.
            // 쿨다운 중에는 방사형 마스크가 이미 상태를 전달하므로 눌리는 모양을 유지한다.
            if (_view.button != null)
                _view.button.interactable =
                    mgr != null && mgr.EquippedCard != null && (ready || mgr.IsOnCooldown);
        }

        private void SetPulsing(bool on)
        {
            if (_view == null) return;
            if (_pulsing == on) return;
            _pulsing = on;

            if (_view.readyGlow != null && _view.readyGlow.gameObject.activeSelf != on)
                _view.readyGlow.gameObject.SetActive(on);

            if (_view.pulse == null) return;
            if (on) _view.pulse.Begin(_pulseTargets);
            else _view.pulse.Stop();
        }

        // ===== 시전 중 하이라이트 =====
        private void OnCastStateChanged(bool casting)
        {
            if (_view == null) return;

            if (_view.frame != null)
                _view.frame.color = casting ? FrameCasting : _frameNormal;

            if (casting)
            {
                SetPulsing(false);
                if (_view.gameObject.activeInHierarchy)
                    UITween.Punch((RectTransform)_view.transform, 0.26f, 0.14f);
            }
            else
            {
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
    }
}
