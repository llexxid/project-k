using UnityEngine;
using UnityEngine.SceneManagement;
using Scripts.Core;
using KingdomIdle.UI;

namespace KingdomIdle.UIToolkit
{
    // GameManager 씬 전환 이벤트 → UITKUIManager 화면 교체
    public sealed class UITKSceneRoutingBridge : MonoBehaviour
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
            if (_lm == null || UITKUIManager.Instance == null)
            {
                Debug.LogError("[UITKSceneRoutingBridge] Missing GameManager/UITKUIManager.");
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
                    UITKUIManager.Instance.ReplaceScreen(UIScreenId.Title, clearStacks: clearStacksOnSceneChanged);
                    break;
                case eSceneType.main:
                    UITKUIManager.Instance.ReplaceScreen(UIScreenId.Main, clearStacks: clearStacksOnSceneChanged);
                    break;
                case eSceneType.dungeon:
                    UITKUIManager.Instance.ReplaceScreen(UIScreenId.Dungeon, clearStacks: clearStacksOnSceneChanged);
                    break;
            }
        }

        private void RouteFromActiveScene()
        {
            if (UITKUIManager.Instance == null) return;

            string active = SceneManager.GetActiveScene().name;

            if (ignoreBootstrap && active == bootstrapSceneName)
                return;

            if (active == titleSceneName)
                UITKUIManager.Instance.ReplaceScreen(UIScreenId.Title, clearStacks: clearStacksOnSceneChanged);
            else if (active == mainSceneName)
                UITKUIManager.Instance.ReplaceScreen(UIScreenId.Main, clearStacks: clearStacksOnSceneChanged);
            else if (active == dungeonSceneName)
                UITKUIManager.Instance.ReplaceScreen(UIScreenId.Dungeon, clearStacks: clearStacksOnSceneChanged);
        }
    }
}