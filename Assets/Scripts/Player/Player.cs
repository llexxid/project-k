using Cysharp.Threading.Tasks;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Core.Utils;
using System;
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

        if (!IsAlive)
        {   
            // 죽었을때
            return false;
        }

        return true;
    }

    public int ConnectDamage(IAttackable attacker)
    {
        int connect;

        if (TakeDamage(attacker))
        {
            connect = 0;
        }
        else
        {
            connect = 1;
        } 
        return connect;
    }

    public void Init(PlayerData data)
    {
        _data = data;
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

        playerOrder = new PlayerOrder();
        playerOrder.Init(this);
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

    // 공격 애니메이션을 재생하고, 애니메이션이 끝나면 onAnimationEnd를 호출
    public void PlayAttackAndApplyDamage(Action onAnimationEnd)
    {
        // 공격 상태로 전환하여 애니메이터에 신호를 보냄
        _playerAction = ePlayerAction.Attack;
        TurnOnAnimation(_playerAction);

        // 중복 코루틴 방지
        StopCoroutine(nameof(RunAttackCoroutine));
        StartCoroutine(RunAttackCoroutine(onAnimationEnd));
    }

    // 공격 애니메이션이 끝날 때까지 대기하는 코루틴
    private IEnumerator RunAttackCoroutine(Action onAnimationEnd)
    {
        // 애니메이터 컴포넌트가 없는 경우 바로 콜백 호출
        if (_am == null)
        {
            onAnimationEnd?.Invoke();
            yield break;
        }

        int attackHash = Animator.StringToHash("Attack");

        // 애니메이터가 Attack 상태로 진입할 때까지 대기 (프레임 단위)
        // 타임아웃을 넣지 않으면 잘못된 상태머신에서 무한루프 될 수 있음.
        float timeout = 2f; // 안전 타임아웃 (초)
        float timer = 0f;

        // 진입 대기: 상태가 Attack으로 바뀌기 전까지 기다림
        while (_am.GetCurrentAnimatorStateInfo(0).shortNameHash != attackHash && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 상태에 진입했으면 normalizedTime이 1 이상일 때까지 대기(한 사이클 완료)
        timer = 0f;
        while (_am.GetCurrentAnimatorStateInfo(0).shortNameHash == attackHash && _am.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 애니메이션 끝 시점에 콜백 호출
        onAnimationEnd?.Invoke();

        // 애니메이션 끝나면 Idle로 전환
        _playerAction = ePlayerAction.Idle;
        TurnOnAnimation(_playerAction);
    }
}
