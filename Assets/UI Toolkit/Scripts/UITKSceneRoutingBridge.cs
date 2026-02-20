using UnityEngine;
using Scripts.Core;
using KingdomIdle.UI;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// GameManager.SceneLoadFinished를 받아 UI Toolkit 화면(UIScreenId)을 교체
    /// </summary>
    public sealed class UITKSceneRoutingBridge : MonoBehaviour
    {
        [SerializeField] private bool clearStacksOnSceneChanged = true;
        [SerializeField] private bool ignoreBootstrap = true;

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
    }
}
