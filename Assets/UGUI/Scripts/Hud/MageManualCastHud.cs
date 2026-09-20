using System.Collections;
using System.Collections.Generic;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using KingdomIdle.UI;
using Scripts.Core.Manager;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomIdle.UGUI
{
    public sealed class MageManualCastHud : MonoBehaviour
    {
        [SerializeField] internal RectTransform tray;
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal MageManualSkillButton[] buttons;
        [SerializeField] internal GameObject aimPrefab;
        MagicAimGraphic _aim;
        readonly List<RaycastResult> _hits = new();
        bool _shown;
        float _visibility;
        int _layoutKey;
        int _rows = 5;
        float _cell = 132;
        RectTransform _guide, _stage;
        MainScreenView _main;
        readonly Vector3[] _corners = new Vector3[4];
        int _slot = -1, _skillId = -1;
        string _battle;
        Vector3 _point;
        bool _valid;

        void Awake() { foreach (var cell in buttons) cell.Bind(this); }

        void LateUpdate()
        {
            var manager = MageTowerManager.Instance; var ui = UIManager.Instance;
            bool available = ui != null && ui.ActiveScreenId == UIScreenId.Main &&
                !ui.HasBlockingPanel && !ui.HasActiveTabPanel && PartyHudController.ModalSuppressCount == 0 &&
                !MageTowerPopupController.IsOpen && !MageTowerDetailPopupController.IsOpen;
            bool show = manager != null && !manager.IsAutoEnabled() && available;
            int layoutKey = 17;
            if (manager != null) for (int i = 0; i < buttons.Length; i++) layoutKey = unchecked(layoutKey * 31 + manager.GetEquippedSkillId(i));
            if (layoutKey != _layoutKey)
            {
                CancelAim(); _layoutKey = layoutKey;
                for (int i = 0; i < buttons.Length; i++)
                    buttons[i].gameObject.SetActive(manager != null && manager.GetEquippedSkillId(i) >= 0);
            }
            if (show != _shown)
            {
                _shown = show; CancelAim();
                group.blocksRaycasts = group.interactable = false;
                if (show) tray.gameObject.SetActive(true);
                UITween.ValueTo(tray, _visibility, show ? 1 : 0, .46f * Mathf.Abs((show ? 1 : 0) - _visibility),
                    value => { _visibility = value; ApplyReveal(); }, () => {
                        if (!_shown) tray.gameObject.SetActive(false);
                        group.blocksRaycasts = group.interactable = _shown;
                    });
            }
            // A modal consumes input immediately; auto toggles retain the entire exit animation.
            group.alpha = available ? 1 : 0;
            if (tray.gameObject.activeSelf) { PositionTray(); ApplyReveal(); }
            if (_slot >= 0 && (manager == null || manager.GetEquippedSkillId(_slot) != _skillId ||
                LocalProgression.State.ActiveBattleId != _battle || StageManager.Instance?.CurrentRunState != eStageRunState.Running)) CancelAim();
        }

        void PositionTray()
        {
            if (_main == null) _main = FindFirstObjectByType<MainScreenView>();
            var main = _main;
            if (main == null) return;
            if (_guide == null) _guide = main.transform.Find("GuideGoal") as RectTransform;
            if (_stage == null && main.waveHud != null) _stage = (RectTransform)main.waveHud.transform;
            var screens = UIManager.Instance.LayerScreens;
            if (tray.parent != screens)
            {
                tray.SetParent(screens, false);
                tray.anchorMin = tray.anchorMax = screens.pivot;
            }
            var parent = tray.parent as RectTransform;
            if (_stage == null || parent == null) return;
            var canvas = parent.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Screen.safeArea.max, camera, out var safeTopRight);
            _stage.GetWorldCorners(_corners);
            float bottom = parent.InverseTransformPoint(_corners[1]).y + 18;
            float top = safeTopRight.y - 190;
            if (_guide != null)
            {
                _guide.GetWorldCorners(_corners);
                // The guide occupies the left side. Only reserve its height if it reaches this column.
                if (parent.InverseTransformPoint(_corners[2]).x > Mathf.Min(safeTopRight.x, parent.rect.xMax) - 154)
                    top = Mathf.Min(top, parent.InverseTransformPoint(_corners[0]).y - 18);
            }
            int count = 0;
            foreach (var cell in buttons) if (cell.gameObject.activeSelf) count++;
            float available = Mathf.Max(132, top - bottom);
            _cell = Mathf.Clamp((available - Mathf.Max(0, count - 1) * 10) / Mathf.Max(1, count), 116, 132);
            _rows = Mathf.Max(1, Mathf.FloorToInt((available + 10) / (_cell + 10)));
            int columns = Mathf.Max(1, Mathf.CeilToInt((float)count / _rows));
            var size = new Vector2(columns * (_cell + 10) - 10, available);
            var position = new Vector3(Mathf.Min(safeTopRight.x, parent.rect.xMax) - 22, bottom, 0);
            if (tray.pivot != new Vector2(1, 0)) tray.pivot = new Vector2(1, 0);
            if (tray.sizeDelta != size) tray.sizeDelta = size;
            if (tray.localPosition != position) tray.localPosition = position;
        }

        void ApplyReveal()
        {
            int order = 0;
            foreach (var cell in buttons)
            {
                if (!cell.gameObject.activeSelf) continue;
                int index = order++;
                float t = Mathf.Clamp01((_visibility * .46f - index * .035f) / .30f);
                float eased = 1 - Mathf.Pow(1 - t, 3);
                var rt = (RectTransform)cell.transform;
                Vector2 target = new Vector2(-_cell * .5f - index / _rows * (_cell + 10), _cell * .5f + index % _rows * (_cell + 10));
                var size = Vector2.one * _cell;
                var position = Vector2.Lerp(new Vector2(_cell * .7f, -24), target, eased);
                var scale = Vector3.one * Mathf.Lerp(.55f, 1, eased);
                if (rt.sizeDelta != size) rt.sizeDelta = size;
                if (rt.anchoredPosition != position) rt.anchoredPosition = position;
                if (rt.localScale != scale) rt.localScale = scale;
                if (cell.visibility != null && cell.visibility.alpha != t) cell.visibility.alpha = t;
            }
        }

        public void BeginAim(int slot, PointerEventData data)
        {
            CancelAim();
            var manager = MageTowerManager.Instance;
            var skill = manager?.GetSkillById(manager.GetEquippedSkillId(slot));
            if (!_shown || skill == null || !skill.CanAimWithBloom(manager.IsBloomEnabled(skill.id)) || manager.IsOnCooldown(slot) || manager.IsCasting(slot) ||
                StageManager.Instance?.CurrentRunState != eStageRunState.Running) return;
            _slot = slot; _skillId = skill.id; _battle = LocalProgression.State.ActiveBattleId;
            if (_aim == null) _aim = Instantiate(aimPrefab, UIManager.Instance.LayerScreens, false).GetComponent<MagicAimGraphic>();
            _aim.healing = skill.IsHealing;
            _aim.gameObject.SetActive(true);
            MoveAim(data);
        }

        public void MoveAim(PointerEventData data)
        {
            if (_slot < 0 || _aim == null) return;
            var camera = MageTowerTargeting.ResolveCamera();
            var manager = MageTowerManager.Instance;
            var skill = manager?.GetSkillById(_skillId);
            if (camera == null || skill == null) { CancelAim(); return; }
            _point = camera.ScreenToWorldPoint(new Vector3(data.position.x, data.position.y, -camera.transform.position.z));
            _point.z = 0;
            _valid = MageTowerManager.IsValidAimPoint(_point);
            _hits.Clear(); EventSystem.current.RaycastAll(data, _hits);
            foreach (var hit in _hits)
            {
                if (hit.gameObject == null || hit.gameObject.transform.IsChildOf(tray)) continue;
                // A visible UI control consumes the drop; scenery and the preview do not raycast.
                _valid = false; break;
            }
            float radius = skill.TargetRadius(manager.IsBloomEnabled(skill.id));
            var parent = (RectTransform)_aim.transform.parent;
            var canvas = parent.GetComponentInParent<Canvas>();
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, camera.WorldToScreenPoint(_point), uiCamera, out var center);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, camera.WorldToScreenPoint(_point + Vector3.right * radius), uiCamera, out var edge);
            var rt = (RectTransform)_aim.transform;
            rt.anchorMin = rt.anchorMax = parent.pivot;
            rt.anchoredPosition = center;
            rt.sizeDelta = Vector2.one * Mathf.Abs(edge.x - center.x) * 2;
            _aim.worldRadius = radius;
            _aim.valid = _valid;
        }

        public void EndAim(PointerEventData data)
        {
            if (_slot < 0) return;
            MoveAim(data);
            var manager = MageTowerManager.Instance;
            if (_valid && _slot >= 0 && LocalProgression.State.ActiveBattleId == _battle && manager != null && manager.GetEquippedSkillId(_slot) == _skillId)
                manager.CastSkillAt(_slot, _point);
            CancelAim();
        }
        public void CancelAim() { _slot = _skillId = -1; _valid = false; if (_aim != null) _aim.gameObject.SetActive(false); }
        void OnApplicationFocus(bool focused) { if (!focused) CancelAim(); }
        void OnDisable() { CancelAim(); _shown = false; _visibility = 0; if (tray != null) tray.gameObject.SetActive(false); }
        void OnDestroy() { if (_aim != null) Destroy(_aim.gameObject); if (tray != null && tray.parent != transform) Destroy(tray.gameObject); }
    }
}
