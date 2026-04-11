using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런타임에서 스킬별 강화 레벨을 보관하고,
/// 강화 레벨이 반영된 최종 수치(데미지 계수·쿨타임)를 계산해 반환한다.
/// 강화 레벨은 PlayerPrefs에 JSON으로 영구 저장된다.
/// </summary>
public class PlayerSkillRuntime
{
    // ─────────────────────────────────────
    //  상수
    // ─────────────────────────────────────
    private const string SAVE_KEY = "SkillEnhanceData";

    // ─────────────────────────────────────
    //  데이터
    // ─────────────────────────────────────
    /// <summary>스킬명 → 현재 강화 레벨 (0 = 미강화)</summary>
    private Dictionary<string, int> _skillLevels = new Dictionary<string, int>();

    // ─────────────────────────────────────
    //  레벨 조회/설정
    // ─────────────────────────────────────

    /// <summary>스킬의 현재 강화 레벨을 반환한다. 등록되지 않은 스킬은 0 반환.</summary>
    public int GetLevel(string skillName)
    {
        _skillLevels.TryGetValue(skillName, out int level);
        return level;
    }

    /// <summary>강화 레벨을 amount만큼 증가시킨다. maxLevel 초과분은 clamp.</summary>
    public void AddLevel(SkillData skill, int amount)
    {
        if (skill == null) return;
        int current = GetLevel(skill.skillName);
        _skillLevels[skill.skillName] = Mathf.Clamp(current + amount, 0, skill.maxLevel);
    }

    /// <summary>모든 스킬 강화 레벨을 0으로 초기화한다.</summary>
    public void ResetAll()
    {
        var keys = new List<string>(_skillLevels.Keys);
        foreach (var k in keys) _skillLevels[k] = 0;
    }

    // ─────────────────────────────────────
    //  수치 계산
    // ─────────────────────────────────────

    /// <summary>
    /// 강화 레벨이 반영된 최종 데미지 계수를 반환한다.
    /// 공식: damage × (1 + damagePerLevel × level)
    /// PlayerSkill.Execute()에서 int baseAtk × 이 값을 곱해 사용한다.
    /// </summary>
    public float GetFinalDamage(SkillData skill)
    {
        if (skill == null) return 0f;
        int level = GetLevel(skill.skillName);
        return skill.damage * (1f + skill.damagePerLevel * level);
    }

    /// <summary>
    /// 강화 레벨이 반영된 최종 쿨타임(초)을 반환한다.
    /// 공식: cooldown × (1 - cooldownReductionPerLevel × level)
    /// 최솟값 = 원본 쿨타임의 10%
    /// </summary>
    public float GetFinalCooldown(SkillData skill)
    {
        if (skill == null) return 0f;
        int level = GetLevel(skill.skillName);
        float reduced = skill.cooldown * (1f - skill.cooldownReductionPerLevel * level);
        return Mathf.Max(reduced, skill.cooldown * 0.1f);
    }

    // ─────────────────────────────────────
    //  저장 / 불러오기
    // ─────────────────────────────────────

    /// <summary>현재 강화 레벨을 PlayerPrefs에 JSON으로 저장한다.</summary>
    public void Save()
    {
        var wrapper = new SaveWrapper(_skillLevels);
        PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    /// <summary>PlayerPrefs에서 강화 레벨을 불러온다.</summary>
    public void Load()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
        {
            return;
        }
        try
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            var wrapper = JsonUtility.FromJson<SaveWrapper>(json);
            _skillLevels = wrapper.ToDictionary();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// 직업 데이터(JobData)의 skillLevels로 런타임 레벨을 즉시 덮어쓴다.
    /// 전직 시 ChangeJob.ApplyJobByIndex()에서 호출한다.
    /// skillLevels가 null이거나 비어있으면 아무 변경도 하지 않는다.
    /// </summary>
    public void Load(JobData data)
    {
        if (data == null || data.skillLevels == null || data.skillLevels.Count == 0)
            return;

        foreach (var entry in data.skillLevels)
        {
            if (string.IsNullOrEmpty(entry.skillName)) continue;
            _skillLevels[entry.skillName] = Mathf.Max(0, entry.level);
        }
    }

    // ─────────────────────────────────────
    //  직렬화 헬퍼 (JsonUtility는 Dictionary 미지원)
    // ─────────────────────────────────────
    [Serializable]
    private class SaveWrapper
    {
        public List<string> keys   = new List<string>();
        public List<int>    values = new List<int>();

        public SaveWrapper() { }
        public SaveWrapper(Dictionary<string, int> dict)
        {
            foreach (var kv in dict) { keys.Add(kv.Key); values.Add(kv.Value); }
        }
        public Dictionary<string, int> ToDictionary()
        {
            var dict = new Dictionary<string, int>();
            for (int i = 0; i < keys.Count && i < values.Count; i++)
                dict[keys[i]] = values[i];
            return dict;
        }
    }
}
