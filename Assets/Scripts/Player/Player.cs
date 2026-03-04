using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Core.Utils;
using Scripts.Users;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour, IAttackable, IDamageable, IRewardable
{
    public PlayerOrder playerOrder;
    public PlayerStatus playerStatus;
    public SkillManager skillManager;
    public SkillDatabase skillDatabase;

    // 애니메이터 관련
    public Animator _am;
    AnimatorComponent<ePlayerAction> _animatorComponent;

    // 행동 트리에서 사용할 행동 상태 변수
    public ePlayerAction _prevAction;
    public ePlayerAction _playerAction;

    public IDamageable currentTarget;
    PlayerData _data;
    private User _user;
    // PlayerStatus.Atk 기반 데미지 (기본 공격력)
    public ulong damage
    {
        get
        {
            return (ulong)(playerStatus?.Atk ?? 0);
        }
    }
    public Vector3 targetPos
    {
        get
        {
            return transform.position;
        }
    }
    public Vector3 attackerPos
    {
        get
        {
            return transform.position;
        }
    }
    public bool TakeDamage(IAttackable attacker)
    {
        ulong dmg = attacker.damage;
        CustomLogger.Log($"Player가 공격을 받고있습니다! DMG : {dmg}");
        bool IsAlive = setHp(dmg);

        if (!IsAlive)
        {   
            // 죽었을때
            return false;
        }

        return true;
    }


    public void Init(PlayerData data, User user)
    {
        _data = data;
        _user = user;
    }
    private void OnDead()
    {
        CustomLogger.Log("Player Is Dead!!");
        _playerAction = ePlayerAction.Dead;
        TurnOnAnimation(_playerAction);
    }
    private bool setHp(ulong damage)
    {
        long totalHp = _data._Hp + _data._extraHp;
        //죽는경우
        if (totalHp - (long)damage <= 0)
        {
            OnDead();
            return false;
        }

        //ExtraHp먼저 깍기
        if ((long)damage > _data._extraHp)
        {
            long remainDamage = (long)damage - _data._extraHp;
            _data._extraHp = 0;
            _data._Hp -= remainDamage;
            return true;
        }

        _data._extraHp = _data._extraHp - (int)damage;
        return true;
    }
    // 초기화 함수
    private void Awake()
    {
        InitializeAnimator();

        playerStatus = new PlayerStatus();

        playerOrder = new PlayerOrder();
        playerOrder.Init(this);

        // [공격 속도 자동 동기화]
        // Animator에 등록된 "Attack" 클립의 실제 길이를 읽어 attackRate에 주입한다.
        // 덕분에 Animation 창에서 클립 길이를 수정해도 코드 변경 없이 공격 속도가 자동으로 맞춰진다.
        //
        // ⚠ 단점
        // 1. 클립 이름이 "Attack"과 정확히 일치해야 동작함 (대소문자 포함)
        //    → 클립 이름이 바뀌면 여기 문자열도 같이 바꿔야 함
        // 2. runtimeAnimatorController의 모든 클립을 순회하므로,
        //    클립 수가 많아질수록 Awake 초기화 시간이 약간 늘어날 수 있음 (미미하지만 주의)
        // 3. Animator가 인스펙터에 연결되지 않은 채 실행되면 fallback(0.4f) 값으로 동작함
        float attackClipLength = GetClipLength("Attack_Anim");
        playerOrder._attack.attackRate = attackClipLength;

        //For Test (에디터 전용 — 빌드에서는 Init()으로 주입)
#if UNITY_EDITOR
        _data._Hp = 50;
        _data._atk = 10;
#endif

    }

    // 애니메이터 초기화 함수
    private void InitializeAnimator()
    {
        Dictionary<ePlayerAction, int> dic = new Dictionary<ePlayerAction, int>();
        dic.Add(ePlayerAction.Idle, Animator.StringToHash("Idle"));
        dic.Add(ePlayerAction.Walk, Animator.StringToHash("Walk"));
        dic.Add(ePlayerAction.Attack, Animator.StringToHash("Attack"));
        dic.Add(ePlayerAction.Dead, Animator.StringToHash("Dead"));

        _animatorComponent = new AnimatorComponent<ePlayerAction>(_am, dic);
    }

    /// <summary>
    /// 전직 시 AnimatorController가 교체된 뒤 호출한다.
    /// AnimatorComponent 내부의 해시 캐시를 새 컨트롤러 기준으로 재구성한다.
    /// ChangeJob.ApplyJobByIndex()에서 호출된다.
    /// </summary>
    public void RebuildAnimatorComponent()
    {
        Dictionary<ePlayerAction, int> dic = new Dictionary<ePlayerAction, int>();
        dic.Add(ePlayerAction.Idle,   Animator.StringToHash("Idle"));
        dic.Add(ePlayerAction.Walk,   Animator.StringToHash("Walk"));
        dic.Add(ePlayerAction.Attack, Animator.StringToHash("Attack"));
        dic.Add(ePlayerAction.Dead,   Animator.StringToHash("Dead"));

        _animatorComponent = new AnimatorComponent<ePlayerAction>(_am, dic);

        // 새 컨트롤러의 Attack 클립 길이를 읽어 attackRate 재동기화
        float clipLen = GetClipLength("Attack_Anim");
        if (playerOrder?._attack != null)
            playerOrder._attack.attackRate = clipLen;

        Debug.Log($"[Player] AnimatorComponent 재구성 완료. Attack 클립: {clipLen}초");
    }

    /// <summary>
    /// Animator에 등록된 AnimationClip 중 clipName과 정확히 일치하는 클립의 길이(초)를 반환한다.
    ///
    /// [동작 원리]
    /// runtimeAnimatorController에 연결된 모든 AnimationClip을 순회하여
    /// clip.name이 clipName과 일치하는 항목의 clip.length를 반환한다.
    ///
    /// [단점]
    /// - 클립 이름 기반 탐색이므로 이름이 바뀌면 찾지 못하고 fallback 반환
    /// - Animator Override Controller를 사용하는 경우 오버라이드된 클립이 반영되지 않을 수 있음
    /// - Awake 시점에만 호출되므로 런타임 도중 클립이 교체되어도 attackRate는 갱신되지 않음
    /// </summary>
    /// <param name="clipName">찾을 AnimationClip의 이름 (대소문자 구분)</param>
    /// <param name="fallback">클립을 찾지 못했을 때 반환할 기본 길이 (초)</param>
    private float GetClipLength(string clipName, float fallback = 0.4f)
    {
        // Animator 또는 컨트롤러가 인스펙터에 연결되지 않은 경우
        if (_am == null || _am.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[Player] Animator가 없어 '{clipName}' 클립 길이를 읽을 수 없습니다. 기본값 {fallback}초 사용.");
            return fallback;
        }

        // 등록된 모든 클립을 순회하며 이름이 일치하는 클립 탐색
        foreach (var clip in _am.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                Debug.Log($"[Player] '{clipName}' 클립 길이: {clip.length}초 → attackRate에 적용");
                return clip.length;
            }
        }

        // 일치하는 클립을 찾지 못한 경우 fallback 반환
        Debug.LogWarning($"[Player] '{clipName}' 클립을 찾지 못했습니다. 기본값 {fallback}초 사용.");
        return fallback;
    }

    // 행동 트리에서 플레이어 행동 상태가 변경될 때마다 애니메이션 업데이트
    private void UpdateAnimation()
    {
        if (_prevAction != _playerAction)
        {
            TurnOffAnimation(_prevAction);
            TurnOnAnimation(_playerAction);

            // 변경 후 이전 상태를 현재 상태로 갱신
            _prevAction = _playerAction;
        }
    }

    // 행동 상태에 따른 애니메이션 제어 함수 (애니메이션 끄기)
    public void TurnOffAnimation(ePlayerAction action)
    {
        switch (action)
        {
            case ePlayerAction.Idle:
            case ePlayerAction.Walk:
            case ePlayerAction.Attack:
                _animatorComponent.TrySetBool(action, false);
                break;
        }
    }

    // 행동 상태에 따른 애니메이션 제어 함수 (애니메이션 켜기)
    public void TurnOnAnimation(ePlayerAction action)
    {
        switch (action)
        {
            case ePlayerAction.Idle:
            case ePlayerAction.Walk:
            case ePlayerAction.Attack:
            case ePlayerAction.Hurt:
                _animatorComponent.TrySetBool(action, true);
                break;
            case ePlayerAction.Dead:
                _animatorComponent.TrySetTrigger(action);
                break;
        }
    }

    void Update()
    {
        // 플레이어 행동 트리 평가
        playerOrder._rootNode?.Evaluate();
    }
    // LateUpdate에서 애니메이션 업데이트 호출 (Update에서 행동 트리 평가 후 애니메이션 상태 변경)
    private void LateUpdate()
    {
        UpdateAnimation();
    }

    // 공격 애니메이션 재생 (데미지와 독립적, 시각적 피드백만 담당)
    public void PlayAttackAnimation()
    {
        _playerAction = ePlayerAction.Attack;
        TurnOnAnimation(_playerAction);
    }

    /// <summary>
    /// Animation Event 전용.
    /// Attack_Anim의 타격 프레임에 이 함수를 등록하면 정확한 타이밍에 데미지가 들어간다.
    /// Unity Animation 창 → 원하는 프레임 → Add Event → 함수 목록에서 "OnAttackHit" 선택.
    /// </summary>
    public void OnAttackHit()
    {
        playerOrder?._attack?.DealDamage();
    }

	public void GiveReward(int gold, int ancientCoin)
	{
        _user.GainCoin(eCurrency.Gold, gold);
        _user.GainCoin(eCurrency.AncientCoin, ancientCoin);
        return;
	}

	public bool Attack(IDamageable target)
	{
        return true;
	}
}
