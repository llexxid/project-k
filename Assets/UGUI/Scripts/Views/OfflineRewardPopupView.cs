using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>오프라인 사냥 정산 결과 팝업 셸. 외형은 Popup_OfflineReward 프리팹에서 편집한다.</summary>
    public sealed class OfflineRewardPopupView : MonoBehaviour
    {
        [SerializeField] internal RectTransform panel;
        [SerializeField] internal Button backdropButton;
        [SerializeField] internal TMP_Text durationLabel;
        [SerializeField] internal TMP_Text killCountLabel;
        [SerializeField] internal RectTransform goldRow;
        [SerializeField] internal TMP_Text goldValueLabel;
        [SerializeField] internal RectTransform ancientCoinRow;
        [SerializeField] internal TMP_Text ancientCoinValueLabel;
        [SerializeField] internal TMP_Text progressLabel;
        [SerializeField] internal Button confirmButton;
    }
}
