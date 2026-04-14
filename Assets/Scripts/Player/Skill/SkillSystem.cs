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
    //  Setup — JobData SO 기반 스킬 구성
    // ────────────────────────────────────────────
    public void Setup(JobData data)
    {
        _basicAttack = null;
        _specials.Clear();
        _busyUntil = 0f;
        AttackRange = 2f;
        for (int i = 0; i < 3; i++) _slots[i] = default;

        if (data == null) return;

        // ── 기본공격 ──
        if (data.basicAttack != null)
        {
            _basicAttack = CreateBasicAttack(data.basicAttack);
            AttackRange = data.basicAttack.range;
            SetSlot(0, "기본공격", false);
        }

        // ── 직업별 패시브 오라 (UI 표시만, 실제 적용은 ChangeJob) ──
        string auraName = GetAuraName(data.jobName);
        if (!string.IsNullOrEmpty(auraName))
            SetSlot(1, auraName, true);

        // ── 스페셜 스킬 ──
        if (data.specialSkills != null)
        {
            foreach (var cfg in data.specialSkills)
            {
                if (cfg == null || cfg.kind == SpecialSkillKind.None) continue;
                var skill = CreateSpecialSkill(cfg);
                if (skill == null) continue;
                _specials.Add(skill);
                // 첫 번째 스페셜 스킬을 슬롯 2 에 표시
                if (_specials.Count == 1)
                    SetSlot(2, skill.DisplayName, skill.IsSelfTriggered ? false : false);
            }
        }
    }

    private ActiveSkill CreateBasicAttack(BasicAttackConfig cfg)
    {
        switch (cfg.type)
        {
            case BasicAttackType.Single:
                return new BasicAttackSingle(_player, cfg.range, cfg.cooldown, cfg.damageMultiplier);
            case BasicAttackType.Rect:
                return new BasicAttackRect(_player, cfg.range, cfg.halfWidth, cfg.halfHeight, cfg.cooldown, cfg.damageMultiplier);
            case BasicAttackType.Projectile:
                return new BasicAttackProjectile(_player, cfg.range, cfg.cooldown, cfg.aoeRadius, cfg.projectileSpeed, cfg.damageMultiplier);
        }
        return null;
    }

    private ActiveSkill CreateSpecialSkill(SpecialSkillConfig cfg)
    {
        switch (cfg.kind)
        {
            case SpecialSkillKind.IronWill:
                return new IronWill(_player, cfg.cooldown, cfg.healPercent, cfg.duration, cfg.triggerHPRatio);
            case SpecialSkillKind.ChargeShot:
                return new ChargeShot(_player, cfg.range, cfg.cooldown, cfg.hitCount, cfg.damageMultiplier);
            case SpecialSkillKind.EnergyPulse:
                return new EnergyPulse(_player, _basicAttack, cfg.triggerRange, cfg.cooldown, cfg.knockbackForce, cfg.damageMultiplier);
        }
        return null;
    }

    private static string GetAuraName(string jobName)
    {
        switch (jobName)
        {
            case "Knight":
            case "Elite_Knight":
                return "수호의 오라";
            case "Archer":
            case "Elite_Archer":
                return "명중의 오라";
            case "Mage":
            case "Elite_Mage":
                return "마력의 오라";
        }
        return null;
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

        // 스페셜 스킬이 활성 상태(애니메이션 재생 중)이면 기본공격 차단
        for (int i = 0; i < _specials.Count; i++)
            if (_specials[i].IsActive) return false;

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
