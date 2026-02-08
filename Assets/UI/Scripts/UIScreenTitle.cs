using UnityEngine;
using UnityEngine.UI;
using Scripts.Core;

namespace KingdomIdle.UI
{
    // NOTE:
    // - 지금 구조는 "타이틀/메인/던전 = 씬" 이므로, 타이틀에서 메인으로 갈 때는 ReplaceScreen이 아니라 씬 로드가 맞음.
    // - PressAnywhere 버튼에 TitlePressAnywhereToLoadScene을 쓰고 있다면, 이 스크립트의 클릭 로직과 중복될 수 있으니 둘 중 하나만 사용 권장.
    public class UIScreenTitle : UIScreen
    {
        [Header("Press Anywhere Button (full-screen)")]
        [SerializeField] private Button pressAnywhereButton;

        [Header("Next Scene")]
        [SerializeField] private eSceneType nextScene = eSceneType.main;

        [SerializeField] private bool disableButtonWhileLoading = true;

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

            if (disableButtonWhileLoading && pressAnywhereButton != null)
                pressAnywhereButton.interactable = false;

            GameManager.Instance.LoadAsyncScene(nextScene);
        }
    }
}
