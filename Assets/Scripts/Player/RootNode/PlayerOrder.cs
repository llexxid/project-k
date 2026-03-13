using System.Collections.Generic;
using UnityEngine;

public class PlayerOrder
{
    public Node _rootNode;

    private PlayerDetection _detection;
    private PlayerMove _move;
    public PlayerAttack _attack;
    private PlayerIdle _idle;

    // 전투 시퀀스의 공격 Selector 노드 (스킬 재조립 시 교체)
    private Selector _attackSelector;

    public void Init(Player player)
    {
        _detection = new PlayerDetection(player);
        _move      = new PlayerMove(player);
        _attack    = new PlayerAttack(player, _detection);
        _idle      = new PlayerIdle(player);

        // 공격 Selector: 스킬(없으면 생략) → 일반 공격 fallback
        _attackSelector = new Selector(new List<Node>
        {
            new PlayerAttack.AttackNode(_attack)
        });

        // 트리 조립: Selector(전투 OR 대기)
        _rootNode = new Selector(new List<Node>
        {
            // 1. 전투 시퀀스 (감지 → 이동 → 공격 Selector)
            new Sequence(new List<Node>
            {
                new PlayerDetection.DetectionNode(_detection),
                new PlayerMove.MoveNode(_move),
                _attackSelector
            }),

            // 2. 대기 (전투 실패 시 실행)
            new PlayerIdle.IdleNode(_idle)
        });
    }

    /// <summary>
    /// 전직 시 새 직업의 스킬 목록으로 공격 Selector를 재조립한다.
    /// ChangeJob.ApplyJobByIndex()에서 호출된다.
    /// </summary>
    public void RebuildSkillTree(List<SkillData> skills, Player player)
    {
        var nodes = new List<Node>();

        // 보유 스킬마다 PlayerSkill LeafNode 생성 (쿨타임 짧은 스킬이 우선)
        // 모든 스킬이 하나의 sharedState를 공유 → 동시 발동 방지
        if (skills != null)
        {
            var sharedState = new PlayerSkill.SkillSharedState();
            foreach (var skillData in skills)
            {
                var skill = new PlayerSkill(player, skillData, _detection, sharedState);
                nodes.Add(new PlayerSkill.SkillNode(skill));
            }
        }

        // 마지막에 일반 공격 fallback 추가
        nodes.Add(new PlayerAttack.AttackNode(_attack));

        // 기존 Selector의 자식 노드 목록을 교체
        _attackSelector.ReplaceChildren(nodes);

        Debug.Log($"[PlayerOrder] 스킬 트리 재조립 완료. 스킬 {skills?.Count ?? 0}개 + 일반 공격");
    }
}
