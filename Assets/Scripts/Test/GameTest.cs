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
    }
}