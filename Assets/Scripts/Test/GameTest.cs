using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace Scripts.Test
{
    public class GameTest : MonoBehaviour
    {
        private StageManager manager;
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                StageManager.Instance.EnterGoldDungeon();
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                StageManager.Instance.ReturnToMainStage();
            }
        }

        [ContextMenu("Stage Test/Enter Gold Dungeon")]
        public void TestEnterGoldDungeon()
        {
            if (!TryGetStageManager()) return;
            Debug.Log("[GameTest] 골드던전 입장");
            manager.EnterGoldDungeon();
        }
        [ContextMenu("Stage Test/Enter Ruby Dungeon")]
        public void TestEnterRubyDungeon()
        {
            if (!TryGetStageManager()) return;
            Debug.Log("[GameTest] 루비던전 입장");
            manager.EnterRubyDungeon();
        }
        [ContextMenu("Stage Test/Return To MainStage")]
        public void TestReturnMain()
        {
            if (!TryGetStageManager()) return;
            Debug.Log("[GameTest] 메인스테이지 복귀");
            manager.ReturnToMainStage();
        }
        
        [ContextMenu("Stage Test/Continue Dungeon")]
        public void TestContinueDungeon()
        {
            if (!TryGetStageManager()) return;
            Debug.Log("[GameTest] 던전 다음난이도 입장시도");
            manager.ContinueDungeon();
        }
        
        [ContextMenu("Stage Test/Retry Dungeon")]
        public void TestRetryDungeon()
        {
            if (!TryGetStageManager()) return;
            Debug.Log("[GameTest] 던전 재시작");
            manager.RestartDungeon();
        }
        private bool TryGetStageManager()
        {
            if (StageManager.Instance == null)
                return false;
            if (manager != null) return true;
            manager = StageManager.Instance;
            return true;
        }
    }
}