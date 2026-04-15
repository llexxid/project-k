using UnityEngine;

/// <summary>
/// EnergyPulse VFX 프리팹에 부착.
/// 활성화 시 애니메이션 재생 → 종료 후 자동 비활성화.
/// </summary>
public class EnergyPulseVFX : MonoBehaviour
{
    private Animator _animator;
    private float _deactivateTime = -1f;

    public void Init()
    {
        _animator = GetComponent<Animator>();
    }

    public void Play(Vector3 position)
    {
        transform.position = position;
        transform.localScale = Vector3.one * 3f;
        gameObject.SetActive(true);

        float length = 0.5f;
        if (_animator != null)
        {
            _animator.Play("EnergyPulse", 0, 0f);

            if (_animator.runtimeAnimatorController != null)
            {
                foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                {
                    length = clip.length;
                    break;
                }
            }
        }

        _deactivateTime = Time.time + length;
    }

    private void Update()
    {
        if (_deactivateTime > 0f && Time.time >= _deactivateTime)
        {
            _deactivateTime = -1f;
            gameObject.SetActive(false);
        }
    }
}
