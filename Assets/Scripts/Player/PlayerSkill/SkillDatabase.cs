using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSkillDatabase", menuName = "ScriptableObjects/SkillDatabase")]
public class SkillDatabase : ScriptableObject
{
    public List<SkillData> skillDataList = new List<SkillData>();

    private Dictionary<string, SkillData> skillDict = new Dictionary<string, SkillData>();

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

    public SkillData GetSkill(string skillName)
    {
        if (skillDict.Count == 0) Initialize();

        skillDict.TryGetValue(skillName, out SkillData targetSkill);

        return targetSkill;
    }
}