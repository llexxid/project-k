using UnityEngine;

namespace Scripts.Core
{
    public class BootstrapEntry : MonoBehaviour
    {
        [SerializeField] private eSceneType firstScene = eSceneType.title;
        [SerializeField] private bool useAsyncLoad = true;

        private void Start()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[BootstrapEntry] GameManager.Instance is null. Put GameManager in bootstrap scene.");
                return;
            }

            if (useAsyncLoad)
                GameManager.Instance.LoadAsyncScene(firstScene);
            else
                GameManager.Instance.LoadScene(firstScene);
        }
    }
}
