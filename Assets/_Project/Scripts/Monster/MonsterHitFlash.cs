using System;
using System.Collections;
using UnityEngine;


namespace Scripts.Monster
{
    [DisallowMultipleComponent]
    public class MonsterHitFlash : MonoBehaviour
    {
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        [SerializeField] private SpriteRenderer[] _targets;

        [Header("Flash Timing")] [SerializeField, Min(0f)]
        private float _holdDuration = 0.04f;

        [SerializeField, Min(0f)] private float _fadeDuration = 0.06f;

        private MaterialPropertyBlock _propertyBlock;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();

            if (_targets == null || _targets.Length == 0)
            {
                _targets = GetComponentsInChildren<SpriteRenderer>(true);
            }


        }

        private void OnDisable()
        {
            ResetFlash();
        }

        /// <summary>
        /// 피격 점멸을 시작한다.
        /// </summary>
        public void Play()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            // 연속으로 피격되면 처음부터 다시 점멸한다.
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(FlashRoutine());
        }

        /// <summary>
        /// 점멸을 중단하고 원래 색상으로 되돌린다.
        /// </summary>
        public void ResetFlash()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            if (_propertyBlock != null)
            {
                SetFlashAmount(0f);
            }
        }

        private IEnumerator FlashRoutine()
        {
            // 피격 순간 완전히 흰색
            SetFlashAmount(1f);

            float elapsed = 0f;

            // 잠깐 흰색 유지
            while (elapsed < _holdDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 즉시 원상복구하는 설정
            if (_fadeDuration <= 0f)
            {
                SetFlashAmount(0f);
                _flashCoroutine = null;
                yield break;
            }

            elapsed = 0f;

            // 흰색에서 원래 색으로 부드럽게 복귀
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;

                float ratio = Mathf.Clamp01(
                    elapsed / _fadeDuration);

                SetFlashAmount(1f - ratio);

                yield return null;
            }

            SetFlashAmount(0f);
            _flashCoroutine = null;
        }

        private void SetFlashAmount(float amount)
        {
            amount = Mathf.Clamp01(amount);

            foreach (SpriteRenderer target in _targets)
            {
                if (target == null)
                {
                    continue;
                }

                // SpriteRenderer에 이미 들어 있는 값을 보존한다.
                target.GetPropertyBlock(_propertyBlock);

                _propertyBlock.SetFloat(
                    FlashAmountId,
                    amount);

                target.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}