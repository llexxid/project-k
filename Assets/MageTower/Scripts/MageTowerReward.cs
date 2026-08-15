using KingdomIdle.KingdomArmy;

namespace KingdomIdle.MageTower
{
    /// <summary>
    /// 마탑 스킬 처치 보상 귀속 헬퍼.
    ///
    /// 마탑 스킬에는 소유 Player 가 없다. 반면 Monster.TakeDamage 는 공격자가 IRewardable 일 때만
    /// 경험치·골드·장비 드롭을 지급하므로, 마탑 스킬 오브젝트가 IRewardable 을 구현하고
    /// 실제 지급은 살아있는 파티원 한 명에게 위임한다.
    ///
    /// 보상 자체는 계정 단위(User 지갑 + 전역 EquipmentManager 드롭)라서 어느 파티원에게
    /// 귀속시켜도 결과가 같다 — 사망 여부도 무관하다 (전멸 중 마탑 킬의 보상 유실 방지).
    /// </summary>
    internal static class MageTowerReward
    {
        public static void GiveToParty(int gold, int ancientCoin)
        {
            var km = KingdomArmyManager.Instance;
            if (km == null) return;

            var players = km.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.User == null) continue;

                p.GiveReward(gold, ancientCoin);
                return;
            }
        }
    }
}
