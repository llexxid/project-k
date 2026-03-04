using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStatus
{
    public int MaxHP    { get; set; } = 100;
    public int HP       { get; set; } = 100;
    public int Atk      { get; set; } = 1;
    public int MovSpeed { get; set; } = 5;
    public float AtkSpeed { get; set; } = 1;
    public string JobName { get; set; } = "Warrior";

    /// <summary>
    /// 전직 완료 시 발행되는 이벤트. 인자는 새 직업 이름.
    /// </summary>
    public System.Action<string> OnJobChanged;

    /// <summary>
    /// JobData의 스탯을 일괄 적용한다.
    /// 전직 시 HP는 MaxHP로 풀회복된다.
    /// </summary>
    public void ApplyJob(JobData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[PlayerStatus] ApplyJob: JobData가 null입니다.");
            return;
        }

        MaxHP    = data.maxHP;
        HP       = data.maxHP;   // 전직 시 HP 풀회복
        Atk      = data.atk;
        MovSpeed = data.movSpeed;
        AtkSpeed = data.atkSpeed;
        JobName  = data.jobName;

        OnJobChanged?.Invoke(JobName);
    }
}

