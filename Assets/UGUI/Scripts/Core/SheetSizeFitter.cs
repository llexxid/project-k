using UnityEngine;
namespace KingdomIdle.UGUI
{
    /// <summary>Reserves the top HUD and bottom navigation on short portrait screens.</summary>
    public sealed class SheetSizeFitter : MonoBehaviour
    {
        public enum HeightMode { Preferred = 0, AvailableSpace = 1, BelowPartyHud = 2 }
        public float preferredHeight=1152;
        public HeightMode heightMode;
        private RectTransform _topBoundary;
        private RectTransform _rect;
        private BottomSheetView _view;
        private bool _applying;
        private void OnEnable(){_rect=(RectTransform)transform;Apply();}
        private void OnRectTransformDimensionsChange(){if(_rect!=null&&isActiveAndEnabled)Apply();}
        private void LateUpdate(){if(heightMode==HeightMode.BelowPartyHud)Apply();}

        /// <summary>팝업을 여는 쪽에서 실제 HUD를 연결한다. 다른 시트는 기본 계산을 그대로 사용한다.</summary>
        public void SetTopBoundary(RectTransform boundary)
        {
            _topBoundary = boundary;
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Apply()
        {
            if (_applying) return;
            var parent=_rect.parent as RectTransform;if(parent==null)return;
            if (heightMode == HeightMode.BelowPartyHud)
            {
                if (_topBoundary == null) return;
                if (_view == null) _view = GetComponentInParent<BottomSheetView>(true);
                if (_view == null || parent.parent != _view.transform) return;
                var root = (RectTransform)_view.transform;
                var party = PartyHudController.Instance != null ? PartyHudController.Instance.HudRect : null;
                float partyHeight = party != null ? party.rect.height : UguiTheme.PartyHudHeight;
                float top = root.InverseTransformPoint(_topBoundary.TransformPoint(
                    new Vector3(_topBoundary.rect.center.x, _topBoundary.rect.yMin, 0))).y - partyHeight;
                _applying = true;
                try
                {
                    // 마스크와 입력 영역도 본문에 한정해 상단 정보·왕국군·하단바를 가리지 않는다.
                    var offset = new Vector2(parent.offsetMax.x, top - root.rect.yMax);
                    if (parent.offsetMax != offset) parent.offsetMax = offset;
                    float available = Mathf.Max(0, parent.rect.height);
                    if (Mathf.Abs(_rect.rect.height - available) > .5f)
                        _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, available);
                    if (_view.backdrop != null)
                    {
                        var backdrop = (RectTransform)_view.backdrop.transform;
                        if (backdrop.offsetMin != parent.offsetMin) backdrop.offsetMin = parent.offsetMin;
                        if (backdrop.offsetMax != parent.offsetMax) backdrop.offsetMax = parent.offsetMax;
                    }
                }
                finally { _applying = false; }
                return;
            }
            // 기존 팝업의 높이 계산은 그대로 보존한다. 부모의 하단 제외 영역을 다시 차감하지 않는다.
            float height=Mathf.Clamp(parent.rect.height-UguiTheme.StageControlsBottom-24,380,preferredHeight);
            if (heightMode == HeightMode.AvailableSpace)
            {
                // HUD가 준비되기 전에는 프리팹의 기존 크기를 유지한다.
                if (_topBoundary == null) return;
                float top = parent.InverseTransformPoint(_topBoundary.TransformPoint(
                    new Vector3(_topBoundary.rect.center.x, _topBoundary.rect.yMin, 0))).y;
                // 하단 앵커가 열린 위치다. 슬라이드 중인 anchoredPosition은 높이에 포함하지 않는다.
                float bottom = parent.rect.yMin + parent.rect.height * _rect.anchorMin.y;
                height = Mathf.Max(0, top - bottom - 24);
            }
            if(Mathf.Abs(_rect.sizeDelta.y-height)>.5f)_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
        }
    }
}
