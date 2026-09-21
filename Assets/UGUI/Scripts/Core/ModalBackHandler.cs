using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>Closes the visible top modal according to its actual UI hierarchy.</summary>
    public sealed class ModalBackHandler : MonoBehaviour
    {
        private static readonly List<ModalBackHandler> Open = new();
        private Action _close;

        /// <summary>패널 스택 밖에 열린 모달도 전투 시간에서 제외할 수 있도록 활성 여부만 조회한다.</summary>
        public static bool HasOpenModal
        {
            get
            {
                // 읽기 중 등록 목록을 정리하거나 닫기 콜백을 실행하지 않는다. 생명주기가 목록을 관리한다.
                for (int i = 0; i < Open.Count; i++)
                {
                    ModalBackHandler candidate = Open[i];
                    if (candidate != null && candidate.isActiveAndEnabled && candidate._close != null) return true;
                }
                return false;
            }
        }

        public static void Bind(GameObject owner, Action close)
        {
            var handler = owner.GetComponent<ModalBackHandler>() ?? owner.AddComponent<ModalBackHandler>();
            handler._close = close;
            if (handler.isActiveAndEnabled && !Open.Contains(handler)) Open.Add(handler);
        }

        /// <summary>안내가 소유한 상세/결과 창을 제외하고 외부 모달 존재를 조회한다. 창을 닫거나 등록 목록을 변경하지 않는다.</summary>
        internal static bool HasUnexpectedModal(Func<GameObject, bool> expected)
        {
            foreach (var modal in Open)
                if (modal != null && modal.isActiveAndEnabled && modal._close != null && !expected(modal.gameObject)) return true;
            return false;
        }
        private void OnEnable() { if (!Open.Contains(this)) Open.Add(this); }
        private void OnDisable() => Open.Remove(this);
        private void OnDestroy() => Open.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Open.Clear();

        public static bool TryCloseTop()
        {
            ModalBackHandler top = null;
            for (int i = Open.Count - 1; i >= 0; i--)
            {
                var candidate = Open[i];
                if (candidate == null || !candidate.isActiveAndEnabled) { Open.RemoveAt(i); continue; }
                if (candidate._close != null && (top == null || DrawsAfter(candidate.transform, top.transform))) top = candidate;
            }
            if (top == null) return false;
            top._close();
            return true;
        }
        private static bool DrawsAfter(Transform a, Transform b)
        {
            var left = new List<Transform>();
            var right = new List<Transform>();
            for (var t = a; t != null; t = t.parent) left.Add(t);
            for (var t = b; t != null; t = t.parent) right.Add(t);
            int i = left.Count - 1, j = right.Count - 1;
            while (i >= 0 && j >= 0 && left[i] == right[j]) { i--; j--; }
            return i >= 0 && (j < 0 || left[i].GetSiblingIndex() > right[j].GetSiblingIndex());
        }
    }
}
