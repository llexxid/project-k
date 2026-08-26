using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 이 오브젝트가 활성인 동안 파티 HUD를 숨긴다.
    /// 전체 화면 모달(던전 난이도 팝업 등)의 루트에 붙여 쓴다 — 파티 HUD는 LayerPopups에
    /// 있어 패널 내부 모달의 딤 위로 떠오르기 때문에, 모달 표시 중에는 내려 준다.
    /// </summary>
    public sealed class PartyHudSuppressor : MonoBehaviour
    {
        private bool _counted;

        private void OnEnable()
        {
            if (_counted) return;
            PartyHudController.ModalSuppressCount++;
            _counted = true;
        }

        private void OnDisable()
        {
            if (!_counted) return;
            PartyHudController.ModalSuppressCount--;
            _counted = false;
        }
    }
}
