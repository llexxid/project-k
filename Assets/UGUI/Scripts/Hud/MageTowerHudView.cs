using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>마법탑 HUD 셸: Auto 버튼 + 스킬 슬롯 N개 + 마탑 버튼 (좌측 세로 열).</summary>
    public sealed class MageTowerHudView : MonoBehaviour
    {
        [Serializable]
        internal sealed class Slot
        {
            public Button button;
            public Image frame;          // 시전 중 하이라이트용 테두리/배경
            public Image icon;
            public TMP_Text label;       // 아이콘 없을 때 스킬명 / 빈 슬롯 "-"
            public Image cooldownMask;   // 아래에서 차오르는 마스크 (Filled Vertical)
            public TMP_Text cooldownText;
        }

        [SerializeField] internal Button autoButton;
        [SerializeField] internal Image autoButtonBg;
        [SerializeField] internal TMP_Text autoButtonLabel;
        [SerializeField] internal Slot[] slots;
        [SerializeField] internal Button towerButton;
    }
}
