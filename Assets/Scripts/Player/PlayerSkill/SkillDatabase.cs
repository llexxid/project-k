using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSkillDatabase", menuName = "ScriptableObjects/SkillDatabase")]
public class SkillDatabase : ScriptableObject
{
    // 인스펙터에서 등록할 스킬 데이터 리스트
    public List<SkillData> skillDataList = new List<SkillData>();

    // 빠른 검색을 위한 딕셔너리
    private Dictionary<string, SkillData> skillDict = new Dictionary<string, SkillData>();

    // 데이터 초기화 및 딕셔너리 구축
    public void Initialize()
    {
        skillDict.Clear();
        foreach (var data in skillDataList)
        {
            if (data != null && !skillDict.ContainsKey(data.skillName))
            {
                skillDict.Add(data.skillName, data);
            }
        }
    }

    // 이름으로 스킬 정보 가져오기
    public SkillData GetSkill(string skillName)
    {
        if (skillDict.Count == 0) Initialize();

        skillDict.TryGetValue(skillName, out SkillData targetSkill);


        return targetSkill;
    }
}