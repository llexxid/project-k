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
        Coroutine _reveal;
        bool _shown;
        int _layoutKey;
        int _columns = 1;
        Vector2Int _screenSize;
        RectTransform _guide;
        readonly Vector3[] _guideCorners = new Vector3[4];
        int _slot = -1, _skillId = -1;
        string _battle;
        Vector3 _point;
        bool _valid;

        void Update()
        {
            var manager = MageTowerManager.Instance; var ui = UIManager.Instance;
            bool show = manager != null && !manager.IsAutoEnabled() && ui != null && ui.ActiveScreenId == UIScreenId.Main &&
                !ui.HasBlockingPanel && !ui.HasActiveTabPanel && PartyHudController.ModalSuppressCount == 0;
            int layoutKey = 17;
            if (manager != null) for (int i = 0; i < buttons.Length; i++) layoutKey = unchecked(layoutKey * 31 + manager.GetEquippedSkillId(i));
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (show != _shown || (show && (layoutKey != _layoutKey || screenSize != _screenSize)))
            {
                _shown = show;
                _layoutKey = layoutKey;
                _screenSize = screenSize;
                if (_reveal != null) StopCoroutine(_reveal);
                if (show) { tray.gameObject.SetActive(true); FitColumns(); _reveal = StartCoroutine(Reveal()); }
                else { CancelAim(); tray.gameObject.SetActive(false); }
            }
            if (_slot >= 0 && (manager == null || manager.GetEquippedSkillId(_slot) != _skillId ||
                LocalProgression.State.ActiveBattleId != _battle || StageManager.Instance?.CurrentRunState != eStageRunState.Running)) CancelAim();
        }

        void FitColumns()
        {
            // Keep the full touch targets below the guide on short screens. Extra slots
            // roll out to the right in additional columns instead of hiding behind it.
            if (_guide == null)
            {
                var main = FindFirstObjectByType<MainScreenView>();
                if (main != null) _guide = main.transform.Find("GuideGoal") as RectTransform;
            }
            int rows = buttons.Length;
            if (_guide != null)
            {
                _guide.GetWorldCorners(_guideCorners);
                float available = tray.InverseTransformPoint(_guideCorners[0]).y - 6;
                rows = Mathf.Clamp(Mathf.FloorToInt(available / 118), 1, buttons.Length);
            }
            int equipped = 0;
            for (int i = 0; i < buttons.Length; i++) if (MageTowerManager.Instance.GetEquippedSkillId(i) >= 0) equipped++;
            _columns = Mathf.Max(1, Mathf.CeilToInt((float)equipped / rows));
        }

        IEnumerator Reveal()
        {
            group.blocksRaycasts = false;
            float time = 0;
            while (time < .55f)
            {
                time += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(time / .12f);
                int visible = 0;
                for (int i = 0; i < buttons.Length; i++)
                {
                    bool equipped = MageTowerManager.Instance.GetEquippedSkillId(i) >= 0;
                    if (buttons[i].gameObject.activeSelf != equipped) buttons[i].gameObject.SetActive(equipped);
                    if (!equipped) continue;
                    var rt = (RectTransform)buttons[i].transform;
                    int order = visible++;
                    float t = Mathf.Clamp01((time - order * .055f) / .28f);
                    float ease = 1 - Mathf.Pow(1 - t, 3);
                    float x = _columns == 1 ? (order % 2 == 0 ? -12 : 12) : -12 + order % _columns * 118;
                    rt.anchoredPosition = new Vector2(Mathf.Lerp(0, x, ease), Mathf.Lerp(-60, 62 + order / _columns * 118, ease));
                    rt.localScale = Vector3.one * Mathf.Lerp(.45f, 1, ease);
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(i % 2 == 0 ? -65 : 65, 0, ease));
                }
                yield return null;
            }
            group.alpha = 1; group.blocksRaycasts = true; _reveal = null;
        }

        public void BeginAim(int slot, PointerEventData data)
        {
            CancelAim();
            var manager = MageTowerManager.Instance;
            var skill = manager?.GetSkillById(manager.GetEquippedSkillId(slot));
            if (!_shown || skill == null || !skill.CanAim || manager.IsOnCooldown(slot) || manager.IsCasting(slot) ||
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
        void OnDisable() { CancelAim(); _shown = false; if (tray != null) tray.gameObject.SetActive(false); }
        void OnDestroy() { if (_aim != null) Destroy(_aim.gameObject); }
    }
}
