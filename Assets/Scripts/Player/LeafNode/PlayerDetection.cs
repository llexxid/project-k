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

    // 카메라 경계 내부 판정용 여유(0~0.5). 0.05 = 화면 경계에서 5% 안쪽까지만 유효
    private const float CameraBoundsInset = 0.05f;

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

            // 기존 타겟이 카메라 밖으로 나가면 추격/공격을 멈춘다
            if (!IsInCameraBounds(player.currentTarget.targetPos))
            {
                player.ResetTarget(player.currentTarget);
                return false;
            }
            return true; // 다음 스텝
		}
		//CustomLogger.Log("Player가 탐지중입니다...");
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
		filter.useLayerMask = true;
		filter.useTriggers = true;

		// 리스트를 재사용하여 가비지 발생을 최소화하는 방식
		int count = Physics2D.OverlapCircle(player.transform.position, detectionRadius, filter, detectedResults);
        if (count == 0)
        {
            return false; //Node Failure
        }

        // 범위 내 가장 가까운 적을 타겟으로 선택
        currentTarget = null;
        Monster mon;
        float closestDist = float.MaxValue;

		for (int i = 0; i < count; i++)
        {
            if (!detectedResults[i].CompareTag("Enemy"))
            {
                continue;
            }

            mon = detectedResults[i].GetComponentInParent<Monster>();
            if (mon == null || mon.MonAction == eMonsterAction.Dead)
			{
                continue;
			}

			// 카메라 경계 밖의 적은 타겟 후보에서 제외
			if (!IsInCameraBounds(detectedResults[i].transform.position))
			    continue;

			float dist = Vector2.Distance(player.transform.position, detectedResults[i].transform.position);
			if (dist < closestDist)
            {
                closestDist = dist;
                currentTarget = detectedResults[i].GetComponentInParent<IDamageable>();
            }
        }

        //Debug.Log($"Player Current Target : {player.currentTarget.gameobj.GetInstanceID()}");
        if (currentTarget != null)
        {
            player.SetTarget(currentTarget);
			return true;
		}
        Debug.Log($"[PlayerDetection] {player.name} detect count={count} but no valid target");
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
