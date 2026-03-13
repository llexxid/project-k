using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// UI Toolkit 기반 피격 데미지 텍스트 매니저.
    /// - 월드 좌표 -> 패널 좌표 변환
    /// </summary>
    [DefaultExecutionOrder(-950)]
    public sealed class UITKDamageTextManager : MonoBehaviour
    {
        private const string PrefKeyDamageText = "settings_damageText";

        [Header("Auto Find (optional)")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Camera worldCamera;

        [Header("Animation")]
        [SerializeField] private float duration = 0.8f;
        [SerializeField] private float risePixels = 80f;
        [SerializeField] private Vector2 screenOffsetPx = new Vector2(0f, 0f);

        [Header("Pooling")]
        [SerializeField] private int warmPool = 24;

        private VisualElement _layer;
        private readonly Stack<Label> _pool = new();
        private readonly List<Entry> _active = new();

        private struct Entry
        {
            public Label Label;
            public Vector2 Start;
            public float StartTime;
        }

        private void Awake()
        {
            if (uiDocument == null)
            {
                // UITKUIManager가 있으면 거기서 UIDocument를 우선 사용
                var uiMgr = UITKUIManager.Instance;
                if (uiMgr != null)
                    uiDocument = uiMgr.GetComponent<UIDocument>();

                if (uiDocument == null)
                    uiDocument = FindFirstObjectByType<UIDocument>();
            }

            if (worldCamera == null)
                worldCamera = Camera.main;

            EnsureLayer();
            WarmupPool();
        }

        private void Update()
        {
            if (_active.Count == 0) return;

            float now = Time.unscaledTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                float t = (now - e.StartTime) / Mathf.Max(0.001f, duration);
                if (t >= 1f)
                {
                    Recycle(e.Label);
                    _active.RemoveAt(i);
                    continue;
                }

                float y = Mathf.Lerp(e.Start.y, e.Start.y - risePixels, t);
                e.Label.style.top = y;
                e.Label.style.opacity = 1f - t;
            }
        }

        public void ShowWorldDamage(Vector3 worldPos, ulong amount)
        {
            ShowWorldDamageInternal(worldPos, amount, null);
        }

        public void ShowWorldDamage(Vector3 worldPos, ulong amount, Color color)
        {
            ShowWorldDamageInternal(worldPos, amount, color);
        }

        private void ShowWorldDamageInternal(Vector3 worldPos, ulong amount, Color? overrideColor)
        {
            // 설정(데미지 문구 출력) OFF면 표시하지 않음
            if (PlayerPrefs.GetInt(PrefKeyDamageText, 1) == 0)
                return;

            if (uiDocument == null) return;
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return;

            EnsureLayer();
            if (_layer == null) return;

            var panel = uiDocument.rootVisualElement.panel;
            if (panel == null) return;

            // 카메라 뒤(화면 밖)면 스킵
            var sp = worldCamera.WorldToScreenPoint(worldPos);
            if (sp.z <= 0f) return;

            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldPos, worldCamera);
            p += screenOffsetPx;

            var lbl = GetOrCreate();
            lbl.text = amount.ToString("N0");
            lbl.style.opacity = 1f;

            // 색상 오버라이드 (지정하지 않으면 CSS 기본 빨간색 사용)
            if (overrideColor.HasValue)
            {
                var c = overrideColor.Value;
                lbl.style.color = new StyleColor(c);
            }
            else
            {
                lbl.style.color = StyleKeyword.Null; // CSS 기본값 사용
            }

            // 간단 중앙 정렬(고정 폭)
            const float w = 120f;
            const float h = 44f;
            lbl.style.width = w;
            lbl.style.height = h;
            lbl.style.left = p.x - w * 0.5f;
            lbl.style.top = p.y - h * 0.5f;

            _layer.Add(lbl);
            _active.Add(new Entry
            {
                Label = lbl,
                Start = new Vector2(lbl.style.left.value.value, lbl.style.top.value.value),
                StartTime = Time.unscaledTime
            });
        }

        private void EnsureLayer()
        {
            if (uiDocument == null) return;
            if (_layer != null && _layer.panel != null) return;

            var root = uiDocument.rootVisualElement;
            if (root == null) return;

            // Panel 위에 보이되, Settings/Loading 같은 오버레이 아래에 있어야 하므로 Layer_Popups 사용
            var popups = root.Q<VisualElement>("Layer_Popups");
            if (popups == null) return;

            _layer = popups.Q<VisualElement>("DamageTextLayer");
            if (_layer != null) return;

            _layer = new VisualElement { name = "DamageTextLayer" };
            _layer.AddToClassList("damage-text-layer");
            _layer.pickingMode = PickingMode.Ignore;
            popups.Add(_layer);
            _layer.BringToFront();
        }

        private void WarmupPool()
        {
            for (int i = 0; i < warmPool; i++)
                _pool.Push(CreateLabel());
        }

        private Label GetOrCreate()
        {
            return _pool.Count > 0 ? _pool.Pop() : CreateLabel();
        }

        private static Label CreateLabel()
        {
            var lbl = new Label("0");
            lbl.AddToClassList("damage-text");
            lbl.pickingMode = PickingMode.Ignore;
            return lbl;
        }

        private void Recycle(Label lbl)
        {
            if (lbl == null) return;
            lbl.RemoveFromHierarchy();
            lbl.style.opacity = 1f;
            _pool.Push(lbl);
        }
    }
}