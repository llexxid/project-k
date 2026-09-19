using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 퀘스트 패널의 직렬화 참조를 보관하는 셸이다.
    /// BalanceQuestPanel이 탭과 목록을 제어하며, 이 View는 진행 판정이나 보상 지급을 하지 않는다.
    /// </summary>
    public sealed class GuidePanelView : BottomSheetView
    {
        // 이전 프리팹과 생성기 참조를 보존한다. 탭형 화면에서는 기존 진행 바와 문구를 숨긴다.
        [SerializeField] internal Image progressFill;
        [SerializeField] internal TMP_Text progressLabel;

        // 네 종류가 공유하는 스크롤 목록이다. 탭을 바꿀 때 내용만 다시 바인딩한다.
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform listContent;

        // 공용 탭 버튼을 한 번 생성할 부모와, 가이드 탭에서만 켜는 상단 카드다.
        [SerializeField] internal RectTransform tabBar;
        [SerializeField] internal GameObject currentQuestRoot;
    }
}
