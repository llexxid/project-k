using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>가상화 목록에서 재사용되는 랭킹 행.</summary>
    public sealed class RankingRowView : MonoBehaviour
    {
        [SerializeField] internal Image background;
        [SerializeField] internal Image rankBadge;
        [SerializeField] internal TMP_Text rankLabel;
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal TMP_Text powerLabel;
        [SerializeField] internal TMP_Text selfMarker;

        internal RectTransform RectTransform => transform as RectTransform;

        /// <summary>행 객체를 지정된 랭킹 데이터로 갱신한다.</summary>
        public void Bind(PowerRankingEntry entry, int dataIndex)
        {
            if (entry == null) return;

            if (rankLabel != null) rankLabel.text = entry.Rank.ToString();
            if (nameLabel != null) nameLabel.text = entry.DisplayName;
            if (powerLabel != null) powerLabel.text = entry.Power.ToString("N0");
            if (selfMarker != null)
            {
                selfMarker.text = entry.IsCurrentPlayer ? "나" : string.Empty;
                selfMarker.gameObject.SetActive(entry.IsCurrentPlayer);
            }

            if (background != null)
            {
                background.color = entry.IsCurrentPlayer
                    ? new Color(0.30f, 0.23f, 0.10f, 1f)
                    : (dataIndex % 2 == 0
                        ? new Color(0.14f, 0.11f, 0.08f, 1f)
                        : new Color(0.18f, 0.14f, 0.09f, 1f));
            }

            if (rankBadge != null)
            {
                rankBadge.color = entry.Rank switch
                {
                    1 => new Color(0.95f, 0.72f, 0.24f, 1f),
                    2 => new Color(0.72f, 0.76f, 0.80f, 1f),
                    3 => new Color(0.72f, 0.43f, 0.22f, 1f),
                    _ => new Color(0.32f, 0.25f, 0.16f, 1f)
                };
            }
        }
    }
}
