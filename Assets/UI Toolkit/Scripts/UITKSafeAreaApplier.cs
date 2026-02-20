using UnityEngine;
using UnityEngine.UIElements;

namespace KingdomIdle.UIToolkit
{
    public sealed class UITKSafeAreaApplier : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private string safeAreaElementName = "SafeArea";

        private Rect _lastSafe;
        private int _lastW, _lastH;

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            ApplyIfChanged(force: true);
        }

        private void Update()
        {
            ApplyIfChanged(force: false);
        }

        private void ApplyIfChanged(bool force)
        {
            if (uiDocument == null) return;

            int w = Screen.width;
            int h = Screen.height;
            Rect safe = Screen.safeArea;

            if (!force && w == _lastW && h == _lastH && safe == _lastSafe)
                return;

            _lastW = w;
            _lastH = h;
            _lastSafe = safe;

            var root = uiDocument.rootVisualElement;
            var safeVe = root.Q<VisualElement>(safeAreaElementName);
            if (safeVe == null) return;

            // safeArea는 (0,0)이 좌하단 픽셀 좌표
            float leftPct = safe.xMin / w;
            float rightPct = (w - safe.xMax) / w;
            float bottomPct = safe.yMin / h;
            float topPct = (h - safe.yMax) / h;

            safeVe.style.left = Length.Percent(leftPct * 100f);
            safeVe.style.right = Length.Percent(rightPct * 100f);
            safeVe.style.bottom = Length.Percent(bottomPct * 100f);
            safeVe.style.top = Length.Percent(topPct * 100f);
        }
    }
}
