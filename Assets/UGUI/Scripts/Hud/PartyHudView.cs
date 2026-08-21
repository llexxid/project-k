using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>파티 HUD 셸: 멤버 3인 (초상화 + HP바 + 스킬 슬롯 3개).</summary>
    public sealed class PartyHudView : MonoBehaviour
    {
        [Serializable]
        internal sealed class SkillSlot
        {
            public GameObject root;
            public Image icon;              // 미니멀 픽셀 아이콘 (텍스트 라벨 대체)
            public Image cooldownMask;
            public TMP_Text cooldownLabel;
            public TMP_Text nameLabel;      // 아이콘이 없을 때만 표시하는 폴백
        }

        [Serializable]
        internal sealed class Member
        {
            public Button portrait;
            public Image portraitImage;
            public Image hpFill;
            public SkillSlot[] skills = new SkillSlot[3];
        }

        [SerializeField] internal RectTransform rect;
        [SerializeField] internal Member[] members = new Member[3];
    }
}
