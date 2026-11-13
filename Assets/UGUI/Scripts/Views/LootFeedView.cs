using System;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using KingdomIdle.Rewards;
using KingdomIdle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>확정된 획득 알림의 표시만 담당한다. 지급·수령·계정 저장은 호출하지 않는다.</summary>
    public sealed class LootFeedView : MonoBehaviour
    {
        public const int Capacity = 3;
        public const float HoldSeconds = 2.2f, FadeSeconds = .15f, RowHeight = 82f, RowGap = 6f;

        [Serializable]
        public sealed class Row
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Image icon, border;
            public TMP_Text name, quantity, state;
            [NonSerialized] internal LootNotification notification;
            [NonSerialized] internal float elapsed;
            [NonSerialized] internal bool occupied, fading;
        }

        public CanvasGroup group;
        public Row[] rows = new Row[Capacity];
        private MainScreenView _main;
        private UIManager _host;
        private UIViewCatalog _catalog;
        private RectTransform _rect, _parent;
        private readonly Vector3[] _corners = new Vector3[4];
        private float _lastTime = -1f;
        private int _slots;
        private bool _bound, _blocked;
        internal int BindCount { get; private set; }
        internal int PlacementWrites { get; private set; }
        internal int AvailableSlots => _slots;
        internal int VisibleCount { get { int n = 0; foreach (var row in rows) if (row.occupied) n++; return n; } }

        // Core는 UGUI를 참조하지 않는다. 정적 판정만 주입해 화면 생명주기를 캡처하지 않는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallFilter() => LootNotifications.SetFilter(Allows);

        internal static bool Allows(LootNotification item)
        {
            if (GamePresentationSettings.HideItemNotifications) return false;
            var ui = UIManager.Instance;
            if (ui != null && ui.ActiveScreenId != UIScreenId.Main) return false;
            return item.Kind switch
            {
                LootNotificationKind.Currency => LootNotificationSettings.AllowsCurrency(item.Currency),
                LootNotificationKind.Equipment => LootNotificationSettings.AllowsEquipment((int)ItemCode.DecodeRarity(item.DefinitionId)),
                LootNotificationKind.MageSkill => LootNotificationSettings.ShowMageSkills,
                _ => false
            };
        }

        internal void Bind(MainScreenView main, UIManager host, UIViewCatalog catalog)
        {
            Unbind();
            _main = main; _host = host; _catalog = catalog;
            _rect = (RectTransform)transform; _parent = transform.parent as RectTransform;
            group.interactable = group.blocksRaycasts = false;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (var row in rows) ClearRow(row);
            LootNotifications.SetFilter(Allows);
            LootNotificationSettings.Changed += OnSettingsChanged;
            GamePresentationSettings.Changed += OnSettingsChanged;
            NumberNotation.Changed += RefreshQuantities;
            LocalProgression.AccountChanged += OnAccountChanged;
            _bound = true; _lastTime = -1f;
        }

        internal void Unbind()
        {
            if (!_bound) return;
            LootNotificationSettings.Changed -= OnSettingsChanged;
            GamePresentationSettings.Changed -= OnSettingsChanged;
            NumberNotation.Changed -= RefreshQuantities;
            LocalProgression.AccountChanged -= OnAccountChanged;
            foreach (var row in rows) ClearRow(row);
            // 영수증 중복 방지 기록은 Core 소유다. 화면 종료는 대기 표시만 폐기한다.
            LootNotifications.ClearPending();
            _bound = false; _main = null; _host = null; _catalog = null;
        }

        private void OnDestroy() => Unbind();
        private void OnEnable() { _lastTime = -1f; }
        private void OnDisable() { foreach (var row in rows) if (row != null) StopFade(row); _lastTime = -1f; }
        private void LateUpdate() { if (_bound) Tick(Time.unscaledTime); }

        internal void Tick(float now, bool? blockedOverride = null)
        {
            if (!_bound) return;
            float delta = _lastTime < 0f ? 0f : Mathf.Max(0f, now - _lastTime);
            _lastTime = now;
            bool blocked = blockedOverride ?? (_host == null || _host.ActiveScreenId != UIScreenId.Main ||
                _host.BlocksQuestBattleTime || (_host.ActiveMain?.HasOpenDropdown ?? false) ||
                Active(_main.waveHud?.deathPopup) || Active(_main.waveHud?.returnPopup));
            if (!blocked) blocked = !RefreshPlacement();
            if (blocked != _blocked)
            {
                foreach (var row in rows) StopFade(row);
                _blocked = blocked;
            }
            float alpha = blocked ? 0f : 1f;
            if (group.alpha != alpha) group.alpha = alpha;
            if (blocked) return;
            CompactRows();
            foreach (var row in rows)
            {
                if (!row.occupied || !row.rect.gameObject.activeSelf) continue;
                row.elapsed += delta;
                if (row.elapsed >= HoldSeconds + FadeSeconds) { ClearRow(row); continue; }
                if (row.elapsed >= HoldSeconds && !row.fading)
                {
                    row.fading = true;
                    UITween.ValueTo(row.group, row.group.alpha, 0f, HoldSeconds + FadeSeconds - row.elapsed,
                        value => row.group.alpha = value);
                }
            }
            CompactRows();
            for (int i = 0; i < _slots; i++)
            {
                if (rows[i].occupied) continue;
                while (LootNotifications.TryTake(out var item))
                {
                    if (!Allows(item)) continue;
                    Present(rows[i], item); break;
                }
            }
        }

        private static bool Active(GameObject value) => value != null && value.activeInHierarchy;

        /// <summary>실제 가이드 body가 없으면 현재 보이는 웨이브 HUD 하단을 사용한다.</summary>
        internal bool RefreshPlacement()
        {
            if (_parent == null || _main == null || _main.waveHud == null) return false;
            var wave = _main.waveHud;
            RectTransform anchor = wave.guide != null && Active(wave.guide.body)
                ? (RectTransform)wave.guide.body.transform
                : Active(wave.dungeonObjective) ? (RectTransform)wave.dungeonObjective.transform
                : Active(wave.mainHud) ? (RectTransform)wave.mainHud.transform : wave.topArea;
            if (anchor == null || !anchor.gameObject.activeInHierarchy) return false;
            Rect bounds = Bounds(anchor);
            float top = bounds.yMin - 10f;
            float bottom = _parent.rect.yMin + UguiTheme.BottomBarHeight + UguiTheme.PartyHudHeight + 20f;
            var party = PartyHudController.Instance != null ? PartyHudController.Instance.HudRect : null;
            if (party != null && party.gameObject.activeInHierarchy) bottom = Mathf.Max(bottom, Bounds(party).yMax + 12f);
            if (_main.bottomBar != null) bottom = Mathf.Max(bottom, Bounds(_main.bottomBar).yMax + 12f);
            int slots = Mathf.Clamp(Mathf.FloorToInt((top - bottom + RowGap) / (RowHeight + RowGap)), 0, Capacity);
            if (slots == 0) return false;
            // 활성 행이 가용 높이를 넘으면 보이지 않는 행만 일시 정지한다. 대기열은 계속 Core가 보존한다.
            float width = Mathf.Min(_parent.rect.width - 48f, _parent.rect.width * .58f);
            var size = new Vector2(width, Capacity * (RowHeight + RowGap) - RowGap);
            var position = new Vector2(24f, top - _parent.rect.yMax);
            if (_rect.sizeDelta != size || _rect.anchoredPosition != position)
            { _rect.sizeDelta = size; _rect.anchoredPosition = position; PlacementWrites++; }
            _slots = slots;
            return true;
        }

        private Rect Bounds(RectTransform value)
        {
            value.GetWorldCorners(_corners);
            Vector3 min = _parent.InverseTransformPoint(_corners[0]), max = min;
            for (int i = 1; i < 4; i++) { var p = _parent.InverseTransformPoint(_corners[i]); min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void CompactRows()
        {
            int next = 0;
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].occupied)
                {
                    var empty = rows[next]; rows[next] = rows[i]; rows[i] = empty; next++;
                }
            // 행 GameObject는 고정 3개다. 이동은 항목 추가·제거 때만 하며 레이아웃 그룹은 없다.
            int position = 0;
            foreach (var row in rows)
            {
                if (!row.occupied) continue;
                bool visible = position < _slots;
                if (row.rect.gameObject.activeSelf != visible)
                { StopFade(row); row.rect.gameObject.SetActive(visible); }
                var target = new Vector2(0f, -position * (RowHeight + RowGap));
                if (row.rect.anchoredPosition != target) row.rect.anchoredPosition = target;
                position++;
            }
        }

        private void Present(Row row, LootNotification item)
        {
            row.notification = item; row.elapsed = 0f; row.occupied = true; row.fading = false;
            Sprite icon = null; string name; Color border = UguiTheme.Bronze;
            switch (item.Kind)
            {
                case LootNotificationKind.Equipment:
                    var data = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetData(item.DefinitionId) : null;
                    name = data != null ? data.DisplayName : "장비";
                    icon = data != null ? data.icon : _catalog.iconChest;
                    border = UguiTheme.RarityColor(ItemCode.DecodeRarity(item.DefinitionId));
                    break;
                case LootNotificationKind.MageSkill:
                    var skill = MageTowerManager.Instance != null ? MageTowerManager.Instance.GetSkillById(item.DefinitionId) : null;
                    name = (skill != null ? skill.DisplayName(false) : "마탑 스킬") + (item.IsFragment ? " 파편" : "");
                    icon = skill != null ? skill.DisplayIcon(false) : _catalog.iconSkillWand;
                    break;
                default: name = RewardPresentation.Name(item.Currency); icon = RewardPresentation.Icon(item.Currency, _catalog); break;
            }
            row.name.text = name;
            row.quantity.text = "+" + NumberNotation.Format(item.Amount);
            row.state.text = item.Disposition switch
            {
                Protocol.Message.Dto.EquipmentDisposition.Mail => "우편 보관",
                Protocol.Message.Dto.EquipmentDisposition.Pending => "대기 보관",
                Protocol.Message.Dto.EquipmentDisposition.Reserve => "예비 보관",
                _ => ""
            };
            bool subtitle = row.state.text.Length > 0;
            row.name.rectTransform.offsetMin = new Vector2(76f, subtitle ? -43f : -68f);
            row.name.rectTransform.offsetMax = new Vector2(-152f, subtitle ? -7f : -14f);
            row.icon.sprite = icon; row.icon.enabled = icon != null; row.border.color = border;
            row.group.alpha = 1f; row.rect.gameObject.SetActive(true);
            BindCount++;
            CompactRows();
        }

        private static void StopFade(Row row) { if (row.group != null) UITween.StopValue(row.group); row.fading = false; }
        private static void ClearRow(Row row)
        {
            if (row == null) return;
            StopFade(row); row.occupied = false; row.elapsed = 0f;
            if (row.rect != null) row.rect.gameObject.SetActive(false);
        }
        private void OnSettingsChanged()
        {
            LootNotifications.Prune();
            foreach (var row in rows) if (row.occupied && !Allows(row.notification)) ClearRow(row);
            CompactRows();
        }
        internal void OnAccountChanged() { foreach (var row in rows) ClearRow(row); _lastTime = -1f; }
        private void RefreshQuantities() { foreach (var row in rows) if (row.occupied) row.quantity.text = "+" + NumberNotation.Format(row.notification.Amount); }
    }
}
