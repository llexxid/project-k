using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    public SkillObjectPool pool;
    public SkillDatabase skillDatabase;

    // 현재 직업에서 사용 가능한 스킬 목록
    private List<SkillData> _currentSkills = new List<SkillData>();

    /// <summary>
    /// 스킬 이펙트/오브젝트를 활성화한다.
    /// 쿨타임 체크·갱신은 PlayerAttack이 전담하므로 여기서는 처리하지 않는다.
    /// </summary>
    public void ActivateSkill(string skillName)
    {
        SkillData data = skillDatabase.GetSkill(skillName);
        if (data == null)
        {
            Debug.LogWarning($"[SkillManager] 스킬 데이터 없음: {skillName}");
            return;
        }

        // 패시브 스킬은 ON/OFF 개념이 없으므로 별도 처리 없음
        if (data.skillType == SkillType.Passive)
        {
            return;
        }

        // 오브젝트 풀에서 스킬 이펙트 오브젝트 꺼내기
        GameObject obj = pool?.GetSkillObject(data);
        if (obj != null)
        {
            // TODO: obj 위치·방향 초기화 등 추가 로직
        }
    }

    /// <summary>
    /// 전직 시 새 직업의 스킬 목록으로 교체한다.
    /// ChangeJob.ApplyJobByIndex()에서 호출된다.
    /// </summary>
    public void RefreshSkills(List<SkillData> newSkills)
    {
        if (newSkills == null)
        {
            _currentSkills.Clear();
            return;
        }

        _currentSkills = new List<SkillData>(newSkills);
    }

    /// <summary>
    /// 현재 직업의 스킬 목록을 반환한다.
    /// </summary>
    public IReadOnlyList<SkillData> GetCurrentSkills() => _currentSkills.AsReadOnly();
}
