using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDetection : MonoBehaviour
{
    public LayerMask playerMask;
    public float detectionRadius;
    private List<Collider2D> detectedResults = new List<Collider2D>();
    public Transform currentTarget; // 발견된 적을 저장할 변수

    public void Detect()
    {
        ContactFilter2D filter = new ContactFilter2D(); // 필터 설정
        filter.SetLayerMask(playerMask); // 레이어 마스크 설정
        filter.useTriggers = true; // 트리거 콜라이더 포함

        // 리스트를 재사용하여 가비지 발생을 최소화하는 방식
        int count = Physics2D.OverlapCircle(transform.position, detectionRadius, filter, detectedResults);

        for (int i = 0; i < count; i++)
        {
            if (detectedResults[i].CompareTag("Enemy"))
            {
                currentTarget = detectedResults[i].transform; // 타겟 저장
                return; // 가장 가까운 적 하나만 찾으면 종료
            }
        }
    }

    // 행동 트리 전용 노드 클래스
    public class DetectionNode : Node
    {
        private PlayerDetection _detection;
        public DetectionNode(PlayerDetection detection) { _detection = detection; }

        public override NodeState Evaluate()
        {
            _detection.Detect();

            // 리스트 내부에 실제로 "Enemy" 태그를 가진 녀석이 있는지 확인
            foreach (var target in _detection.detectedResults)
            {
                if (target.CompareTag("Enemy")) return NodeState.Success;
            }
            return NodeState.Failure;
        }
    }

    void OnDrawGizmos() // 범위 그리기
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}

/*
동작 원리

1. Sequence 노드 (PlayerOrder에 있는)가 먼저 DetectionNode를 실행합니다.

2. 감지에 성공하면 PlayerDetection.currentTarget에 적 정보가 저장되고 Success가 반환됩니다.

3. 이어서 MoveNode가 실행됩니다.

4. 적이 멀리 있다면 PlayerMove는 이동하며 Running을 반환합니다. (트리는 다음 프레임에 다시 MoveNode를 실행

5. 적에게 가까워지면 Success를 반환하여, 다음 순서인 공격(Attack) 단계로 넘어갑니다.
*/
