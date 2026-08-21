using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>현재 사용자 카드와 가상화 목록을 표시하는 전투력 랭킹 팝업.</summary>
    public sealed class PowerRankingPopupView : MonoBehaviour
    {
        [Header("Chrome")]
        [SerializeField] internal Button backdrop;
        [SerializeField] internal Button closeButton;
        [SerializeField] internal RectTransform panel;

        [Header("Current player")]
        [SerializeField] internal Image avatar;
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal TMP_Text powerLabel;
        [SerializeField] internal TMP_Text rankLabel;

        [Header("Ranking list")]
        [SerializeField] internal VirtualizedRankingList rankingList;

        /// <summary>현재 사용자와 더미 랭킹을 새로 계산해 표시한다.</summary>
        public void Populate(string playerName, long playerPower)
        {
            IReadOnlyList<PowerRankingEntry> entries = DummyPowerRankingProvider.Create(playerName, playerPower);
            PowerRankingEntry currentPlayer = null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsCurrentPlayer) continue;
                currentPlayer = entries[i];
                break;
            }

            if (currentPlayer != null)
            {
                if (nameLabel != null) nameLabel.text = currentPlayer.DisplayName;
                if (powerLabel != null) powerLabel.text = currentPlayer.Power.ToString("N0");
                if (rankLabel != null) rankLabel.text = currentPlayer.Rank.ToString();
            }

            rankingList?.SetEntries(entries);
        }
    }
}
