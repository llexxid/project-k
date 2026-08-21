using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 같은 게임 오브젝트의 버튼을 클릭하면 Inspector에서 지정한 로컬 UI 오브젝트를 비활성화한다.
    /// UIManager 스택과 무관한 프리팹 내부 설명창, 툴팁, 소형 팝업을 단순히 숨길 때 사용한다.
    /// <br/> * UIManager에서 관리하는 프리팹은 반드시 PopPanelOnClick 사용
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class HideTargetOnClick : MonoBehaviour
    {
        //클릭 시 비활성화할 오브젝트
        [SerializeField] private GameObject target;
        
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            // 설정이나 에디터 실행 순서에 관계없이 버튼 참조를 보장한다.
            if (button == null)
                button = GetComponent<Button>();

            button.onClick.AddListener(HideTarget);
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(HideTarget);
        }

        // 목표 오브젝트를 비활성화.
        private void HideTarget()
        {
            if (target == null)
            {
                Debug.LogWarning($"[{nameof(HideTargetOnClick)}] '{name}'에 숨길 대상이 지정되지 않았습니다.", this);
                return;
            }

            // UIManager 소유 패널은 스택이 어긋날 수 있으므로 이 컴포넌트가 아니라 PopPanelOnClick을 사용한다.
            target.SetActive(false);
        }
    }
}
