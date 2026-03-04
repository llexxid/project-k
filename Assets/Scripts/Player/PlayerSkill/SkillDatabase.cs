using UnityEngine;
using System.Collections.Generic;

// ================================================================
//  스킬 등록 방법 (코드 수정 없이 에디터에서만 작업)
// ================================================================
//
//  ── STEP 1. SkillData 에셋 생성 ──────────────────────────────
//  Project 창 우클릭 → Create > ScriptableObjects > SkillData
//  Inspector 필드:
//    · Skill Name   : 스킬 고유 이름 (예: "Wind_Lance")
//    · Skill Type   : Active (자동 발동)
//    · Damage       : 공격력 계수 (예: 1.5 → 기본 Atk × 1.5배)
//    · Cooldown     : 재사용 대기시간 (초)
//
//  ── STEP 2. VFX 연결 (선택 사항) ────────────────────────────
//  Skill Name == eVFXType 열거형 이름이면 자동 재생.
//  새 이름 쓰려면 eVFXType 열거형에 같은 이름 추가.
//
//  ── STEP 3. JobData에 스킬 등록 ─────────────────────────────
//  원하는 직업 JobData 에셋 Inspector > Skills → + → SkillData 드래그
//  · 리스트 앞 번호가 BT 우선순위 (앞 스킬 쿨타임 중이면 다음 스킬 시도)
//
//  ── STEP 4. Animation Event (일반 공격 한정) ─────────────────
//  Attack_Anim 타격 프레임 → Add Event → Function: OnAttackHit()
//  스킬 데미지는 PlayerSkill.Execute()에서 자동 처리. 별도 설정 불필요.
// ================================================================

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