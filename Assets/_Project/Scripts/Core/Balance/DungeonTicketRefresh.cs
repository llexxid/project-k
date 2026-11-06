using Scripts.Core.Manager;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.Balance
{
    /// <summary>자정 이후 티켓 표시는 서버에서 다시 받는다. 기기 시각으로 입장권을 지급하지 않는다.</summary>
    internal static class DungeonTicketRefresh
    {
        private static long _generation = -1;
        private static bool _pending;
        private static float _retryAt;

        internal static void RequestIfDue()
        {
            if (!LocalProgression.IsReady) return;
            if (_generation != LocalProgression.AccountGeneration)
            {
                _generation = LocalProgression.AccountGeneration;
                _pending = false;
                _retryAt = 0;
            }
            var state = LocalProgression.State;
            if (_pending || Time.realtimeSinceStartup < _retryAt || state.DungeonTicketResetUtc > LocalProgression.UtcNow ||
                NetworkManager.Instance == null || !NetworkManager.Instance.IsGameDataReady || UserManager.Instance == null) return;
            _pending = true;
            _retryAt = Time.realtimeSinceStartup + 30f;
            long generation = _generation;
            UserManager.Instance.RefreshFromServer(_ =>
            {
                if (generation == LocalProgression.AccountGeneration) _pending = false;
            });
        }
    }
}
