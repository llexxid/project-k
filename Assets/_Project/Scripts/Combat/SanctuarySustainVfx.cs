using UnityEngine;

namespace KingdomIdle.Combat
{
    /// <summary>The emblem rises once, holds while the field pulses, then descends once.</summary>
    public sealed class SanctuarySustainVfx : MonoBehaviour
    {
        public Transform crest;
        PooledSpellVfx _life;
        Vector3 _rest;
        float _age;
        int _generation = -1;
        void Awake() { _life = GetComponent<PooledSpellVfx>(); _rest = crest.localPosition; }
        void Update()
        {
            if (_generation != _life.SpawnGen) { _generation = _life.SpawnGen; _age = 0; }
            _age += Time.deltaTime;
            float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_age / .32f));
            float fall = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_life.lifetime - _age) / .32f));
            crest.localPosition = _rest + Vector3.down * (.45f * (1 - Mathf.Min(rise, fall)));
        }
    }
}
