using UnityEngine;
using KingdomIdle.Divine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// DivineSkillManager 상태 변경 → 궁극기 HUD 갱신 (MageTowerHudBridge와 동일 관례).
    /// 매니저가 UI보다 늦게 초기화될 수 있어 구독 성공까지 매 프레임 재시도한다.
    /// </summary>
    [DefaultExecutionOrder(-930)]
    public sealed class DivineSkillHudBridge : MonoBehaviour
    {
        private DivineSkillManager _mgr;

        private void Start()
        {
            TrySubscribe();
        }

        private void Update()
        {
            if (_mgr == null) TrySubscribe();
        }

        private void OnDestroy()
        {
            if (_mgr == null) return;

            _mgr.OnStateChanged -= OnStateChanged;
            _mgr.OnCardAcquired -= OnCardAcquired;
            _mgr = null;
        }

        private void TrySubscribe()
        {
            var mgr = DivineSkillManager.Instance;
            if (mgr == null) return;

            _mgr = mgr;
            _mgr.OnStateChanged += OnStateChanged;
            _mgr.OnCardAcquired += OnCardAcquired;
            OnStateChanged();
        }

        private void OnStateChanged()
        {
            if (DivineSkillHudController.Instance != null)
                DivineSkillHudController.Instance.Refresh();
        }

        /// <summary>카드 획득 — 첫 카드는 자동 장착되므로 HUD를 즉시 다시 그린다.</summary>
        private void OnCardAcquired(int cardId, bool isNew)
        {
            OnStateChanged();
        }
    }
}
