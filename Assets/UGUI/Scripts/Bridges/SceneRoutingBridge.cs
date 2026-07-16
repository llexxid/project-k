using UnityEngine;
using UnityEngine.SceneManagement;
using Scripts.Core;
using KingdomIdle.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>LoadManager 씬 전환 이벤트 → UIManager 화면 교체 (UITKSceneRoutingBridge 이식).</summary>
    public sealed class SceneRoutingBridge : MonoBehaviour
    {
        [Header("Options")]
        [SerializeField] private bool clearStacksOnSceneChanged = true;
        [SerializeField] private bool ignoreBootstrap = true;

        [Header("Scene Name Mapping (must match GameManager)")]
        [SerializeField] private string bootstrapSceneName = "bootstrap";
        [SerializeField] private string titleSceneName = "title";
        [SerializeField] private string mainSceneName = "main";
        [SerializeField] private string dungeonSceneName = "dungeon";

        private LoadManager _lm;

        private void Start()
        {
            _lm = LoadManager.Instance;
            if (_lm == null || UIManager.Instance == null)
            {
                Debug.LogError("[SceneRoutingBridge] Missing LoadManager/UIManager.");
                enabled = false;
                return;
            }

            _lm.SceneLoadFinished += OnSceneLoadFinished;

            // 현재 활성 씬 기준으로 1회 라우팅
            RouteFromActiveScene();
        }

        private void OnDestroy()
        {
            if (_lm != null)
                _lm.SceneLoadFinished -= OnSceneLoadFinished;
        }

        private void OnSceneLoadFinished(eSceneType type)
        {
            if (ignoreBootstrap && type == eSceneType.bootstrap)
                return;

            switch (type)
            {
                case eSceneType.title:
                    UIManager.Instance.ReplaceScreen(UIScreenId.Title, clearStacks: clearStacksOnSceneChanged);
                    break;
                case eSceneType.main:
                    UIManager.Instance.ReplaceScreen(UIScreenId.Main, clearStacks: clearStacksOnSceneChanged);
                    break;
                case eSceneType.dungeon:
                    UIManager.Instance.ReplaceScreen(UIScreenId.Dungeon, clearStacks: clearStacksOnSceneChanged);
                    break;
            }
        }

        private void RouteFromActiveScene()
        {
            if (UIManager.Instance == null) return;

            string active = SceneManager.GetActiveScene().name;

            if (ignoreBootstrap && active == bootstrapSceneName)
                return;

            if (active == titleSceneName)
                UIManager.Instance.ReplaceScreen(UIScreenId.Title, clearStacks: clearStacksOnSceneChanged);
            else if (active == mainSceneName)
                UIManager.Instance.ReplaceScreen(UIScreenId.Main, clearStacks: clearStacksOnSceneChanged);
            else if (active == dungeonSceneName)
                UIManager.Instance.ReplaceScreen(UIScreenId.Dungeon, clearStacks: clearStacksOnSceneChanged);
        }
    }
}
