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
        public Button job, rarity, sort, automatic, dismantle;
        private Func<EquipmentData, bool> _usable;
        private Action _refresh;
        private bool _bound;
        private static readonly string[] Rarities = { "전체", "일반", "레어", "에픽" };
        private static readonly string[] Sorts = { "등급", "공격력", "강화", "획득순" };

        public void Bind(Func<EquipmentData, bool> usable, Action refresh, bool hasSettingsShortcut = false)
        {
            _usable = usable;
            _refresh = refresh;
            automatic.gameObject.SetActive(!hasSettingsShortcut);
            if (!_bound)
            {
                job.onClick.AddListener(() => Change(s => s.EquipmentUsableOnly = !s.EquipmentUsableOnly, _refresh));
                rarity.onClick.AddListener(() => Change(s => s.EquipmentRarityFilter = (s.EquipmentRarityFilter + 2) % 4 - 1, _refresh));
                sort.onClick.AddListener(() => Change(s => s.EquipmentSort = (s.EquipmentSort + 1) % 4, _refresh));
                automatic.onClick.AddListener(EquipmentActionDialog.Settings);
                dismantle.onClick.AddListener(() =>
                {
                    var filters = LocalProgression.State;
                    string rarityName = Rarities[filters.EquipmentRarityFilter + 1];
                    string jobName = filters.EquipmentUsableOnly ? "사용 가능" : "전체";
                    EquipmentActionDialog.Dismantle(EquipmentEconomy.Preview(Accepts), _refresh, $"등급 {rarityName} · 직업 {jobName}");
                });
                _bound = true;
            }
            var state = LocalProgression.State;
            Label(job, state.EquipmentUsableOnly ? "직업\n사용가능" : "직업\n전체");
            Label(rarity, "등급\n" + Rarities[state.EquipmentRarityFilter + 1]);
            Label(sort, "정렬\n" + Sorts[state.EquipmentSort]);
            Label(automatic, "장비\n설정");
            Label(dismantle, "일괄\n분해");
            RefreshSummary();
        }

        public bool Accepts(int code)
        {
            var data = EquipmentManager.Instance?.GetData(code);
            var state = LocalProgression.State;
            return data != null && (state.EquipmentRarityFilter < 0 || (int)data.rarity == state.EquipmentRarityFilter) &&
                (!state.EquipmentUsableOnly || _usable == null || _usable(data));
        }

        public bool HasMatches => LocalProgression.State.Equipment.Any(x=>Accepts(x.Code)) ||
            LocalProgression.State.PendingEquipment.Any(x=>Accepts(x.Code)) || LocalProgression.State.LegacyEquipment.Any(x=>Accepts(x.Code));

        public IEnumerable<EquipmentInstance> Sort(IEnumerable<EquipmentInstance> items)
        {
            var indexed = items.Select((item, index) => (item, index)).Where(x => x.item?.baseData != null && Accepts(x.item.baseData.itemCode));
            return (LocalProgression.State.EquipmentSort switch {
                1 => indexed.OrderByDescending(x => x.item.GetFinalAtk()).ThenByDescending(x => x.index),
                2 => indexed.OrderByDescending(x => x.item.enhancementLevel).ThenByDescending(x => x.index),
                3 => indexed.OrderByDescending(x => x.index),
                _ => indexed.OrderByDescending(x => x.item.baseData.rarity).ThenByDescending(x => x.item.enhancementLevel).ThenByDescending(x => x.index)
            }).Select(x => x.item);
        }

        private static void Label(Button button, string text) => button.GetComponentInChildren<TMP_Text>().text = text;
        private static void Change(Action<ProgressionState> change, Action refresh)
        { if (LocalProgression.Execute("equipment-browse", s => { change(s); return true; })) refresh?.Invoke(); }
        private void OnEnable() => LocalProgression.Changed += RefreshSummary;
        private void OnDisable() => LocalProgression.Changed -= RefreshSummary;
        private void RefreshSummary()
        {
            var s = LocalProgression.State;
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
