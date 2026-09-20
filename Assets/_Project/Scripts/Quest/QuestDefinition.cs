using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum eQuestPresentationType
{
    None = 0,
    HighlightButton = 1,
    OpenPopup = 2,
    FocusContent = 3,
    TutorialMessage = 4
}

public enum eQuestProgressMode
{
    EventCount = 0,
    CurrentState = 1,
    LifetimeTotal = 2
}

//퀘스트 클리어 조건
public enum eQuestObjectiveType
{
    StageClear = 0, //스테이지 클리어
    MonsterKill = 1, //몬스터 처치

    LevelUp = 2, //레벨업
    Enhance = 3, //장비 강화
    EquipmentObtain = 4, //장비 획득
    EquipmentEquip = 5, //장비 장착
    ItemUse = 6, //아이템 사용

    GachaUse = 7, //뽑기권 사용

    DungeonEnter = 8, //던전 입장
    DungeonClear = 9, //던전 클리어

    JobChange = 10, //직업 변경
    SkillEquip = 11, //스킬 장착
    PlayerLevel = 12,
    BossKill = 13,
    StatEnhance = 14,
    SkillObtain = 15,
    SkillEnhance = 16,
    SkillAwaken = 17,
    Reincarnate = 18,
    ReincarnationLevel = 19,
    OfflineClaim = 20,
    QuestAllClear = 21,
    EquipmentEnhance = 22,
    BattleTime = 23,
    MainWaveClear = 24,
    SkillCast = 25
}
//퀘스트 종류(가이드, 일일, 주간, 도전과제)
public enum eQuestCategory
{
    Guide = 0,
    Daily = 1,
    Weekly = 2,
    Achievement = 3
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
