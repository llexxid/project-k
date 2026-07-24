using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 버튼 "눌림" 촉감 피드백 — 누르면 살짝 작아지고 떼면 복귀(캐주얼 모바일 UI 표준 연출).
    /// 대상 Selectable이 비활성(interactable=false)이면 반응하지 않는다.
    /// Button의 색/스프라이트 전이와 독립적으로 Transform 스케일만 다루므로 함께 사용 가능.
    /// 팩토리(F.ButtonOn)가 모든 버튼에 자동 부착한다. 인스펙터에서 pressedScale 조정 가능.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("눌렀을 때 스케일 배율")]
        [SerializeField] private float pressedScale = 0.94f;
        [Tooltip("스케일이 적용될 대상(비우면 자기 자신)")]
        [SerializeField] private RectTransform target;
        [SerializeField] private Selectable selectable;

        private void Reset()
        {
            target = transform as RectTransform;
            selectable = GetComponent<Selectable>();
        }

        private void Awake()
        {
            if (target == null) target = transform as RectTransform;
            if (selectable == null) selectable = GetComponent<Selectable>();
        }

        private bool Interactable => selectable == null || selectable.IsInteractable();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable || target == null) return;
            UITween.ScaleTo(target, pressedScale, 0.06f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (target == null) return;
            UITween.ScaleTo(target, 1f, 0.10f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (target == null) return;
            UITween.ScaleTo(target, 1f, 0.10f);
        }
    }
}
