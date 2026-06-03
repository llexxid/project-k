using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Scripts.Core;
using UnityEngine;

namespace Scripts.Test
{
    public class GameTest : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Debug.Log("Duplication Loading Test");
                DuplicationLoadingTest().Forget();
            }
        }

        private async UniTaskVoid DuplicationLoadingTest()
        {
            GameManager.Instance.LoadAsyncScene(eSceneType.main);
            await UniTask.WaitForSeconds(0.1f);
            GameManager.Instance.LoadAsyncScene(eSceneType.main);
        }
    }
}