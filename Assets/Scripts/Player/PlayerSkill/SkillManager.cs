using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    public SkillObjectPool pool;
    public SkillDatabase skillDatabase;
    public Animator anim;

    // 각 스킬의 다음 사용 가능 시간을 저장하는 딕셔너리
    private Dictionary<string, float> _skillCooldowns = new Dictionary<string, float>();

    public void ActivateSkill(string skillName, Vector3 position)
    {
        SkillData data = skillDatabase.GetSkill(skillName);
        if (data == null) return;

        // 1. 패시브 스킬 로직 (쿨타임 미적용)
        if (data.skillType == SkillType.Passive)
        {
            Debug.Log($"{data.skillName} 패시브 효과 적용 중...");
            return;
        }

        // 2. 쿨타임 체크
        if (_skillCooldowns.TryGetValue(skillName, out float nextReadyTime))
        {
            if (Time.time < nextReadyTime)
            {
                Debug.Log($"{skillName} 쿨타임 중: {nextReadyTime - Time.time:F1}초 남음");
                return;
            }
        }

        // 3. 액티브 스킬 발동 및 쿨타임 갱신
        _skillCooldowns[skillName] = Time.time + data.cooldown;

        if (data.skillPrefab != null)
        {
            GameObject skillObj = pool.GetSkillObject(data);
            skillObj.transform.position = position;
            Debug.Log($"{data.skillName} 발동! 데미지: {data.damage}");
        }
        else
        {
            anim.Play(data.animationStateName);
            Debug.Log($"{data.skillName} 발동(이펙트 없음)");
        }
    }
}