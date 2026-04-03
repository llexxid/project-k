using System;
using UnityEngine;
using Scripts.Core.Utils;

namespace Scripts.Core
{
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("Boss")]
        [Tooltip("eStage의 long 값 (예: Stage1_1 = 8590000129)")]
        [SerializeField] private long _bossStageValue;
        [SerializeField] private float _bossTimeLimit = 30f;

        // ── 상태 ──
        private eStage _currentStage;
        private int _currentStageNumber;
        private int _currentWave;
        private bool _isBossWave;
        private bool _bossAutoChallenge = true;
        private bool _loopMode;
        private bool _bossFailedReturn;

        private float _bossTimer;
        private bool _bossTimerActive;

        private bool _deathPopupActive;
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
        public int CurrentStageNumber => _currentStageNumber;
        public int CurrentWave => _currentWave;
        public bool IsBossWave => _isBossWave;
        public bool IsLoopMode => _loopMode;
        public bool BossAutoChallenge => _bossAutoChallenge;
        public eStage CurrentStage => _currentStage;

        // ══════════════════════════════════════
        //  초기 진입
        // ══════════════════════════════════════

        public void BeginFromStage(eStage stage)
        {
            ParseStage(stage, out _currentStageNumber, out _currentWave);
            _currentStage = stage;
            _isBossWave = false;
            _loopMode = false;
            _bossFailedReturn = false;
            StartWave();
        }

        // ══════════════════════════════════════
        //  웨이브 시작/종료
        // ══════════════════════════════════════

        private void StartWave()
        {
            _bossTimerActive = false;
            _deathPopupActive = false;
            Time.timeScale = 1f;

            ReviveAllPlayers();
            DespawnAllMonsters();

            OnWaveChanged?.Invoke(_currentStageNumber, _currentWave, _isBossWave);

            if (_isBossWave)
            {
                _bossTimer = _bossTimeLimit;
                _bossTimerActive = true;
                StageManager.Instance.StartStage((eStage)_bossStageValue);
            }
            else
            {
                StageManager.Instance.StartStage(_currentStage);
            }
        }

        // ══════════════════════════════════════
        //  웨이브 클리어 (StageManager에서 호출)
        // ══════════════════════════════════════

        public void OnWaveCleared()
        {
            if (_isBossWave)
            {
                _bossTimerActive = false;
                OnBossCleared();
                return;
            }

            if (_currentWave >= 10)
            {
                if (_bossAutoChallenge)
                {
                    _isBossWave = true;
                    FadeAndStart();
                    return;
                }
                else
                {
                    _loopMode = true;
                    OnLoopModeChanged?.Invoke(true);
                    FadeAndStart();
                    return;
                }
            }

            _currentWave++;
            _currentStage = (eStage)((ulong)_currentStage + 1);
            _loopMode = false;
            OnLoopModeChanged?.Invoke(false);
            FadeAndStart();
        }

        private void OnBossCleared()
        {
            _isBossWave = false;

            _currentStageNumber++;
            _currentWave = 1;
            _bossFailedReturn = false;

            ulong stageAdder = 0x0000000000010000;
            ulong baseStage = (ulong)_currentStage & 0xFFFFFFFFFFFF0000;
            _currentStage = (eStage)(baseStage + stageAdder + 1);

            var fade = CameraFade.Instance;
            if (fade != null)
            {
                fade.FadeOut(0.4f, () =>
                {
                    eStage prevResource = (eStage)((ulong)_currentStage - stageAdder - 1 + 10);
                    GameManager.Instance.LoadStage(prevResource, _currentStage, (stage) =>
                    {
                        _currentStage = stage;
                        ParseStage(stage, out _currentStageNumber, out _currentWave);
                        StartWave();
                        fade.FadeIn(0.4f);
                    });
                });
            }
            else
            {
                StartWave();
            }
        }

        // ══════════════════════════════════════
        //  전원 사망 처리
        // ══════════════════════════════════════

        public void HandleAllPlayersDead()
        {
            _bossTimerActive = false;

            var fade = CameraFade.Instance;
            if (fade != null)
                fade.FadeOut(0.3f, () => ShowDeathPopup());
            else
                ShowDeathPopup();
        }

        private void ShowDeathPopup()
        {
            Time.timeScale = 0f;
            _deathPopupActive = true;
            _deathPopupTimer = DeathPopupDuration;
            OnDeathPopupShow?.Invoke();
        }

        public void OnDeathPopupChoose(bool retryCurrentWave)
        {
            _deathPopupActive = false;
            OnDeathPopupHide?.Invoke();

            if (_isBossWave)
            {
                _isBossWave = false;
                _bossFailedReturn = true;

                _bossAutoChallenge = false;
                OnBossAutoChallengeChanged?.Invoke(false);

                _loopMode = true;
                OnLoopModeChanged?.Invoke(true);

                _currentWave = 10;
                ulong baseStage = (ulong)_currentStage & 0xFFFFFFFFFFFF0000;
                _currentStage = (eStage)(baseStage + 10);

                FadeInAndStart();
                return;
            }

            if (retryCurrentWave)
            {
                _loopMode = false;
                OnLoopModeChanged?.Invoke(false);
                FadeInAndStart();
            }
            else
            {
                if (_currentWave > 1)
                {
                    _currentWave--;
                    _currentStage = (eStage)((ulong)_currentStage - 1);
                }

                _loopMode = true;
                OnLoopModeChanged?.Invoke(true);
                FadeInAndStart();
            }
        }

        // ══════════════════════════════════════
        //  반복 모드 / 보스 자동 도전
        // ══════════════════════════════════════

        public void DisableLoopMode()
        {
            _loopMode = false;
            OnLoopModeChanged?.Invoke(false);
        }

        public void SetBossAutoChallenge(bool enabled)
        {
            _bossAutoChallenge = enabled;
            OnBossAutoChallengeChanged?.Invoke(enabled);
        }

        // ══════════════════════════════════════
        //  페이드 유틸
        // ══════════════════════════════════════

        private void FadeAndStart()
        {
            var fade = CameraFade.Instance;
            if (fade != null)
            {
                fade.FadeOutIn(0.3f, 0.3f,
                    onDark: () =>
                    {
                        DespawnAllMonsters();
                        ReviveAllPlayers();
                        OnWaveChanged?.Invoke(_currentStageNumber, _currentWave, _isBossWave);
                        if (_isBossWave)
                        {
                            _bossTimer = _bossTimeLimit;
                            _bossTimerActive = true;
                            StageManager.Instance.StartStage((eStage)_bossStageValue);
                        }
                        else
                        {
                            StageManager.Instance.StartStage(_currentStage);
                        }
                    });
            }
            else
            {
                StartWave();
            }
        }

        private void FadeInAndStart()
        {
            var fade = CameraFade.Instance;
            DespawnAllMonsters();
            ReviveAllPlayers();

            Time.timeScale = 1f;
            OnWaveChanged?.Invoke(_currentStageNumber, _currentWave, _isBossWave);

            if (_isBossWave)
            {
                _bossTimer = _bossTimeLimit;
                _bossTimerActive = true;
                StageManager.Instance.StartStage((eStage)_bossStageValue);
            }
            else
            {
                StageManager.Instance.StartStage(_currentStage);
            }

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

        private void DespawnAllMonsters()
        {
            var monsters = FindObjectsByType<Scripts.Monster.Monster>(FindObjectsSortMode.None);
            foreach (var m in monsters)
            {
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                m.gameObject.SetActive(false);
                MonsterSpawner.Instance.ReleaseMonster(m.Type, m);
            }
        }

        private void ParseStage(eStage stage, out int stageNum, out int wave)
        {
            ulong val = (ulong)stage;
            wave = (int)(val & 0xFFFF);
            ulong stageRaw = (val & 0xFFFFFFFFFFFF0000);
            ulong stage1Base = (ulong)eStage.Stage1 & 0xFFFFFFFFFFFF0000;
            ulong diff = stageRaw - stage1Base;
            stageNum = (int)(diff / 0x10000) + 1;
        }
    }
}
