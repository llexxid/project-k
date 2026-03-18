using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 내 모든 직업 데이터를 보관하는 ScriptableObject.
/// Assets > Create > ScriptableObjects > JobDatabase 로 에셋 생성한 뒤
/// 인스펙터에서 JobData 에셋들을 등록한다.
/// </summary>
[CreateAssetMenu(fileName = "JobDatabase", menuName = "ScriptableObjects/JobDatabase")]
public class JobDatabase : ScriptableObject
{
    [Tooltip("게임에 등록된 직업 목록. 인스펙터에서 JobData 에셋을 추가하세요.")]
    public List<JobData> jobs = new List<JobData>();

    /// <summary>
    /// 직업 이름으로 JobData를 검색한다.
    /// </summary>
    public JobData GetJob(string jobName)
    {
        return jobs.Find(j => j.jobName == jobName);
    }

    /// <summary>
    /// 인덱스로 JobData를 가져온다. 범위를 벗어나면 null 반환.
    /// </summary>
    public JobData GetJob(int index)
    {
        if (index < 0 || index >= jobs.Count) return null;
        return jobs[index];
    }

    public int Count => jobs.Count;
}
