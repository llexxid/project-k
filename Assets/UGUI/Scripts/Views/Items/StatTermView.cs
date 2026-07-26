using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 상세 스탯 방정식의 탭 가능한 '항(term)' — 숫자 버튼. 탭하면 설명 팝업 표시.
    /// 프리팹: Item_StatTerm.prefab. 컨트롤러가 값/설명/위치를 세팅.
    /// </summary>
    public sealed class StatTermView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal TMP_Text label;

        public RectTransform Rect => (RectTransform)transform;

        public void Set(string text, string explanation, Action<string, RectTransform> onTap)
        {
            if (label != null) label.text = text;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                var r = Rect;
                button.onClick.AddListener(() => onTap?.Invoke(explanation, r));
            }
        }
    }
}
