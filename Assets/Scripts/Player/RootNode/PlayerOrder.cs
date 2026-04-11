using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerOrder
{
    public Node _rootNode;

    private PlayerDetection _detection;
    public PlayerMove _move;
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
                //사실상 여기어 Attack - Skill - AttackNode가 엮어있는거네.
            }),

            // 2. 대기 (전투 실패 시 실행)
            new PlayerIdle.IdleNode(_idle)
        });

        // PlayerOrder.cs - 현재 코드 수정
        RebuildSkillTree(player.skillManager.GetCurrentSkills().ToList(), player);
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
                // 패시브 스킬은 BT에서 실행하지 않음 (ChangeJob에서 별도 적용)
                if (skillData.skillType == SkillType.Passive) continue;

                if (skillData.skillEffectType == SkillEffectType.Parry)
                {
                    var parry = new PlayerParry(player, skillData, sharedState);
                    nodes.Add(new PlayerParry.ParryNode(parry));
                }
                else
                {
                    var skill = new PlayerSkill(player, skillData, _detection, sharedState);
                    nodes.Add(new PlayerSkill.SkillNode(skill));
                }
            }
        }

        int skillNodeCount = nodes.Count; // 마지막 AttackNode 추가 전 개수

        // 마지막에 일반 공격 fallback 추가
        //여기서 Attack
        PlayerAttack.AttackNode Anode = new PlayerAttack.AttackNode(_attack);
		nodes.Add(Anode);

        // 기존 Selector의 자식 노드 목록을 교체
        _attackSelector.ReplaceChildren(nodes);
    }

    private bool _isAbort;

    public bool IsAbort => _isAbort;

    public void InterruptBT()
    {
        _isAbort = true;
    }

    public void RecoveryBT()
    {         
        _isAbort = false;
    }

    public void ExecuteNode()
    {
        if (_isAbort) return;
        _rootNode.Evaluate();
    }
}
