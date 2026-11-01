using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 메인 화면 스테이지/웨이브 HUD (스테이지 라벨, 루프 아이콘, 보스 자동 도전 토글,
    /// 보스 타이머 바, 전원 사망 팝업). WaveUIController가 바인딩한다.
    /// </summary>
    public sealed class WaveHudView : MonoBehaviour
    {
        [Header("Stage row")]
        [SerializeField] internal TMP_Text lblStage;
        [SerializeField] internal Button btnLoopIcon;
        [SerializeField] internal Button btnStageAction;
        [SerializeField] internal GameObject bossChallengeRoot;
        [SerializeField] internal Toggle tglBossChain;
        [Header("Progression HUD")]
        [SerializeField] internal RectTransform topArea;
        [SerializeField] internal GameObject mainHud, mainProgress, chapterProgress;
        [SerializeField] internal TMP_Text progressLabel, remainingLabel, stageActionLabel;
        [SerializeField] internal Image progressFill;
        [SerializeField] internal Image[] chapterSteps;
        [SerializeField] internal Button btnStageSelect;
        [SerializeField] internal GuideGoalView guide;
        [Header("Dungeon objective")]
        [SerializeField] internal GameObject dungeonObjective, dungeonReturn;
        [SerializeField] internal TMP_Text dungeonTitle, dungeonTime, dungeonProgress, dungeonReward, dungeonName;
        [SerializeField] internal Image dungeonFill;
        [SerializeField] internal Button btnDungeonReturn;
        [Header("Dungeon return confirmation")]
        [SerializeField] internal GameObject returnPopup;
        [SerializeField] internal RectTransform returnPanel;
        [SerializeField] internal Button returnConfirm, returnCancel, returnBackdrop;

        [Header("Boss timer")]
        [SerializeField] internal GameObject bossTimerBar;
        [SerializeField] internal Image bossTimerFill;

        [Header("Death popup")]
        [SerializeField] internal GameObject deathPopup;
        [SerializeField] internal TMP_Text lblDeathMsg;
        [SerializeField] internal Button btnDeathYes;
        [SerializeField] internal Button btnDeathNo;
        [SerializeField] internal Image deathTimerFill;
    }
}
