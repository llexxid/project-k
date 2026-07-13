using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum eQuestPresentationType
{
    None,
    HighlightButton,
    OpenPopup,
    FocusContent,
    TutorialMessage
}

public enum eQuestProgressMode
{
    EventCount,
    CurrentState,
    LifetimeTotal
}

//퀘스트 클리어 조건
public enum eQuestObjectiveType
{
    StageClear, //스테이지 클리어
    MonsterKill, //몬스터 처치

    LevelUp, //레벨업
    Enhance, //장비 강화
    EquipmentObtain, //장비 획득
    EquipmentEquip, //장비 장착
    ItemUse, //아이템 사용

    GachaUse, //뽑기권 사용

    DungeonEnter, //던전 입장
    DungeonClear, //던전 클리어

    JobChange, //직업 변경
    SkillEquip //스킬 장착
}
//퀘스트 종류(가이드, 일일, 주간, 도전과제)
public enum eQuestCategory
{
    Guide, 
    Daily,
    Weekly,
    Achievement
}

[Serializable]
public class QuestDefinition 
{
    [Tooltip("퀘스트의 ID값")]
    public long QuestId;
    public eQuestCategory Category;

    [Tooltip("퀘스트 제목")]
    public string Title;
    [Tooltip("실제 퀘스트창에 표시될 이름")]
    public string Description;
    
    [Tooltip("퀘스트의 목표")]
    public eQuestObjectiveType ObjectiveType;
    [Tooltip("목표달성이 어떻게 되는지 확인 \nEventCount : 퀘스트가 활성화 된 순간부터 카운트\nCurrentState : 현재 진행상황")]
    public eQuestProgressMode ProgressMode;
    [Tooltip("특정 목표값(ex. 스테이지 1-1 = 8590000129, 제한없으면 0)")]
    public long TargetId;
    [Tooltip("목표달성 횟수")]
    public int RequiredCount;

    [Tooltip("다음 퀘스트 번호")]
    public int NextQuestId;
    [Tooltip("보상 그룹")]
    public int RewardGroupId;

    [Tooltip("퀘스트 반복가능 여부(일간/주간)")]
    public bool IsRepeatable;
    [Tooltip("퀘스트 UI작용")]
    public eQuestPresentationType PresentationType;
}
