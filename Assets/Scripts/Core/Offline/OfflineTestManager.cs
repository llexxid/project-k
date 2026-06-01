using System.Collections;
using System.Collections.Generic;
using Scripts.Server.DTO;
using UnityEngine;

namespace Scripts.Core.Offline
{
    public sealed class OfflineTestManager : MonoBehaviour
    {
        public static OfflineTestManager Instance { get; private set; }

        [SerializeField] private eStage startStage = eStage.Stage1;
        [SerializeField] private long expPerKill = 5;
        [SerializeField] private long goldPerKill = 10;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetupOfflineUser()
        {
            UserManager.Instance.SetupOfflineUser(startStage);
        }

        //기존 NetworkManager의 OnHuntReward 대체메서드
        //킬스코어 / 저장 / 로그작성용
        public void ApplyHuntReward(List<HuntResult> huntResults)
        {
            long killCount = 0;

            if (huntResults != null)
            {
                foreach (var result in huntResults)
                    killCount += result.Count;
            }

            UserData current = UserManager.Instance.GetUserData();

            var reward = new OnHuntResponseDTO
            {
                Level = current._level,
                Exp = current._exp,
                KillScore = current._killScore + killCount,
                Gold = UserManager.Instance.GetUserCoin(),
                AncientCoin = UserManager.Instance.GetUserAncientCoin()
            };

            UserManager.Instance.SetHuntResult(reward);

            Debug.Log($"[OfflineTest] Hunt result synced. kills={killCount}");

        }

        public void ApplyEnchantHp(int count)
        {
            Debug.Log($"[OfflineTest] Enchant HP skipped/applied locally. count={count}");
        }

        public void ApplyEnchantAtk(int count)
        {
            Debug.Log($"[OfflineTest] Enchant ATK skipped/applied locally. count={count}");
        }
    }
}