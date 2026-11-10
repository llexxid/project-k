using TMPro;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class NoticePopupView : MonoBehaviour
    {
        [SerializeField] internal RectTransform panel;
        [SerializeField] internal UnityEngine.UI.Button closeButton, backdropButton;
        [SerializeField] internal UnityEngine.UI.ScrollRect scroll;

        [SerializeField] internal UnityEngine.UI.Button patchToggle;
        [SerializeField] internal TMP_Text patchTitle, patchCaret, patchBody;

        [SerializeField] internal TMP_Text status;
        [SerializeField] internal UnityEngine.UI.Button retryButton;
        [SerializeField] internal NoticeRowView rowTemplate;
    }
}
