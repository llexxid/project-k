using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    public SkillObjectPool pool;
    public SkillDatabase skillDatabase;

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
            Debug.Log($"[SkillManager] {data.skillName} 패시브 효과 적용 중...");
            return;
        }

        // 오브젝트 풀에서 스킬 이펙트 오브젝트 꺼내기
        GameObject obj = pool?.GetSkillObject(data);
        if (obj != null)
        {
            Debug.Log($"[SkillManager] {skillName} 이펙트 활성화");
            // TODO: obj 위치·방향 초기화 등 추가 로직
        }
    }
}