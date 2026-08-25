using Core.Stage.Presentation;
using Scripts.Monster;
using UnityEngine;

namespace Core.Stage.Action.Tasks
{
    /// <summary>
    /// 초기 보스 스폰 이후 전투를 잠시 멈추고 보스 입장 카메라 연출이 끝나기를 기다린다.
    /// 카메라 구성이나 보스 대상이 없으면 스테이지가 멈추지 않도록 즉시 완료한다.
    /// </summary>
    public sealed class BossIntroActionTask : StageActionTask
    {
        private StageCameraDirector _cameraDirector;
        private float _previousTimeScale;
        private bool _ownsTimeScalePause;

        /// <summary>보스와 카메라 제어기를 확인한 뒤 전투 시간 정지와 연출을 시작한다.</summary>
        protected override void OnStart(StageActionContext context)
        {
            _cameraDirector = context.CameraDirector;
            if (_cameraDirector == null)
            {
                Debug.LogWarning("[BossIntroActionTask] StageCameraDirector가 없어 보스 입장 연출을 건너뜁니다.");
                Complete();
                return;
            }

            if (!context.TryGetBossMonster(out Monster bossMonster))
            {
                Debug.LogWarning("[BossIntroActionTask] 스폰된 보스를 찾지 못해 보스 입장 연출을 건너뜁니다.");
                Complete();
                return;
            }

            // 몬스터 AI와 플레이어 전투는 멈추되 Cinemachine은 unscaled time으로 계속 움직인다.
            _previousTimeScale = Time.timeScale;
            _ownsTimeScalePause = true;
            Time.timeScale = 0f;

            if (!_cameraDirector.TryPlayBossIntro(bossMonster.transform, HandleIntroCompleted))
            {
                // Inspector 참조 누락 같은 구성 오류가 있어도 즉시 복구하고 전투를 계속한다.
                RestoreTimeScale();
                Complete();
            }
        }

        /// <summary>카메라가 게임플레이 시점으로 돌아오면 현재 Task를 완료한다.</summary>
        private void HandleIntroCompleted()
        {
            Complete();
        }

        /// <summary>정상 완료 시 일시정지를 해제한 뒤 다음 전투 Task로 넘긴다.</summary>
        protected override void OnEnd(StageActionContext context)
        {
            RestoreTimeScale();
        }

        /// <summary>스테이지 전환이나 재시작 시 카메라 연출과 일시정지를 함께 취소한다.</summary>
        protected override void OnCancel(StageActionContext context)
        {
            _cameraDirector?.CancelBossIntro();
            RestoreTimeScale();
        }

        /// <summary>이 Task가 변경한 Time.timeScale만 시작 전 값으로 되돌린다.</summary>
        private void RestoreTimeScale()
        {
            if (!_ownsTimeScalePause)
                return;

            Time.timeScale = _previousTimeScale;
            _ownsTimeScalePause = false;
        }
    }
}
