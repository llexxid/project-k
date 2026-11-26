using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class EquipmentToolbarView : MonoBehaviour
    {
        public TMP_Text summary;
        public Button direction, sort, automatic, dismantle;
        public Image directionArrow, autoBorder;
        private Action _refresh;
        private bool _bound;
        private static readonly string[] Sorts = { "등급별", "직업별", "이름별", "획득순" };

        public void Bind(Action refresh)
        {
            _refresh = refresh;
            if (!_bound)
            {
                direction.onClick.AddListener(() => Change(s => s.EquipmentSortAscending = !s.EquipmentSortAscending, _refresh));
                sort.onClick.AddListener(() => Change(s => s.EquipmentSort = (s.EquipmentSort + 1) % Sorts.Length, _refresh));
                automatic.onClick.AddListener(EquipmentActionDialog.Settings);
                dismantle.onClick.AddListener(() => EquipmentActionDialog.BulkDismantle(_refresh));
                _bound = true;
            }
            Label(sort, Sorts[LocalProgression.State.EquipmentSort]);
            directionArrow.rectTransform.localEulerAngles = new Vector3(0, 0, LocalProgression.State.EquipmentSortAscending ? -90 : 90);
            RefreshSummary();
        }

        public bool Accepts(int code) => EquipmentManager.Instance?.GetData(code) != null;

        public bool HasMatches => LocalProgression.State.Equipment.Any(x => Accepts(x.Code)) ||
            LocalProgression.State.PendingEquipment.Any(x => Accepts(x.Code)) || LocalProgression.State.LegacyEquipment.Any(x => Accepts(x.Code));

        public IEnumerable<EquipmentInstance> Sort(IEnumerable<EquipmentInstance> items)
        {
            var available = items.Where(item => item?.baseData != null);
            var state = LocalProgression.State;
            // Stable server IDs preserve acquisition order across inventory sorts and fresh snapshots.
            var indexed = available.Select((item, index) => (item, acquired: item.serverInstanceId ?? index));
            var ordered = state.EquipmentSort switch
            {
                1 => state.EquipmentSortAscending
                    ? indexed.OrderBy(x => (int)ItemCode.DecodeJobMask(x.item.baseData.itemCode))
                    : indexed.OrderByDescending(x => (int)ItemCode.DecodeJobMask(x.item.baseData.itemCode)),
                2 => state.EquipmentSortAscending
                    ? indexed.OrderBy(x => x.item.baseData.DisplayName, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ko-KR"), false))
                    : indexed.OrderByDescending(x => x.item.baseData.DisplayName, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ko-KR"), false)),
                3 => state.EquipmentSortAscending ? indexed.OrderBy(x => x.acquired) : indexed.OrderByDescending(x => x.acquired),
                _ => state.EquipmentSortAscending ? indexed.OrderBy(x => x.item.baseData.rarity) : indexed.OrderByDescending(x => x.item.baseData.rarity)
            };
            return ordered.ThenByDescending(x => x.item.enhancementLevel).ThenByDescending(x => x.acquired).Select(x => x.item);
        }

        private static void Label(Button button, string text) => button.GetComponentInChildren<TMP_Text>().text = text;
        private static void Change(Action<ProgressionState> change, Action refresh)
        { if (LocalProgression.Execute("equipment-browse", s => { change(s); return true; })) refresh?.Invoke(); }
        private void OnEnable() => LocalProgression.Changed += RefreshSummary;
        private void OnDisable() => LocalProgression.Changed -= RefreshSummary;
        private void RefreshSummary()
        {
            var s = LocalProgression.State;
            if (autoBorder != null)
            {
                bool enabled = s.AutoDismantleMask != 0;
                if (enabled && !autoBorder.gameObject.activeSelf)
                {
                    autoBorder.gameObject.SetActive(true);
                    UITween.PulseGraphic(autoBorder, UguiTheme.Bronze, UguiTheme.ActionGold, 1.8f);
                }
                else if (!enabled && autoBorder.gameObject.activeSelf)
                {
                    UITween.StopGraphicPulse(autoBorder);
                    autoBorder.gameObject.SetActive(false);
                }
            }
            long stored = s.PendingEquipment.Count + s.LegacyEquipment.Sum(x => (long)x.Count);
            string text = $"보유 {s.Equipment.Count}/{EquipmentManager.InventoryCapacity}  ·  강화석 {NumberNotation.Format(LocalProgression.Balance(eCurrency.EquipmentStone))}";
            if (stored > 0) text += $"\n추가 보관 {NumberNotation.Format(stored)} · 기한 없이 보관";
            if (summary == null || summary.text == text) return;
            summary.text = text;
            var line = summary.GetComponent<LayoutElement>();
            var panel = GetComponent<LayoutElement>();
            if (line != null && panel != null)
            {
                float height = stored > 0 ? 92 : 48;
                panel.preferredHeight += height - line.preferredHeight;
                line.preferredHeight = height;
            }
        }
    }
}
