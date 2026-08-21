using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Core.Stage.Presentation
{
    /// <summary>
    /// 씬에 배치된 Cinemachine 카메라들의 우선순위와 추적 대상을 제어한다.
    /// 스테이지 진행 규칙은 알지 않고, 요청받은 보스 입장 카메라 연출만 재생한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineBrain))]
    public sealed class StageCameraDirector : MonoBehaviour
    {
        /// <summary>현재 스테이지 씬에서 사용 중인 카메라 연출 제어기다.</summary>
        public static StageCameraDirector Instance { get; private set; }

        /// <summary>필수 Cinemachine 참조가 모두 연결되었는지 여부다.</summary>
        public bool IsConfigured =>
            _brain != null &&
            _gameplayCamera != null &&
            _bossIntroCamera != null;

        [Header("Cinemachine References")]
        [Tooltip("실제 Main Camera에 부착된 Cinemachine Brain")]
        [SerializeField] private CinemachineBrain _brain;

        [Tooltip("평상시 전투 화면을 담당하는 CM_GamePlay")]
        [SerializeField] private CinemachineCamera _gameplayCamera;

        [Tooltip("보스 입장 연출을 담당하는 CM_BossIntro")]
        [SerializeField] private CinemachineCamera _bossIntroCamera;

        [Header("Boss Intro Timing")]
        [Tooltip("게임플레이 카메라와 보스 카메라 사이를 이동하는 시간")]
        [Min(0f)]
        [SerializeField] private float _blendDuration = 0.25f;

        [Tooltip("보스에게 도착한 뒤 보스를 보여 주는 시간")]
        [Min(0f)]
        [SerializeField] private float _bossHoldDuration = 1f;

        [Header("Priority While Playing")]
        [Tooltip("보스 연출 중 CM_GamePlay에 임시로 적용할 우선순위")]
        [SerializeField] private int _gameplayPriority = 10;

        [Tooltip("보스 연출 중 CM_BossIntro에 임시로 적용할 우선순위")]
        [SerializeField] private int _bossIntroPriority = 100;

        // 연출이 끝나거나 취소될 때 사용자가 Inspector에서 설정한 원래 값을 되돌리기 위한 캐시다.
        private bool _originalIgnoreTimeScale;
        private CinemachineBlendDefinition _originalDefaultBlend;
        private int _originalGameplayPriority;
        private int _originalBossIntroPriority;

        private Coroutine _bossIntroRoutine;
        private System.Action _onBossIntroCompleted;

        private void Reset()
        {
            // 이 컴포넌트는 Main Camera에 붙이므로 같은 오브젝트의 Brain을 자동으로 찾아 준다.
            _brain = GetComponent<CinemachineBrain>();
        }

        private void Awake()
        {
            // 씬 참조를 StageActionContext에 전달할 수 있도록 현재 씬의 Director를 등록한다.
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[StageCameraDirector] 씬에 StageCameraDirector가 둘 이상 존재합니다.");
                enabled = false;
                return;
            }

            Instance = this;

            // Reset이 호출되지 않는 기존 오브젝트에 붙였을 때도 Brain을 안전하게 보완한다.
            if (_brain == null)
                _brain = GetComponent<CinemachineBrain>();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            // 씬이 내려가는 중 연출 코루틴과 정적 씬 참조가 남지 않도록 정리한다.
            CancelBossIntro();
            Instance = null;
        }

        /// <summary>
        /// 보스 카메라의 추적 대상을 지정하고 왕복 블렌딩 연출을 시작한다.
        /// 연출을 시작할 수 없으면 false를 반환하므로 호출자는 전투를 계속 진행할 수 있다.
        /// </summary>
        public bool TryPlayBossIntro(Transform bossTarget, System.Action onCompleted)
        {
            if (!IsConfigured)
            {
                Debug.LogWarning("[StageCameraDirector] Cinemachine 참조가 연결되지 않아 보스 입장 연출을 건너뜁니다.");
                return false;
            }

            if (bossTarget == null)
            {
                Debug.LogWarning("[StageCameraDirector] 보스 Transform이 없어 보스 입장 연출을 건너뜁니다.");
                return false;
            }

            // 이전 요청이 남아 있다면 먼저 원래 카메라 상태로 복구한 뒤 새 요청을 시작한다.
            CancelBossIntro();
            CaptureOriginalState();

            _onBossIntroCompleted = onCompleted;

            // 풀에서 매번 다른 보스가 나오므로 연출 시점에 현재 보스를 Tracking Target으로 주입한다.
            _bossIntroCamera.Follow = bossTarget;
            _bossIntroCamera.PreviousStateIsValid = false;

            // 전투가 Time.timeScale = 0으로 멈춰도 카메라는 unscaled time으로 블렌딩해야 한다.
            _brain.IgnoreTimeScale = true;
            CinemachineBlendDefinition introBlend = _brain.DefaultBlend;
            introBlend.Time = _blendDuration;
            _brain.DefaultBlend = introBlend;

            // Brain은 활성/비활성보다 높은 Priority의 카메라를 선택해 자연스럽게 블렌딩한다.
            _gameplayCamera.Priority = _gameplayPriority;
            _bossIntroCamera.Priority = Mathf.Max(_bossIntroPriority, _gameplayPriority + 1);

            _bossIntroRoutine = StartCoroutine(PlayBossIntroRoutine());
            return true;
        }

        /// <summary>
        /// 진행 중인 보스 입장 연출을 중단하고 시작 전 Cinemachine 설정으로 복구한다.
        /// StageActionTask가 취소될 때 완료 콜백은 호출하지 않는다.
        /// </summary>
        public void CancelBossIntro()
        {
            if (_bossIntroRoutine == null)
                return;

            StopCoroutine(_bossIntroRoutine);
            _bossIntroRoutine = null;
            _onBossIntroCompleted = null;
            RestoreOriginalState();
        }

        /// <summary>보스에게 이동하고, 잠시 보여 준 뒤, 게임플레이 카메라로 돌아온다.</summary>
        private IEnumerator PlayBossIntroRoutine()
        {
            // Time.timeScale과 무관한 시간으로 보스 카메라까지 이동한다.
            yield return WaitForUnscaledSeconds(_blendDuration);

            // 카메라가 보스에게 도착한 상태를 약 1초 동안 유지한다.
            yield return WaitForUnscaledSeconds(_bossHoldDuration);

            // 원래 Priority를 복구하면 Brain이 다시 CM_GamePlay를 선택한다.
            _gameplayCamera.Priority = _originalGameplayPriority;
            _bossIntroCamera.Priority = _originalBossIntroPriority;

            // 게임플레이 카메라로 돌아오는 블렌딩이 끝날 때까지 전투 시작을 보류한다.
            yield return WaitForUnscaledSeconds(_blendDuration);

            System.Action completed = _onBossIntroCompleted;
            _bossIntroRoutine = null;
            _onBossIntroCompleted = null;
            RestoreOriginalState();

            // 상태 복구를 먼저 끝낸 뒤 Task를 완료해 다음 전투 Task가 안전하게 시작되게 한다.
            completed?.Invoke();
        }

        /// <summary>현재 Brain과 카메라 설정을 연출 종료 후 복구할 수 있도록 저장한다.</summary>
        private void CaptureOriginalState()
        {
            _originalIgnoreTimeScale = _brain.IgnoreTimeScale;
            _originalDefaultBlend = _brain.DefaultBlend;
            _originalGameplayPriority = _gameplayCamera.Priority;
            _originalBossIntroPriority = _bossIntroCamera.Priority;
        }

        /// <summary>보스 추적 대상을 해제하고 사용자가 설정한 원래 Cinemachine 상태로 되돌린다.</summary>
        private void RestoreOriginalState()
        {
            if (!IsConfigured)
                return;

            _gameplayCamera.Priority = _originalGameplayPriority;
            _bossIntroCamera.Priority = _originalBossIntroPriority;
            _bossIntroCamera.Follow = null;
            _bossIntroCamera.PreviousStateIsValid = false;
            _brain.DefaultBlend = _originalDefaultBlend;
            _brain.IgnoreTimeScale = _originalIgnoreTimeScale;
        }

        /// <summary>Time.timeScale이 0이어도 경과하는 대기 시간을 제공한다.</summary>
        private static IEnumerator WaitForUnscaledSeconds(float duration)
        {
            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
