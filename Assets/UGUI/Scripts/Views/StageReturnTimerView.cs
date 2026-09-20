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
        private void OnEnable() { _shownSeconds = -1; Refresh(); }
        private void Update() => Refresh();
        private void Refresh()
        {
            var stage = StageManager.Instance;
            if (stage == null) return;
            float remaining = Mathf.Max(0, stage.ReturnCountdownRemaining);
            if (fill != null) fill.fillAmount = Mathf.Clamp01(remaining / stage.ReturnCountdownDuration);
            int seconds = Mathf.CeilToInt(remaining);
            if (label != null && seconds != _shownSeconds)
            { label.text = $"{seconds}초 후 전투에 복귀"; _shownSeconds = seconds; }
        }
    }
}
