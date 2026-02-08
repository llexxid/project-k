using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

//1.추가적인 정보표현
//2. 로그 레벨에 따라서, 로그 관리.
//   일일히 다 주석처리 , 전처리 처리는 힘듦.

namespace Scripts.Core
{
    public static class CustomLogger
    {
        [Conditional("DEV_MODE")]
        public static void Log(string msg)
        {
            UnityEngine.Debug.LogFormat("[{0}] message : {1}", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), msg);
        }

        [Conditional("DEV_MODE")]
        public static void LogWarning(string msg)
        {
            UnityEngine.Debug.LogFormat("[{0}] message : {1}", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), msg);
        }

        public static void LogError(string msg)
        {
            UnityEngine.Debug.LogFormat("[{0}] message : {1}", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), msg);
            //에러는 그 자리에서 Break를 해서, 잡을 수 있도록 유도.
            UnityEngine.Debug.Break();
        }
    }
}

