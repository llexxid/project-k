using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Scripts.Core;
using UnityEngine;

namespace Scripts.Test
{
    public class GameTest : MonoBehaviour
    {
        #if UNITY_EDITOR
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                StageManager.Instance.ClearWave();
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                
            }
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Debug.Log("Duplication Loading Test");
                DuplicationLoadingTest().Forget();
            }
        }

        private async UniTaskVoid DuplicationLoadingTest()
        {
            LoadManager.Instance.LoadAsyncScene(eSceneType.main);
            await UniTask.WaitForSeconds(0.1f);
            LoadManager.Instance.LoadAsyncScene(eSceneType.main);
        }
        #endif
    }
}