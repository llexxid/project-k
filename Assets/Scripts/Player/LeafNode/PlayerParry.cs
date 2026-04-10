using Scripts.Core;
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
    private readonly PlayerSkill.SkillSharedState _sharedState;

    private float _nextAvailableTime = 0f;

    public PlayerParry(Player player, SkillData skillData, PlayerSkill.SkillSharedState sharedState)
    {
        _player      = player;
        _skillData   = skillData;
        _sharedState = sharedState;
    }

    public NodeState Execute()
    {
        if (Time.time < _nextAvailableTime) return NodeState.Failure;
        Debug.Log($"[PlayerParry] FIRING player={_player.name} Time={Time.time:F2}");

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

        // 공유 잠금: 패링 지속 시간 동안 다른 스킬 발동 방지 (패링 애니메이션 보호)
        if (_sharedState != null)
            _sharedState.nextAvailableTime = Time.time + _skillData.parryDuration;

        // SFX 재생
        if (!string.IsNullOrEmpty(_skillData.skillSFXName) &&
            System.Enum.TryParse(_skillData.skillSFXName, out eSFXType sfxType))
        {
            SFXManager.Instance.GetSFX(
                sfxType,
                _player.transform.position,
                Quaternion.identity,
                sfx => sfx.PlaySFX()
            );
        }

        // 애니메이션 재생 (parryDuration 동안 Idle/Walk 덮어쓰기 차단)
        _player.PlaySkillAnimation(_skillData.animationStateName, _skillData.parryDuration);

        return NodeState.Success;
    }

    public class ParryNode : Node
    {
        private readonly PlayerParry _parry;
        public ParryNode(PlayerParry parry) { _parry = parry; }
        public override NodeState Evaluate() => _parry.Execute();
    }
}
