using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UI
{
    public class UIPanelCloseBinder : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton == null)
            {
                Debug.LogWarning("[UIPanelCloseBinder] closeButton is not assigned.");
                return;
            }

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                if (UIManager.Instance == null)
                {
                    Debug.LogError("[UIPanelCloseBinder] UIManager.Instance is null.");
                    return;
                }

                UIManager.Instance.RequestBack(); // Popup 있으면 팝업부터, 없으면 패널 Pop
            });
        }
    }
}
