using Scripts.Users;
using UnityEngine;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 장비 패널 UI 외부 호출용 브릿지.
    /// 다른 팀원 코드에서 장비 UI를 건드릴 때 이 클래스만 사용한다.
    ///
    /// [사용 방법]
    ///   1. Player.Start()에서 Init(player, user) 호출 → 레퍼런스 연결
    ///   2. 장비 패널을 열 때 OpenPanel() 호출
    ///   3. 나머지(드롭 알림 등)는 이벤트를 통해 자동으로 갱신됨
    /// </summary>
    public static class UITKEquipmentPanelBridge
    {
        // ── Controller 접근 ───────────────────────────────────────────
        public static UITKEquipmentPanelController Ensure()
        {
            if (UITKEquipmentPanelController.Instance != null)
                return UITKEquipmentPanelController.Instance;

            if (UITKUIManager.Instance != null)
            {
                var go = UITKUIManager.Instance.gameObject;
                var c  = go.GetComponent<UITKEquipmentPanelController>();
                if (c == null) c = go.AddComponent<UITKEquipmentPanelController>();
                return c;
            }

            var obj = new GameObject("UITKEquipmentPanelController");
            Object.DontDestroyOnLoad(obj);
            return obj.AddComponent<UITKEquipmentPanelController>();
        }

        // ── Player.Start()에서 호출 ───────────────────────────────────
        /// <summary>
        /// Player와 User 레퍼런스를 Controller에 넘기고 이벤트 구독을 시작한다.
        /// Player.Start()에서 반드시 호출해야 한다.
        /// </summary>
        public static void Init(Player player, User user)
        {
            //Ensure()?.Init(player, user);
        }

        // ── 외부에서 패널 열기/닫기 ──────────────────────────────────
        public static void OpenPanel()  => Ensure()?.OpenPanel();
        public static void ClosePanel() => Ensure()?.ClosePanel();
    }
}
