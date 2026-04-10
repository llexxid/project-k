using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 강화 시스템의 진입점 MonoBehaviour.
///
/// [씬 세팅]
///   빈 GameObject → SkillEnhanceManager 컴포넌트 추가
///   인스펙터에서 skillDatabase 필드에 SkillDatabase 에셋 연결
///
/// [키보드 입력 (테스트용)]
///   1 — DB의 모든 스킬 +1 레벨 강화
///   2 — DB의 모든 스킬 +10 레벨 강화
///   3 — DB의 모든 스킬 +100 레벨 강화
///   0 — 모든 스킬 강화 레벨 초기화
/// </summary>
public class SkillEnhanceManager : MonoBehaviour
{
    // ─────────────────────────────────────
    //  인스펙터 연결
    // ─────────────────────────────────────
    [Tooltip("SkillDatabase 에셋을 직접 연결하세요.")]
    [SerializeField] private SkillDatabase skillDatabase;

    // ─────────────────────────────────────
    //  런타임 (공개 — PlayerSkill에서 참조)
    // ─────────────────────────────────────
    public PlayerSkillRuntime Runtime { get; private set; }

    // ─────────────────────────────────────
    //  싱글턴
    // ─────────────────────────────────────
    public static SkillEnhanceManager Instance { get; private set; }

    // ─────────────────────────────────────
    //  생명주기
    // ─────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Runtime = new PlayerSkillRuntime();
        Runtime.Load();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) Enhance(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Enhance(10);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Enhance(100);
        if (Input.GetKeyDown(KeyCode.Alpha0)) ResetAll();
    }

    // ─────────────────────────────────────
    //  ContextMenu (인스펙터 우클릭)
    // ─────────────────────────────────────
    [ContextMenu("강화 +1")]   public void Enhance1()   => Enhance(1);
    [ContextMenu("강화 +10")]  public void Enhance10()  => Enhance(10);
    [ContextMenu("강화 +100")] public void Enhance100() => Enhance(100);

    // ─────────────────────────────────────
    //  강화 처리
    // ─────────────────────────────────────

    /// <summary>SkillDatabase에 등록된 모든 스킬을 amount 레벨 강화한다.</summary>
    private void Enhance(int amount)
    {
        if (skillDatabase == null)
        {
            Debug.LogWarning("[SkillEnhanceManager] SkillDatabase가 연결되지 않았습니다.");
            return;
        }

        List<SkillData> list = skillDatabase.skillDataList;
        if (list == null || list.Count == 0)
        {
            Debug.LogWarning("[SkillEnhanceManager] SkillDatabase에 스킬이 없습니다.");
            return;
        }

        foreach (var skill in list)
        {
            if (skill == null) continue;

            int before = Runtime.GetLevel(skill.skillName);
            Runtime.AddLevel(skill, amount);
            int after = Runtime.GetLevel(skill.skillName);

        }

        Runtime.Save();
    }

    /// <summary>모든 스킬 강화 레벨을 초기화한다. (테스트 전용)</summary>
    [ContextMenu("강화 초기화")]
    private void ResetAll()
    {
        Runtime.ResetAll();
        Runtime.Save();
    }

    /// <summary>특정 스킬 하나를 amount 레벨 강화한다. (향후 UI 연동용)</summary>
    public void EnhanceSingle(SkillData skill, int amount = 1)
    {
        if (skill == null) return;
        int before = Runtime.GetLevel(skill.skillName);
        Runtime.AddLevel(skill, amount);
        Runtime.Save();
    }
}
