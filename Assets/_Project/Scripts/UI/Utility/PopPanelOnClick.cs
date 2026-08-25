using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 같은 게임 오브젝트의 버튼을 클릭하면 <see cref="UIManager"/>가 소유한 최상단 패널을 닫는다.
    /// 공통 BottomSheetView 배선을 사용하지 않는 예외 패널의 닫기 버튼에서 스택 수명을 안전하게 유지하기 위해 사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class PopPanelOnClick : MonoBehaviour
    {
        /// <summary>이 컴포넌트와 같은 게임 오브젝트에 있는 클릭 입력 소스다.</summary>
        private Button button;

        /// <summary>필수 버튼 참조를 한 번 캐싱한다.</summary>
        private void Awake()
        {
            button = GetComponent<Button>();
        }

        /// <summary>컴포넌트가 활성화될 때 자기 클릭 동작만 등록한다.</summary>
        private void OnEnable()
        {
            // 도메인 리로드 설정이나 에디터 실행 순서에 관계없이 버튼 참조를 보장한다.
            if (button == null)
                button = GetComponent<Button>();

            button.onClick.AddListener(PopPanel);
        }

        /// <summary>컴포넌트가 비활성화될 때 자신이 등록한 클릭 동작만 제거한다.</summary>
        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(PopPanel);
        }

        /// <summary>현재 UIManager에 최상단 패널 닫기를 요청한다.</summary>
        private void PopPanel()
        {
            // 패널 파괴와 이전 패널 재활성화는 스택 소유자인 UIManager에 위임한다.
            UIManager manager = UIManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning($"[{nameof(PopPanelOnClick)}] UIManager가 없어 패널을 닫을 수 없습니다.", this);
                return;
            }

            manager.PopPanel();
        }
    }
}
