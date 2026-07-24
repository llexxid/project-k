using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 스탯 비교표의 한 행 (스탯명 / 현재 / 신규 / 변화). 프리팹: Item_StatCompareRow.prefab
    /// 헤더 행과 데이터 행을 겸한다. 변화 셀(cell3)은 항상 굵게, 색은 상승/하락에 따라 지정.
    /// </summary>
    public sealed class StatCompareRowView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text cell0;  // 스탯명 (좌측 정렬)
        [SerializeField] internal TMP_Text cell1;  // 현재
        [SerializeField] internal TMP_Text cell2;  // 신규
        [SerializeField] internal TMP_Text cell3;  // 변화

        public void Set(string c0, string c1, string c2, string c3,
            Color normalColor, Color diffColor, bool isHead)
        {
            var normalStyle = isHead ? FontStyles.Bold : FontStyles.Normal;
            if (cell0 != null) { cell0.text = c0; cell0.color = normalColor; cell0.fontStyle = normalStyle; }
            if (cell1 != null) { cell1.text = c1; cell1.color = normalColor; cell1.fontStyle = normalStyle; }
            if (cell2 != null) { cell2.text = c2; cell2.color = normalColor; cell2.fontStyle = normalStyle; }
            if (cell3 != null) { cell3.text = c3; cell3.color = diffColor; cell3.fontStyle = FontStyles.Bold; }
        }
    }
}
