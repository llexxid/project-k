using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>뽑기 결과 팝업 셸. GachaResultPopupController가 카드 리스트를 채운다.</summary>
    public sealed class GachaResultPopupView : MonoBehaviour
    {
        [SerializeField] internal RectTransform box;   // 팝업 박스 (PopIn 애니메이션 대상)
        [SerializeField] internal TMP_Text title;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform grid;
        [SerializeField] internal RectTransform buttonRow;
        [SerializeField] internal Button btnDone;
        [SerializeField] internal Button btnRePull1;
        [SerializeField] internal Button btnRePullN;
        [SerializeField] internal TMP_Text btnRePull1Label;
        [SerializeField] internal TMP_Text btnRePullNLabel;
    }
}
