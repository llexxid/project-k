using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public SkillObjectPool pool;
    public SkillDatabase skillDatabase; // 인스펙터에서 SkillDatabase 할당
    public Animator anim;

    public void ActivateSkill(string skillName, Vector3 position)
    {
        SkillData data = skillDatabase.GetSkill(skillName);
        if (data == null) return;

        // 1. 패시브 스킬 로직 (프리팹 생성 안 함)
        if (data.skillType == SkillType.Passive)
        {
            Debug.Log($"{data.skillName} 패시브 효과 적용 중...");
            return;
        }

        // 2. 액티브 스킬 로직 (프리팹 유무 체크)
        if (data.skillPrefab != null)
        {
            GameObject skillObj = pool.GetSkillObject(data);
            skillObj.transform.position = position;
            Debug.Log($"{data.skillName} 발동! 데미지: {data.damage}");
        }
        else
        {
            // 프리팹은 없지만 로직만 있는 액티브 스킬 처리
            anim.Play(data.animationStateName);
            Debug.Log($"{data.skillName} 발동(이펙트 없음)");
        }
    }
}