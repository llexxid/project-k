using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.Balance;

namespace KingdomIdle.UGUI
{
    public sealed class ShopOfferView : MonoBehaviour
    {
        [SerializeField] internal string offerId;
        [SerializeField] internal TMP_Text title, detail, amount, price;
        [SerializeField] internal Button action;

        internal static readonly Color PreviewActionColor = new Color(.79f, .73f, .63f, 1);
        private void OnEnable() => ApplyActionAppearance();

        // 표시 역할만 정한다. 실제 광고 지급과 준비 중 입력은 기존 컨트롤러가 처리한다.
        internal void ApplyActionAppearance()
        {
            bool ready = ShopCatalog.Find(offerId)?.Payment == ShopPayment.RewardedAd;
            Color color = ready ? UguiTheme.ActionGold : PreviewActionColor;
            if (action != null && action.targetGraphic != null && action.targetGraphic.color != color)
                action.targetGraphic.color = color;
            if (price != null)
            {
                price.color = UguiTheme.InfoInk;
                price.fontStyle = FontStyles.Bold;
                if (price.font != null) price.fontSharedMaterial = price.font.material;
            }
        }
    }
}
