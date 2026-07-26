using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 타이틀/엠블럼용 은은한 루프 애니메이션 — 부드러운 상하 부유 + 미세 스케일 호흡.
    /// unscaledDeltaTime 사용(타임스케일 무관), 프레임당 할당 없음 → 모바일에서 가볍다.
    /// 인스펙터에서 진폭/속도 조절 가능.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UITitleFloat : MonoBehaviour
    {
        [SerializeField] internal float floatAmplitude = 8f;    // 상하 픽셀 진폭
        [SerializeField] internal float floatPeriod = 2.6f;     // 상하 주기(초)
        [SerializeField] internal float breatheAmplitude = 0.02f; // 스케일 호흡 진폭(±비율)
        [SerializeField] internal float breathePeriod = 3.4f;   // 호흡 주기(초)

        private RectTransform _rt;
        private Vector2 _basePos;
        private Vector3 _baseScale;
        private float _t;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _basePos = _rt.anchoredPosition;
            _baseScale = _rt.localScale;
        }

        private void OnEnable()
        {
            // 다시 켜질 때 기준값 재캡처(레이아웃 이후 위치 반영)
            if (_rt == null) _rt = (RectTransform)transform;
            _basePos = _rt.anchoredPosition;
            _baseScale = _rt.localScale;
            _t = 0f;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (floatAmplitude != 0f && floatPeriod > 0.01f)
            {
                float y = Mathf.Sin(_t * (2f * Mathf.PI / floatPeriod)) * floatAmplitude;
                _rt.anchoredPosition = new Vector2(_basePos.x, _basePos.y + y);
            }
            if (breatheAmplitude != 0f && breathePeriod > 0.01f)
            {
                float s = 1f + Mathf.Sin(_t * (2f * Mathf.PI / breathePeriod)) * breatheAmplitude;
                _rt.localScale = _baseScale * s;
            }
        }

        private void OnDisable()
        {
            if (_rt != null)
            {
                _rt.anchoredPosition = _basePos;
                _rt.localScale = _baseScale;
            }
        }
    }
}
