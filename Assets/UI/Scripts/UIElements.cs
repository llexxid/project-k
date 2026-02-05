using System;
using UnityEngine;

namespace KingdomIdle.UI
{
    public abstract class UIElement : MonoBehaviour
    {
        [SerializeField] private bool startsVisible = true;

        protected CanvasGroup CanvasGroup { get; private set; }

        protected virtual void Awake()
        {
            EnsureCanvasGroup();
            SetVisible(startsVisible, instant: true);
        }

        private void EnsureCanvasGroup()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            if (CanvasGroup == null)
                CanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public virtual void SetVisible(bool visible, bool instant)
        {
            gameObject.SetActive(true);

            CanvasGroup.alpha = visible ? 1f : 0f;
            CanvasGroup.interactable = visible;
            CanvasGroup.blocksRaycasts = visible;

            if (!visible)
                gameObject.SetActive(false);
        }

        public virtual void SetInteractable(bool interactable)
        {
            if (CanvasGroup == null) EnsureCanvasGroup();
            CanvasGroup.interactable = interactable;
            CanvasGroup.blocksRaycasts = interactable;
        }

        /// <summary>
        /// 뒤로가기 요청이 UI에서 자체 처리되면 true 반환(=매니저가 Pop하지 않음)
        /// </summary>
        public virtual bool HandleBackRequested() => false;
    }

    // 루트 화면(교체)
    public abstract class UIScreen : UIElement
    {
        public virtual void OnEnter(object payload) { }
        public virtual void OnExit() { }
    }

    // 기능 패널(스택)
    public abstract class UIPanel : UIElement
    {
        public virtual void OnPushed(object payload) { }
        public virtual void OnPopped() { }

        public virtual void OnCovered() { }   // 위에 다른 패널/팝업이 올라왔을 때
        public virtual void OnRevealed() { }  // 다시 top이 되었을 때
    }

    // 팝업(스택)
    public abstract class UIPopup : UIElement
    {
        public virtual void OnPushed(object payload) { }
        public virtual void OnPopped() { }

        public virtual void OnCovered() { }
        public virtual void OnRevealed() { }
    }

    // 시스템 오버레이(로딩/토스트 등)
    public abstract class UIOverlay : UIElement
    {
        public virtual void OnShow(object payload) { }
        public virtual void OnHide() { }
    }

    // 선택: 오버레이 기능 인터페이스(있으면 UIManager가 자동 호출)
    public interface ILoadingOverlay
    {
        void SetMessage(string message);
    }

    public interface IToastOverlay
    {
        void ShowToast(string message, float durationSeconds);
    }

    internal static class UIReflectionTextUtil
    {
        public static void TrySetText(Component target, string value)
        {
            if (target == null) return;

            var type = target.GetType();
            var prop = type.GetProperty("text");
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
            {
                prop.SetValue(target, value);
                return;
            }

            var field = type.GetField("text");
            if (field != null && field.FieldType == typeof(string))
            {
                field.SetValue(target, value);
            }
        }
    }
}
