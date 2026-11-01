using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    public sealed class GachaRewardsPopupView : MonoBehaviour
    {
        [SerializeField] internal RectTransform box;
        [SerializeField] internal TMP_Text title;
        [SerializeField] internal UnityEngine.UI.Button close;
        [SerializeField] internal GachaTabContentView rewards;
    }
}
