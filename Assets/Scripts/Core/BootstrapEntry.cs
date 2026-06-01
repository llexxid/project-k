using Scripts.Core.Manager;
using Scripts.Core.Utils;
using UnityEngine;

namespace Scripts.Core
{
    public class BootstrapEntry : MonoBehaviour
    {
        [SerializeField] private eSceneType firstScene = eSceneType.title;
        [SerializeField] private bool useAsyncLoad = true;

        [OfflineMode] [SerializeField] private bool isOfflineMode;
        
        private void Start()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[BootstrapEntry] GameManager.Instance is null. Put GameManager in bootstrap scene.");
                return;
            }

            //오프라인 모드시 타이틀 씬 진입 생략 + 테스트용 환경 구성
            if (isOfflineMode)
            {
                NetworkManager.Instance?.SetOfflineMode(isOfflineMode);
                UserManager.Instance?.SetupOfflineUser(eStage.Stage1_1);
                GameManager.Instance?.LoadAsyncScene(eSceneType.main);
                return;
            }
            if (useAsyncLoad)
                GameManager.Instance.LoadAsyncScene(firstScene);
            else
                GameManager.Instance.LoadScene(firstScene);
        }
    }
}
