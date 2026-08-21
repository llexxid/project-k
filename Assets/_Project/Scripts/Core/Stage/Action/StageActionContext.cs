using Core.Stage.Presentation;
using Scripts.Core;
using Scripts.Monster;
using Scripts.Monster.SO;

namespace Core.Stage.Action
{
    /// <summary>
    /// StageActionTask가 전역 Manager를 직접 조회하지 않도록 현재 세션과 실행 서비스를 제공한다.
    /// </summary>
    public sealed class StageActionContext
    {
        public StageDefinition Definition => Session.Definition;
        public StageSession Session { get; }
        public StageSpawnController SpawnController { get; }
        public MonsterSpawnLocationSO SpawnLocation { get; }
        public StageCameraDirector CameraDirector { get; }
        public bool HasPendingResult => Session.HasPendingResult;

        public StageActionContext(
            StageSession session,
            StageSpawnController spawnController,
            MonsterSpawnLocationSO spawnLocation,
            StageCameraDirector cameraDirector)
        {
            Session = session;
            SpawnController = spawnController;
            SpawnLocation = spawnLocation;
            CameraDirector = cameraDirector;
        }

        /// <summary>최초 전투 Task가 시작될 때 세션의 전투 상태와 외부 알림을 활성화한다.</summary>
        public void BeginBattle()
        {
            Session.BeginBattle();
        }

        /// <summary>전투 Task가 활성화된 동안에만 Rule과 반복 스폰을 갱신한다.</summary>
        public void TickBattle(StageActionTime time)
        {
            Session.TickRule(time.DeltaTime);
            if (!Session.HasPendingResult)
                SpawnController.Tick(time.DeltaTime);
        }

        /// <summary>초기 스폰 단계에서 생성해 둔 보스 몬스터를 연출 대상으로 제공한다.</summary>
        public bool TryGetBossMonster(out Monster monster)
        {
            return SpawnController.TryGetBossMonster(out monster);
        }

        /// <summary>Sequence 완료 또는 취소 시 반복 스폰을 중단한다.</summary>
        public void Stop()
        {
            SpawnController.Stop();
        }
    }
}
