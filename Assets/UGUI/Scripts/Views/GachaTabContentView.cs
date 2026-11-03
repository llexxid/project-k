using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>기존 뽑기 본문과 별도 획득 목록 팝업이 사용하는 표시 참조.</summary>
    public sealed class GachaTabContentView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text messageLabel;
        [SerializeField] internal TMP_Text costLabel;
        [SerializeField] internal RectTransform pullRow;
        [SerializeField] internal RectTransform rewardGrid;
        [SerializeField] internal UnityEngine.UI.Button ratesButton;
        [SerializeField] internal UnityEngine.UI.Button rewardsButton;
        [SerializeField] internal RectTransform pityRoot;
        [SerializeField] internal TMP_Text pityLabel;
        [SerializeField] internal TMP_Text pityRemaining;
        [SerializeField] internal UnityEngine.UI.Image pityFill;
        [SerializeField] internal TMP_Text heroTitle;
        [SerializeField] internal UnityEngine.UI.Image heroIcon;
        [SerializeField] internal Sprite equipmentArt;
        [SerializeField] internal Sprite mageArt;
    }
}
