using System;
using UnityEngine;

namespace KingdomIdle.Divine
{
    /// <summary>
    /// 전투 로직(Divine 모듈)과 UI 연출(UGUI 모듈) 사이의 얇은 연결점.
    /// UI 레이어가 핸들러를 등록하고, 전투 코드는 등록 여부만 확인한다 —
    /// UI가 없는 씬/테스트에서도 스킬이 그대로 동작하게 하기 위함.
    /// </summary>
    public static class DivinePresentation
    {
        /// <summary>
        /// 컷인 재생기. (카드, 컷인 종료 콜백) → 재생 시작했으면 true.
        /// UI 레이어(DivineCutInController)가 등록한다.
        /// </summary>
        public static Func<DivineSkillSO, Action, bool> CutInHandler;

        /// <summary>컷인 재생을 시도한다. 재생기가 없거나 거부하면 false — 호출측은 즉시 발동한다.</summary>
        public static bool TryPlayCutIn(DivineSkillSO card, Action onComplete)
        {
            if (card == null || !card.cutInEnabled) return false;

            var handler = CutInHandler;
            if (handler == null) return false;

            try
            {
                return handler(card, onComplete);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        /// <summary>연출 중 여부 — HUD 버튼 잠금 등에 쓴다.</summary>
        public static bool CutInPlaying { get; set; }
    }
}
