using Scripts.Core;
using Scripts.Core.inteface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour, IAttackable, IDamageable
{
    public Wallet wallet;
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

    // IAttackable 인터페이스 구현
    public int damage 
    {
        get 
        {
            return 10; 
        }
    }

    // IAttackable 인터페이스 구현
    public Vector3 targetPos
    {
        get
        {
            return transform.position;
        }
    }

    // IDamageable 인터페이스 구현
    public Vector3 attackerPos
    {
        get
        {
            return transform.position;
        }
    }

    // IDamageable 인터페이스 구현
    public bool TakeDamage(IAttackable attacker)
    {
        return true;
    }

    // 플레이어와 코인이 콜라이더 충돌 감지
    public void OnTriggerEnter(Collider other)
    {
        // 1. 충돌한 물체의 태그(String)를 eCurrency(Enum)로 변환 시도
        // 성공하면 true를 반환하고, 변환된 Enum 값은 'type' 변수에 담깁니다.
        if (System.Enum.TryParse(other.tag, out eCurrency type))
        {
            // 2. 해당 물체에서 Coin 컴포넌트(Value 값) 가져오기
            Coin coin = other.GetComponent<Coin>();

            if (coin != null)
            {
                // 3. 변환된 Enum 타입(type)과 코인의 값(Value)을 지갑에 전달
                wallet.AddCoins(type, coin.Value);

                // 코인 획득 후 비활성화
                other.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.Log("변환 실패");
        }
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

        // 임시 키 입력으로 플레이어 이동 (WASD)
        // - 삭제 예정
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(Vector3.forward * 2 * Time.deltaTime);
        }
        if(Input.GetKey(KeyCode.S))
        {
            transform.Translate(Vector3.back * 2 * Time.deltaTime);
        }
        if(Input.GetKey(KeyCode.A))
        {
            transform.Translate(Vector3.left * 2 * Time.deltaTime);
        }
        if(Input.GetKey(KeyCode.D))
        {
            transform.Translate(Vector3.right * 2 * Time.deltaTime);
        }
    }

    // LateUpdate에서 애니메이션 업데이트 호출 (Update에서 행동 트리 평가 후 애니메이션 상태 변경)
    private void LateUpdate()
    {
        UpdateAnimation();
    }
}
