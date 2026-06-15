using Scripts.Core.Manager;
using Scripts.Core.Utils;
using UnityEngine;

namespace Scripts.Core
{
    public class BootstrapEntry : MonoBehaviour
    {
        [SerializeField] private eSceneType firstScene = eSceneType.title;
        [SerializeField] private bool useAsyncLoad = true;
        
        private void Start()
        {
            if (LoadManager.Instance == null)
            {
                Debug.LogError("[BootstrapEntry] LoadManager.Instance is null. Put LoadManager in bootstrap scene.");
                return;
            }
            if (useAsyncLoad)
                LoadManager.Instance.LoadAsyncScene(firstScene);
            else
                LoadManager.Instance.LoadScene(firstScene);
        }
    }
}
