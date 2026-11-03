using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class ShopPopupView : BottomSheetView
    {
        [SerializeField] internal TMP_Text wallet, income;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal Button[] tabs;
        [SerializeField] internal GameObject[] pages;
        [SerializeField] internal ShopOfferView[] offers;

        internal bool Bound;
        internal int SelectedTab;

        void OnDisable() => ShopPopupController.Detach(this);
    }
}
