using UnityEngine;
using UnityEngine.SceneManagement;
using Scripts.Core;
using KingdomIdle.UI;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// GameManager.SceneLoadFinished를 받아 UI Toolkit 화면(UIScreenId)을 교체.
    /// 이벤트를 놓쳤을 때를 대비해, 시작 시 현재 활성 씬 기준으로도 1회 라우팅한다.
    /// </summary>
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

        private GameManager _gm;

        private void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null || UITKUIManager.Instance == null)
            {
                Debug.LogError("[UITKSceneRoutingBridge] Missing GameManager/UITKUIManager.");
                enabled = false;
                return;
            }

            _gm.SceneLoadFinished += OnSceneLoadFinished;

            // ✅ 이벤트를 놓쳤을 경우 대비: 현재 활성 씬 기준으로도 1회 화면 세팅
            RouteFromActiveScene();
        }

        private void OnDestroy()
        {
            if (_gm != null)
                _gm.SceneLoadFinished -= OnSceneLoadFinished;
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