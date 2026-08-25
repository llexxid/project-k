using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 버튼을 클릭하면 Inspector에서 지정한 로컬 UI 오브젝트를 활성화한다.
    /// UIManager가 생성하지 않는 프리팹 내부 설명창, 툴팁, 소형 팝업을 단순히 표시할 때 사용한다.
    /// <br/> * UIManager에서 관리하는 프리팹은 반드시 OpenPanelOnClick 사용
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ShowTargetOnClick : MonoBehaviour
    {
        //클릭 시 활성화할 프리팹 내부 또는 씬의 UI 오브젝트
        [SerializeField] private GameObject target;
        private Button button;

        //필수 버튼 참조를 한 번 캐싱한다.
        private void Awake()
        {
            button = GetComponent<Button>();
        }
        
        private void OnEnable()
        {
            // 설정이나 에디터 실행 순서에 관계없이 버튼 참조를 보장.
            if (button == null)
                button = GetComponent<Button>();

            button.onClick.AddListener(ShowTarget);
        }

        // 컴포넌트가 비활성화될 때 자신이 등록한 클릭 동작만 제거
        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(ShowTarget);
        }

        // 목표 오브젝트를 활성화
        private void ShowTarget()
        {
            if (target == null)
            {
                Debug.LogWarning($"[{nameof(ShowTargetOnClick)}] '{name}'에 표시할 대상이 지정되지 않았습니다.", this);
                return;
            }

            // 이 컴포넌트의 책임은 활성 상태 변경 하나뿐이며 애니메이션이나 스택 관리는 수행하지 않는다.
            target.SetActive(true);
        }
    }
}
