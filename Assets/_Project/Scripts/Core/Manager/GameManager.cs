using Cysharp.Threading.Tasks;
using KingdomIdle.OfflineRewards;
using KingdomIdle.Balance;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using Scripts.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Reincarnation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Scripts.Core
{
    
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        
        public ReincarnationService Reincarnation { get; private set; }
        public OfflineRewardManager OfflineReward { get; private set; }
        public KingdomIdle.CheckIn.CheckInService CheckIn { get; private set; }
        public KingdomIdle.Mailbox.MailboxService Mailbox { get; private set; }
        private float _nextRewardSync;
        private bool _rewardsPaused;
        private Action _retryEconomyEntry;
        private bool _economyEntryBusy;
        private GameplayLoading.Scope _entryLoading;

        public bool RetryEconomyEntry()
        {
            if (_economyEntryBusy || _retryEconomyEntry == null) return false;
            _retryEconomyEntry();
            return true;
        }
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Init();
                DontDestroyOnLoad(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        private void Init()
        {
            var policy = new ReincarnationPolicy();
            var store = new ReincarnationStore();
            var gateway = new ReincarnationGateway();

            Reincarnation = new ReincarnationService(policy, store, gateway);
            Mailbox = new KingdomIdle.Mailbox.MailboxService(() => KingdomIdle.UGUI.TitleLobbyPresentation.SavedEnglish ? "en" : "ko");

            OfflineReward = GetComponent<OfflineRewardManager>();
            if (OfflineReward == null)
                OfflineReward = gameObject.AddComponent<OfflineRewardManager>();
        }
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            KingdomIdle.Balance.LocalProgression.AccountChanged += ScheduleRewardSync;
            EconomyBatchProgression.SpendingFailed += ShowGrowthFailure;
        }
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            KingdomIdle.Balance.LocalProgression.AccountChanged -= ScheduleRewardSync;
            EconomyBatchProgression.SpendingFailed -= ShowGrowthFailure;
        }
        private void ScheduleRewardSync() { _nextRewardSync = 0; Mailbox?.CheckAccount(); }
        private void ShowGrowthFailure() => KingdomIdle.UGUI.UIManager.Instance?.ShowToast(EconomyBatchProgression.FailureMessage);
        private void OnApplicationPause(bool paused) { _rewardsPaused = paused; ScheduleRewardSync(); }
        // 출석은 포커스와 무관하게 확인하고 일시정지 중에만 쉰다. 우편 목록은 열린 패널에서만 갱신한다.
        private void OnApplicationFocus(bool focused) { ScheduleRewardSync(); }
        private void Update()
        {
            if (_rewardsPaused || Time.unscaledTime < _nextRewardSync) return;
            _nextRewardSync = Time.unscaledTime + 1f;
            var ui = KingdomIdle.UGUI.UIManager.Instance;
            var network = NetworkManager.Instance;
            // 초기 Guest 상태에 출석을 만들지 않는다. 실제 로그인 계정의 저장이 준비된 뒤 시작한다.
            if (network == null || string.IsNullOrEmpty(network.GetSessionID()) ||
                !KingdomIdle.Balance.LocalProgression.IsReady ||
                KingdomIdle.Balance.LocalProgression.AccountKey != network.GetSessionID() || ui == null || ui.Catalog == null) return;
            CheckIn ??= new KingdomIdle.CheckIn.CheckInService(ui.Catalog.checkInRewards);
            CheckIn.RecordVisit();
        }

        
        #region 플레이어 생존 관리
        // ── 플레이어 생존 관리 ──────────────────────────────────────
        // "사망 애니메이션 완료" 횟수를 셈. IsDead는 데미지 즉시 true가 되므로 사용 불가.
        private int _deathAnimationDoneCount = 0;

        /// <summary>
        /// 플레이어의 사망 애니메이션이 끝난 뒤 호출.
        /// 모든 플레이어의 애니메이션이 끝나야 게임을 정지한다.
        /// </summary>
        public void ReportPlayerDead()
        {
            _deathAnimationDoneCount++;

            //int totalPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None).Length;
            if (_deathAnimationDoneCount >= 3)
                OnAllPlayersDead();
        }
        public void ReportPlayerRevived()
        {
            _deathAnimationDoneCount = Mathf.Max(0, _deathAnimationDoneCount - 1);
        }
        private void OnAllPlayersDead()
        {
            Debug.Log("[GameManager] 전원 사망 애니메이션 완료");
            _deathAnimationDoneCount = 0;

            if (StageManager.Instance != null)
            {
                StageManager.Instance.DefeatWave();
                return;
            }

            Time.timeScale = 0f;
        }
        #endregion

        #region 씬 변경 관리
        
        /// <summary> 씬 변경이 완료되었을 시 진행되는 후처리과정 </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HandleSceneReadyAsync(scene, mode).Forget();
        /// <summary> 1프레임 대기한 후 실제로 후처리 진행하는 메서드</summary>
        private async UniTaskVoid HandleSceneReadyAsync(Scene scene, LoadSceneMode mode)
        {
            //해당 메서드는 나중에 씬별 메서드를 따로 만들거나 핸들러 딕셔너리 등 정리할 필요가 있음
            try
            {
                // 1프레임 대기해서 Start()까지 다 끝난 뒤 실행을 보장
                await UniTask.NextFrame();
                if (LoadManager.Instance == null)
                    return;
                if (scene.name == LoadManager.Instance.GetSceneName(eSceneType.main))
                {
                    HandleMainSceneReady();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Scene ready handling failed: {scene.name}\n{ex}");
            }
        }

        private void HandleMainSceneReady()
        {
            Debug.Log("메인 씬 진입");

            MonsterSpawner.Instance.OnEnterScene();
            VFXManager.Instance.OnEnterScene();
            SFXManager.Instance.PlayBGM(eSFXType.BGM);

            if (Camera.main != null && Camera.main.GetComponent<CameraFade>() == null)
                Camera.main.gameObject.AddComponent<CameraFade>();

            UserManager.Instance.CreateCharacter();
            var network = NetworkManager.Instance;
            long generation = network != null ? network.AccountGeneration : -1;
            long localGeneration = LocalProgression.AccountGeneration;
            bool IsCurrent() => network != null && NetworkManager.Instance == network && network.IsGameDataReady &&
                network.AccountGeneration == generation && LocalProgression.AccountGeneration == localGeneration;

            void EndEntryLoading()
            {
                _entryLoading?.Dispose(); _entryLoading = null; _economyEntryBusy = false;
            }

            void Failed()
            {
                if (!IsCurrent()) return;
                EndEntryLoading();
                string message = "이전 저장 결과를 확인하지 못했습니다. 입력 기록은 보존되어 있습니다. 다시 확인해 주세요.";
                if (!KingdomIdle.UGUI.EquipmentActionDialog.ShowConfirmation("저장 결과 확인", message, "다시 확인", () => RetryEconomyEntry()))
                    KingdomIdle.UGUI.UIManager.Instance?.ShowToast(message);
            }

            void BeginMainStage()
            {
                if (!IsCurrent()) return;
                if (EconomyBatchProgression.HasUnknownResult) { Failed(); return; }
                EndEntryLoading(); _retryEconomyEntry = null;
                var state = LocalProgression.State;
                if (state.Economy.Server?.OwnershipChanged == true || EconomyBatchProgression.HasHeldSpending)
                    KingdomIdle.UGUI.UIManager.Instance?.ShowToast(state.Economy.Server?.OwnershipChanged == true
                        ? "다른 기기의 미처리 강화 입력은 취소되었습니다. 확정된 성장은 유지됩니다."
                        : "실행되지 않은 강화 입력이 있습니다. 육성 화면에서 확인 후 재개해 주세요.");
                if (StageManager.Instance != null)
                    StageManager.Instance.BeginStage(UserManager.Instance.GetUserCurrentStage());
                Direction.GameDirectManager.Instance?.RequestIncompleteGuides();
                if (network != null && NetworkManager.Instance == network && network.AccountGeneration == generation)
                    KingdomIdle.UGUI.NoticePopupController.BeginMainEntry();
                Mailbox?.CheckAfterLogin();
            }

            void ClaimOffline()
            {
                if (!IsCurrent()) return;
                if (OfflineReward != null) OfflineReward.TryClaim(UserManager.Instance.GetUserCurrentStage(), BeginMainStage);
                else BeginMainStage();
            }

            void RecoverBatchAfterMain(bool ok)
            {
                if (!IsCurrent()) return;
                if (!ok) { Failed(); return; }
                if (!EconomyBatchProgression.Available || EconomyBatchProgression.RecoveryComplete) { ClaimOffline(); return; }
                if (!EconomyBatchProgression.RecoverPending(result => { if (result) ClaimOffline(); else Failed(); },
                    previousSessionReplaced: true)) Failed();
            }

            void RecoverMain(bool ok)
            {
                if (!IsCurrent()) return;
                if (!ok || StageManager.Instance == null || !StageManager.Instance.RecoverPendingMainCompletion(RecoverBatchAfterMain)) Failed();
            }

            _entryLoading?.Dispose(); _economyEntryBusy = false;
            _retryEconomyEntry = () =>
            {
                if (!IsCurrent()) return;
                _economyEntryBusy = true;
                _entryLoading = GameplayLoading.Begin("economy-login-recovery", blockInput: true);
                // 메인 진입은 성공한 Auth 뒤에만 실행된다. 이전 세션은 서버 계정 잠금 안에서 교체됐다.
                if (EconomyBatchProgression.Available && EconomyBatchProgression.HasUnresolvedBatch)
                {
                    if (!EconomyBatchProgression.RecoverPending(RecoverMain, previousSessionReplaced: true)) Failed();
                }
                else if (EconomyBatchProgression.HasUnknownResult)
                    UserManager.Instance.RefreshFromServer(ok => { if (ok && !EconomyBatchProgression.HasUnknownResult) RecoverMain(true); else Failed(); });
                else RecoverMain(true);
            };
            RetryEconomyEntry();
        }
        
        #endregion

    }
}
