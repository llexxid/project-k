using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Direction;
using KingdomIdle.Balance;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>로컬 저장 기반 실습 검증은 거절하고 계정 상태를 바꾸지 않는 표시 검증을 보존한다.</summary>
    public static class InteractiveGuideAcceptance
    {
        private static string Output => Environment.GetEnvironmentVariable("TUTORIAL_PREVIEW_QA_OUTPUT") ?? "C:/GitHub/QA/TutorialPreview-20261104";

        public static UniTask RunAsync(GameObject root, UIManager ui, GameDirectManager manager, Action<bool, string> check)
            => throw new InvalidOperationException("로컬 진행·실습 성공·보상 초기화 기반 검증은 중단되었습니다. KingdomIdle.Balance.ServerTutorialAcceptance.Run()으로 서버 계약을 검증하세요.");

        public static UniTask RunPreviewAsync(UIManager ui, Action<bool, string> check)
        {
            if (!Application.isPlaying || !(LocalProgression.AccountKey?.StartsWith("balance-qa-") ?? false) || Scripts.Core.Manager.NetworkManager.Instance != null)
                throw new InvalidOperationException("네트워크 관리자가 없는 격리 PlayMode 계정이 필요합니다.");
            return ExerciseHiddenGeometry(ui, check);
        }

        /// <summary>실제 안내 프리팹으로 화면 밖·마스크 밖 대상의 최초 표시와 복귀를 검사한다. 임시 대상과 View는 성공·실패 모두 정리한다.</summary>
        private static async UniTask ExerciseHiddenGeometry(UIManager ui, Action<bool, string> check)
        {
            var overlay = Object.Instantiate(ui.Catalog.overlayFeatureGuide, ui.LayerOverlays, false);
            var view = overlay.GetComponent<FeatureGuideView>();
            var viewport = new GameObject("GuideGeometryViewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
            var target = new GameObject("GuideGeometryTarget", typeof(RectTransform));
            try
            {
                var clip = (RectTransform)viewport.transform; clip.SetParent(ui.LayerScreens, false); clip.sizeDelta = new Vector2(200, 200);
                var rect = (RectTransform)target.transform; rect.SetParent(clip, false); rect.sizeDelta = new Vector2(100, 100);
                var step = new GameDirectStep(new GameDirectStepData { id = "geometry", target = GameDirectTarget.Development, title = "표시 복구 검사", description = "대상이 보일 때 다시 표시합니다." }, 1, 1);
                await Settle();
                foreach (var offset in new[] { new Vector2(100000, 0), new Vector2(300, 0) })
                {
                    rect.anchoredPosition = offset;
                    view.Show(step, rect, () => true);
                    check(!view.IsVisible && !view.inputGroup.blocksRaycasts, "최초 표시에서 화면·마스크 밖 대상은 예외 없이 입력 해제: " + offset.x);
                    rect.anchoredPosition = Vector2.zero;
                    view.Show(step, rect, () => true); await Settle();
                    check(view.IsVisible && view.HighlightRect.width > 0, "대상 복귀 후 같은 View·단계 재표시: " + offset.x);
                    rect.anchoredPosition = offset; await Settle();
                    check(!view.IsVisible && !view.inputGroup.blocksRaycasts, "표시 중 대상 이탈도 입력 해제: " + offset.x);
                }
            }
            finally { view.Hide(); Object.Destroy(overlay); Object.Destroy(viewport); await Settle(); }
        }

        /// <summary>긴 장비 목록의 상단·중간·하단에서 강조 범위와 스크롤을 검사한다. 시험용 위치만 바꾸며 계정에는 쓰지 않는다.</summary>
        private static async UniTask CheckEquipmentViewport(GameObject root, UIManager ui, Action<bool, string> check)
        {
            ui.GuideTargets.TryGet(GameDirectTarget.ArmyEquipment, out var target);
            var scroll = target.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            check(target == scroll.viewport && scroll.content.rect.height > scroll.viewport.rect.height * 2, "긴 장비 목록 대신 실제 뷰포트 등록");
            foreach (float position in new[] { 1f, .5f, 0f })
            {
                scroll.StopMovement(); scroll.verticalNormalizedPosition = position; await Settle();
                AssertViewport(ui, check);
                await Capture(root, ui, "equipment-scroll-" + position.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), check);
            }
            CheckScroll(ui, GameDirectTarget.ArmyEquipment, check);
        }

        /// <summary>현재 화면 비율에서 강조 테두리가 뷰포트를 벗어나지 않는지 확인한다. 계층이나 레이아웃은 수정하지 않는다.</summary>
        private static void AssertViewport(UIManager ui, Action<bool, string> check)
        {
            ui.GuideTargets.TryGet(GameDirectTarget.ArmyEquipment, out var target);
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var view = ui.ActiveFeatureGuide;
            var low = view.transform.InverseTransformPoint(corners[0]); var high = view.transform.InverseTransformPoint(corners[2]);
            var hole = view.HighlightRect;
            check(hole.width > 0 && hole.height > 0 && hole.xMin >= low.x - 1 && hole.yMin >= low.y - 1 && hole.xMax <= high.x + 1 && hole.yMax <= high.y + 1,
                "장비 강조는 현재 뷰포트 안에만 표시");
            check(view.highlight.localScale == Vector3.one, "뷰포트 테두리 점멸 확대 없음");
        }

        /// <summary>실제 안내·패널을 세 비율로 렌더한다. 기기 측정과 구별하고 카드·문자 잘림 및 UI 메모리를 기록한다.</summary>
        private static async UniTask Capture(GameObject root, UIManager ui, string name, Action<bool, string> check)
        {
            var canvas = root.GetComponent<Canvas>(); var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            var cameraGo = new GameObject("GuideRatioCamera", typeof(Camera)); var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true; cameraGo.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .12f, .08f);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                foreach (var size in new[] { new Vector2Int(360, 800), new Vector2Int(540, 960), new Vector2Int(900, 1200) })
                {
                    var rt = new RenderTexture(size.x, size.y, 24); var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    var previous = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = rt; await Settle(); camera.Render(); RenderTexture.active = rt;
                        if (GameDirectInteraction.Step?.Target == GameDirectTarget.ArmyEquipment) AssertViewport(ui, check);
                        image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                        Directory.CreateDirectory(Output); File.WriteAllBytes($"{Output}/{name}-{size.x}x{size.y}.png", image.EncodeToPNG());
                        check(!ui.ActiveFeatureGuide.descriptionLabel.isTextOverflowing, "실습 본문 잘림 없음: " + name + size);
                    }
                    finally { RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(image); }
                }
                File.AppendAllText(Output + "/interactive-render.txt", $"{name}: batches={UnityStats.batches}, drawCalls={UnityStats.drawCalls}, allocated={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()} (Editor simulated ratios)\n");
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = oldCamera; Object.Destroy(cameraGo); await Settle(); }
        }

        /// <summary>한 프레임 이상의 레이아웃·지연 파괴를 기다린다. 게임 시간 배율을 변경하지 않는다.</summary>
        private static async UniTask Settle() { await UniTask.NextFrame(); await UniTask.NextFrame(); Canvas.ForceUpdateCanvases(); }
        /// <summary>실제 게임 루프에서 조건을 기다리며 무한 대기 대신 현재 단계와 함께 오류를 기록한다.</summary>
        private static async UniTask Until(Func<bool> predicate, string label)
        { float end = Time.realtimeSinceStartup + 12; while (!predicate()) { if (Time.realtimeSinceStartup > end) throw new TimeoutException(label + " current=" + GameDirectInteraction.Step?.Id); await UniTask.NextFrame(); } }
        /// <summary>EventSystem 최상단 히트에 실제 클릭을 전달한다. 원본 버튼 이벤트를 직접 호출해서 입력 차단 검사를 우회하지 않는다.</summary>
        private static string Click(RectTransform target)
        {
            if (target == null) throw new InvalidOperationException("클릭 대상 없음");
            var canvas = target.GetComponentInParent<Canvas>();
            var point = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, target.TransformPoint(target.rect.center));
            if (GameDirectInteraction.Step?.Target == GameDirectTarget.MageTower)
            {
                var region = FeatureGuideAnchor.FocusRegion(target, GameDirectTarget.MageTower);
                point = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null,
                    target.TransformPoint(target.rect.min + Vector2.Scale(target.rect.size, region.center)));
            }
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            if (hits.Count == 0) throw new InvalidOperationException("레이캐스트 없음: " + target.name);
            var hit = hits[0].gameObject;
            // 실제 마탑은 짧은 누름/뗌을 UILongPressButton으로 받는다. 같은 포인터 경로로 열기 동작을 검사한다.
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
            if (hit.name == "InputBlocker" && GameDirectInteraction.Step?.Target == GameDirectTarget.MageTower)
                throw new InvalidOperationException("마탑 입력 가림: " + string.Join(",", hits.Select(x => x.gameObject.name)) + " point=" + point + " target=" + target.name);
            return hit.name;
        }

        /// <summary>실제 레이캐스트로 통과하는 읽기 영역에서 드래그한다. 버튼 호출이나 스크롤 위치 직접 대입으로 입력 필터를 우회하지 않는다.</summary>
        private static void CheckScroll(UIManager ui, GameDirectTarget targetId, Action<bool, string> check)
        {
            ui.GuideTargets.TryGet(targetId, out var target);
            var scroll = target.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            check(scroll != null, "전직 정보 읽기용 ScrollRect 연결");
            var camera = scroll.GetComponentInParent<Canvas>().worldCamera;
            var viewport = scroll.viewport;
            var hits = new List<RaycastResult>();
            for (int y = 1; y < 10; y++)
                for (int x = 1; x < 10; x++)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint(new Vector2(
                        Mathf.Lerp(viewport.rect.xMin, viewport.rect.xMax, x / 10f), Mathf.Lerp(viewport.rect.yMin, viewport.rect.yMax, y / 10f))));
                    var data = new PointerEventData(EventSystem.current) { position = point, pressPosition = point, button = PointerEventData.InputButton.Left };
                    hits.Clear(); EventSystem.current.RaycastAll(data, hits);
                    if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject) != scroll.gameObject) continue;
                    float before = scroll.content.anchoredPosition.y;
                    data.pointerPressRaycast = hits[0];
                    ExecuteEvents.Execute(scroll.gameObject, data, ExecuteEvents.initializePotentialDrag);
                    ExecuteEvents.Execute(scroll.gameObject, data, ExecuteEvents.beginDragHandler);
                    data.position += new Vector2(0, scroll.verticalNormalizedPosition > .5f ? 45 : -45);
                    ExecuteEvents.Execute(scroll.gameObject, data, ExecuteEvents.dragHandler);
                    ExecuteEvents.Execute(scroll.gameObject, data, ExecuteEvents.endDragHandler);
                    check(Mathf.Abs(scroll.content.anchoredPosition.y - before) > 1, "안내 중 실제 드래그로 정보 스크롤 가능");
                    return;
                }
            check(false, "입력 필터를 통과하는 읽기 스크롤 영역 없음");
        }
    }
}
