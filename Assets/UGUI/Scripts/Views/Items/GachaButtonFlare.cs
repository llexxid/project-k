using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Short, unscaled blue wisps. No idle updates or runtime hierarchy construction.</summary>
    public sealed class GachaButtonFlare : MonoBehaviour
    {
        public Image[] wisps;
        public Sprite[] frames;
        private Action _cancel;
        private Coroutine _routine;

        public void Play(Action complete, Action cancel)
        {
            if (_routine != null) return;
            if (GamePresentationSettings.LowSpec || frames == null || frames.Length == 0)
            { complete?.Invoke(); return; }
            _cancel = cancel;
            _routine = StartCoroutine(Animate(complete));
        }

        private IEnumerator Animate(Action complete)
        {
            foreach (var wisp in wisps)
            {
                wisp.enabled = true;
                UITween.SetFlashRingBaseColor(wisp, new Color(.48f, .68f, .86f, .38f));
                UITween.FlashRing(wisp, .48f, 1.35f);
            }
            int previous = -1;
            for (float time = 0; time < .48f; time += Time.unscaledDeltaTime)
            {
                int frame = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(time / .48f * frames.Length));
                if (frame != previous)
                {
                    for (int i = 0; i < wisps.Length; i++) wisps[i].sprite = frames[(frame + i * 2) % frames.Length];
                    previous = frame;
                }
                yield return null;
            }
            _routine = null; _cancel = null; ResetVisuals();
            complete?.Invoke();
        }

        private void OnEnable() => ResetVisuals();
        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            var cancel = _cancel; _cancel = null;
            ResetVisuals(); cancel?.Invoke();
        }
        private void ResetVisuals()
        {
            if (wisps == null) return;
            foreach (var wisp in wisps)
                if (wisp != null) { wisp.enabled = false; wisp.color = Color.clear; wisp.rectTransform.localScale = Vector3.one; }
        }
    }
}
