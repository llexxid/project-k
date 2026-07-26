using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 닫기 버튼에서 대상 오브젝트를 제거하거나 비활성화한다.
    /// UIManager가 생성한 패널은 PopPanel 모드를 사용해야 패널 스택도 함께 정리된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class CloseBtn : MonoBehaviour
    {
        public enum CloseMode
        {
            DestroyTarget,
            DisableTarget,
            PopPanel,
        }

        [SerializeField] private CloseMode closeMode = CloseMode.DisableTarget;
        [SerializeField] private GameObject target;

        private Button button;

        private void Reset()
        {
            button = GetComponent<Button>();

            // 컴포넌트를 추가했을 때 바로 사용할 수 있도록 기본 대상을 부모로 지정한다.
            // 버튼이 TitleBar처럼 한 단계 더 깊게 들어가 있다면 Inspector에서 루트를 다시 지정한다.
            if (target == null && transform.parent != null)
                target = transform.parent.gameObject;
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Execute);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(Execute);
        }

        /// <summary>클릭 시 지정한 오브젝트를 제거하도록 설정한다.</summary>
        public void BindClose(GameObject obj)
        {
            target = obj;
            closeMode = CloseMode.DestroyTarget;
        }

        /// <summary>클릭 시 지정한 오브젝트를 비활성화하도록 설정한다.</summary>
        public void BindDisable(GameObject obj)
        {
            target = obj;
            closeMode = CloseMode.DisableTarget;
        }

        /// <summary>클릭 시 UIManager의 최상위 패널을 닫도록 설정한다.</summary>
        public void BindPopPanel()
        {
            target = null;
            closeMode = CloseMode.PopPanel;
        }

        /// <summary>Button 외부에서도 현재 설정된 닫기 동작을 실행할 수 있다.</summary>
        public void Execute()
        {
            switch (closeMode)
            {
                case CloseMode.DestroyTarget:
                {
                    GameObject closeTarget = ResolveTarget();
                    if (closeTarget != null)
                        Destroy(closeTarget);
                    break;
                }

                case CloseMode.DisableTarget:
                {
                    GameObject closeTarget = ResolveTarget();
                    if (closeTarget != null)
                        closeTarget.SetActive(false);
                    break;
                }

                case CloseMode.PopPanel:
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.PopPanel();
                    else
                        Debug.LogWarning("[CloseBtn] UIManager가 없어 패널을 닫을 수 없습니다.", this);
                    break;
                }
            }
        }

        private GameObject ResolveTarget()
        {
            if (target != null)
                return target;

            Debug.LogWarning(
                $"[CloseBtn] '{name}'의 닫기 대상이 지정되지 않았습니다.",
                this);
            return null;
        }
    }
}
