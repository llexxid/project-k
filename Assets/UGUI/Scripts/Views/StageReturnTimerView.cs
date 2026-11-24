using Scripts.Core.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class StageReturnTimerView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text label;
        [SerializeField] internal Image fill;
        private int _shownSeconds = -1;
        private string _shownAction;
        private void OnEnable() { _shownSeconds = -1; Refresh(); }
        private void Update() => Refresh();
        private void Refresh()
        {
            var stage = StageManager.Instance;
            if (stage == null) return;
            float remaining = Mathf.Max(0, stage.ReturnCountdownRemaining);
            if (fill != null) fill.fillAmount = Mathf.Clamp01(remaining / stage.ReturnCountdownDuration);
            int seconds = Mathf.CeilToInt(remaining);
            string action = stage.CurrentRunState == eStageRunState.ResultPending ? "메인 스테이지 복귀" : "전투에 복귀";
            bool waiting = stage.CurrentRunState == eStageRunState.ResultPending && !stage.DungeonResultCountdownActive;
            var destination = stage.CurrentRunState == eStageRunState.ResultPending ? stage.AutoDungeonDestination : null;
            if (destination.HasValue) action = destination.Value == stage.CurrentStage ? "같은 단계 재도전" : "다음 던전 도전";
            if (waiting) action = "다음 행동을 선택하세요.";
            if (label == null || seconds == _shownSeconds && action == _shownAction) return;
            label.text = waiting ? action : $"{seconds}초 후 {action}";
            _shownSeconds = seconds;
            _shownAction = action;
        }
    }
}
