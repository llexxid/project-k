using UnityEngine;

/// <summary>
/// 직업 하나의 스탯·비주얼 정보를 담는 ScriptableObject.
/// Assets > Create > ScriptableObjects > JobData 로 에셋 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewJobData", menuName = "ScriptableObjects/JobData")]
public class JobData : ScriptableObject
{
    [Header("직업 정보")]
    public string jobName;                              // 직업 이름 (예: "Knight", "Mage")

    [Header("비주얼")]
    public Sprite jobSprite;                            // 전직 시 교체할 캐릭터 스프라이트
    public RuntimeAnimatorController animatorController;// 전직 시 교체할 애니메이터 컨트롤러

    [Header("기본 스탯")]
    public int maxHP;               // 최대 체력
    public int atk;                 // 공격력
    public int movSpeed;            // 이동 속도
    public float atkSpeed;          // 공격 속도 (초)

    [Header("전직 비용")]
    [Tooltip("첫 전직 시 필요한 골드. 이미 해금된 직업은 무료.")]
    public int unlockCost;
}

