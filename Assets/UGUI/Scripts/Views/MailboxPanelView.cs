using TMPro;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class MailboxPanelView : BottomSheetView
    {
        public RectTransform content;
        public TMP_Text emptyLabel, totalLabel, unclaimedLabel, claimedLabel, claimAllLabel;
        public UnityEngine.UI.Button claimAll, deleteClaimed;
    }
}
