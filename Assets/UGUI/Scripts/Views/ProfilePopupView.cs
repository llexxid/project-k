using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.Manager;
using System.Collections.Generic;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 실제 저장된 계정 성장과 전투 진행을 표시하는 프로필.
    /// </summary>
    public sealed class ProfilePopupView : MonoBehaviour
    {
        [Header("Chrome")]
        [SerializeField] internal Button backdrop;
        [SerializeField] internal Button closeButton;
        [SerializeField] internal RectTransform panel;

        [Header("Identity")]
        [SerializeField] internal Image avatar;
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal Button editNameButton;
        [SerializeField] internal TMP_Text levelLabel;      // 레벨 배지 숫자
        [SerializeField] internal Image xpFill;             // XP 바 채움
        [SerializeField] internal TMP_Text xpLabel;         // "cur / next"
        [SerializeField] internal TMP_Text idLabel;         // 플레이어 ID

        [Header("Summary pills")]
        [SerializeField] internal Button powerButton;
        [SerializeField] internal TMP_Text powerLabel;      // 전투력(CP)
        [SerializeField] internal TMP_Text trophyLabel;     // 트로피
        [SerializeField] internal TMP_Text guildLabel;      // 길드

        [Header("Season / league card")]
        [SerializeField] internal Image leagueEmblem;
        [SerializeField] internal TMP_Text leagueLabel;
        [SerializeField] internal TMP_Text leagueTrophyLabel;

        [Header("Stats grid (label→value)")]
        [SerializeField] internal TMP_Text[] statValues;    // 스테이지클리어/랭킹/승/퍼펙트/킬/최고리그

        [Header("Game-unique")]
        [SerializeField] internal TMP_Text kingdomLevelLabel;   // 왕국 레벨(게임 고유)
        [SerializeField] internal TMP_Text totalJobsLabel;      // 보유 전직 수(게임 고유)

        internal void Populate(string playerName, long power)
        {
            var state = LocalProgression.State;
            Set(nameLabel, string.IsNullOrWhiteSpace(playerName) ? "모험가" : playerName);
            Set(levelLabel, state.AccountLevel.ToString());
            Set(kingdomLevelLabel, $"Lv. {state.AccountLevel}");
            Set(powerLabel, NumberNotation.Format(power));
            var next = BalanceMath.NextExp(state.AccountLevel);
            Set(xpLabel, next.HasValue ? $"{NumberNotation.Format(state.Experience)} / {NumberNotation.Format(next.Value)}" : "최대 레벨");
            if (xpFill != null) xpFill.fillAmount = next.HasValue ? Mathf.Clamp01((float)((double)state.Experience / next.Value)) : 1;
            var jobs = new HashSet<string>();
            foreach (var unlocked in state.UnlockedJobs.Values)
                foreach (var job in unlocked) if (JobData.IsAvailable(job)) jobs.Add(job);
            Set(totalJobsLabel, $"{jobs.Count}종");
            string stage = state.HighestMainClear > 0
                ? $"{StageParser.GetStageNumber((eStage)state.HighestMainClear)}-{StageParser.GetWaveNumber((eStage)state.HighestMainClear)}" : "미완료";
            string[] values = { stage, NumberNotation.Format(state.ReincarnationLevel), NumberNotation.Format(state.ReincarnationCount),
                $"{state.GoldDungeonClear}단계", NumberNotation.Format(state.Kills), $"{state.RubyDungeonClear}단계" };
            for (int i = 0; i < values.Length && i < statValues.Length; i++) Set(statValues[i], values[i]);
        }

        static void Set(TMP_Text label, string value)
        { if (label != null && label.text != value) label.text = value; }
    }
}
