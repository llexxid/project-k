using UnityEngine;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 피격 데미지 텍스트 호출용 브릿지.
    /// </summary>
    public static class UITKDamageTextBridge
    {
        private static UITKDamageTextManager _cached;

        public static void ShowOnTransform(Transform target, ulong amount)
        {
            if (target == null) return;
            ShowWorld(GetHeadWorldPos(target), amount);
        }

        public static void ShowWorld(Vector3 worldPos, ulong amount)
        {
            var mgr = EnsureManager();
            if (mgr == null) return;
            mgr.ShowWorldDamage(worldPos, amount);
        }

        private static UITKDamageTextManager EnsureManager()
        {
            if (_cached != null) return _cached;

            // 1) UITKUIManager 오브젝트에 붙여서 사용
            if (UITKUIManager.Instance != null)
            {
                var go = UITKUIManager.Instance.gameObject;
                _cached = go.GetComponent<UITKDamageTextManager>();
                if (_cached == null)
                    _cached = go.AddComponent<UITKDamageTextManager>();
                return _cached;
            }

            // 2) 씬에서 찾기
            _cached = Object.FindFirstObjectByType<UITKDamageTextManager>();
            if (_cached != null) return _cached;

            // 3) 그래도 없으면 생성
            var obj = new GameObject("UITKDamageTextManager");
            Object.DontDestroyOnLoad(obj);
            _cached = obj.AddComponent<UITKDamageTextManager>();
            return _cached;
        }

        private static Vector3 GetHeadWorldPos(Transform t)
        {
            // Renderer 기준으로 머리 위치 추정(없으면 단순 Y 오프셋)
            var r = t.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                var b = r.bounds;
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
            return t.position + Vector3.up * 1.2f;
        }
    }
}