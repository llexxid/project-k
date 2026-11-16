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
        public const float HoldSeconds = 2.2f, FadeSeconds = .20f, RowHeight = 50f, RowGap = 4f;
        public const float WidthRatio = .348f, MaxHoldSeconds = 2.8f, BorderOpacity = .32f;
        internal const float EnterSeconds = .14f, RefreshOutSeconds = .10f, RefreshInSeconds = .12f, MinimumReadSeconds = 1f;
        internal enum MotionPhase { Entering, Holding, RefreshOut, RefreshIn, Exiting }

        [Serializable]
        public sealed class Row
        {
            public RectTransform rect;
            public RectTransform visual;
            public CanvasGroup group;
            public Image icon, border;
            public TMP_Text name, quantity, state;
            [NonSerialized] internal LootNotification notification;
            [NonSerialized] internal float elapsed, holdSeconds;
            [NonSerialized] internal bool occupied, fading;
            [NonSerialized] internal MotionPhase phase;
            [NonSerialized] internal float phaseTime, enterDelay, nextRefreshAge, readUntilAge, exitAlpha, exitY;
            [NonSerialized] internal bool refreshPending, layoutReady;
            [NonSerialized] internal float layoutFrom, layoutTarget, layoutTime;
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
        private void OnDisable() { _lastTime = -1f; }
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
            // 직전 프레임이 가려졌으면 복귀까지의 긴 간격은 읽을 수 없었던 시간이다.
            if (_blocked) delta = 0f;
            _blocked = blocked;
            float alpha = blocked ? 0f : 1f;
            if (group.alpha != alpha) group.alpha = alpha;
            if (blocked) return;
            int position = 0;
            foreach (var row in rows)
            {
                if (!row.occupied) continue;
                // 배치 갱신으로 다시 나타나는 행은 숨겨진 동안의 시간을 차감하지 않는다.
                if (position++ < _slots && row.rect.gameObject.activeSelf) Advance(row, delta);
            }
            CompactRows();
            // 대량 획득은 같은 표시 행의 수량만 갱신한다. 서로 다른 대기 항목의 순서와 만료는 큐가 보존한다.
            if (LootNotifications.Count > 0)
                foreach (var row in rows)
                {
                    if (!row.occupied || !row.rect.gameObject.activeSelf || row.fading ||
                        row.elapsed > MaxHoldSeconds - .65f || !Allows(row.notification)) continue;
                    if (!LootNotifications.TryTakeMatching(row.notification, out var incoming)) continue;
                    row.notification.Amount += incoming.Amount;
                    if (row.phase == MotionPhase.Entering) SetQuantity(row);
                    else if (row.phase != MotionPhase.RefreshOut) row.refreshPending = true;
                    // 늦은 수량 갱신은 잠깐 더 읽게 하되 반복 획득이 한 행을 영구 점유하지 않게 한다.
                    row.holdSeconds = Mathf.Min(MaxHoldSeconds, Mathf.Max(row.holdSeconds, row.elapsed + .65f));
                    row.readUntilAge = Mathf.Min(MaxHoldSeconds, Mathf.Max(row.readUntilAge, row.elapsed + .65f));
                    StartRefreshIfReady(row);
                }
            // 새 종류가 기다리면 충분히 읽은 한 행만 먼저 퇴장시킨다. 슬롯이 비기 전에는 큐를 소비하지 않는다.
            if (LootNotifications.Count > 0)
            {
                bool full = true, exiting = false;
                for (int i = 0; i < _slots; i++) { full &= rows[i].occupied; exiting |= rows[i].fading; }
                if (full && !exiting)
                    for (int i = 0; i < _slots; i++)
                        if (rows[i].phase == MotionPhase.Holding && rows[i].elapsed >= rows[i].readUntilAge)
                        { BeginExit(rows[i], 0f); break; }
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
            foreach (var row in rows)
                if (row.occupied && row.rect.gameObject.activeSelf) RenderMotion(row, delta);
        }

        private static void SetQuantity(Row row)
        {
            string text = "+" + NumberNotation.Format(row.notification.Amount);
            if (row.quantity.text != text) row.quantity.text = text;
        }

        private static void StartRefreshIfReady(Row row)
        {
            if (!row.refreshPending || row.phase != MotionPhase.Holding || row.elapsed < row.nextRefreshAge) return;
            row.refreshPending = false; row.phase = MotionPhase.RefreshOut; row.phaseTime = 0f;
            row.nextRefreshAge = row.elapsed + .30f;
        }

        private static void BeginExit(Row row, float elapsed)
        {
            SetQuantity(row);
            row.refreshPending = false; row.fading = true; row.phase = MotionPhase.Exiting; row.phaseTime = elapsed;
            row.exitAlpha = row.group.alpha; row.exitY = row.visual != null ? row.visual.anchoredPosition.y : 0f;
        }

        private static void Advance(Row row, float delta)
        {
            row.elapsed += delta; row.phaseTime += delta;
            if (!row.fading && row.elapsed >= row.holdSeconds) BeginExit(row, row.elapsed - row.holdSeconds);
            if (row.fading)
            {
                if (row.phaseTime >= FadeSeconds) ClearRow(row);
                return;
            }
            if (row.phase == MotionPhase.Entering && row.phaseTime >= EnterSeconds + row.enterDelay)
            { row.phase = MotionPhase.Holding; row.phaseTime = 0f; }
            if (row.phase == MotionPhase.RefreshOut && row.phaseTime >= RefreshOutSeconds)
            { SetQuantity(row); row.phase = MotionPhase.RefreshIn; row.phaseTime -= RefreshOutSeconds; }
            if (row.phase == MotionPhase.RefreshIn && row.phaseTime >= RefreshInSeconds)
            { row.phase = MotionPhase.Holding; row.phaseTime = 0f; }
            StartRefreshIfReady(row);
        }

        private static void RenderMotion(Row row, float delta)
        {
            // 동일 unscaled clock으로 상태를 진행시켜 메뉴 중단·풀 재사용에 지연 콜백이 남지 않는다.
            float travel = GamePresentationSettings.LowSpec ? 3f : 8f;
            float y = 0f, alpha = 1f;
            switch (row.phase)
            {
                case MotionPhase.Entering:
                    float enter = UITween.EaseOutCubic(Mathf.Clamp01((row.phaseTime - row.enterDelay) / EnterSeconds));
                    y = Mathf.Lerp(-travel, 0f, enter); alpha = enter; break;
                case MotionPhase.RefreshOut:
                    float leave = Mathf.Clamp01(row.phaseTime / RefreshOutSeconds);
                    y = travel * .75f * leave; alpha = 1f - leave; break;
                case MotionPhase.RefreshIn:
                    float refresh = UITween.EaseOutCubic(Mathf.Clamp01(row.phaseTime / RefreshInSeconds));
                    y = Mathf.Lerp(-travel * .75f, 0f, refresh); alpha = refresh; break;
                case MotionPhase.Exiting:
                    float exit = Mathf.Clamp01(row.phaseTime / FadeSeconds);
                    y = Mathf.Lerp(row.exitY, travel, UITween.EaseOutCubic(exit)); alpha = row.exitAlpha * (1f - exit); break;
            }
            if (row.visual != null)
            {
                var offset = new Vector2(0f, y);
                if (row.visual.anchoredPosition != offset) row.visual.anchoredPosition = offset;
            }
            if (row.group.alpha != alpha) row.group.alpha = alpha;
            row.layoutTime = Mathf.Min(.16f, row.layoutTime + delta);
            var position = new Vector2(0f, Mathf.Lerp(row.layoutFrom, row.layoutTarget,
                UITween.EaseOutCubic(row.layoutTime / .16f)));
            if (row.rect.anchoredPosition != position) row.rect.anchoredPosition = position;
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
            float width = Mathf.Min(_parent.rect.width - 48f, Mathf.Max(368f, _parent.rect.width * WidthRatio));
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
                if (row.rect.gameObject.activeSelf != visible) row.rect.gameObject.SetActive(visible);
                float target = -position * (RowHeight + RowGap);
                if (!row.layoutReady)
                {
                    row.layoutReady = true; row.layoutFrom = row.layoutTarget = target; row.layoutTime = .16f;
                    row.rect.anchoredPosition = new Vector2(0f, target);
                }
                else if (row.layoutTarget != target)
                { row.layoutFrom = row.rect.anchoredPosition.y; row.layoutTarget = target; row.layoutTime = 0f; }
                position++;
            }
        }

        private void Present(Row row, LootNotification item)
        {
            row.notification = item; row.elapsed = 0f; row.holdSeconds = HoldSeconds; row.occupied = true; row.fading = false;
            row.phase = MotionPhase.Entering; row.phaseTime = 0f; row.nextRefreshAge = 0f;
            row.refreshPending = false; row.layoutReady = false;
            row.enterDelay = 0f;
            foreach (var other in rows)
                if (other != row && other.occupied && other.layoutReady && other.layoutTime < .16f)
                { row.enterDelay = .16f; break; }
            row.readUntilAge = MinimumReadSeconds + row.enterDelay;
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
            row.name.rectTransform.offsetMin = new Vector2(52f, subtitle ? -28f : -44f);
            row.name.rectTransform.offsetMax = new Vector2(-142f, subtitle ? 0f : -6f);
            border.a = BorderOpacity;
            row.icon.sprite = icon; row.icon.enabled = icon != null; row.border.color = border;
            row.group.alpha = 0f; row.rect.gameObject.SetActive(true);
            BindCount++;
            CompactRows();
        }

        private static void ClearRow(Row row)
        {
            if (row == null) return;
            row.occupied = row.fading = row.refreshPending = row.layoutReady = false;
            row.elapsed = row.phaseTime = 0f;
            if (row.group != null) row.group.alpha = 1f;
            if (row.visual != null) row.visual.anchoredPosition = Vector2.zero;
            if (row.rect != null) row.rect.gameObject.SetActive(false);
        }
        private void OnSettingsChanged()
        {
            LootNotifications.Prune();
            foreach (var row in rows) if (row.occupied && !Allows(row.notification)) ClearRow(row);
            CompactRows();
        }
        internal void OnAccountChanged() { foreach (var row in rows) ClearRow(row); _lastTime = -1f; }
        private void RefreshQuantities() { foreach (var row in rows) if (row.occupied) SetQuantity(row); }
    }
}
