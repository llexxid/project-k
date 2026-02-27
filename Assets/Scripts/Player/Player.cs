using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Core.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour, IAttackable, IDamageable
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
	public ulong damage 
    {
        get 
        {
            return (_data._atk + _data._extraAtk); 
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

        CustomLogger.Log($"Player HP : {_data._Hp}");
		if (!IsAlive)
		{   
            //죽었을때?
			return false;
		}
		return true;
	}

	public void Init(PlayerData data)
	{
        _data = data;
	}
    private void OnDead()
    {
		CustomLogger.Log("Player Is Dead!!");
		_playerAction = ePlayerAction.Dead;
        //gameObject.SetActive(false);
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

        playerOrder = new PlayerOrder();
        playerOrder.Init(this);

        //For Test 
        _data._Hp = 50;
        _data._atk = 10;

	}

    // 애니메이터 초기화 함수
    private void InitializeAnimator()
    {
        Dictionary<ePlayerAction, int> dic = new Dictionary<ePlayerAction, int>();
        dic.Add(ePlayerAction.Idle, Animator.StringToHash("Idle"));
        dic.Add(ePlayerAction.Walk, Animator.StringToHash("Walk"));
        dic.Add(ePlayerAction.Attack, Animator.StringToHash("Attack"));
        dic.Add(ePlayerAction.Dead, Animator.StringToHash("Dead"));
        dic.Add(ePlayerAction.Hurt, Animator.StringToHash("Hurt"));

        _animatorComponent = new AnimatorComponent<ePlayerAction>(_am, dic);
    }

    // 행동 트리에서 플레이어 행동 상태가 변경될 때마다 애니메이션 업데이트
    private void UpdateAnimation()
    {
        if (_prevAction != _playerAction)
        {
            TurnOffAnimation(_prevAction);
            TurnOnAnimation(_playerAction);
        }
    }

    // 행동 상태에 따른 애니메이션 제어 함수 (애니메이션 끄기)
    private void TurnOffAnimation(ePlayerAction action)
    {
        switch (action)
        {
            case ePlayerAction.Idle:
            case ePlayerAction.Attack:
            case ePlayerAction.Hurt:
                _animatorComponent.TrySetBool(action, false);
                /*** Fall through ***/
                break;
        }
    }

    // 행동 상태에 따른 애니메이션 제어 함수 (애니메이션 켜기)
    private void TurnOnAnimation(ePlayerAction action)
    {
        switch (action)
        {
            case ePlayerAction.Idle:
            case ePlayerAction.Attack:
            case ePlayerAction.Hurt:
                _animatorComponent.TrySetBool(action, true);
                break;
            /*** Fall through ***/
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

	public bool Attack(IDamageable target)
	{
		throw new System.NotImplementedException();
	}
}
