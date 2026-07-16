using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>토글 값 변경 시 공용 클릭 SFX 재생 (PlayClickSfxOnClick의 Toggle 버전).</summary>
    [RequireComponent(typeof(Toggle))]
    public sealed class PlayClickSfxOnToggle : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Toggle>().onValueChanged.AddListener(_ =>
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.PlayButtonClickSfx();
            });
        }
    }
}
