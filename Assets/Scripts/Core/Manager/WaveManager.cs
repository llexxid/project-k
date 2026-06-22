using System;
using UnityEngine;
using Scripts.Core.Utils;

namespace Scripts.Core
{
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }
        private StageManager stageManager;
        [Header("Boss")]
        [Tooltip("eStage의 long 값 (예: Stage1_1 = 8590000129)")]
        [SerializeField] private long _bossStageValue;
        [SerializeField] private float _bossTimeLimit = 30f;

        // ── 상태 ──
        /*private eStage _currentStage;
        private int _currentStageNumber;
        private int _currentWave;
        private bool _isBossWave;
        private bool _bossAutoChallenge = true;
        private bool _loopMode;*/
        private bool _bossFailedReturn;

        private float _bossTimer;
        private bool _bossTimerActive;

        private bool _deathPopupActive;
        private bool _deathPopupHandled; // 중복 호출(클릭+타임아웃) 방지
        private float _deathPopupTimer;
        private const float DeathPopupDuration = 15f;

        // ── 이벤트 ──
        public event Action<int, int, bool> OnWaveChanged;
        public event Action<bool> OnLoopModeChanged;
        public event Action<bool> OnBossAutoChallengeChanged;
        public event Action OnDeathPopupShow;
        public event Action OnDeathPopupHide;
        public event Action<float> OnDeathPopupTick;
        public event Action<float> OnBossTimerTick;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Update()
        {
            if (_bossTimerActive)
            {
                _bossTimer -= Time.deltaTime;
                OnBossTimerTick?.Invoke(Mathf.Clamp01(_bossTimer / _bossTimeLimit));
                if (_bossTimer <= 0f)
                {
                    _bossTimerActive = false;
                    HandleAllPlayersDead();
                }
            }

            if (_deathPopupActive)
            {
                _deathPopupTimer -= Time.unscaledDeltaTime;
                OnDeathPopupTick?.Invoke(Mathf.Clamp01(_deathPopupTimer / DeathPopupDuration));
                if (_deathPopupTimer <= 0f)
                {
                    _deathPopupActive = false;
                    OnDeathPopupChoose(false);
                }
            }
        }

        // ── 외부 접근 ──
        
         public int CurrentStageNumber => StageManager.Instance.StageNumber;
        public int CurrentWave => StageManager.Instance.WaveNumber;
        public bool IsBossWave => StageManager.Instance.IsBossWave;
        public bool IsLoopMode => StageManager.Instance.IsLoopMode;
        public bool BossAutoChallenge => StageManager.Instance.BossAutoChallenge;
        public eStage CurrentStage => StageManager.Instance.CurrentStage;
        
        // ══════════════════════════════════════
        //  초기 진입
        // ══════════════════════════════════════

        public void BeginFromStage(eStage stage)
        {
            stageManager = StageManager.Instance;
            StageManager.Instance.InitStage(stage);
            StartWave(stage);
        }

        // ══════════════════════════════════════
        //  웨이브 시작/종료
        // ══════════════════════════════════════
        private void StartWave(eStage stage)
        {
            _bossTimerActive = false;
            _deathPopupActive = false;
            Time.timeScale = 1f;

            ReviveAllPlayers();
            //DespawnAllMonsters();
            StageManager.Instance.ResetWaveCount();
            OnWaveChanged?.Invoke(stageManager.StageNumber, stageManager.WaveNumber, stageManager.IsBossWave);

            if (stageManager.IsBossWave)
            {
                _bossTimer = _bossTimeLimit;
                _bossTimerActive = true;
            }
            StageManager.Instance.SpawnStageMonster(stage);
            
        }

        // ══════════════════════════════════════
        //  웨이브 클리어 (StageManager에서 호출)
        // ══════════════════════════════════════

        public void OnWaveCleared()
        {
            eStageResult result = StageRule.GetNextWave(stageManager.CurrentStage, out var nextStage);
            switch (result)
            {
                case eStageResult.StageChanged: //보스 클리어로 스테이지 변경
                    GoNextStage();
                    break;
                case eStageResult.BossWaveEntered: //보스 스테이지 입장
                    if (stageManager.BossAutoChallenge)
                    {
                        GoNextWave();
                        break;
                    }
                    stageManager.SetLoopMode(true);
                    OnLoopModeChanged?.Invoke(true);
                    GoNextWave();
                    break;
                case eStageResult.WaveChanged: //일반 스테이지 클리어
                    stageManager.SetLoopMode(false);
                    OnLoopModeChanged?.Invoke(false);
                    GoNextWave();
                    break;
                default:
                    CustomLogger.LogError($"[WaveManager]: 웨이브 클리어 작동방식이 작동하지 않습니다 {result}");
                    break;
            }
        }


        // ══════════════════════════════════════
        //  전원 사망 처리
        // ══════════════════════════════════════

        public void HandleAllPlayersDead()
        {
            _bossTimerActive = false;
            ShowDeathPopup();
        }

        private void ShowDeathPopup()
        {
            Time.timeScale = 0f;
            _deathPopupActive = true;
            _deathPopupHandled = false;
            _deathPopupTimer = DeathPopupDuration;
            OnDeathPopupShow?.Invoke();
        }

        public void OnDeathPopupChoose(bool retryCurrentWave)
        {
            // 메서드 중복실행 방지
            if (_deathPopupHandled) return;
            _deathPopupHandled = true;
            _deathPopupActive = false;
            OnDeathPopupHide?.Invoke();

            if (stageManager.IsBossWave)
            {
                _bossFailedReturn = true;
                stageManager.MovePrev(); //이전 스테이지로 이동
                
                stageManager.SetBossAutoChallenge(false);
                OnBossAutoChallengeChanged?.Invoke(false);
                stageManager.SetLoopMode(true);
                OnLoopModeChanged?.Invoke(true);
                
                OnWaveRestart();
                return;
            }

            if (retryCurrentWave)
            {
                stageManager.SetLoopMode(false);
                OnLoopModeChanged?.Invoke(false);
                OnWaveRestart();
            }
            else
            {
                stageManager.MovePrev();

                stageManager.SetLoopMode(true);
                OnLoopModeChanged?.Invoke(true);
                OnWaveRestart();
            }
        }

        // ══════════════════════════════════════
        //  반복 모드 / 보스 자동 도전
        // ══════════════════════════════════════

        public void DisableLoopMode()
        {
            stageManager.SetLoopMode(false);
            OnLoopModeChanged?.Invoke(false);
        }

        public void SetBossAutoChallenge(bool value)
        {
            CustomLogger.Log($"[WaveManager] : BossAutoChallenge is Changed : {value}");
            stageManager.SetBossAutoChallenge(value);
            OnBossAutoChallengeChanged?.Invoke(value);
        }

        /// <summary>
        /// 보스 처치후 다음 스테이지로 넘어가는 메서드
        /// </summary>
        private void GoNextStage()
        {
            _bossTimerActive = false;
            _bossFailedReturn = false;

            eStage prevStage = stageManager.CurrentStage;
            stageManager.MoveNext(); //스테이지 이동은 여기서 
            var fade = CameraFade.Instance;
            if (fade != null)
            {
                fade.FadeOut(0.4f, () =>
                {
                    LoadManager.Instance.LoadStage(prevStage, stageManager.CurrentStage, (stage) =>
                    {
                        StartWave(stageManager.CurrentStage);
                        fade.FadeIn(0.4f);
                    }); 
                });
            }
            else
            {
                StartWave(stageManager.CurrentStage);
            }
        }
        /// <summary>
        /// 웨이브 클리어 후 새로운 웨이브 몬스터를 스폰하는 메서드
        /// </summary>
        private void GoNextWave()
        {
            var fade = CameraFade.Instance;
            if (fade != null)
            {
                fade.FadeOutIn(0.3f, 0.3f, onDark: () =>
                    {
                        //DespawnAllMonsters();
                        ReviveAllPlayers();
                        StageManager.Instance.ResetWaveCount();
                        if (!stageManager.IsLoopMode)
                            stageManager.MoveNext();
                        OnWaveChanged?.Invoke(stageManager.StageNumber, stageManager.WaveNumber, stageManager.IsBossWave);
                        if (stageManager.IsBossWave)
                        {
                            _bossTimer = _bossTimeLimit;
                            _bossTimerActive = true;
                        }
                        Debug.Log("begin");
                        StageManager.Instance.SpawnStageMonster(stageManager.CurrentStage);
                    });
            }
            else
            {
                if (!stageManager.IsLoopMode)
                    stageManager.MoveNext();
                StartWave(stageManager.CurrentStage);
            }
        }

        /// <summary>
        /// 웨이브 실패 후 다시 시작하는 메서드
        /// </summary>
        private void OnWaveRestart() 
        {
            var fade = CameraFade.Instance;
            //DespawnAllMonsters();
            ReviveAllPlayers();
            StageManager.Instance.ResetWaveCount();

            Time.timeScale = 1f;
            OnWaveChanged?.Invoke(stageManager.StageNumber, stageManager.WaveNumber, stageManager.IsBossWave);
 
            StageManager.Instance.SpawnStageMonster(stageManager.CurrentStage);
            
            if (fade != null)
                fade.FadeIn(0.4f);
        }

        // ══════════════════════════════════════
        //  유틸
        // ══════════════════════════════════════

        private void ReviveAllPlayers()
        {
            var um = UserManager.Instance;
            if (um == null) return;
            var userField = typeof(UserManager).GetField("_user",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (userField == null) return;
            var user = userField.GetValue(um) as Scripts.Users.User;
            if (user?._players == null) return;

            foreach (var p in user._players)
            {
                if (p != null) p.Revive();
            }
        }

        // private void DespawnAllMonsters()
        // {
        //     var monsters = FindObjectsByType<Scripts.Monster.Monster>(FindObjectsSortMode.None);
        //     foreach (var m in monsters)
        //     {
        //         if (m == null || !m.gameObject.activeInHierarchy) continue;
        //         m.gameObject.SetActive(false);
        //         MonsterSpawner.Instance.ReleaseMonster(m.Type, m);
        //     }
        // }
    }
}
