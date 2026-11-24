using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class MageAwakeningView : MonoBehaviour
    {
        public CanvasGroup veil;
        public Image emblem, glow;
        public Image[] stars;
        public TMP_Text caption;
        private MageTowerSkillSO skill;
        private int previous, awarded;
        private bool bloom, revealed;
        private float opened, confirmed;
        private long account;
        private static readonly Color Lit = new Color(1f, .79f, .32f);
        private static readonly Color Unlit = new Color(.32f, .31f, .34f);

        public void Begin(MageTowerSkillSO selected, int level)
        {
            skill = selected;
            account = LocalProgression.AccountGeneration;
            opened = Time.unscaledTime;
            previous = level;
            awarded = -1;
            revealed = bloom = false;
            emblem.sprite = selected.DisplayIcon(false);
            emblem.rectTransform.anchoredPosition = new Vector2(0, 68);
            emblem.transform.localScale = Vector3.one;
            caption.text = selected.DisplayName(false) + " 각성 중";
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].color = i < previous ? Lit : Unlit;
                stars[i].transform.localScale = Vector3.one;
            }
            glow.color = new Color(1, .69f, .28f, 0);
            veil.alpha = 0;
            gameObject.SetActive(true);
        }

        public void Confirm(int level, bool enabled)
        {
            if (!gameObject.activeSelf) return;
            awarded = Mathf.Clamp(level, 0, stars.Length);
            bloom = enabled && previous < stars.Length && awarded == stars.Length;
            confirmed = Mathf.Max(Time.unscaledTime, opened + .28f);
        }

        public void Close() => gameObject.SetActive(false);

        private void Update()
        {
            if (account != LocalProgression.AccountGeneration) { Close(); return; }
            float now = Time.unscaledTime;
            veil.alpha = Mathf.Clamp01((now - opened) / .18f);
            float t = now - confirmed;
            if (awarded < 0 || t < 0)
            {
                glow.color = new Color(1, .69f, .28f, .10f + .05f * Mathf.Sin((now - opened) * 5f));
                return;
            }
            int last = awarded - 1;
            float progress = Mathf.Clamp01(t / .33f);
            if (last >= previous)
            {
                stars[last].color = Color.Lerp(Unlit, Lit, progress);
                stars[last].transform.localScale = Vector3.one * (1 + .34f * Mathf.Sin(progress * Mathf.PI));
            }
            float peak = bloom ? .64f : .2f;
            float light = Mathf.Clamp01(1f - Mathf.Abs(t - peak) / .4f);
            glow.color = new Color(1, .75f, .36f, .09f + .34f * light);
            if (bloom && !GamePresentationSettings.LowSpec)
                emblem.transform.localScale = Vector3.one * (1f + .08f * light);
            if (bloom && t < peak && GamePresentationSettings.ScreenShake && !GamePresentationSettings.LowSpec)
                emblem.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 68f) * light * 5f, 68);
            if (t >= peak && !revealed)
            {
                revealed = true;
                emblem.rectTransform.anchoredPosition = new Vector2(0, 68);
                emblem.sprite = skill.DisplayIcon(bloom);
                caption.text = bloom ? skill.DisplayName(true) + " · 개화" : skill.DisplayName(false) + " · " + awarded + "각성";
            }
            float hold = bloom ? 1.65f : .95f;
            if (t >= hold) veil.alpha = 1f - Mathf.Clamp01((t - hold) / .2f);
            if (t >= hold + .2f) Close();
        }
    }
}
