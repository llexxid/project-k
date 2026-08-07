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

    // 카메라 경계 내부 판정용 여유(0~0.5). 0.02 = 화면 경계에서 2% 안쪽까지만 유효
    private const float CameraBoundsInset = 0.02f;

    private static bool IsInCameraBounds(Vector3 worldPos)
    {
        var cam = Camera.main;
        if (cam == null) return true;
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z < 0f) return false;
        return vp.x >= CameraBoundsInset && vp.x <= 1f - CameraBoundsInset
            && vp.y >= CameraBoundsInset && vp.y <= 1f - CameraBoundsInset;
    }

    public bool Detect()
    {
		if (player.currentTarget != null)
		{
            // 타겟이 이미 Dead 상태면 즉시 해제 → 스폰 위치 복귀 가능
            Monster currentMon = player.currentTarget.gameobj?.GetComponent<Monster>();
            if (currentMon != null && currentMon.MonAction == eMonsterAction.Dead)
            {
                player.ResetTarget(player.currentTarget);
                return false;
            }

            // [개선] 기존 타겟이 카메라 밖으로 나가더라도, 플레이어와 매우 가깝다면(2.0f) 추격을 유지
            float distToCurrent = Vector2.Distance(player.transform.position, player.currentTarget.targetPos);
            if (!IsInCameraBounds(player.currentTarget.targetPos) && distToCurrent > 2.0f)
            {
                player.ResetTarget(player.currentTarget);
                return false;
            }
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
            if (mon == null || mon.MonAction == eMonsterAction.Dead) continue;

            float dist = Vector2.Distance(player.transform.position, detectedResults[i].transform.position);

			// [핵심 개선] 카메라 안에 있거나, 카메라 밖이라도 플레이어와 매우 가깝다면(2.0f) 탐지 허용
			if (IsInCameraBounds(detectedResults[i].transform.position) || dist <= 2.0f)
            {
                if (dist < closestDist)
                {
                    closestDist = dist;
                    currentTarget = detectedResults[i].GetComponentInParent<IDamageable>();
                }
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
