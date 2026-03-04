using UnityEngine;
using System;

/// <summary>
/// 플레이어 전직 시스템.
/// J키를 누르면 JobDatabase에 등록된 직업을 순서대로 순환한다.
/// UI 연동이 필요할 경우 OnJobChanged 이벤트를 구독하면 된다.
/// </summary>
public class ChangeJob : MonoBehaviour
{
    [Tooltip("게임에 등록된 모든 직업 데이터. 인스펙터에서 JobDatabase.asset을 연결하세요.")]
    [SerializeField] private JobDatabase jobDatabase;

    // Player 컴포넌트를 통해 playerStatus / skillManager / Animator를 참조
    private Player _player;
    private SpriteRenderer _spriteRenderer;

    /// <summary>현재 적용된 직업의 인덱스 (순환에 사용)</summary>
    private int _currentJobIndex = 0;

    // ───────────────────────────────────────────
    // UI 연동 이벤트
    // UI 쪽에서 OnJobChanged를 구독하면 전직 완료 시 알림을 받을 수 있다.
    // ───────────────────────────────────────────
    #region UI Events

    /// <summary>
    /// 전직이 완료됐을 때 발행된다.
    /// 인자: (직업 이름, 현재 인덱스, 전체 직업 수)
    /// </summary>
    public event Action<string, int, int> OnJobChanged;

    #endregion
    // ───────────────────────────────────────────


    private void Start()
    {
        _player = GetComponent<Player>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_player == null)
        {
            Debug.LogError("[ChangeJob] Player 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        if (jobDatabase == null || jobDatabase.Count == 0)
        {
            Debug.LogError("[ChangeJob] JobDatabase가 비어있거나 연결되지 않았습니다.");
            return;
        }

        // 시작 시 첫 번째 직업(index 0) 적용
        ApplyJobByIndex(0);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            CycleToNextJob();
        }
    }

    /// <summary>
    /// 다음 직업으로 순환 전직한다.
    /// </summary>
    private void CycleToNextJob()
    {
        if (jobDatabase == null || jobDatabase.Count == 0) return;

        _currentJobIndex = (_currentJobIndex + 1) % jobDatabase.Count;
        ApplyJobByIndex(_currentJobIndex);
    }

    /// <summary>
    /// 직업 이름으로 즉시 전직한다.
    /// </summary>
    public void ChangeJobByName(string jobName)
    {
        if (jobDatabase == null) return;

        int idx = jobDatabase.jobs.FindIndex(j => j.jobName == jobName);
        if (idx < 0)
        {
            Debug.LogWarning($"[ChangeJob] 직업 '{jobName}'을 JobDatabase에서 찾을 수 없습니다.");
            return;
        }
        _currentJobIndex = idx;
        ApplyJobByIndex(_currentJobIndex);
    }

    /// <summary>
    /// 인덱스로 직업을 적용한다. PlayerStatus 스탯 갱신 + SkillManager 스킬 교체.
    /// </summary>
    private void ApplyJobByIndex(int index)
    {
        JobData data = jobDatabase.GetJob(index);
        if (data == null)
        {
            Debug.LogWarning($"[ChangeJob] index {index}에 해당하는 JobData가 없습니다.");
            return;
        }

        // 1. PlayerStatus 스탯 갱신
        _player.playerStatus.ApplyJob(data);

        // 2. 공격속도 변경 시 attackRate도 동기화
        if (_player.playerOrder?._attack != null)
        {
            _player.playerOrder._attack.attackRate = data.atkSpeed;
        }

        // 3. SkillManager에 직업 스킬 갱신
        _player.skillManager?.RefreshSkills(data.skills);

        // 4. BT 스킬 트리 재조립 (새 직업 스킬 → BT LeafNode로 자동 등록)
        _player.playerOrder?.RebuildSkillTree(data.skills, _player);

        // 5. 스프라이트 교체
        if (_spriteRenderer != null && data.jobSprite != null)
            _spriteRenderer.sprite = data.jobSprite;

        // 5. 애니메이터 컨트롤러 교체
        if (_player._am != null && data.animatorController != null)
        {
            _player._am.runtimeAnimatorController = data.animatorController;
            // 컨트롤러 교체 후 AnimatorComponent의 해시 캐시를 재구성
            _player.RebuildAnimatorComponent();
        }

        // 6. UI 이벤트 발행
        OnJobChanged?.Invoke(data.jobName, index, jobDatabase.Count);

        Debug.Log($"[ChangeJob] 전직 완료: {data.jobName} (HP:{data.maxHP} ATK:{data.atk})");
    }
}
