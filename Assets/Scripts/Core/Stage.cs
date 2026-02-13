using Scripts.Core;
using Scripts.Core.SO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Scripts.Core.SO.StageMetaDataSO;

namespace Scripts.Core
{
    using Monster = Scripts.Monster.Monster;
    public class Stage
    {
        private eStage _currentStage;
        private int totalEnemy;
        public Stage(eStage current)
        {
            _currentStage = current;
            totalEnemy = 0;
        }
        //Todo : SpawnLocation 정해야함.
        public void OnStageEnter()
        {
            //MonsterSpawn시작
            List<StageInfo_v> needStageInfo = StageManager.Instance.GetStageMonsterInfo(_currentStage);

            for (int i = 0; i < needStageInfo.Count; i++)
            {
                totalEnemy += needStageInfo[i]._count;
                //Spawn하기 
                MonsterSpawner.Instance.SpawnMonster(needStageInfo[i]._type, Vector3.zero, Quaternion.identity, out Monster mon);

                //Todo : Monster에게 Stage주입해주기
            }
        }

        public void OnMonsterDead()
        {
            totalEnemy--;
            if (totalEnemy <= 0)
            {
                OnStageClear();
            }
        }

        //스테이지 클리어 -> 다음 스테이지로 진행
        private void OnStageClear()
        {
            //eStage의 하위16비트는 wave정보
            //eStage의 상위16비트는 stage정보
            int stage = (int)_currentStage & (int)CONSTANT_VALUE.StageMask;
            int wave = (int)_currentStage & (int)CONSTANT_VALUE.WaveMask;

            if (wave == (int)CONSTANT_VALUE.WAVE_END)
            {
                //다음 스테이지로 전환
                stage = stage + (1 << 16);
                wave = 1;
                _currentStage = (eStage)(stage | wave);
                // 앞쪽 스테이지 전환시에는 Load할게 필요함.
                // Todo : StageLoad기능 만들어야함.
                //OnEnterStage실행
                OnStageClear();
            }
            else
            {
                //다음 wave로 진행
                _currentStage = (eStage)((int)_currentStage++);
                OnStageClear();
            }
        }
    }

}
