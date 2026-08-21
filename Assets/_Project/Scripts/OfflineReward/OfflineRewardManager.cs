using System;
using System.Collections;
using KingdomIdle.UGUI;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.CloudScriptModels;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Server.DTO;
using UnityEngine;

namespace KingdomIdle.OfflineRewards
{
    /// <summary>
    /// 계정별 마지막 활동 시각을 추적하고, 메인 전투 시작 전에 기존 OnHuntReward로 오프라인 사냥을 정산한다.
    /// 실패한 요청 계획은 PlayerPrefs에 남겨 다음 접속에서 같은 몬스터 분포로 다시 시도한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfflineRewardManager : MonoBehaviour
    {
        // PlayFabId를 붙이는 이유는 한 기기에서 다양한 계정으로 로그인할때의 정보를 분리하기 위함
        // 계정별 마지막 활동 시각을 PlayerPrefs에 저장하기 위한 Key 접두사
        // 실제 Key: offline_reward_last_active_utc_ms_{PlayFabId}
        private const string LastActiveKeyPrefix = "offline_reward_last_active_utc_ms_";
        // 계정별 미처리 오프라인 보상 계획을 PlayerPrefs에 저장하기 위한 Key 접두사
        // 실제 Key: offline_reward_pending_plan_{PlayFabId}
        private const string PendingPlanKeyPrefix = "offline_reward_pending_plan_";
        
        private const float SessionWaitTimeoutSeconds = 10f;
        private DateTimeOffset _launchUtc;
        private string _activePlayerId;
        private bool _claimStarted; //오프라인 보상 요청이 시작했는지
        private bool _claimFinished; //성공, 실패 관계없이 처리가 완료되었는지
        private Action _onCompleted;

        private void Awake()
        {
            _launchUtc = DateTimeOffset.UtcNow;
        }

        //앱이 백그라운드로 갔을 때 시간을 저장
        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveLastActive(DateTimeOffset.UtcNow);
        }

        //앱이 종료되었을 때 시간을 저장
        private void OnApplicationQuit()
        {
            SaveLastActive(DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// 저장된 오프라인 시간을 현재 스테이지 몬스터 비중으로 분배하고 서버 정산 후 완료 콜백을 호출한다.
        /// 완료 콜백은 성공·실패와 관계없이 한 번 호출되어 메인 전투가 계속 시작되게 한다.
        /// </summary>
        public void TryClaim(eStage stageId, Action onCompleted)
        {
            if (_claimStarted)
            {
                onCompleted?.Invoke();
                return;
            }

            _claimStarted = true;
            _onCompleted = onCompleted;

            NetworkManager network = NetworkManager.Instance;
            StageManager stageManager = StageManager.Instance;
            _activePlayerId = network != null ? network.GetSessionID() : null;
            if (network == null || stageManager == null ||
                string.IsNullOrWhiteSpace(_activePlayerId))
            {
                Debug.LogWarning(
                    "[OfflineReward] 네트워크 또는 스테이지 계정 정보가 없어 정산을 건너뜁니다.");
                Complete();
                return;
            }
            
            //이전에 계산은 했지만 서버요청에 실패했던 보상이 있는지 확인
            OfflineRewardPlan plan = LoadPendingPlan();
            if (plan == null)
            {
                //마지막 접속시간이 없으면 오프라인보상 계산불가
                if (!TryReadLastActive(out DateTimeOffset lastActiveUtc))
                {
                    SaveLastActive(_launchUtc);
                    Complete();
                    return;
                }

                SaveLastActive(_launchUtc);
                if (!stageManager.TryGetStageDefinition(stageId, out StageDefinition definition))
                {
                    Debug.LogWarning(
                        $"[OfflineReward] 스테이지 정의를 찾을 수 없습니다: {stageId}");
                    Complete();
                    return;
                }
                //마지막 접속시간에 따른 오프라인 보상 계산
                plan = OfflineRewardCalculator.CreatePlan(
                    _launchUtc - lastActiveUtc,
                    definition);
                if (!plan.HasReward)
                {
                    Complete();
                    return;
                }
                //보상을 저장 후 서버에 요청하는 이유는 서버요청 중 종료되거나 실패하는 경우 기존 계산결과 손실을 막기위함
                SavePendingPlan(plan);
                /*
                 * 다만
                 * 서버 지급 성공
                 * → 클라이언트가 응답을 받기 전에 종료
                 * → PendingPlan이 남음
                 * → 다음 접속에서 같은 보상을 다시 요청의 문제가 발생할 수 있어 서버와 이야기해서 RewardClaimId 같은 멱등성 키가 필요
                 */
            }
            else
            {
                SaveLastActive(_launchUtc);
            }

            StartCoroutine(ClaimWhenSessionReady(plan));
        }
        
        /// <summary>
        /// 서버 세션이 완료될때까지 대기한 후 _sessionGUID가 준비되면 보상요청
        /// </summary>
        /// <param name="plan"></param>
        /// <returns></returns>
        private IEnumerator ClaimWhenSessionReady(OfflineRewardPlan plan)
        {
            float deadline = Time.realtimeSinceStartup + SessionWaitTimeoutSeconds;
            //Time.realtimeSinceStartup 쓰는 이유는 게임 내 timeScale = 0이어도 실제시간 기준으로 계산해야하기 때문
            while (!NetworkManager.Instance.HasSessionGuid &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!NetworkManager.Instance.HasSessionGuid)
            {
                HandleClaimFailure(
                    "서버 세션 준비 시간이 초과되어 다음 접속 때 다시 시도합니다.");
                yield break;
            }
            
            //OfflineRewardPlan plan의 값을 HuntResult타입으로 변경(NetworkManager.OnHuntReward사용목적) 
            var hunts = plan.CreateHuntResults();
            if (hunts.Count == 0)
            {
                ClearPendingPlan();
                Complete();
                yield break;
            }

            long previousGold = UserManager.Instance.GetUserCoin();
            long previousAncientCoin = UserManager.Instance.GetUserAncientCoin();

            NetworkManager.Instance.OnHuntReward(
                hunts,
                result => HandleClaimSuccess(
                    result,
                    plan,
                    previousGold,
                    previousAncientCoin),
                HandleNetworkError);
        }

        /// <summary>
        /// NetworkManager.OnHuntResult 완료시 콜백되는 메서드.
        /// <br/> 계산후 값들(골드나 고대주화...)을 팝업창에 띄우기 위한 목적
        /// </summary>
        private void HandleClaimSuccess(
            ExecuteFunctionResult result,
            OfflineRewardPlan plan,
            long previousGold,
            long previousAncientCoin)
        {
            if (_claimFinished)
                return;

            try
            {
                string response = JsonConvert.SerializeObject(result.FunctionResult);
                OnHuntResponseDTO huntResult =
                    JsonConvert.DeserializeObject<OnHuntResponseDTO>(response);
                if (huntResult == null)
                {
                    HandleClaimFailure("서버 오프라인 보상 응답이 비어 있습니다.");
                    return;
                }

                ClearPendingPlan();
                UserManager.Instance.SetHuntResult(huntResult);

                var claimResult = new OfflineRewardClaimResult(
                    plan,
                    Math.Max(0L, huntResult.Gold - previousGold), //huntResult = 서버에서 보상처리 후 결과(기존 100, 사냥보상 20 => huntResult = 120)
                    Math.Max(0L, huntResult.AncientCoin - previousAncientCoin),
                    huntResult.Level,
                    huntResult.Exp,
                    huntResult.KillScore);
                OfflineRewardPopupController.Show(claimResult);
                Complete();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[OfflineReward] 서버 응답 처리 실패\n{exception}");
                HandleClaimFailure("오프라인 보상 응답 처리에 실패했습니다.");
            }
        }

        private void HandleNetworkError(PlayFabError error)
        {
            string message = error != null
                ? error.ErrorMessage
                : "알 수 없는 네트워크 오류";
            HandleClaimFailure(
                $"오프라인 보상 요청 실패: {message}\n다음 접속 때 다시 시도합니다.");
        }

        private void HandleClaimFailure(string message)
        {
            if (_claimFinished)
                return;

            Debug.LogWarning($"[OfflineReward] {message}");
            UIManager.Instance?.ShowToast("오프라인 보상은 다음 접속 때 다시 시도합니다.");
            Complete();
        }

        /// <summary>
        /// 마지막으로 접속한 시간 계산
        /// </summary>
        private bool TryReadLastActive(out DateTimeOffset lastActiveUtc)
        {
            lastActiveUtc = default;
            string raw = PlayerPrefs.GetString(GetLastActiveKey(), string.Empty);
            if (!long.TryParse(raw, out long unixMilliseconds))
                return false;

            try
            {
                lastActiveUtc = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
                return lastActiveUtc <= _launchUtc;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private void SaveLastActive(DateTimeOffset utc)
        {
            if (string.IsNullOrWhiteSpace(_activePlayerId))
                return;

            PlayerPrefs.SetString(
                GetLastActiveKey(),
                utc.ToUnixTimeMilliseconds().ToString());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 이전에 오프라인 보상계획은 만들어졌지만 서버 요청에 실패한 계획를 불러오기
        /// </summary>
        /// <returns></returns>
        private OfflineRewardPlan LoadPendingPlan()
        {
            string raw = PlayerPrefs.GetString(GetPendingPlanKey(), string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                OfflineRewardPlan plan = JsonUtility.FromJson<OfflineRewardPlan>(raw);
                return plan != null && plan.HasReward ? plan : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OfflineReward] 보류 보상 로드 실패: {exception.Message}");
                return null;
            }
        }

        private void SavePendingPlan(OfflineRewardPlan plan)
        {
            PlayerPrefs.SetString(GetPendingPlanKey(), JsonUtility.ToJson(plan));
            PlayerPrefs.Save();
        }

        private void ClearPendingPlan()
        {
            PlayerPrefs.DeleteKey(GetPendingPlanKey());
            PlayerPrefs.Save();
        }

        private string GetLastActiveKey()
        {
            return LastActiveKeyPrefix + _activePlayerId;
        }

        private string GetPendingPlanKey()
        {
            return PendingPlanKeyPrefix + _activePlayerId;
        }

        private void Complete()
        {
            if (_claimFinished)
                return;

            _claimFinished = true;
            Action callback = _onCompleted;
            _onCompleted = null;
            callback?.Invoke();
        }
    }
}
