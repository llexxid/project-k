using System.Collections;
using UnityEngine;

namespace KingdomIdle.UI
{
    public class UIBoot : MonoBehaviour
    {
        [SerializeField] private float fakeLoadingSeconds = 1.0f;

        private IEnumerator Start()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[UIBoot] UIManager.Instance is null. Make sure UIManager exists in scene.");
                yield break;
            }

            UIManager.Instance.SetLoading(true, "Loading...");
            yield return new WaitForSeconds(fakeLoadingSeconds);
            UIManager.Instance.SetLoading(false);

            UIManager.Instance.ReplaceScreen(UIScreenId.Title);
        }
    }
}
