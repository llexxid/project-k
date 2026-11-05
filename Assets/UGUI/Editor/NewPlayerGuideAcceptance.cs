using System;
using UnityEditor;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>이전 로컬 계정 저장으로 서버 튜토리얼을 검증했다고 보고하지 않는다.</summary>
    public static class NewPlayerGuideAcceptance
    {
        [MenuItem("KingdomIdle/Direction/Validate tutorial startup")]
        public static void Begin() => throw new InvalidOperationException("로컬 진행 기반 시작 검증은 중단되었습니다. KingdomIdle.Balance.ServerTutorialAcceptance.Run()으로 서버 계약을 검증하세요. 로그인·재설치 동작은 서버 배포 후 실제 계정으로 검증해야 합니다.");

        public static void BeginBatch() => Begin();
    }
}
