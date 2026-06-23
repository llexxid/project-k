using KingdomIdle.UIToolkit;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Core.Utils;
using Scripts.Monster;
using Scripts.Users;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour, IAttackable, IDamageable, IRewardable
{
    public PlayerOrder playerOrder;
    public PlayerStatus playerStatus;
    public SkillSystem skillSystem;

    public EquipmentManager equipmentManager;

    [SerializeField]
    private EquipmentDatabase _equipmentDatabase;

    [SerializeField]
    private EquipmentDropTableSO _equipmentDropTable;

    [SerializeField]
    private MageProjectile _mageProjectilePrefab;
    public MageProjectile MageProjectilePrefab => _mageProjectilePrefab;

    [SerializeField]
    private EnergyPulseVFX _energyPulseVFXPrefab;
    public EnergyPulseVFX EnergyPulseVFXPrefab => _energyPulseVFXPrefab;

    public Animator _am;
    AnimatorComponent<ePlayerAction> _animatorComponent;

    private ePlayerAction _currentAction = ePlayerAction.Idle;
    private float _attackAnimEndTime = 0f;
    private bool _pendingAnimRecovery = false;

    private bool _isDead = false;
    public bool IsDead => _isDead;
    public User User => _user;

    /// <summary>기본공격/스킬 애니메이션이 재생 중이면 true. 이동 금지 판정에 사용.</summary>
    public bool IsInAttackAnimation => Time.time < _attackAnimEndTime;

    /// <summary>
    /// 기본공격 사이클(애니메이션 + 쿨타임) 동안 이동 금지를 유지하기 위해
    /// _attackAnimEndTime 을 연장한다. 이미 더 먼 시점이면 유지.
    /// </summary>
    public void ExtendAttackLock(float duration)
    {
        if (duration <= 0f) return;
        float newEnd = Time.time + duration;
        if (newEnd > _attackAnimEndTime)
            _attackAnimEndTime = newEnd;
    }

    [SerializeField]
    private IDamageable _currentTarget;
    public IDamageable currentTarget;
    PlayerData _data;
    private User _user;

    public event Action<IDamageable> OnDeath;

    public ePlayerAction CurrentAction
    {
        get { return _currentAction; }
    }
    public ulong damage
    {
        get { return (ulong)(playerStatus?.Atk ?? 0); }
    }
    public Vector3 targetPos
    {
        get { return transform.position; }
    }
    public Vector3 attackerPos
    {
        get { return transform.position; }
    }
    public GameObject gameobj
    {
        get { return transform.gameObject; }
    }

    /// <summary>현재 HP 비율 (0~1).</summary>
    public float HPRatio
    {
        get
        {
            int maxHP = playerStatus?.MaxHP ?? 1;
            return maxHP > 0 ? (float)_data._Hp / maxHP : 1f;
        }
    }

    public bool TakeDamage(IAttackable attacker)
    {
        ulong dmg = attacker.damage;
        //CustomLogger.Log($"Player가 공격을 받고있습니다! DMG : {dmg}");

        UITKDamageTextBridge.ShowOnTransform(transform, dmg, Color.white);

        bool IsAlive = setHp(dmg);
        if (!IsAlive) return false;
        return true;
    }

    private Vector3 _initialSpawnPos;
    private bool _initialSpawnPosCaptured;

    public void Init(PlayerData data, User user)
    {
        _data = data;
        _user = user;

        if (!_initialSpawnPosCaptured)
        {
            _initialSpawnPos = transform.position;
            _initialSpawnPosCaptured = true;
        }

        KingdomIdle.UIToolkit.UITKEquipmentPanelBridge.Init(this, _user);
    }

    private void OnDead()
    {
        if (_isDead) return;
        _isDead = true;
        CustomLogger.Log("Player Is Dead!!");
        SetAnimation(ePlayerAction.Dead);

        StartCoroutine(PauseAfterDeadAnimation());
    }

    public void ResetTarget(IDamageable target)
    {
        _currentTarget = null;
        currentTarget = null;
    }

    public void SetTarget(IDamageable target)
    {
        if (target == null) return;
        if (_currentTarget != null)
        {
            _currentTarget.OnDeath -= ResetTarget;
        }

        target.OnDeath += ResetTarget;
        _currentTarget = target;
        currentTarget = target;
    }

    private IEnumerator PauseAfterDeadAnimation()
    {
        float deadAnimLength = GetClipLength("Dead_Anim");
        yield return new WaitForSeconds(deadAnimLength);

        OnDeath?.Invoke(this);
        OnDeath = null;
        gameObject.SetActive(false);

        playerOrder?.InterruptBT();

        Scripts.Core.GameManager.Instance?.ReportPlayerDead();
    }

    private bool setHp(ulong damage)
    {
        long totalHp = _data._Hp + _data._extraHp;
        if (totalHp - (long)damage <= 0)
        {
            OnDead();
            return false;
        }

        if ((long)damage > _data._extraHp)
        {
            long remainDamage = (long)damage - _data._extraHp;
            _data._extraHp = 0;
            _data._Hp -= remainDamage;
        }
        else
        {
            _data._extraHp = _data._extraHp - (int)damage;
        }

        return true;
    }

    /// <summary>HP 회복 (IronWill 등).</summary>
    public void Heal(int amount)
    {
        if (_isDead || amount <= 0) return;
        long maxHP = playerStatus?.MaxHP ?? _data._MaxHp;
        _data._Hp = System.Math.Min(_data._Hp + amount, maxHP);
    }

    /// <summary>현재 HP 를 MaxHP 로 채운다 (직업 변경 등 전체 스탯 리셋 시).</summary>
    public void RefillHP()
    {
        if (playerStatus == null) return;
        _data._Hp = playerStatus.MaxHP;
        _data._extraHp = 0;
        playerStatus.HP = playerStatus.MaxHP;
    }

    public void Revive()
    {
        _isDead = false;
        _data._Hp = playerStatus.MaxHP;
        _data._extraHp = 0;
        playerStatus.HP = playerStatus.MaxHP;

        if (_initialSpawnPosCaptured)
            transform.position = _initialSpawnPos;

        gameObject.SetActive(true);
        SetAnimation(ePlayerAction.Idle);
        playerOrder?.Init(this);
        playerOrder?.RecoveryBT();
        Scripts.Core.GameManager.Instance?.ReportPlayerRevived();
        ResetTarget(this);
    }

    private void Awake()
    {
        InitializeAnimator();

        playerStatus = new PlayerStatus();

        equipmentManager = new EquipmentManager();
        equipmentManager.Init(playerStatus, _equipmentDatabase, _equipmentDropTable);

        skillSystem = new SkillSystem(this);

        playerOrder = new PlayerOrder();
        playerOrder.Init(this);

#if UNITY_EDITOR
        _data._Hp = 50;
        _data._atk = 10;
#endif
    }

    private void InitializeAnimator()
    {
        Dictionary<ePlayerAction, int> dic = new Dictionary<ePlayerAction, int>();
        dic.Add(ePlayerAction.Idle, Animator.StringToHash("Idle"));
        dic.Add(ePlayerAction.Walk, Animator.StringToHash("Walk"));
        dic.Add(ePlayerAction.Attack, Animator.StringToHash("Attack"));
        dic.Add(ePlayerAction.Dead, Animator.StringToHash("Dead"));

        _animatorComponent = new AnimatorComponent<ePlayerAction>(_am, dic);
    }

    public void RebuildAnimatorComponent()
    {
        Dictionary<ePlayerAction, int> dic = new Dictionary<ePlayerAction, int>();
        dic.Add(ePlayerAction.Idle, Animator.StringToHash("Idle"));
        dic.Add(ePlayerAction.Walk, Animator.StringToHash("Walk"));
        dic.Add(ePlayerAction.Attack, Animator.StringToHash("Attack"));
        dic.Add(ePlayerAction.Dead, Animator.StringToHash("Dead"));

        _animatorComponent = new AnimatorComponent<ePlayerAction>(_am, dic);
    }

    public float GetClipLength(string clipName, float fallback = 0.4f)
    {
        if (_am == null || _am.runtimeAnimatorController == null)
            return fallback;

        foreach (var clip in _am.runtimeAnimatorController.animationClips)
        {
            if (string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase))
                return clip.length;
        }

        return fallback;
    }

    void Update()
    {
        if (_isDead) return;

        // 스킬 애니메이션(IronWill, EnergyPulse, Tripple_Shot 등)이 끝난 뒤
        // outgoing transition이 없는 상태에서 빠져나오기 위해
        // Attack_Anim(정상 전이가 있는 상태) 끝 지점으로 강제 이동
        if (_pendingAnimRecovery && Time.time >= _attackAnimEndTime)
        {
            _pendingAnimRecovery = false;
            if (_am != null)
                _am.Play(Animator.StringToHash("Attack_Anim"), 0, 1f);
        }

        skillSystem?.Tick();
        playerOrder._rootNode?.Evaluate();
    }

    public void SetAnimation(ePlayerAction next)
    {
        if (next == ePlayerAction.Dead)
        {
            ApplyAnimation(next);
            return;
        }

        if (Time.time < _attackAnimEndTime && next != ePlayerAction.Attack)
            return;

        if (next == _currentAction && next != ePlayerAction.Attack)
            return;

        // 기본공격(BasicAttack → SetAnimation(Attack) 경유) 시
        // 애니메이션 보호 시간을 설정하여 IdleNode 등이 공격을 즉시 덮어쓰지 못하게 한다.
        // PlaySkillAnimation 이 이미 보호 시간을 잡은 경우에는 중복 설정하지 않는다.
        if (next == ePlayerAction.Attack && Time.time >= _attackAnimEndTime)
        {
            _attackAnimEndTime = Time.time + GetClipLength("Attack_Anim", 0.4f);
        }

        ApplyAnimation(next);
    }

    public void PlaySkillAnimation(string stateName, float animProtectDuration = -1f)
    {
        float protect = animProtectDuration > 0f
            ? animProtectDuration
            : GetClipLength("Attack_Anim", 0.4f);
        _attackAnimEndTime = Time.time + protect;

        if (!string.IsNullOrEmpty(stateName) && _am != null)
        {
            if (_currentAction == ePlayerAction.Idle || _currentAction == ePlayerAction.Walk)
                _animatorComponent.TrySetBool(_currentAction, false);

            _am.ResetTrigger(Animator.StringToHash("Attack"));
            _am.Play(Animator.StringToHash(stateName), 0);
            _currentAction = ePlayerAction.Attack;

            // IronWill, EnergyPulse, Tripple_Shot_Anim 등
            // outgoing transition이 없는 상태 → protect 시간 후 Attack_Anim 끝으로 복귀
            _pendingAnimRecovery = true;
        }
        else
        {
            _pendingAnimRecovery = false;
            SetAnimation(ePlayerAction.Attack);
        }
    }

    private void ApplyAnimation(ePlayerAction next)
    {
        if (_currentAction == ePlayerAction.Idle || _currentAction == ePlayerAction.Walk)
            _animatorComponent.TrySetBool(_currentAction, false);

        switch (next)
        {
            case ePlayerAction.Idle:
            case ePlayerAction.Walk:
            case ePlayerAction.Hurt:
                _animatorComponent.TrySetBool(next, true);
                break;
            case ePlayerAction.Attack:
            case ePlayerAction.Dead:
                _animatorComponent.TrySetTrigger(next);
                break;
        }

        _currentAction = next;
    }

    private readonly List<IDamageable> _pendingSkillTargets = new List<IDamageable>();
    private ulong _pendingSkillDamage;
    private bool _hasPendingSkillDamage;

    public void SetPendingSkillDamage(List<IDamageable> targets, int damage)
    {
        _pendingSkillTargets.Clear();
        _pendingSkillTargets.AddRange(targets);
        _pendingSkillDamage = (ulong)damage;
        _hasPendingSkillDamage = _pendingSkillTargets.Count > 0;
    }

    // 스킬 데미지 적용 (Animation Event)
    public void OnSkillHit()
    {
        if (!_hasPendingSkillDamage) return;
        _hasPendingSkillDamage = false;

        for (int i = 0; i < _pendingSkillTargets.Count; i++)
        {
            IDamageable target = _pendingSkillTargets[i];

            var mono = target as MonoBehaviour;
            if (mono == null || !mono.gameObject.activeInHierarchy) continue;

            bool isAlive = target.TakeDamage(new ActiveSkill.DamageProxy(_pendingSkillDamage, this));
            if (!isAlive)
            {
                SetAnimation(ePlayerAction.Idle);
            }
        }
        _pendingSkillTargets.Clear();
    }

    // 기본공격 Animation Event → OnSkillHit 위임
    public void OnAttackHit()
    {
        OnSkillHit();
    }

    private eVFXType _pendingVFXType;
    private readonly List<Vector3> _pendingVFXPositions = new List<Vector3>();
    private readonly List<Transform> _pendingVFXTargets = new List<Transform>();
    private float _pendingVFXFacing;
    private int _pendingVFXDuration;
    private bool _pendingVFXFlip;
    private bool _hasPendingVFX;

    public void SetPendingSkillVFX(eVFXType vfxType, Vector3 vfxPos, float facing, int duration, bool flip, Transform followTarget = null)
    {
        _pendingVFXType = vfxType;
        _pendingVFXPositions.Clear();
        _pendingVFXPositions.Add(vfxPos);
        _pendingVFXTargets.Clear();
        _pendingVFXTargets.Add(followTarget);
        _pendingVFXFacing = facing;
        _pendingVFXDuration = duration;
        _pendingVFXFlip = flip;
        _hasPendingVFX = true;
    }

    public void AddPendingSkillVFXTarget(Vector3 vfxPos, Transform followTarget = null)
    {
        _pendingVFXPositions.Add(vfxPos);
        _pendingVFXTargets.Add(followTarget);
    }

    public void OnSkillVFXStart()
    {
        if (!_hasPendingVFX) return;
        _hasPendingVFX = false;

        eVFXType vfxType = _pendingVFXType;
        float facing = _pendingVFXFacing;
        int duration = _pendingVFXDuration;
        bool flipVFX = _pendingVFXFlip;

        for (int i = 0; i < _pendingVFXPositions.Count; i++)
        {
            Vector3 capturedPos = _pendingVFXPositions[i];
            Transform capturedTarget = i < _pendingVFXTargets.Count ? _pendingVFXTargets[i] : null;
            VFXManager.Instance?.GetVFX(vfxType, capturedPos, Quaternion.identity,
                (vfx) =>
                {
                    if (vfx == null) return;
                    Vector3 s = vfx.transform.localScale;
                    bool flip = facing >= 0f ? flipVFX : !flipVFX;
                    s.x = flip ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
                    vfx.transform.localScale = s;
                    vfx.ActiveEffect(duration, capturedTarget);
                });
        }
    }

    public void GiveReward(int gold, int ancientCoin)
    {
        _user.GainCoin(eCurrency.Gold, gold);
        _user.GainCoin(eCurrency.AncientCoin, ancientCoin);

        equipmentManager?.TryDropEquipment();
        return;
    }

    public bool Attack(IDamageable target)
    {
        return true;
    }

    public ulong GetTypeId()
    {
        return 0;
    }
}
