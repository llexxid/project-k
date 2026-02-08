using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UI
{
    public class UIScreenMain : UIScreen
    {
        [Header("Bottom Tabs")]
        [SerializeField] private Button btnDevelopment;
        [SerializeField] private Button btnKingdomArmy;
        [SerializeField] private Button btnGacha;
        [SerializeField] private Button btnStore;
        [SerializeField] private Button btnDungeon;

        [Header("Behaviour")]
        [SerializeField] private bool clearPanelsBeforeOpen = true; // 탭은 '교체 느낌'으로 1개만 유지하고 싶으면 true 추천

        protected override void Awake()
        {
            base.Awake();

            Bind(btnDevelopment, () => OpenTabPanel(UIPanelId.Development));
            Bind(btnKingdomArmy, () => OpenTabPanel(UIPanelId.KingdomArmy));
            Bind(btnGacha, () => OpenTabPanel(UIPanelId.Gacha));
            Bind(btnStore, () => OpenTabPanel(UIPanelId.Store));
            Bind(btnDungeon, () => OpenTabPanel(UIPanelId.Dungeon));
        }

        private void OnDestroy()
        {
            Unbind(btnDevelopment);
            Unbind(btnKingdomArmy);
            Unbind(btnGacha);
            Unbind(btnStore);
            Unbind(btnDungeon);
        }

        private void OpenTabPanel(UIPanelId id)
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[UIScreenMain] UIManager.Instance is null.");
                return;
            }

            // 바텀 탭은 보통 "하나만 떠있게" 운영하는 게 UX가 깔끔해서,
            // 기본값은 기존 패널을 지우고 새 패널을 여는 방식으로 해둠.
            if (clearPanelsBeforeOpen)
            {
                UIManager.Instance.ClearPopups();
                UIManager.Instance.ClearPanels();
            }

            UIManager.Instance.PushPanel(id);
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void Unbind(Button button)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
        }
    }
}
