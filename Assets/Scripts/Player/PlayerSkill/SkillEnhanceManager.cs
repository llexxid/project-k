using UnityEngine;

/// <summary>
/// 스킬 강화 시스템 — 스펙아웃됨.
/// 씬에 배치된 GameObject 참조 유지를 위해 빈 컴포넌트로 남겨둔다.
/// </summary>
public class SkillEnhanceManager : MonoBehaviour
{
    public static SkillEnhanceManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
}
