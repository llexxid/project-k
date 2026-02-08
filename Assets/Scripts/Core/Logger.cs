using System.Diagnostics;
using UnityEngine;

// 핵심: Debug 이름 충돌 해결( System.Diagnostics.Debug vs UnityEngine.Debug )
using Debug = UnityEngine.Debug;

namespace Scripts.Core
{
    /// <summary>
    /// 프로젝트 공용 로거.
    /// - Log/Warning은 DEV_MODE 정의 시에만 출력
    /// - Error는 항상 출력
    /// </summary>
    public static class Logger
    {
        [Conditional("DEV_MODE")]
        public static void Log(string msg)
        {
            Debug.Log(msg);
        }

        [Conditional("DEV_MODE")]
        public static void LogWarning(string msg)
        {
            Debug.LogWarning(msg);
        }

        public static void LogError(string msg)
        {
            Debug.LogError(msg);

#if UNITY_EDITOR
            // 에디터에서만 멈춰서 디버깅하기 쉽게
            Debug.Break();
#endif
        }
    }

    /// <summary>
    /// 호환용 별칭(다른 스크립트가 CustomLogger를 쓰고 있어도 수정 없이 동작)
    /// - "로거 하나만" 유지: 내부 동작은 전부 Logger로 위임
    /// </summary>
    public static class CustomLogger
    {
        [Conditional("DEV_MODE")]
        public static void Log(string msg) => Logger.Log(msg);

        [Conditional("DEV_MODE")]
        public static void LogWarning(string msg) => Logger.LogWarning(msg);

        public static void LogError(string msg) => Logger.LogError(msg);
    }
}
