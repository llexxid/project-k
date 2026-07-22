using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>토스트 셸: 중앙 박스 + 라벨. 입력을 막지 않는다(레이캐스트 비대상).</summary>
    public sealed class ToastView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text label;
    }
}
