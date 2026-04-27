using UnityEngine;

namespace KingdomIdle.UIToolkit
{
    public static class UITKPartyHudBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsure()
        {
            Ensure();
        }

        public static UITKPartyHudController Ensure()
        {
            if (UITKPartyHudController.Instance != null)
                return UITKPartyHudController.Instance;

            if (UITKUIManager.Instance != null)
            {
                var go = UITKUIManager.Instance.gameObject;
                var c = go.GetComponent<UITKPartyHudController>();
                if (c == null) c = go.AddComponent<UITKPartyHudController>();
                return c;
            }

            var obj = new GameObject("UITKPartyHudController");
            Object.DontDestroyOnLoad(obj);
            return obj.AddComponent<UITKPartyHudController>();
        }

        public static void SetMemberHealth(int memberIndex, float current, float max)
        {
            Ensure()?.SetMemberHealth(memberIndex, current, max);
        }

        public static void SetMemberSkillCount(int memberIndex, int count)
        {
            Ensure()?.SetMemberSkillCount(memberIndex, count);
        }

        public static void SetPortraitSprite(int memberIndex, Sprite sprite)
        {
            Ensure()?.SetPortraitSprite(memberIndex, sprite);
        }
    }
}
