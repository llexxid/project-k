using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace KingdomIdle.UGUI
{
    [RequireComponent(typeof(Toggle))]
    public sealed class ToggleSwitchView : MonoBehaviour
    {
        public Image track;
        public RectTransform knob;
        private Toggle _toggle;
        private Coroutine _motion;
        private void OnEnable()
        {
            _toggle=GetComponent<Toggle>(); _toggle.onValueChanged.AddListener(Changed);
            Refresh();
        }
        private void OnDisable()
        {
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(Changed);
            if (_motion != null) StopCoroutine(_motion);
            _motion=null;
        }
        private void Changed(bool on)
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion=StartCoroutine(Animate(on));
        }
        private IEnumerator Animate(bool on)
        {
            float from=knob != null ? Mathf.InverseLerp(-34,34,knob.anchoredPosition.x) : 0;
            for(float t=0;t<1;t+=Time.unscaledDeltaTime/0.14f)
            { Apply(Mathf.Lerp(from,on?1:0,Mathf.SmoothStep(0,1,t))); yield return null; }
            Apply(on?1:0); _motion=null;
        }
        public void Refresh() { if (_toggle != null) Apply(_toggle.isOn?1:0); }
        private void Apply(float t)
        {
            if(knob != null) knob.anchoredPosition=new Vector2(Mathf.Lerp(-34,34,t),0);
            if(track != null) track.color=Color.Lerp(UguiTheme.RusticSurface,UguiTheme.Bronze,t);
        }
    }
}
