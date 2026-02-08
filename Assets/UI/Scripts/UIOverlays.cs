using System.Collections;
using UnityEngine;

namespace KingdomIdle.UI
{
    public class UILoadingOverlay : UIOverlay, ILoadingOverlay
    {
        [SerializeField] private Component messageText; // UnityEngine.UI.Text 또는 TMP_Text 등 (text 프로퍼티만 있으면 OK)

        public void SetMessage(string message)
        {
            UIReflectionTextUtil.TrySetText(messageText, message ?? string.Empty);
        }
    }

    public class UIToastOverlay : UIOverlay, IToastOverlay
    {
        [SerializeField] private Component messageText; // UnityEngine.UI.Text 또는 TMP_Text 등
        private Coroutine _routine;

        public void ShowToast(string message, float durationSeconds)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CoToast(message, durationSeconds));
        }

        private IEnumerator CoToast(string message, float seconds)
        {
            UIReflectionTextUtil.TrySetText(messageText, message ?? string.Empty);
            gameObject.SetActive(true);
            SetVisible(true, instant: true);

            yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));

            SetVisible(false, instant: true);
            _routine = null;
        }
    }
}
