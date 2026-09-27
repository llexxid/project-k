using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class ShopOfferView : MonoBehaviour
    {
        [SerializeField] internal string offerId;
        [SerializeField] internal TMP_Text title, detail, amount, price;
        [SerializeField] internal Button action;
    }
}
