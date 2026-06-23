using Cysharp.Threading.Tasks;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using Scripts.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Scripts.Core
{
    
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

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

            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.HandleAllPlayersDead();
                return;
            }

            Time.timeScale = 0f;
        }
        #endregion

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
        private void Init(){}
        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        
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
            eStage curUserStage = UserManager.Instance.GetUserCurrentStage();

            // WaveManager가 있으면 웨이브 흐름을 위임
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.BeginFromStage(curUserStage);
            }
            else
            {
                StageManager.Instance.SpawnStageMonster(curUserStage);
            }
        }
    }
}