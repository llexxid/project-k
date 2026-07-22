using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 피격 데미지 텍스트 호출용 브릿지 (UITKDamageTextBridge 이식 — 시그니처 동일).
    /// 게임플레이 코드(Player/Monster/MageTower)가 호출한다.
    /// </summary>
    public static class DamageTextBridge
    {
        private static DamageTextManager _cached;

        public static void ShowOnTransform(Transform target, ulong amount)
        {
            if (target == null) return;
            ShowWorld(GetHeadWorldPos(target), amount);
        }

        public static void ShowOnTransform(Transform target, ulong amount, Color color)
        {
            if (target == null) return;
            ShowWorld(GetHeadWorldPos(target), amount, color);
        }

        public static void ShowWorld(Vector3 worldPos, ulong amount)
        {
            var mgr = EnsureManager();
            if (mgr == null) return;
            mgr.ShowWorldDamage(worldPos, amount);
        }

        public static void ShowWorld(Vector3 worldPos, ulong amount, Color color)
        {
            var mgr = EnsureManager();
            if (mgr == null) return;
            mgr.ShowWorldDamage(worldPos, amount, color);
        }

        private static DamageTextManager EnsureManager()
        {
            if (_cached != null) return _cached;

            // 1) UIManager 오브젝트에 붙여서 사용
            if (UIManager.Instance != null)
            {
                var go = UIManager.Instance.gameObject;
                _cached = go.GetComponent<DamageTextManager>();
                if (_cached == null)
                    _cached = go.AddComponent<DamageTextManager>();
                return _cached;
            }

            // 2) 씬에서 찾기
            _cached = Object.FindFirstObjectByType<DamageTextManager>();
            if (_cached != null) return _cached;

            // 3) 그래도 없으면 생성
            var obj = new GameObject("DamageTextManager");
            Object.DontDestroyOnLoad(obj);
            _cached = obj.AddComponent<DamageTextManager>();
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
