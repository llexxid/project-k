using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UI
{
    public class UIScreenTitle : UIScreen
    {
        [Header("Press Anywhere Button (full-screen)")]
        [SerializeField] private Button pressAnywhereButton;

        [SerializeField] private UIScreenId nextScreen = UIScreenId.Main;

        protected override void Awake()
        {
            base.Awake();

            if (pressAnywhereButton == null)
            {
                Debug.LogWarning("[UIScreenTitle] pressAnywhereButton is not assigned.");
                return;
            }

            pressAnywhereButton.onClick.RemoveListener(OnPressAnywhere);
            pressAnywhereButton.onClick.AddListener(OnPressAnywhere);
        }

        private void OnDestroy()
        {
            if (pressAnywhereButton != null)
                pressAnywhereButton.onClick.RemoveListener(OnPressAnywhere);
        }

        private void OnPressAnywhere()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[UIScreenTitle] UIManager.Instance is null.");
                return;
            }

            UIManager.Instance.ReplaceScreen(nextScreen);
        }
    }
}
