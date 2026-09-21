using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomIdle.UGUI
{
    /// <summary>ScrollRect와 같은 객체에서 드래그 상태만 전달한다. 실제 스크롤은 ScrollRect가 처리한다.</summary>
    public sealed class QuestScrollDragRelay : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public Action<bool> DragChanged;
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) DragChanged?.Invoke(true);
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) DragChanged?.Invoke(false);
        }
        private void OnDisable() { DragChanged?.Invoke(false); }
    }
}
