using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>Screen_Title 셸: 배경 탭 캐처 + 로그인 버튼 + 로그인 팝업.</summary>
    public sealed class TitleScreenView : MonoBehaviour
    {
        [SerializeField] internal Button bgClickCatcher;
        [SerializeField] internal Button btnLogin;
        [SerializeField] internal TMP_Text pressHint;

        [Header("Login popup")]
        [SerializeField] internal GameObject popupLogin;
        [SerializeField] internal Button popupLoginDim;   // 바깥 탭 → 닫기
        [SerializeField] internal RectTransform popupLoginBox;
        [SerializeField] internal Button btnLoginGuest;
        [SerializeField] internal Button btnLoginGoogle;
        [SerializeField] internal Button btnLoginApple;
    }
}
