using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomIdle.UI
{
    public sealed class SceneToUIScreenBridge : MonoBehaviour
    {
        [Header("Scene Name (must match Build Settings)")]
        [SerializeField] private string titleScene = "title";
        [SerializeField] private string mainScene = "main";
        [SerializeField] private string dungeonScene = "dungeon";

        [Header("Behaviour")]
        [SerializeField] private bool clearStacksOnScreenChange = true;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 에디터에서 특정 씬부터 플레이하는 경우 대비(선택이지만 유용)
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (UIManager.Instance == null) return;

            if (scene.name == titleScene)
            {
                UIManager.Instance.ReplaceScreen(UIScreenId.Title, payload: null, clearStacks: clearStacksOnScreenChange);
                return;
            }

            if (scene.name == mainScene)
            {
                UIManager.Instance.ReplaceScreen(UIScreenId.Main, payload: null, clearStacks: clearStacksOnScreenChange);
                return;
            }

            if (scene.name == dungeonScene)
            {
                UIManager.Instance.ReplaceScreen(UIScreenId.Dungeon, payload: null, clearStacks: clearStacksOnScreenChange);
                return;
            }

            // bootstrap 등은 아무것도 안 띄움
        }
    }
}
