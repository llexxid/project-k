using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>Panel_Guide 셸: 진행 바 + 진행 라벨 + 스크롤 리스트.</summary>
    public sealed class GuidePanelView : BottomSheetView
    {
        [SerializeField] internal Image progressFill;
        [SerializeField] internal TMP_Text progressLabel;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform listContent;
    }
}
