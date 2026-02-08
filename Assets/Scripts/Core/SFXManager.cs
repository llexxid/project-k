using UnityEngine;

namespace Scripts.Core
{
    public class SFXManager : MonoBehaviour
    {
        public static SFXManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // TODO: AudioSource Pooling / SFX DataStore 등은 이후 단계에서 확장
        }
    }
}
