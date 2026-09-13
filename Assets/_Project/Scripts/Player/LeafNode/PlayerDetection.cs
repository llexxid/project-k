using Scripts.Core.inteface;
using Scripts.Core.Utils;
using Scripts.Monster;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;

public class PlayerDetection
{
    public float detectionRadius = 3.5f; // 모바일 화면 기준, stopDistance보다 커야 함
    private List<Collider2D> detectedResults = new List<Collider2D>();
    public Player player;
    public IDamageable currentTarget;
    public PlayerDetection(Player player)
    {
        this.player = player;
    }

    LayerMask enemyLayer = GameLayers.EnemyMask;

    public bool Detect()
    {
		if (player.currentTarget != null)
		{
            // 타겟이 이미 Dead 상태면 즉시 해제 → 스폰 위치 복귀 가능
            Monster currentMon = player.currentTarget.gameobj?.GetComponent<Monster>();
            if (currentMon == null || !currentMon.isActiveAndEnabled || currentMon.MonAction == eMonsterAction.Dead)
            {
                player.ResetTarget(player.currentTarget);
                return false;
            }

            // UI panels change the camera viewport, not combat eligibility.
            // Keep a live acquired target until death or release so ranged enemies
            // cannot attack from a camera-excluded strip and stall wave income.
            return true; // 다음 스텝
		}

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
		filter.useLayerMask = true;
		filter.useTriggers = true;

		int count = Physics2D.OverlapCircle(player.transform.position, detectionRadius, filter, detectedResults);
        if (count == 0) return false;

        currentTarget = null;
        float closestDist = float.MaxValue;

		for (int i = 0; i < count; i++)
        {
            if (!detectedResults[i].CompareTag("Enemy")) continue;

            var mon = detectedResults[i].GetComponentInParent<Monster>();
            if (mon == null || !mon.isActiveAndEnabled || mon.MonAction == eMonsterAction.Dead) continue;

            float dist = Vector2.Distance(player.transform.position, detectedResults[i].transform.position);

            // Physics range is the shared combat boundary on every screen ratio.
            if (dist < closestDist)
            {
                closestDist = dist;
                currentTarget = mon;
            }
        }

        if (currentTarget != null)
        {
            player.SetTarget(currentTarget);
			return true;
		}
        return false;
    }

    public class DetectionNode : Node
    {
        private PlayerDetection _detection;
        public DetectionNode(PlayerDetection detection) { _detection = detection; }

        public override NodeState Evaluate()
        {
            bool IsDetect = _detection.Detect();
            if (IsDetect)
            {
				return NodeState.Success;
			}   
            return NodeState.Failure;
        }
    }

}
