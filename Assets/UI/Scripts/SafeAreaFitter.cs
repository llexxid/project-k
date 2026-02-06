using UnityEngine;

namespace KingdomIdle.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Header("Apply Axis")]
        [SerializeField] private bool applyX = true;
        [SerializeField] private bool applyY = true;

        private RectTransform _rt;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;
        private ScreenOrientation _lastOrientation;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            // 해상도/방향/세이프에어리어가 바뀌면 다시 적용
            if (Screen.safeArea != _lastSafeArea ||
                _lastResolution.x != Screen.width ||
                _lastResolution.y != Screen.height ||
                _lastOrientation != Screen.orientation)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;

            if (_rt == null) _rt = GetComponent<RectTransform>();

            // Screen 좌표 → Anchor(0~1) 좌표로 변환
            Vector2 anchorMin = safe.position;
            Vector2 anchorMax = safe.position + safe.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // X/Y 선택 적용(원하면 한 축만 세이프 적용 가능)
            Vector2 finalMin = _rt.anchorMin;
            Vector2 finalMax = _rt.anchorMax;

            if (applyX)
            {
                finalMin.x = anchorMin.x;
                finalMax.x = anchorMax.x;
            }

            if (applyY)
            {
                finalMin.y = anchorMin.y;
                finalMax.y = anchorMax.y;
            }

            _rt.anchorMin = finalMin;
            _rt.anchorMax = finalMax;

            // Stretch 상태에서 위치 오프셋은 0으로 고정
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
