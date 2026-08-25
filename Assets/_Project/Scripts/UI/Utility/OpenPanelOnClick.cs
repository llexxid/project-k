using KingdomIdle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 같은 게임 오브젝트의 버튼을 클릭하면 지정한 <see cref="UIPanelId"/> 패널을 연다.
    /// 조건 검사나 payload 생성이 필요없는 단순 패널 진입 버튼에서 Controller의 반복을 줄이기 위해 사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class OpenPanelOnClick : MonoBehaviour
    {
        /// <summary>클릭했을 때 <see cref="UIManager"/>에 열기를 요청할 패널 식별자다.</summary>
        [SerializeField] private UIPanelId panelId;
        //새 패널을 열기 전에 현재 패널 스택을 모두 비울지 여부
        [SerializeField] private bool clearBefore;
        //열린 패널의 하단 탭 선택 상태 포함여부
        [SerializeField] private bool isTabPanel;
        
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button == null)
                button = GetComponent<Button>();

            button.onClick.AddListener(OpenPanel);
        }
        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(OpenPanel);
        }

        /// <summary>현재 UIManager에 직렬화된 패널 열기를 요청한다.</summary>
        private void OpenPanel()
        {
            // 패널의 생성과 스택 소유권은 UIManager에 있으므로 컴포넌트가 직접 프리팹을 생성하지 않는다.
            UIManager manager = UIManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning($"[{nameof(OpenPanelOnClick)}] UIManager가 없어 '{panelId}' 패널을 열 수 없습니다.", this);
                return;
            }

            // 단순 버튼에는 payload가 없으며, 추가 데이터가 필요하면 해당 기능의 Controller가 열기를 담당해야 한다.
            manager.PushPanel(panelId, payload: null, clearBefore: clearBefore, isTabPanel: isTabPanel);
        }
    }
}
