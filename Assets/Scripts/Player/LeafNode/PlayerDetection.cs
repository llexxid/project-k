using Scripts.Core.inteface;
using Scripts.Core.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDetection
{
    public float detectionRadius = 3.5f; // 모바일 화면 기준, stopDistance보다 커야 함
    private List<Collider2D> detectedResults = new List<Collider2D>();
    public IDamageable currentTarget;
    public Player player;
    public PlayerDetection(Player player)
    {
        this.player = player;
    }

    LayerMask enemyLayer = GameLayers.EnemyMask;

    public void Detect()
    {
        CustomLogger.Log("Player가 탐지중입니다...");
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useTriggers = true;

        // 리스트를 재사용하여 가비지 발생을 최소화하는 방식
        int count = Physics2D.OverlapCircle(player.transform.position, detectionRadius, filter, detectedResults);

        for (int i = 0; i < count; i++)
        {
            if (detectedResults[i].CompareTag("Enemy"))
            {
                currentTarget = detectedResults[i].gameObject.GetComponent<IDamageable>(); // 타겟 저장

                player.currentTarget  = currentTarget; // 플레이어의 타겟 변수에도 저장

                return; // 가장 가까운 적 하나만 찾으면 종료
            }
            else Debug.Log("적이 감지되지 않음");
        }
    }

    public class DetectionNode : Node
    {
        private PlayerDetection _detection;
        public DetectionNode(PlayerDetection detection) { _detection = detection; }

        public override NodeState Evaluate()
        {
            _detection.Detect();
            foreach (var target in _detection.detectedResults)
            {
                if (target.CompareTag("Enemy")) return NodeState.Success;
                else Debug.Log("적이 감지되지 않음");
            }
            return NodeState.Failure;
        }
    }

}
