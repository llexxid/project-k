using Direction;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>투명 입력 막에서 실제 최상단 UI를 검사한다. 강조 구멍의 좌표만으로 무관한 버튼을 통과시키지 않는다.</summary>
    public sealed class FeatureGuideInputGate : MonoBehaviour, ICanvasRaycastFilter
    {
        public FeatureGuideView Owner { get; set; }
        private readonly List<Canvas> _canvases = new();

        /// <summary>안내가 숨겨질 때 임시 탐색 참조를 비운다. 다음 표시의 입력 검사에서 현재 Canvas 목록을 다시 수집한다.</summary>
        private void OnDisable() => _canvases.Clear();

        /// <summary>포인터 위치의 원본 Graphic을 직접 검사한다. EventSystem.RaycastAll 재진입은 UGUI의 공유 정렬 버퍼를 깨므로 사용하지 않는다.</summary>
        public bool IsRaycastLocationValid(Vector2 point, Camera eventCamera)
        {
            var step = GameDirectInteraction.Step;
            var ui = UIManager.Instance;
            if (Owner == null || step == null || !GameDirectInteraction.Armed || EventSystem.current == null || ui == null) return true;
            var canvas = Owner.GetComponentInParent<Canvas>().rootCanvas;
            Graphic first = null;
            // 마탑 HUD는 별도 하위 Canvas에 등록된다. 루트 Canvas 목록만 읽으면 실제 마탑을 찾지 못해 입력이 막힌다.
            // 재사용 List로 현재 살아 있는 하위 Canvas도 검사한다. RaycastAll 재진입이나 안내 오브젝트 비활성화는 하지 않는다.
            canvas.GetComponentsInChildren(false, _canvases);
            foreach (var childCanvas in _canvases)
            {
                var raycaster = childCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null || !raycaster.isActiveAndEnabled) continue;
                var graphics = GraphicRegistry.GetGraphicsForCanvas(childCanvas);
                // UGUI의 IndexedSet은 foreach 열거자를 제공하지 않으므로 인덱스로 읽는다.
                for (int i = 0; i < graphics.Count; i++)
                {
                    var graphic = graphics[i];
                    if (graphic == null || !graphic.raycastTarget || graphic.depth < 0 || graphic.canvasRenderer.cull ||
                        !graphic.gameObject.activeInHierarchy || graphic.transform.IsChildOf(Owner.transform)) continue;
                    if (first != null && !IsInFront(graphic, first)) continue;
                    if (RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, point, eventCamera) && graphic.Raycast(point, eventCamera)) first = graphic;
                }
            }
            if (first != null)
            {
                    var target = first.transform;
                    // 경제 성공 후에는 결과 확인만 허용한다. 저장 응답과 다음 프레임 사이 연타도 막는다.
                    if (GameDirectInteraction.Succeeded)
                        return !Matches(ui, GameDirectTarget.GachaResultClose, target);
                    foreach (var id in step.AllowedTargets)
                        if (Matches(ui, id, target)) return false;
                    if (step.AllowScroll && ui.GuideTargets.TryGet(step.Target, out var anchor))
                    {
                        var scroll = anchor.GetComponentInParent<ScrollRect>();
                        var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(first.gameObject);
                        if (scroll != null && target.IsChildOf(scroll.transform) && click == null && target.GetComponentInParent<Selectable>() == null) return false;
                    }
            }
            return true;
        }

        /// <summary>같은 UI 루트의 후보를 Canvas 정렬과 절대 렌더 깊이로 비교한다. 별도 Canvas의 마탑 위를 덮는 HUD도 우선한다.</summary>
        private static bool IsInFront(Graphic candidate, Graphic current)
        {
            int layer = SortingLayer.GetLayerValueFromID(candidate.canvas.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(current.canvas.sortingLayerID));
            if (layer != 0) return layer > 0;
            int order = candidate.canvas.sortingOrder.CompareTo(current.canvas.sortingOrder);
            return order != 0 ? order > 0 : candidate.depth > current.depth;
        }

        /// <summary>현재 살아 있는 등록 대상과 실제 히트의 소유 관계를 비교한다. 화면 재생성 때 별도 캐시 갱신이 필요 없다.</summary>
        private static bool Matches(UIManager ui, GameDirectTarget id, Transform hit) =>
            ui.GuideTargets.TryGet(id, out var rect) && (hit == rect || hit.IsChildOf(rect));
    }
}
