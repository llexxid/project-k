using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class ShopPopupView : MonoBehaviour
    {
        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Fit(); }
        void Fit()
        {
            if (panel == null || transform.parent is not RectTransform parent) return;
            var size = new Vector2(Mathf.Min(980, parent.rect.width - 48), Mathf.Min(1460, parent.rect.height - 64));
            if (size.x > 0 && size.y > 0 && panel.sizeDelta != size) panel.sizeDelta = size;
        }
        [SerializeField] internal RectTransform panel;
        [SerializeField] internal Button backdrop, close;
        [SerializeField] internal TMP_Text wallet, income;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal Button[] tabs;
        [SerializeField] internal GameObject[] pages;
        [SerializeField] internal ShopOfferView[] offers;
    }
}
