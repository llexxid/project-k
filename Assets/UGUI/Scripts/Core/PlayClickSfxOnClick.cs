using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 버튼 클릭 시 공용 클릭 SFX를 재생한다.
    /// 생성기가 모든 버튼에 부착 — 팀이 프리팹에 새 버튼을 추가할 때도 복사해 쓰면 된다.
    /// (기존 UITKUIManager.RegisterButtonClickSfx 대응)
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PlayClickSfxOnClick : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(static () =>
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.PlayButtonClickSfx();
            });
        }
    }
}
