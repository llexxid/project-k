using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>던전 클리어 결과 팝업 셸. 외형은 Popup_DungeonClear 프리팹에서 편집한다.</summary>
    public sealed class DungeonClearPopupView : MonoBehaviour
    {
        [SerializeField] internal RectTransform panel;
        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal Button exitButton;
        [SerializeField] internal Button nextButton;
        [SerializeField] internal Button retryButton;
    }
}
