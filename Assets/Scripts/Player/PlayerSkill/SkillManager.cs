using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    public SkillObjectPool pool;
    public SkillDatabase skillDatabase;
    float remainingTime = 0f;

    // 각 스킬의 다음 사용 가능 시간을 저장하는 딕셔너리
    private Dictionary<string, float> _skillCooldowns = new Dictionary<string, float>();

    public float ActivateSkill(string skillName)
    {
        SkillData data = skillDatabase.GetSkill(skillName);
        if (data == null)
        {
            Debug.LogWarning($"스킬 데이터가 없습니다: {skillName}");
            return remainingTime;
        }

        // 1. 패시브 스킬 로직 (쿨타임 미적용)
        if (data.skillType == SkillType.Passive)
        {
            Debug.Log($"{data.skillName} 패시브 효과 적용 중...");
            return remainingTime;
        }

        // 2. 쿨타임 체크
        if (_skillCooldowns.TryGetValue(skillName, out float nextReadyTime))
        {
            Debug.Log(remainingTime + " : 556");

            if (Time.time < nextReadyTime)
            {
                Debug.Log($"{skillName} 쿨타임 중: {nextReadyTime - Time.time:F1}초 남음");

                remainingTime = nextReadyTime - Time.time;

                return remainingTime;
            }
            else 
            {
                Debug.Log($"{skillName} 사용 가능");
            }
        }
        else
        {
            Debug.Log($"{skillName} 처음 사용");
        }

            return remainingTime;
    }
}