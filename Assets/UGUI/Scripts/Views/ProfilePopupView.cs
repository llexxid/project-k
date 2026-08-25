using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 프로필 팝업(더미/플레이스홀더). 좌상단 프로필 버튼으로 열린다.
    /// 서버 미연동 — MainScreenController가 보유 데이터(닉네임/레벨)만 채우고 나머진 샘플값.
    /// 프리팹: Popup_Profile.prefab. 상용 아이들 게임 공통 구성 + 게임 고유 요소.
    /// </summary>
    public sealed class ProfilePopupView : MonoBehaviour
    {
        [Header("Chrome")]
        [SerializeField] internal Button backdrop;
        [SerializeField] internal Button closeButton;
        [SerializeField] internal RectTransform panel;

        [Header("Identity")]
        [SerializeField] internal Image avatar;
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal Button editNameButton;
        [SerializeField] internal TMP_Text levelLabel;      // 레벨 배지 숫자
        [SerializeField] internal Image xpFill;             // XP 바 채움
        [SerializeField] internal TMP_Text xpLabel;         // "cur / next"
        [SerializeField] internal TMP_Text idLabel;         // 플레이어 ID

        [Header("Summary pills")]
        [SerializeField] internal Button powerButton;
        [SerializeField] internal TMP_Text powerLabel;      // 전투력(CP)
        [SerializeField] internal TMP_Text trophyLabel;     // 트로피
        [SerializeField] internal TMP_Text guildLabel;      // 길드

        [Header("Season / league card")]
        [SerializeField] internal Image leagueEmblem;
        [SerializeField] internal TMP_Text leagueLabel;
        [SerializeField] internal TMP_Text leagueTrophyLabel;

        [Header("Stats grid (label→value)")]
        [SerializeField] internal TMP_Text[] statValues;    // 스테이지클리어/랭킹/승/퍼펙트/킬/최고리그

        [Header("Game-unique")]
        [SerializeField] internal TMP_Text kingdomLevelLabel;   // 왕국 레벨(게임 고유)
        [SerializeField] internal TMP_Text totalJobsLabel;      // 보유 전직 수(게임 고유)
    }
}
