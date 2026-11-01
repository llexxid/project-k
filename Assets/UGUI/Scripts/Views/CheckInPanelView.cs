using TMPro;

namespace KingdomIdle.UGUI
{
    public sealed class CheckInPanelView : BottomSheetView
    {
        public RewardSlotView[] slots;
        public TMP_Text monthLabel, countLabel, countdown;
        public UnityEngine.UI.GridLayoutGroup grid;
    }
}
