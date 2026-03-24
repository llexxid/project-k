using UnityEngine;

/// <summary>
/// 패링 스킬 BT LeafNode.
/// 쿨타임마다 자동 발동하여 일정 시간 동안 패링 상태를 활성화한다.
/// 패링 중 피격 시 데미지 무효화 + 반격 데미지 적용은 Player.TakeDamage()에서 처리한다.
/// </summary>
public class PlayerParry
{
    private readonly Player    _player;
    private readonly SkillData _skillData;

    private float _nextAvailableTime = 0f;

    public PlayerParry(Player player, SkillData skillData)
    {
        _player    = player;
        _skillData = skillData;
    }

    public NodeState Execute()
    {
        if (Time.time < _nextAvailableTime) return NodeState.Failure;

        // 쿨타임 소모 (강화 레벨 반영)
        var enhancer = SkillEnhanceManager.Instance;
        float finalCooldown = enhancer != null
            ? enhancer.Runtime.GetFinalCooldown(_skillData)
            : _skillData.cooldown;
        _nextAvailableTime = Time.time + finalCooldown;

        // 반격 데미지 계수 (강화 레벨 반영, damage 필드를 계수로 재활용)
        float counterMultiplier = enhancer != null
            ? enhancer.Runtime.GetFinalDamage(_skillData)
            : _skillData.damage;

        // 패링 활성화
        _player.ActivateParry(_skillData.parryDuration, counterMultiplier);

        // 애니메이션 재생
        _player.PlaySkillAnimation(_skillData.animationStateName);

        Debug.Log($"[패링] {_skillData.skillName} 활성화 " +
                  $"(지속: {_skillData.parryDuration}s, 반격계수: {counterMultiplier:F2})");

        return NodeState.Success;
    }

    public class ParryNode : Node
    {
        private readonly PlayerParry _parry;
        public ParryNode(PlayerParry parry) { _parry = parry; }
        public override NodeState Evaluate() => _parry.Execute();
    }
}
