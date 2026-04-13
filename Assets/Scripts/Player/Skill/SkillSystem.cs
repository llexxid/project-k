using UnityEngine;

/// <summary>
/// 플레이어 한 명의 스킬 목록을 관리한다.
/// Player.Awake()에서 생성, ChangeJob에서 Setup() 호출.
/// </summary>
public class SkillSystem
{
    private readonly Player _player;
    private ActiveSkill _basicAttack;
    private readonly System.Collections.Generic.List<ActiveSkill> _specials
        = new System.Collections.Generic.List<ActiveSkill>();
    private float _busyUntil;

    /// <summary>기본공격 사거리 (Move 정지 거리 · Detection 반경 결정).</summary>
    public float AttackRange { get; private set; }

    // ── UI 표시 슬롯 (최대 3) ──
    public struct DisplaySlot
    {
        public string Name;
        public bool IsPassive;
        public bool Active;
    }
    private readonly DisplaySlot[] _slots = new DisplaySlot[3];

    public int SlotCount
    {
        get
        {
            int c = 0;
            for (int i = 0; i < 3; i++) if (_slots[i].Active) c++;
            return c;
        }
    }

    public SkillSystem(Player player) { _player = player; }

    // ────────────────────────────────────────────
    //  Setup — 직업별 스킬 구성
    // ────────────────────────────────────────────
    public void Setup(string jobName)
    {
        _basicAttack = null;
        _specials.Clear();
        _busyUntil = 0f;
        AttackRange = 2f;
        for (int i = 0; i < 3; i++) _slots[i] = default;

        switch (jobName)
        {
            case "Spearman":
                _basicAttack = new BasicAttackSingle(_player, range: 2f, cooldown: 0.5f);
                AttackRange = 2f;
                SetSlot(0, "기본공격", false);
                break;

            case "Knight":
                _basicAttack = new BasicAttackSingle(_player, range: 2f, cooldown: 1f);
                AttackRange = 2f;
                SetSlot(0, "기본공격", false);
                SetSlot(1, "수호의 오라", true);
                break;

            case "Elite_Knight":
                _basicAttack = new BasicAttackRect(_player, range: 2f, halfWidth: 1.5f, halfHeight: 1f, cooldown: 1f);
                _specials.Add(new IronWill(_player, cooldown: 30f, healPercent: 0.1f, duration: 15f));
                AttackRange = 2f;
                SetSlot(0, "기본공격", false);
                SetSlot(1, "수호의 오라", true);
                SetSlot(2, "강철 의지", false);
                break;

            case "Archer":
                _basicAttack = new BasicAttackSingle(_player, range: 4f, cooldown: 2f);
                AttackRange = 4f;
                SetSlot(0, "기본공격", false);
                SetSlot(1, "명중의 오라", true);
                break;

            case "Elite_Archer":
                _basicAttack = new BasicAttackSingle(_player, range: 4f, cooldown: 2f);
                _specials.Add(new ChargeShot(_player, range: 4f, cooldown: 8f, hitCount: 3));
                AttackRange = 4f;
                SetSlot(0, "기본공격", false);
                SetSlot(1, "명중의 오라", true);
                SetSlot(2, "집중 사격", false);
                break;

            case "Mage":
                _basicAttack = new BasicAttackProjectile(_player, range: 5f, cooldown: 2f, aoeRadius: 0.5f);
                AttackRange = 5f;
                SetSlot(0, "기본공격", false);
                SetSlot(1, "마력의 오라", true);
                break;

            case "Elite_Mage":
                _basicAttack = new BasicAttackProjectile(_player, range: 5f, cooldown: 2f, aoeRadius: 0.5f);
                _specials.Add(new EnergyPulse(_player, triggerRange: 3f, cooldown: 10f, knockbackForce: 5f));
                AttackRange = 5f;
                SetSlot(0, "기본공격", false);
                SetSlot(1, "마력의 오라", true);
                SetSlot(2, "에너지 파동", false);
                break;
        }
    }

    // ────────────────────────────────────────────
    //  Execution (BT 리프에서 호출)
    // ────────────────────────────────────────────
    public bool TryExecuteSkills()
    {
        if (Time.time < _busyUntil) return false;

        // 스페셜 스킬 우선 (IsSelfTriggered가 아닌 것만)
        for (int i = 0; i < _specials.Count; i++)
        {
            var skill = _specials[i];
            if (skill.IsSelfTriggered) continue;
            if (skill.IsReady && skill.CanExecute())
            {
                float busy = skill.Execute();
                _busyUntil = Time.time + busy;
                return true;
            }
        }

        // 기본공격
        if (_basicAttack != null && _basicAttack.IsReady && _basicAttack.CanExecute())
        {
            float busy = _basicAttack.Execute();
            _busyUntil = Time.time + busy;
            return true;
        }

        return false;
    }

    // ────────────────────────────────────────────
    //  Tick (매 프레임)
    // ────────────────────────────────────────────
    public void Tick()
    {
        _basicAttack?.Tick();

        for (int i = 0; i < _specials.Count; i++)
        {
            var skill = _specials[i];
            skill.Tick();

            // 자기 트리거 스킬 (IronWill 등) — BT 밖에서도 발동
            if (skill.IsSelfTriggered && skill.IsReady && skill.CanExecute()
                && Time.time >= _busyUntil)
            {
                float busy = skill.Execute();
                _busyUntil = Time.time + busy;
            }
        }
    }

    // ────────────────────────────────────────────
    //  UI
    // ────────────────────────────────────────────
    public DisplaySlot GetSlot(int index)
    {
        if (index < 0 || index >= 3) return default;
        return _slots[index];
    }

    public float GetSlotCooldown(int index)
    {
        if (index < 0 || index >= 3) return 0f;
        if (!_slots[index].Active || _slots[index].IsPassive) return 0f;

        // slot 0 = 기본공격
        if (index == 0 && _basicAttack != null) return _basicAttack.CooldownRemaining;

        // slot 2 = 첫 번째 스페셜 스킬
        if (index == 2 && _specials.Count > 0) return _specials[0].CooldownRemaining;

        return 0f;
    }

    private void SetSlot(int index, string name, bool isPassive)
    {
        _slots[index] = new DisplaySlot { Name = name, IsPassive = isPassive, Active = true };
    }

    // ── 직업별 스킬 정보 (UI 표시용) ──
    public struct SkillInfo
    {
        public string Name;
        public bool IsPassive;
        public string Description;
    }

    /// <summary>해당 직업이 보유한 스킬 목록을 반환 (UI 패널용).</summary>
    public static SkillInfo[] GetJobSkillInfo(string jobName)
    {
        switch (jobName)
        {
            case "Spearman":
                return new[]
                {
                    new SkillInfo { Name = "기본공격", IsPassive = false, Description = "단일 대상 근접 공격" }
                };
            case "Knight":
                return new[]
                {
                    new SkillInfo { Name = "기본공격", IsPassive = false, Description = "단일 대상 근접 공격" },
                    new SkillInfo { Name = "수호의 오라", IsPassive = true, Description = "팀 전체 HP +100%" }
                };
            case "Elite_Knight":
                return new[]
                {
                    new SkillInfo { Name = "기본공격", IsPassive = false, Description = "전방 직사각형 범위 공격" },
                    new SkillInfo { Name = "수호의 오라", IsPassive = true, Description = "팀 전체 HP +100%" },
                    new SkillInfo { Name = "강철 의지", IsPassive = false, Description = "HP 50% 미만 시 15초간 회복" }
                };
            case "Archer":
                return new[]
                {
                    new SkillInfo { Name = "기본공격", IsPassive = false, Description = "원거리 즉발 공격" },
                    new SkillInfo { Name = "명중의 오라", IsPassive = true, Description = "팀 전체 ATK +100%" }
                };
            case "Elite_Archer":
                return new[]
                {
                    new SkillInfo { Name = "기본공격", IsPassive = false, Description = "원거리 즉발 공격" },
                    new SkillInfo { Name = "명중의 오라", IsPassive = true, Description = "팀 전체 ATK +100%" },
                    new SkillInfo { Name = "집중 사격", IsPassive = false, Description = "3연속 타격" }
                };
            case "Mage":
                return new[]
                {
                    new SkillInfo { Name = "기본공격", IsPassive = false, Description = "직선 투사체 + 소범위 폭발" },
                    new SkillInfo { Name = "마력의 오라", IsPassive = true, Description = "팀 ATK +50% · HP +50%" }
                };
            case "Elite_Mage":
                return new[]
                {
                    new SkillInfo { Name = "기본공격", IsPassive = false, Description = "직선 투사체 + 소범위 폭발" },
                    new SkillInfo { Name = "마력의 오라", IsPassive = true, Description = "팀 ATK +50% · HP +50%" },
                    new SkillInfo { Name = "에너지 파동", IsPassive = false, Description = "원형 범위 피해 + 넉백" }
                };
            default:
                return System.Array.Empty<SkillInfo>();
        }
    }

    // ── BT 노드 ──
    public class SkillSystemNode : Node
    {
        private readonly SkillSystem _system;
        public SkillSystemNode(SkillSystem system) { _system = system; }

        public override NodeState Evaluate()
        {
            if (_system == null) return NodeState.Failure;
            return _system.TryExecuteSkills() ? NodeState.Success : NodeState.Failure;
        }
    }
}
