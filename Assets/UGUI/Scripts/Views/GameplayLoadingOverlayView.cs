using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>문구 위치를 유지하며 게임 배속과 무관하게 로딩 프레임을 재생한다.</summary>
    public sealed class GameplayLoadingOverlayView : MonoBehaviour
    {
        [SerializeField] internal UnityEngine.UI.Image indicator;
        [SerializeField] internal Sprite[] frames;
        private float _elapsed;
        private int _frame = -1;
        private UnityEngine.UI.Image _dim;
        public bool IsShowingIndicator => gameObject.activeInHierarchy && indicator != null && indicator.gameObject.activeSelf;

        internal void SetPresentation(bool visible)
        {
            if (_dim == null) _dim = transform.Find("Dim")?.GetComponent<UnityEngine.UI.Image>();
            if (_dim != null) _dim.color = new Color(0, 0, 0, visible ? .3f : 0f);
            if (indicator != null && indicator.gameObject.activeSelf != visible) indicator.gameObject.SetActive(visible);
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            _frame = -1;
            SetFrame(0);
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;
            _elapsed = (_elapsed + Time.unscaledDeltaTime) % (frames.Length * .1f);
            SetFrame((int)(_elapsed / .1f));
        }

        private void SetFrame(int frame)
        {
            if (indicator == null || frames == null || frames.Length == 0 || _frame == frame) return;
            _frame = frame;
            indicator.sprite = frames[frame];
        }
    }
}
