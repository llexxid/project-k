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
    public SkillManager skillManager;
    public SkillDatabase skillDatabase;

    /// <summary>장비 시스템 메인 매니저. UI나 외부 로직에서 참조 가능.</summary>
    public EquipmentManager equipmentManager;

    [SerializeField]
    [Tooltip("EquipmentDatabase.asset을 인스펙터에서 연결하세요.")]
    private EquipmentDatabase _equipmentDatabase;

    [SerializeField]
    [Tooltip("EquipmentDropTableSO.asset을 인스펙터에서 연결하세요.")]
    private EquipmentDropTableSO _equipmentDropTable;

    public Animator _am;
    AnimatorComponent<ePlayerAction> _animatorComponent;

    // 현재 재생 중인 애니메이션 상태 (SetAnimation의 단일 진입점이 관리)
    private ePlayerAction _currentAction = ePlayerAction.Idle;

    // 공격 애니메이션 보호 타이머: 이 시간 동안 Idle/Walk가 애니메이션을 덮어쓰지 못함
    private float _attackAnimEndTime = 0f;

    // 보호막 패시브 발동 여부 (전직 시 리셋, 직업당 1회)
    private bool _shieldPassiveActivated = false;

    // 패링 상태
    private float _parryEndTime        = 0f;
    private float _parryCounterMultiplier = 0f;
    public bool IsParrying => Time.time < _parryEndTime;

    // 사망 여부 (true이면 BT 평가 중단)
    private bool _isDead = false;
    /// <summary>외부 컴포넌트(ChangeJob 등)에서 사망 여부를 읽기 위한 프로퍼티</summary>
    public bool IsDead => _isDead;
    /// <summary>전직 비용 차감 등 외부에서 User에 접근할 때 사용</summary>
    public User User => _user;

    [SerializeField]
    private IDamageable _currentTarget;
    public IDamageable currentTarget;
    PlayerData _data;
    private User _user;

	public event Action OnDeath;

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

	public bool TakeDamage(IAttackable attacker)
    {
        // 패링 중 피격: 데미지 무효화 + 반격
        if (IsParrying)
        {
            ulong counterDamage = (ulong)Mathf.RoundToInt(playerStatus.Atk * _parryCounterMultiplier);
            var damageable = attacker as IDamageable;
            damageable?.TakeDamage(new PlayerSkill.DamageProxy(counterDamage, gameobj));

            UITKDamageTextBridge.ShowOnTransform(transform, (ulong)counterDamage, Color.yellow);
            Debug.Log($"[Player] 패링 성공! 반격 데미지: {counterDamage}");
            return true;
        }

        ulong dmg = attacker.damage;
        CustomLogger.Log($"Player가 공격을 받고있습니다! DMG : {dmg}");

        // 플레이어 피격 데미지 텍스트 (하얀색)
        UITKDamageTextBridge.ShowOnTransform(transform, dmg, Color.white);

        // ── [DEBUG] 플레이어 무적 — 제거 시 이 블록 삭제 ──
        if (UITKDebugMenuController.PlayerInvincible) return true;

        bool IsAlive = setHp(dmg);
        if (!IsAlive) return false;
        return true;
    }

    public void Init(PlayerData data, User user)
    {
        _data = data;
        _user = user;

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

    public void ResetTarget()
    {
        Debug.Log($"Player : {gameObject.GetInstanceID()} Reset Target");
        _currentTarget = null;
        currentTarget = null;
	}

    public void SetTarget(IDamageable target)
    {
        if (target == null)
        {
            return;
        }
        Debug.Log($"Player : {gameObject.GetInstanceID()} |Player SetMonster : {target.gameobj.GetInstanceID()} | target_action : {target.gameobj.GetComponent<Monster>().MonAction}");
        target.OnDeath += ResetTarget;
        _currentTarget = target;
        currentTarget = target;
    }

    private IEnumerator PauseAfterDeadAnimation()
    {
        float deadAnimLength = GetClipLength("Dead_Anim");
        Debug.Log($"[Player] 사망 애니메이션 대기: {deadAnimLength}초");
        yield return new WaitForSeconds(deadAnimLength);

		OnDeath?.Invoke();
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

        TryActivateShieldPassive();
        return true;
    }

    /// <summary>
    /// HP 감소 후 호출. 보호막 패시브 조건을 충족하면 보호막을 부여한다.
    /// 전직당 1회만 발동된다 (ResetShieldPassive로 리셋).
    /// </summary>
    private void TryActivateShieldPassive()
    {
        if (_shieldPassiveActivated) return;
        if (skillManager == null || playerStatus == null) return;

        float hpRatio = (float)_data._Hp / playerStatus.MaxHP;

        foreach (var skill in skillManager.GetCurrentSkills())
        {
            if (skill.skillType != SkillType.Passive) continue;
            if (skill.passiveShieldAmount <= 0) continue;
            if (hpRatio > skill.passiveShieldHPThreshold) continue;

            _data._extraHp += skill.passiveShieldAmount;
            _shieldPassiveActivated = true;
            Debug.Log($"[Player] 보호막 패시브 발동: +{skill.passiveShieldAmount} " +
                      $"(현재 HP {hpRatio:P0} ≤ 임계값 {skill.passiveShieldHPThreshold:P0})");
            break;
        }
    }

    /// <summary>
    /// 전직 시 ChangeJob에서 호출. 보호막 패시브 발동 기록을 초기화한다.
    /// </summary>
    public void ResetShieldPassive()
    {
        _shieldPassiveActivated = false;
    }

    /// <summary>
    /// PlayerParry에서 호출. duration 초 동안 패링 상태를 활성화한다.
    /// 패링 중 TakeDamage()가 호출되면 데미지 무효화 + 반격이 발동된다.
    /// </summary>
    public void ActivateParry(float duration, float counterMultiplier)
    {
        _parryEndTime          = Time.time + duration;
        _parryCounterMultiplier = counterMultiplier;
    }

    private void Awake()
    {
        InitializeAnimator();

        playerStatus = new PlayerStatus();

        equipmentManager = new EquipmentManager();
        equipmentManager.Init(playerStatus, _equipmentDatabase, _equipmentDropTable);

        playerOrder = new PlayerOrder();
        playerOrder.Init(this);

        float attackClipLength = GetClipLength("Attack_Anim");
        playerOrder._attack.attackRate = attackClipLength;

        //For Test (에디터 전용 — 빌드에서는 Init()으로 주입)
#if UNITY_EDITOR
        _data._Hp = 50;
        _data._atk = 10;
#endif
    }

    private void InitializeAnimator()
    {
        Dictionary<ePlayerAction, int> dic = new Dictionary<ePlayerAction, int>();
        dic.Add(ePlayerAction.Idle,   Animator.StringToHash("Idle"));
        dic.Add(ePlayerAction.Walk,   Animator.StringToHash("Walk"));
        dic.Add(ePlayerAction.Attack, Animator.StringToHash("Attack"));
        dic.Add(ePlayerAction.Dead,   Animator.StringToHash("Dead"));

        _animatorComponent = new AnimatorComponent<ePlayerAction>(_am, dic);
    }

    /// <summary>
    /// 전직 시 AnimatorController가 교체된 뒤 호출한다.
    /// </summary>
    public void RebuildAnimatorComponent()
    {
        Dictionary<ePlayerAction, int> dic = new Dictionary<ePlayerAction, int>();
        dic.Add(ePlayerAction.Idle,   Animator.StringToHash("Idle"));
        dic.Add(ePlayerAction.Walk,   Animator.StringToHash("Walk"));
        dic.Add(ePlayerAction.Attack, Animator.StringToHash("Attack"));
        dic.Add(ePlayerAction.Dead,   Animator.StringToHash("Dead"));

        _animatorComponent = new AnimatorComponent<ePlayerAction>(_am, dic);

        float clipLen = GetClipLength("Attack_Anim");
        if (playerOrder?._attack != null)
            playerOrder._attack.attackRate = clipLen;

        Debug.Log($"[Player] AnimatorComponent 재구성 완료. Attack 클립: {clipLen}초");
    }

    private float GetClipLength(string clipName, float fallback = 0.4f)
    {
        if (_am == null || _am.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[Player] Animator가 없어 '{clipName}' 클립 길이를 읽을 수 없습니다. 기본값 {fallback}초 사용.");
            return fallback;
        }

        foreach (var clip in _am.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                Debug.Log($"[Player] '{clipName}' 클립 길이: {clip.length}초");
                return clip.length;
            }
        }

        Debug.LogWarning($"[Player] '{clipName}' 클립을 찾지 못했습니다. 기본값 {fallback}초 사용.");
        return fallback;
    }

    void Update()
    {
        if (_isDead) return;
        playerOrder._rootNode?.Evaluate();
    }

    /// <summary>
    /// 애니메이션 상태 변경의 단일 진입점.
    /// - 공격 애니메이션 재생 중에는 Idle/Walk 요청을 무시한다 (Attack 보호).
    /// - 현재 상태와 동일하면 중복 적용하지 않는다 (Attack/Dead Trigger 제외).
    /// - 이전 상태의 bool을 끄고 새 상태의 bool/trigger를 켠다.
    /// BT 리프 노드는 _playerAction을 직접 건드리지 않고 이 메서드만 호출한다.
    /// </summary>
    public void SetAnimation(ePlayerAction next)
    {
        // Dead는 항상 즉시 적용
        if (next == ePlayerAction.Dead)
        {
            ApplyAnimation(next);
            return;
        }

        // 공격 애니메이션 재생 중에는 Idle/Walk 무시
        if (Time.time < _attackAnimEndTime
            && next != ePlayerAction.Attack)
        {
            return;
        }

        // 동일 상태 중복 적용 방지 (Trigger인 Attack은 매번 발동해야 하므로 예외)
        if (next == _currentAction && next != ePlayerAction.Attack)
            return;

        ApplyAnimation(next);
    }

    /// <summary>
    /// 공격 애니메이션 재생. 타이머를 설정한 뒤 SetAnimation에 위임한다.
    /// PlayerAttack에서 호출한다.
    /// </summary>
    public void PlayAttackAnimation()
    {
        // 타이머를 먼저 설정해야 SetAnimation 내부에서 보호가 즉시 유효해짐
        _attackAnimEndTime = Time.time + (playerOrder?._attack?.attackRate ?? 0.4f);
        SetAnimation(ePlayerAction.Attack);
    }

    /// <summary>
    /// 스킬 전용 애니메이션 재생.
    /// stateName이 있으면 Animator 상태를 직접 재생하고, 비어있으면 기본 Attack으로 폴백한다.
    /// PlayerSkill에서 호출한다.
    /// </summary>
    public void PlaySkillAnimation(string stateName)
    {
        _attackAnimEndTime = Time.time + (playerOrder?._attack?.attackRate ?? 0.4f);

        if (!string.IsNullOrEmpty(stateName) && _am != null)
        {
            // 이전 bool 상태(Idle/Walk) 해제
            if (_currentAction == ePlayerAction.Idle || _currentAction == ePlayerAction.Walk)
                _animatorComponent.TrySetBool(_currentAction, false);

            _am.Play(Animator.StringToHash(stateName));
            _currentAction = ePlayerAction.Attack;
        }
        else
        {
            SetAnimation(ePlayerAction.Attack);
        }
    }

    private void ApplyAnimation(ePlayerAction next)
    {
        // 이전 bool 기반 상태 끄기 (Idle, Walk만 bool 파라미터)
        if (_currentAction == ePlayerAction.Idle || _currentAction == ePlayerAction.Walk)
            _animatorComponent.TrySetBool(_currentAction, false);

        // 새 상태 켜기
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

    // PlayerSkill이 애니메이션 트리거 직전에 채워두는 스킬 데미지 대기 데이터
    private readonly List<IDamageable> _pendingSkillTargets = new List<IDamageable>();
    private ulong  _pendingSkillDamage;
    private bool _hasPendingSkillDamage;

    /// <summary>
    /// PlayerSkill이 애니메이션 트리거 전에 호출. 타격 대상과 데미지를 등록한다.
    /// </summary>
    public void SetPendingSkillDamage(List<IDamageable> targets, int damage)
    {
        _pendingSkillTargets.Clear();
        _pendingSkillTargets.AddRange(targets);
        _pendingSkillDamage      = (ulong)damage;
        _hasPendingSkillDamage   = _pendingSkillTargets.Count > 0;
    }

    /// <summary>
    /// Animation Event 전용.
    /// 스킬 애니메이션의 타격 프레임에 등록한다.
    /// </summary>
    public void OnSkillHit()
    {
        if (!_hasPendingSkillDamage) return;
        _hasPendingSkillDamage = false;

        for (int i = 0; i < _pendingSkillTargets.Count; i++)
        {
            IDamageable target = _pendingSkillTargets[i];

            // 이미 비활성화된 오브젝트(사망 등) 건너뜀
            var mono = target as MonoBehaviour;
            if (mono == null || !mono.gameObject.activeInHierarchy) continue;

            bool isAlive = target.TakeDamage(new PlayerSkill.DamageProxy(_pendingSkillDamage, gameobj));
            if (!isAlive)
            {
                SetAnimation(ePlayerAction.Idle);
                currentTarget = null;
            }
        }
        _pendingSkillTargets.Clear();
    }

    // PlayerSkill이 애니메이션 트리거 직전에 채워두는 VFX 대기 데이터
    private eVFXType _pendingVFXType;
    private readonly List<Vector3> _pendingVFXPositions = new List<Vector3>(); // Execute() 시점에 확정된 월드 좌표 목록
    private float    _pendingVFXFacing;   // 방향 반전 판단용 (scale.x 부호)
    private int      _pendingVFXDuration;
    private bool     _pendingVFXFlip;
    private bool     _hasPendingVFX;

    /// <summary>
    /// PlayerSkill이 애니메이션 트리거 전에 호출.
    /// VFX 위치는 Execute() 시점에 확정해서 저장한다.
    /// </summary>
    public void SetPendingSkillVFX(eVFXType vfxType, Vector3 vfxPos, float facing, int duration, bool flip)
    {
        _pendingVFXType     = vfxType;
        _pendingVFXPositions.Clear();
        _pendingVFXPositions.Add(vfxPos);
        _pendingVFXFacing   = facing;
        _pendingVFXDuration = duration;
        _pendingVFXFlip     = flip;
        _hasPendingVFX      = true;
    }

    /// <summary>
    /// vfxOnTarget 스킬에서 추가 타겟 위치를 등록할 때 호출.
    /// SetPendingSkillVFX 호출 이후에 사용해야 한다.
    /// </summary>
    public void AddPendingSkillVFXTarget(Vector3 vfxPos)
    {
        _pendingVFXPositions.Add(vfxPos);
    }

    /// <summary>
    /// Animation Event 전용.
    /// 스킬 공격 애니메이션의 VFX 시작 프레임에 등록한다.
    /// </summary>
    public void OnSkillVFXStart()
    {
        if (!_hasPendingVFX) return;
        _hasPendingVFX = false;

        eVFXType vfxType = _pendingVFXType;
        float    facing  = _pendingVFXFacing;
        int      duration = _pendingVFXDuration;
        bool     flipVFX = _pendingVFXFlip;

        foreach (Vector3 vfxPos in _pendingVFXPositions)
        {
            Vector3 capturedPos = vfxPos;
            VFXManager.Instance?.GetVFX(vfxType, capturedPos, Quaternion.identity,
                (vfx) =>
                {
                    if (vfx == null) return;
                    Vector3 s    = vfx.transform.localScale;
                    bool    flip = facing >= 0f ? flipVFX : !flipVFX;
                    s.x = flip ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
                    vfx.transform.localScale = s;
                    vfx.ActiveEffect(duration);
                });
        }
    }

    /// <summary>
    /// Animation Event 전용.
    /// Attack_Anim의 타격 프레임에 이 함수를 등록하면 정확한 타이밍에 데미지가 들어간다.
    /// </summary>
    public void OnAttackHit()
    {
        playerOrder?._attack?.DealDamage();
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
}
