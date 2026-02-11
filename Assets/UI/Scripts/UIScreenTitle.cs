using UnityEngine;
using UnityEngine.UI;
using Scripts.Core;

namespace KingdomIdle.UI
{
    public class UIScreenTitle : UIScreen
    {
        [Header("Press Anywhere Button (full-screen)")]
        [SerializeField] private Button pressAnywhereButton;

        [Header("Next Scene")]
        [SerializeField] private eSceneType nextScene = eSceneType.main;

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
            if (GameManager.Instance == null)
            {
                Debug.LogError("[UIScreenTitle] GameManager.Instance is null.");
                return;
            }

            GameManager.Instance.LoadAsyncScene(nextScene);
        }
    }
}
