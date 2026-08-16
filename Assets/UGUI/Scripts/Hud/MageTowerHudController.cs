using UnityEngine;
using KingdomIdle.MageTower;
using KingdomIdle.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 마법탑 스킬 HUD (UITKMageTowerHudController 이식).
    /// Auto 토글, 슬롯 탭 시전, 쿨다운 마스크, 시전 중 하이라이트, 마탑 팝업 열기.
    /// </summary>
    [DefaultExecutionOrder(-935)]
    public sealed class MageTowerHudController : MonoBehaviour
    {
        public static MageTowerHudController Instance { get; private set; }

        private static readonly Color AutoOffBg = UguiTheme.SurfaceFaint;
        private static readonly Color AutoOnBg = new Color(40f / 255f, 180f / 255f, 100f / 255f, 0.40f);
        private static readonly Color AutoOffText = new Color(1f, 1f, 1f, 0.30f);
        private static readonly Color AutoOnText = new Color(180f / 255f, 1f, 210f / 255f, 1f);
        private static readonly Color SlotFrameNormal = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color SlotFrameCasting = new Color(180f / 255f, 220f / 255f, 1f, 1f);

        private MageTowerHudView _view;
        private bool _autoEnabled;
        private bool _subscribed;

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
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            mgr.OnCooldownTick += OnCooldownTick;
            mgr.OnCastingChanged += OnCastingChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var mgr = MageTowerManager.Instance;
            if (mgr != null)
            {
                mgr.OnCooldownTick -= OnCooldownTick;
                mgr.OnCastingChanged -= OnCastingChanged;
            }
            _subscribed = false;
        }

        private void Update()
        {
            // MageTowerManager가 늦게 초기화될 수 있어 구독 재시도 (기존 OnEnable 타이밍 이슈 보완)
            if (!_subscribed) Subscribe();

            EnsureHudBuilt();
            UpdateVisibility();
        }

        private void EnsureHudBuilt()
        {
            if (_view != null) return;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerScreens == null || mgr.Catalog == null || mgr.Catalog.hudMageTower == null)
                return;

            var go = Instantiate(mgr.Catalog.hudMageTower, mgr.LayerScreens, false);
            _view = go.GetComponent<MageTowerHudView>();
            if (_view == null)
            {
                Debug.LogError("[MageTowerHud] MageTowerHudView 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }

            go.transform.SetAsLastSibling();

            if (_view.autoButton != null)
                _view.autoButton.onClick.AddListener(OnAutoBtnClicked);

            for (int i = 0; i < _view.slots.Length; i++)
            {
                int idx = i;
                var slot = _view.slots[i];
                if (slot?.button != null)
                    slot.button.onClick.AddListener(() => OnSlotClicked(idx));
            }

            ApplyAutoVisual();
            RefreshSlots();
        }

        private void UpdateVisibility()
        {
            if (_view == null) return;

            var mgr = UIManager.Instance;
            bool onMain = mgr != null && mgr.ActiveScreenId == UIScreenId.Main;
            if (_view.gameObject.activeSelf != onMain)
                _view.gameObject.SetActive(onMain);
        }

        // ===== Slot Click → 시전 =====
        private void OnSlotClicked(int slotIndex)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            int skillId = mgr.GetEquippedSkillId(slotIndex);
            if (skillId < 0) return;
            if (mgr.IsOnCooldown(slotIndex)) return;
            if (mgr.IsCasting(slotIndex)) return;

            mgr.CastSkill(slotIndex);
        }

        // ===== 시전 중 테두리 하이라이트 =====
        private void OnCastingChanged(int slotIndex, bool casting)
        {
            if (_view == null || slotIndex < 0 || slotIndex >= _view.slots.Length) return;
            var slot = _view.slots[slotIndex];
            if (slot?.frame == null) return;

            slot.frame.color = casting ? SlotFrameCasting : SlotFrameNormal;
        }

        private void OnAutoBtnClicked()
        {
            _autoEnabled = !_autoEnabled;
            ApplyAutoVisual();

            var mgr = MageTowerManager.Instance;
            if (mgr != null)
                mgr.SetAutoEnabled(_autoEnabled);
        }

        private void ApplyAutoVisual()
        {
            if (_view == null) return;
            if (_view.autoButtonBg != null)
                _view.autoButtonBg.color = _autoEnabled ? AutoOnBg : AutoOffBg;
            if (_view.autoButtonLabel != null)
                _view.autoButtonLabel.color = _autoEnabled ? AutoOnText : AutoOffText;
        }

        // ===== Cooldown UI =====
        private void OnCooldownTick()
        {
            if (_view == null) return;

            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            for (int i = 0; i < _view.slots.Length && i < MageTowerManager.SlotCount; i++)
            {
                var slot = _view.slots[i];
                if (slot?.cooldownMask == null) continue;

                if (mgr.IsOnCooldown(i))
                {
                    float ratio = mgr.GetCooldownRatio(i);

                    slot.cooldownMask.gameObject.SetActive(true);
                    slot.cooldownMask.fillAmount = Mathf.Clamp01(ratio);

                    float remaining = mgr.GetEffectiveCooldown(mgr.GetEquippedSkillId(i)) * ratio;
                    if (slot.cooldownText != null)
                    {
                        slot.cooldownText.gameObject.SetActive(true);
                        slot.cooldownText.text = $"{remaining:F1}";
                    }
                }
                else
                {
                    slot.cooldownMask.gameObject.SetActive(false);
                    if (slot.cooldownText != null)
                        slot.cooldownText.gameObject.SetActive(false);
                }
            }
        }

        // ===== Refresh Slots =====
        public void RefreshSlots()
        {
            if (_view == null) return;

            var mgr = MageTowerManager.Instance;

            for (int i = 0; i < _view.slots.Length && i < MageTowerManager.SlotCount; i++)
            {
                var slot = _view.slots[i];
                if (slot == null) continue;

                int skillId = mgr != null ? mgr.GetEquippedSkillId(i) : -1;
                var so = mgr != null && skillId >= 0 ? mgr.GetSkillById(skillId) : null;

                // 아이콘 슬롯이 실제로 배선돼 있고 스킬 아이콘이 있을 때만 아이콘 표시.
                // 그 외에는 항상 스킬명(또는 빈 슬롯 "-")을 보여줘 '빈 박스'가 남지 않게 한다.
                bool hasIcon = so != null && so.icon != null && slot.icon != null;
                if (slot.icon != null) slot.icon.gameObject.SetActive(hasIcon);
                if (hasIcon) slot.icon.sprite = so.icon;

                if (slot.label != null)
                {
                    if (so != null)
                    {
                        // 아이콘이 보이면 라벨 숨김, 아이콘이 없으면 스킬명 표시
                        slot.label.text = hasIcon ? "" : so.nameKor;
                        slot.label.fontSize = 22f;
                        slot.label.color = UguiTheme.TextPrimary;
                    }
                    else
                    {
                        slot.label.text = "-";
                        slot.label.fontSize = 28f;
                        slot.label.color = new Color(1f, 1f, 1f, 0.35f);
                    }
                }
            }
        }
    }
}
