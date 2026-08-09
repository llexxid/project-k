using System;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Scripts.Core.Utils
{
    /// <summary>
    /// Addressables handle 상태를 읽기 쉽게 표현하는 확장 메서드 모음.
    /// 유효하지 않은 handle은 로딩할 대상이 없는 상태로 취급한다.
    /// </summary>
    public static class AddressableHandleExtensions
    {
        /// <summary>유효한 handle이 있고 아직 완료되지 않았으면 true를 반환한다.</summary>
        public static bool IsLoading<T>(this AsyncOperationHandle<T> handle)
        {
            return handle.IsValid() && !handle.IsDone;
        }
        
        /// <summary>handle이 없거나 이미 완료된 상태면 true를 반환한다.</summary>
        public static bool IsDoneOrEmpty<T>(this AsyncOperationHandle<T> handle)
        {
            return !handle.IsValid() || handle.IsDone;
        }
    }
}