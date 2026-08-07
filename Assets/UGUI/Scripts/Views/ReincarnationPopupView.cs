using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>환생 확인 팝업 셸. 외형은 Popup_Reincarnation 프리팹에서 편집한다.</summary>
    public sealed class ReincarnationPopupView : MonoBehaviour
    {
        [SerializeField] internal RectTransform panel;
        [SerializeField] internal TMP_Text statusLabel;
        [SerializeField] internal TMP_Text infoLabel;
        [SerializeField] internal Button backdropButton;
        [SerializeField] internal Button cancelButton;
        [SerializeField] internal Button confirmButton;
    }
}
