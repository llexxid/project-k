using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// UGUI 피격 데미지 텍스트 매니저 (UITKDamageTextManager 이식).
    /// 월드 좌표 → 캔버스 로컬 좌표로 투영, TMP 라벨 풀링, 상승+페이드 애니메이션.
    /// </summary>
    [DefaultExecutionOrder(-950)]
    public sealed class DamageTextManager : MonoBehaviour
    {
        private const string PrefKeyDamageText = "settings_damageText";

        [Header("Auto Find (optional)")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] internal RectTransform layer;   // DamageTextLayer (LayerPopups 하위)

        [Header("Animation")]
        [SerializeField] private float duration = 0.8f;
        [SerializeField] private float risePixels = 80f;
        [SerializeField] private Vector2 screenOffsetPx = new Vector2(0f, 0f);

        [Header("Pooling")]
        [SerializeField] private int warmPool = 24;

        private readonly Stack<TMP_Text> _pool = new();
        private readonly List<Entry> _active = new();

        private struct Entry
        {
            public TMP_Text Label;
            public Vector2 Start;
            public float StartTime;
        }

        private void Awake()
        {
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

                // UGUI는 y+가 위 — 위로 상승
                float y = Mathf.Lerp(e.Start.y, e.Start.y + risePixels, t);
                var rt = e.Label.rectTransform;
                rt.anchoredPosition = new Vector2(e.Start.x, y);
                e.Label.alpha = 1f - t;
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

            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return;

            EnsureLayer();
            if (layer == null) return;

            // 카메라 뒤(화면 밖)면 스킵
            var sp = worldCamera.WorldToScreenPoint(worldPos);
            if (sp.z <= 0f) return;

            // Screen Space Overlay 캔버스 → 카메라 파라미터 null
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, sp, null, out Vector2 local))
                return;

            local += screenOffsetPx;

            var lbl = GetOrCreate();
            lbl.text = amount.ToString("N0");
            lbl.alpha = 1f;
            lbl.color = overrideColor ?? UguiTheme.WarnRed;

            var rt = lbl.rectTransform;
            rt.SetParent(layer, false);
            rt.anchoredPosition = local;
            lbl.gameObject.SetActive(true);

            _active.Add(new Entry
            {
                Label = lbl,
                Start = local,
                StartTime = Time.unscaledTime
            });
        }

        private void EnsureLayer()
        {
            if (layer != null) return;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerPopups == null) return;

            var existing = mgr.LayerPopups.Find("DamageTextLayer") as RectTransform;
            if (existing != null)
            {
                layer = existing;
                return;
            }

            var go = new GameObject("DamageTextLayer", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(mgr.LayerPopups, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
            layer = rt;
        }

        private void WarmupPool()
        {
            if (layer == null) return;
            for (int i = 0; i < warmPool; i++)
            {
                var lbl = CreateLabel();
                if (lbl == null) break;
                lbl.gameObject.SetActive(false);
                _pool.Push(lbl);
            }
        }

        private TMP_Text GetOrCreate()
        {
            while (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            return CreateLabel();
        }

        private TMP_Text CreateLabel()
        {
            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;

            // 카탈로그의 아이템 프리팹(아웃라인 머티리얼 포함) 우선 사용
            if (catalog != null && catalog.itemDamageText != null && layer != null)
            {
                var go = Object.Instantiate(catalog.itemDamageText, layer, false);
                var tmp = go.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
                Object.Destroy(go);
            }

            // 폴백: 코드로 생성
            var fallbackGo = new GameObject("DamageText", typeof(RectTransform));
            if (layer != null) fallbackGo.transform.SetParent(layer, false);
            var text = fallbackGo.AddComponent<TextMeshProUGUI>();
            if (catalog != null && catalog.defaultFont != null) text.font = catalog.defaultFont;
            text.fontSize = UguiTheme.FontDamageText;
            text.fontStyle = FontStyles.Bold;
            text.color = UguiTheme.WarnRed;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var rt = text.rectTransform;
            rt.sizeDelta = new Vector2(240f, 44f);
            return text;
        }

        private void Recycle(TMP_Text lbl)
        {
            if (lbl == null) return;
            lbl.gameObject.SetActive(false);
            lbl.alpha = 1f;
            _pool.Push(lbl);
        }
    }
}
