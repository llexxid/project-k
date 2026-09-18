using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using Scripts.Core.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class MageManualSkillButton : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] internal int slot;
        [SerializeField] internal Image icon, cooldown;
        [SerializeField] internal TMP_Text label;
        [SerializeField] internal Button button;
        MageManualCastHud _hud;
        bool _dragged;
        float _nextRefresh;
        void Awake()
        {
            _hud = GetComponentInParent<MageManualCastHud>();
            button.onClick.AddListener(() => {
                if (_dragged) { _dragged = false; return; }
                var manager = MageTowerManager.Instance;
                if (manager != null && !manager.CastSkill(slot))
                    UIManager.Instance?.ShowToast(manager.IsOnCooldown(slot) ? "스킬을 준비 중입니다." : "시전할 대상이 없습니다.");
            });
        }
        void OnEnable() { _nextRefresh = 0; _dragged = false; }
        void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .05f;
            var manager = MageTowerManager.Instance;
            var skill = manager?.GetSkillById(manager.GetEquippedSkillId(slot));
            if (skill == null) { icon.enabled = false; button.interactable = false; label.text = "비어 있음"; return; }
            var sprite = skill.DisplayIcon(manager.IsBloomEnabled(skill.id));
            if (icon.sprite != sprite) icon.sprite = sprite;
            icon.enabled = true;
            float ratio = manager.GetCooldownRatio(slot);
            cooldown.fillAmount = ratio;
            bool ready = ratio <= 0 && !manager.IsCasting(slot);
            button.interactable = true; // Cooldown buttons still explain their state.
            string text = ready ? skill.nameKor : $"{Mathf.CeilToInt(ratio * manager.GetEffectiveCooldown(skill.id))}초";
            if (label.text != text) label.text = text;
        }
        public void OnPointerDown(PointerEventData data) => _dragged = false;
        public void OnBeginDrag(PointerEventData data)
        {
            _dragged = true;
            _hud?.BeginAim(slot, data);
        }
        public void OnDrag(PointerEventData data) => _hud?.MoveAim(data);
        public void OnEndDrag(PointerEventData data) { _hud?.EndAim(data); _dragged = true; }
        void OnDisable() => _hud?.CancelAim();
    }
}
