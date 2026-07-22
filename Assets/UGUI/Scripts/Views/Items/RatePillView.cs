using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 확률 요약 알약 (등급별 % 표시). 프리팹: Item_RatePill.prefab
    /// </summary>
    public sealed class RatePillView : MonoBehaviour
    {
        [SerializeField] internal Image background;
        [SerializeField] internal Image frame;
        [SerializeField] internal TMP_Text label;

        public void Set(string text, Color accent)
        {
            if (label != null)
            {
                label.text = text;
                label.color = accent;
            }
            if (frame != null) frame.color = accent;
            if (background != null) background.color = new Color(accent.r, accent.g, accent.b, 0.12f);
        }
    }
}
