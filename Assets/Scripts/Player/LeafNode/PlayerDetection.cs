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
    public PlayerDetection(Player player)
    {
        this.player = player;
    }

    LayerMask enemyLayer = GameLayers.EnemyMask;

    public bool Detect()
    {
		if (player.currentTarget != null)
		{
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

        // 기존 타겟이 살아있고 범위 안에 있으면 그대로 유지 (겹침 시 타겟 흔들림 방지)
        /*
        if (currentTarget != null)
        {
            var currentMono = currentTarget as MonoBehaviour;
            if (currentMono != null && currentMono.gameObject.activeInHierarchy)
            {
                float distToCurrent = Vector2.Distance(
                    player.transform.position, currentMono.transform.position);
                if (distToCurrent <= detectionRadius)
                    return;
            }
        } */

        // 범위 내 가장 가까운 적을 타겟으로 선택
        IDamageable closest = null;
        Monster mon;
        float closestDist = float.MaxValue;

		Debug.Log($"Detect Count : {count}");
		for (int i = 0; i < count; i++)
        {
            if (!detectedResults[i].CompareTag("Enemy"))
            {
                continue;
            }

            mon = detectedResults[i].GetComponent<Monster>();
			if (mon.MonAction == eMonsterAction.Dead)
			{
                Debug.Log($"{i} | Player Detect DeadMonster : {mon.gameObject.GetInstanceID()}");
                continue;
			}

			float dist = Vector2.Distance(player.transform.position, detectedResults[i].transform.position);
			if (dist < closestDist)
            {
                closestDist = dist;
                closest = detectedResults[i].GetComponent<IDamageable>();
            }
        }

        //Debug.Log($"Player Current Target : {player.currentTarget.gameobj.GetInstanceID()}");
        if (closest != null)
        {
            player.SetTarget(closest);
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
