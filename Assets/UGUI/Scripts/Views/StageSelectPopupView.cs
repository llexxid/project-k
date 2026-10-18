using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class StageSelectPopupView : MonoBehaviour
    {
        [Serializable] public struct Cell
        {
            public Button button;
            public TMP_Text label, status;
        }
        [SerializeField] internal RectTransform panel;
        [SerializeField] internal Button backdrop, close, previous, next, frontier, travel;
        [SerializeField] internal TMP_Text chapter, current, details, frontierLabel, travelLabel;
        [SerializeField] internal Cell[] cells;
    }
}
