using TMPro;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class NoticeRowView : MonoBehaviour
    {
        [SerializeField] internal UnityEngine.UI.Button toggle;
        [SerializeField] internal TMP_Text title, date, caret, body;
    }
}
