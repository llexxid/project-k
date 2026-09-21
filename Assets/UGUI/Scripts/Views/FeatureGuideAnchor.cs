using Direction;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>동적으로 재생성되는 실제 UI의 안정적인 안내 ID다. 버튼의 원래 리스너를 대체하지 않는다.</summary>
    public sealed class FeatureGuideAnchor : MonoBehaviour
    {
        private GameDirectTarget _id;
        private bool _bound;
        private UnityEngine.UI.Button _button;
        private FeatureGuideTargetRegistry _registry;
        private Rect _focusRegion = new(0, 0, 1, 1);

        /// <summary>UI 생성 뒤 실제 입력 대상과 클릭 관찰을 연결한다. 선택적 focusRegion은 버튼 안에서 강조할 정규화 영역이며 입력 범위는 바꾸지 않는다.</summary>
        public static void Bind(Component target, GameDirectTarget id, Rect? focusRegion = null)
        {
            if (target == null) return;
            foreach (var existing in target.GetComponents<FeatureGuideAnchor>())
                if (existing._bound && existing._id == id) { existing._focusRegion = focusRegion ?? new Rect(0, 0, 1, 1); existing.OnEnable(); return; }
            var anchor = target.gameObject.AddComponent<FeatureGuideAnchor>();
            anchor._id = id; anchor._bound = true;
            anchor._focusRegion = focusRegion ?? new Rect(0, 0, 1, 1);
            anchor._button = target.GetComponent<UnityEngine.UI.Button>();
            if (anchor._button != null) anchor._button.onClick.AddListener(anchor.Clicked);
            anchor.OnEnable();
        }

        /// <summary>안내 표시 시 같은 ID의 강조 영역을 반환한다. 별도 설정이 없으면 전체 사각형을 사용하며 참조나 상태를 변경하지 않는다.</summary>
        public static Rect FocusRegion(Component target, GameDirectTarget id)
        {
            foreach (var anchor in target.GetComponents<FeatureGuideAnchor>())
                if (anchor._bound && anchor._id == id) return anchor._focusRegion;
            return new Rect(0, 0, 1, 1);
        }

        /// <summary>원본 버튼 동작 이후 현재 단계에 클릭을 알린다. 저장이나 경제 성공 판정은 하지 않는다.</summary>
        private void Clicked() => GameDirectInteraction.NotifyClick(_id);

        /// <summary>활성화 때 현 UI 루트에 등록한다. 초기 AddComponent 중에는 아직 ID가 없으므로 건너뛴다.</summary>
        private void OnEnable()
        {
            if (!_bound) return;
            _registry = UIManager.Instance?.GuideTargets;
            _registry?.RegisterTarget(_id, transform as RectTransform);
        }

        /// <summary>비활성화 때 자신이 등록한 참조만 제거해 이전 인스턴스가 새 UI 등록을 지우지 않게 한다.</summary>
        private void OnDisable() => _registry?.UnregisterTarget(_id, transform as RectTransform);

        /// <summary>파괴 때 버튼 리스너와 레지스트리 참조를 해제한다. 계정 전환 뒤 오래된 클릭이 남지 않는다.</summary>
        private void OnDestroy() { OnDisable(); if (_button != null) _button.onClick.RemoveListener(Clicked); _registry = null; }
    }
}
