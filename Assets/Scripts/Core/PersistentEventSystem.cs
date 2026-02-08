using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomIdle.UI
{
    public sealed class PersistentEventSystem : MonoBehaviour
    {
        private static PersistentEventSystem _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // EventSystem이 붙어있는 오브젝트를 영구 유지
            DontDestroyOnLoad(gameObject);
        }
    }
}
