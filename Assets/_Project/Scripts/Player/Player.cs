using KingdomIdle.UGUI;
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
    //Events
    public event Action<IDamageable> OnDeath;

    //Public Property
    public User User => _user;
    public bool IsDead => _isDead;
    public ePlayerAction CurrentAction => _currentAction; 
    public ulong damage => (ulong)(playerStatus?.Atk ?? 0);
    public Vector3 targetPos => transform.position;
    public Vector3 attackerPos => transform.position;
    public GameObject gameobj => transform.gameObject;
    public int PlayerIndex => _data._index;    
    /// <summary>현재 공격 애니메이션의 재생 여부를 반환</summary>
    public bool IsInAttackAnimation => Time.time < _attackAnimEndTime;
    public MageProjectile MageProjectilePrefab => _mageProjectilePrefab;
    public EnergyPulseVFX EnergyPulseVFXPrefab => _energyPulseVFXPrefab;
   
    //Public Variables
    public PlayerOrder playerOrder;
    public PlayerStatus playerStatus;
    public SkillSystem skillSystem;   
    public Animator _am;
    public IDamageable currentTarget;
    public PlayerEquipmentManager PlayerEquipmentManager;
    
    /// <summary>현재 HP 비율 (0~1).</summary>
    public float HPRatio
    {
        get
        {
            int maxHP = playerStatus?.MaxHP ?? 1;
            return maxHP > 0 ? (float)_data._Hp / maxHP : 1f;
        }
    }
    
    [SerializeField]
    private EquipmentDatabase _equipmentDatabase;
    [SerializeField]
    private EquipmentDropTableSO _equipmentDropTable;
    [SerializeField]
    private MageProjectile _mageProjectilePrefab;
    [SerializeField]
    private EnergyPulseVFX _energyPulseVFXPrefab; 
    [SerializeField]
    private IDamageable _currentTarget;    
    
    //Data
    private PlayerData _data;
    private User _user;
    private bool _isDead;
    private const float MAX_ATTACK_ANIMATION_SPEED = 3f; //스킬모션 최대 스피드(3배까지) 
    private const float MIN_SKILL_INTERVAL = 0.1f; //스킬모션 최소 작동시간(0.1초까지)

    //Animation
    private AnimatorComponent<ePlayerAction> _animatorComponent;
    private ePlayerAction _currentAction = ePlayerAction.Idle;
    private float _attackAnimEndTime;
    private bool _pendingAnimRecovery;
    private static readonly int AttackStateHash = Animator.StringToHash("Attack_Anim");
    private static readonly int AttackAnimationSpeedHash = Animator.StringToHash("AttackAnimSpeed");
    
    //Skill
    private readonly List<IDamageable> _pendingSkillTargets = new List<IDamageable>();
    private ulong _pendingSkillDamage;
    private bool _hasPendingSkillDamage;
    
    //SpawnPos
    private Vector3 _initialSpawnPos;
    private bool _initialSpawnPosCaptured;
    
    //VFX
    [NonSerialized] 
    private eVFXType _pendingVFXType;
    private readonly List<Vector3> _pendingVFXPositions = new List<Vector3>();
    private readonly List<Transform> _pendingVFXTargets = new List<Transform>();
    private float _pendingVFXFacing;
    private int _pendingVFXDuration;
    private bool _pendingVFXFlip;
    private bool _hasPendingVFX;    
    
    #region Unity Life Cycle
    private void Awake()
    {
        InitializeAnimator();

        playerStatus = new PlayerStatus();

        PlayerEquipmentManager = new PlayerEquipmentManager();

        skillSystem = new SkillSystem(this);

        playerOrder = new PlayerOrder();
        playerOrder.Init(this);

        #if UNITY_EDITOR
            _data._Hp = 50;
            _data._atk = 10;
        #endif
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
    #endregion

    #region Animation
    public readonly struct AttackAnimationTiming
    {
        public readonly float AnimationDuration;
        public readonly float EffectiveInterval;
        public readonly float PlaybackSpeed;

        public AttackAnimationTiming(
            float animationDuration,
            float effectiveInterval,
            float playbackSpeed)
        {
            AnimationDuration = animationDuration;
            EffectiveInterval = effectiveInterval;
            PlaybackSpeed = playbackSpeed;
        }
    }
    
    public AttackAnimationTiming PlayBasicSkillAnimation(
        float requestedInterval)
    {
        float clipLength = GetClipLength("Attack_Anim", 0.4f);

        float safeInterval = Mathf.Max(
            requestedInterval,
            MIN_SKILL_INTERVAL);

        // 간격보다 클립이 길 때만 애니메이션을 빠르게 한다.
        float requiredSpeed = clipLength / safeInterval;
        float playbackSpeed = Mathf.Clamp(
            requiredSpeed,
            1f,
            MAX_ATTACK_ANIMATION_SPEED);

        float animationDuration = clipLength / playbackSpeed;

        // 3배속으로도 모션을 완료할 수 없는 간격이면 실제 스킬 발동 간격도 애니메이션 완료 시간으로 제한
        float effectiveInterval = Mathf.Max(
            safeInterval,
            animationDuration);

        _pendingAnimRecovery = false;
        _attackAnimEndTime = Time.time + animationDuration;

        if (_currentAction == ePlayerAction.Idle ||
            _currentAction == ePlayerAction.Walk)
        {
            _animatorComponent.TrySetBool(_currentAction, false);
        }

        _am.SetFloat(AttackAnimationSpeedHash, playbackSpeed);

        // 바로 전 공격 상태가 아직 남아 있어도 항상 처음부터 재생
        _am.Play(AttackStateHash, 0, 0f);

        _currentAction = ePlayerAction.Attack;

        return new AttackAnimationTiming(
            animationDuration,
            effectiveInterval,
            playbackSpeed);
    }
    
    private IEnumerator PauseAfterDeadAnimation()
    {
        float deadAnimLength = GetClipLength("Dead_Anim");
        yield return new WaitForSeconds(deadAnimLength);

        OnDeath?.Invoke(this);
        OnDeath = null;
        gameObject.SetActive(false);
        playerOrder?.InterruptBT();

        GameManager.Instance?.ReportPlayerDead();
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


    #endregion

    #region Battle

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

    public bool TakeDamage(IAttackable attacker)
    {
        ulong dmg = attacker.damage;

        DamageTextBridge.ShowOnTransform(transform, dmg, Color.white);

        bool IsAlive = setHp(dmg);
        if (!IsAlive) return false;
        return true;
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
            _data._extraHp -= (int)damage;
        }

        return true;
    }
    
    public bool Attack(IDamageable target)
    {
        return true;
    }

    // 현재 탐색된 대상이 사망 / 감지불가 상태가 될 때 현재 타겟을 리셋
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
        GameManager.Instance?.ReportPlayerRevived();
        ResetTarget(this);
    }

    public void SetPendingSkillDamage(List<IDamageable> targets, int damage)
    {
        _pendingSkillTargets.Clear();
        _pendingSkillTargets.AddRange(targets);
        _pendingSkillDamage = (ulong)damage;
        _hasPendingSkillDamage = _pendingSkillTargets.Count > 0;
    }

    public void OnProjectileRelease()
    {
        skillSystem?.HandleProjectileRelease();
    }
    
    // 기본공격 Animation Event → OnSkillHit 위임
    public void OnAttackHit() => OnSkillHit();
    
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


    #endregion

    /*
     * 실행 시점
     * SceneManager.sceneLoaded이벤트 발행
     * -> GameManager.HandleSceneReadyAsync()
     * -> GameManager.HandleMainSceneReady()
     * -> UserManager.CreateCharacter()
     * -> Player.Init()
     */
    public void Init(PlayerData data, User user)
    {
        _data = data;
        _user = user;

        if (!_initialSpawnPosCaptured)
        {
            _initialSpawnPos = transform.position;
            _initialSpawnPosCaptured = true;
        }
        //각 플레이어별 장비 매니저 초기화용 호출. Awake에서는 _data가 초기화되어있지 않아 init에서 실행
        PlayerEquipmentManager.Init(playerStatus, _data._index);
    }

    private void OnDead()
    {
        if (_isDead) return;
        _isDead = true;
        CustomLogger.Log("Player Is Dead!!");
        SetAnimation(ePlayerAction.Dead);

        StartCoroutine(PauseAfterDeadAnimation());
    }

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

        EquipmentManager.Instance.TryDropEquipment();
    }



    public ulong GetTypeId()
    {
        return 0;
    }
}
