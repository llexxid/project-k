using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 전직 카드 (왕국군 전직 목록). 프리팹: Item_JobCard.prefab
    /// </summary>
    public sealed class JobCardView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal Image stateFrame;   // 상태 테두리 (현재/보유/정예)
        [SerializeField] internal TMP_Text badge;     // 우상단 배지
        [SerializeField] internal Image image;        // 직업 스프라이트
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal TMP_Text statLabel;
        [SerializeField] internal TMP_Text fragLabel; // 파편 현황 / 무료 재전직
        [SerializeField] internal TMP_Text prereqLabel; // 선행 조건 미충족

        public Button Button => button;
        public void SetComingSoon(JobData spearman, bool elite)
        {
            Set(spearman, UguiTheme.RusticSurfaceDark, null, elite ? "2차" : "1차", UguiTheme.TextSecondary,
                "", "", UguiTheme.TextSecondary, null);
            if (image != null) { image.sprite=spearman?.Portrait; image.color=new Color(.16f,.16f,.18f,1); image.preserveAspect=true; }
            if (nameLabel != null) nameLabel.text="곧 추가 예정";
            if (button != null) { button.onClick.RemoveAllListeners(); button.interactable=false; }
        }

        public void Set(JobData job, Color bgColor, Color? frameColor,
            string badgeText, Color badgeColor,
            string statText, string fragText, Color fragColor,
            string prereqText)
        {
            if (background != null) background.color = bgColor;

            if (stateFrame != null)
            {
                bool hasFrame = frameColor.HasValue;
                stateFrame.gameObject.SetActive(hasFrame);
                if (hasFrame) stateFrame.color = frameColor.Value;
            }

            if (badge != null)
            {
                badge.text = badgeText;
                badge.color = badgeColor;
            }

            if (image != null)
            {
                image.sprite = job != null ? job.Portrait : null;
                image.color = Color.white;
                image.enabled = image.sprite != null;
                image.gameObject.SetActive(image.sprite != null);
            }

            if (nameLabel != null) nameLabel.text = job != null ? job.DisplayName : "";
            if (statLabel != null) statLabel.text = statText;

            if (fragLabel != null)
            {
                fragLabel.text = fragText;
                fragLabel.color = fragColor;
            }

            if (prereqLabel != null)
            {
                bool has = !string.IsNullOrEmpty(prereqText);
                prereqLabel.gameObject.SetActive(has);
                if (has) prereqLabel.text = prereqText;
            }
        }

        public void OnClick(Action handler)
        {
            if (button == null || handler == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => handler());
        }
    }
}
